// SPDX-License-Identifier: MIT

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Periodically runs YOLO detection on the screen and manages bounding box overlays.
/// Handles touch-to-detection interaction with screen capture popup support.
/// </summary>
public class AutoDetectionManager : MonoBehaviour
{
    [Header("References")]
    public YoloDetector detector;
    public Camera arCamera;

    [Header("Auto Detection")]
    [Tooltip("Seconds between automatic detection runs")]
    public float detectionInterval = 3f;
    [Tooltip("Seconds before bounding boxes fade out")]
    public float boxFadeTime = 5f;

    [Header("Visual")]
    public Color boxColor = new Color(1f, 0.2f, 0.2f, 0.9f);
    public int boxBorderWidth = 3;
    public int labelFontSize = 18;

    public event Action<DetectionTapData> OnDetectionTapped;

    public struct TimedDetection
    {
        public YoloDetector.DetectionResult result;
        public float timestamp;
    }

    List<TimedDetection> m_ActiveDetections = new List<TimedDetection>();
    bool m_AutoRunning;
    bool m_Paused;
    bool m_IsDetecting;
    Texture2D m_LastScreenshot;
    GUIStyle m_BoxStyle;
    GUIStyle m_LabelStyle;

    int m_DetectionCount;
    float m_LastScanTime;

    public int detectionCount => m_DetectionCount;
    public float timeSinceLastScan => Time.time - m_LastScanTime;
    public List<TimedDetection> activeDetections => m_ActiveDetections;

    void Start()
    {
        if (arCamera == null)
            arCamera = Camera.main;
    }

    void Update()
    {
        // Remove expired detections
        float cutoff = Time.time - boxFadeTime;
        m_ActiveDetections.RemoveAll(d => d.timestamp < cutoff);
        m_DetectionCount = m_ActiveDetections.Count;

        // Handle touch input
        if (!m_Paused)
            HandleTouchInput();
    }

    // --- Auto Detection Control ---

    public void StartAutoDetection()
    {
        m_AutoRunning = true;
        m_Paused = false;
        StartCoroutine(AutoDetectionLoop());
    }

    public void StopAutoDetection()
    {
        m_AutoRunning = false;
        m_Paused = false;
        StopAllCoroutines();
    }

    public void PauseAutoDetection()
    {
        m_Paused = true;
    }

    public void ResumeAutoDetection()
    {
        m_Paused = false;
    }

    IEnumerator AutoDetectionLoop()
    {
        while (m_AutoRunning)
        {
            if (!m_Paused && !m_IsDetecting)
            {
                yield return StartCoroutine(RunDetection());
            }
            yield return new WaitForSeconds(detectionInterval);
        }
    }

    IEnumerator RunDetection()
    {
        if (detector == null || !detector.isInitialized)
            yield break;

        m_IsDetecting = true;

        yield return new WaitForEndOfFrame();

        // Capture screen
        Texture2D screenTex = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
        screenTex.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
        screenTex.Apply();

        // Run detection
        var results = detector.Detect(screenTex);

        // Store results with timestamp
        float now = Time.time;
        m_LastScanTime = now;

        foreach (var r in results)
        {
            m_ActiveDetections.Add(new TimedDetection
            {
                result = r,
                timestamp = now
            });
        }

        // Keep last screenshot for potential crop
        if (m_LastScreenshot != null)
            Destroy(m_LastScreenshot);
        m_LastScreenshot = screenTex;

        m_IsDetecting = false;
    }

    // --- Touch Interaction ---

    void HandleTouchInput()
    {
        Vector2 inputPos = Vector2.zero;
        bool isInput = false;

        if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)
        {
            inputPos = Input.GetTouch(0).position;
            isInput = true;
        }
        else if (Input.GetMouseButtonDown(0))
        {
            inputPos = Input.mousePosition;
            isInput = true;
        }

        if (isInput)
        {
            var detection = FindDetectionAtScreenPoint(inputPos);
            if (detection.HasValue)
            {
                var tapData = CreateTapData(detection.Value, inputPos);
                OnDetectionTapped?.Invoke(tapData);
            }
        }
    }

    public YoloDetector.DetectionResult? FindDetectionAtScreenPoint(Vector2 screenPos)
    {
        float normX = screenPos.x / Screen.width;
        float normY = 1.0f - (screenPos.y / Screen.height);

        foreach (var td in m_ActiveDetections)
        {
            if (td.result.box.Contains(new Vector2(normX, normY)))
                return td.result;
        }
        return null;
    }

    DetectionTapData CreateTapData(YoloDetector.DetectionResult detection, Vector2 screenPos)
    {
        var data = new DetectionTapData
        {
            label = detection.label,
            confidence = detection.score,
            normalizedBox = detection.box,
            timestamp = DateTime.Now.ToString("HH:mm:ss")
        };

        // Create screenshot copy
        if (m_LastScreenshot != null)
        {
            data.screenshot = Instantiate(m_LastScreenshot);

            // Crop detection region
            data.croppedImage = CropDetection(m_LastScreenshot, detection.box);
        }

        return data;
    }

    Texture2D CropDetection(Texture2D source, Rect normalizedBox)
    {
        int x = Mathf.Clamp(Mathf.RoundToInt(normalizedBox.x * source.width), 0, source.width - 1);
        int y = Mathf.Clamp(Mathf.RoundToInt(normalizedBox.y * source.height), 0, source.height - 1);
        int w = Mathf.Clamp(Mathf.RoundToInt(normalizedBox.width * source.width), 1, source.width - x);
        int h = Mathf.Clamp(Mathf.RoundToInt(normalizedBox.height * source.height), 1, source.height - y);

        // Unity texture Y is bottom-up, normalized Y is top-down
        int flippedY = source.height - y - h;
        flippedY = Mathf.Clamp(flippedY, 0, source.height - h);

        var pixels = source.GetPixels(x, flippedY, w, h);
        var cropped = new Texture2D(w, h, TextureFormat.RGB24, false);
        cropped.SetPixels(pixels);
        cropped.Apply();
        return cropped;
    }

    // --- GUI Rendering ---

    void OnGUI()
    {
        if (m_ActiveDetections.Count == 0) return;

        InitGUIStyles();

        float now = Time.time;
        foreach (var td in m_ActiveDetections)
        {
            float age = now - td.timestamp;
            float alpha = Mathf.Clamp01(1f - (age / boxFadeTime));

            DrawBoundingBox(td.result, alpha);
        }
    }

    void InitGUIStyles()
    {
        if (m_BoxStyle != null) return;

        m_BoxStyle = new GUIStyle();
        m_BoxStyle.normal.background = MakeColorTexture(boxColor);
        m_BoxStyle.border = new RectOffset(boxBorderWidth, boxBorderWidth, boxBorderWidth, boxBorderWidth);

        m_LabelStyle = new GUIStyle(GUI.skin.label);
        m_LabelStyle.fontSize = labelFontSize;
        m_LabelStyle.fontStyle = FontStyle.Bold;
        m_LabelStyle.normal.textColor = Color.white;
        m_LabelStyle.alignment = TextAnchor.UpperLeft;
        m_LabelStyle.padding = new RectOffset(4, 4, 2, 2);
    }

    void DrawBoundingBox(YoloDetector.DetectionResult result, float alpha)
    {
        float x = result.box.x * Screen.width;
        float y = result.box.y * Screen.height;
        float w = result.box.width * Screen.width;
        float h = result.box.height * Screen.height;

        var rect = new Rect(x, y, w, h);
        var prevColor = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, alpha);

        // Draw border lines
        var borderColor = new Color(boxColor.r, boxColor.g, boxColor.b, alpha);
        DrawRect(new Rect(x, y, w, boxBorderWidth), borderColor);                      // top
        DrawRect(new Rect(x, y + h - boxBorderWidth, w, boxBorderWidth), borderColor); // bottom
        DrawRect(new Rect(x, y, boxBorderWidth, h), borderColor);                      // left
        DrawRect(new Rect(x + w - boxBorderWidth, y, boxBorderWidth, h), borderColor); // right

        // Draw label background
        string labelText = $"{result.label} {(result.score * 100):F0}%";
        var labelContent = new GUIContent(labelText);
        var labelSize = m_LabelStyle.CalcSize(labelContent);
        DrawRect(new Rect(x, y - labelSize.y, labelSize.x + 8, labelSize.y), new Color(0, 0, 0, 0.7f * alpha));

        m_LabelStyle.normal.textColor = new Color(1f, 1f, 1f, alpha);
        GUI.Label(new Rect(x + 4, y - labelSize.y, labelSize.x + 8, labelSize.y), labelText, m_LabelStyle);

        GUI.color = prevColor;
    }

    void DrawRect(Rect rect, Color color)
    {
        var prevColor = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = prevColor;
    }

    Texture2D MakeColorTexture(Color color)
    {
        var tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, color);
        tex.Apply();
        return tex;
    }

    void OnDestroy()
    {
        if (m_LastScreenshot != null)
            Destroy(m_LastScreenshot);
    }
}
