"""Deterministic 512 px colour atlases for the five Golden Lv1 item meshes."""
from pathlib import Path
from PIL import Image, ImageDraw
import math
import random

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "Assets/Art/Materials/Items/GoldenLv1Final"
OUT.mkdir(parents=True, exist_ok=True)

# Each item is one opaque material. Geometry UVs address 4x4 reusable colour swatches.
# The mild grain gives leather, cotton and plastic their own value response at gameplay scale.
PALETTES = {
    "Passport": ("leather", [
        (27, 52, 88), (20, 39, 68), (228, 185, 83), (238, 227, 199),
        (44, 69, 100), (156, 129, 88), (250, 241, 216), (24, 48, 80),
    ]),
    "Towel": ("weave", [
        (242, 232, 211), (223, 211, 188), (61, 143, 151), (205, 108, 86),
        (249, 242, 225), (187, 173, 152), (52, 117, 128), (221, 127, 103),
    ]),
    "Shampoo": ("plastic", [
        (221, 115, 86), (240, 151, 116), (35, 78, 87), (247, 239, 219),
        (188, 86, 69), (255, 208, 156), (34, 65, 74), (233, 222, 198),
    ]),
    "Sunglasses": ("tortoise", [
        (113, 61, 37), (155, 90, 53), (30, 39, 46), (208, 144, 90),
        (83, 47, 34), (44, 57, 63), (229, 181, 104), (174, 109, 67),
    ]),
    "TravelPouch": ("weave", [
        (206, 159, 66), (179, 127, 49), (75, 70, 57), (225, 187, 102),
        (151, 107, 48), (233, 208, 151), (52, 95, 96), (192, 152, 81),
    ]),
}


def make(name, kind, colours):
    rng = random.Random(2002 + sum(ord(c) for c in name))
    image = Image.new("RGB", (512, 512))
    pixels = image.load()
    for row in range(4):
        for col in range(4):
            base = colours[(row * 4 + col) % len(colours)]
            for local_y in range(128):
                for local_x in range(128):
                    x, y = col * 128 + local_x, row * 128 + local_y
                    noise = rng.randrange(-4, 5)
                    if kind == "weave":
                        noise += round(3.0 * math.sin(local_x * 1.6) + 3.0 * math.sin(local_y * 1.6))
                    elif kind == "leather":
                        noise += round(2.5 * math.sin(local_x * .19) * math.cos(local_y * .17))
                    elif kind == "tortoise":
                        noise += round(8.0 * math.sin(local_x * .08 + math.sin(local_y * .09)))
                    else:
                        noise += round(1.5 * math.sin(local_y * .05))
                    pixels[x, y] = tuple(max(0, min(255, channel + noise)) for channel in base)
    path = OUT / ("T_Item_" + name + "_BaseColor.png")
    image.save(path, optimize=True)
    print(path.name, path.stat().st_size)


for item, (surface, palette) in PALETTES.items():
    make(item, surface, palette)
