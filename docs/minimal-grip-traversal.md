# Minimal grip traversal (experimental)

Opt in with `--minimal-grip`. The existing controls remain the default.
Aim with the mouse. Press and hold the left button to attach and power the web winch;
release to coast. The capture click never fires. Esc suspends input, detaches, and
requires a neutral button before rearming. No WASD, jump, separate reel, launch,
velocity steering, automatic refire or route-position rescue is used in this mode.

## Physical contract

The winch applies acceleration along the real, collision-tested web, with a 55 m/s²
acceleration cap, 18 m/s inward speed target and 1200 J/kg/s positive work cap.
Approaching a surface lowers the target and permits bounded radial braking;
tangential motion is untouched. Rope take-up consumes only distance already
travelled inward. Ordinary unilateral rope and swept building constraints remain.
`GripMotorWork` records signed kinetic work in J/kg; `GripMotorAcceleration` records
signed radial acceleration. These are game-scale powered-web forces, not claims
that an unpowered human or real glove can generate them.

`BufferedGripInput` is a transport-neutral future-glove mock, not firmware integration.
It accepts monotonic sequence numbers and host-monotonic receipt times, expires held
state after 200 ms, and requires neutral after loss. Actual raw head translation and
rotation are not motor inputs. Existing wrist v1 event semantics are unchanged.
Mouse/hand aim remains a continuous control channel: one button does not mean no aim.

## Verification and limits

- Portable Windows suite: 172 tests, 170 passed and 2 platform skips; manifest validation
  and eight PowerShell parser assertions passed.
- Changed Core and Viewer C# assemblies compile against the preserved Unity 6000.3.22f1
  Windows player references. Existing native traversal validation passed.
- Main-thread execution of Swing NUnit assertions in a separate copied player:
  197 passed, one Editor-only `SerializedInspectorFlowFlagsReachTheExistingPhysicsSettings`
  skipped. A disposable reflection runner supplies setup/teardown and test-case arguments.
  This is not the official Unity Test Runner or a clean Unity build.
- The new 12/20 distinct-building restricted-input fixtures and full historical replay
  matrices are being measured. They are scripted pilots, not trained AI or human playtests.
- Unity Editor, a clean Player build, physical glove input, human feel/comfort and rendered
  streamed-city traversal are not yet verified. Headless rendering exceptions do not count
  as a rendering pass. Keep the feature opt-in until those gates are complete.
