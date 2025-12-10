"""
동영상에서 프레임을 추출하는 스크립트
3D Gaussian Splatting 학습을 위한 이미지 데이터 준비
"""

import os
import sys
import argparse
import cv2
from pathlib import Path


def extract_frames(
    video_path: str,
    output_dir: str,
    frame_interval: int = 10,
    max_frames: int = None,
    resize_width: int = None
):
    """
    동영상에서 프레임을 추출합니다.
    
    Args:
        video_path: 입력 동영상 경로
        output_dir: 출력 이미지 폴더 경로
        frame_interval: 프레임 추출 간격 (기본값: 10프레임마다 1장)
        max_frames: 최대 추출 프레임 수 (None이면 제한 없음)
        resize_width: 리사이즈할 너비 (None이면 원본 크기 유지)
    """
    
    # 출력 폴더 생성
    output_path = Path(output_dir)
    images_path = output_path / "input"
    images_path.mkdir(parents=True, exist_ok=True)
    
    # 동영상 열기
    cap = cv2.VideoCapture(video_path)
    if not cap.isOpened():
        print(f"오류: 동영상을 열 수 없습니다 - {video_path}")
        sys.exit(1)
    
    # 동영상 정보
    total_frames = int(cap.get(cv2.CAP_PROP_FRAME_COUNT))
    fps = cap.get(cv2.CAP_PROP_FPS)
    width = int(cap.get(cv2.CAP_PROP_FRAME_WIDTH))
    height = int(cap.get(cv2.CAP_PROP_FRAME_HEIGHT))
    
    print(f"동영상 정보:")
    print(f"  - 총 프레임: {total_frames}")
    print(f"  - FPS: {fps:.2f}")
    print(f"  - 해상도: {width}x{height}")
    print(f"  - 프레임 추출 간격: {frame_interval}")
    
    # 예상 추출 프레임 수
    expected_frames = total_frames // frame_interval
    if max_frames:
        expected_frames = min(expected_frames, max_frames)
    print(f"  - 예상 추출 프레임: {expected_frames}")
    print()
    
    # 프레임 추출
    frame_idx = 0
    saved_count = 0
    
    while True:
        ret, frame = cap.read()
        if not ret:
            break
        
        if frame_idx % frame_interval == 0:
            # 리사이즈
            if resize_width:
                aspect_ratio = height / width
                new_height = int(resize_width * aspect_ratio)
                frame = cv2.resize(frame, (resize_width, new_height))
            
            # 저장
            filename = f"frame_{saved_count:05d}.jpg"
            filepath = images_path / filename
            cv2.imwrite(str(filepath), frame, [cv2.IMWRITE_JPEG_QUALITY, 95])
            
            saved_count += 1
            
            if saved_count % 10 == 0:
                print(f"  추출 진행: {saved_count}/{expected_frames}")
            
            if max_frames and saved_count >= max_frames:
                break
        
        frame_idx += 1
    
    cap.release()
    
    print()
    print(f"프레임 추출 완료!")
    print(f"  - 저장된 이미지: {saved_count}장")
    print(f"  - 저장 위치: {images_path}")
    
    return str(images_path)


def main():
    parser = argparse.ArgumentParser(
        description="3D Gaussian Splatting 학습을 위한 동영상 프레임 추출"
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
        default="./data",
        help="출력 폴더 경로 (기본값: ./data)"
    )
    parser.add_argument(
        "--interval", "-i",
        type=int,
        default=10,
        help="프레임 추출 간격 (기본값: 10)"
    )
    parser.add_argument(
        "--max-frames", "-m",
        type=int,
        default=None,
        help="최대 추출 프레임 수 (기본값: 제한 없음)"
    )
    parser.add_argument(
        "--resize-width", "-r",
        type=int,
        default=None,
        help="리사이즈할 너비 (기본값: 원본 크기 유지, 권장: 1920 또는 1280)"
    )
    
    args = parser.parse_args()
    
    print("=" * 60)
    print("3D Gaussian Splatting - 프레임 추출")
    print("=" * 60)
    print()
    
    extract_frames(
        video_path=args.video,
        output_dir=args.output,
        frame_interval=args.interval,
        max_frames=args.max_frames,
        resize_width=args.resize_width
    )


if __name__ == "__main__":
    main()

