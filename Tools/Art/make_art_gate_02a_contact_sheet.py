"""Assemble the ART-GATE-02A Blender review renders into one contact sheet."""

from pathlib import Path

from PIL import Image, ImageDraw, ImageFont


ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "Builds/art-gate-02a"
PANELS = [
    ("Physical interior", "interior-alone.png"),
    ("Inside golden suitcase", "interior-in-golden-suitcase.png"),
    ("Gameplay angle", "interior-gameplay-angle.png"),
    ("Interior wireframe | 2,208 tris", "interior-wireframe.png"),
    ("Open | existing | 8,816 tris", "sweater-open-existing-high.png"),
    ("Open | proposal | 8,816 tris", "sweater-open-proposed-high.png"),
    ("Open | existing side", "sweater-open-existing-side.png"),
    ("Open | proposal side", "sweater-open-proposed-side.png"),
    ("Folded | existing | 9,600 tris", "sweater-folded-existing-high.png"),
    ("Folded | proposal | 7,900 tris", "sweater-folded-proposed-high.png"),
    ("Folded | existing side", "sweater-folded-existing-side.png"),
    ("Folded | proposal side", "sweater-folded-proposed-side.png"),
]
CELL, LABEL, MARGIN = 500, 44, 20
sheet = Image.new("RGB", (4 * CELL + 5 * MARGIN,
                          3 * (CELL + LABEL) + 4 * MARGIN), "#e8e3d9")
draw = ImageDraw.Draw(sheet)
font_path = Path("C:/Windows/Fonts/segoeui.ttf")
font = ImageFont.truetype(str(font_path), 22) if font_path.exists() else ImageFont.load_default()
for index, (caption, filename) in enumerate(PANELS):
    col, row = index % 4, index // 4
    x = MARGIN + col * (CELL + MARGIN)
    y = MARGIN + row * (CELL + LABEL + MARGIN)
    with Image.open(OUT / filename) as source:
        sheet.paste(source.convert("RGB").resize((CELL, CELL), Image.Resampling.LANCZOS), (x, y))
    draw.text((x + 8, y + CELL + 7), caption, fill="#283a38", font=font)
sheet.save(OUT / "art-gate-02a-contact-sheet.png", optimize=True)
