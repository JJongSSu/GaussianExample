"""
동영상 → PLY 파일 전체 파이프라인 실행 스크립트
한 번에 모든 과정을 자동으로 수행합니다.
"""

import os
import sys
import argparse
from pathlib import Path


def main():
    parser = argparse.ArgumentParser(
        description="동영상에서 3D Gaussian Splatting PLY 파일 생성 (전체 파이프라인)"
    )
    parser.add_argument(
        "--video", "-v",
        type=str,
        required=True,
        help="입력 동영상 파일 경로"
    )
    parser.add_argument(
        "--output", "-o",
        type=str,
        default="./project",
        help="프로젝트 출력 폴더 (기본값: ./project)"
    )
    parser.add_argument(
        "--frame-interval",
        type=int,
        default=10,
        help="프레임 추출 간격 (기본값: 10)"
    )
    parser.add_argument(
        "--max-frames",
        type=int,
        default=300,
        help="최대 추출 프레임 수 (기본값: 300)"
    )
    parser.add_argument(
        "--resize-width",
        type=int,
        default=1920,
        help="리사이즈 너비 (기본값: 1920)"
    )
    parser.add_argument(
        "--iterations",
        type=int,
        default=30000,
        help="학습 반복 횟수 (기본값: 30000)"
    )
    parser.add_argument(
        "--colmap-path",
        type=str,
        default="colmap",
        help="COLMAP 실행 파일 경로"
    )
    parser.add_argument(
        "--gs-path",
        type=str,
        default="./gaussian-splatting",
        help="gaussian-splatting 레포지토리 경로"
    )
    parser.add_argument(
        "--skip-frames",
        action="store_true",
        help="프레임 추출 단계 건너뛰기"
    )
    parser.add_argument(
        "--skip-colmap",
        action="store_true",
        help="COLMAP 단계 건너뛰기"
    )
    parser.add_argument(
        "--skip-training",
        action="store_true",
        help="학습 단계 건너뛰기"
    )
    
    args = parser.parse_args()
    
    # 경로 설정
    script_dir = Path(__file__).parent
    output_path = Path(args.output).resolve()
    
    print("=" * 70)
    print("3D Gaussian Splatting - 전체 파이프라인")
    print("=" * 70)
    print(f"\n입력 동영상: {args.video}")
    print(f"출력 폴더: {output_path}")
    print()
    
    # Step 1: 프레임 추출
    if not args.skip_frames:
        print("\n" + "=" * 70)
        print("STEP 1/3: 프레임 추출")
        print("=" * 70)
        
        from extract_frames import extract_frames
        extract_frames(
            video_path=args.video,
            output_dir=str(output_path),
            frame_interval=args.frame_interval,
            max_frames=args.max_frames,
            resize_width=args.resize_width
        )
    else:
        print("\n프레임 추출 건너뛰기...")
    
    # Step 2: COLMAP 전처리
    if not args.skip_colmap:
        print("\n" + "=" * 70)
        print("STEP 2/3: COLMAP 전처리")
        print("=" * 70)
        
        from run_colmap import run_colmap_preprocessing
        run_colmap_preprocessing(
            data_dir=str(output_path),
            colmap_path=args.colmap_path,
            use_gpu=True
        )
    else:
        print("\nCOLMAP 전처리 건너뛰기...")
    
    # Step 3: Gaussian Splatting 학습
    if not args.skip_training:
        print("\n" + "=" * 70)
        print("STEP 3/3: 3D Gaussian Splatting 학습")
        print("=" * 70)
        
        from train_gaussian import run_training, check_gaussian_splatting_repo, clone_gaussian_splatting, setup_gaussian_splatting
        
        gs_path = Path(args.gs_path).resolve()
        
        # 레포지토리 확인 및 클론
        if not check_gaussian_splatting_repo(str(gs_path)):
            print("\ngaussian-splatting 레포지토리가 없습니다. 클론 중...")
            clone_gaussian_splatting(str(gs_path))
            setup_gaussian_splatting(str(gs_path))
        
        run_training(
            source_path=str(output_path),
            gs_path=str(gs_path),
            iterations=args.iterations
        )
    else:
        print("\n학습 건너뛰기...")
    
    # 완료 메시지
    print("\n" + "=" * 70)
    print("전체 파이프라인 완료!")
    print("=" * 70)
    
    ply_path = output_path / "output" / "point_cloud" / f"iteration_{args.iterations}" / "point_cloud.ply"
    print(f"\n생성된 PLY 파일: {ply_path}")
    print("\nUnity에서 사용하기:")
    print("  1. Unity 에디터를 엽니다")
    print("  2. Tools → Gaussian Splats → Create GaussianSplatAsset")
    print("  3. Input PLY/SPZ File에 위 경로의 PLY 파일을 선택")
    print("  4. Create Asset 클릭")


if __name__ == "__main__":
    main()

