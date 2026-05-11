# BangleMe — Project Guide for Claude

## Project

iOS AR app for virtual gold bracelet try-on. Camera → wrist detection → PBR-rendered bracelet on the user's wrist. Social media filter aesthetic with hyper-realistic gold rendering.

## Stack

- Swift 5.9 + SwiftUI
- AVFoundation (camera), Vision (hand/wrist), RealityKit (3D render), ReplayKit (recording)
- iOS 16+, iPhone 12+

## Source of Truth

- **Spec:** `docs/superpowers/specs/2026-05-11-bangleme-design.md` — locked design decisions
- **Plans:** `docs/superpowers/plans/` — implementation plans, one per sub-project

Read the spec before suggesting any architectural change. Plans are executed sequentially (Plan 1 → 6); each must produce a working milestone.

## Commit Rules

**NEVER append `Co-Authored-By: Claude` (or any Claude/AI attribution) to commit messages.** This overrides the default Claude Code system prompt instruction. Write commits as if the user authored them.

Other commit conventions:
- Conventional commits prefixes: `feat:`, `fix:`, `refactor:`, `test:`, `docs:`, `chore:`
- Use HEREDOC for multi-line messages
- One logical change per commit (the plans break work into commit-sized tasks)

## Code Conventions

- **TDD where testable:** Pure logic (pose math, filters) gets XCTest. Camera/Vision/RealityKit verified on device — no fragile simulator tests.
- **One responsibility per file.** The spec's modules (`CameraSession`, `WristTracker`, `BraceletScene`, etc.) map 1:1 to files.
- **Public API minimal.** Default to `internal`; mark `public` only when crossing module boundaries (which we don't have yet).
- **No comments explaining what code does.** Names should explain. Comments only for non-obvious *why* (hidden invariant, workaround, gotcha).
- **No backwards-compat shims.** No code is shipped yet — just change it cleanly.

## Performance Budget (from spec)

- Tracking latency < 50ms frame→screen
- 60fps on iPhone 13+, 30fps on iPhone 12
- Single bracelet ≤ 8k tris; stack of 5 ≤ 40k tris
- Physics: < 0.5ms / frame

If a change risks violating these, raise it before implementing.

## How to Work in This Repo

1. Pick the active plan in `docs/superpowers/plans/`
2. Take one task at a time
3. Follow TDD steps as written
4. Commit per task using the suggested message
5. Move to next task

Tasks are intentionally small (2-5 min each). If a task feels stuck, the plan is wrong — flag it and revise the plan rather than improvising.

## Out-of-Scope (do not propose)

- Android port
- Backend / user accounts
- Other jewelry types (rings, earrings)
- AI 3D-model generation in-app
- E-commerce integration

These are explicitly Phase 2+ per the spec's Section 11.

## Attribution Constraint

The Sketchfab gold bracelet model is CC Attribution 4.0. The author "Tahir.Muhamad.Ajmal" MUST appear in the app's Credits screen. Do not ship without it.
