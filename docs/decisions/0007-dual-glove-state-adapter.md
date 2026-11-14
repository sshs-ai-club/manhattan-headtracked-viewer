# 0007: Independent glove state adapter

- Status: Proposed
- Scope: requested dual-glove continuation of grip controls; existing PR, no v1 wire-format change.
- Decision: keep each hand's calibrated aim/held state independent and fail safe; expose a transport-neutral adapter before choosing firmware or transport v2.
- Compatibility: wrist v1 stays untouched. Core/swing input interfaces stay unchanged. No scene or package changes.
- Motion contract: the current single-rope engine accepts the latest deliberate press; no automatic held-hand takeover or fabricated bilateral anchors.
- Sensors: accelerometer-only samples cannot assert absolute yaw. Reject them for absolute-ray traversal. A calibrated orientation provider and heading reference must be validated separately.
- Gates: actual transport, source selection, two simultaneous physical ropes/dual-anchor launch, native tests and hardware/camera/collision integration remain unverified.
- Human review: Hardware and Swing owners must review before activation.
