"""
COLMAP을 사용하여 이미지에서 카메라 파라미터를 추출하는 스크립트
3D Gaussian Splatting 학습을 위한 전처리
"""

import os
import sys
import argparse
import subprocess
from pathlib import Path


def run_command(cmd: list, desc: str = ""):
    """명령어를 실행하고 결과를 출력합니다."""
    print(f"\n{'=' * 60}")
    print(f"실행: {desc}")
    print(f"명령어: {' '.join(cmd)}")
    print("=" * 60)

    result = subprocess.run(cmd, capture_output=False)

    if result.returncode != 0:
        print(f"오류 발생! 종료 코드: {result.returncode}")
        return False
    return True


def run_colmap_preprocessing(
    data_dir: str,
    colmap_path: str = "colmap",
    use_gpu: bool = True,
    camera_model: str = "SIMPLE_RADIAL"
):
    """
    COLMAP을 사용하여 SfM(Structure from Motion) 처리를 수행합니다.

    Args:
        data_dir: 입력 이미지가 있는 데이터 디렉토리 (input 폴더 포함)
        colmap_path: COLMAP 실행 파일 경로
        use_gpu: GPU 사용 여부
        camera_model: 카메라 모델 (OPENCV, PINHOLE 등)
    """

    data_path = Path(data_dir)
    images_path = data_path / "input"

    if not images_path.exists():
        print(f"오류: 이미지 폴더를 찾을 수 없습니다 - {images_path}")
        sys.exit(1)

    # COLMAP 작업 디렉토리 생성
    distorted_path = data_path / "distorted" / "sparse"
    distorted_path.mkdir(parents=True, exist_ok=True)

    database_path = data_path / "distorted" / "database.db"

    print(f"\n데이터 경로: {data_path}")
    print(f"이미지 경로: {images_path}")

    # 이미지 수 확인
    image_files = list(images_path.glob("*.jpg")) + list(images_path.glob("*.png"))
    print(f"발견된 이미지: {len(image_files)}장")

    if len(image_files) < 10:
        print("경고: 이미지가 너무 적습니다. 최소 50장 이상을 권장합니다.")

    gpu_flag = "1" if use_gpu else "0"

    # Step 1: Feature Extraction
    print("\n" + "=" * 60)
    print("Step 1/4: 특징점 추출 (Feature Extraction)")
    print("=" * 60)

    cmd_feature = [
        colmap_path, "feature_extractor",
        "--database_path", str(database_path),
        "--image_path", str(images_path),
        "--ImageReader.single_camera", "1",
        "--ImageReader.camera_model", camera_model,
        # "--SiftExtraction.use_gpu", gpu_flag
    ]

    if not run_command(cmd_feature, "특징점 추출"):
        sys.exit(1)

    # Step 2: Feature Matching
    print("\n" + "=" * 60)
    print("Step 2/4: 특징점 매칭 (Feature Matching)")
    print("=" * 60)

    cmd_matcher = [
        colmap_path, "exhaustive_matcher",
        "--database_path", str(database_path)
        # "--SiftMatching.use_gpu", gpu_flag
    ]

    if not run_command(cmd_matcher, "특징점 매칭"):
        sys.exit(1)

    # Step 3: Sparse Reconstruction (Mapper)
    print("\n" + "=" * 60)
    print("Step 3/4: 희소 재구성 (Sparse Reconstruction)")
    print("=" * 60)

    cmd_mapper = [
        colmap_path, "mapper",
        "--database_path", str(database_path),
        "--image_path", str(images_path),
        "--output_path", str(distorted_path),
        # Bundle Adjustment 안정성 향상 옵션
        "--Mapper.ba_global_function_tolerance", "0.000001",
        # 최소 관측 수 감소 (적은 이미지에서도 작동)
        "--Mapper.ba_local_num_images", "6",
        "--Mapper.ba_global_points_ratio", "1.1",
        "--Mapper.ba_global_max_num_iterations", "50",
        "--Mapper.ba_global_max_refinements", "3",
        # 초기화 안정성 향상
        "--Mapper.init_min_num_inliers", "50",
        "--Mapper.init_max_error", "4.0",
        # 삼각측량 조건 완화
        "--Mapper.tri_min_angle", "1.5",
        "--Mapper.tri_ignore_two_view_tracks", "0"
    ]

    if not run_command(cmd_mapper, "희소 재구성"):
        sys.exit(1)

    # Step 4: Image Undistortion
    print("\n" + "=" * 60)
    print("Step 4/4: 이미지 왜곡 보정 (Image Undistortion)")
    print("=" * 60)

    cmd_undistort = [
        colmap_path, "image_undistorter",
        "--image_path", str(images_path),
        "--input_path", str(distorted_path / "0"),
        "--output_path", str(data_path),
        "--output_type", "COLMAP"
    ]

    if not run_command(cmd_undistort, "이미지 왜곡 보정"):
        sys.exit(1)

    # 결과 확인
    sparse_path = data_path / "sparse" / "0"
    if sparse_path.exists():
        print("\n" + "=" * 60)
        print("COLMAP 전처리 완료!")
        print("=" * 60)
        print(f"\n결과물 위치:")
        print(f"  - 희소 모델: {sparse_path}")
        print(f"  - 보정된 이미지: {data_path / 'images'}")
    else:
        # sparse/0이 없으면 수동으로 생성
        sparse_path.mkdir(parents=True, exist_ok=True)

        # distorted/sparse/0에서 복사
        import shutil
        src_sparse = distorted_path / "0"
        if src_sparse.exists():
            for f in src_sparse.iterdir():
                shutil.copy2(f, sparse_path)
            print(f"\n희소 모델 복사 완료: {sparse_path}")

    print(f"\n다음 단계: 3D Gaussian Splatting 학습을 실행하세요.")
    print(f"  python train_gaussian.py --source_path {data_path}")


def main():
    parser = argparse.ArgumentParser(
        description="COLMAP을 사용한 SfM 전처리"
    )
    parser.add_argument(
        "--data", "-d",
        type=str,
        required=True,
        help="데이터 디렉토리 경로 (input 폴더가 있는 위치)"
    )
    parser.add_argument(
        "--colmap-path",
        type=str,
        default="colmap",
        help="COLMAP 실행 파일 경로 (기본값: colmap)"
    )
    parser.add_argument(
        "--no-gpu",
        action="store_true",
        help="GPU 사용 안 함"
    )
    parser.add_argument(
        "--camera-model",
        type=str,
        default="SIMPLE_RADIAL",
        choices=["SIMPLE_RADIAL", "PINHOLE", "OPENCV", "RADIAL"],
        help="카메라 모델 (기본값: SIMPLE_RADIAL - 가장 안정적)"
    )

    args = parser.parse_args()

    print("=" * 60)
    print("3D Gaussian Splatting - COLMAP 전처리")
    print("=" * 60)

    run_colmap_preprocessing(
        data_dir=args.data,
        colmap_path=args.colmap_path,
        use_gpu=not args.no_gpu,
        camera_model=args.camera_model
    )


if __name__ == "__main__":
    main()

