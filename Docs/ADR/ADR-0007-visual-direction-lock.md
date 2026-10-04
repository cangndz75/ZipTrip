# ADR-0007: Visual Direction Lock

- **Status:** Proposed (human review)
- **Date:** 2026-10-04
- **Amends:** Blueprint v1.1 §16 Art Direction (visual ceiling only), `Docs/Product/art-bible.md` (MG-1 mini art bible), `Docs/Product/golden-lv1-style-frame.md` (STYLE-FRAME-01)
- **Does not amend:** ADR-0006 (gameplay contract), ADR-0002 (fixed camera), Delta Δ-01 (Phase A scope lock), the Golden Lv1 content lock

## Direction statement

> ZipTrip targets a vibrant premium-casual travel-puzzle aesthetic: tactile, richly materialed, highly readable at phone scale, warm and aspirational, with the visual finish of the approved target mockups while preserving the top-down spatial-puzzle gameplay contracts defined by the Blueprint and ADRs.

In short: we take the mockups' **visual quality**. We do not take all of their **mechanics**.

## Context

On 2026-10-04 eight target mockups were supplied (Seviye 1 "İlk Yolculuk", 10, 20, 40, 50, 60, 70, 90). The gap report (`Builds/target-gap-report/ZipTrip_Hedef_Mesafe_Raporu.pdf`) found:

- The engine is close to the target. Zone, Access, Adjacency±, two layers, compartments, staging, Fold/Compress/Nest and the Pack/Extract/Repack objectives already exist.
- The visible product is far from the target. Only three golden item assets exist, the Golden Lv1 items are procedural flat shapes, and the UI is a mid-saturation paper skin.
- The mockups conflict with the MG-1 art bible ("orta doygunluk") and with gameplay contracts: `Kontrol Et`, mascot over the play area, coins, lives, move limits, stars, Balance/weight, Group, sliding blocks.

Without a written direction, every UI or art ticket would have to pick a side, and work done on the wrong side gets redone. This ADR picks the side.

## Decisions

### 1. Status of the target mockups

The mockups are the **visual north star and art-direction spec**. They are spec for:

- premium casual quality and "finished game" perception
- saturation, vibrancy and colour separation
- tactile material richness
- item readability and fidelity
- suitcase richness (padding, seams, straps, hardware)
- travel / vacation fantasy
- visual density, depth and polish

They are **not** spec for mechanics, meta or every UI element. Gameplay and meta stay governed by Blueprint v1.1, the v1.2 Delta and ADR-0006.

| Element seen in the mockups | Decision | Authority |
|---|---|---|
| Vibrant palette, rich materials, item fidelity, depth | **Take** | this ADR |
| Rule card with item icons, numbered badges, highlighted keywords | **Take** (presentation of existing rules) | this ADR |
| Item names on Source Tray cards, per-definition count (e.g. 0/2) | **Take** | this ADR |
| Destination fantasy in the backdrop | **Take**, under the hierarchy rule (Gate C) | this ADR |
| Green premium-casual hero tier | **Take, only for "Sonraki" after Packed** (Decision 3) | this ADR |
| `Kontrol Et` / `Onayla` / any check button | **Reject** | ADR-0006 #4 |
| Mascot and large logo in the gameplay screen | **Reject in gameplay** (Decision 2) | Blueprint §16 |
| Coins / currency | Phase B | Δ-01 |
| Lives, stars, move limits, gifts | **Reject for now** | ADR-0006 slice scope |
| Balance, weight limits, Group, sliding blocks, Hint, Shuffle | Not taken; needs its own ADR | ADR-0006 |
| Visible cell grid, "+" slots, glowing target cells | **Adapt** to the gameplay contract: no fake slot affordance; cell guides only where the contract allows (drag preview) | ADR-0006 #12, ZT-040B |
| Level numbers above 10, level map | Out of Phase A | Δ-01 |

### 2. Mascot and logo

- The gameplay screen has **no large mascot** and **no large ZipTrip logo**. Blueprint §16 is kept as written.
- The mascot is not rejected as a brand asset. It may appear in: store screenshots and listing art, loading, level complete / level transition, level map and meta screens, onboarding, and promotional / marketing creative.
- During active play no mascot covers the suitcase or the puzzle area.
- In the gameplay HUD, brand identity is carried by iconography, palette and travel motifs, not by the logo.

Mockup mascot + logo: **visual brand reference = yes; gameplay layout spec = no.**

### 3. Action hierarchy and the green hero tier

- `Kontrol Et` does not exist. ADR-0006 automatic completion stays.
- There is **no green hero CTA during active play**. Contextual actions such as Rotate stay secondary.
- After the Zip It ritual completes successfully, **"Sonraki"** is the one strong hero CTA and uses the green premium-casual visual tier from the target.
- The hero tier never appears before completion and never triggers completion.

### 4. Existing approved assets

Nothing approved is thrown away. Geometry, interaction and layout locks stay. The old "orta doygunluk" art bible is no longer the visual ceiling.

| Asset | Locked | Allowed later |
|---|---|---|
| Golden cabin suitcase | Model, proportions, hinge | Material richness, saturation, roughness, lighting polish |
| Interior | Geometry, board relationship | Material, AO and lighting polish |
| Sweater | Mesh and art direction | Restrained material / colour tuning only if it falls outside the new palette family. No regeneration. |
| UI-SLICE-01.1 | Architecture, interaction, responsive layout | Saturation, contrast, iconography and hero-tier tuning toward the target. No redesign from scratch. |

ART-GATE-02B (passport, towel, shampoo, sunglasses, travel pouch) is produced **directly to the new direction**. The MG-1 mid-saturation palette is not its target.

### 5. Palette and tokens

- Numeric palette values are defined as **art bible tokens** (`art-bible.md`), not copied as RGB values from the AI mockups.
- Tokens are a starting point; the binding benchmark is the in-engine Golden Lv1 reference capture (Decision 6).

### 6. Golden Lv1 Visual Gate

There is no pixel parity with the AI mockups. The single production benchmark is a **real in-engine Golden Lv1 reference capture**.

Golden Lv1 passes only when every criterion below passes.

**A. Item readability**
- Each item is recognisable without a label in about one second on a real 1080×2340 gameplay screenshot.
- Passport, towel, shampoo, sunglasses and travel pouch are understood without text labels.

**B. Material separation**
- Fabric, leather, plastic, paper and metal do not read as the same material.
- At phone scale at least a base-colour difference plus a roughness or depth difference is readable.

**C. Screen hierarchy.** First-glance order:
1. suitcase / puzzle state
2. objectives
3. loose (Source Tray) items
4. active contextual action
5. decorative environment

Backdrop props never get higher contrast than the puzzle.

**D. Suitcase dominance**
- The suitcase stays the main visual object.
- UI and environment never overpower it.

**E. Premium casual test**
- The current build, the Golden target capture and two or three high-quality casual puzzle references are compared side by side at the same phone size.
- Question: "Which one is clearly a prototype?"
- If ZipTrip clearly stands out as the prototype, the gate **fails**.

**F. Proxy ban.** At Golden Lv1 approval there is no debug or proxy item, no placeholder label and no procedural flat block.

**G. Phone-scale gate.** A zoomed editor view is not acceptance evidence. A native-scale 1080×2340 capture is required.

**H. Device gate.** Before the final lock, readability, colour, contrast, drag and completion are checked on a physical phone.

### 7. Execution order

| Step | Work |
|---|---|
| A | ADR-0007 + art bible / style frame docs |
| B | Golden Lv1 candidate → real shipped Lv1 integration |
| C | ART-GATE-02B production assets (new direction) |
| D | Golden Lv1 asset integration + visual parity review |
| E | Suitcase / material richness pass |
| F | PACKED-SLICE |
| G | POLISH / JUICE |
| H | Physical-device Golden Lv1 gate (Decision 6) |
| I | Only then: Lv2–Lv10 validation content |

- B and C may run in parallel. If they touch the same files, finish the shipping integration first and leave a clean checkpoint.
- **No Lv2–Lv10 art or content expansion starts before Golden Lv1 passes the gate.**

## Consequences

**Positive**
- Every art and UI ticket has one visual target and one acceptance gate.
- Engine and gameplay contracts are untouched; no mechanic enters through the art direction.
- Approved assets keep their geometry and layout, so the change is a material, colour and finish pass, not a rebuild.

**Negative / costs**
- Higher art cost per item: richer materials, more texture work, more lighting tuning.
- Existing assets may need a material / colour pass to sit in the new family.
- Gate E is a comparative judgement; it needs a fixed reference set and a named reviewer.
- Content expansion (Lv2–Lv10) waits for Golden Lv1, which delays the ADR-0006 slice questions about Extract and Repack.

## Alternatives considered

- **Keep MG-1 mid saturation:** rejected. It is the gap the target mockups are meant to close.
- **Pixel parity with the mockups:** rejected. They are AI-generated and photoreal; real-time mobile Unity cannot match them, and they contain non-physical layouts.
- **Adopt the mockups wholesale (mechanics included):** rejected. It reintroduces the static "Kontrol Et" loop that ADR-0006 replaced, plus out-of-scope meta.
- **Scale Lv2–Lv10 first, polish later:** rejected. It multiplies the art debt across ten levels before the target is proven once.

## Process

- Acceptance needs a v1.2 Delta entry (Δ-22) that authorises this ADR for the visual ceiling of Blueprint §16, in the same pattern as Δ-21. Proposed text: *"`Docs/ADR/ADR-0007-visual-direction-lock.md` is accepted and is the art-direction authority. It raises the visual ceiling of Blueprint v1.1 §16 to the target mockups. §16's avoid list (preschool look, characters covering gameplay, large gameplay logo, child-like UI) stays in force. Gameplay and meta are unchanged."*
- Open item: where the eight target mockups live in the repo (they are binary art and fall under `.gitattributes` / Git LFS rules).
- Visual direction changes after acceptance need an amendment to this ADR.
