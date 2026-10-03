#!/usr/bin/env python3
"""
Convert SVG to PNG using cairosvg.
"""

import sys
import argparse
from pathlib import Path

try:
    import resvg_py
    HAS_RESVG = True
except ImportError:
    HAS_RESVG = False

try:
    import cairosvg
    HAS_CAIRO = True
except Exception:
    HAS_CAIRO = False

if not HAS_RESVG and not HAS_CAIRO:
    print("Error: Neither resvg_py nor working cairosvg found.")
    print("Install with: pip install resvg_py")
    sys.exit(1)


def svg_to_png(svg_path: str, png_path: str, width: int = 1024, height: int = 1024) -> bool:
    """
    Convert SVG file to PNG.

    Args:
        svg_path: Path to input SVG file
        png_path: Path to output PNG file
        width: Output width in pixels
        height: Output height in pixels

    Returns:
        True if successful, False otherwise
    """
    if HAS_RESVG:
        try:
            with open(svg_path, 'r', encoding='utf-8') as f:
                svg_data = f.read()
            png_bytes = resvg_py.svg_to_bytes(svg_data, width=width, height=height)
            with open(png_path, 'wb') as f:
                f.write(png_bytes)
            print(f"✓ Converted via resvg: {svg_path} -> {png_path}")
            return True
        except Exception as e:
            print(f"resvg conversion error: {e}")
            if not HAS_CAIRO:
                return False

    if HAS_CAIRO:
        try:
            cairosvg.svg2png(
                url=svg_path,
                write_to=png_path,
                output_width=width,
                output_height=height
            )
            print(f"✓ Converted via cairo: {svg_path} -> {png_path}")
            return True
        except Exception as e:
            print(f"Error converting SVG to PNG: {e}")
            return False

    return False


def main():
    parser = argparse.ArgumentParser(description="Convert SVG to PNG")
    parser.add_argument("svg_file", help="Path to SVG file")
    parser.add_argument("--output", "-o", help="Output PNG path (default: same name with .png)")
    parser.add_argument("--width", "-w", type=int, default=1024, help="Output width (default: 1024)")
    parser.add_argument("--height", "-H", type=int, default=1024, help="Output height (default: 1024)")

    args = parser.parse_args()

    svg_path = Path(args.svg_file)
    if not svg_path.exists():
        print(f"Error: SVG file not found: {svg_path}")
        sys.exit(1)

    if args.output:
        png_path = Path(args.output)
    else:
        png_path = svg_path.with_suffix('.png')

    success = svg_to_png(str(svg_path), str(png_path), args.width, args.height)
    sys.exit(0 if success else 1)


if __name__ == "__main__":
    main()
