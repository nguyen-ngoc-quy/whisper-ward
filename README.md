# Whisper Ward 👁️‍🗨️

> **A 3D Tactical Stealth Experience with Handcrafted AI Architecture in Unity 6 LTS**

[![Unity 6 LTS](https://img.shields.io/badge/Unity-6000.3.17f1%20LTS-black?logo=unity)](https://unity.com/)
[![Render Pipeline](https://img.shields.io/badge/Pipeline-URP-informational)](https://unity.com/srp/Universal-Render-Pipeline)
[![Automated Tests](https://img.shields.io/badge/Tests-295%2B%20Passing-success)](tests/)
[![Architecture](https://img.shields.io/badge/GC%20Allocations-Zero%20on%20Hot%20Paths-blue)](#performance--engineering-standards)
[![License](https://img.shields.io/badge/License-MIT-lightgrey)](LICENSE)

---

## 🎯 Overview & Portfolio Showcase

**Whisper Ward** is a third-person stealth action game engineered to showcase advanced **Game AI architecture, mathematical simulation, and systems engineering** built entirely from the ground up in C# without third-party behavior tree or sensor plugins.

Taking inspiration from classic tactical stealth titles (*Metal Gear Solid*, *Thief*, *Splinter Cell*), Whisper Ward puts the player in an infiltrated security compound where intelligent AI guards coordinate, investigate subtle acoustic disturbances, broadcast radio alerts, and pursue evasive targets.

### Key Technical Highlights
- 🧠 **Handcrafted AI Architecture**: Custom Raycast Perception Sensor ($90^\circ, 12\text{ m}$), Distance-Inverse Suspicion Accumulator, 3-State FSM (`Patrol` $\leftrightarrow$ `Investigate` $\leftrightarrow$ `Chase`), and Coordinated Swarm Radio Alerting.
- 📐 **Rigorous Mathematical Models**: Explicit differential equations for suspicion accumulation ($\frac{dA}{dt} = \min(1.5/d, 0.60)$), exponential residual wariness decay, and planar distance stride-length integration.
- 🔊 **Zero-Allocation 4-Bus Audio Engine (`ADR-0003`)**: Hierarchical mixing (`Master`, `SFX_Diegetic`, `Feedback_NonDiegetic`, `Ambience`), pre-allocated 16-channel voice pool, priority voice stealing, and dynamic ambient ducking ($-6\text{ dB}$ on active Chase).
- 🏆 **Stealth Grade Operator**: Strict ranking formula awarding Rank S exclusively for zero-detection ghost runs while distinguishing between spotted chases and distraction tool usage.
- 🛡️ **295+ Automated NUnit Tests**: 100% pass rate across 37 test suites verifying physics collisions, AI state machines, math formulas, audio pooling, and UI transitions.
- ⚡ **Zero GC Allocations**: All hot paths (movement ticks, perception raycasts, audio voice requests, UI updates) generate zero managed heap allocations.

---

## 🎮 Gameplay & Core Loop

```
[Observe Routes] ──> [Sneak / Crouch] ──> [Distract with Burst Tool]
         │                                         │
         ▼                                         ▼
[Acquire Keycard] ──> [Unlock Security Gate] ──> [Extract to Safety]
                                                   │
                                                   ▼
                                         [Grade Operator: S/A/B/C/F]
```

### Controls

| Action | Keyboard / Mouse | Gamepad | Description |
|---|---|---|---|
| **Move** | `W` `A` `S` `D` | Left Stick | Camera-relative character displacement |
| **Look / Orbit** | Mouse | Right Stick | Smooth third-person orbital camera |
| **Crouch / Sneak** | `C` / `Left Ctrl` | `B` / `Circle` | Silent movement ($1.4\text{ m}$ stride, zero acoustic noise) |
| **Sprint** | `Left Shift` | Left Stick Click | Fast sprint ($2.6\text{ m}$ stride, audible up to $8.0\text{ m}$) |
| **Throw Burst Tool** | `Right Click` / `F` | Right Trigger | Ballistic noise-maker distraction projectile ($10.0\text{ m}$ pulse) |
| **Interact / Pickup**| `E` | `X` / `Square` | Collect Keycard, open Security Gate |
| **Pause / Resume** | `Escape` | `Start` | Toggles in-game pause menu and freezes simulation |

---

## 🧠 AI Systems Architecture

```
                    ┌────────────────────────────┐
                    │   Vision Cone Sensor       │ ◄── Linecast (Layer 20 Solid)
                    │   (90.0° FOV, 12.0m, 5 Hz) │ ◄── Crouch/Stand Target Height
                    └─────────────┬──────────────┘
                                  │
                                  ▼
                    ┌────────────────────────────┐
                    │   Suspicion Accumulator    │ ◄── dA/dt = min(1.5 / dist, 0.60)
                    │   & Residual Wariness (R)  │ ◄── Dynamic Threshold T_entry(R)
                    └─────────────┬──────────────┘
                                  │
              ┌───────────────────┴───────────────────┐
              ▼                                       ▼
┌───────────────────────────┐           ┌───────────────────────────┐
│  State: Investigate       │           │  State: Chase             │
│  - Speed: 5.0 m/s         │           │  - Speed: 7.5 m/s         │
│  - 4.0s Dwell Scan ±45°   │           │  - Radio Alert Broadcast  │
│  - Residual Wariness gain │           │  - Catch: 5.5m in 1.0s    │
└───────────────────────────┘           └───────────────────────────┘
```

1. **Vision Cone Sensor (`VisionConeSensor.cs`)**:
   - Evaluates horizontal field-of-view ($90.0^\circ$, half-angle $45.0^\circ$) at 5 Hz cadence.
   - Non-allocating linecasts on Layer 20 (Solid World) ensure guards cannot see through walls or pillars.
   - Targets evaluate at $1.35\text{ m}$ when standing and $0.80\text{ m}$ when crouching.
2. **Suspicion Accumulator (`SuspicionAccumulator.cs`)**:
   - Distance-inverse charge accumulation grants players an intuitive reaction window ($0.5\text{--}0.8\text{ s}$) to duck into cover before Investigate triggers.
   - Broken line-of-sight triggers an exponential decay curve with a $16.0\text{ s}$ time constant.
3. **Guard FSM Controller (`GuardFSMRuntimeController.cs`)**:
   - Manages state handoffs: `Patrol` ($2.3\text{ m/s}$) $\to$ `Investigate` ($5.0\text{ m/s}$) $\to$ `Chase` ($7.5\text{ m/s}$) $\to$ `Captured`.
   - At last known position, guards conduct a rhythmic $4.0\text{ s}$ sinusoidal dwell scan ($\pm 45^\circ$) before giving up and returning to their patrol route.
4. **Coordinated Radio Alert (`GuardRadioAlertTransceiver.cs`)**:
   - Guards escalate to Chase and transmit an alert broadcast ($25.0\text{ m}$ radius).
   - Nearby guards receive the coordinate and immediately converge on the target area in synchronized multi-guard search formations.

---

## 🛠️ Project Structure

```text
Whisper Ward/
├── Assets/
│   ├── Editor/                      # Automated build pipelines & scene builder
│   │   ├── CorePlaygroundBuilder.cs # Programmatic 3D scene & prefab generator
│   │   └── ReleaseBuildAutomation.cs# Windows & WebGL release packaging tools
│   ├── Materials/                   # Slate, concrete, moonlit materials
│   └── Scenes/
│       └── CorePlayground.unity     # Interactive 3D test arena & playable level
├── src/
│   ├── AI/                          # Core AI Intelligence
│   │   ├── Alert/                   # Radio bark transceivers & multi-guard coordinator
│   │   ├── FSM/                     # Finite State Machine runtime controllers
│   │   ├── Navigation/              # NavMesh pathfinding queries & patrol drivers
│   │   └── Perception/              # Vision sensors, hearing, and suspicion accumulators
│   ├── Core/
│   │   ├── Audio/                   # AudioManager, footstep audio, guard audio controllers
│   │   ├── Camera/                  # Orbital camera rig & hide-spot triggers
│   │   ├── Contracts/               # System interfaces (IAudioService, IPlayerController)
│   │   ├── Environment/             # LockedSecurityGate, ExtractionZone, HideSpot
│   │   ├── Grade/                   # StealthGradeOperator scoring engine
│   │   ├── Objectives/              # Keycard pickups & MissionLifecycleController
│   │   └── Player/                  # Kinematic player controller & noise makers
│   ├── Foundation/                  # Physics query service & input buffering
│   └── UI/                          # HUDs, Scorecards, Overhead markers & Pause Menu
├── tests/                           # 295+ Automated NUnit Test Suites
│   ├── unit/                        # Isolated unit tests (AI, Audio, Grade, UI, Physics)
│   └── integration/                 # Multi-system end-to-end integration tests
└── docs/                            # Deep technical architecture & specifications
    ├── portfolio-showcase.md        # Technical portfolio architecture whitepaper
    └── engine-reference/            # Version-pinned Unity 6 LTS reference docs
```

---

## 🚀 Quickstart & How to Play

### Requirements
- **Unity 6 LTS** (`6000.3.17f1` or later recommended)
- **Universal Render Pipeline (URP)** package installed

### Opening in Unity Editor
1. Clone the repository:
   ```bash
   git clone https://github.com/nguyenngocquy/Whisper-Ward.git
   ```
2. Open **Unity Hub** and click **Add** $\to$ select the `Whisper Ward` folder.
3. Open the project with **Unity 6 LTS (6000.3.17f1)**.

### Running the 3D Playground Scene
1. In the Unity top menu bar, select:
   **`Whisper Ward` $\to$ `Build CorePlayground Scene`**
   *(This programmatically constructs the full 3D arena, materials, lighting, NavMesh, player, and guards).*
2. Open `Assets/Scenes/CorePlayground.unity`.
3. Press **Play** (▶️) in the editor toolbar.

### Running Automated Tests
1. In Unity, open the Test Runner window: **`Window` $\to$ `General` $\to$ `Test Runner`**.
2. Select **EditMode** or **PlayMode** and click **Run All**.
3. All **295+ automated tests** will execute and report passing green checkmarks.

---

## 📊 Performance & Engineering Standards

- **Target Framerate**: $60\text{ FPS}$ ($16.67\text{ ms}$ budget).
- **Draw Calls**: $\approx 180\text{--}260$ (well beneath the $1000$ PC and $300$ WebGL budget ceilings).
- **AI Execution Envelope**: $< 0.8\text{ ms}$ total frame execution for all active guards.
- **Garbage Collection**: **0 Bytes GC allocation** on hot-path update loops.

For detailed mathematical proofs and architecture specs, read the [Technical Portfolio Showcase](docs/portfolio-showcase.md).

---

## 📜 License
This project is licensed under the MIT License — see the [LICENSE](LICENSE) file for details.
