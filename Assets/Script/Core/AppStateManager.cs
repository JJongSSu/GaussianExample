// SPDX-License-Identifier: MIT

using System;
using UnityEngine;

/// <summary>
/// Central state machine for the bridge inspection AR app.
/// States: Loading → Localizing → Inspecting → DetailView
/// </summary>
public class AppStateManager : MonoBehaviour
{
    public enum AppState
    {
        Loading,
        Localizing,
        Inspecting,
        DetailView
    }

    [Header("References")]
    public ImageLocalizationManager localizationManager;
    public AutoDetectionManager detectionManager;
    public InspectionUIManager uiManager;
    public MobileGaussianOptimizer gaussianOptimizer;

    [Header("State")]
    [SerializeField] private AppState m_CurrentState = AppState.Loading;

    public AppState currentState => m_CurrentState;

    public event Action<AppState, AppState> OnStateChanged;

    void Start()
    {
        EnterState(AppState.Loading);
    }

    void OnEnable()
    {
        if (localizationManager != null)
            localizationManager.OnLocalizationSucceeded += HandleLocalizationSucceeded;
        if (detectionManager != null)
            detectionManager.OnDetectionTapped += HandleDetectionTapped;
        if (uiManager != null)
            uiManager.OnPopupClosed += HandlePopupClosed;
    }

    void OnDisable()
    {
        if (localizationManager != null)
            localizationManager.OnLocalizationSucceeded -= HandleLocalizationSucceeded;
        if (detectionManager != null)
            detectionManager.OnDetectionTapped -= HandleDetectionTapped;
        if (uiManager != null)
            uiManager.OnPopupClosed -= HandlePopupClosed;
    }

    public void TransitionTo(AppState newState)
    {
        if (m_CurrentState == newState) return;

        var oldState = m_CurrentState;
        ExitState(m_CurrentState);
        m_CurrentState = newState;
        EnterState(newState);
        OnStateChanged?.Invoke(oldState, newState);

        Debug.Log($"[AppState] {oldState} → {newState}");
    }

    void EnterState(AppState state)
    {
        switch (state)
        {
            case AppState.Loading:
                EnterLoading();
                break;
            case AppState.Localizing:
                EnterLocalizing();
                break;
            case AppState.Inspecting:
                EnterInspecting();
                break;
            case AppState.DetailView:
                EnterDetailView();
                break;
        }
    }

    void ExitState(AppState state)
    {
        switch (state)
        {
            case AppState.Loading:
                break;
            case AppState.Localizing:
                ExitLocalizing();
                break;
            case AppState.Inspecting:
                ExitInspecting();
                break;
            case AppState.DetailView:
                ExitDetailView();
                break;
        }
    }

    // --- Loading ---

    void EnterLoading()
    {
        if (uiManager != null)
            uiManager.ShowStatus("Loading...", 0f);

        // Check GPU compatibility
        if (gaussianOptimizer != null && !gaussianOptimizer.CheckGpuCompatibility())
        {
            if (uiManager != null)
                uiManager.ShowStatus("GPU not supported. Vulkan + Compute Shader required.", -1f);
            Debug.LogError("[AppState] GPU compatibility check failed.");
            return;
        }

        // Once everything is loaded, transition to Localizing
        // In a real scenario, wait for assets to load; here we proceed next frame.
        Invoke(nameof(FinishLoading), 0.5f);
    }

    void FinishLoading()
    {
        TransitionTo(AppState.Localizing);
    }

    // --- Localizing ---

    void EnterLocalizing()
    {
        if (uiManager != null)
            uiManager.ShowStatus("Localizing... Point camera at the bridge.", -1f);

        if (localizationManager != null)
        {
            localizationManager.enabled = true;
            localizationManager.StartLocalization();
        }

        if (detectionManager != null)
            detectionManager.enabled = false;
    }

    void ExitLocalizing()
    {
        if (localizationManager != null)
            localizationManager.StopLocalization();
    }

    // --- Inspecting ---

    void EnterInspecting()
    {
        if (uiManager != null)
            uiManager.ShowStatus("Inspecting", -1f);

        if (detectionManager != null)
        {
            detectionManager.enabled = true;
            detectionManager.StartAutoDetection();
        }
    }

    void ExitInspecting()
    {
        if (detectionManager != null)
            detectionManager.StopAutoDetection();
    }

    // --- DetailView ---

    void EnterDetailView()
    {
        if (detectionManager != null)
            detectionManager.PauseAutoDetection();
    }

    void ExitDetailView()
    {
        if (detectionManager != null)
            detectionManager.ResumeAutoDetection();
    }

    // --- Event Handlers ---

    void HandleLocalizationSucceeded()
    {
        if (m_CurrentState == AppState.Localizing)
            TransitionTo(AppState.Inspecting);
    }

    void HandleDetectionTapped(DetectionTapData tapData)
    {
        if (m_CurrentState == AppState.Inspecting)
        {
            if (uiManager != null)
                uiManager.ShowDetailPopup(tapData);
            TransitionTo(AppState.DetailView);
        }
    }

    void HandlePopupClosed()
    {
        if (m_CurrentState == AppState.DetailView)
            TransitionTo(AppState.Inspecting);
    }

    // --- Public API ---

    public void RequestRelocalization()
    {
        if (m_CurrentState == AppState.Inspecting || m_CurrentState == AppState.DetailView)
        {
            if (uiManager != null)
                uiManager.CloseDetailPopup();
            TransitionTo(AppState.Localizing);
        }
    }
}

/// <summary>
/// Data passed when a detection bounding box is tapped.
/// </summary>
[Serializable]
public class DetectionTapData
{
    public Texture2D screenshot;
    public Texture2D croppedImage;
    public string label;
    public float confidence;
    public Rect normalizedBox;
    public string timestamp;
}
