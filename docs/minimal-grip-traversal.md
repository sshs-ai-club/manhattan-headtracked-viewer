# Minimal grip traversal (experimental)

Opt in with `--minimal-grip`. The existing controls remain the default.
Aim with the mouse; press/hold left mouse to attach and power the web; release to coast.
The capture click never fires. Esc cancels input and requires neutral before rearming.
One button still includes continuous aim. There is no WASD, jump, separate reel, launch,
velocity steering, automatic refire, or route-position rescue in this mode.

## Physical contract

The powered winch adds inward acceleration only (55 m/s² cap), up to an inward speed
target of 18 m/s, with positive work capped at 1200 J/kg/s. It cannot push outward to
brake motion toward an anchor. `GripMotorWork` is signed kinetic work in J/kg.
A compliant tensile spring/damper replaces the old rigid constraint in this mode:
35 s⁻² stiffness, 8 s⁻¹ damping, 90 m/s² tension cap, and no rope position projection.
Slack take-up consumes only actual inward travel; routing preserves elastic rest length.
`TensionAcceleration` and `GripMotorAcceleration` are separate: the motor cap is not a
whole-body acceleration cap. Collision and gravity remain real simulation constraints.
These are game-scale powered-web forces, not human-safe force/comfort guarantees.

`BufferedGripInput` is a future-glove mock, not firmware. It requires monotonic sequence
numbers and host-monotonic receipt times, expires held state after 200 ms, and latches
loss/releases even between render reads. Neutral is required before rearming. Existing
wrist v1 events are unchanged. Raw tracked head pose is not a movement force input.

## Measured checks

Windows, Unity 6000.3.22f1 preserved player, modified managed assemblies compiled with
.NET 8 Roslyn against that player's references. This is not a clean Unity Editor build.
- Portable suite: 170/172 passed, two skips; manifest and eight PowerShell checks passed.
- Existing native traversal validation passed. Historical AirChain: ON 1080/1080,
  OFF comparison 396/1080; all 2160 executed. Slingshot: 1152/1152 passed.
- Main-thread NUnit assertion harness: 229 passed, one Editor serialization test skipped
  (includes 16 camera assertions from the separate camera draft).
- One-grip restricted scripted fixtures: 12/12 passed across 12/20 distinct collider
  buildings, 5/9 alternating right-angle course changes, 45/50 m spacing, 30/60/120 fps.
  Every shot/release is an explicit input; actual attach/release counts must match, with
  no automatic detach, collision, floor touch or additional body reset. Travel 408–794 m;
  maximum motor acceleration 55, cable tension 90, total acceleration 134.23 m/s²;
  rope position correction zero. This is open-air course turning, not facade wrapping.
  The state-aware pilot uses visible high facade anchors and speed-aware release timing.
- Independent review found nonphysical push braking, loss hidden by fresh packets and
  overly permissive route evidence, and a release-frame pause. Failure-first checks now cover those cases. Early
  blocked/fixed-timing fixtures failed; they were not counted as successful human play.

## Remaining gates

Additional runtime-compatible tests: 268 passed, three `SurfaceVariantFactoryTests`
failed identically in both the changed and unchanged-baseline headless players:
`NativeFactoryRoundTripPreservesOpticalCoverageAndRestoresImportedPointersAndEmission`,
`OneSourceHasIndependentDefaultAndFactoryCachesEvenWhenFactoriesHaveValueEquality`, and
`RootsRegisteredWhileOffShareOnlyTheirFactoryAndReleaseTheLastVariantReference`.
The first has a missing glTF shader, the second reads smoothness 0 instead of .15, and
the third expects immediate EditMode destruction while Player `Destroy` is deferred.
Baseline comparison is not an Editor pass; all three remain verification limitations.
The full `tools/test.ps1` passed portable checks then stopped because `unity` is absent. Editor-only baking,
serialization and coroutine PlayMode suites remain unrun without the pinned Editor.
Camera component opt-in was probed but active camera motion was gated by uncaptured input
and paused movement; filter/pivot/package tests are not proof of active human camera feel.
No physical glove, human comfort/playtest, rendered streamed-city traversal, trained-policy
performance or clean distributable build is claimed. Keep this opt-in and the PR draft.
