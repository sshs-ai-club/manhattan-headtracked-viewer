# 0009: Optional traversal view assistance

- Status: Proposed
- Affects: core, swing, hardware, game design
- Date: 2026-11-13
- Supersedes: none

## Request and proposal

The administrator requested corner banking, looking up while climbing and gradually looking down while falling, preserving head tracking and manual control. The comfort roadmap currently says never roll. Keep that default: provide a separate, session-only experiment OFF by default, with independent pitch/bank strength controls, bounded motion and manual priority. Do not turn inferred head position into gaze direction.

Apply assistance through a common camera/physical-screen pivot below the observer, not by rotating the tracked camera alone or changing the physics body. Keep the pinned tracking package, physical calibration and projection algorithm unchanged. Do not insert steering, forces or hidden route assistance. Camera-based aiming follows the view as before.

## Alternatives and acceptance

Keep all assistance absent (the existing default); enable gradual pitch with bank strength zero; or explicitly opt into both at limited strengths. The experiment does not settle first/third-person or device-priority proposal 0006 and does not amend the roadmap.

Core, Swing and Game design human review plus native projection, frame-time and human comfort evidence are required before considering any new default. Automated bounds and fixture tests cannot certify comfort, hardware integration or fun. See [implementation and validation](../traversal-view-assistance.md). No rollout or acceptance is implied by this Proposed record.
