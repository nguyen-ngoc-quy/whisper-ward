# Story 003: Technical Architecture Showcase & Release Portfolio Guide

> **Epic**: Release Packaging & Performance Certification (`production/epics/release-packaging/EPIC.md`)  
> **Story ID**: `DOCS-PORTFOLIO-01`  
> **Status**: Complete  
> **Layer**: Technical Architecture / Portfolio Presentation  
> **Type**: Documentation / Showcase  
> **Estimate**: 3h (0.4d)  
> **Manifest Version**: 2026-10-27  
> **Owner**: `technical-director`  

---

## Context

**GDD**: `PLAN.md` (Tuần 8 — Portfolio Packaging), `CLAUDE.md`  
**Governing ADRs**: All ADRs (`ADR-0001` through `ADR-0008`)  
**Engine**: Unity 6 LTS (6000.3.17f1) | **Risk**: LOW  

**Showcase Content**:
1. **Portfolio Identity & Value Proposition**:
   - Highlighting custom-written AI systems (Perception, Suspicion Accumulator, FSM, Coordinated Alert propagation) leveraging AI academic background.
2. **Architecture Breakdown**:
   - Clean architecture diagram (Core, AI, Player, Audio, UI, Environment).
   - Zero-allocation hot paths and deterministic NUnit testing suite (281+ tests).
3. **Mathematical Models Codified**:
   - Suspicion formula ($dA/dt = \min(k/d, \text{maxRate})$).
   - Distance/Stride footstep integration.
   - Stealth Grade Operator grading scale.
4. **How to Run & Play**:
   - Unity 6 Editor instructions, controls, and game loop walkthrough.

---

## Acceptance Criteria

- [x] **AC-PORT-01 — Comprehensive Technical Readme**: Root `README.md` updated with architecture overview, control schemes, and portfolio highlights.
- [x] **AC-PORT-02 — Portfolio Showcase Document**: `docs/portfolio-showcase.md` authored detailing custom AI design, mathematical formulas, and testing metrics.
- [x] **AC-PORT-03 — Playable Playground Guide**: Clear instructions for running `CorePlayground.unity` and building the project.

---

## QA Test Cases

- Documentation verification: verify complete technical depth, formula accuracy, and file path citations.

---

## Test Evidence
- Canonical Document: `docs/portfolio-showcase.md` and `README.md`
