using UnityEngine;
using System.Collections.Generic;
using Unity.InferenceEngine;

public class YoloDetector : MonoBehaviour
{
    [Header("Model Settings")]
    public ModelAsset yoloModelAsset;
    public TextAsset labelFile;
    public float confidenceThreshold = 0.5f;
    public float iouThreshold = 0.4f;

    [Header("Input Settings")]
    public Vector2Int inputResolution = new Vector2Int(640, 640);

    [Header("Backend")]
    [Tooltip("CPU backend recommended on mobile to avoid GPU contention with Gaussian Splatting")]
    public BackendType backendType = BackendType.CPU;

    private Model runtimeModel;
    private Worker worker;
    private string[] labels;
    private bool m_Initialized;

    public struct DetectionResult
    {
        public Rect box;       // normalized box coordinates (0~1)
        public float score;    // confidence
        public int classIndex;
        public string label;
    }

    public bool isInitialized => m_Initialized;

    void Start()
    {
        Initialize();
    }

    public void Initialize()
    {
        if (m_Initialized) return;

        if (yoloModelAsset == null)
        {
            Debug.LogError("[YoloDetector] ONNX model asset not assigned.");
            return;
        }

        if (labelFile != null)
        {
            labels = labelFile.text.Split(new char[] { '\n', '\r' }, System.StringSplitOptions.RemoveEmptyEntries);
        }
        else
        {
            Debug.LogWarning("[YoloDetector] No label file. Using default 'Object'.");
            labels = new string[] { "Object" };
        }

        runtimeModel = ModelLoader.Load(yoloModelAsset);
        worker = new Worker(runtimeModel, backendType);
        m_Initialized = true;
    }

    public List<DetectionResult> Detect(Texture inputTexture)
    {
        if (!m_Initialized || worker == null) return new List<DetectionResult>();

        // 1. 전처리
        using Tensor<float> inputTensor = TextureConverter.ToTensor(inputTexture, inputResolution.x, inputResolution.y, 3);

        // 2. 추론
        worker.Schedule(inputTensor);

        // 3. 결과 획득
        using Tensor<float> outputTensor = worker.PeekOutput() as Tensor<float>;
        float[] outputData = outputTensor.DownloadToArray();

        // 4. 후처리
        return ProcessOutput(outputData, outputTensor.shape);
    }

    private List<DetectionResult> ProcessOutput(float[] data, TensorShape shape)
    {
        List<DetectionResult> detections = new List<DetectionResult>();

        // YOLOv8 Output Shape: (1, 4 + ClassCount, Anchors)
        // shape[1] = 채널 수 (4 + Classes)
        // shape[2] = 앵커 수
        int channels = shape[1];
        int anchors = shape[2];
        int numClasses = labels.Length;

        // 모델 출력 채널 수와 라벨 수가 맞는지 확인 (4 + numClasses == channels)
        // 만약 맞지 않으면 경고를 띄우거나 안전하게 처리
        if (channels < 4 + 1) return detections; // 최소 1개 클래스는 있어야 함

        for (int i = 0; i < anchors; i++)
        {
            // 가장 높은 점수를 가진 클래스 찾기
            float maxScore = 0f;
            int bestClassIdx = -1;

            // 채널 4부터 클래스 스코어 시작
            // 실제 데이터 인덱스: (channel_index * anchors) + anchor_index
            for (int c = 0; c < numClasses; c++)
            {
                int channelIdx = 4 + c;
                if (channelIdx >= channels) break;

                float currentScore = data[channelIdx * anchors + i];
                if (currentScore > maxScore)
                {
                    maxScore = currentScore;
                    bestClassIdx = c;
                }
            }

            if (maxScore > confidenceThreshold)
            {
                // Box 정보 (0,1,2,3 채널)
                float cx = data[0 * anchors + i];
                float cy = data[1 * anchors + i];
                float w = data[2 * anchors + i];
                float h = data[3 * anchors + i];

                // 0~1 정규화
                float normX = (cx - w / 2) / inputResolution.x;
                float normY = (cy - h / 2) / inputResolution.y;
                float normW = w / inputResolution.x;
                float normH = h / inputResolution.y;

                detections.Add(new DetectionResult
                {
                    box = new Rect(normX, normY, normW, normH),
                    score = maxScore,
                    classIndex = bestClassIdx,
                    label = (bestClassIdx >= 0 && bestClassIdx < labels.Length) ? labels[bestClassIdx] : "Unknown"
                });
            }
        }

        return ApplyNMS(detections);
    }

    private List<DetectionResult> ApplyNMS(List<DetectionResult> detections)
    {
        detections.Sort((a, b) => b.score.CompareTo(a.score));
        List<DetectionResult> finalBoxes = new List<DetectionResult>();

        while (detections.Count > 0)
        {
            var best = detections[0];
            finalBoxes.Add(best);
            detections.RemoveAt(0);

            for (int i = detections.Count - 1; i >= 0; i--)
            {
                float iou = CalculateIoU(best.box, detections[i].box);
                // 같은 클래스끼리만 NMS를 적용할지, 모든 박스에 대해 할지 선택. 보통은 클래스별로 함.
                // 여기서는 간단히 IoU만 보고 제거
                if (iou > iouThreshold)
                {
                    detections.RemoveAt(i);
                }
            }
        }

        return finalBoxes;
    }

    private float CalculateIoU(Rect boxA, Rect boxB)
    {
        float xA = Mathf.Max(boxA.x, boxB.x);
        float yA = Mathf.Max(boxA.y, boxB.y);
        float xB = Mathf.Min(boxA.x + boxA.width, boxB.x + boxB.width);
        float yB = Mathf.Min(boxA.y + boxA.height, boxB.y + boxB.height);

        float interArea = Mathf.Max(0, xB - xA) * Mathf.Max(0, yB - yA);
        float boxAArea = boxA.width * boxA.height;
        float boxBArea = boxB.width * boxB.height;

        return interArea / (boxAArea + boxBArea - interArea);
    }

    void OnDestroy()
    {
        worker?.Dispose();
    }
}