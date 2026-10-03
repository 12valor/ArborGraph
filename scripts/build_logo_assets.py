#!/usr/bin/env python3
"""
ArborGraph Production Asset Generator
Uses resvg_py and Pillow to produce:
- arborgraph-logo.svg
- arborgraph-logo.png (1024x1024)
- arborgraph.ico (16, 24, 32, 48, 64, 128, 256)
- Resources/ArborGraph.ico
- site/assets/logo.svg
- site/favicon.ico
"""

import os
import sys
from pathlib import Path
import resvg_py
from PIL import Image
import io

def get_svg_content(stroke_weight=3.5, apex_r=7.5, term_r=5.5, edge_dash=True, is_dark=False):
    primary_color = "#FFFFFF" if is_dark else "#111827"
    accent_color = "#3B82F6" if is_dark else "#005FB8"
    inner_cut = "#111827" if is_dark else "#FFFFFF"
    dash_attr = 'stroke-dasharray="3 3"' if edge_dash else ''

    return f'''<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 100 100" width="100%" height="100%">
  <!-- ArborGraph Brand Mark (Filesystem Analytics & Visualization) -->
  <g fill="none">
    <!-- Hierarchical Vertical Stem / Root -->
    <line x1="50" y1="84" x2="50" y2="56" stroke="{primary_color}" stroke-width="{stroke_weight}" stroke-linecap="round"/>
    
    <!-- Bilateral Branches -->
    <path d="M 50 56 C 42 56 30 52 26 38" stroke="{primary_color}" stroke-width="{stroke_weight}" stroke-linecap="round"/>
    <path d="M 50 56 C 58 56 70 52 74 38" stroke="{primary_color}" stroke-width="{stroke_weight}" stroke-linecap="round"/>
    
    <!-- Central Vertical Hierarchy to Apex Node -->
    <line x1="50" y1="56" x2="50" y2="24" stroke="{primary_color}" stroke-width="{stroke_weight}" stroke-linecap="round"/>
    
    <!-- Relational Graph Edge -->
    <path d="M 26 38 Q 50 30 74 38" stroke="{accent_color}" stroke-width="{max(2.0, stroke_weight * 0.6):.1f}" {dash_attr} stroke-linecap="round"/>
    
    <!-- Terminal Data Nodes -->
    <circle cx="26" cy="38" r="{term_r}" fill="{primary_color}"/>
    <circle cx="74" cy="38" r="{term_r}" fill="{primary_color}"/>
    
    <!-- Apex Storage Anchor Node -->
    <circle cx="50" cy="22" r="{apex_r}" fill="{accent_color}"/>
    <circle cx="50" cy="22" r="{apex_r * 0.35:.1f}" fill="{inner_cut}"/>
  </g>
</svg>'''

def main():
    root_dir = Path(__file__).resolve().parent.parent

    # 1. Output main vector SVG
    master_svg = get_svg_content(stroke_weight=3.5, apex_r=7.5, term_r=5.5, edge_dash=True, is_dark=False)
    
    svg_paths = [
        root_dir / "arborgraph-logo.svg",
        root_dir / "site" / "assets" / "logo.svg",
        root_dir / "Resources" / "ArborGraph.svg"
    ]
    
    for p in svg_paths:
        p.parent.mkdir(parents=True, exist_ok=True)
        with open(p, "w", encoding="utf-8") as f:
            f.write(master_svg)
        print(f"[+] Saved vector SVG: {p}")

    # 2. Render high-res PNG (1024x1024 and 512x512)
    png_bytes_1024 = resvg_py.svg_to_bytes(master_svg, width=1024, height=1024)
    png_paths = [
        root_dir / "arborgraph-logo.png",
        root_dir / "site" / "assets" / "logo.png"
    ]
    for p in png_paths:
        with open(p, "wb") as f:
            f.write(png_bytes_1024)
        print(f"[+] Saved high-res PNG: {p}")

    # 3. Render tuned multi-resolution icon layers for Windows ICO
    # At tiny sizes (16, 24), optical compensation increases line weight so it remains distinct
    sizes = [16, 24, 32, 48, 64, 128, 256]
    icon_images = []

    for s in sizes:
        if s <= 16:
            sw = 7.0
            ar = 10.0
            tr = 8.0
            dash = False
        elif s <= 24:
            sw = 6.0
            ar = 9.0
            tr = 7.0
            dash = False
        elif s <= 32:
            sw = 5.0
            ar = 8.5
            tr = 6.5
            dash = True
        elif s <= 48:
            sw = 4.5
            ar = 8.0
            tr = 6.0
            dash = True
        elif s <= 64:
            sw = 4.0
            ar = 7.5
            tr = 5.5
            dash = True
        else:
            sw = 3.5
            ar = 7.5
            tr = 5.5
            dash = True

        opt_svg = get_svg_content(stroke_weight=sw, apex_r=ar, term_r=tr, edge_dash=dash)
        png_data = resvg_py.svg_to_bytes(opt_svg, width=s, height=s)
        img = Image.open(io.BytesIO(png_data)).convert("RGBA")
        icon_images.append(img)
        print(f"  - Rendered optical layer {s}x{s}")

    # 4. Save multi-resolution Windows ICO
    ico_paths = [
        root_dir / "arborgraph.ico",
        root_dir / "Resources" / "ArborGraph.ico",
        root_dir / "site" / "favicon.ico"
    ]
    for p in ico_paths:
        p.parent.mkdir(parents=True, exist_ok=True)
        # Pillow save with sizes
        icon_images[-1].save(
            p,
            format="ICO",
            sizes=[(img.width, img.height) for img in icon_images],
            append_images=icon_images[:-1]
        )
        print(f"[+] Saved multi-resolution ICO ({len(icon_images)} sizes): {p}")

    print("\nAll ArborGraph brand assets successfully generated!")

if __name__ == "__main__":
    main()
