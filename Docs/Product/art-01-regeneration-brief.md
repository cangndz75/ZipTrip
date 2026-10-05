# ART-01 source proportion notes — Passport, Sunglasses, Travel Organizer

The existing canonical Golden Lv1 GLBs are the accepted ART-01 inputs. Their source proportions are
non-blocking unless the production capture reveals an obvious visual or gameplay problem; no `_v2.glb`
replacement is required for ART-01 completion. Runtime fitting remains uniform-scale only.

Scope note: these measurements document source-shape imperfections only. Item IDs, footprints, rules,
solver, StateHash and lv1-fit are unchanged. No stretching.

Pipeline: ADR-0009 unchanged — ChatGPT image (locked chat, both golden references attached to every request,
one object, pure white background, 3/4 view from slightly above) → contact-sheet review at ~50 pt → Tripo
image-to-3D (H3.1, Ultra Mesh off, AI Complete off, Texture on 2K, Remove Lighting on, PBR on, Triangle).

## Proportion targets (top-down, as the item lies in the suitcase)

`Tools/Art/prepare_art01.py` reports fill for review but does not reject a canonical source based on aspect ratio.

| Item | Footprint | Ideal width ÷ length | Accepted band | Current source | Tripo polycount |
|---|---|---|---|---|---|
| Passport | 1×2 | 0.60 (stylized booklet) | 0.55 – 0.65 (≈73–86% length fill) | 0.76 (rejected, 62%) | 5K (simple) |
| Sunglasses | 2×1 | 2.1 (length ÷ width 0.47) | 1.9 – 2.35 | 3.1 (rejected) | 10K (detailed) |
| Travel Organizer | 2×3 | 0.66 | 0.59 – 0.73 | 0.91 (rejected) | 10K (detailed) |

Thickness is free; the pipeline caps visual height at 0.5 cell.

## Image requests (append to the locked ADR-0009 chat; attach both golden references)

**Passport.** A literal premium passport booklet, closed, lying flat, with a smooth premium navy cover. Slightly
taller than a real passport: the cover is about 1.65 times as long as it is wide (width ÷ length ≈ 0.60) — still
clearly a passport, not a long wallet. Plain cover: no globe, crest, emblem, logo, text, debossed mark or circular
panel of any kind. Restrained gold edge binding and a slim gold spine detail only; cream page block visible at the
edges. Clean, slightly soft leather, subtle tonal variation, soft sheen.

**Sunglasses.** Premium acetate sunglasses, arms folded behind the frame, lying flat with the lenses facing up.
Seen from above the whole object is about twice as wide as it is deep: choose a naturally tall lens shape —
oversized soft-square or rounded-rectangle lenses — rather than widening the frame. Thick tortoiseshell or
caramel acetate frame, dark solid opaque lenses (no transparency, no reflections), clearly separated from the
frame by geometry. Small brass hinge pins only.

**Travel Organizer.** Premium zip-around travel organizer, closed, lying flat, portrait rectangle about 2:3
(two units wide, three long), soft rounded corners. Sage or olive textile body, darker leather edge binding,
one brass zipper running around the edge with an enlarged pull, one front flap or panel break. No logo, text,
or stitched pattern.

## Future replacement hand-off

Any later source regeneration belongs to a separate art pass. ART-01 continues to use the canonical filenames;
the stable Passport emblem cleanup remains part of the runtime build while the current source is in use.
