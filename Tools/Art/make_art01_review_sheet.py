"""Make ART-01 review sheets from the shipped Lv1 Unity PNG captures."""

from pathlib import Path

from PIL import Image, ImageDraw, ImageFont, ImageOps


ROOT = Path(__file__).resolve().parents[2]
CAPTURES = ROOT / "Builds/art01-captures"
FONT = ImageFont.truetype("C:/Windows/Fonts/arial.ttf", 26)


def panel(source, label, width, height):
    tile = Image.new("RGB", (width, height + 52), "#161d24")
    with Image.open(source) as image:
        fitted = ImageOps.contain(image.convert("RGB"), (width, height), Image.Resampling.LANCZOS)
        tile.paste(fitted, ((width - fitted.width) // 2, (height - fitted.height) // 2))
    ImageDraw.Draw(tile).text((12, height + 12), label, font=FONT, fill="#f2f3ed")
    return tile


before = panel(CAPTURES / "comparison-shipped-old-items.png", "SHIPPED OLD ITEMS", 432, 936)
after = panel(CAPTURES / "01-level-start.png", "ART-01 NEW ITEMS", 432, 936)
comparison = Image.new("RGB", (912, 1060), "#161d24")
comparison.paste(before, (12, 24))
comparison.paste(after, (468, 24))
comparison.save(CAPTURES / "ART-01-before-after.png", optimize=True)

# closeup-regions.txt holds each item's screen rect in the 1080x2340 composition, written by
# CaptureShippedLv1WithArt01. The close-up source uses the same 1080x2340 coordinates.
PAD = 36
regions = []
for line in (CAPTURES / "closeup-regions.txt").read_text().splitlines():
    label, *box = line.split("\t")
    x0, y0, x1, y1 = map(int, box)
    regions.append((label, (x0 - PAD, y0 - PAD, x1 + PAD, y1 + PAD)))
sheet = Image.new("RGB", (1592, 1128), "#161d24")
with Image.open(CAPTURES / "closeup-source.png") as source:
    for index, (label, box) in enumerate(regions):
        crop = source.crop(box)
        tile = Image.new("RGB", (496, 536), "#222c32")
        fitted = ImageOps.contain(crop.convert("RGB"), (472, 472), Image.Resampling.LANCZOS)
        tile.paste(fitted, ((496 - fitted.width) // 2, (472 - fitted.height) // 2))
        ImageDraw.Draw(tile).text((12, 488), label, font=FONT, fill="#f2f3ed")
        sheet.paste(tile, (24 + (index % 3) * 520, 24 + (index // 3) * 552))
sheet.save(CAPTURES / "ART-01-six-item-closeups.png", optimize=True)
