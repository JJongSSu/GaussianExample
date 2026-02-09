// SPDX-License-Identifier: MIT

using System;
using UnityEngine;
using GaussianSplatting.Runtime;

/// <summary>
/// Dynamically adjusts GaussianSplatRenderer quality parameters for mobile performance.
/// Monitors FPS and auto-downgrades/upgrades settings to maintain target framerate.
/// </summary>
public class MobileGaussianOptimizer : MonoBehaviour
{
    [Header("References")]
    public GaussianSplatRenderer splatRenderer;

    [Header("Target")]
    [Tooltip("Target framerate to maintain")]
    public int targetFPS = 30;

    [Header("Quality Presets")]
    public QualityLevel currentQuality = QualityLevel.Medium;

    [Header("Auto-Adaptation")]
    public bool enableAutoAdaptation = true;
    [Tooltip("Seconds between FPS checks")]
    public float adaptationInterval = 2f;

    [Header("Mobile Defaults")]
    [Range(0, 3)] public int mobileSHOrder = 0;
    [Range(1, 30)] public int mobileSortNthFrame = 4;
    [Range(0.1f, 2.0f)] public float mobileSplatScale = 1.0f;

    public enum QualityLevel
    {
        Low,
        Medium,
        High
    }

    // Quality parameter sets
    static readonly QualityParams[] k_QualityPresets = new QualityParams[]
    {
        // Low: maximum performance
        new QualityParams { shOrder = 0, sortNthFrame = 8, splatScale = 0.7f },
        // Medium: balanced
        new QualityParams { shOrder = 0, sortNthFrame = 4, splatScale = 0.85f },
        // High: best visuals on capable devices
        new QualityParams { shOrder = 1, sortNthFrame = 2, splatScale = 1.0f },
    };

    struct QualityParams
    {
        public int shOrder;
        public int sortNthFrame;
        public float splatScale;
    }

    float m_AdaptationTimer;
    int m_FrameCount;
    float m_FpsAccumulator;
    float m_CurrentFPS;
    bool m_GpuCompatible = true;

    void Start()
    {
        Application.targetFrameRate = targetFPS;

        if (splatRenderer == null)
            splatRenderer = FindAnyObjectByType<GaussianSplatRenderer>();

        ApplyQuality(currentQuality);
    }

    void Update()
    {
        if (!enableAutoAdaptation) return;

        m_FrameCount++;
        m_FpsAccumulator += Time.unscaledDeltaTime;
        m_AdaptationTimer += Time.unscaledDeltaTime;

        if (m_AdaptationTimer >= adaptationInterval)
        {
            m_CurrentFPS = m_FrameCount / m_FpsAccumulator;
            m_FrameCount = 0;
            m_FpsAccumulator = 0f;
            m_AdaptationTimer = 0f;

            AdaptQuality();
        }
    }

    void AdaptQuality()
    {
        if (m_CurrentFPS < targetFPS * 0.8f) // Below 80% of target
        {
            if (currentQuality > QualityLevel.Low)
            {
                currentQuality--;
                ApplyQuality(currentQuality);
                Debug.Log($"[MobileOptimizer] FPS={m_CurrentFPS:F0}, downgrading to {currentQuality}");
            }
        }
        else if (m_CurrentFPS > targetFPS * 1.2f) // Above 120% of target
        {
            if (currentQuality < QualityLevel.High)
            {
                currentQuality++;
                ApplyQuality(currentQuality);
                Debug.Log($"[MobileOptimizer] FPS={m_CurrentFPS:F0}, upgrading to {currentQuality}");
            }
        }
    }

    public void ApplyQuality(QualityLevel level)
    {
        currentQuality = level;
        var preset = k_QualityPresets[(int)level];

        if (splatRenderer != null)
        {
            splatRenderer.m_SHOrder = preset.shOrder;
            splatRenderer.m_SortNthFrame = preset.sortNthFrame;
            splatRenderer.m_SplatScale = preset.splatScale;
        }
    }

    public void ApplyMobileDefaults()
    {
        if (splatRenderer != null)
        {
            splatRenderer.m_SHOrder = mobileSHOrder;
            splatRenderer.m_SortNthFrame = mobileSortNthFrame;
            splatRenderer.m_SplatScale = mobileSplatScale;
        }
    }

    /// <summary>
    /// Checks if the current GPU supports requirements for Gaussian Splatting.
    /// Returns true if compatible, false otherwise.
    /// </summary>
    public bool CheckGpuCompatibility()
    {
        bool compatible = true;
        string issues = "";

        // Check Vulkan (on Android)
        if (Application.platform == RuntimePlatform.Android)
        {
            if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Vulkan)
            {
                issues += "Vulkan required (current: " + SystemInfo.graphicsDeviceType + "). ";
                compatible = false;
            }
        }

        // Check compute shader support
        if (!SystemInfo.supportsComputeShaders)
        {
            issues += "Compute shaders not supported. ";
            compatible = false;
        }

        // Check GPU compute buffer support
        if (SystemInfo.maxComputeBufferInputsVertex < 1)
        {
            issues += "Compute buffer vertex inputs not supported. ";
            compatible = false;
        }

        // Log GPU info
        Debug.Log($"[MobileOptimizer] GPU: {SystemInfo.graphicsDeviceName}, " +
                  $"API: {SystemInfo.graphicsDeviceType}, " +
                  $"Compute: {SystemInfo.supportsComputeShaders}, " +
                  $"MaxTexSize: {SystemInfo.maxTextureSize}");

        if (!compatible)
        {
            Debug.LogError($"[MobileOptimizer] GPU compatibility issues: {issues}");
        }

        m_GpuCompatible = compatible;
        return compatible;
    }

    public float GetCurrentFPS() => m_CurrentFPS;
    public bool IsGpuCompatible() => m_GpuCompatible;
}
