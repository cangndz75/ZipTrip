# ADR-0006: Dynamic Packing Puzzle Contract

- **Status:** Accepted
- **Date:** 2026-10-03
- **Amends:** Engineering Blueprint v1.1 + v1.2 Delta
- **Supersedes:** ZipTrip GDD v0.3 as implementation authority (v0.3 is kept as future content exploration only)

## Context

GDD v0.3 tried to answer "how do we carry 300 levels?" and in doing so replaced the locked core game with a content framework: 20 rule families, 10 mini-games, 6 item states, a 30-level slice, and a static "place items, press Kontrol Et" loop. Pack + Repack + Extract, which carry ZipTrip's identity, disappeared from it.

A static final-state constraint puzzle collapses into drag-and-drop trial and error on mobile. ZipTrip's distinctive value is temporal: managing space, layers and access order across state changes. This ADR locks that contract so ZT implementation tickets have a single, stable reference.

## Product statement

> ZipTrip is a spatial puzzle game where you solve Pack, Extract and Repack problems by managing the space, layers and access order inside a suitcase.

Player loop: **prepare → place → cover → re-access when needed → add new items → rearrange.**

## Decisions

### Core profiles and puzzle nature

1. **Core profiles are Pack, Extract and Repack.** All three are in launch scope.
   - **Pack:** empty or partly empty suitcase; fit the required items legally.
   - **Extract:** suitcase arrives full; a target item must be taken out to a destination; the rest must remain legal.
   - **Repack:** suitcase arrives full and legal; new items are added; the player must find a new legal layout while disturbing the existing one as little as staging allows.
2. **The puzzle includes temporal state transitions.** Final-state validation alone is not the core.
3. **Layer / Access is a first-class mechanic**, not one rule among equals. It is the temporal backbone that Extract and Repack depend on.
4. **No `Kontrol Et` button.** Live feedback during play; completion triggers automatically (see 16).

### Rule vocabulary

5. **The rule vocabulary is small and parametric.** New content comes from parameters, profile and board elements, not new rule types.
6. **Puzzle rules:** Zone, Adjacency ± (required / forbidden), Group, Access, Balance.
   - Group = all items with a tag form one connected component over the adjacency graph.
   - Access = item must be accessible in the final state (derived from layers, see 12).
   - Balance = left/right weight. Used sparingly; first candidate for removal if telemetry shows confusion. Total-weight limits are out of scope until a Selection mechanic exists.
   - **Rule semantics (clarification).**
     - Selectors range over the active rule domain only (Decision 16). Unary rules (Zone, Access): every selected subject must satisfy the predicate.
     - Zone(Z) holds for an item only when all occupied XY columns of its effective suitcase placement are in Z; touching Z with part of the footprint is not enough. Zones apply identically across layers (Decision 12).
     - A nested child's effective placement is that of its outermost parent. If that parent is not in the suitcase (Source Tray, staging, destination), the child satisfies no Zone rule.
     - AdjacencyRequired(subjects, targets): every selected subject is adjacent to at least one distinct selected target. AdjacencyForbidden(subjects, targets): no selected subject–target pair is adjacent. An item never satisfies its own adjacency requirement. A nested child has no adjacency while nested (unlike Zone, it does not inherit its parent's contacts).
     - Rule violations never make a move illegal; they only affect rule status (Decision 15).
7. **Fit is not a rule; it is a board invariant.**

### Modifiers

8. **Item modifiers are Fold, Compress and Nest.** Each manipulates a different dimension:

| Modifier | Dimension | Effect |
|---|---|---|
| Fold | X/Y | Changes footprint shape. Area stays the same or close; never a free size reduction. |
| Compress | Z | Changes thickness (e.g. 2 layers → 1). May disable other options (e.g. nesting). |
| Nest | Containment | Small item goes inside another item's internal capacity; occupies no grid cells. |

Roll, Vacuum, Protected and Travel Bottle are not separate states.

- **Where Fold and Compress happen (clarification).** Fold and Compress are preparation operations. An item's Fold/Compress state may be configured only while the item is outside the suitcase: in the Source Tray (Decision 11) or in staging.
  - They may not be applied in place to an item occupying suitcase cells, nor to a nested child while it remains nested.
  - If a packed item needs a different Fold/Compress state (e.g. during Repack), it must first become accessible, be relocated to staging, and then be re-placed in the desired authored state.
  - There is no separate zero-cost Fold/Compress gameplay action. The chosen authored state is committed as part of the next placement/relocation; previewing or selecting a state before commit is not a move (Decision 18).
  - Nest remains a containment relocation: accessible child → accessible compatible parent is one move, and a parent moves with all its nested children (Decision 17).
- **Compress is Z-only (clarification).** A Compress transition keeps the same normalized XY footprint and strictly reduces thickness (with launch thickness 1..2, that is 2 → 1). Rotation stays a placement property. Nest capability is per item definition, not per state, in the validation slice.

### Ritual and scope

9. **Zip It is a completion ritual, not a mini-game.** Final legal placement → short pause → rule indicators complete → straps snap → lid closes → zip → victory burst. X-Ray Control is the only mini-game research candidate; all others are backlog.
10. **First milestone is a 10-level design validation slice** (see Slice). Not shipping onboarding; no difficulty-curve or retention conclusions are drawn from it.

### Board model

11. **Staging capacity is a core board parameter, not a rule.** Without a limit, Extract becomes "dump and retrieve" and Repack becomes "Pack from scratch."
    - Slice: `stagingCapacity = 2`. Later levels may use 1 / 2 / 3; capacity is a difficulty knob in its own right (3 = easy, 2 = normal, 1 = hard on the same board).
    - Each slot holds exactly one item (a parent with nested children counts as one item and keeps its children).
    - Slots carry no geometry or rotation, accept no nesting, and never block each other. Bed/table is a visual metaphor only.
    - **Slot identity (clarification, ZT-041, human-approved 2026-10-03).** A staged item's location carries its slot index `0 … stagingCapacity − 1`; the index is canonical state (part of the state hash) so undo restores the exact slot. A move into staging names a slot or takes the lowest free one; naming an occupied or out-of-range slot is rejected. Two items never share a slot. Staging → staging is not a Decision 18 move and stays unsupported.
    - **Source Tray vs staging (clarification).** The Source Tray (unplaced pool) is distinct from staging.
      - The Source Tray holds required items that have not yet entered the suitcase. Pack starts with its unpacked required items there; Repack may introduce new incoming items (e.g. souvenirs) there.
      - The Source Tray has no capacity limit in the validation slice and does not consume staging capacity.
      - Staging is only temporary holding space for items removed from the suitcase during rearrangement.
      - Source Tray items take no part in suitcase blocking, support or adjacency until placed.
      - A committed Source Tray → suitcase placement is one move (Decision 18). Source Tray → staging is not supported in the slice.
      - Completion requires every required Source Tray item to have entered the suitcase where the objective requires it (Decision 16).
12. **2.5D board, `L = 2`.**
    - Board is `W × H × L`; zones are per cell. Pockets are separate compartments with their own small grids (typically `L = 1`).
    - **Zone granularity (clarification).** Zones are XY-column metadata: a zone on `(x, y)` applies to every layer of that column, each column has at most one zone, and layer-sensitive play is expressed through Layer / Access, never per-layer zones.
    - Item thickness is per state (1 or 2 layers).
    - **Full support:** every occupied cell of an upper-layer item's footprint must be supported by some lower-layer item. Multiple lower items may share support. No physics, centre of mass or partial-support thresholds.
    - **Blocking:** `blockers(i)` = items on a higher layer whose footprint overlaps `i`. `accessible(i) ⇔ blockers(i) = ∅`.
    - **Adjacency counts vertical neighbours** as well as lateral ones (a snow globe on a sweater is protected; shoes on clothes are touching).
    - Hidden lower-layer content must be readable while the upper layer is occupied (ghost / X-ray view).
    - **Physical occupancy (clarification).** "Overlap" always means physical volume overlap, never XY overlap.
      - A placement with footprint `F` (current state, current rotation), base layer `layer` and current-state `thickness` occupies the physical cells `{ (x, y, z) | (x, y) ∈ F, z ∈ [layer, layer + thickness) }`.
      - A placement is valid only when (1) `layer ≥ 0` and `layer + thickness ≤ L` for its compartment, and (2) no two items occupy the same physical `(x, y, z)` cell. Footprint cells must also lie inside the compartment's valid cells.
      - XY footprint overlap across **different** layers is legal and intentional: it is what creates support and blocking.
      - With launch board `L = 2`: a thickness-1 item may sit on layer 0 or layer 1; a thickness-2 item can only start at layer 0.
      - Full support still applies on top of this: for every footprint cell `(x, y)` of an item with `layer > 0`, the cell `(x, y, layer − 1)` must be occupied by some item.
      - Occupancy is always computed from the item's current state. Fold changes `F`; Compress changes `thickness`. Because those states are chosen outside the suitcase (Decision 8), occupancy is evaluated for the state committed with the placement.
      - `blockers(i)` = items `j ≠ i` whose footprint overlaps `i`'s footprint in XY **and** whose base layer is `≥ i.layer + i.thickness`.
      - **Vertical adjacency:** `i` and `j` touch vertically when their footprints overlap in XY and one's base layer equals the other's `layer + thickness`. **Lateral adjacency:** some occupied cell of `i` and some occupied cell of `j` are orthogonal XY neighbours at the same `z`.
      - Nested children occupy no board cells (Decision 8) and are not part of this occupancy computation.
      - This is a discrete layered-cell model. No arbitrary voxel physics, centre of mass, or partial support.
    - **Automatic layer resolution (clarification).** The player never chooses a layer in the validation slice. A 2D drop candidate resolves to the **lowest legal layer**.
      - For `L = 2`: if layer 0 satisfies bounds, mask and physical occupancy, use layer 0; otherwise evaluate layer 1, which is legal only if full support is satisfied. A thickness-2 item cannot start on layer 1.
      - If no layer is legal, the placement preview is invalid.
      - No layer toggle, layer button or explicit Z-axis control in the slice. Presentation may show the resolved candidate layer during preview.
13. **Rules and Objectives are separate concepts.** Rules describe what a legal layout is; objectives describe what a level asks for (Pack / Extract / Repack).

### Validation architecture

14. **Runtime and solver use one shared pure-C# evaluator.** No Unity dependency; the same code is called by gameplay, editor tools and headless tests. State is immutable, hashable and undo-friendly.
15. **Three validation layers; intermediate rule violations are allowed.**

| Layer | Enforced at every intermediate state? | Contents |
|---|---|---|
| Board Invariants | Yes, illegal moves are rejected | No physical volume overlap (Decision 12), in bounds, full support, staging capacity |
| Puzzle Rules | No, evaluated live and shown as indicators | Zone, Adjacency ±, Group, Access, Balance |
| Objective | At completion only | Profile-specific (see 16) |

A move that temporarily breaks a rule (e.g. moving headphones to staging splits the tech group) is legal; the indicator turns red but the move is not blocked.

16. **Completion contract per profile.** Zip It / completion fires automatically when, after a move:
    - **Pack:** all required items inside ∧ board invariants valid ∧ all rules satisfied ∧ staging empty.
    - **Repack:** all existing required items + all new items inside ∧ board invariants valid ∧ all rules satisfied ∧ staging empty.
    - **Extract:** extraction target is in its extraction destination (e.g. security tray) ∧ remaining suitcase satisfies invariants and rules ∧ staging empty. Moving the target to a staging slot is **not** completion.
    - **Active rule domain (clarification).** Puzzle rules are evaluated against the active item set: the items participating in the suitcase objective.
      - An item that has been moved to an extraction destination leaves the suitcase rule domain. Items on the board, nested inside board items, or in staging remain in the domain.
      - A rule that references an item outside the domain no longer has to be satisfied for that item, unless the rule explicitly declares a post-extraction scope. Tag-based rules (e.g. Group) evaluate over the in-domain members of the tag; item-pair rules (e.g. Adjacency) whose subject has left the domain are satisfied for that pair.
      - Extract completion still requires: target at its extraction destination ∧ remaining suitcase invariants valid ∧ remaining applicable rules satisfied ∧ staging empty.
      - Example: if Laptop took part in a Group or Adjacency rule, extracting Laptop must not make completion impossible solely because Laptop is no longer inside the suitcase.
      - This is generic evaluator/data-model behaviour. Rule data declares its scope; no item-specific or X-Ray-specific code paths.
    - **Extraction destination (clarification).** An extraction destination is a level-defined external objective sink.
      - Slice: one destination, capacity 1, no geometry or grid. It accepts the authored extraction target, does not consume staging capacity, and is outside the suitcase rule domain.
      - Moving an accessible target there is one move (Decision 18). After commit the target cannot be moved back by normal gameplay; Undo may restore the previous state.
      - Placing the target in staging is not extraction completion.
      - Data model stays generic (`id`, `capacity`, accepted item ids / tags / objective roles), but multi-destination gameplay is not part of the slice.
17. **Nest access follows the parent.**
    - A nested child is accessible only if its parent is accessible.
    - When the parent is accessible, the child can be taken out without removing the parent from the suitcase; the child goes to staging or the board.
    - Moving a parent moves its nested children with it as one move.
18. **Atomic move semantics (clarification).** Min-move metrics, telemetry, undo and the planning solver share one definition: **one committed relocation of one accessible item is one move.**
    - Each of these costs exactly one move: Source Tray → suitcase; suitcase → another legal suitcase placement; suitcase → staging; staging → suitcase; accessible nested child → staging; accessible nested child → board; accessible child → accessible compatible parent (nest); accessible target → extraction destination.
    - Previewing a placement is not a move. Invalid or uncommitted previews are not moves.
    - Rotating, or selecting a Fold/Compress state, before committing a placement is not a separate move; the rotation and the authored state are part of the committed relocation (Decision 8).
    - Undo reverses state but does not increment the move counter. The move counter is session-level data, not canonical puzzle state (Delta Δ-09), so it is not part of the state hash.
    - Moving a parent item carries all its nested children and remains one move.
    - Camera movement, selection, UI taps and inspection are never moves.
    - Pack min-move stays outside scoring. For Extract and Repack, solver `minMoves` remains an **authoring metric only** until playtest data justifies player-facing stars.

## Validation slice

| Lv | Profile | Teaches |
|---:|---|---|
| 1 | Pack | Fit (small, non-automatic choice) |
| 2 | Pack | Rotate |
| 3 | Pack | Zone |
| 4 | Pack | Fit + Zone, tighter |
| 5 | Pack | Fold |
| 6 | Pack | Adjacency |
| 7 | Pack | Layer 2 |
| 8 | Extract | Access + staging 2 |
| 9 | Pack | Access rule |
| 10 | Repack | 2 souvenirs + staging 2 |

**Questions the slice must answer:**
- Is Pack fun on its own?
- Does Layer genuinely deepen the puzzle?
- Does Extract produce a sequencing "aha"?
- Does Repack feel different from Pack?
- After Lv10, does the player want one more suitcase?

**Slice scope**
- Profiles: Pack, Extract, Repack
- Manipulation: move, rotate, undo
- Modifiers: Fold, Compress, Nest supported by the engine; 1–2 visible uses are enough
- Rules: Fit (invariant), Zone, Adjacency, Access. Group and Balance after the slice
- Feedback: live legality, invalid-placement preview, snap, Zip It ritual
- Meta: Passport only
- Mini-game: X-Ray prototype at most

**Out of slice:** stars, lives, move limits, more than 10 levels, postcard/map meta, other mini-games, new rule types.

## Solver / validator (minimum)

| Check | Pack (CSP) | Extract / Repack (state-space planning) |
|---|---|---|
| Solvable | Backtracking + propagation | BFS / IDA* with move bound |
| Trivial? | Solved by greedy placement | Solved by moving blockers to staging only, without in-suitcase re-placement |
| Solution count | Count up to 50 | — |
| Rule coupling | Remove each rule; if the solution set is unchanged, the rule is dead | Same |
| Min moves | — | Recorded as an authoring metric |

- **Min moves is an authoring metric only.** It is logged with player moves in telemetry, but not tied to stars until human play data shows how mathematical and intuitive optima differ.
- **Canonical state hashing** may merge items only when truly interchangeable. Equivalence key ≈ `itemDefinition + state + tags + objectiveRole + mutableProperties`. Two identical T-shirts where one is objective-required are not equivalent.
- Move types for search: place a Source Tray item into a legal cell; take an accessible item (to staging, a free legal cell, a compatible parent, or the extraction destination); place a staged item into a legal cell. Fold/Compress states are chosen as part of placements from the Source Tray or staging. Each search edge is exactly one move as defined in Decision 18, so solver `minMoves` and player move counts are directly comparable.

## Consequences

**Positive**
- ZipTrip's identity rests on temporal packing, which competitors lack and which fits the travel fantasy directly.
- Three modifiers map cleanly to three dimensions; no state duplicates another.
- Difficulty can scale through staging capacity, board elements and rule coupling without inflating the rule vocabulary.
- One evaluator prevents runtime/solver drift.

**Negative / costs**
- Extract/Repack validation is a planning problem; it is meaningfully more complex than a static placement solver.
- Two layers create a readability burden for the lower layer; ghost/X-ray presentation must be designed early.
- The slice cannot validate onboarding pacing or retention; that needs a later, longer build.

## Alternatives considered

- **Static rule-check puzzle (GDD v0.3):** rejected; collapses into trial and error and drops Extract/Repack.
- **Full 3D voxel suitcase:** rejected; unreadable top-down and costly for solver and UX.
- **Unlimited staging:** rejected; removes the decision from Extract and Repack.
- **Strict rule enforcement at every intermediate state:** rejected; makes valid rearrangement sequences impossible.

## Process

No further design discovery on core. New core decisions require a new ADR or an amendment to this one. ZT tickets implement this ADR together with Blueprint v1.1 + v1.2 Delta.
