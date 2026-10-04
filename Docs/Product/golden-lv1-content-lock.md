# GOLDEN-LV1-CONTENT-LOCK

Status: proposal for human review. It records the Golden Level 1 content target. It does not replace Blueprint v1.1 / v1.2 Delta, and the shipped `lv1-fit` is unchanged.

Proof fixture (non-shipping): `Assets/Scripts/Tests/EditMode/Fixtures/LevelsV2/golden-lv1-candidate.json`, tested by `GoldenLv1CandidateTests`.

## Roster

| Instance | Definition id | Footprint (w×h) | Thickness | Rotations | Initial location | Source of footprint |
|---|---|---|---|---|---|---|
| passport-1 | `passport` | 1×2 | 1 | 0°, 90° | suitcase `main` (3,0) r0 | Blueprint Ek A (canonical) |
| sweater-1 | `sweater` (open) | 3×3 | 1 | 0° | suitcase `main` (0,0) r0 | shipped catalog / Ek A |
| towel-1 | `towel` (open only) | 1×4 | 1 | 0°, 90° | suitcase `main` (0,3) r0 | Blueprint Ek A (canonical) |
| shampoo-1 | `shampoo` | 1×3 | 1 | 0°, 90° | Source Tray | new |
| sunglasses-1 | `sunglasses` | 2×1 | 1 | 0°, 90° | Source Tray | new |
| travel-pouch-1 | `travel-pouch` | 2×3 | 1 | 0°, 90° | Source Tray | new |

Footprint decisions:
- The ticket proposed passport 2×3 and towel 3×2. Blueprint Ek A already defines `passport` 1×2 and `towel` 1×4 (open) / 2×2 (folded). By the authority order the canonical sizes stand (human decision 2026-10-04). They are also more believable physically: a 2×3 passport would be the size of the book.
- `towel` uses only its open state here. Its canonical folded state (2×2) is deferred so that Golden Lv1 needs no Fold.
- The new items are sized to recover density, as the human decision directed: shampoo 1×3 (same as the canonical `bottle`), sunglasses 2×1, travel pouch 2×3.

## Board, zones, rules

Board: one compartment `main`, 5×7, L = 1, every cell valid. y = 0 is the back row, next to the lid hinge. Each column belongs to at most one zone, so the two zones cannot overlap.

```text
      x0 x1 x2 x3 x4
y0     S  S  S  P  .     upper
y1     S  S  S  P  .     upper
y2     S  S  S  r  r     (r = right zone)
y3     T  .  .  r  r
y4     T  .  .  r  r
y5     T  .  .  r  r
y6     T  .  .  r  r
```

- Zone `upper`: rows 0–1, all five columns (10 cells).
- Zone `right`: columns 3–4, rows 2–6 (10 cells).
- Rule `passport-upper`: Zone, subject `instance:passport-1`, zone `upper`. Display text: "Pasaport üst bölgede olmalı."
- Rule `shampoo-right`: Zone, subject `instance:shampoo-1`, zone `right`. Display text: "Şampuan sağ bölgede olmalı."
- No pocket wording: there is no pocket compartment. Per ADR-0006, pockets would be separate compartments.

Objective: `pack`, all six instances required. Staging capacity is 0. No Fold, Compress or Nest.

## Occupancy and solver

- Solved occupancy: 26 / 35 = 74.3% (the ticket's 29/35 target was dropped with the canonical footprints, by the human decision above).
- Initial open space: 20 cells, a 2×4 pocket at left-middle (x1–2, y3–6), the 2×5 right strip, and (4,0)–(4,1).
- PackSolverV2: **Solvable**, ≥ 50 solutions (search cap reached), 3 pre-placed items held fixed.
  - Rule coupling: `shampoo-right` is Coupled; `passport-upper` is Inconclusive (the passport is pre-placed and held fixed by the solver; players can still move it, and leaving `upper` breaks the rule; this is tested).
  - Greedy: false. If the pouch is dropped into the right strip first, the shampoo no longer fits there. This is the level's one recoverable "read the rule" moment.
- Replay: the first solution (pouch (1,3), shampoo (3,2), sunglasses (3,5)) completes on the last accepted move, through `PuzzleSession` (3 moves).
- Negative: all items packed with the shampoo at (1,3) → not complete; `shampoo-right` offends.

## Asset production still needed (ART-GATE-02B)

Passport, towel (rolled, 1×4), shampoo bottle (1×3), sunglasses (folded, 2×1), travel pouch (2×3). Sweater open is the approved asset and is reused.

Integration (catalog entries, golden visual mapping, replacing `lv1-fit`) is a later production ticket.
