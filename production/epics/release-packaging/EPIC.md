# Epic: Release Packaging & Performance Certification

> **Layer**: DevOps / Performance / Portfolio  
> **GDD Reference**: `PLAN.md` (Tuần 8 — Bug Fix, Build & Portfolio Packaging)  
> **Governing ADRs**: `ADR-0002` (Performance Budgets), `technical-preferences.md`  
> **Status**: Complete  
> **Stories**: 3 stories (`PERF-01` [Complete], `BUILD-01` [Complete], `DOCS-PORTFOLIO-01` [Complete])  

## Overview

The Release Packaging & Performance Certification epic prepares **Whisper Ward** for deployment and portfolio showcase. It establishes automated performance budget verification (60 FPS, draw call thresholds, zero GC hot paths), automated build scripts for Windows Standalone and WebGL, and an exhaustive Technical Architecture Portfolio Showcase document highlighting custom AI systems.

## Stories

| # | Story | Type | Status | Owner |
|---|-------|------|--------|-------|
| `PERF-01` | Performance Budget & Frame Time Validator | Performance / Verification | Complete | performance-analyst |
| `BUILD-01` | Release Build Automation & Platform Packaging | DevOps / Automation | Complete | devops-engineer |
| `DOCS-PORTFOLIO-01` | Technical Architecture Showcase & Release Portfolio Guide | Docs / Portfolio | Complete | technical-director |

## Definition of Done

This epic is complete when:
- Performance budget validator confirms 60 FPS ($16.6\text{ ms}$ budget), draw call ceiling ($\le 1000$ PC, $\le 300$ WebGL), and zero GC hot path allocations.
- Build automation script validates scene references, asset bundles, and export pipeline settings.
- Portfolio showcase document provides comprehensive technical breakdown of AI perception, FSM, sound hierarchy, mathematical formulas, and instructions to run.
