"""Rebuild the small, reusable UI-SLICE-01.1 stationery texture kit."""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFilter
import random

OUT = Path(__file__).resolve().parents[2] / "Assets/Resources/UiSlice011"
OUT.mkdir(parents=True, exist_ok=True)
random.seed(1101)


def grain(image, amount=8):
    px = image.load()
    for y in range(image.height):
        for x in range(image.width):
            r, g, b, a = px[x, y]
            if a:
                n = random.randrange(-amount, amount + 1)
                px[x, y] = tuple(max(0, min(255, c + n)) for c in (r, g, b)) + (a,)


def save(name, image):
    grain(image)
    image.save(OUT / (name + ".png"), optimize=True)


def panel(name, size, shape, fill, rim, inner, radius=20, stitch=False):
    w, h = size
    image = Image.new("RGBA", size)
    d = ImageDraw.Draw(image)
    if shape == "tag":
        points = [(24, 3), (w - 35, 3), (w - 4, 34), (w - 4, h - 23), (w - 27, h - 4), (5, h - 4), (5, 22)]
        d.polygon(points, fill=rim)
        points = [(25, 8), (w - 37, 8), (w - 10, 36), (w - 10, h - 25), (w - 29, h - 10), (11, h - 10), (11, 24)]
        d.polygon(points, fill=fill)
        d.line(points + [points[0]], fill=inner, width=2)
    elif shape == "note":
        points = [(5, 5), (w - 44, 4), (w - 5, 42), (w - 6, h - 6), (8, h - 4)]
        d.polygon(points, fill=rim)
        d.polygon([(10, 10), (w - 47, 10), (w - 12, 44), (w - 12, h - 12), (14, h - 10)], fill=fill)
        d.polygon([(w - 47, 10), (w - 47, 44), (w - 12, 44)], fill=(225, 213, 189))
        d.line([(w - 47, 10), (w - 47, 44), (w - 12, 44)], fill=inner, width=2)
        d.line([(16, 12), (w - 50, 12)], fill=(255, 253, 246), width=2)
    elif shape == "tray":
        d.rounded_rectangle((3, 3, w - 4, h - 4), radius, fill=rim)
        d.rounded_rectangle((10, 10, w - 11, h - 11), radius - 4, fill=fill, outline=inner, width=2)
        d.arc((15, 14, w - 16, h - 14), 190, 350, fill=(225, 210, 180), width=3)
        if stitch:
            d.rounded_rectangle((17, 17, w - 18, h - 18), radius - 8, outline=(174, 143, 101), width=1)
    else:
        d.rounded_rectangle((3, 3, w - 4, h - 4), radius, fill=rim)
        d.rounded_rectangle((8, 8, w - 9, h - 10), radius - 4, fill=fill, outline=inner, width=2)
        d.line([(radius, 10), (w - radius, 10)], fill=(255, 255, 250), width=2)
    save(name, image)


panel("luggage_label", (512, 192), "tag", (247, 234, 207), (179, 142, 91), (228, 209, 172))
panel("checklist", (512, 256), "note", (250, 244, 228), (207, 185, 151), (234, 222, 196))
panel("checklist_tab", (384, 96), "note", (250, 244, 228), (207, 185, 151), (234, 222, 196))
panel("action_tag", (384, 128), "tag", (239, 216, 165), (170, 125, 61), (255, 239, 195))
panel("ticket", (384, 128), "tag", (235, 182, 72), (145, 97, 44), (255, 223, 132))
panel("tray_slot", (256, 256), "tray", (222, 204, 171), (151, 119, 82), (187, 156, 113), 28, True)
panel("control_seal", (192, 192), "seal", (245, 231, 203), (167, 135, 97), (218, 195, 159), 86)

for name in ("stamp_check", "stamp_cross"):
    image = Image.new("RGBA", (64, 64))
    d = ImageDraw.Draw(image)
    if name == "stamp_check":
        d.line([(12, 34), (26, 47), (52, 17)], fill=(249, 244, 226), width=9, joint="curve")
    else:
        d.line([(16, 16), (48, 48)], fill=(252, 243, 223), width=8)
        d.line([(48, 16), (16, 48)], fill=(252, 243, 223), width=8)
    save(name, image)

image = Image.new("RGBA", (192, 64))
d = ImageDraw.Draw(image)
d.polygon([(6, 5), (187, 2), (181, 60), (9, 62)], fill=(218, 185, 124, 190))
d.line([(8, 12), (183, 10)], fill=(255, 238, 195, 155), width=3)
save("tape", image)

for path in OUT.glob("*.png"):
    meta = path.with_suffix(".png.meta")
    if meta.exists():
        settings = meta.read_text(encoding="utf-8")
        settings = settings.replace("enableMipMap: 1", "enableMipMap: 0")
        settings = settings.replace("nPOTScale: 1", "nPOTScale: 0")
        meta.write_text("\n".join(line.rstrip() for line in settings.splitlines()) + "\n", encoding="utf-8")
    print(path.name, Image.open(path).size, path.stat().st_size)
