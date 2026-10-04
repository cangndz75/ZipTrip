"""BACKDROP-SLICE-01: authors the Golden Lv1 travel-world textures used by PackingTable.

Writes (Assets/Art/Backdrop/Textures):
  T_Backdrop_WhitewashWood.png  1024 x 2048 RGB  - table surface covering SurfaceRect (12 x 24 world units, no tiling)
  T_Backdrop_Props.png          1024 x 1024 RGBA - edge-prop atlas, soft contact shadows and rotation baked in

The prop drawings and their "quiet" treatment come from the locked STYLE-FRAME-01 paintover
(make_golden_lv1_style_frame.py), so runtime props match the approved frame. Prints the atlas rects (texels) that
PackingTable.Props mirrors.
"""
import math
import os
import random
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

sys.path.insert(0, os.path.dirname(__file__))
import make_golden_lv1_style_frame as sf  # noqa: E402

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT = os.path.join(ROOT, "Assets", "Art", "Backdrop", "Textures")

SURFACE_W, SURFACE_H = 1024, 2048
WORLD_W, WORLD_H = 12.0, 24.0          # PackingTable.SurfaceRect size
TEXELS = SURFACE_W / WORLD_W             # 85.3 texels per world unit
BASE = np.array([233, 223, 205], np.float32)  # also PackingTable.SurfaceTone (edge fade target)
ATLAS = 1024
ATLAS_SCALE = 1.0                        # props drawn at 1x style-frame px (~160 texels per world unit)


def surface():
    rng = np.random.default_rng(11)
    random.seed(11)
    h, w = SURFACE_H, SURFACE_W
    ys, xs = np.mgrid[0:h, 0:w].astype(np.float32)
    img = np.empty((h, w, 3), np.float32)
    img[:] = BASE
    # Broad painted boards (3.4 units) with the centre board under the Source Tray, so no seam crosses it.
    board = 3.4 * TEXELS
    phase = (xs - w / 2 + board / 2) / board
    index = np.floor(phase)
    local = phase - index
    tones = {int(k): 1.0 + rng.uniform(-0.018, 0.014) for k in range(-4, 5)}
    img *= np.vectorize(lambda k: tones.get(int(k), 1.0))(index)[:, :, None]
    # grain: low-frequency streaks along each board (no high-frequency noise)
    streak = rng.normal(0, 1, (1, w)).repeat(h, 0)
    streak = np.array(Image.fromarray(((streak + 4) * 30).clip(0, 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(3)), np.float32)
    wave = np.sin(ys / (0.9 * TEXELS) + xs / 37.0) * 0.5 + 0.5
    img *= 0.985 + 0.012 * wave[:, :, None] + (streak[:, :, None] / 255.0 - 0.47) * 0.03
    # soft board seams (low contrast)
    seam = np.exp(-((np.minimum(local, 1 - local) * board) ** 2) / (2 * 1.6 ** 2))
    img *= (1.0 - 0.07 * seam)[:, :, None]
    # whitewash: broad brush blotches
    blot = rng.normal(0, 1, (h // 16, w // 16))
    blot = np.array(Image.fromarray(((blot + 3) * 42).clip(0, 255).astype(np.uint8)).resize((w, h), Image.BICUBIC)
                    .filter(ImageFilter.GaussianBlur(18)), np.float32) / 255.0
    img *= (0.99 + (blot - 0.5) * 0.035)[:, :, None]
    # window light from the top-left: warmer/brighter there, a touch cooler/darker toward the bottom-right
    g = (xs / w) * 0.35 + (ys / h) * 0.65
    img *= (1.035 - 0.07 * g)[:, :, None]
    img[:, :, 0] *= 1.0 + 0.012 * (0.5 - g)
    img[:, :, 2] *= 1.0 - 0.012 * (0.5 - g)
    # baked dappled leaf shadow near the left / right edges, below the suitcase
    leaves = Image.new("L", (w, h), 0)
    d = ImageDraw.Draw(leaves)
    for cx, cy, n in ((0.03, 0.70, 14), (0.98, 0.64, 12), (0.95, 0.86, 10)):
        for _ in range(n):
            a = random.uniform(0, math.pi)
            r = random.uniform(30, 80)
            px, py = cx * w + math.cos(a) * r * 1.6, cy * h + math.sin(a) * r
            d.ellipse((px - 30, py - 10, px + 30, py + 10), fill=255)
    leaves = np.array(leaves.filter(ImageFilter.GaussianBlur(14)), np.float32) / 255.0
    img = img * (1 - 0.11 * leaves[:, :, None]) + np.array([70, 78, 52], np.float32) * 0.11 * leaves[:, :, None]
    # fade to the flat base tone over the outer world unit, matching the fallback table colour beyond the rect
    edge = np.minimum.reduce([xs, w - 1 - xs, ys, h - 1 - ys]) / TEXELS
    t = np.clip(edge, 0, 1)[:, :, None]
    img = img * t + BASE * (1 - t)
    img += rng.normal(0, 0.8, img.shape)  # dither against banding only
    return Image.fromarray(img.clip(0, 255).astype(np.uint8), "RGB")


def prop_cell(tile, angle, blur=12, offset=(8, 12), opacity=0.2):
    """Rotated, quieted prop with its soft contact shadow, trimmed to content."""
    rotated = tile.rotate(angle, resample=Image.BICUBIC, expand=True)
    pad = blur * 3 + max(offset)
    cell = Image.new("RGBA", (rotated.width + 2 * pad, rotated.height + 2 * pad), (0, 0, 0, 0))
    alpha = rotated.split()[3].point(lambda a: int(a * opacity))
    shade = Image.new("RGBA", rotated.size, sf.SHADOW + (0,))
    shade.putalpha(alpha)
    holder = Image.new("RGBA", cell.size, (0, 0, 0, 0))
    holder.alpha_composite(shade, (pad + offset[0], pad + offset[1]))
    holder = holder.filter(ImageFilter.GaussianBlur(blur))
    cell.alpha_composite(holder)
    cell.alpha_composite(rotated, (pad, pad))
    return cell.crop(cell.getbbox())


def atlas():
    random.seed(7)
    np.random.seed(7)
    s = ATLAS_SCALE
    hat = 0.72  # mostly cropped off screen: fewer texels
    cells = [
        ("BoardingPass", s, prop_cell(sf.quiet(sf.boarding_pass(), scale=s), -9, opacity=0.2)),
        ("Postcard", s, prop_cell(sf.quiet(sf.postcard(), scale=s), 8, opacity=0.2)),
        ("FoldedMap", s, prop_cell(sf.quiet(sf.folded_map(), scale=s), -14, opacity=0.18)),
        ("OliveSprig", s, prop_cell(sf.quiet(sf.olive_sprig(), scale=s), 160, blur=10, opacity=0.14)),
        ("StrawHat", hat, prop_cell(sf.quiet(sf.hat_brim(), scale=hat), 0, blur=14, opacity=0.16)),
    ]
    img = Image.new("RGBA", (ATLAS, ATLAS), (0, 0, 0, 0))
    x = y = row = 0
    rects = []
    for name, scale, cell in sorted(cells, key=lambda c: -c[2].height):
        if x + cell.width > ATLAS:
            x, y, row = 0, y + row + 4, 0
        if y + cell.height > ATLAS:
            raise SystemExit("atlas overflow at " + name)
        img.alpha_composite(cell, (x, y))
        rects.append((name, x, y, cell.width, cell.height, scale))
        x += cell.width + 4
        row = max(row, cell.height)
    return img, rects


def main():
    os.makedirs(OUT, exist_ok=True)
    surface().save(os.path.join(OUT, "T_Backdrop_WhitewashWood.png"))
    img, rects = atlas()
    img.save(os.path.join(OUT, "T_Backdrop_Props.png"))
    # one style-frame px = 1/136.4 world units at 1080x2340; the frame drew props at quiet scale 0.85
    for name, x, y, w, h, scale in rects:
        k = 0.85 / scale / 136.4
        print('%-13s texels (%d, %d, %d, %d)  world %.3f x %.3f' % (name, x, y, w, h, w * k, h * k))


if __name__ == "__main__":
    main()
