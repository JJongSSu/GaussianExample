// SPDX-License-Identifier: MIT

using System;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Manages all inspection UI: status bar, detection overlay info, detail popup, and settings panel.
/// Creates UI elements programmatically under a Screen Space - Overlay canvas.
/// </summary>
public class InspectionUIManager : MonoBehaviour
{
    [Header("References")]
    public AppStateManager appStateManager;
    public AutoDetectionManager detectionManager;
    public MobileGaussianOptimizer gaussianOptimizer;
    public ImageLocalizationManager localizationManager;

    [Header("Colors")]
    public Color panelBackground = new Color(0f, 0f, 0f, 0.7f);
    public Color accentColor = new Color(0.2f, 0.6f, 1f, 1f);
    public Color warningColor = new Color(1f, 0.4f, 0.2f, 1f);

    public event Action OnPopupClosed;

    // UI elements
    Canvas m_Canvas;
    GameObject m_StatusPanel;
    Text m_StatusText;
    GameObject m_BottomBar;
    Text m_BottomText;
    GameObject m_DetailPopup;
    RawImage m_PopupImage;
    Text m_PopupInfoText;
    GameObject m_SettingsPanel;
    Button m_RelocBtn;
    Button m_SettingsBtn;
    Button m_ManualAlignBtn;
    Button m_ClosePopupBtn;
    Button m_SaveBtn;
    Button m_CloseSettingsBtn;

    // Settings sliders
    Slider m_SHOrderSlider;
    Slider m_SortFreqSlider;
    Slider m_DetectIntervalSlider;
    Text m_SettingsInfoText;

    DetectionTapData m_CurrentTapData;

    void Awake()
    {
        CreateUI();
    }

    void Update()
    {
        UpdateBottomBar();
        UpdateSettingsInfo();
    }

    // --- Public API ---

    public void ShowStatus(string message, float progress)
    {
        if (m_StatusText != null)
            m_StatusText.text = message;

        if (m_StatusPanel != null)
            m_StatusPanel.SetActive(true);
    }

    public void ShowDetailPopup(DetectionTapData tapData)
    {
        m_CurrentTapData = tapData;

        if (m_DetailPopup == null) return;
        m_DetailPopup.SetActive(true);

        if (m_PopupImage != null && tapData.croppedImage != null)
            m_PopupImage.texture = tapData.croppedImage;

        if (m_PopupInfoText != null)
        {
            m_PopupInfoText.text =
                $"Defect: {tapData.label}\n" +
                $"Confidence: {(tapData.confidence * 100):F0}%\n" +
                $"Time: {tapData.timestamp}\n" +
                $"Box: ({tapData.normalizedBox.x:F2}, {tapData.normalizedBox.y:F2}, " +
                $"{tapData.normalizedBox.width:F2}, {tapData.normalizedBox.height:F2})";
        }
    }

    public void CloseDetailPopup()
    {
        if (m_DetailPopup != null)
            m_DetailPopup.SetActive(false);

        // Clean up tap data textures
        if (m_CurrentTapData != null)
        {
            if (m_CurrentTapData.screenshot != null) Destroy(m_CurrentTapData.screenshot);
            if (m_CurrentTapData.croppedImage != null) Destroy(m_CurrentTapData.croppedImage);
            m_CurrentTapData = null;
        }

        OnPopupClosed?.Invoke();
    }

    // --- Bottom Bar ---

    void UpdateBottomBar()
    {
        if (m_BottomText == null || detectionManager == null) return;

        float timeSince = detectionManager.timeSinceLastScan;
        int count = detectionManager.detectionCount;
        string timeStr = timeSince < 60f ? $"{timeSince:F0}s ago" : "N/A";

        m_BottomText.text = $"Detections: {count} | Last scan: {timeStr}";

        if (gaussianOptimizer != null)
        {
            float fps = gaussianOptimizer.GetCurrentFPS();
            if (fps > 0)
                m_BottomText.text += $" | FPS: {fps:F0}";
        }
    }

    void UpdateSettingsInfo()
    {
        if (m_SettingsInfoText == null || !m_SettingsPanel.activeSelf) return;

        string info = "";
        if (gaussianOptimizer != null)
        {
            info += $"Quality: {gaussianOptimizer.currentQuality}\n";
            info += $"FPS: {gaussianOptimizer.GetCurrentFPS():F0}\n";
            info += $"GPU: {SystemInfo.graphicsDeviceName}\n";
        }
        m_SettingsInfoText.text = info;
    }

    // --- Save Screenshot ---

    void SaveScreenshotToGallery()
    {
        if (m_CurrentTapData == null || m_CurrentTapData.screenshot == null)
        {
            Debug.LogWarning("[UI] No screenshot to save.");
            return;
        }

        string filename = $"BridgeInspection_{DateTime.Now:yyyyMMdd_HHmmss}.png";
        string path = Path.Combine(Application.persistentDataPath, filename);

        byte[] pngData = m_CurrentTapData.screenshot.EncodeToPNG();
        File.WriteAllBytes(path, pngData);

        Debug.Log($"[UI] Screenshot saved: {path}");

        // On Android, trigger media scan so it appears in gallery
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var intent = new AndroidJavaObject("android.content.Intent",
                "android.intent.action.MEDIA_SCANNER_SCAN_FILE"))
            using (var uri = new AndroidJavaClass("android.net.Uri")
                .CallStatic<AndroidJavaObject>("parse", "file://" + path))
            {
                intent.Call<AndroidJavaObject>("setData", uri);
                activity.Call("sendBroadcast", intent);
            }
            Debug.Log("[UI] Media scan broadcast sent.");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[UI] Media scan failed: {e.Message}");
        }
#endif
    }

    // --- UI Construction ---

    void CreateUI()
    {
        // Canvas
        var canvasGO = new GameObject("InspectionCanvas");
        canvasGO.transform.SetParent(transform);
        m_Canvas = canvasGO.AddComponent<Canvas>();
        m_Canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        m_Canvas.sortingOrder = 100;
        canvasGO.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasGO.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1080, 1920);
        canvasGO.AddComponent<GraphicRaycaster>();

        CreateStatusPanel(canvasGO.transform);
        CreateBottomBar(canvasGO.transform);
        CreateDetailPopup(canvasGO.transform);
        CreateSettingsPanel(canvasGO.transform);
    }

    void CreateStatusPanel(Transform parent)
    {
        m_StatusPanel = CreatePanel(parent, "StatusPanel",
            new Vector2(0, 1), new Vector2(1, 1),
            new Vector2(0, -80), new Vector2(0, 0));

        m_StatusText = CreateText(m_StatusPanel.transform, "StatusText", "Initializing...", 20);
        m_StatusText.alignment = TextAnchor.MiddleCenter;
        SetAnchors(m_StatusText.gameObject, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        // Buttons container (right side of status bar)
        m_RelocBtn = CreateButton(m_StatusPanel.transform, "RelocBtn", "Relocalize",
            new Vector2(1, 0.5f), new Vector2(1, 0.5f),
            new Vector2(-180, 0), new Vector2(100, 36));
        m_RelocBtn.onClick.AddListener(() =>
        {
            if (appStateManager != null)
                appStateManager.RequestRelocalization();
        });

        m_SettingsBtn = CreateButton(m_StatusPanel.transform, "SettingsBtn", "Settings",
            new Vector2(1, 0.5f), new Vector2(1, 0.5f),
            new Vector2(-70, 0), new Vector2(80, 36));
        m_SettingsBtn.onClick.AddListener(() =>
        {
            if (m_SettingsPanel != null)
                m_SettingsPanel.SetActive(!m_SettingsPanel.activeSelf);
        });

        m_ManualAlignBtn = CreateButton(m_StatusPanel.transform, "ManualBtn", "Manual",
            new Vector2(0, 0.5f), new Vector2(0, 0.5f),
            new Vector2(70, 0), new Vector2(80, 36));
        m_ManualAlignBtn.onClick.AddListener(() =>
        {
            if (localizationManager != null)
                localizationManager.EnableManualAlignment();
        });
    }

    void CreateBottomBar(Transform parent)
    {
        m_BottomBar = CreatePanel(parent, "BottomBar",
            new Vector2(0, 0), new Vector2(1, 0),
            new Vector2(0, 0), new Vector2(0, 50));

        m_BottomText = CreateText(m_BottomBar.transform, "BottomText", "Detections: 0 | Last scan: N/A", 16);
        m_BottomText.alignment = TextAnchor.MiddleCenter;
        SetAnchors(m_BottomText.gameObject, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
    }

    void CreateDetailPopup(Transform parent)
    {
        m_DetailPopup = CreatePanel(parent, "DetailPopup",
            new Vector2(0.1f, 0.15f), new Vector2(0.9f, 0.85f),
            Vector2.zero, Vector2.zero);
        m_DetailPopup.SetActive(false);

        // Popup background - darker
        var bg = m_DetailPopup.GetComponent<Image>();
        bg.color = new Color(0.1f, 0.1f, 0.15f, 0.95f);

        // Close button
        m_ClosePopupBtn = CreateButton(m_DetailPopup.transform, "CloseBtn", "X",
            new Vector2(1, 1), new Vector2(1, 1),
            new Vector2(-30, -30), new Vector2(40, 40));
        m_ClosePopupBtn.onClick.AddListener(CloseDetailPopup);

        // Image display
        var imgGO = new GameObject("PopupImage");
        imgGO.transform.SetParent(m_DetailPopup.transform, false);
        m_PopupImage = imgGO.AddComponent<RawImage>();
        var imgRT = imgGO.GetComponent<RectTransform>();
        imgRT.anchorMin = new Vector2(0.05f, 0.4f);
        imgRT.anchorMax = new Vector2(0.5f, 0.9f);
        imgRT.offsetMin = Vector2.zero;
        imgRT.offsetMax = Vector2.zero;

        // Info text
        m_PopupInfoText = CreateText(m_DetailPopup.transform, "InfoText", "", 18);
        var infoRT = m_PopupInfoText.GetComponent<RectTransform>();
        infoRT.anchorMin = new Vector2(0.52f, 0.4f);
        infoRT.anchorMax = new Vector2(0.95f, 0.9f);
        infoRT.offsetMin = Vector2.zero;
        infoRT.offsetMax = Vector2.zero;
        m_PopupInfoText.alignment = TextAnchor.UpperLeft;

        // Save button
        m_SaveBtn = CreateButton(m_DetailPopup.transform, "SaveBtn", "Save to Gallery",
            new Vector2(0.25f, 0), new Vector2(0.25f, 0),
            new Vector2(0, 60), new Vector2(160, 50));
        m_SaveBtn.onClick.AddListener(SaveScreenshotToGallery);
    }

    void CreateSettingsPanel(Transform parent)
    {
        m_SettingsPanel = CreatePanel(parent, "SettingsPanel",
            new Vector2(0.15f, 0.2f), new Vector2(0.85f, 0.8f),
            Vector2.zero, Vector2.zero);
        m_SettingsPanel.SetActive(false);

        var bg = m_SettingsPanel.GetComponent<Image>();
        bg.color = new Color(0.1f, 0.1f, 0.15f, 0.95f);

        // Title
        var title = CreateText(m_SettingsPanel.transform, "Title", "Settings", 24);
        title.alignment = TextAnchor.MiddleCenter;
        title.fontStyle = FontStyle.Bold;
        var titleRT = title.GetComponent<RectTransform>();
        titleRT.anchorMin = new Vector2(0, 0.9f);
        titleRT.anchorMax = new Vector2(1, 1);
        titleRT.offsetMin = Vector2.zero;
        titleRT.offsetMax = Vector2.zero;

        // Close button
        m_CloseSettingsBtn = CreateButton(m_SettingsPanel.transform, "CloseSettingsBtn", "X",
            new Vector2(1, 1), new Vector2(1, 1),
            new Vector2(-30, -30), new Vector2(40, 40));
        m_CloseSettingsBtn.onClick.AddListener(() => m_SettingsPanel.SetActive(false));

        // SH Order slider
        float yPos = 0.78f;
        CreateSliderWithLabel(m_SettingsPanel.transform, "SH Order", 0, 3, 0, yPos,
            (val) =>
            {
                if (gaussianOptimizer != null && gaussianOptimizer.splatRenderer != null)
                    gaussianOptimizer.splatRenderer.m_SHOrder = (int)val;
            }, out m_SHOrderSlider);

        // Sort frequency slider
        yPos -= 0.15f;
        CreateSliderWithLabel(m_SettingsPanel.transform, "Sort Every N Frames", 1, 16, 4, yPos,
            (val) =>
            {
                if (gaussianOptimizer != null && gaussianOptimizer.splatRenderer != null)
                    gaussianOptimizer.splatRenderer.m_SortNthFrame = (int)val;
            }, out m_SortFreqSlider);

        // Detection interval slider
        yPos -= 0.15f;
        CreateSliderWithLabel(m_SettingsPanel.transform, "Detection Interval (s)", 1, 10, 3, yPos,
            (val) =>
            {
                if (detectionManager != null)
                    detectionManager.detectionInterval = val;
            }, out m_DetectIntervalSlider);

        // Info text
        m_SettingsInfoText = CreateText(m_SettingsPanel.transform, "SettingsInfo", "", 14);
        var infoRT = m_SettingsInfoText.GetComponent<RectTransform>();
        infoRT.anchorMin = new Vector2(0.05f, 0.05f);
        infoRT.anchorMax = new Vector2(0.95f, 0.35f);
        infoRT.offsetMin = Vector2.zero;
        infoRT.offsetMax = Vector2.zero;
        m_SettingsInfoText.alignment = TextAnchor.UpperLeft;
    }

    // --- UI Helpers ---

    GameObject CreatePanel(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax,
        Vector2 offsetMin, Vector2 offsetMax)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = panelBackground;
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;
        return go;
    }

    Text CreateText(Transform parent, string name, string text, int fontSize)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var txt = go.AddComponent<Text>();
        txt.text = text;
        txt.fontSize = fontSize;
        txt.color = Color.white;
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(10, 0);
        rt.offsetMax = new Vector2(-10, 0);
        return txt;
    }

    Button CreateButton(Transform parent, string name, string label,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, Vector2 size)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = accentColor;
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;

        var txtGO = new GameObject("Text");
        txtGO.transform.SetParent(go.transform, false);
        var txt = txtGO.AddComponent<Text>();
        txt.text = label;
        txt.fontSize = 14;
        txt.color = Color.white;
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.alignment = TextAnchor.MiddleCenter;
        var txtRT = txtGO.GetComponent<RectTransform>();
        txtRT.anchorMin = Vector2.zero;
        txtRT.anchorMax = Vector2.one;
        txtRT.offsetMin = Vector2.zero;
        txtRT.offsetMax = Vector2.zero;

        return btn;
    }

    void CreateSliderWithLabel(Transform parent, string label, float min, float max, float defaultVal,
        float yAnchor, Action<float> onChange, out Slider slider)
    {
        // Label
        var labelText = CreateText(parent, label + "Label", label, 16);
        var labelRT = labelText.GetComponent<RectTransform>();
        labelRT.anchorMin = new Vector2(0.05f, yAnchor);
        labelRT.anchorMax = new Vector2(0.45f, yAnchor + 0.08f);
        labelRT.offsetMin = Vector2.zero;
        labelRT.offsetMax = Vector2.zero;
        labelText.alignment = TextAnchor.MiddleLeft;

        // Slider
        var sliderGO = new GameObject(label + "Slider");
        sliderGO.transform.SetParent(parent, false);
        var sliderRT = sliderGO.AddComponent<RectTransform>();
        sliderRT.anchorMin = new Vector2(0.48f, yAnchor + 0.01f);
        sliderRT.anchorMax = new Vector2(0.85f, yAnchor + 0.07f);
        sliderRT.offsetMin = Vector2.zero;
        sliderRT.offsetMax = Vector2.zero;

        // Slider background
        var bgGO = new GameObject("Background");
        bgGO.transform.SetParent(sliderGO.transform, false);
        var bgImg = bgGO.AddComponent<Image>();
        bgImg.color = new Color(0.3f, 0.3f, 0.3f, 1f);
        var bgRT = bgGO.GetComponent<RectTransform>();
        bgRT.anchorMin = Vector2.zero;
        bgRT.anchorMax = Vector2.one;
        bgRT.offsetMin = Vector2.zero;
        bgRT.offsetMax = Vector2.zero;

        // Fill area
        var fillAreaGO = new GameObject("Fill Area");
        fillAreaGO.transform.SetParent(sliderGO.transform, false);
        var fillAreaRT = fillAreaGO.AddComponent<RectTransform>();
        fillAreaRT.anchorMin = Vector2.zero;
        fillAreaRT.anchorMax = Vector2.one;
        fillAreaRT.offsetMin = Vector2.zero;
        fillAreaRT.offsetMax = Vector2.zero;

        var fillGO = new GameObject("Fill");
        fillGO.transform.SetParent(fillAreaGO.transform, false);
        var fillImg = fillGO.AddComponent<Image>();
        fillImg.color = accentColor;
        var fillRT = fillGO.GetComponent<RectTransform>();
        fillRT.anchorMin = Vector2.zero;
        fillRT.anchorMax = Vector2.one;
        fillRT.offsetMin = Vector2.zero;
        fillRT.offsetMax = Vector2.zero;

        // Handle
        var handleAreaGO = new GameObject("Handle Slide Area");
        handleAreaGO.transform.SetParent(sliderGO.transform, false);
        var handleAreaRT = handleAreaGO.AddComponent<RectTransform>();
        handleAreaRT.anchorMin = Vector2.zero;
        handleAreaRT.anchorMax = Vector2.one;
        handleAreaRT.offsetMin = Vector2.zero;
        handleAreaRT.offsetMax = Vector2.zero;

        var handleGO = new GameObject("Handle");
        handleGO.transform.SetParent(handleAreaGO.transform, false);
        var handleImg = handleGO.AddComponent<Image>();
        handleImg.color = Color.white;
        var handleRT = handleGO.GetComponent<RectTransform>();
        handleRT.sizeDelta = new Vector2(20, 0);
        handleRT.anchorMin = new Vector2(0, 0);
        handleRT.anchorMax = new Vector2(0, 1);

        slider = sliderGO.AddComponent<Slider>();
        slider.fillRect = fillRT;
        slider.handleRect = handleRT;
        slider.minValue = min;
        slider.maxValue = max;
        slider.wholeNumbers = true;
        slider.value = defaultVal;
        slider.onValueChanged.AddListener((v) => onChange(v));

        // Value text
        var valueText = CreateText(parent, label + "Value", defaultVal.ToString("F0"), 16);
        var valueRT = valueText.GetComponent<RectTransform>();
        valueRT.anchorMin = new Vector2(0.87f, yAnchor);
        valueRT.anchorMax = new Vector2(0.95f, yAnchor + 0.08f);
        valueRT.offsetMin = Vector2.zero;
        valueRT.offsetMax = Vector2.zero;
        valueText.alignment = TextAnchor.MiddleCenter;

        slider.onValueChanged.AddListener((v) => valueText.text = v.ToString("F0"));
    }

    void SetAnchors(GameObject go, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        var rt = go.GetComponent<RectTransform>();
        if (rt == null) return;
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;
    }
}
