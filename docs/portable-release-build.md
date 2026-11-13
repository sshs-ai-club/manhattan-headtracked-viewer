# Reproduce the portable Windows distribution

This is a maintainer workflow. End users only download the release ZIP and run
`Run-HeadTrackedDemo.cmd`. Do not send playtesters through Unity setup, source
pack downloads, or tracker installation.

## Reproduce the existing release

Use Windows x64, Python 3.13.5 with pip, Git and the pinned technology checkout.
The city player is the unchanged, verified v0.1.0 artifact pinned in
`tools/portable-player.json`. Download that ZIP from its recorded public URL.
Keep it outside source control. Prepare the technology checkout with
`tools/setup.ps1 -SkipSource -SkipUnity`, or use a clean checkout at the exact
revision in `tools/technology-version.json`.

```powershell
python tools/build_portable_tracker.py --output .cache/portable-stage --technology-root .tools/head-tracked-display
Copy-Item docs/windows-release.md .cache/portable-stage/README-Windows.md
powershell -NoProfile -ExecutionPolicy Bypass -File tools/test.ps1 -DataOnly
```

The builder verifies Python/model/notice archives against `portable-runtime.json`,
downloads the pinned Windows wheels, verifies their digests against official PyPI
metadata, installs into a new staging directory and copies only pinned tracker
and launcher source. The tracker receives one guarded compatibility change:
load the verified face model with Python `read_bytes()` and MediaPipe
`model_asset_buffer`, so Windows folders containing non-ASCII characters work.
Its manifest records the original tracker source digest and this change. All
tracking math and options are unchanged. It does not reuse a local `.venv`. Use a fresh staging path
for each run; the builder will not overwrite an existing environment.

Commit any tooling/documentation changes before packaging. `package_portable_windows.py`
requires a clean checkout and verifies that the Unity source, source manifest and
technology pin match the base player's source revision.

```powershell
python tools/package_portable_windows.py --base-player-zip C:\Downloads\ManhattanViewer-v0.1.0-Windows-x64.zip --runtime-stage .cache/portable-stage --output Builds/Distribution/ManhattanViewer-v0.1.1-Windows-x64.zip
```

The command validates the base ZIP's pinned SHA-256 and clean embedded identity,
combines it with allowlisted portable runtime files, replaces the guide with the
English Windows guide, and writes a ZIP, `.sha256` and `.integrity.json` receipt.
It hashes bytes again during copying, rejects unsafe paths/aliases and unlisted
staging files, and excludes logs, generated caches and personal calibration.
The intentional redistributable face model and newly assembled embedded runtime
are release inputs; neither is committed to Git.

Before publication, extract the exact ZIP into a separate folder, verify every
receipt entry, run both launcher modes with installed Python/Git absent from PATH,
check named camera enumeration and lifecycle cleanup, and retain a scoped report.
Use a separate calibration directory during QA and preserve personal settings.
Do not publish raw camera frames, personal calibration or machine-specific logs.

## Package a newly validated player

First build and validate the intended Windows player with the normal build
workflow. Package it with `tools/package_windows.py`, which checks its embedded
identity, source manifest and notices. Keep the resulting player ZIP and its
`.integrity.json` receipt outside source control. Do not use an arbitrary ZIP or
edit an old binary's identity to make it appear to contain current gameplay.

You can pass an explicit pin without changing the checked-in historical pin.
For example, after producing `.cache/player-current.zip` and its receipt:

```powershell
$receipt = Get-Content .cache/player-current.integrity.json -Raw | ConvertFrom-Json
@{
    sha256 = (Get-FileHash .cache/player-current.zip -Algorithm SHA256).Hash.ToLowerInvariant()
    viewerRevision = $receipt.buildIdentity.viewerRevision
} | ConvertTo-Json | Set-Content .cache/player-current.json -Encoding UTF8
```

The pin must contain a lowercase 64-character `sha256` and a lowercase
40-character `viewerRevision`. Duplicate JSON keys, missing inputs and malformed
digests are rejected. Additional descriptive fields such as `version` and `url`
are allowed, but no URL is downloaded by the packager. A digest of an unvalidated
ZIP is not release qualification.

Prepare a fresh matching tracker stage as above, and stage a Windows guide that
accurately describes this player and its controls. The historical guide describes
v0.1.1 flight only; do not ship that claim unchanged for a new swing build.
Then use an explicit distribution version, for example:

```powershell
python tools/package_portable_windows.py --base-player-zip .cache/player-current.zip --player-pin .cache/player-current.json --distribution-version v0.2.0-preview.1 --runtime-stage .cache/portable-stage --output Builds/Distribution/ManhattanViewer-v0.2.0-preview.1-Windows-x64.zip
```

`--distribution-version` is required with `--player-pin`. It accepts 1–64 ASCII
letters, digits, dots, underscores, plus or minus signs, starting with a letter
or digit. Omitting both flags keeps the historical `portable-player.json` and
`v0.1.1` defaults. The original clean-checkout/source comparison still applies;
on current sources the historical player will be rejected if the Unity source,
source manifest or technology pin differs. The explicit pin is read once and the
same snapshot is used for the source check and archive validation. Receipt fields
keep the distribution revision/version separate from the unchanged embedded
player identity. Player and tracker technology revisions must still match.

Repeat the extraction and two-mode Windows checks above for the exact resulting
ZIP. Record download bytes, extraction time, and time until the city is usable
on a clean target machine before claiming a first-use improvement. This tooling
avoids end-user developer setup when a validated player is distributed; it does
not create that player, reduce city data, add remote streaming, or establish a
measured loading-time improvement by itself. Publishing a release remains a
separate human action.
