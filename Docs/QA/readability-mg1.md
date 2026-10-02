# ZA-005 / MG-1 — human readability test

**Status:** WAIVED / OVERRIDDEN BY HUMAN DECISION (2026-10-02). This is not a PASS.

## Human override record

- The test instrument is valid and frozen.
- Only P01 was observed: **1/4 within tolerance**.
- The item-level P01 answers were not supplied for this record, so the participant/item rows below remain blank rather than inventing data.
- P02–P05 observations were not collected.
- The remaining readability risk is consciously accepted for now.
- ZT-016 gameplay integration re-check was human accepted on 2026-10-02: mesh, hidden-footprint and ghost agreement passed; Sneaker Pair L-footprint and Laptop 3×4 gameplay readability were accepted.
- This ZT-016 result does not convert MG-1 to PASS. The waiver and accepted readability risk remain in force.

## Canonical contract

- Five participants, P01–P05; four separate shapes/states each: **20 observations**.
- Tolerance: at most **1 differing drawn cell**.
- Pass gate: **>=16/20 observations within tolerance** and no systematic error.
- Sweater Open and Sweater Folded are distinct observations.
- Human scoring clarification: compare the drawn occupied-cell sets after shifting the answer's occupied bounding box to top-left `(0,0)` without rotating or reflecting it. Cell difference is the symmetric difference: a missing or extra occupied cell counts one; moving an occupied cell counts two.
- Human systematic-error clarification: the same wrong cell for the same item recurring in **>=3/5 participants** is a systematic error. The same recurring error in **2/5 participants** is a review flag only and does not independently fail the gate. Record the cell and participant IDs before deciding the final gate.
- This sheet is facilitator-only. Never show canonical footprints, another participant's answers, or scoring to the current participant.

## Presentation flow

1. Use the portrait `MG1ReadabilityTest` app on the test phone. The canonical 75° camera, Cabin and accepted prefab scales are fixed. The grid/debug overlays start off.
2. Start the app afresh for each participant. It randomizes the four-item order and shows one item at a time. Record the observed order in the sheet. The random seed and order are also logged for audit; neither is shown on screen.
3. Give every participant the same prompt: **“Bu nesnenin valizde kaç ve hangi kareleri kaplayacağını düşünüyorsunuz? Boş kareli kağıtta kaplayacağı kareleri işaretleyin.”** Do not name the footprint, explain the grid answer, correct a response, or show an earlier response.
4. Record the unedited answer before any normalization or feedback. The facilitator holds the screen for 1.5 seconds to advance after the answer; a short tap does not advance. After item four the app shows no item. Restart for the next participant.
5. Only after each response, transcribe its occupied cells as `X` and empty cells as `.`, trim empty top/left rows and columns, and compare with the facilitator answer key below. Keep the response orientation as drawn. Record symmetric difference, PASS/FAIL and notes.
6. After all 20 observations, check the repeated wrong-cell pattern by item. If fewer than 16 observations pass or a systematic error exists, list the assets requiring revision. Do not extend the test indefinitely to force a pass.

## Blank result sheet

Use a paper/photo identifier or verbatim row diagram in **Raw answer**. Leave scoring cells blank until a response exists. `Order` is the observed position 1–4 for that participant.

| Participant | Item | Order | Raw answer | Normalized footprint | Cell difference | PASS/FAIL | Notes |
| --- | --- | --- | --- | --- | --- | --- | --- |
| P01 | Laptop |  |  |  |  |  |  |
| P01 | Sweater Open |  |  |  |  |  |  |
| P01 | Sweater Folded |  |  |  |  |  |  |
| P01 | Sneaker Pair |  |  |  |  |  |  |
| P02 | Laptop |  |  |  |  |  |  |
| P02 | Sweater Open |  |  |  |  |  |  |
| P02 | Sweater Folded |  |  |  |  |  |  |
| P02 | Sneaker Pair |  |  |  |  |  |  |
| P03 | Laptop |  |  |  |  |  |  |
| P03 | Sweater Open |  |  |  |  |  |  |
| P03 | Sweater Folded |  |  |  |  |  |  |
| P03 | Sneaker Pair |  |  |  |  |  |  |
| P04 | Laptop |  |  |  |  |  |  |
| P04 | Sweater Open |  |  |  |  |  |  |
| P04 | Sweater Folded |  |  |  |  |  |  |
| P04 | Sneaker Pair |  |  |  |  |  |  |
| P05 | Laptop |  |  |  |  |  |  |
| P05 | Sweater Open |  |  |  |  |  |  |
| P05 | Sweater Folded |  |  |  |  |  |  |
| P05 | Sneaker Pair |  |  |  |  |  |  |

## Facilitator-only answer key

Read this section **after** the participant has answered the item. Shapes follow the canonical authored orientation; `X` is occupied.

| Item | Canonical rows | Area | Tolerance |
| --- | --- | ---: | --- |
| Laptop | `XXX / XXX / XXX / XXX` | 12 | ≤1 cell difference |
| Sweater Open | `XXX / XXX / XXX` | 9 | ≤1 cell difference |
| Sweater Folded | `XX / XX / XX / XX` | 8 | ≤1 cell difference |
| Sneaker Pair | `X. / X. / XX` | 4 | ≤1 cell difference |

## Final scoring

- Passing observations: 1 / 4 collected; 16 observations not collected
- Repeated wrong cell by item and participant IDs: ___
- Systematic error present: not assessable from one participant
- MG-1 gate: WAIVED / OVERRIDDEN BY HUMAN DECISION — not PASS
- ZT-016 re-check: human accepted; mesh/hidden-footprint/ghost agreement PASS. MG-1 readability risk remains accepted under this waiver.
- Device / date / facilitator: human observation record / 2026-10-02 / human-owned
