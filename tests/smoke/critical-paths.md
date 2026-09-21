# Smoke Test: Critical Paths

**Purpose**: Run these 10-15 checks in under 15 minutes before any QA hand-off.  
**Run via**: `/smoke-check` (which reads this file)  
**Update**: Add new entries when new core systems are implemented.  

---

## 1. Core Stability (Always Run)

1. Game launches to main gameplay scene without crash.
2. Virtual Clock starts and increments deterministically without NaN/Infinity.
3. Pause menu toggles without audio hitching or state corruption.
4. Attempt Epoch increments atomically upon capture/respawn.

---

## 2. Core Mechanics (Stealth & AI Loop)

5. **Locomotion**: Player can crouch ($1.8\text{ m/s}$), walk ($3.6\text{ m/s}$), and sprint ($6.25\text{ m/s}$).
6. **Headroom Clearance**: Standing up under low ceiling is blocked; player remains crouched.
7. **Burst Projectile**: Throwing Burst lands at aimed location and emits $10.5\text{ m}$ sound radius.
8. **Guard Investigation**: Guard turns and moves to sound location without freezing.
9. **HideSpot**: Player enters HideSpot, visibility is masked, translation is locked ($\Delta \vec{p} = \vec{0}$).
10. **2.5D Catch Gate**: Guard catches player within $5.50\text{ m}$ only with line-of-sight and $|\Delta Y| \le 1.0\text{ m}$.

---

## 3. Data Integrity & Telemetry

11. Telemetry ring buffer records FSM transitions up to 2048 entries without memory growth.
12. Room Grade calculates score deductions and outputs letter rank (S/A/B/C/F).
13. WebGL LocalStorage syncs trace log under key `"ww_fsm_trace_latest"`.

---

## 4. Performance & Audio

14. Framerate maintains stable 60 fps (budget 16.6 ms per frame).
15. Low-Pass Filter smoothly cuts off to $800\text{ Hz}$ when inside HideSpot and $1200\text{ Hz}$ when occluded.
