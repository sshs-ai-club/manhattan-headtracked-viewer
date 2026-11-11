# Roadmap

Plan for turning the Manhattan head-tracked viewer into a booth game for the 천년제 on **Dec 29, 2026** (09:00 to 12:00). Read [GDD](../game/GDD.md) for what the game is and [ROLES](ROLES.md) for who owns each area. Checked boxes mean verified in the repository at the time of writing; everything else is a proposal. Every task becomes one GitHub issue (see [AGENTS.md](../../AGENTS.md#parallel-work-with-ai-agents)); this file lists them, the issue holds who does it.

## Where we are (Nov 11)

Read from `main`:

- [x] Streamed Manhattan with off-axis head-tracked projection, webcam tracker, Windows release ZIP; release v0.1.1 is a viewer with flight only (Core).
- [x] Free flight with keyboard, mouse, HID gamepad/BLE controller (Core, Hardware).
- [x] Web swing with anchor assist, rope wrap, wall run, two-hand launch and speed effects with a wind sound (Swing). Runs only in development traversal builds.
- [x] Wrist input: parser, calibration and UDP receiver. `MouseWebShooter` listens to `HandAimController.Fired`, so a wrist can fire once (Hardware). No release signal, no real ESP32 test, and the receiver must be placed by hand.
- [ ] `hardware/` holds only a README; the firmware lives in a personal project.
- [ ] Game layer: no rings, timer, score, leaderboard or menus.
- [ ] 1v1: no networking code.
- [ ] Character and audio: procedural web visuals only; wind is the only sound.
- [ ] Decisions 0001 to 0008 are all Proposed.

Almost all recent commits come from one person, so everyone else must be able to build and merge without waiting on them.

## Booth plan to Dec 29

Each date closes the tasks listed under it. If one slips, cut **stretch** tasks rather than move the code freeze. Areas are in brackets; owners come from the issue, not this file.

| | Date | Milestone | Done when |
|---|---|---|---|
| M0 | Nov 14 | **One team** | Tasks on the GitHub Projects board, ROLES confirmed, open decisions answered |
| M1 | Nov 22 | **Booth build boots** | One program opens straight into swing mode on a 10-ring route; ESP32 sends over Wi-Fi; everyone builds from source |
| M2 | Nov 29 | **Core loop** | Rings, timer, finish and restart work; the wrist holds and releases the web, with mouse fallback |
| M3 | Dec 6 | **Booth flow and sync** | Name to result in one 4-minute slot, leaderboard, two laptops see each other, hands and sounds in |
| M4 | Dec 13 | **Feature complete** | Match rules, 3 devices, spectator screen, 60 fps on both laptops. 1v1 go or no-go on **Dec 15** |
| M5 | Dec 20 | **Code freeze** | Booth release installed on all three laptops, comfort test done. Only crash fixes merge after this |
| M6 | Dec 29 | **Booth day** | Rehearsal Dec 22 to 23, day-of checklist Dec 27, booth 09:00 to 12:00 |

### Nov 14

- [ ] Update [ROLES](ROLES.md) and put every task in this plan on the GitHub Projects board, one issue each. (design)
- [ ] Accept or reject decisions 0001 to 0008; 0002, 0003 and 0006 affect the booth directly. (design, all)
- [ ] Weekly 30-minute check-in until Dec 20; each person reports what merged, not what was tried. (design)

### Nov 22

- [ ] Booth build that starts in swing mode, with development keys hidden. (core, swing)
- [ ] One ring route: 10 rings, about 3 minutes, in Midtown, in city coordinates, starting on a rooftop. (world, design)
- [ ] Move the wrist device to ESP32 and send `yaw,pitch,fire` over Wi-Fi UDP to the laptop hotspot (port 8766). In progress. (hardware)
- [ ] Buy the 2 webcams and test tracking at the second seat (about 1.3 m). (core, design)
- [ ] Everyone builds and runs the project on their own laptop ([team quickstart](../team-quickstart.md)). (all)

### Nov 29

- [ ] Ring pass detection, timer, finish line, one-key restart. (design)
- [ ] Hold and release: the device sends a held state and the game lets go when it stops. (hardware, swing)
- [ ] The receiver is added to the booth build automatically, shows connection status and falls back to the mouse. (hardware)
- [ ] Separate recentring from grabbing: a button, or recentre only at the start of a run. (hardware)
- [ ] Measure aim drift over 3 minutes; choose MPU-6050 or BNO085. (hardware)

### Dec 6

- [ ] Booth flow: name entry, face calibration, countdown, run, result. (design)
- [ ] Leaderboard per 부, saved to a local file, with a reset. (design)
- [ ] Position sync between the two laptops over LAN: UDP, 20 to 30 updates a second, absolute city coordinates. (core)
- [ ] Opponent avatar and their web line. (character, core)
- [ ] Simple first-person hands with a firing pose. (character)
- [ ] Web shoot, attach, ring and finish sounds, plus one music loop with a clear licence. In progress. (audio)

### Dec 13

- [ ] Match rules: web pulls the opponent, rooftop capture points, 3 rounds of 90 seconds; one laptop decides hits and score. (design, swing, core)
- [ ] Ring pass flash, finish and result screen effects. (character)
- [ ] Spectator view on the 전자칠판: play feed, face tracking, speed and angle, leaderboard. (core, design)
- [ ] 60 fps on both booth laptops at the booth resolution, with a quality preset. (core)
- [ ] Build 3 wrist devices with straps, power bank and cable; vibration optional. (hardware)
- [ ] Firmware, wiring and pin table in `hardware/`. (hardware)
- [ ] Stretch: head-dodge obstacles using the tracked eye position. (design, world)
- [ ] Stretch: hidden items that only show when the player leans. (design, world)
- [ ] Line up equipment: 2 monitors, 2 GPU laptops plus a spare, LAN cable, power strips, HDMI; write down who brings what. (all)
- [ ] Buy the blackout curtains. (all)

### Dec 15

- [ ] Go or no-go on the full duel. No-go means 2부 runs as a two-player race on the synced build. (design, core)

### Dec 20, code freeze

- [ ] Portable offline booth release installed on both laptops and the spare. (core)
- [ ] Comfort playtest with 5 outside players ([protocol](#comfort)); fix only the top issues. (design)
- [ ] Window frame from 우드락 and 시트지, sized to the play monitor, not covering the webcam. (world)
- [ ] One-page poster: how to play, and why the screen looks like a window. (design)

### Dec 22 to 29

- [ ] Buy snacks (week of the festival). (all)
- [ ] Full rehearsal in a classroom: set up in 30 minutes, run the 1부 flow for 20 minutes with outside players. (all)
- [ ] Day-of checklist by Dec 27: setup order for 08:30 to 09:00, who stands where, what to do if a laptop or device fails. (design)
- [ ] Check the application result and the assigned room when announced; the room decides where the monitors, the 전자칠판 and the queue go. (design)

## Streams

### Comfort

Goal: someone with no VR experience plays ten minutes without feeling dizzy. This outranks every feature.

Likely causes, so we can test one at a time:

| Cause | What to try | Owner |
|---|---|---|
| Head-tracking jitter and latency | Measure end-to-end latency (move head, see change). Check the existing filter in the `head-tracked-display` package (verify what it does before changing it). Candidate: 1-Euro filter and short prediction. Keep a recorded-head-motion test so changes are comparable | Core |
| The world moves, your body does not (vection) | Keep the physical window frame visible as a rest reference. Never roll the camera. Cap angular acceleration when swinging. Optional comfort vignette at speed, off by default and exposed as a setting, not a hidden effect | Swing + Core |
| Low or uneven frame rate | 60 fps floor on the reference machine, frame-time spikes tracked (not only average). See Rendering | Core |
| Sudden direction changes at attach and release | Ease the attach pull, smooth the release, tune `attachPull` and rope length ratchet | Swing |
| Fast mouse look decoupled from head | Mouse look smoothing and a sensitivity default that is lower than now | Swing |
| Tracking loss (face leaves camera) | Hold the last pose, fade back in; never snap | Core |

Comfort test protocol (owner: Game design): the same route, ten minutes, fresh player, they rate nausea, eye strain and disorientation 0 to 3 at the start, at five minutes and at the end. Pass is no rating above 1 for at least four of five players. Record GPU, frame rate and settings with it.

### Rendering and performance

Source of truth: [validation](../validation.md), reference machine RTX 4070 Laptop 8 GB, 1920x1200. Swing speed (up to 70 m/s per `SwingSettings.maxSpeed`) is far above the old 10 m/s walking speed the streamer was tuned for.

- [ ] Benchmark route at swing speeds (30, 50, 70 m/s). Does streaming keep the city loaded, and does the skyline fallback hide pop-in? (Core)
- [ ] Collision proxies now stay on for swinging. Measure their cost; target from [architecture](../architecture.md) is at most 1 ms added P95 CPU. (Core + Swing)
- [ ] Frame-time budget table (CPU, GPU, streaming) per feature, so new features have a limit to fit in. (Core)
- [ ] Temporal anti-aliasing ghosting at speed; shadow cost; texture memory. Cheap quality presets for weaker demo machines. (Core)
- [ ] Hero and VFX budget: hero model under 20k triangles, one material, no per-frame allocations. (Character)
- [ ] Do not change comparison switches (Tracking, Window, Surface) behaviour; those are accepted contracts in [AGENTS.md](../../AGENTS.md).

### Swing, movement and animation

Goal: swinging feels good within the first ten seconds and gets better with skill.

- [ ] Playtest the existing swing end to end; list the five worst things. (Swing, with a second player)
- [ ] Always something to grab: anchor assist exists; still check that a visible reticle turns solid when a hit is possible. (Swing)
- [ ] Speed techniques that reward skill: release boost at the bottom of the arc, web zip to a point, air dive. Add one at a time, each with a decision or a feel note. (Swing, Design)
- [ ] Ground start without attach-pull jerk; landing and rooftop running. (Swing)
- [ ] Hero in view: first person only for v1 (the camera is the eyes, so head tracking stays correct). Arms and hands with web shooters, a firing pose, a swing pose, arms that reach toward the anchor. (Character)
- [ ] Web visuals: a curved rope with a short travel time, a hit splat on the anchor, a release flick. Replace the temporary cylinder. (Character, Swing)
- [ ] Speed feel: wind sound, camera shake kept off (comfort), particle streaks at the edges. (Character, Audio)
- [ ] Hero model: either original, or an existing one with a licence that allows use. See [decision 0002](../decisions/0002-name-and-ip.md).

### Hardware and software pairing

Current state: BLE gamepad as an HID joystick (`ControllerMotionInput`), wrist device protocol over UDP (`WristUdpReceiver`), serial receiver compiled out under .NET Standard 2.1, bridge and firmware kept outside the repo. See [decision 0005](../decisions/0005-hardware-input-contract.md) and [hardware/README](../../hardware/README.md).

- [ ] Put firmware and the serial/BLE-to-UDP bridge in `hardware/` with pinned dependencies, so anyone can reproduce the device. (Hardware)
- [ ] Wire aim and fire into the swing. Fire is wired through `MouseWebShooter` (fires once); hold and release is the Nov 29 booth task. (Hardware + Swing)
- [ ] One input facade so the game asks "what is the player doing" (move, aim ray, fire, release, recenter) and does not care which device answers. Priority: wrist, then mouse; controller for movement. (Core defines, Hardware implements)
- [ ] On-screen device status: connected, calibrated, last packet age, battery if available, with one-key recalibration. (Hardware + Design)
- [ ] Latency and drop test with a real device: packets per second, worst gap, behaviour on disconnect (fall back to mouse, never freeze). (Hardware)
- [ ] Pairing guide for a new person in under ten minutes, tested by someone who did not write it. (Hardware)
- [ ] Wrist aim over a head-tracked screen: the ray goes from the *tracked eye* through the screen point, so leaning changes what you aim at. Decide whether that is wanted or confusing. (Hardware + Design playtest)

### Audio

Goal: sound makes the swing feel physical. No audio exists yet.

Events other areas must raise (Swing owns the list, Audio listens): `WebFired`, `WebAttached`, `WebMissed`, `WebReleased`, `Landed`, `SpeedChanged(speed)`, `RingCollected`, `RunStarted`, `RunFinished`.

- [ ] Placeholder SFX for web fire, attach, release, landing, ring pickup. (Audio)
- [ ] Wind loop whose volume and pitch follow speed. (Audio)
- [ ] City ambience: traffic, distant sirens, rooftop wind; positional so it follows the tracked head. (Audio)
- [ ] One music loop for M1, then adaptive layers that fade in with speed or combo. (Audio)
- [ ] Mix: master loudness target and ducking so SFX stay readable over music. (Audio)
- [ ] Every sound recorded in the licence table with source and licence. CC0 or original preferred; CC-BY needs attribution. (Audio)
- [ ] Formats: short effects as `.wav`, music and long loops as `.ogg`. Both tracked by LFS.

### World and levels

- [ ] A route is a list of ring positions in city coordinates (never the floating-origin scene position). Store as data, not scene edits, so several people can add routes without conflicts. (World)
- [ ] Find areas where the swing works well: tall dense blocks. Mark dead zones: parks, rivers, gaps where no building can be attached. (World + Swing)
- [ ] Landmarks to route through (bridges, towers, the park edge) as player-visible goals. (World)
- [ ] Collectables, mission spots, rooftop landing pads. (World + Design)
- [ ] Optional extra models: the Sketchfab Manhattan model linked by the team (CC-BY, 336k faces) could be used for landmarks only; check it with Core before importing since it is a different pipeline from the NYC data. See [decision 0007](../decisions/0007-assets-and-licences.md).

### Game design and production

- [ ] Own the [GDD](../game/GDD.md) and keep it short enough to read in five minutes.
- [ ] Keep the decision index in [docs/decisions](../decisions/README.md) current; close open ones at M0.
- [ ] Task board (GitHub Projects or Issues with area labels). One issue per task, one owner per issue.
- [ ] Playtest every milestone with people outside the team. Write what they did, not what they said they liked.
- [ ] Decide the story and mode set per [decision 0004](../decisions/0004-modes-and-story.md).

## Risks

| Risk | Why it matters | Mitigation |
|---|---|---|
| Dizziness | Single biggest threat to a head-tracked game | Comfort stream first; protocol above; comfort options |
| Core bus factor | One person knows rendering and tracking | Docs as you go; second person builds from source early |
| Merge conflicts in Unity files | Scene and `.meta` files do not merge | Scene frozen; prefabs and code; small PRs |
| Using Spider-Man branding | Trademark and licence issue if the game is shared widely | [Decision 0002](../decisions/0002-name-and-ip.md) |
| Asset licences | Public repo, public release | Licence line in every PR; LFS for binaries |
| Hardware not reproducible | Firmware and bridge live in a personal project | Move into `hardware/` |
| Too many agents, too few reviewers | Review becomes the bottleneck | Small PRs, area owners review, agents never merge |
| Heavy setup excludes people | Source build needs several GB and a Unity licence | Playtest from the ZIP; only builders need Unity |
| Scope growth | 1v1 and stretch features can swallow the schedule | Go or no-go on Dec 15; cut stretch tasks before moving the Dec 20 freeze |
| Silence on `main` | Nothing merged between Oct 21 and Nov 11 | Weekly check-in reporting merges only |
| Decision 0003 vs the 1v1 plan | 0003 recommends no live multiplayer | Update 0003 or agree now that 2부 falls back to a synced race |
| Wrist device fails at the booth | A run stops in front of the queue | Mouse fallback with no restart; a charged spare device |
| Windows only | Release and tracker are Windows x64 | Accept and say so; do not promise Mac or web |
