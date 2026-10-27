# Story 002: Release Build Automation & Platform Packaging

> **Epic**: Release Packaging & Performance Certification (`production/epics/release-packaging/EPIC.md`)  
> **Story ID**: `BUILD-01`  
> **Status**: Complete  
> **Layer**: DevOps / Pipeline Tools  
> **Type**: Tooling / Automation  
> **Estimate**: 3h (0.4d)  
> **Manifest Version**: 2026-10-27  
> **Owner**: `devops-engineer`  

---

## Context

**GDD**: `PLAN.md` (Tuần 8 — Build & Platform Targets), `technical-preferences.md` (PC & WebGL Targets)  
**Governing ADRs**: `ADR-0008` (Tooling Automation)  
**Engine**: Unity 6 LTS (6000.3.17f1) | **Risk**: LOW  

**Build Pipeline Contracts**:
1. **Target Platforms**:
   - Windows Standalone x64 (`BuildTarget.StandaloneWindows64`).
   - WebGL Demo (`BuildTarget.WebGL`).
2. **Build Configurations**:
   - Release profile: `DevelopmentBuild = false`, Code Stripping enabled, Brotli / Gzip compression for WebGL.
   - CorePlayground scene inclusion (`Assets/Scenes/CorePlayground.unity`).
3. **Artifact Output Paths**:
   - `Builds/Windows/WhisperWard.exe`
   - `Builds/WebGL/`

---

## Acceptance Criteria

- [x] **AC-BUILD-01 — Build Pipeline Target Resolution**: Builder determines valid platform target parameters for Windows and WebGL.
- [x] **AC-BUILD-02 — Scene Asset Validation**: Ensures scene inclusion array contains `Assets/Scenes/CorePlayground.unity` as the primary startup scene.
- [x] **AC-BUILD-03 — WebGL Compression & Canvas Scaling**: Configures proper canvas scaling and output directory validation.

---

## QA Test Cases

- `test_release_builder_validates_scene_inclusion_list`
- `test_release_builder_configures_windows_standalone_target`
- `test_release_builder_configures_webgl_target_settings`

---

## Test Evidence
- Unit Tests: `tests/unit/tools/release_build_automation_test.cs`
