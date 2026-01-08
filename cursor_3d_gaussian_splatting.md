# 3D Gaussian splatting 개념 설명
_Exported on 12/5/2025 at 09:24:13 GMT+9 from Cursor (2.1.48)_

---

**User**

3D Gaussian Splatting : 3DGS에 대해 설명해줘. 그리고 3DGS를 이해해야하는데 필요한 부가적인 개념들도 추가적으로 설명해줘.

---

**Cursor**

**3D Gaussian Splatting (3DGS)**은 2023년 SIGGRAPH에서 발표된 이후 컴퓨터 그래픽스와 컴퓨터 비전 분야(특히 Novel View Synthesis - 새로운 시점 합성)에서 **NeRF(Neural Radiance Fields)**를 빠르게 대체하고 있는 혁신적인 기술입니다.

3DGS와 이를 이해하기 위한 필수 개념들을 체계적으로 설명해 드리겠습니다.

---

### 1. 3D Gaussian Splatting (3DGS)이란?

간단히 말해, **3D 공간을 수백만 개의 "3D 가우시안(타원형 구름)"으로 채워서 장면을 표현하는 기술**입니다.

기존의 NeRF가 공간의 색상과 밀도를 알아내기 위해 무거운 인공신경망(MLP) 연산을 수행했다면, 3DGS는 **명시적인(Explicit)** 점 데이터를 사용하여 **실시간 렌더링(30~100+ FPS)**과 **빠른 학습 속도**를 달성합니다.

#### 3DGS의 핵심 구성 요소 (하나의 가우시안이 가지는 정보)
하나의 3D 가우시안은 단순한 점(Point)이 아니라, 다음과 같은 속성을 가진 **3차원 타원체(Ellipsoid)**입니다.

1.  **위치 (Position, $\mu$):** 3D 공간 상의 중심 좌표 (X, Y, Z).
2.  **공분산 (Covariance, $\Sigma$):** 타원체의 모양을 결정합니다. 실제로는 **크기(Scale)**와 **회전(Rotation - Quaternion)**으로 분해하여 저장합니다. (얼마나 길쭉한지, 어느 방향으로 누워있는지)
3.  **불투명도 (Opacity, $\alpha$):** 해당 가우시안이 얼마나 투명한지.
4.  **색상 (Color - Spherical Harmonics):** 보는 각도에 따라 달라지는 색상 정보.

#### 작동 원리 (Workflow)
1.  **초기화:** SfM(Structure from Motion)으로 얻은 희소한 점 구름(Sparse Point Cloud)에서 시작합니다.
2.  **Splatting (투영):** 3D 가우시안들을 카메라 시점에 맞춰 2D 화면 평면에 "Splat(철퍼덕 던짐)"합니다.
3.  **렌더링:** 겹쳐진 가우시안들을 깊이(Depth) 순서대로 정렬하고 알파 블렌딩하여 이미지를 만듭니다.
4.  **학습 (Optimization):** 렌더링 된 이미지와 실제 사진의 차이(Loss)를 계산하여, 가우시안의 위치, 모양, 투명도 등을 수정합니다. (필요하면 가우시안을 쪼개거나 삭제하기도 함)

---

### 2. 3DGS를 이해하기 위한 필수 부가 개념들

3DGS는 완전히 새로운 개념이라기보다, 기존 그래픽스와 비전 기술의 영리한 결합입니다. 다음 개념들을 알면 깊이 있는 이해가 가능합니다.

#### 1) Structure from Motion (SfM) & COLMAP
*   **개념:** 2D 이미지 여러 장에서 특징점(Feature)을 매칭하여 3D 점 구름과 카메라의 위치/각도를 추정하는 기술입니다.
*   **3DGS에서의 역할:** 3DGS 학습을 시작하기 위한 **초기 데이터(점 구름, 카메라 포즈)**를 제공합니다. 주로 **COLMAP**이라는 오픈소스 라이브러리를 사용합니다.

#### 2) NeRF (Neural Radiance Fields)
*   **개념:** 3D 공간을 좌표(x, y, z)와 보는 방향($\theta, \phi$)을 입력받아 색상과 밀도를 뱉어내는 **인공신경망 함수**로 정의하는 방식입니다.
*   **3DGS와의 관계:** NeRF는 **임시적(Implicit)** 표현이라 학습/렌더링이 느립니다. 3DGS는 이를 해결하기 위해 나온 **명시적(Explicit)** 표현 방식입니다. NeRF의 한계를 이해하면 3DGS의 혁신성을 알 수 있습니다.

#### 3) 다변량 정규 분포 (Multivariate Gaussian Distribution)
*   **개념:** 통계학에서 종 모양의 곡선을 그리는 정규 분포를 3차원으로 확장한 것입니다.
*   **3DGS에서의 역할:** 3DGS의 "Splat" 하나하나가 바로 이 3차원 정규 분포의 등고선(타원체)입니다. 수학적으로 미분이 용이하여 학습(역전파)에 유리합니다.

#### 4) Splatting & Rasterization (스플래팅 & 래스터화)
*   **개념:**
    *   **Splatting:** 3D 공간의 점(또는 입자)을 2D 화면에 투영할 때, 점 하나를 픽셀 하나가 아니라 주변으로 퍼지는 영역(Splat)으로 그리는 고전적인 렌더링 기법입니다. (눈 뭉치를 벽에 던져서 퍼지는 자국을 상상하세요.)
    *   **Rasterization:** 벡터나 수식으로 된 이미지를 픽셀 그리드로 변환하는 과정입니다.
*   **3DGS에서의 역할:** 3D 가우시안을 2D 화면에 빠르게 그려내는 핵심 엔진입니다. 3DGS는 타일 기반(Tile-based) 래스터라이저를 사용하여 속도를 극대화했습니다.

#### 5) 구면 조화 함수 (Spherical Harmonics, SH)
*   **개념:** 구(Sphere) 표면에서 정의되는 함수들로, 복잡한 신호를 주파수 성분으로 분해하는 기법입니다. (3D 그래픽스에서의 푸리에 변환 같은 존재)
*   **3DGS에서의 역할:** **"보는 각도에 따른 색상 변화"**를 표현합니다. 예를 들어, 금속 재질은 보는 각도에 따라 반짝임이 다릅니다. 단순 RGB 값이 아니라 SH 계수를 저장함으로써 이러한 반사 효과(View-dependent effect)를 구현합니다.

#### 6) 미분 가능한 렌더링 (Differentiable Rendering)
*   **개념:** 렌더링 과정(3D → 2D)을 수학적으로 미분 가능하게 만들어, 결과 이미지의 오차를 통해 3D 파라미터를 역으로 학습할 수 있게 하는 기술입니다.
*   **3DGS에서의 역할:** 이것이 없다면 학습이 불가능합니다. 렌더링 파이프라인 전체가 미분 가능하기 때문에, 이미지 차이를 통해 "이 가우시안을 왼쪽으로 좀 더 옮겨야겠군", "이 가우시안은 투명하게 만들어야겠군" 하는 판단을 수학적으로 수행합니다.

### 요약
*   **3DGS**는 수많은 타원형 붓터치(Gaussian)로 3D 공간을 그리는 기술입니다.
*   **SfM**으로 뼈대를 잡고, **미분 가능한 래스터라이저**를 통해 **가우시안의 모양과 색(SH)**을 학습하며, **NeRF**보다 훨씬 빠르고 선명합니다.

---

**User**

3DGS를 AR 증강 기술에 활용하려면 어떤 부분을 고려해야 하는지, 어떤 방법을 사용해야하는지에 대해 알려줘.

---

**Cursor**

3D Gaussian Splatting(3DGS)을 AR(증강현실)에 적용하는 것은 현재 XR 업계에서 가장 뜨거운 주제 중 하나입니다. 3DGS는 **실사 수준의 렌더링**을 제공하지만, 모바일 기기의 **제한된 컴퓨팅 자원**이라는 명확한 한계가 존재합니다.

AR 환경(특히 모바일/헤드셋)에서 3DGS를 활용하기 위해 고려해야 할 점과 구체적인 구현 방법들을 정리해 드립니다.

---

### 1. AR 적용 시 핵심 고려 사항 (Challenges)

모바일 AR 환경(iOS, Android, Vision Pro 등)은 고성능 데스크탑 GPU와 환경이 다릅니다.

#### 1) 렌더링 성능 (The Sorting Bottleneck)
*   **문제:** 3DGS는 올바른 알파 블렌딩을 위해 매 프레임 수백만 개의 가우시안을 **카메라 거리 순으로 정렬(Sorting)**해야 합니다. 이는 CPU/GPU 대역폭을 많이 소모하며, 모바일 기기의 배터리 광탈과 발열의 주원인이 됩니다.
*   **고려사항:** 60fps 방어를 위해 가우시안 개수를 제한하거나, 정렬 알고리즘이 고도로 최적화된 뷰어(예: Metal/Vulkan Compute Shader 기반)가 필요합니다.

#### 2) 메모리와 용량 (VRAM & Storage)
*   **문제:** 학습된 원본 `.ply` 파일은 수백 MB에서 GB 단위입니다. 모바일 RAM 용량을 초과하면 앱이 강제 종료(Crash)됩니다.
*   **고려사항:** 데이터 압축과 경량화가 필수입니다.

#### 3) 물리적 상호작용 및 오클루전 (Physics & Occlusion)
*   **문제:** 3DGS는 '메시(Mesh)'가 아니라 '점 구름(Point Cloud)' 형태입니다. 따라서 AR 캐릭터가 3DGS로 스캔된 의자 뒤로 숨거나(Occlusion), 바닥에 닿는(Collision) 처리가 기본적으로 불가능합니다.
*   **고려사항:** 시각적 표현은 3DGS로 하되, 물리 처리를 위한 투명한 **Proxy Mesh(대리 메쉬)**가 필요합니다.

---

### 2. 구현 방법 및 기술적 접근 (Solutions)

Unity와 ARFoundation 환경을 기준으로 구체적인 해결책입니다.

#### 1) 3DGS 모델 경량화 (Optimization)
모바일 구동을 위해 다음 단계들이 필수적입니다.
*   **Pruning (가지치기):** 투명도가 낮거나(거의 안 보이는), 너무 큰 가우시안을 제거하여 개수를 줄입니다.
*   **SH(Spherical Harmonics) 차수 낮추기:** 색상 정보를 담는 SH 계수를 기본 3차(Degree 3)에서 0차(단색) 또는 1차로 낮추면 파일 크기가 1/3 ~ 1/4로 줄어듭니다. 모바일 화면에서는 큰 차이가 느껴지지 않습니다.
*   **Quantization (양자화):** 32bit float 데이터를 16bit float이나 8bit 정수로 변환하여 메모리 사용량을 줄입니다.

#### 2) Unity에서의 렌더링 파이프라인
기존 Unity 렌더러는 3DGS를 지원하지 않으므로 커스텀 렌더러가 필요합니다.
*   **오픈소스 활용:** Unity 커뮤니티에서 가장 널리 쓰이는 Aras Pranckevičius의 [UnityGaussianSplatting](https://github.com/aras-p/UnityGaussianSplatting) 패키지를 사용하는 것이 표준입니다. (Compute Shader를 사용하여 정렬 및 렌더링을 처리)
*   **상용 SDK:** **Luma AI** 등의 플랫폼에서 제공하는 Unity SDK를 사용하면, 모바일에 최적화된 뷰어를 쉽게 붙일 수 있습니다. (Luma는 `.ply`를 자체 포맷으로 변환하여 스트리밍/렌더링 함)

#### 3) AR 좌표계 정렬 (Alignment)
3DGS로 캡처한 공간을 실제 AR 공간과 일치시켜야 합니다.
*   **수동 정렬:** 앱 실행 시 3DGS 모델을 띄우고, 사용자가 터치 제스처로 실제 사물 위치에 맞추게 합니다.
*   **Image Tracking / Object Tracking:** 특정 마커나 사물을 인식했을 때 그 위치를 기준으로 3DGS 모델을 `ARAnchor`에 부착합니다.
*   **Geospatial API (VPS):** 작성자님이 보고 계신 `GeospatialStreetscapeManager.cs`와 관련이 깊습니다. 구글의 VPS를 통해 얻은 정확한 위도/경도/높이 값을 3DGS 모델의 원점과 매핑하면, 건물 규모의 3DGS를 실제 거리에 1:1로 덮어씌울 수 있습니다.

#### 4) 물리 상호작용 구현 (Proxy Mesh)
3DGS 학습 시, 3DGS 데이터에서 **Mesh를 추출하는 알고리즘(예: Marching Cubes, Poisson Reconstruction)**을 함께 돌려 저해상도 Mesh를 얻습니다.
*   Unity에서 이 Mesh를 가져와 `Mesh Collider`를 추가하고 `Mesh Renderer`는 끕니다.
*   이렇게 하면 시각적으로는 고퀄리티 3DGS가 보이고, 물리적으로는 투명 메쉬가 충돌을 처리합니다.

---

### 3. Unity 코드 예시 (개념)

3DGS 객체를 AR 환경에 배치하는 간단한 개념 코드입니다.

```csharp
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

// 가상의 3DGS 렌더러 컴포넌트가 있다고 가정
// 실제로는 'UnityGaussianSplatting' 같은 에셋의 컴포넌트를 제어해야 함
public class ARGaussianSplatController : MonoBehaviour
{
    [SerializeField] GameObject gaussianSplatPrefab; // 3DGS 에셋
    [SerializeField] ARRaycastManager raycastManager;
    
    private GameObject spawnedObject;

    void Update()
    {
        if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)
        {
            PlaceSplatModel(Input.GetTouch(0).position);
        }
    }

    void PlaceSplatModel(Vector2 touchPosition)
    {
        List<ARRaycastHit> hits = new List<ARRaycastHit>();
        if (raycastManager.Raycast(touchPosition, hits, TrackableType.PlaneWithinPolygon))
        {
            Pose hitPose = hits[0].pose;

            if (spawnedObject == null)
            {
                spawnedObject = Instantiate(gaussianSplatPrefab, hitPose.position, hitPose.rotation);
                
                // 중요: ARAnchor를 붙여서 트래킹 드리프트를 방지
                var anchor = spawnedObject.AddComponent<ARAnchor>();
            }
            else
            {
                // 위치 재조정
                spawnedObject.transform.position = hitPose.position;
                spawnedObject.transform.rotation = hitPose.rotation;
            }
            
            // 3DGS는 보통 Y축(Up)이나 스케일이 데이터마다 다를 수 있어 보정이 필요할 수 있음
            // spawnedObject.transform.localScale = Vector3.one * 0.1f; 
        }
    }
}
```

### 요약: 추천 워크플로우

1.  **캡처:** Polycam, Luma AI 등으로 대상 공간 촬영.
2.  **학습/변환:** PC에서 3DGS 학습 후, **SuperSplat** 같은 도구를 써서 불필요한 부분 자르기(Crop) 및 용량 최적화.
3.  **Unity 임포트:** [UnityGaussianSplatting](https://github.com/aras-p/UnityGaussianSplatting) 패키지 설치 후 데이터 임포트.
4.  **AR 설정:** AR Foundation 설정이 된 씬에 3DGS 오브젝트 배치.
5.  **쉐이더/렌더러 조정:** 모바일 타겟 빌드 시 Compute Shader 지원 여부 확인 (Vulkan/Metal 설정).

```csharp
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

// 가상의 3DGS 렌더러 컴포넌트가 있다고 가정
// 실제로는 'UnityGaussianSplatting' 같은 에셋의 컴포넌트를 제어해야 함
public class ARGaussianSplatController : MonoBehaviour
{
    [SerializeField] GameObject gaussianSplatPrefab; // 3DGS 에셋
    [SerializeField] ARRaycastManager raycastManager;
    
    private GameObject spawnedObject;

    void Update()
    {
        if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)
        {
            PlaceSplatModel(Input.GetTouch(0).position);
        }
    }

    void PlaceSplatModel(Vector2 touchPosition)
    {
        List<ARRaycastHit> hits = new List<ARRaycastHit>();
        if (raycastManager.Raycast(touchPosition, hits, TrackableType.PlaneWithinPolygon))
        {
            Pose hitPose = hits[0].pose;

            if (spawnedObject == null)
            {
                spawnedObject = Instantiate(gaussianSplatPrefab, hitPose.position, hitPose.rotation);
                
                // 중요: ARAnchor를 붙여서 트래킹 드리프트를 방지
                var anchor = spawnedObject.AddComponent<ARAnchor>();
            }
            else
            {
                // 위치 재조정
                spawnedObject.transform.position = hitPose.position;
                spawnedObject.transform.rotation = hitPose.rotation;
            }
            
            // 3DGS는 보통 Y축(Up)이나 스케일이 데이터마다 다를 수 있어 보정이 필요할 수 있음
            // spawnedObject.transform.localScale = Vector3.one * 0.1f; 
        }
    }
}
```

