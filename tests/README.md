# Test Infrastructure

**Engine**: Unity 6 LTS (6000.3.17f1)  
**Test Framework**: Unity Test Framework (NUnit)  
**CI/CD**: `.github/workflows/tests.yml` (`game-ci/unity-test-runner@v4`)  
**Setup Date**: 2026-09-21  

---

## Directory Layout

```text
tests/
  ├── unit/               # Isolated unit tests (formulas, state machines, logic)
  ├── integration/        # Cross-system integration and lifecycle tests
  ├── EditMode/           # Unity EditMode tests (pure logic, no PlayMode scene overhead)
  ├── PlayMode/           # Unity PlayMode tests (Physics, NavMesh, Coroutines, Scene)
  ├── smoke/              # Critical path checklist for /smoke-check gate
  └── evidence/           # Visual/Feel, UI, and playtest sign-off records
```

---

## Running Tests

### Local Execution via Unity Editor
1. Mở Unity Editor (`6000.3.17f1`).
2. Chọn menu `Window` → `General` → `Test Runner`.
3. Chọn tab **EditMode** hoặc **PlayMode**.
4. Nhấn **Run All** để thực thi toàn bộ test suite.

### Headless CLI Execution
```bash
# Chạy EditMode tests headlessly
Unity.exe -batchmode -runTests -projectPath . -testPlatform EditMode -testResults test-results/editmode.xml -nographics

# Chạy PlayMode tests headlessly
Unity.exe -batchmode -runTests -projectPath . -testPlatform PlayMode -testResults test-results/playmode.xml -nographics
```

---

## Test Naming Conventions

- **Files**: `[System]_[Feature]_Tests.cs` (PascalCase, e.g., `SuspicionGradeSystemTests.cs`, `GuardFSM_Transition_Tests.cs`)
- **Functions**: `Test_[Scenario]_[ExpectedResult]` (e.g., `Test_Uncrouch_BlockedByCeiling_StaysCrouched()`)
- **Determinism**: Toàn bộ unit test phải tất định (deterministic), không dùng `Random` ngẫu nhiên không seed, không phụ thuộc vào `Time.deltaTime` thời gian thực mà sử dụng `VirtualTimestamp`.

---

## Story Type → Test Evidence Matrix

| Story Type | Required Evidence | Storage Location | Gate Level |
| :--- | :--- | :--- | :--- |
| **Logic** (formulas, AI, FSM, math) | Automated unit test — must pass | `tests/unit/[system]/` or `tests/EditMode/` | **BLOCKING** |
| **Integration** (multi-system contracts) | Integration test OR documented playtest | `tests/integration/[system]/` or `tests/PlayMode/` | **BLOCKING** |
| **Visual/Feel** (animation, VFX, feel) | Screenshot + lead sign-off | `tests/evidence/` or `production/qa/evidence/` | ADVISORY |
| **UI** (menus, HUD, screens) | Interaction test OR manual walkthrough | `tests/evidence/` or `production/qa/evidence/` | ADVISORY |
| **Config/Data** (balance tuning) | Smoke check pass | `production/qa/smoke-[date].md` | ADVISORY |

---

## Continuous Integration (CI)

Tất cả các bài kiểm tra được chạy tự động trên GitHub Actions mỗi khi có commit đẩy lên nhánh `main` hoặc tạo Pull Request:
- Workflow: `.github/workflows/tests.yml`
- Unity License: Yêu cầu thiết lập secret `UNITY_LICENSE` trong repository settings.
- Rule: **No merge if tests fail** — Kiểm thử là chốt chặn bắt buộc trong CI.
