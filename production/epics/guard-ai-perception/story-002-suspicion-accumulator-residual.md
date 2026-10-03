# Story 002: Suspicion Accumulator & Residual Wariness Engine

> **Epic**: Guard AI & Perception (`production/epics/guard-ai-perception/EPIC.md`)  
> **Story ID**: `GUARD-02`  
> **Status**: Complete  
> **Layer**: AI & Perception  
> **Type**: Logic  
> **Estimate**: 4h (0.5d)  
> **Manifest Version**: 2026-10-03  
> **Owner**: `systems-designer`  

## Context

**GDD**: `design/gdd/perception.md` (R6 Accumulator, R7 Confirm Window, R9 Residual, R11 Cancel-Cap, F1..F5 Formulas)  
**Governing ADRs**: `ADR-0001` (Event Bus)  
**Engine**: Unity 6 LTS (6000.3.17f1) | **Risk**: LOW  

**Formulas & Rules**:
- Charge rate: $dA/dt = \min(k / d, \text{rate\_max})$ with $k = 1.5$, $\text{rate\_max} = 0.60\text{ A/s}$.
- Accumulator clamp: $A \in [0.0, 1.0]$. Fully resets to $0$ on LOS break.
- Investigate entry threshold: $T_{base} = 0.30$. Reaching $T_{entry}$ starts confirm window $t_{win} = 1.2\text{ s}$.
- Residual charge rate: $r_{res} = 0.10\text{ /s}$ during LOS exposure.
- Residual decay: $R(t) = R_0 \times e^{-t / \tau_{res}}$ with $\tau_{res} = 16.0\text{ s}$.
- Dynamic entry threshold: $T_{entry}(R) = \max(T_{base} - k_{res} \times R, 0.2 \times T_{base})$ with $k_{res} = 0.24$.
- Cancel counter: $n_{cancel} \ge 3$ player-caused window cancels triggers cap-forced Investigate.

---

## Acceptance Criteria

- [x] **AC-GUARD-05 — Distance-Inverse Accumulator Integration**: At distance $d=3.0\text{ m}$, charge rate is $1.5 / 3.0 = 0.50\text{ A/s}$. At $d \le 2.5\text{ m}$, rate clamps to $0.60\text{ A/s}$.
- [x] **AC-GUARD-06 — LOS Break Reset**: When LOS breaks, accumulator $A$ resets immediately to $0.0$, but residual $R$ is preserved.
- [x] **AC-GUARD-07 — Confirm Window & Escalation Commit**: When $A \ge T_{entry}$, a $1.2\text{ s}$ confirm window opens. Sustained LOS past $1.2\text{ s}$ emits `InvestigateCommit`. Breaking LOS inside window cancels commit and increments cancel counter.
- [x] **AC-GUARD-08 — Residual Threshold Sinking & Cancel-Cap Closer**: At $R = 1.0$, $T_{entry}$ sinks from $0.30$ to $0.06$. At $n_{cancel} = 3$, an immediate forced investigate event is emitted.
