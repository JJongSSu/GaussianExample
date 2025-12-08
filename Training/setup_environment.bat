@echo off
chcp 65001 > nul
echo ============================================================
echo 3D Gaussian Splatting 환경 설정
echo ============================================================
echo.

REM Python 확인
python --version > nul 2>&1
if errorlevel 1 (
    echo [오류] Python이 설치되어 있지 않습니다.
    echo Python 3.8 이상을 설치해주세요: https://www.python.org/downloads/
    pause
    exit /b 1
)

echo [1/5] Python 버전 확인...
python --version

echo.
echo [2/5] pip 업그레이드...
python -m pip install --upgrade pip

echo.
echo [3/5] 기본 패키지 설치...
pip install opencv-python numpy torch torchvision torchaudio --index-url https://download.pytorch.org/whl/cu121
pip install plyfile tqdm Pillow

echo.
echo [4/5] gaussian-splatting 레포지토리 클론...
if not exist "gaussian-splatting" (
    git clone https://github.com/graphdeco-inria/gaussian-splatting.git --recursive
) else (
    echo gaussian-splatting 폴더가 이미 존재합니다.
)

echo.
echo [5/5] gaussian-splatting 서브모듈 설치...
cd gaussian-splatting

if exist "./gaussian-splatting/submodules/diff-gaussian-rasterization" (
    echo diff-gaussian-rasterization 설치 중...
    pip install ./gaussian-splatting/submodules/diff-gaussian-rasterization --no-build-isolation
)

if exist "./gaussian-splatting/submodules/simple-knn" (
    echo simple-knn 설치 중...
    pip install ./gaussian-splatting/submodules/simple-knn --no-build-isolation
)

cd ..

echo.
echo ============================================================
echo 환경 설정 완료!
echo ============================================================
echo.
echo 다음 단계:
echo   1. COLMAP 설치: https://github.com/colmap/colmap/releases
echo   2. COLMAP을 PATH에 추가하거나 경로를 기억해두세요
echo   3. run_pipeline.bat 를 실행하여 동영상을 변환하세요
echo.
pause

