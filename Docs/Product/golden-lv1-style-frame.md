# Golden Lv1: LAYOUT-SLICE-01 and STYLE-FRAME-01

Status: design target for human review. This is not shipping UI and not a Blueprint/Delta change. The roster comes from [golden-lv1-content-lock.md](golden-lv1-content-lock.md).

Artifacts (regenerable):
- Greybox: `GoldenLv1LayoutReview.CaptureGoldenLv1Layout` (explicit PlayMode test) writes `Builds/golden-lv1/layout/`.
- Style frame: the same run writes the base plates to `Builds/golden-lv1/style/`; then `python Tools/Art/make_golden_lv1_style_frame.py` writes `style-frame-clean.png`, `style-frame-annotated.png` and `current-vs-style-frame.png`.

## Layout decision (LAYOUT-SLICE-01)

The camera framing, the suitcase scale and the top-down camera are unchanged. The suitcase body is 94% of the screen width at 1080×2340.

| Region | Variant A | Variant B | Selected |
|---|---|---|---|
| Objective | Paper note pinned to the open lid's lining, left of the strap buckle | Full-width card under the header | **A**: uses the lid as a real surface and keeps the right-hand X straps and buckle readable. B hides most of the lid lining. |
| Tray | Shared felt panel (as shipped) | One card per remaining item on a shell that hugs the cards | **B**: the A panel stays full width, so with one item left it is a large empty rectangle (about 13% of the screen). B shrinks as items leave. |

Selected layout = **H** (A's note + B's tray), plus:
- **Header:** safe-area-anchored card with the travel icon, "Seviye 1" and "İlk Yolculuk". Identity only; no coins, lives or stars.
- **Bottom dock:** Undo / Restart within thumb reach (140 px targets) and a contextual hero slot in the centre:
  - "Döndür" while the selected item can rotate;
  - otherwise a quiet "N eşya kaldı";
  - later, "Sonraki" after Packed.
  - It never replaces the drop as the completion trigger. It earned its place: it gives Rotate a thumb-reachable home and fills what was leftover canvas.
- **Note sizing:** the note scales into the space between the header and the bed's back edge, never below 60%. It must never cover the playable bed.

Measured at 1080×2340 (`layout-metrics.txt`, `deadspace.txt`): the largest plain-background rectangle is ≤ 9.7% of the screen in every capture, and the tray-to-dock gap is 5.3%.

| Case | Result |
|---|---|
| 1080×2340 | Suitcase dominant, header and note above the bed, tray centred, dock anchored. |
| 9:16 (1080×1920) | Suitcase 80% wide; note scales to fit; dock gap 2.3%. |
| Android cutout (110 px inset) | Header moves below the cutout; note 0.76×. |
| iPhone Dynamic Island (177 / 102 px) | Header and dock both inset; note drops to the 0.6× floor. **Risk:** the rule text gets small (≈ 17 px); UI-SLICE-01 should switch to a compact two-line chip below a size threshold. |

## Mini style guide (STYLE-FRAME-01)

**Palette**
- Ink `#2F3A3C`; muted `#7A7468`.
- Paper `#FBF7EE`; cream card `#F7F1E6`; button fill `#EFE6D6`.
- Teal `#4E9FA2`: satisfied, identity.
- Coral `#E07A5F` / terracotta `#C36F58`: warm accents and warnings.
- Mustard `#D7AA58`: the one hero action and selection.
- Deep teal `#31515D`: details.
- Backdrop: whitewashed wood `#E9DFCD`.

**Typography:** Bricolage Grotesque only.
- ExtraBold: titles (48 header, 27 note title, 42 contextual action).
- SemiBold: body (32 subtitle, 26 rules, 22 captions).
- Minimum body size at 1080 width: 26 px.

**Spacing:** 32 px screen gutter, 24–30 px card padding, 16 px between stacked cards. Every real control is ≥ 132 px (44 pt) square.

**Radius:** header 40, note 16, tray shell 46, item cards 26, dock 52, contextual pill 62 (full round).

**Action hierarchy:**
- Contextual action (Rotate): medium priority. A 350×124 soft-mustard pill (`#EED6A2`) with a 3 px mustard edge and a 5 px under-edge.
- Hero: the full-strength mustard pill (`#D7AA58`, 520×140, 8 px under-edge) is reserved for "Sonraki" after the Packed moment.
- Completion itself is automatic.

**Shadows:** one warm shadow colour, `rgb(58,44,30)`, never black.
- Cards: blur 16–18, y +10, 30%.
- Resting item on a card: blur 10, 38%.
- Lifted / selected item: larger offset (y +14) and blur.
- Suitcase cast shadow on the backdrop: blur 26, +18/+28, 42%.

**UI surfaces:** 9-slice paper with a 1 px top highlight. The note gets a washi-tape accent. No gradients beyond these.

**Objective note:** 500×146 px compact travel note, pinned on the lid left of the buckle, ≥ 40 px clear of the playable bed. On narrow / notch-heavy layouts it becomes a one-line rule chip (text stays ≥ 26 px; tap expands the full note). This is shown on the annotated frame; it is not yet a runtime breakpoint.

**Item material language:** top-down, rounded and tactile; matte fabrics with soft cylinder/puff shading; 1–2 accent stripes from the palette; a visible front-face thickness strip. Same family as the approved sweater: teal / coral / mustard on warm cream.

Towel cues: terry texture, slightly wavy soft outline, stripes that wrap around the roll, a loose outer flap edge, and the fabric spiral on the near end.

**Backdrop:** top-down flat-lay of sunny whitewashed wood planks, with a soft dappled leaf shadow baked into the texture. Edge props stay outside every interaction zone and may sit partly under the dock or off screen: boarding pass, folded map, Santorini postcard, olive sprig, straw-hat brim.

Props are kept quiet so they rank last in the hierarchy:
- ~85% scale, about 60% saturation, about 70% contrast, lifted 12% toward the backdrop tone;
- shadows at 14–20% opacity;
- cropped at the screen edges.

**Icons:** flat single-colour ink glyphs with rounded stroke ends at 8 px stroke (undo, restart, rotate, check), plus the teal travel badge in the header.

**Lighting intent:** a warm key from the top-left (matches the existing `PackingTable` key and the contact-shadow direction), mild contrast and warmth, and a light vignette. This is within the existing post stack; no depth of field.

## Implementation map

| Visible element in the frame | Owner |
|---|---|
| Whitewashed wood flat-lay, leaf shadow, edge props, suitcase cast shadow on the backdrop | BACKDROP-SLICE |
| Header card, objective note (lid-pinned, scaling rule, compact fallback), bottom dock, contextual hero slot, tray shell + per-item cards with reflow, selection outline, typography/palette tokens | UI-SLICE-01 |
| Passport, rolled towel, shampoo, folded sunglasses, travel pouch production meshes + 1K textures; Golden Lv1 catalog/visual mapping | ART-GATE-02B |
| Rule check animation in the note, item lift/settle polish, warm grade and vignette tuning | POLISH-SLICE |
| "Sonraki" in the hero slot after completion, Packed moment restyle to match the paper cards | PACKED-SLICE |
| Real suitcase, interior, sweater, camera framing, safe-area header placement | Existing (accepted); no new work |

Not shown, by design: mascot, coins, stars, lives, a "Kontrol Et" check button, a 3/4 camera, a visible grid, and pocket wording (there is no pocket compartment).
