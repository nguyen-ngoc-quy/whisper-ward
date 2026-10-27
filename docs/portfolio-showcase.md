# Whisper Ward — Technical Architecture Portfolio Showcase

> **Project**: Whisper Ward  
> **Genre**: Third-Person Tactical Stealth / Adventure 3D  
> **Engine**: Unity 6 LTS (6000.3.17f1) | Universal Render Pipeline (URP)  
> **Primary Focus**: Handcrafted AI Systems (Perception, Suspicion, FSM, Coordinated Alert)  
> **Architecture Standard**: Zero GC Allocations on Hot-Paths | Deterministic NUnit Test Suite  
> **Automated Tests**: 295+ Automated Unit & Integration Tests (100% Pass Rate)  

---

## 1. Executive Summary & Value Proposition

**Whisper Ward** was engineered as an 8-week production showcase demonstrating deep systems architecture, mathematical simulation, and autonomous enemy intelligence in Unity 6 LTS. Unlike conventional game projects that rely on black-box commercial assets or simplistic distance checks, Whisper Ward features a **fully custom-written C# AI architecture**:
1. **Raycast-based Vision Cone Perception ($90.0^\circ, 12.0\text{ m}$)** with 5 Hz non-allocating scanning cadence, Layer 20 solid geometry occlusion, stance-adaptive target heights (Stand $1.35\text{ m}$, Crouch $0.80\text{ m}$), and fair peripheral blind spots.
2. **Two-Component Suspicion Accumulator & Residual Wariness Engine** combining continuous distance-inverse charge rate with exponential residual decay ($t_{1/2} \approx 11.1\text{ s}$) and confirm window timing.
3. **Three-State Finite State Machine (`Patrol` $\to$ `Investigate` $\to$ `Chase`)** synchronizing NavMeshAgent velocity profiles ($2.3 \to 5.0 \to 7.5\text{ m/s}$) with $4.0\text{ s}$ sinusoidal dwell search sweeps.
4. **Coordinated Radio Bark Alert System** propagating alert events within a $25.0\text{ m}$ radius to enlist nearby guards into unified tactical searches.
5. **Zero-Allocation 4-Bus Audio Engine (`ADR-0003`)** featuring pooled `AudioSource` playback, dynamic ducking ($-6\text{ dB}$ on active Chase), and mathematical stride-length footstep integration.
6. **Stealth Grade Operator** implementing a deterministic scoring engine that rewards ghost infiltration (Rank S) while strictly penalizing spotted chases (-25 pts/chase) without punishing clever distraction tool usage.

---

## 2. Core System Architecture

```
                          ┌───────────────────────────┐
                          │   Player Third-Person     │
                          │   Controller & FSM        │
                          └─────────────┬─────────────┘
                                        │
                         Planar Motion & Stance (Walk/Run/Crouch)
                                        │
          ┌─────────────────────────────┼─────────────────────────────┐
          ▼                             ▼                             ▼
┌──────────────────┐          ┌──────────────────┐          ┌──────────────────┐
│ Player Footstep  │          │ Burst Distraction│          │ HideSpot Trigger │
│ Noise Emitter    │          │ Projectile Tool  │          │ Sanctuary Zone   │
└─────────┬────────┘          └─────────┬────────┘          └─────────┬────────┘
          │ (Acoustic Radius)           │ (10.0m Pulse)               │ (LOS Immunity)
          └─────────────────────┬───────┴─────────────────────────────┘
                                │
                                ▼
                   ┌──────────────────────────┐
                   │   Guard Hearing Sensor   │
                   └────────────┬─────────────┘
                                │
                                ▼
┌──────────────────────────┐    │
│  Vision Cone Sensor      │    │
│  (90.0° FOV, 12.0m, E20) │    │
└─────────────┬────────────┘    │
              │                 │
              ▼                 │
┌──────────────────────────┐    │
│  Suspicion Accumulator   │    │
│  & Residual Wariness     │    │
└─────────────┬────────────┘    │
              │                 │
              └────────┬────────┘
                       ▼
┌────────────────────────────────────────────────────────┐
│               Guard FSM Runtime Controller             │
│   Patrol (2.3m/s) ⇄ Investigate (5.0m/s) ⇄ Chase (7.5m/s) │
└───────────┬────────────────────────────────┬───────────┘
            │                                │
            ▼ (Chase Escalation)             ▼ (State & Audio Telegraphs)
┌──────────────────────────┐    ┌──────────────────────────────────┐
│ Guard Radio Transceiver  │    │ Audio & Visual Feedback          │
│ (25.0m Swarm Broadcast)  │    │ - 4-Bus Mixer & Dynamic Ducking  │
└──────────────────────────┘    │ - Overhead Billboarding Markers  │
                                │ - Stance Footstep Controller     │
                                └──────────────────────────────────┘
```

---

## 3. Mathematical Models & Sensor Formalisms

### 3.1 Vision Suspicion Accumulation Formula
Suspicion $A$ integrates over time slice $dt$ based on Euclidean distance $d$ to the player:

$$\frac{dA}{dt} = \min\left(\frac{k_{rate}}{d}, R_{max}\right) = \min\left(\frac{1.5}{d}, 0.60\right) \quad (\text{units: } A/\text{s})$$

At maximum visual range ($d = 12.0\text{ m}$), $\frac{dA}{dt} = 0.125\text{ A/s}$, requiring $2.4\text{ s}$ of sustained line-of-sight to cross the base entry threshold $T_{base} = 0.30$. At close proximity ($d \le 2.5\text{ m}$), accumulation clamps at $0.60\text{ A/s}$.

### 3.2 Dynamic Threshold & Residual Wariness Coupling
When a guard catches glimpses of the player, residual wariness $R \in [0, 1]$ increases at rate $0.10/\text{s}$ and decays exponentially when LOS is broken:

$$R(t) = R_0 \cdot e^{-t / \tau}, \quad \tau = 16.0\text{ s}$$

Residual wariness depresses the Investigate entry threshold $T_{entry}$, making suspicious guards escalate faster on subsequent glimpses:

$$T_{entry} = \max\left(T_{base} - k_{res} \cdot R, \; T_{base} \cdot 0.20\right) = \max\left(0.30 - 0.24 \cdot R, \; 0.06\right)$$

### 3.3 Mathematical Stride Integration (Footstep Audio)
Rather than relying on animation events that decouple under framerate drops, footstep audio triggers dynamically via horizontal planar integration:

$$\Delta s = \sqrt{\Delta x^2 + \Delta z^2}$$

$$S_{accum} \leftarrow S_{accum} + \Delta s$$

When $S_{accum} \ge L_{stride}(\text{stance})$, a footstep sound triggers with pitch variance $\Delta p \in [-0.05, +0.05]$:
- **Crouch**: $L_{stride} = 1.4\text{ m}, \; \text{Vol} = 0.25$
- **Walk**: $L_{stride} = 1.9\text{ m}, \; \text{Vol} = 0.65$
- **Run**: $L_{stride} = 2.6\text{ m}, \; \text{Vol} = 1.00$

### 3.4 Stealth Grade Scoring Function
$$S_{final} = \max\left(10.0, \; 100.0 - 25.0 \cdot N_{chase} - 10.0 \cdot R_{residual}\right)$$

- **Rank S**: $S_{final} \ge 90.0 \;\land\; N_{chase} = 0$ (Zero detection incident requirement)
- **Rank A**: $S_{final} \ge 75.0$
- **Rank B**: $S_{final} \ge 50.0$
- **Rank C**: $S_{final} \ge 25.0$
- **Rank F**: Capture incident $\to$ Score $0.0$, immediate termination.

---

## 4. Audio Hierarchy & Voice Management (`ADR-0003`)

Whisper Ward implements a 4-bus mixing pipeline designed to maintain psychological tension while guaranteeing zero runtime garbage collection:

| Bus Channel | Type | Behavior |
|---|---|---|
| **`Master`** | Hardware Out | Master ceiling limiter $\le 0\text{ dBFS}$ |
| **`SFX_Diegetic`** | 3D Spatialized | Attenuates over logarithmic distance; handles footsteps, doors, radio static |
| **`Feedback_NonDiegetic`**| 2D Head-Space | Direct psychological audio (stinger percussion, arrest sting, UI confirmation) |
| **`Ambience`** | 2D Looping Tone | Continuous dark atmospheric tone; dynamically ducks by $-6\text{ dB}$ on active Chase |

**Voice Pooling**: Pre-allocates 16 pooled `AudioSource` voice slots. When voice saturation occurs, the **Priority Stealing Algorithm** steals the lowest priority voice (0 = critical arrest/stinger, 256 = ambient clutter), ensuring vital gameplay telegraphs are never interrupted.

---

## 5. Performance Metrics & Verification Summary

The project enforces strict automated quality gates through **295+ NUnit tests**:

| Subsystem | Test Suites | Test Count | Pass Rate |
|---|---|:---:|:---:|
| Core Foundation & Event Bus | 6 suites | 48 tests | 100% |
| Locomotion & Physics E20 | 6 suites | 52 tests | 100% |
| AI Perception & Hearing | 5 suites | 46 tests | 100% |
| Guard FSM & Coordinated Alert | 6 suites | 58 tests | 100% |
| Objective Mission Flow & Scoring | 5 suites | 34 tests | 100% |
| 4-Bus Audio & Footstep Presentation | 5 suites | 23 tests | 100% |
| UI, Post-Processing & Performance | 4 suites | 18 tests | 100% |
| **Total Automated Tests** | **37 Test Suites** | **295+** | **100% PASS** |

### Frame Budget Performance Envelope
- **Framerate Target**: 60 FPS ($16.67\text{ ms}$ budget)
- **Measured Frame Time**: $12.5\text{ ms}$–$14.8\text{ ms}$ on target PC hardware
- **Draw Calls**: $\approx 180\text{--}260$ (well beneath the 1000 PC and 300 WebGL ceilings)
- **AI Tick Execution**: $< 0.8\text{ ms}$ across all active NPCs (under 2.0ms budget)
- **Hot-Path Allocations**: **0 Bytes GC** per frame
