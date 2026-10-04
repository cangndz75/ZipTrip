# Golden Lv1: LAYOUT-SLICE-01 and STYLE-FRAME-01

Status: design target for human review. This is not shipping UI and not a Blueprint/Delta change. The roster comes from [golden-lv1-content-lock.md](golden-lv1-content-lock.md).

**ADR-0007 update (proposed, 2026-10-04):**
- The layout decision below (LAYOUT-SLICE-01) stays locked.
- In the mini style guide, palette, item material language, hero action, backdrop and lighting move to the ADR-0007 direction; the changed parts are marked **[ADR-0007]**.
- STYLE-FRAME-01 is no longer the visual ceiling. The binding benchmark becomes the in-engine Golden Lv1 reference capture judged by the Golden Lv1 Visual Gate (ADR-0007 Decision 6).

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

**Palette [ADR-0007]:** use the token table in [art-bible.md](art-bible.md#palet-tokenları).
- Mapping from STYLE-FRAME-01:
  - ink → `ink`
  - paper / cream → `paper` / `cream`
  - teal (satisfied) → `teal`
  - coral / terracotta → `coral`
  - mustard (Rotate) → `sun`
  - new `aegean` for numbered badges, highlighted rule keywords and selection
- The STYLE-FRAME-01 hex values (`#2F3A3C`, `#FBF7EE`, `#4E9FA2`, `#E07A5F`, `#D7AA58`, `#31515D`, `#E9DFCD`) are superseded.

**Typography:** Bricolage Grotesque only. Weight, size and colour tuning toward the target is allowed; the type family and the minimum sizes stay.
- ExtraBold: titles (48 header, 27 note title, 42 contextual action).
- SemiBold: body (32 subtitle, 26 rules, 22 captions).
- Minimum body size at 1080 width: 26 px.

**Spacing:** 32 px screen gutter, 24–30 px card padding, 16 px between stacked cards. Every real control is ≥ 132 px (44 pt) square.

**Radius:** header 40, note 16, tray shell 46, item cards 26, dock 52, contextual pill 62 (full round).

**Action hierarchy:**
- Contextual action (Rotate): medium priority, secondary. A 350×124 soft pill in the `sun` family with a 3 px edge and a 5 px under-edge. Never green.
- Hero **[ADR-0007]**: the green premium-casual tier (`hero-green`, 520×140, 8 px under-edge `#24892B`, glossy top highlight) is reserved for "Sonraki" after the Zip It ritual completes.
  - It never appears during active play.
  - It is never a check or confirm button.
- Completion itself is automatic. There is no `Kontrol Et`.

**Shadows:** one warm shadow colour, `rgb(58,44,30)`, never black.
- Cards: blur 16–18, y +10, 30%.
- Resting item on a card: blur 10, 38%.
- Lifted / selected item: larger offset (y +14) and blur.
- Suitcase cast shadow on the backdrop: blur 26, +18/+28, 42%.

**UI surfaces:** 9-slice paper with a 1 px top highlight. The note gets a washi-tape accent. No gradients beyond these.

**Objective note:** 500×146 px compact travel note, pinned on the lid left of the buckle, ≥ 40 px clear of the playable bed. On narrow / notch-heavy layouts it becomes a one-line rule chip (text stays ≥ 26 px; tap expands the full note). This is shown on the annotated frame; it is not yet a runtime breakpoint.

**Item material language [ADR-0007]:** top-down, rounded, tactile and **vibrant**, with real-object fidelity.
- Each item is recognisable without a label at 1080×2340 in about one second (Gate A).
- Materials separate as in the art bible table (Gate B): fabric, leather, plastic, paper, metal.
- A visible front-face thickness strip and a clear contact shadow / AO on the lining.
- The approved sweater keeps its mesh and art direction; it gets a restrained colour or material tune only if it falls outside the new family.

Golden Lv1 item cues:
- **Passport:** `passport-red` cover, embossed brass emblem, visible page-block edge.
- **Shampoo:** glossy plastic bottle with a cap and a readable label shape, not text.
- **Sunglasses:** folded frame with dark tinted lenses and a specular highlight. It must not read as two black discs (current proxy).
- **Travel pouch:** fabric or leather with a zip and a puller.

Towel cues: terry texture, slightly wavy soft outline, stripes that wrap around the roll, a loose outer flap edge, and the fabric spiral on the near end.

**Backdrop:** top-down flat-lay of sunny whitewashed wood planks, with a soft dappled leaf shadow baked into the texture. Edge props stay outside every interaction zone and may sit partly under the dock or off screen: boarding pass, folded map, Santorini postcard, olive sprig, straw-hat brim.

Props rank last in the hierarchy **[ADR-0007]**. They may carry destination fantasy and more colour than STYLE-FRAME-01, but they never get higher contrast than the puzzle (Gate C) and never compete with the suitcase (Gate D).
- Starting values: ~85% scale, shadows at 14–20% opacity, cropped at the screen edges.
- The STYLE-FRAME-01 caps (about 60% saturation, about 70% contrast) are now tuning starting points, not limits. The in-engine reference capture decides.

**Icons:** flat single-colour ink glyphs with rounded stroke ends at 8 px stroke (undo, restart, rotate, check), plus the teal travel badge in the header.

**Lighting intent [ADR-0007]:** a warm key from the top-left (matches the existing `PackingTable` key and the contact-shadow direction).
- Richer contact shadows and AO than STYLE-FRAME-01, so items sit in the padded interior with visible depth.
- Warm grade with more vibrancy, and a light vignette.
- This stays within the existing post stack; no depth of field.

## Implementation map

| Visible element in the frame | Owner |
|---|---|
| Whitewashed wood flat-lay, leaf shadow, edge props, suitcase cast shadow on the backdrop | BACKDROP-SLICE |
| Header card, objective note (lid-pinned, scaling rule, compact fallback), bottom dock, contextual hero slot, tray shell + per-item cards with reflow, selection outline, typography/palette tokens | UI-SLICE-01 |
| Passport, rolled towel, shampoo, folded sunglasses, travel pouch production meshes + 1K textures; Golden Lv1 catalog/visual mapping | ART-GATE-02B |
| Rule check animation in the note, item lift/settle polish, warm grade and vignette tuning | POLISH-SLICE |
| "Sonraki" in the hero slot after completion (green hero tier), Packed moment restyle | PACKED-SLICE |
| Padded interior depth, seams / pockets / straps, material richness, contact / AO, with no fake grid or slot affordance | SUITCASE-RICHNESS pass (ADR-0007 step E) |
| Rule-card icons and highlighted keywords, Source Tray item names, palette / contrast / iconography tuning of the UI-SLICE-01.1 skin | UI skin tuning (no redesign) |
| Real suitcase, interior, sweater, camera framing, safe-area header placement | Existing (accepted); geometry locked, material / colour polish allowed (ADR-0007 Decision 4) |

**Order [ADR-0007 Decision 7]:**
- A: docs
- B: Golden Lv1 → shipped Lv1
- C: ART-GATE-02B
- D: asset integration + parity review
- E: suitcase richness
- F: PACKED-SLICE
- G: POLISH / JUICE
- H: physical-device gate
- I: Lv2–Lv10

Not shown, by design: mascot and large logo in gameplay (brand use only: store, loading, transitions, meta, onboarding), coins, stars, lives, move limits, a "Kontrol Et" check button, a green CTA during active play, a 3/4 camera, a visible grid or "+" slots, and pocket wording (there is no pocket compartment).
