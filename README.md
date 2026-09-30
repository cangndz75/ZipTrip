# ZipTrip

ZipTrip is a mobile-first 3D travel packing puzzle game built with Unity.

## Project Status

Current phase: Phase A — Vertical Slice

Gameplay development has not started yet.
The repository is currently being bootstrapped and validated before core implementation.

## Engine

- Unity 6.3 LTS
- Editor version: 6000.3.25f1
- Universal Render Pipeline (URP)
- Target platforms: Android and iOS
- Primary development platform: Windows

## Repository Structure

- `Assets/` — Unity project assets and source code
- `Packages/` — Unity package configuration
- `ProjectSettings/` — Unity project settings
- `Docs/Product/` — canonical product documents
- `Docs/ADR/` — architecture decision records
- `Docs/Plan/` — execution plans
- `Docs/Tickets/` — implementation tickets
- `Docs/QA/` — QA evidence and gate reports
- `AGENTS.md` — mandatory instructions for AI coding agents

## Source of Truth

Product and technical decisions are governed by:

1. ZipTrip Blueprint v1.1
2. ZipTrip Blueprint v1.2 Delta

Execution plans and tickets implement those documents and do not override them.

## Opening the Project

1. Install Unity 6.3 LTS `6000.3.25f1`.
2. Install Android Build Support, SDK/NDK and OpenJDK through Unity Hub.
3. Clone the repository.
4. Run:

   ```bash
   git lfs pull