# Game design document

Working title: **Manhattan Swing** (placeholder, see [decision 0002](../decisions/0002-name-and-ip.md)). Owner: Game design. Keep this under five minutes of reading; put details in linked files.

## One sentence

You sit at your own monitor, which becomes a real window into Manhattan, and swing between skyscrapers by shooting webs, aiming with your hand.

## Goal: the 천년제 booth (Dec 29, 2026)

The first public target is the school festival booth, **09:00 to 12:00 on Dec 29**. Every feature is judged by whether it helps that morning. Dated tasks are in the [ROADMAP](../team/ROADMAP.md#booth-plan-to-dec-29).

- **1부, race:** one player at a time swings a 10-ring route in about 3 minutes. Each player gets a 4-minute slot: name entry, face calibration, countdown, run, result. Top 3 of each 부 on the leaderboard win a prize.
- **2부, 1v1:** two players on two laptops over LAN. The full duel (web pulls the opponent, rooftop capture points, 3 rounds of 90 seconds) is decided go or no-go on **Dec 15**; the fallback is a two-player race on the synced build. 1v1 needs [decision 0003](../decisions/0003-multiplayer.md) updated, because it currently recommends no live multiplayer.
- **Spectators** watch on the 전자칠판: the play feed, the player's face tracking, speed and angle, and the leaderboard. The window effect only works for the seated player, so the queue needs its own screen.
- **Booth staff never open Unity.** The booth build starts straight into swing mode with development keys hidden, and one key restarts a run.
- **Must not fail:** a dropped wrist device falls back to the mouse with no restart; 60 fps on both booth laptops; seated play only.

Code freeze is **Dec 20**. If a milestone slips, cut stretch features, not the freeze date.

## Why this can be fun

The unusual part is the *window*. Because the screen is tracked to your head, buildings pass with real depth when you lean, and near things (cables, window frames, the web itself) look physically close. A normal game cannot do this. The game should keep reminding you of it. The motivation videos are [Johnny Lee's Wiimote head tracking](https://www.youtube.com/watch?v=Jd3-eiid-Uw) (the technique we use) and a [short about wanting to be Spider-Man](https://www.youtube.com/shorts/FZVpuuolfcY) (the fantasy). The Sketchfab [Manhattan model](https://sketchfab.com/3d-models/new-york-city-manhattan-372bc495b3a941308f4a3198bc45e17b) is reference only for now.

## Pillars

Use these to say yes or no to ideas.

1. **Swinging feels great.** Rhythm, momentum, release at the right moment. If a feature makes the swing worse, cut it.
2. **Nobody gets dizzy.** Comfort beats spectacle. See [comfort](../team/ROADMAP.md#comfort).
3. **Use the window.** Prefer goals that reward leaning, peeking around corners, and depth.
4. **Three-minute sessions.** A run is short, restartable instantly, and easy to show someone at a table. At the booth, a whole turn fits in 4 minutes.
5. **Anyone can play.** Mouse works; controller and wrist device make it better, never required.

## Core loop (seconds)

Aim, fire, attach, swing, release at the right point, land or fire again. The loop is repeated 50 or more times a run. Everything else is the reason to repeat it.

## Layers on top (minutes)

| Layer | Idea | Cost | Stage |
|---|---|---|---|
| Time trial (1부 race) | Swing a ring route, beat the leaderboard | Low | Booth, required |
| Head-dodge obstacles | Pigeons or a helicopter; the tracked eye position is the hit test, so ducking for real is the input | Low | Booth, stretch |
| Lean-only items | Hidden items that only appear when the player leans | Low | Booth, stretch |
| Synced race (2부 fallback) | Two laptops, same route, see the opponent | Medium | Booth, required if 1v1 is no-go |
| 1v1 duel (2부) | Web pulls the opponent, rooftop capture points, 3 x 90 s rounds | High | Booth, go or no-go Dec 15 |
| Free swing | Collect floating tokens across the city, no timer | Low | after the booth |
| Rescue / delivery | Reach a rooftop point and carry something to another | Medium | after the booth |
| Ghost race | Race your own or a friend's recorded run | Medium | after the booth, see [decision 0003](../decisions/0003-multiplayer.md) |

## Booth slice: 1부 race

- One route of about 3 minutes in Midtown, 10 rings in city coordinates, difficulty rising along the route, start on a rooftop.
- Ring pass detection, timer, finish line, instant restart on one key.
- Booth flow: name entry, face calibration, countdown, run, result. Leaderboard per 부 saved to a local file, with a reset.
- Web shoot, attach, ring and finish sounds, wind that follows speed, one music loop with a clear licence.
- Simple rounded first-person hands with a firing pose (the club agreed simple looks better than half-finished detail), ring pass flash, finish and result effects.
- Anchor assist so there is always something to grab (already built).
- Mouse always works; the wrist device holds and releases the web when connected.

Exit test: the comfort playtest with 5 outside players on Dec 20. Note who gets dizzy or confused, then fix only the top issues. At least three should ask to play again without being asked.

## 2부 1v1 rules (if go on Dec 15)

- Positions sync over LAN at 20 to 30 updates a second in absolute city coordinates (the floating origin differs per laptop).
- One button: aiming at a building swings, aiming at the opponent pulls them. One laptop decides hits and score.
- Rooftop capture points, 3 rounds of 90 seconds.

## Controls (today, from [web-swing](../web-swing.md))

| Input | Action |
|---|---|
| Hold left click / release left click | Fire and hold / detach |
| Hold right click / E | Reel in |
| Q hold / release | Charge / release two-hand launch |
| Z | Pay out |
| R | Toggle flight / traversal |
| Space | Ground jump / swing hop / wall jump |
| WASD | Air steer |
| Wrist device | Aim and fire once today; hold to keep the web and release to let go is a booth task. Recentre must not happen in mid-swing |
| Gamepad / BLE | Move (aim and fire not defined yet) |

Swinging runs only in development traversal builds today; the booth build will start in it. Which input wins when several are connected is [decision 0006](../decisions/0006-camera-and-input-priority.md).

## Feedback the player needs

Reticle that shows when a hit is possible; web travel time visible; speed readout; ring pass sound and flash; time delta against best; clear fail and instant retry. No text walls. Device connection status on screen.

## Out of scope for now

Online multiplayer beyond the two booth laptops on one LAN, third-person camera, character progression, voice acting, story cutscenes, mobile or web builds. Each can be reopened by a decision record.

## Open decisions

In [docs/decisions](../decisions/README.md); 0001 to 0008 are all Proposed. The booth depends directly on 0002 (no Spider-Man name, suit or logo anywhere public), 0003 (multiplayer, must change for 1v1) and 0006 (input priority). Accept or reject them in the week of Nov 11.
