"""ART-CC02: softened/desaturated copy of the Santorini backdrop for the table only (the polaroid keeps the original),
so the suitcase stays the dominant visual mass. Offline; no runtime blur."""
from pathlib import Path
from PIL import Image, ImageEnhance, ImageFilter

ROOT = Path(__file__).resolve().parents[2] / "Assets/Resources/UiSlice011"
source = Image.open(ROOT / "santorini_vacation.png").convert("RGB")
soft = source.filter(ImageFilter.GaussianBlur(2.6))
soft = ImageEnhance.Color(soft).enhance(0.78)
soft = ImageEnhance.Contrast(soft).enhance(0.92)
soft.save(ROOT / "santorini_vacation_soft.png")
print(ROOT / "santorini_vacation_soft.png", soft.size)
