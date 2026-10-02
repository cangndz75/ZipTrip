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
7. **Fit is not a rule; it is a board invariant.**

### Modifiers

8. **Item modifiers are Fold, Compress and Nest.** Each manipulates a different dimension:

| Modifier | Dimension | Effect |
|---|---|---|
| Fold | X/Y | Changes footprint shape. Area stays the same or close; never a free size reduction. |
| Compress | Z | Changes thickness (e.g. 2 layers → 1). May disable other options (e.g. nesting). |
| Nest | Containment | Small item goes inside another item's internal capacity; occupies no grid cells. |

Roll, Vacuum, Protected and Travel Bottle are not separate states.

### Ritual and scope

9. **Zip It is a completion ritual, not a mini-game.** Final legal placement → short pause → rule indicators complete → straps snap → lid closes → zip → victory burst. X-Ray Control is the only mini-game research candidate; all others are backlog.
10. **First milestone is a 10-level design validation slice** (see Slice). Not shipping onboarding; no difficulty-curve or retention conclusions are drawn from it.

### Board model

11. **Staging capacity is a core board parameter, not a rule.** Without a limit, Extract becomes "dump and retrieve" and Repack becomes "Pack from scratch."
    - Slice: `stagingCapacity = 2`. Later levels may use 1 / 2 / 3; capacity is a difficulty knob in its own right (3 = easy, 2 = normal, 1 = hard on the same board).
    - Each slot holds exactly one item (a parent with nested children counts as one item and keeps its children).
    - Slots carry no geometry or rotation, accept no nesting, and never block each other. Bed/table is a visual metaphor only.
12. **2.5D board, `L = 2`.**
    - Board is `W × H × L`; zones are per cell. Pockets are separate compartments with their own small grids (typically `L = 1`).
    - Item thickness is per state (1 or 2 layers).
    - **Full support:** every occupied cell of an upper-layer item's footprint must be supported by some lower-layer item. Multiple lower items may share support. No physics, centre of mass or partial-support thresholds.
    - **Blocking:** `blockers(i)` = items on a higher layer whose footprint overlaps `i`. `accessible(i) ⇔ blockers(i) = ∅`.
    - **Adjacency counts vertical neighbours** as well as lateral ones (a snow globe on a sweater is protected; shoes on clothes are touching).
    - Hidden lower-layer content must be readable while the upper layer is occupied (ghost / X-ray view).
13. **Rules and Objectives are separate concepts.** Rules describe what a legal layout is; objectives describe what a level asks for (Pack / Extract / Repack).

### Validation architecture

14. **Runtime and solver use one shared pure-C# evaluator.** No Unity dependency; the same code is called by gameplay, editor tools and headless tests. State is immutable, hashable and undo-friendly.
15. **Three validation layers; intermediate rule violations are allowed.**

| Layer | Enforced at every intermediate state? | Contents |
|---|---|---|
| Board Invariants | Yes, illegal moves are rejected | No overlap, in bounds, full support, staging capacity |
| Puzzle Rules | No, evaluated live and shown as indicators | Zone, Adjacency ±, Group, Access, Balance |
| Objective | At completion only | Profile-specific (see 16) |

A move that temporarily breaks a rule (e.g. moving headphones to staging splits the tech group) is legal; the indicator turns red but the move is not blocked.

16. **Completion contract per profile.** Zip It / completion fires automatically when, after a move:
    - **Pack:** all required items inside ∧ board invariants valid ∧ all rules satisfied ∧ staging empty.
    - **Repack:** all existing required items + all new items inside ∧ board invariants valid ∧ all rules satisfied ∧ staging empty.
    - **Extract:** extraction target is in its extraction destination (e.g. security tray) ∧ remaining suitcase satisfies invariants and rules ∧ staging empty. Moving the target to a staging slot is **not** completion.
17. **Nest access follows the parent.**
    - A nested child is accessible only if its parent is accessible.
    - When the parent is accessible, the child can be taken out without removing the parent from the suitcase; the child goes to staging or the board.
    - Moving a parent moves its nested children with it as one move.

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
- Move types for search: take an accessible item (to staging or a free legal cell); place a staged item into a legal cell.

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
