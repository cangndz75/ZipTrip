from pathlib import Path
from PIL import Image, ImageDraw, ImageFont


ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "Builds" / "visual-align-03-revision"
CAP = OUT / "captures"


def font(size: int):
    candidates = [
        ROOT / "Assets" / "Art" / "Fonts" / "BricolageGrotesque-SemiBold.ttf",
        Path("C:/Windows/Fonts/arialbd.ttf"),
    ]
    for candidate in candidates:
        if candidate.exists():
            return ImageFont.truetype(str(candidate), size)
    return ImageFont.load_default()


def fit_phone(path: Path, width=420, height=910):
    image = Image.open(path).convert("RGB")
    image.thumbnail((width, height), Image.Resampling.LANCZOS)
    canvas = Image.new("RGB", (width, height), "#e8ddc9")
    canvas.paste(image, ((width - image.width) // 2, (height - image.height) // 2))
    return canvas


def labelled(image, label, width=None):
    width = width or image.width
    canvas = Image.new("RGB", (width, image.height + 58), "#173f46")
    canvas.paste(image, ((width - image.width) // 2, 58))
    draw = ImageDraw.Draw(canvas)
    draw.text((width // 2, 29), label, font=font(26), fill="white", anchor="mm")
    return canvas


def comparison():
    target = ROOT / "Builds" / "approved-target-lv1.png"
    previous = OUT / "va03-frozen" / "A-level-start.png"
    current = CAP / "A-level-start.png"
    target_image = Image.open(target).convert("RGB")
    # Exact human-supplied Lv1 north star. Never crop a previous comparison strip or substitute Lv10.
    cells = [
        labelled(fit_phone_from_image(target_image), "APPROVED TARGET"),
        labelled(fit_phone(previous), "VA03"),
        labelled(fit_phone(current), "NEW"),
    ]
    sheet = Image.new("RGB", (sum(cell.width for cell in cells), max(cell.height for cell in cells)), "#e8ddc9")
    x = 0
    for cell in cells:
        sheet.paste(cell, (x, 0))
        x += cell.width
    sheet.save(OUT / "01-approved-target-va03-new.png", quality=95)


def fit_phone_from_image(image, width=420, height=910):
    image = image.copy()
    image.thumbnail((width, height), Image.Resampling.LANCZOS)
    canvas = Image.new("RGB", (width, height), "#e8ddc9")
    canvas.paste(image, ((width - image.width) // 2, (height - image.height) // 2))
    return canvas


def contact():
    names = [
        ("A-level-start.png", "A  LEVEL START"),
        ("B-selected-rotate-visible.png", "B  SELECTED / ROTATE"),
        ("C-mid-drag.png", "C  MID-DRAG"),
        ("D-one-item-remaining.png", "D  ONE REMAINING"),
        ("E-pre-completion.png", "E  PRE-COMPLETION"),
        ("F-zip02-mid-close.png", "F  ZIP-02 MID-CLOSE"),
        ("G-final-paketlendi-sonraki.png", "G  FINAL / SONRAKI"),
    ]
    cells = [labelled(fit_phone(CAP / name, 330, 715), label, 330) for name, label in names]
    sheet = Image.new("RGB", (1320, 2 * cells[0].height), "#e8ddc9")
    for index, cell in enumerate(cells):
        sheet.paste(cell, ((index % 4) * 330, (index // 4) * cell.height))
    sheet.save(OUT / "02-new-a-g-contact-sheet.png", quality=95)


def closeups():
    start = Image.open(CAP / "A-level-start.png").convert("RGB")
    selected = Image.open(CAP / "B-selected-rotate-visible.png").convert("RGB")
    final = Image.open(CAP / "G-final-paketlendi-sonraki.png").convert("RGB")
    crops = [
        (start.crop((120, 0, 960, 210)), "HEADER"),
        (start.crop((20, 180, 1060, 570)), "MISSION CARD"),
        (selected.crop((40, 1690, 1040, 2200)), "STAGING DOCK"),
        (selected.crop((80, 2160, 1000, 2340)), "CONTROLS"),
        (final.crop((130, 1760, 950, 2320)), "COMPLETION ACTIONS"),
    ]
    cards = []
    for crop, label in crops:
        crop.thumbnail((620, 420), Image.Resampling.LANCZOS)
        card = Image.new("RGB", (660, 500), "#e8ddc9")
        ImageDraw.Draw(card).rectangle((0, 0, 660, 58), fill="#173f46")
        ImageDraw.Draw(card).text((330, 29), label, font=font(26), fill="white", anchor="mm")
        card.paste(crop, ((660 - crop.width) // 2, 68 + (420 - crop.height) // 2))
        cards.append(card)
    sheet = Image.new("RGB", (1320, 1500), "#e8ddc9")
    for index, card in enumerate(cards):
        sheet.paste(card, ((index % 2) * 660, (index // 2) * 500))
    sheet.save(OUT / "03-new-closeups.png", quality=95)


if __name__ == "__main__":
    OUT.mkdir(parents=True, exist_ok=True)
    comparison()
    contact()
    closeups()
    print(OUT)
