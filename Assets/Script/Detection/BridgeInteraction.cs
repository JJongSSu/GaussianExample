using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class BridgeInteraction : MonoBehaviour
{
    public YoloDetector detector;
    public bool showDebugBoxes = true;

    private List<YoloDetector.DetectionResult> currentResults = new List<YoloDetector.DetectionResult>();
    private bool isProcessing = false;

    void Update()
    {
        bool isInput = Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began;
        Vector2 inputPos = Vector2.zero;

        if (isInput)
        {
            inputPos = Input.GetTouch(0).position;
        }
        else if (Input.GetMouseButtonDown(0))
        {
            isInput = true;
            inputPos = Input.mousePosition;
        }

        if (isInput && !isProcessing)
        {
            StartCoroutine(CaptureAndDetect(inputPos));
        }
    }

    IEnumerator CaptureAndDetect(Vector2 touchPos)
    {
        isProcessing = true;

        yield return new WaitForEndOfFrame();

        Texture2D screenTex = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
        screenTex.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
        screenTex.Apply();

        if (detector != null)
        {
            currentResults = detector.Detect(screenTex);
        }

        Destroy(screenTex);

        CheckHit(touchPos);

        isProcessing = false;
    }

    void CheckHit(Vector2 touchPos)
    {
        float normX = touchPos.x / Screen.width;
        float normY_Image = 1.0f - (touchPos.y / Screen.height);

        bool hit = false;
        string hitLabel = "";

        foreach (var result in currentResults)
        {
            if (result.box.Contains(new Vector2(normX, normY_Image)))
            {
                hit = true;
                hitLabel = result.label;
                // 가장 위에 있는(먼저 감지된) 하나만 처리하거나, break 없이 다 찾을 수도 있음
                break;
            }
        }

        if (hit)
        {
            Debug.Log($"<color=green>터치 성공! 감지된 객체: {hitLabel} (Pos: {touchPos})</color>");
        }
        else
        {
            Debug.Log($"<color=red>허공 터치 (Pos: {touchPos})</color>");
        }
    }

    void OnGUI()
    {
        if (!showDebugBoxes) return;

        GUI.skin.box.fontSize = 20;
        GUI.skin.box.alignment = TextAnchor.UpperLeft;

        foreach (var result in currentResults)
        {
            float x = result.box.x * Screen.width;
            float y = result.box.y * Screen.height;
            float w = result.box.width * Screen.width;
            float h = result.box.height * Screen.height;

            GUI.Box(new Rect(x, y, w, h), $"{result.label}\n{(result.score * 100):F0}%");
        }
    }
}