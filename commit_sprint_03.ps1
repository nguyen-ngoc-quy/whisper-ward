git add -A
$commitMsg = @"
feat(ai): complete Sprint 03 stealth gameplay loop

- Implement VisionConeSensor (100 deg FOV, 12m, 5Hz) with stance-adaptive aim points (GUARD-01)
- Implement SuspicionAccumulator with residual wariness engine and dynamic threshold sinking (GUARD-02)
- Implement VisionConeVisualizer procedural 3D mesh with MaterialPropertyBlock styling (GUARD-03)
- Implement GuardFSMRuntimeController with 3-state speed coupling and catch contract (GUARD-04)
- Implement search dwell look-around scan and give-up return to Patrol (GUARD-05)
- Implement PlayerFootstepNoiseEmitter and GuardHearingSensor with E20 linecast occlusion (NOISE-01)
- Add 46 unit tests and 5 end-to-end integration tests for complete stealth loop
- Update CorePlaygroundBuilder and scene wiring for interactive 3D stealth arena

Co-Authored-By: Claude Code <noreply@anthropic.com>
"@

git commit -m $commitMsg
git push origin main
