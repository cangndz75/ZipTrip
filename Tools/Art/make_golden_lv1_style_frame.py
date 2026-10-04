"""STYLE-FRAME-01 paintover for Golden Lv1 (review artwork, not shipping UI).

Inputs (from GoldenLv1LayoutReview.CaptureGoldenLv1Layout, Builds/golden-lv1/style):
  style-fg-black.png / style-fg-white.png  difference matte of the real Unity render (no table, no post)
  style-layout.json                         screen rects of UI panels and items
Outputs (same folder): style-frame-clean.png, style-frame-annotated.png, current-vs-style-frame.png

Everything painted here maps to a planned owner (see Docs/Product/golden-lv1-style-frame.md): the suitcase,
interior and sweater are the real render; item paintovers stand in for ART-GATE-02B meshes; the backdrop and props
for BACKDROP-SLICE; the cards for UI-SLICE-01.
"""
import json
import math
import os
import random
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
STYLE = os.path.join(ROOT, "Builds", "golden-lv1", "style")
LAYOUT = os.path.join(ROOT, "Builds", "golden-lv1", "layout")
FONTS = os.path.join(ROOT, "Assets", "Art", "Fonts", "BricolageGrotesque")
W, H = 1080, 2340
random.seed(7)
np.random.seed(7)

INK = (47, 58, 60)
MUTED = (122, 116, 104)
CREAM = (247, 241, 230)
PAPER = (251, 247, 238)
TEAL = (78, 159, 162)
DEEP = (49, 81, 93)
CORAL = (224, 122, 95)
TERRA = (195, 111, 88)
MUSTARD = (215, 170, 88)
SHADOW = (58, 44, 30)


def font(weight, size):
    name = "BricolageGrotesque-ExtraBold.ttf" if weight == "bold" else "BricolageGrotesque-SemiBold.ttf"
    return ImageFont.truetype(os.path.join(FONTS, name), size)


def layer():
    return Image.new("RGBA", (W, H), (0, 0, 0, 0))


def soft_shadow(base, box, radius, blur, offset, opacity, color=SHADOW):
    mask = Image.new("L", (W, H), 0)
    x0, y0, x1, y1 = box
    ImageDraw.Draw(mask).rounded_rectangle((x0 + offset[0], y0 + offset[1], x1 + offset[0], y1 + offset[1]), radius,
                                           fill=int(255 * opacity))
    mask = mask.filter(ImageFilter.GaussianBlur(blur))
    shade = Image.new("RGBA", (W, H), color + (0,))
    shade.putalpha(mask)
    base.alpha_composite(shade)


def rotated_paste(base, tile, center, angle):
    rotated = tile.rotate(angle, resample=Image.BICUBIC, expand=True)
    base.alpha_composite(rotated, (int(center[0] - rotated.width / 2), int(center[1] - rotated.height / 2)))


def tile_shadow(base, tile, center, angle, blur=14, offset=(8, 12), opacity=0.32):
    rotated = tile.rotate(angle, resample=Image.BICUBIC, expand=True)
    alpha = rotated.split()[3].point(lambda a: int(a * opacity))
    shade = Image.new("RGBA", rotated.size, SHADOW + (0,))
    shade.putalpha(alpha)
    shade = shade.filter(ImageFilter.GaussianBlur(blur))
    base.alpha_composite(shade, (int(center[0] - rotated.width / 2 + offset[0]), int(center[1] - rotated.height / 2 + offset[1])))
    base.alpha_composite(rotated, (int(center[0] - rotated.width / 2), int(center[1] - rotated.height / 2)))


# ------------------------------------------------------------------ backdrop: whitewashed wood flat-lay + props

def backdrop():
    img = np.zeros((H, W, 3), np.float32)
    plank = 172
    x = -40
    while x < W:
        tone = np.array([233, 223, 205], np.float32) * (1.0 + random.uniform(-0.025, 0.02))
        grain = np.cumsum(np.random.normal(0, 1, (plank,)), axis=0)
        grain = (grain - grain.mean()) / (np.abs(grain).max() + 1e-5)
        lines = np.sin(np.linspace(0, random.uniform(14, 26), plank) + grain * 2.2) * 0.5 + 0.5
        col = tone[None, :] * (0.965 + 0.05 * lines[:, None])
        x0, x1 = max(x, 0), min(x + plank, W)
        img[:, x0:x1, :] = col[(x0 - x):(x1 - x)][None, :, :]
        img[:, max(x1 - 2, 0):x1, :] *= 0.86
        x += plank
    # long vertical streaks (whitewash brush) and fine noise
    streak = np.random.normal(0, 1, (1, W)).repeat(H, 0)
    streak = np.array(Image.fromarray(((streak + 3) * 40).clip(0, 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(2)), np.float32)
    img *= (0.985 + (streak[:, :, None] / 255.0) * 0.03)
    img += np.random.normal(0, 2.0, img.shape)
    # sun from the top-left
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
    light = 1.04 - 0.08 * ((xx / W) * 0.4 + (yy / H) * 0.6)
    img *= light[:, :, None]
    base = Image.fromarray(img.clip(0, 255).astype(np.uint8)).convert("RGBA")
    # dappled leaf shadow (a static 2D overlay in the backdrop texture)
    leaves = Image.new("L", (W, H), 0)
    d = ImageDraw.Draw(leaves)
    for cx, cy, n in ((1020, 1700, 16), (60, 2000, 12), (980, 2150, 10)):
        for _ in range(n):
            ang = random.uniform(0, math.pi)
            r = random.uniform(30, 90)
            px, py = cx + math.cos(ang) * r * 1.8, cy + math.sin(ang) * r
            d.ellipse((px - 34, py - 12, px + 34, py + 12), fill=255)
    leaves = leaves.filter(ImageFilter.GaussianBlur(16)).point(lambda a: int(a * 0.13))
    shade = Image.new("RGBA", (W, H), (70, 78, 52, 0))
    shade.putalpha(leaves)
    base.alpha_composite(shade)
    return base


def boarding_pass():
    t = Image.new("RGBA", (330, 150), (0, 0, 0, 0))
    d = ImageDraw.Draw(t)
    d.rounded_rectangle((0, 0, 329, 149), 14, fill=(250, 246, 238))
    d.rounded_rectangle((0, 0, 329, 34), 14, fill=TEAL)
    d.rectangle((0, 20, 329, 34), fill=TEAL)
    d.text((14, 5), "BİNİŞ KARTI", font=font("bold", 20), fill=CREAM)
    d.text((14, 48), "IST", font=font("bold", 40), fill=INK)
    d.text((112, 58), "→", font=font("semi", 30), fill=MUSTARD)
    d.text((160, 48), "JTR", font=font("bold", 40), fill=INK)
    d.text((14, 104), "Koltuk 12A  ·  Kapı B4", font=font("semi", 18), fill=MUTED)
    for i in range(0, 150, 10):
        d.rectangle((246, i, 248, i + 5), fill=(205, 196, 180))
    for k in range(9):
        d.rectangle((262 + k * 6, 60, 264 + k * 6 - (k % 3), 130), fill=INK)
    return t


def postcard():
    t = Image.new("RGBA", (290, 200), (0, 0, 0, 0))
    d = ImageDraw.Draw(t)
    d.rounded_rectangle((0, 0, 289, 199), 10, fill=(252, 249, 243))
    d.rectangle((12, 12, 277, 150), fill=(120, 182, 214))
    d.rectangle((12, 104, 277, 150), fill=(52, 120, 168))
    d.ellipse((210, 24, 250, 64), fill=(246, 214, 120))
    for x0, y0, x1, y1 in ((30, 78, 90, 128), (84, 92, 150, 140), (150, 70, 220, 132), (205, 96, 262, 142)):
        d.rectangle((x0, y0, x1, y1), fill=(250, 250, 246))
        d.rectangle((x0 + 10, y0 + 14, x0 + 18, y0 + 26), fill=(52, 120, 168))
    d.pieslice((158, 40, 212, 96), 180, 360, fill=(38, 92, 160))
    d.rectangle((182, 30, 186, 44), fill=(250, 250, 246))
    d.text((14, 160), "Santorini", font=font("bold", 26), fill=DEEP)
    d.rectangle((240, 158, 276, 190), outline=(205, 190, 165), width=2)
    return t


def folded_map():
    t = Image.new("RGBA", (300, 230), (0, 0, 0, 0))
    d = ImageDraw.Draw(t)
    panels = [(232, 222, 196), (222, 210, 182), (236, 226, 200)]
    for i in range(3):
        d.polygon([(i * 100, 6 if i % 2 else 0), ((i + 1) * 100, 0 if i % 2 else 6), ((i + 1) * 100, 230 if i % 2 else 224),
                   (i * 100, 224 if i % 2 else 230)], fill=panels[i])
    d.line([(10, 160), (60, 120), (130, 140), (190, 80), (290, 100)], fill=TERRA, width=4)
    d.line([(20, 40), (90, 70), (170, 40), (260, 190)], fill=(150, 170, 160), width=3)
    for cx, cy in ((60, 120), (190, 80)):
        d.ellipse((cx - 8, cy - 8, cx + 8, cy + 8), fill=TERRA)
    d.ellipse((120, 150, 230, 210), fill=(168, 205, 220))
    return t


def olive_sprig():
    t = Image.new("RGBA", (300, 260), (0, 0, 0, 0))
    d = ImageDraw.Draw(t)
    d.line([(20, 250), (150, 140), (280, 20)], fill=(110, 98, 70), width=5)
    for i in range(9):
        p = i / 8.0
        bx, by = 20 + 260 * p, 250 - 230 * p
        for s in (-1, 1):
            leaf = Image.new("RGBA", (90, 30), (0, 0, 0, 0))
            ImageDraw.Draw(leaf).ellipse((0, 4, 88, 26), fill=(128, 146, 98) if s > 0 else (106, 126, 84))
            ImageDraw.Draw(leaf).line([(4, 15), (84, 15)], fill=(160, 176, 128), width=2)
            leaf = leaf.rotate(40 + s * 45 + random.uniform(-10, 10), expand=True, resample=Image.BICUBIC)
            t.alpha_composite(leaf, (int(bx - leaf.width / 2 + s * 18), int(by - leaf.height / 2)))
    return t


def hat_brim():
    t = Image.new("RGBA", (520, 520), (0, 0, 0, 0))
    d = ImageDraw.Draw(t)
    d.ellipse((0, 0, 519, 519), fill=(222, 194, 136))
    for r in range(20, 260, 14):
        d.ellipse((260 - r, 260 - r, 260 + r, 260 + r), outline=(204, 174, 116), width=3)
    d.ellipse((130, 130, 390, 390), fill=(214, 184, 124))
    d.ellipse((150, 150, 370, 370), outline=(42, 82, 96), width=18)
    return t


def quiet(tile, scale=0.85, saturation=0.62, contrast=0.72):
    """Lower a prop's visual priority: smaller, desaturated and flatter, lifted toward the backdrop tone."""
    from PIL import ImageEnhance
    alpha = tile.split()[3]
    rgb = tile.convert("RGB")
    rgb = ImageEnhance.Color(rgb).enhance(saturation)
    rgb = ImageEnhance.Contrast(rgb).enhance(contrast)
    rgb = Image.blend(rgb, Image.new("RGB", rgb.size, (233, 223, 205)), 0.12)
    out = rgb.convert("RGBA")
    out.putalpha(alpha)
    return out.resize((int(tile.width * scale), int(tile.height * scale)), Image.LANCZOS)


# ------------------------------------------------------------------ item paintovers (ART-GATE-02B stand-ins)

def item_tile(w, h):
    return Image.new("RGBA", (int(w), int(h)), (0, 0, 0, 0))


def grad_capsule(w, h, color, radius, horizontal_shade=True, strength=0.22):
    t = item_tile(w, h)
    arr = np.zeros((int(h), int(w), 4), np.float32)
    xs = np.linspace(-1, 1, int(w))[None, :] if horizontal_shade else np.linspace(-1, 1, int(h))[:, None]
    shade = 1.0 + strength * (0.35 - xs ** 2) - 0.08 * xs
    arr[:, :, :3] = np.array(color, np.float32)[None, None, :] * shade[:, :, None] if horizontal_shade else \
        np.array(color, np.float32)[None, None, :] * shade[:, :, None]
    arr[:, :, 3] = 255
    body = Image.fromarray(arr.clip(0, 255).astype(np.uint8), "RGBA")
    mask = Image.new("L", (int(w), int(h)), 0)
    ImageDraw.Draw(mask).rounded_rectangle((0, 0, int(w) - 1, int(h) - 1), radius, fill=255)
    t.paste(body, (0, 0), mask)
    return t


def passport(w, h):
    t = grad_capsule(w, h, (38, 63, 102), 12, strength=0.12)
    d = ImageDraw.Draw(t)
    d.rounded_rectangle((0, h - 16, w - 1, h - 1), 10, fill=(27, 45, 74))
    d.rectangle((w - 10, 10, w - 6, h - 20), fill=(236, 226, 204))
    cx, cy = w / 2 - 3, h * 0.38
    d.ellipse((cx - 26, cy - 26, cx + 26, cy + 26), outline=(215, 178, 96), width=4)
    d.arc((cx - 26, cy - 12, cx + 26, cy + 12), 0, 360, fill=(215, 178, 96), width=2)
    d.line([(cx, cy - 26), (cx, cy + 26)], fill=(215, 178, 96), width=2)
    f = font("bold", 15)
    tw = d.textlength("PASAPORT", font=f)
    d.text((cx - tw / 2, h * 0.62), "PASAPORT", font=f, fill=(215, 178, 96))
    d.rounded_rectangle((cx - 18, h * 0.76, cx + 18, h * 0.76 + 22), 4, outline=(215, 178, 96), width=2)
    return t


def towel(w, h):
    """Rolled terry towel lying lengthwise: cylinder shading, soft wavy outline, loose outer flap, stripes that wrap
    around the roll and the spiral of the roll visible on the near (front) end."""
    w, h = int(w), int(h)
    face_h = int(w * 0.5)
    body_h = h - face_h // 2
    ys, xs = np.mgrid[0:body_h, 0:w].astype(np.float32)
    u = (xs - w / 2) / (w / 2)
    wobble = 1.0 + 0.025 * np.sin(ys / 23.0) + 0.015 * np.sin(ys / 9.0 + 1.3)
    inside = np.abs(u) <= 0.97 * wobble
    cap = np.clip(1.0 - ((ys - w * 0.45) / (w * 0.45)) ** 2, 0, 1) if body_h > 0 else 1
    inside &= (ys >= w * 0.45) | (u ** 2 <= cap)
    shade = 0.80 + 0.26 * np.sqrt(np.clip(1 - u ** 2, 0, 1)) - 0.05 * u
    col = np.zeros((body_h, w, 3), np.float32)
    col[:] = (243, 238, 228)
    # stripes follow the roll's curvature (they wrap around it)
    for c in (0.14, 0.80):
        yc = body_h * c + 10 * np.sqrt(np.clip(1 - u ** 2, 0, 1))
        col[np.abs(ys - yc) < 12] = TEAL
        col[np.abs(ys - (yc + 19)) < 3] = CORAL
    col *= shade[:, :, None]
    loops = np.random.normal(0, 1, (body_h, w)).astype(np.float32)
    loops = np.array(Image.fromarray(((loops + 3) * 40).clip(0, 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(0.8)), np.float32)
    col += (loops[:, :, None] - 120) * 0.16
    # loose outer flap: an edge running along the roll with a soft shadow under it
    fx = w * 0.66 + 3 * np.sin(ys / 31.0)
    col[(xs > fx) & (xs < fx + 7)] *= 0.78
    col[(xs > fx - 3) & (xs <= fx)] *= 1.07
    rgba = np.zeros((body_h, w, 4), np.float32)
    rgba[:, :, :3] = col
    rgba[:, :, 3] = inside * 255
    t = item_tile(w, h)
    t.alpha_composite(Image.fromarray(rgba.clip(0, 255).astype(np.uint8), "RGBA"), (0, 0))
    # near end of the roll: an elliptical face with the fabric spiral
    face = item_tile(w, face_h)
    fd = ImageDraw.Draw(face)
    fd.ellipse((2, 2, w - 3, face_h - 3), fill=(226, 218, 204))
    cx, cy = w / 2, face_h / 2
    pts = []
    for k in range(260):
        a = k * 0.1
        r = 2.0 + a * 2.1
        if r > w / 2 - 6:
            break
        pts.append((cx + math.cos(a) * r, cy + math.sin(a) * r * (face_h / w)))
    fd.line(pts, fill=(176, 166, 150), width=4, joint="curve")
    fd.line([(p[0] + 1, p[1] - 2) for p in pts], fill=(246, 241, 232), width=2)
    t.alpha_composite(face, (0, h - face_h))
    return t


def sunglasses(w, h):
    t = item_tile(w, h)
    d = ImageDraw.Draw(t)
    d.rounded_rectangle((8, 6, w - 8, 20), 6, fill=(92, 56, 34))
    lw = (w - 30) / 2
    for i in range(2):
        x0 = 6 + i * (lw + 18)
        d.rounded_rectangle((x0, 14, x0 + lw, h - 6), 34, fill=(122, 74, 46))
        for _ in range(14):
            sx, sy = random.uniform(x0 + 6, x0 + lw - 10), random.uniform(18, h - 14)
            d.ellipse((sx, sy, sx + 10, sy + 6), fill=(150, 96, 56))
        d.rounded_rectangle((x0 + 9, 23, x0 + lw - 9, h - 15), 28, fill=(34, 42, 46))
        d.line([(x0 + 24, h - 26), (x0 + lw * 0.6, 30)], fill=(120, 150, 160), width=5)
    d.rectangle((6 + lw - 4, 30, 6 + lw + 22, 44), fill=(122, 74, 46))
    return t


def shampoo(w, h):
    t = item_tile(w, h)
    cap = grad_capsule(w * 0.62, h * 0.28, DEEP, 12, strength=0.3)
    t.alpha_composite(cap, (int(w * 0.19), 0))
    body = grad_capsule(w, h * 0.8, CORAL, int(w * 0.42), strength=0.3)
    t.alpha_composite(body, (0, int(h * 0.2)))
    d = ImageDraw.Draw(t)
    d.rounded_rectangle((w * 0.16, h * 0.42, w * 0.84, h * 0.78), 8, fill=(250, 244, 232))
    d.ellipse((w * 0.36, h * 0.47, w * 0.64, h * 0.53), fill=TEAL)
    for k in range(3):
        d.rectangle((w * 0.26, h * (0.58 + k * 0.05), w * 0.74, h * (0.58 + k * 0.05) + 3), fill=(205, 192, 172))
    d.line([(w * 0.2, h * 0.26), (w * 0.2, h * 0.92)], fill=(246, 176, 156), width=5)
    return t


def pouch(w, h):
    t = grad_capsule(w, h, (217, 169, 78), 36, strength=0.3)
    arr = np.array(t).astype(np.float32)
    arr[:, :, :3] += np.random.normal(0, 4, arr.shape[:2])[:, :, None] * (arr[:, :, 3:4] > 0)
    t = Image.fromarray(arr.clip(0, 255).astype(np.uint8), "RGBA")
    d = ImageDraw.Draw(t)
    d.rounded_rectangle((0, h - 18, w - 1, h - 1), 14, fill=(184, 140, 60))
    d.rounded_rectangle((18, 34, w - 18, 52), 8, fill=DEEP)
    for x in range(24, int(w) - 22, 8):
        d.rectangle((x, 39, x + 4, 47), fill=(216, 205, 182))
    d.rounded_rectangle((w - 44, 26, w - 20, 74), 8, fill=TERRA)
    d.rounded_rectangle((w * 0.52, h * 0.66, w * 0.86, h * 0.84), 8, fill=(244, 232, 210))
    d.text((w * 0.58, h * 0.68), "ZT", font=font("bold", 24), fill=TERRA)
    for y in range(70, int(h) - 30, 26):
        d.line([(16, y), (w - 16, y + 4)], fill=(206, 158, 70), width=2)
    return t


# ------------------------------------------------------------------ UI (UI-SLICE-01 target)

def undo_icon(d, cx, cy, r, color, flip=False):
    s = -1 if flip else 1
    d.arc((cx - r, cy - r, cx + r, cy + r), 200 if not flip else -20, 520 if not flip else 300, fill=color, width=8)
    ax = cx - s * r * 0.95
    d.polygon([(ax - s * 2, cy - 22), (ax - s * 26, cy + 2), (ax + s * 18, cy + 8)], fill=color)


def rotate_icon(d, cx, cy, r, color):
    d.arc((cx - r, cy - r, cx + r, cy + r), 40, 330, fill=color, width=7)
    d.polygon([(cx + r * 0.75, cy - r * 0.95), (cx + r * 1.25, cy - r * 0.2), (cx + r * 0.35, cy - r * 0.15)], fill=color)


def check(d, cx, cy, s, color):
    d.line([(cx - s * 0.45, cy), (cx - s * 0.1, cy + s * 0.35), (cx + s * 0.5, cy - s * 0.35)], fill=color, width=6, joint="curve")


def main():
    lay = json.load(open(os.path.join(STYLE, "style-layout.json"), encoding="utf-8"))
    black = np.asarray(Image.open(os.path.join(STYLE, "style-fg-black.png")).convert("RGB"), np.float32)
    white = np.asarray(Image.open(os.path.join(STYLE, "style-fg-white.png")).convert("RGB"), np.float32)
    alpha = 1.0 - ((white - black).mean(axis=2) / 255.0)
    alpha = alpha.clip(0, 1)

    # Suitcase plate only: the Source Tray is repainted below (rows below the handle gap).
    tray_items = [v["rect"] for v in lay["items"].values() if v["tray"]]
    tray_top = min(r[1] for r in tray_items) - 56
    ramp = np.clip((tray_top - np.arange(H, dtype=np.float32)) / 12.0, 0.0, 1.0)
    alpha *= ramp[:, None]
    black *= ramp[:, None, None]
    alpha[alpha < 0.02] = 0

    out = backdrop()
    # props, outside the interaction zones (edges, below/beside the tray, partly under the dock), kept quiet:
    # desaturated, lower contrast, ~85 % scale, pushed to the screen edges, soft shadows.
    tile_shadow(out, quiet(folded_map()), (60, 2020), -14, blur=12, opacity=0.18)
    tile_shadow(out, quiet(boarding_pass()), (92, 1730), -9, blur=12, opacity=0.2)
    tile_shadow(out, quiet(hat_brim()), (1100, 2230), 0, blur=20, opacity=0.16)
    tile_shadow(out, quiet(postcard()), (1000, 1730), 8, blur=12, opacity=0.2)
    tile_shadow(out, quiet(olive_sprig()), (40, 1450), 160, blur=10, opacity=0.14)

    # suitcase cast shadow on the flat-lay (key light from the top-left)
    body = Image.fromarray((np.where(alpha > 0.5, 255, 0)).astype(np.uint8)).filter(ImageFilter.GaussianBlur(26))
    body = body.point(lambda a: int(a * 0.42))
    shade = Image.new("RGBA", (W, H), SHADOW + (0,))
    shade.putalpha(Image.fromarray(np.roll(np.roll(np.asarray(body), 28, 0), 18, 1)))
    out.alpha_composite(shade)

    # composite the real render (premultiplied over black)
    bg = np.asarray(out.convert("RGB"), np.float32)
    comp = black + (1.0 - alpha[:, :, None]) * bg
    out = Image.fromarray(comp.clip(0, 255).astype(np.uint8)).convert("RGBA")

    # board item paintovers (sweater is the approved real asset and stays as rendered)
    painters = {"passport-1": passport, "towel-1": towel, "sunglasses-1": sunglasses, "shampoo-1": shampoo,
                "travel-pouch-1": pouch}
    for iid, v in lay["items"].items():
        if v["tray"] or iid not in painters:
            continue
        x0, y0, x1, y1 = v["rect"]
        out.alpha_composite(painters[iid](x1 - x0, y1 - y0), (x0, y0))

    # Source Tray: one shell that hugs the remaining cards (reflows as items leave), one paper card per item.
    pad = 26
    cards = [(r[0] - pad, r[1] - pad + (16 if iid == "travel-pouch-1" else 0), r[2] + pad, r[3] + pad)
             for iid, v in lay["items"].items() if v["tray"] for r in [v["rect"]]]
    sx0, sy0 = min(c[0] for c in cards) - 30, min(c[1] for c in cards) - 30
    sx1, sy1 = max(c[2] for c in cards) + 30, max(c[3] for c in cards) + 30
    soft_shadow(out, (sx0, sy0, sx1, sy1), 46, 18, (10, 16), 0.32)
    d = ImageDraw.Draw(out)
    d.rounded_rectangle((sx0, sy0, sx1, sy1), 46, fill=(214, 196, 160))
    d.rounded_rectangle((sx0 + 10, sy0 + 10, sx1 - 10, sy1 - 10), 38, fill=(226, 211, 180))
    for k in range(sy0 + 24, sy1 - 20, 9):
        d.line([(sx0 + 20, k), (sx1 - 20, k)], fill=(218, 202, 168), width=2)
    for iid, v in lay["items"].items():
        if not v["tray"]:
            continue
        x0, y0, x1, y1 = v["rect"]
        selected = iid == "travel-pouch-1"
        card = (x0 - pad, y0 - pad + (16 if selected else 0), x1 + pad, y1 + pad)
        soft_shadow(out, card, 26, 8, (4, 7), 0.25)
        d.rounded_rectangle(card, 26, fill=PAPER, outline=MUSTARD if selected else (234, 224, 206), width=5 if selected else 2)
        # item contact / lift shadow
        lift = 24 if selected else 8
        soft_shadow(out, (x0 + 6, y0 + 10, x1 - 2, y1 + 2), 30, 10 + lift * 0.4, (lift * 0.4, lift * 0.6), 0.38)
        out.alpha_composite(painters[iid](x1 - x0, y1 - y0), (x0, y0))

    # Header (safe-area top): identity only.
    hx0, hy0, hx1, hy1 = lay["header"]
    soft_shadow(out, (hx0, hy0, hx1, hy1), 40, 16, (0, 10), 0.3)
    d.rounded_rectangle((hx0, hy0, hx1, hy1), 40, fill=CREAM)
    d.rounded_rectangle((hx0 + 4, hy0 + 3, hx1 - 4, hy0 + 10), 6, fill=(255, 252, 245))
    cx, cy = hx0 + 76, (hy0 + hy1) / 2
    d.ellipse((cx - 46, cy - 46, cx + 46, cy + 46), fill=TEAL)
    d.rounded_rectangle((cx - 26, cy - 12, cx + 26, cy + 24), 8, fill=CREAM)
    d.rounded_rectangle((cx - 11, cy - 24, cx + 11, cy - 10), 5, outline=CREAM, width=5)
    d.rectangle((cx - 26, cy + 2, cx + 26, cy + 6), fill=TEAL)
    d.text((hx0 + 150, hy0 + 18), "Seviye 1", font=font("bold", 48), fill=INK)
    d.text((hx0 + 152, hy0 + 76), "İlk Yolculuk", font=font("semi", 32), fill=MUTED)

    # Objective note pinned on the open lid: a compact travel note left of the strap buckle, clear of the bed.
    nw, nh = 500, 146
    note = Image.new("RGBA", (nw, nh), (0, 0, 0, 0))
    nd = ImageDraw.Draw(note)
    nd.rounded_rectangle((0, 0, nw - 1, nh - 1), 16, fill=PAPER)
    for y in (84, 122):
        nd.line([(20, y), (nw - 20, y)], fill=(238, 229, 211), width=2)
    nd.text((22, 9), "Yolculuk Hazırlıkları", font=font("bold", 27), fill=INK)
    nd.ellipse((22, 53, 52, 83), fill=TEAL)
    check(nd, 37, 68, 17, PAPER)
    nd.text((64, 50), "Pasaport üst bölgede olmalı", font=font("semi", 26), fill=INK)
    nd.ellipse((22, 92, 52, 122), outline=(185, 178, 166), width=3)
    nd.text((64, 89), "Şampuan sağ bölgede olmalı", font=font("semi", 26), fill=INK)
    tape = Image.new("RGBA", (86, 28), MUSTARD + (215,)).rotate(-28, expand=True, resample=Image.BICUBIC)
    note.alpha_composite(tape, (nw - tape.width + 12, -10))
    ncx = lay["lid"][0] + 26 + nw / 2
    ncy = lay["header"][3] + 14 + nh / 2
    tile_shadow(out, note, (ncx, ncy), -1.5, blur=10, offset=(5, 8), opacity=0.26)

    # Bottom dock: secondary Undo / Restart at thumb reach; the contextual hero slot shows Rotate while the selected
    # item can rotate (otherwise "2 eşya kaldı"; "Sonraki" after the Packed moment).
    x0, y0, x1, y1 = lay["dock"]
    soft_shadow(out, (x0, y0, x1, y1), 52, 18, (0, 10), 0.3)
    d.rounded_rectangle((x0, y0, x1, y1), 52, fill=CREAM)
    for bx, label, flip in ((x0 + 98, "Geri Al", False), (x1 - 98, "Baştan", True)):
        by = (y0 + y1) / 2
        d.ellipse((bx - 64, by - 70, bx + 64, by + 58), fill=(236, 226, 208))
        d.ellipse((bx - 64, by - 74, bx + 64, by + 54), fill=(244, 236, 222))
        undo_icon(d, bx, by - 16, 22, INK, flip)
        f = font("semi", 22)
        d.text((bx - d.textlength(label, font=f) / 2, by + 20), label, font=f, fill=INK)
    # Medium priority: the full-width mustard hero is reserved for "Sonraki" after the Packed moment.
    acx, acy = (lay["action"][0] + lay["action"][2]) / 2, (lay["action"][1] + lay["action"][3]) / 2
    ax0, ay0, ax1, ay1 = acx - 175, acy - 62, acx + 175, acy + 62
    d.rounded_rectangle((ax0, ay0 + 5, ax1, ay1 + 5), 62, fill=(214, 186, 128))
    d.rounded_rectangle((ax0, ay0, ax1, ay1), 62, fill=(238, 214, 162), outline=(205, 164, 84), width=3)
    rotate_icon(d, ax0 + 104, acy, 22, INK)
    d.text((ax0 + 146, acy - 25), "Döndür", font=font("bold", 42), fill=INK)

    # device punch-hole (the header sits below it, inside the safe area)
    d.ellipse((540 - 18, 52 - 18, 540 + 18, 52 + 18), fill=(10, 10, 12))

    # final grade: gentle warmth, contrast and vignette (existing post stack range)
    arr = np.asarray(out.convert("RGB"), np.float32)
    arr = (arr - 128) * 1.04 + 128
    arr *= np.array([1.015, 1.0, 0.975], np.float32)
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
    v = ((xx - W / 2) / (W * 0.75)) ** 2 + ((yy - H / 2) / (H * 0.7)) ** 2
    arr *= (1.0 - 0.12 * v.clip(0, 1))[:, :, None]
    clean = Image.fromarray(arr.clip(0, 255).astype(np.uint8))
    clean.save(os.path.join(STYLE, "style-frame-clean.png"))

    # annotated
    ann = clean.convert("RGBA")
    a = ImageDraw.Draw(ann)
    safe_top = lay["safe"][1]
    hatch = layer()
    hd = ImageDraw.Draw(hatch)
    hd.rectangle((0, 0, W, safe_top), fill=(220, 60, 60, 70))
    ann.alpha_composite(hatch)
    tags = [
        ("Safe area (cutout inset)", (0, 0, W, safe_top), (200, 60, 60)),
        ("Header", lay["header"], (49, 81, 93)),
        ("Objective note", (ncx - nw / 2 - 8, ncy - nh / 2 - 10, ncx + nw / 2 + 8, ncy + nh / 2 + 10), (196, 120, 40)),
        ("Suitcase (real golden asset)", (28, lay["lid"][1] + 120, 1052, tray_top - 20), (150, 60, 50)),
        ("Playable bed (5 x 7, hidden grid)", lay["bed"], (40, 140, 120)),
        ("Source tray (cards reflow)", (sx0, sy0, sx1, sy1), (120, 90, 40)),
        ("Contextual action", (int(ax0) - 6, int(ay0) - 6, int(ax1) + 6, int(ay1) + 6), (170, 120, 20)),
        ("Backdrop / props", (6, 1600, 250, 2110), (90, 110, 70)),
        ("Backdrop / props", (850, 1620, 1074, 2110), (90, 110, 70)),
    ]
    f = font("bold", 26)
    for text, (bx0, by0, bx1, by1), col in tags:
        a.rounded_rectangle((bx0 + 3, by0 + 3, bx1 - 3, by1 - 3), 14, outline=col + (255,), width=5)
        tw = a.textlength(text, font=f)
        tx, ty = bx0 + 12, by0 + 10
        if text.startswith("Suitcase"):
            tx, ty = bx0 + 60, by1 - 60
        if text.startswith("Header"):
            tx, ty = bx1 - tw - 22, by1 - 46
        if text.startswith("Objective"):
            tx, ty = bx1 - tw - 10, by1 + 6
        tx = min(tx, W - tw - 22)
        if text.startswith("Safe"):
            tx, ty = 20, safe_top - 44
        a.rounded_rectangle((tx - 8, ty - 4, tx + tw + 10, ty + 34), 10, fill=col + (235,))
        a.text((tx, ty), text, font=f, fill=(255, 255, 255))
    cx0, cy0 = 470, 905
    a.rounded_rectangle((cx0, cy0, cx0 + 400, cy0 + 196), 18, fill=(255, 255, 255, 236), outline=(196, 120, 40, 255), width=4)
    a.text((cx0 + 16, cy0 + 12), "Narrow / notch layouts (future):", font=font("bold", 22), fill=INK)
    a.text((cx0 + 16, cy0 + 40), "compact rule chip, text stays >= 26 px", font=font("semi", 20), fill=MUTED)
    chip = (cx0 + 16, cy0 + 82, cx0 + 384, cy0 + 136)
    a.rounded_rectangle(chip, 27, fill=PAPER, outline=(225, 212, 188), width=2)
    a.ellipse((chip[0] + 12, chip[1] + 12, chip[0] + 42, chip[1] + 42), fill=TEAL)
    check(a, chip[0] + 27, chip[1] + 27, 16, PAPER)
    a.text((chip[0] + 50, chip[1] + 12), "Pasaport", font=font("semi", 26), fill=INK)
    a.ellipse((chip[0] + 186, chip[1] + 12, chip[0] + 216, chip[1] + 42), outline=(185, 178, 166), width=3)
    a.text((chip[0] + 224, chip[1] + 12), "Şampuan", font=font("semi", 26), fill=INK)
    a.text((cx0 + 16, cy0 + 150), "tap expands the full note", font=font("semi", 20), fill=MUTED)
    ann.convert("RGB").save(os.path.join(STYLE, "style-frame-annotated.png"))

    # current vs style frame
    cur = Image.open(os.path.join(LAYOUT, "00-current-shipped-lv1.png")).convert("RGB")
    side = Image.new("RGB", (W * 2 + 60, H + 120), (255, 255, 255))
    side.paste(cur, (0, 120))
    side.paste(clean, (W + 60, 120))
    sd = ImageDraw.Draw(side)
    sd.text((20, 30), "Current shipped Lv1 (real build)", font=font("bold", 54), fill=INK)
    sd.text((W + 80, 30), "STYLE-FRAME-01 (reachable target)", font=font("bold", 54), fill=INK)
    side.save(os.path.join(STYLE, "current-vs-style-frame.png"))
    print("ok", tray_top, (sx0, sy0, sx1, sy1), (ncx, ncy))


if __name__ == "__main__":
    sys.exit(main())
