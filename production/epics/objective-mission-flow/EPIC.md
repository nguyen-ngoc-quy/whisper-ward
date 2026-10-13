# Epic: Objective Mission Flow & Stealth Mastery Loop

> **Epic ID**: `EPIC-OBJ-FLOW`  
> **Status**: Active (Sprint 05)  
> **Target Milestone**: Playable Vertical Slice  
> **Owner**: `lead-programmer` / `gameplay-programmer`  
> **Governing GDDs**: `design/gdd/game-concept.md`, `design/gdd/systems-index.md` (Systems #7, #8, #9)  

---

## 1. Executive Summary

Epic này thiết lập vòng lặp nhiệm vụ chơi được hoàn chỉnh (Playable Objective Loop) cho **Whisper Ward**:
- Thu thập vật phẩm nhiệm vụ (`Keycard`) đặt trong khu vực tuần tra của lính gác.
- Tương tác mở khóa cổng bảo an (`Locked Security Gate`).
- Tiến vào ranh giới thoát hiểm (`Extraction Zone`) để hoàn thành màn chơi.
- Điều phối trạng thái vòng đời nhiệm vụ tổng thể thông qua `MissionLifecycleController`.

---

## 2. Stories in Epic

| Story ID | Title | Priority | Status | Est. Days |
|---|---|---|---|---|
| `OBJ-01` | Objective Keycard Pickup & Inventory State | Must Have | Ready | 0.5d |
| `OBJ-02` | Locked Security Gate & Extraction Zone | Must Have | Ready | 0.5d |
| `FLOW-01` | Mission Game Loop & Lifecycle Controller | Should Have | Backlog | 0.4d |

---

## 3. Architecture & Dependency Flow

```text
[Player] 
   │
   ├─► [ObjectiveKeycardPickup] (Proximity Interaction)
   │        │
   │        └─► HasKeycard = true ──► [LockedSecurityGate] (Unlock & Open)
   │                                           │
   │                                           └─► [ExtractionZone] (Boundary Trigger)
   │                                                      │
   └──────────────────────────────────────────────────────┴─► [MissionLifecycleController]
                                                                     │
                                                                     └─► [StealthGradeOperator]
                                                                               │
                                                                               └─► [EndOfMissionScorecardHUD]
```
