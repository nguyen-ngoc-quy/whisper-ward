# QA Evidence: SCENE-06 — URP Moonlit Stealth Post-Processing & Distinct Materials

> **Story**: `production/epics/level-playground/story-006-materials-post-processing.md`  
> **Date**: 2026-09-22  
> **Engine**: Unity 6 LTS (6000.3.17f1) | Universal Render Pipeline (`com.unity.render-pipelines.universal` 17.3.0)  
> **Reviewer**: `technical-artist` / `gameplay-programmer` / `qa-lead`  
> **Result**: VERIFIED & APPROVED ✅  

---

## 1. Distinct High-Contrast Material Configuration

| Material Asset | Target Surface | Hex Code | Color Vector (RGB) | Visual Contrast Role | Status |
|---|---|---|---|---|:---:|
| `Mat_Floor_Slate.mat` | `Arena_Floor_20x20` | `#1C222B` | `(0.110, 0.133, 0.169)` | Low-reflectance dark ground plane | **PASS** ✅ |
| `Mat_Wall_Concrete.mat` | Perimeter & Corridors | `#38404B` | `(0.220, 0.251, 0.294)` | Solid structural boundaries | **PASS** ✅ |
| `Mat_Player_Teal.mat` | `Player_Capsule` visual | `#2AC3A2` | `(0.165, 0.765, 0.635)` | Player avatar identifiable at a glance | **PASS** ✅ |
| `Mat_Guard_HostileRed.mat` | `Guard_PatrolNPC` visual | `#D9383A` | `(0.851, 0.220, 0.227)` | Immediate threat visual identification | **PASS** ✅ |

---

## 2. Post-Processing & Mood Profile Setup

| Component / Override | Configured Parameters | Visual Objective | Status |
|---|---|---|:---:|
| **Global Volume** | `isGlobal = true`, `weight = 1.0` | Arena-wide atmospheric coverage | **PASS** ✅ |
| **Tonemapping** | `TonemappingMode.ACES` | Cinematic dynamic range compression | **PASS** ✅ |
| **Vignette** | `intensity = 0.25`, smooth perimeter falloff | Subtle claustrophobic peripheral darkening | **PASS** ✅ |
| **Moonlit Directional Light** | `#A0C4E2`, intensity $0.85$, soft shadows | Deep cold moonlit highlights | **PASS** ✅ |

---

## 3. Acceptance Criteria Verification Matrix

| Acceptance Criterion | Verification Method | Status |
|---|---|:---:|
| **AC-SCENE-18 — High Contrast Materials** | Distinct URP Lit materials for Floor, Walls, Player, and Guard | **PASS** ✅ |
| **AC-SCENE-19 — Moonlit Post-Processing Volume** | Global Volume with ACES tonemapping & Vignette ($0.25$) | **PASS** ✅ |

---

## 4. Sign-Off

| Role | Name / Agent | Verdict | Signature Date |
|---|---|:---:|:---:|
| **Technical Artist** | `technical-artist` | `[x] Approved` | 2026-09-22 |
| **Gameplay Programmer** | `gameplay-programmer` | `[x] Approved` | 2026-09-22 |
| **QA Lead** | `qa-lead` | `[x] Approved` | 2026-09-22 |
