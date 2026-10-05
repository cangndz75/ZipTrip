# ADR-0010: Gameplay Camera Pitch — 25° Orthographic View

- **Status:** Accepted
- **Date:** 2026-10-05
- **Deciders:** Can (CAM-01 human decision)
- **Supersedes:** only the camera-angle value in ADR-0002 §Numerical Parameters (`rotation (75, 0, 0)`)
- **Keeps:** every other ADR-0002 decision — fixed camera, orthographic projection, no orbit/pan/zoom, aspect policy,
  framing system, camera never part of gameplay legality

## Context

The ADR-0002 camera (orthographic, 75° pitch, 15° tilt from straight down) reads cleanly but shows little suitcase
depth or item volume. CAM-01 compared four presets on the shipped Golden Lv1 at 1080×2340 and 1080×1920:

| Preset | Projection | Pitch / tilt | Far-row cell (1080×2340) | Far ÷ near | Suitcase width | Result |
|---|---|---|---|---|---|---|
| A | orthographic | 75° / 15° | 137×132 px | 1.00 | 94% | previous production |
| **B** | **orthographic** | **65° / 25°** | **137×124 px** | **1.00** | **94%** | **accepted** |
| C | perspective, 30° FOV | 70° / 20° | 114×103 px | 0.89 | 78% | rejected |
| D | perspective, 30° FOV | 55° / 35° | 108×82 px | 0.84 | 76% | rejected |

All four kept picking, ghost/commit alignment and ZIP-02 exact. Perspective (C, D) shrank the usable suitcase and the
far row; B adds visible wall depth and item volume while keeping one cell scale across the board.

## Decision

The production gameplay camera is **orthographic, pitch 65° (25° tilt from straight top-down)**, framed by the
existing ADR-0002 framing rule (`PuzzleCameraFraming`). It stays a single fixed, authored camera with no runtime
camera controls, no perspective and no in-game selector. Readability of the suitcase, tray and gameplay remains
authoritative over depth.

## Consequences

- One constant changes: `PuzzleCameraFraming.Pitch = 65`. Picking, snapping and legality are unchanged; they are
  projection-independent (ray against the board plane; Domain decides legality).
- Backdrop props derive their screen offset from the pitch, so they follow automatically.
- The legacy `PuzzleHarness` and the Cabin/MG-1 readability review scenes keep their own 75° literal; they are not
  the shipped gameplay camera.
- Guarded by `GameplayCameraContractTests` (both aspects: picking on every anchor, ghost and commit alignment, equal
  near/far cell scale, tray/HUD clearance, ZIP-02 mid-close and final).

## Carry-forward (ART-01)

The ART-01 neighbour-overlap test (`Art01GoldenVisualTests.SolvedLv1Items_DoNotReadOverNeighbouringFootprints_FromGameplayCamera`)
casts rays from the camera position as if the gameplay camera were perspective. The production camera is
orthographic, so that measurement is incorrect and must be fixed (rays along the camera forward) when ART-01
resumes. Its 4.5% value is not an authoritative gameplay threshold.
