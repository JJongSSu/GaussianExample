// SPDX-License-Identifier: MIT

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

/// <summary>
/// Image-based localization manager for aligning Gaussian Splat model with AR scene.
/// Uses COLMAP camera poses from reference images to compute the transform that
/// maps the splat model into AR world space.
///
/// Localization pipeline:
/// 1. Capture AR camera frame
/// 2. Compare with reference images (simple similarity matching)
/// 3. Use matched reference's known COLMAP pose to compute alignment transform
/// 4. Apply transform to GaussianSplatRoot
///
/// Falls back to manual alignment mode if automatic localization fails.
/// </summary>
public class ImageLocalizationManager : MonoBehaviour
{
    [Header("References")]
    public Transform gaussianSplatRoot;
    public ARCameraManager arCameraManager;
    public Camera arCamera;
    public ColmapReferenceDatabase referenceDatabase;

    [Header("Reference Data")]
    [Tooltip("Path to cameras.json for reference frames")]
    public string camerasJsonPath;

    [Header("Localization Settings")]
    [Tooltip("Seconds between localization attempts")]
    public float localizationInterval = 2f;
    [Tooltip("Minimum match quality (0-1) to accept localization")]
    public float matchThreshold = 0.3f;

    [Header("Manual Alignment")]
    public bool manualAlignmentMode;
    [Tooltip("Speed for manual position adjustment")]
    public float manualMoveSpeed = 0.5f;
    [Tooltip("Speed for manual rotation adjustment")]
    public float manualRotateSpeed = 30f;

    [Header("State")]
    [SerializeField] private bool m_IsLocalizing;
    [SerializeField] private bool m_IsLocalized;
    [SerializeField] private int m_BestMatchIndex = -1;
    [SerializeField] private float m_BestMatchScore;

    public event Action OnLocalizationSucceeded;
    public event Action OnLocalizationFailed;

    public bool isLocalized => m_IsLocalized;

    Coroutine m_LocalizationCoroutine;
    Vector2 m_LastTouchPos;
    bool m_IsDragging;
    int m_ManualFingerCount;

    void Start()
    {
        if (arCamera == null)
            arCamera = Camera.main;

        // Load reference database if path is set
        if (referenceDatabase != null && !string.IsNullOrEmpty(camerasJsonPath))
        {
            referenceDatabase.LoadFromCamerasJson(camerasJsonPath);
            referenceDatabase.LoadReferenceImages();
        }
    }

    // --- Localization Control ---

    public void StartLocalization()
    {
        if (m_IsLocalizing) return;
        m_IsLocalizing = true;
        m_IsLocalized = false;

        if (referenceDatabase != null && referenceDatabase.referenceFrames.Count > 0)
        {
            m_LocalizationCoroutine = StartCoroutine(LocalizationLoop());
        }
        else
        {
            Debug.LogWarning("[Localization] No reference frames. Switching to manual alignment.");
            manualAlignmentMode = true;
        }
    }

    public void StopLocalization()
    {
        m_IsLocalizing = false;
        if (m_LocalizationCoroutine != null)
        {
            StopCoroutine(m_LocalizationCoroutine);
            m_LocalizationCoroutine = null;
        }
    }

    // --- Automatic Localization ---

    IEnumerator LocalizationLoop()
    {
        while (m_IsLocalizing && !m_IsLocalized)
        {
            yield return StartCoroutine(TryLocalize());

            if (m_IsLocalized)
            {
                Debug.Log($"[Localization] Succeeded! Match index={m_BestMatchIndex}, score={m_BestMatchScore:F3}");
                OnLocalizationSucceeded?.Invoke();
                yield break;
            }

            yield return new WaitForSeconds(localizationInterval);
        }
    }

    IEnumerator TryLocalize()
    {
        if (arCameraManager == null || referenceDatabase == null)
            yield break;

        // Try to get camera image
        Texture2D cameraImage = null;

        if (arCameraManager.TryAcquireLatestCpuImage(out var cpuImage))
        {
            // Convert XRCpuImage to Texture2D
            var conversionParams = new XRCpuImage.ConversionParams
            {
                inputRect = new RectInt(0, 0, cpuImage.width, cpuImage.height),
                outputDimensions = new Vector2Int(cpuImage.width / 2, cpuImage.height / 2), // Downscale
                outputFormat = TextureFormat.RGBA32,
                transformation = XRCpuImage.Transformation.MirrorY
            };

            int size = cpuImage.GetConvertedDataSize(conversionParams);
            var buffer = new Unity.Collections.NativeArray<byte>(size, Unity.Collections.Allocator.Temp);
            cpuImage.Convert(conversionParams, buffer);

            cameraImage = new Texture2D(
                conversionParams.outputDimensions.x,
                conversionParams.outputDimensions.y,
                conversionParams.outputFormat, false);
            cameraImage.LoadRawTextureData(buffer);
            cameraImage.Apply();

            buffer.Dispose();
            cpuImage.Dispose();
        }
        else
        {
            // Fallback: capture from screen
            yield return new WaitForEndOfFrame();
            cameraImage = new Texture2D(Screen.width / 2, Screen.height / 2, TextureFormat.RGB24, false);
            // Read at half res for performance
            var rt = RenderTexture.GetTemporary(Screen.width / 2, Screen.height / 2);
            Graphics.Blit(null, rt);
            RenderTexture.active = rt;
            cameraImage.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            cameraImage.Apply();
            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(rt);
        }

        // Match against reference images
        m_BestMatchIndex = -1;
        m_BestMatchScore = 0f;

        for (int i = 0; i < referenceDatabase.referenceFrames.Count; i++)
        {
            var refFrame = referenceDatabase.referenceFrames[i];
            if (refFrame.image == null) continue;

            float score = ComputeImageSimilarity(cameraImage, refFrame.image);
            if (score > m_BestMatchScore)
            {
                m_BestMatchScore = score;
                m_BestMatchIndex = i;
            }
        }

        Destroy(cameraImage);

        // Accept match if above threshold
        if (m_BestMatchIndex >= 0 && m_BestMatchScore >= matchThreshold)
        {
            ApplyLocalization(m_BestMatchIndex);
            m_IsLocalized = true;
        }
    }

    /// <summary>
    /// Simple color histogram-based image similarity.
    /// For production, replace with ORB feature matching via native OpenCV plugin.
    /// </summary>
    float ComputeImageSimilarity(Texture2D imageA, Texture2D imageB)
    {
        // Downsample both to small size for quick comparison
        int sampleSize = 32;
        var rtA = RenderTexture.GetTemporary(sampleSize, sampleSize);
        var rtB = RenderTexture.GetTemporary(sampleSize, sampleSize);
        Graphics.Blit(imageA, rtA);
        Graphics.Blit(imageB, rtB);

        var smallA = new Texture2D(sampleSize, sampleSize, TextureFormat.RGB24, false);
        var smallB = new Texture2D(sampleSize, sampleSize, TextureFormat.RGB24, false);

        RenderTexture.active = rtA;
        smallA.ReadPixels(new Rect(0, 0, sampleSize, sampleSize), 0, 0);
        smallA.Apply();

        RenderTexture.active = rtB;
        smallB.ReadPixels(new Rect(0, 0, sampleSize, sampleSize), 0, 0);
        smallB.Apply();

        RenderTexture.active = null;
        RenderTexture.ReleaseTemporary(rtA);
        RenderTexture.ReleaseTemporary(rtB);

        // Compute color histogram similarity (normalized cross-correlation)
        var pixelsA = smallA.GetPixels();
        var pixelsB = smallB.GetPixels();

        Destroy(smallA);
        Destroy(smallB);

        float dotProduct = 0f;
        float magA = 0f;
        float magB = 0f;

        for (int i = 0; i < pixelsA.Length; i++)
        {
            float rA = pixelsA[i].r, gA = pixelsA[i].g, bA = pixelsA[i].b;
            float rB = pixelsB[i].r, gB = pixelsB[i].g, bB = pixelsB[i].b;

            dotProduct += rA * rB + gA * gB + bA * bB;
            magA += rA * rA + gA * gA + bA * bA;
            magB += rB * rB + gB * gB + bB * bB;
        }

        if (magA < 0.001f || magB < 0.001f)
            return 0f;

        return dotProduct / (Mathf.Sqrt(magA) * Mathf.Sqrt(magB));
    }

    /// <summary>
    /// Apply the localization transform to GaussianSplatRoot.
    /// Computes: T_splatRoot = T_ar_camera * inverse(T_colmap_in_unity)
    /// </summary>
    void ApplyLocalization(int matchIndex)
    {
        var refFrame = referenceDatabase.referenceFrames[matchIndex];

        if (gaussianSplatRoot == null || arCamera == null) return;

        // The reference frame stores the camera pose in Unity coordinate space
        // (already converted from COLMAP by ColmapReferenceDatabase).
        //
        // We want to find transform T such that:
        //   T * refFrame.position = arCamera.position
        //   T * refFrame.rotation = arCamera.rotation
        //
        // This means: T = arCamera.pose * inverse(refFrame.pose)

        // Construct the reference camera transform matrix (COLMAP camera in Unity space)
        Matrix4x4 refCamMatrix = Matrix4x4.TRS(refFrame.position, refFrame.rotation, Vector3.one);

        // Current AR camera transform
        Matrix4x4 arCamMatrix = Matrix4x4.TRS(
            arCamera.transform.position,
            arCamera.transform.rotation,
            Vector3.one);

        // Alignment transform: maps COLMAP/Unity space to AR world space
        Matrix4x4 alignmentMatrix = arCamMatrix * refCamMatrix.inverse;

        // Apply to GaussianSplatRoot
        gaussianSplatRoot.position = alignmentMatrix.GetColumn(3);
        gaussianSplatRoot.rotation = alignmentMatrix.rotation;
        // Keep scale as-is (typically Vector3.one)

        Debug.Log($"[Localization] Applied alignment from ref '{refFrame.imageName}'. " +
                  $"Root pos={gaussianSplatRoot.position}, rot={gaussianSplatRoot.rotation.eulerAngles}");
    }

    // --- Manual Alignment Mode ---

    void Update()
    {
        if (!manualAlignmentMode || gaussianSplatRoot == null) return;

        HandleManualAlignment();
    }

    void HandleManualAlignment()
    {
        if (Input.touchCount == 1)
        {
            var touch = Input.GetTouch(0);

            if (touch.phase == TouchPhase.Began)
            {
                m_LastTouchPos = touch.position;
                m_IsDragging = true;
            }
            else if (touch.phase == TouchPhase.Moved && m_IsDragging)
            {
                Vector2 delta = touch.position - m_LastTouchPos;
                m_LastTouchPos = touch.position;

                // Single finger: rotate
                gaussianSplatRoot.Rotate(Vector3.up, delta.x * manualRotateSpeed * Time.deltaTime, Space.World);
                gaussianSplatRoot.Rotate(Vector3.right, -delta.y * manualRotateSpeed * Time.deltaTime, Space.World);
            }
            else if (touch.phase == TouchPhase.Ended)
            {
                m_IsDragging = false;
            }
        }
        else if (Input.touchCount == 2)
        {
            // Two fingers: translate in XY plane
            var touch0 = Input.GetTouch(0);
            var touch1 = Input.GetTouch(1);

            if (touch0.phase == TouchPhase.Moved || touch1.phase == TouchPhase.Moved)
            {
                Vector2 avgDelta = (touch0.deltaPosition + touch1.deltaPosition) * 0.5f;
                Vector3 move = arCamera.transform.right * avgDelta.x + arCamera.transform.up * avgDelta.y;
                gaussianSplatRoot.position += move * manualMoveSpeed * Time.deltaTime * 0.01f;

                // Pinch to scale (distance)
                float prevDist = Vector2.Distance(
                    touch0.position - touch0.deltaPosition,
                    touch1.position - touch1.deltaPosition);
                float curDist = Vector2.Distance(touch0.position, touch1.position);

                if (prevDist > 0.01f)
                {
                    float scaleFactor = curDist / prevDist;
                    gaussianSplatRoot.localScale *= scaleFactor;
                }
            }
        }

        // Mouse fallback (editor testing)
        if (Application.isEditor)
        {
            if (Input.GetMouseButton(1)) // Right-click drag: rotate
            {
                float mx = Input.GetAxis("Mouse X");
                float my = Input.GetAxis("Mouse Y");
                gaussianSplatRoot.Rotate(Vector3.up, mx * manualRotateSpeed * Time.deltaTime * 10f, Space.World);
                gaussianSplatRoot.Rotate(Vector3.right, -my * manualRotateSpeed * Time.deltaTime * 10f, Space.World);
            }

            if (Input.GetMouseButton(2)) // Middle-click drag: translate
            {
                float mx = Input.GetAxis("Mouse X");
                float my = Input.GetAxis("Mouse Y");
                gaussianSplatRoot.position += (arCamera.transform.right * mx + arCamera.transform.up * my) * manualMoveSpeed * Time.deltaTime;
            }

            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.001f)
            {
                gaussianSplatRoot.position += arCamera.transform.forward * scroll * manualMoveSpeed * 2f;
            }
        }
    }

    /// <summary>
    /// Switch to manual alignment mode as a fallback.
    /// </summary>
    public void EnableManualAlignment()
    {
        manualAlignmentMode = true;
        m_IsLocalized = true; // Allow proceeding even without auto-localization
        OnLocalizationSucceeded?.Invoke();
        Debug.Log("[Localization] Manual alignment mode enabled.");
    }
}
