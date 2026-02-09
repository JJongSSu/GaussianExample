// SPDX-License-Identifier: MIT

#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR.ARFoundation;
using UnityEditor;
using UnityEditor.SceneManagement;
using GaussianSplatting.Runtime;

/// <summary>
/// Editor utility to auto-generate the BridgeInspectionScene with proper AR + Gaussian Splatting hierarchy.
/// Menu: Tools > Bridge Inspection > Create Inspection Scene
/// </summary>
public static class BridgeInspectionSceneSetup
{
    [MenuItem("Tools/Bridge Inspection/Create Inspection Scene")]
    public static void CreateScene()
    {
        // Create new scene
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // --- AR Session ---
        var arSessionGO = new GameObject("AR Session");
        arSessionGO.AddComponent<ARSession>();

        // --- XR Origin ---
        var xrOriginGO = new GameObject("XR Origin");
        var xrOrigin = xrOriginGO.AddComponent<Unity.XR.CoreUtils.XROrigin>();

        // Camera Offset (required by XROrigin)
        var cameraOffsetGO = new GameObject("Camera Offset");
        cameraOffsetGO.transform.SetParent(xrOriginGO.transform);
        xrOrigin.CameraFloorOffsetObject = cameraOffsetGO;

        // AR Camera
        var arCameraGO = new GameObject("AR Camera");
        arCameraGO.transform.SetParent(cameraOffsetGO.transform);
        arCameraGO.tag = "MainCamera";

        var camera = arCameraGO.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.black;
        camera.nearClipPlane = 0.1f;
        camera.farClipPlane = 100f;

        arCameraGO.AddComponent<ARCameraManager>();
        arCameraGO.AddComponent<ARCameraBackground>();
        arCameraGO.AddComponent<TrackedPoseDriver>();

        xrOrigin.Camera = camera;

        // --- GaussianSplatRoot ---
        var splatRootGO = new GameObject("GaussianSplatRoot");

        var bridgeSplatGO = new GameObject("BridgeSplat");
        bridgeSplatGO.transform.SetParent(splatRootGO.transform);

        var splatRenderer = bridgeSplatGO.AddComponent<GaussianSplatRenderer>();
        splatRenderer.m_SHOrder = 0;
        splatRenderer.m_SortNthFrame = 4;
        splatRenderer.m_SplatScale = 1.0f;

        // Try to find and assign shaders
        AssignShaders(splatRenderer);

        // --- Systems ---
        var systemsGO = new GameObject("Systems");

        // AppStateManager
        var appStateGO = new GameObject("AppStateManager");
        appStateGO.transform.SetParent(systemsGO.transform);
        var appState = appStateGO.AddComponent<AppStateManager>();

        // ImageLocalizationManager
        var localizationGO = new GameObject("ImageLocalizationManager");
        localizationGO.transform.SetParent(systemsGO.transform);
        var localization = localizationGO.AddComponent<ImageLocalizationManager>();
        localization.gaussianSplatRoot = splatRootGO.transform;
        localization.arCamera = camera;
        localization.arCameraManager = arCameraGO.GetComponent<ARCameraManager>();

        // ColmapReferenceDatabase
        var refDbGO = new GameObject("ColmapReferenceDatabase");
        refDbGO.transform.SetParent(localizationGO.transform);
        var refDb = refDbGO.AddComponent<ColmapReferenceDatabase>();
        localization.referenceDatabase = refDb;

        // AutoDetectionManager
        var detectionGO = new GameObject("AutoDetectionManager");
        detectionGO.transform.SetParent(systemsGO.transform);
        var detectionMgr = detectionGO.AddComponent<AutoDetectionManager>();
        detectionMgr.arCamera = camera;
        detectionMgr.enabled = false;

        // YoloDetector
        var yoloGO = new GameObject("YoloDetector");
        yoloGO.transform.SetParent(detectionGO.transform);
        var yoloDetector = yoloGO.AddComponent<YoloDetector>();
        yoloDetector.backendType = Unity.InferenceEngine.BackendType.CPU;
        detectionMgr.detector = yoloDetector;

        // Try to assign YOLO model
        AssignYoloModel(yoloDetector);

        // MobileGaussianOptimizer
        var optimizerGO = new GameObject("MobileGaussianOptimizer");
        optimizerGO.transform.SetParent(systemsGO.transform);
        var optimizer = optimizerGO.AddComponent<MobileGaussianOptimizer>();
        optimizer.splatRenderer = splatRenderer;

        // --- UI ---
        var uiGO = new GameObject("InspectionUI");
        var uiMgr = uiGO.AddComponent<InspectionUIManager>();
        uiMgr.appStateManager = appState;
        uiMgr.detectionManager = detectionMgr;
        uiMgr.gaussianOptimizer = optimizer;
        uiMgr.localizationManager = localization;

        // --- Wire up AppStateManager references ---
        appState.localizationManager = localization;
        appState.detectionManager = detectionMgr;
        appState.uiManager = uiMgr;
        appState.gaussianOptimizer = optimizer;

        // --- EventSystem ---
        if (Object.FindAnyObjectByType<EventSystem>() == null)
        {
            var eventSystemGO = new GameObject("EventSystem");
            eventSystemGO.AddComponent<EventSystem>();
            eventSystemGO.AddComponent<StandaloneInputModule>();
        }

        // --- Directional Light (for visibility in editor) ---
        var lightGO = new GameObject("Directional Light");
        var light = lightGO.AddComponent<Light>();
        light.type = LightType.Directional;
        lightGO.transform.rotation = Quaternion.Euler(50, -30, 0);

        // Mark scene dirty
        EditorSceneManager.MarkSceneDirty(scene);

        // Save scene
        string scenePath = "Assets/Scenes/BridgeInspectionScene.unity";
        EnsureDirectoryExists(scenePath);
        EditorSceneManager.SaveScene(scene, scenePath);

        Debug.Log($"[BridgeInspection] Scene created at {scenePath}");
        Debug.Log("[BridgeInspection] TODO: Assign GaussianSplatAsset to BridgeSplat > GaussianSplatRenderer");
        Debug.Log("[BridgeInspection] TODO: Assign YOLO model and label file to YoloDetector");
        Debug.Log("[BridgeInspection] TODO: Set cameras.json path in ImageLocalizationManager");

        EditorUtility.DisplayDialog("Bridge Inspection Scene",
            "Scene created successfully!\n\n" +
            "Next steps:\n" +
            "1. Assign GaussianSplatAsset to BridgeSplat\n" +
            "2. Assign YOLO model to YoloDetector\n" +
            "3. Set cameras.json path in ImageLocalizationManager\n" +
            "4. Place reference images in StreamingAssets/LocalizationDB/",
            "OK");
    }

    static void AssignShaders(GaussianSplatRenderer renderer)
    {
        renderer.m_ShaderSplats = Shader.Find("Gaussian Splatting/Render Splats");
        renderer.m_ShaderComposite = Shader.Find("Gaussian Splatting/Composite");
        renderer.m_ShaderDebugPoints = Shader.Find("Gaussian Splatting/Debug/Render Debug Points");
        renderer.m_ShaderDebugBoxes = Shader.Find("Gaussian Splatting/Debug/Render Debug Boxes");

        string[] guids = AssetDatabase.FindAssets("SplatUtilities t:ComputeShader");
        if (guids.Length > 0)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            renderer.m_CSSplatUtilities = AssetDatabase.LoadAssetAtPath<ComputeShader>(path);
        }
    }

    static void AssignYoloModel(YoloDetector detector)
    {
        string[] guids = AssetDatabase.FindAssets("full_coping_imgsz640 t:ModelAsset");
        if (guids.Length > 0)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            detector.yoloModelAsset = AssetDatabase.LoadAssetAtPath<Unity.InferenceEngine.ModelAsset>(path);
        }
    }

    static void EnsureDirectoryExists(string assetPath)
    {
        string dir = System.IO.Path.GetDirectoryName(assetPath);
        if (!string.IsNullOrEmpty(dir) && !AssetDatabase.IsValidFolder(dir))
        {
            string[] parts = dir.Replace("\\", "/").Split('/');
            string current = parts[0]; // "Assets"
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
#endif
