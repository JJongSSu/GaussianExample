# 3D Gaussian Splatting 학습 파이프라인

현장 동영상에서 3D Gaussian Splatting PLY 파일을 생성하는 도구입니다.
생성된 PLY 파일은 Unity의 `UnityGaussianSplatting` 패키지에서 사용할 수 있습니다.

## 📋 사전 요구사항

### 필수 설치

1. **Python 3.8+**
   - https://www.python.org/downloads/
   - 설치 시 "Add Python to PATH" 체크

2. **NVIDIA GPU + CUDA**
   - CUDA 11.8 이상 권장
   - https://developer.nvidia.com/cuda-downloads

3. **Git**
   - https://git-scm.com/downloads

4. **COLMAP**
   - https://github.com/colmap/colmap/releases
   - Windows: `COLMAP-x.x-windows-cuda.zip` 다운로드
   - 압축 해제 후 PATH에 추가하거나 경로 기억

5. **Visual Studio Build Tools** (Windows)
   - https://visualstudio.microsoft.com/visual-cpp-build-tools/
   - "Desktop development with C++" 워크로드 설치

## 🚀 빠른 시작

### 방법 1: 배치 파일 사용 (Windows)

```batch
# 1. 환경 설정 (최초 1회)
setup_environment.bat

# 2. 동영상 변환
run_pipeline.bat C:\path\to\your\video.mp4
```

### 방법 2: Python 스크립트 직접 실행

```bash
# 1. 환경 설정
pip install -r requirements.txt

# 2. gaussian-splatting 클론
git clone https://github.com/graphdeco-inria/gaussian-splatting.git --recursive

# 3. 서브모듈 설치
pip install gaussian-splatting/submodules/diff-gaussian-rasterization
pip install gaussian-splatting/submodules/simple-knn

# 4. 전체 파이프라인 실행
python run_full_pipeline.py --video "C:\path\to\video.mp4" --output "./my_project"
```

## 📁 파이프라인 단계별 설명

### Step 1: 프레임 추출

동영상에서 학습에 사용할 이미지 프레임을 추출합니다.

```bash
python extract_frames.py --video "video.mp4" --output "./data" --interval 10 --max-frames 300
```

**옵션:**
- `--video, -v`: 입력 동영상 경로 (필수)
- `--output, -o`: 출력 폴더 (기본값: ./data)
- `--interval, -i`: 프레임 추출 간격 (기본값: 10)
- `--max-frames, -m`: 최대 프레임 수 (기본값: 제한없음, 권장: 200-400)
- `--resize-width, -r`: 리사이즈 너비 (권장: 1920 또는 1280)

### Step 2: COLMAP 전처리

추출된 이미지에서 카메라 파라미터를 추출합니다 (Structure from Motion).

```bash
python run_colmap.py --data "./data" --colmap-path "colmap"
```

**옵션:**
- `--data, -d`: 데이터 디렉토리 (input 폴더가 있는 위치)
- `--colmap-path`: COLMAP 실행 파일 경로
- `--no-gpu`: GPU 사용 안 함
- `--camera-model`: 카메라 모델 (OPENCV, PINHOLE 등)

### Step 3: Gaussian Splatting 학습

COLMAP 결과를 사용하여 3D Gaussian Splatting 모델을 학습합니다.

```bash
python train_gaussian.py --source_path "./data" --gs_path "./gaussian-splatting" --iterations 30000
```

**옵션:**
- `--source_path, -s`: COLMAP 처리된 데이터 경로
- `--gs_path, -g`: gaussian-splatting 레포지토리 경로
- `--output_path, -o`: 출력 경로
- `--iterations, -i`: 학습 반복 횟수 (기본값: 30000)
- `--clone`: 레포지토리 자동 클론
- `--setup`: 환경 설정 실행

## 📹 좋은 결과를 위한 촬영 팁

1. **충분한 커버리지**: 대상을 360도 또는 최소 180도 이상 돌면서 촬영
2. **일정한 속도**: 너무 빠르지 않게 천천히 이동
3. **충분한 오버랩**: 연속 프레임 간 70-80% 겹침 권장
4. **안정적인 조명**: 일정한 조명 환경 유지
5. **움직이는 물체 피하기**: 사람, 차량 등 움직이는 물체 최소화
6. **고해상도**: 1080p 이상 권장

## 📂 출력 구조

```
project/
├── input/                          # 추출된 프레임 이미지
├── distorted/                      # COLMAP 중간 결과
│   ├── database.db
│   └── sparse/
├── images/                         # 왜곡 보정된 이미지
├── sparse/                         # 최종 COLMAP 결과
│   └── 0/
│       ├── cameras.bin
│       ├── images.bin
│       └── points3D.bin
└── output/                         # 학습 결과
    └── point_cloud/
        ├── iteration_7000/
        │   └── point_cloud.ply
        ├── iteration_15000/
        │   └── point_cloud.ply
        └── iteration_30000/
            └── point_cloud.ply     ← Unity에서 사용할 파일
```

## 🎮 Unity에서 사용하기

1. Unity 에디터에서 `Tools → Gaussian Splats → Create GaussianSplatAsset` 메뉴 열기
2. `Input PLY/SPZ File`에 생성된 `point_cloud.ply` 파일 선택
3. 압축 옵션 선택 (Very Low도 충분히 좋음)
4. `Create Asset` 클릭
5. 생성된 에셋을 `GaussianSplatRenderer` 컴포넌트에 할당

## ⚠️ 문제 해결

### COLMAP이 실패하는 경우
- 이미지가 너무 적음 → 프레임 추출 간격 줄이기 (--interval 5)
- 특징점이 부족함 → 더 선명한 동영상 사용
- 메모리 부족 → 이미지 크기 줄이기 (--resize-width 1280)

### 학습이 실패하는 경우
- CUDA 오류 → CUDA 버전 확인, GPU 드라이버 업데이트
- 메모리 부족 → 이미지 수 줄이기, 해상도 낮추기
- 서브모듈 오류 → `pip install` 명령 다시 실행

### 결과 품질이 좋지 않은 경우
- 더 많은 이미지 사용 (300-500장)
- 학습 반복 횟수 늘리기 (--iterations 50000)
- 더 높은 해상도 사용

## 📚 참고 자료

- [3D Gaussian Splatting 논문](https://repo-sam.inria.fr/fungraph/3d-gaussian-splatting/)
- [공식 gaussian-splatting GitHub](https://github.com/graphdeco-inria/gaussian-splatting)
- [UnityGaussianSplatting GitHub](https://github.com/aras-p/UnityGaussianSplatting)
- [COLMAP 문서](https://colmap.github.io/index.html)

