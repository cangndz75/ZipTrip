"""Compose real Unity captures with the exact user-supplied north star (no fallback target)."""
import argparse
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont, ImageOps

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "Builds/target-convergence-01"
FONT = ImageFont.truetype("C:/Windows/Fonts/arialbd.ttf", 26)
SHOTS = ["A-level-start", "B-selected-rotate-visible", "C-mid-drag", "D-one-item-remaining",
         "E-pre-completion", "F-zip02-mid-close", "G-final-paketlendi-sonraki"]


def tile(path, label, size):
    image = Image.new("RGB", (size[0], size[1] + 60), "#182d30")
    with Image.open(path) as source:
        fitted = ImageOps.contain(source.convert("RGB"), size, Image.Resampling.LANCZOS)
        image.paste(fitted, ((size[0] - fitted.width) // 2, 60 + (size[1] - fitted.height) // 2))
    ImageDraw.Draw(image).text((14, 16), label, font=FONT, fill="#fff1d4")
    return image


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--target", type=Path, help="Exact newly approved target PNG/JPG")
    parser.add_argument("--contacts-only", action="store_true", help="Do not create the target comparison")
    args = parser.parse_args()
    if not args.contacts_only and (args.target is None or not args.target.is_file()):
        parser.error("The approved target file must exist; previous targets are never substituted.")
    if not args.contacts_only:
        columns = [(args.target, "APPROVED TARGET"), (OUT / "current/A-level-start.png", "CURRENT"),
                   (OUT / "new/A-level-start.png", "NEW")]
        sheet = Image.new("RGB", (1620, 1230), "#182d30")
        for i, (path, label) in enumerate(columns):
            sheet.paste(tile(path, label, (540, 1170)), (i * 540, 0))
        sheet.save(OUT / "APPROVED-TARGET-CURRENT-NEW.png")
    comparison = Image.new("RGB", (1080, 1230), "#182d30")
    for i, label in enumerate(("CURRENT", "NEW")):
        comparison.paste(tile(OUT / label.lower() / "A-level-start.png", label, (540, 1170)), (i * 540, 0))
    comparison.save(OUT / "CURRENT-NEW.png")
    contact = Image.new("RGB", (1512, 1758), "#182d30")
    for i, shot in enumerate(SHOTS):
        contact.paste(tile(OUT / "new" / (shot + ".png"), shot, (378, 819)), ((i % 4) * 378, (i // 4) * 879))
    contact.save(OUT / "NEW-A-G.png")
    # All crops are of actual rendered pixels, never painted/retouched evidence.
    closeups = Image.new("RGB", (1800, 1560), "#182d30")
    crops = [("A-level-start", "MISSION MODULE", (35, 175, 1045, 625)),
             ("A-level-start", "SUITCASE INTERIOR", (100, 650, 960, 1610)),
             ("A-level-start", "STAGING DOCK", (90, 1630, 1020, 2340)),
             ("G-final-paketlendi-sonraki", "COMPLETION", (60, 460, 1030, 2340))]
    for i, (shot, label, box) in enumerate(crops):
        with Image.open(OUT / "new" / (shot + ".png")) as source:
            fitted = ImageOps.contain(source.crop(box), (870, 700), Image.Resampling.LANCZOS)
            x, y = (i % 2) * 900, (i // 2) * 780
            ImageDraw.Draw(closeups).text((x + 16, y + 15), label, font=FONT, fill="#fff1d4")
            closeups.paste(fitted, (x + (900 - fitted.width) // 2, y + 65))
    closeups.save(OUT / "NEW-closeups.png")
    print(OUT)


if __name__ == "__main__":
    main()
