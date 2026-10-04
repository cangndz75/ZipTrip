"""Assemble UI-SLICE-01.1 review captures from real Unity-rendered frames."""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
import shutil

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "Builds/ui-slice-01"
OUT = ROOT / "Builds/ui-slice-01-1"
CAPTURES = OUT / "captures"
CAPTURES.mkdir(parents=True, exist_ok=True)
STYLE = ROOT / "Builds/golden-lv1/style/style-frame-clean.png"

states = [
    ("01-golden-initial", "01-initial"),
    ("02-rule-complete-pending", "01-initial"),
    ("03-rule-invalid", "03-passport-invalid"),
    ("04-compact-chip", "12-iphone-notch-chip"),
    ("05-tray-three", "01-initial"),
    ("06-tray-two", "06-two-items-in-tray"),
    ("07-tray-one", "07-one-item-in-tray"),
    ("08-item-selected", "04-one-item-selected"),
    ("09-rotate-visible", "05-rotate-visible"),
    ("10-rotate-hidden-status", "01-initial"),
    ("11-undo-disabled", "01-initial"),
    ("12-undo-enabled", "06-two-items-in-tray"),
    ("13-packed", "09-zip-it-completed"),
    ("14-nine-sixteen", "10-9x16"),
    ("15-android-cutout", "11-android-cutout"),
    ("16-iphone-notch", "12-iphone-notch-chip"),
    ("17-shipped-lv1", "15-shipped-lv1"),
    ("18-shipped-lv2", "16-shipped-lv2"),
]
for target, original in states:
    shutil.copyfile(SOURCE / (original + ".png"), CAPTURES / (target + ".png"))

FONT = Path("C:/Windows/Fonts/arial.ttf")
font = ImageFont.truetype(str(FONT), 28)
small = ImageFont.truetype(str(FONT), 22)


def pair(name, left, right, left_title, right_title):
    a = left.convert("RGB") if isinstance(left, Image.Image) else Image.open(left).convert("RGB")
    b = Image.open(right).convert("RGB")
    height = 1170
    a = a.resize((round(a.width * height / a.height), height), Image.Resampling.LANCZOS)
    b = b.resize((round(b.width * height / b.height), height), Image.Resampling.LANCZOS)
    margin = 28
    sheet = Image.new("RGB", (a.width + b.width + margin * 3, height + 100), (239, 231, 214))
    d = ImageDraw.Draw(sheet)
    sheet.paste(a, (margin, 70))
    sheet.paste(b, (margin * 2 + a.width, 70))
    d.text((margin, 21), left_title, font=font, fill=(49, 51, 47))
    d.text((margin * 2 + a.width, 21), right_title, font=font, fill=(49, 51, 47))
    sheet.save(OUT / name, optimize=True)


# The previous UI-SLICE-01 render was preserved as the right half of its original style comparison sheet.
ui01 = Image.open(SOURCE / "13-style-frame-vs-ui-slice-01.png").crop((740, 80, 1460, 1640))
pair("A-ui-slice-01-before-after.png", ui01, SOURCE / "13-style-frame-state.png",
     "UI-SLICE-01 BEFORE", "UI-SLICE-01.1 AFTER")
pair("B-style-frame-vs-ui-011.png", STYLE, SOURCE / "13-style-frame-state.png", "STYLE-FRAME-01", "UI-SLICE-01.1")
shutil.copyfile(SOURCE / "01-initial.png", OUT / "D-clean-1080x2340.png")

# Crops stay at native 1:1 resolution. Each is taken from a real scene render.
details = [
    ("Header", "01-initial", (240, 0, 830, 175)),
    ("Objective", "01-initial", (25, 155, 515, 330)),
    ("Complete stamp", "01-initial", (45, 195, 180, 270)),
    ("Invalid stamp", "03-passport-invalid", (45, 195, 180, 270)),
    ("Compact chip", "12-iphone-notch-chip", (25, 320, 600, 420)),
    ("Organizer tray", "01-initial", (175, 1580, 890, 2010)),
    ("Selected item", "04-one-item-selected", (175, 1590, 425, 2010)),
    ("Undo", "06-two-items-in-tray", (40, 2130, 250, 2300)),
    ("Rotate", "05-rotate-visible", (340, 2140, 745, 2300)),
    ("Restart", "01-initial", (845, 2130, 1060, 2300)),
    ("Sonraki / Tekrar", "09-zip-it-completed", (210, 1810, 850, 2100)),
]
columns = 2
tile_w, tile_h = 750, 520
sheet = Image.new("RGB", (columns * tile_w + 40, ((len(details) + 1) // 2) * tile_h + 40), (239, 231, 214))
d = ImageDraw.Draw(sheet)
for index, (label, filename, box) in enumerate(details):
    im = Image.open(SOURCE / (filename + ".png")).convert("RGB").crop(box)
    x = 20 + (index % columns) * tile_w
    y = 20 + (index // columns) * tile_h
    d.text((x + 12, y + 8), label, font=small, fill=(49, 51, 47))
    if im.width > tile_w - 20 or im.height > tile_h - 48:
        raise ValueError("Detail crop exceeds 1:1 tile: " + label)
    sheet.paste(im, (x + 10, y + 42))
sheet.save(OUT / "C-phone-detail-1to1.png", optimize=True)
print("Review captures and sheets:", OUT)
