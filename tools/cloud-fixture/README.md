# Disposable cloud footprint test setup

Scratch branch only, based on PR #28 at 9191c40b1e1762e0a8d7ca79bf0d098595fcd0bf. Do not merge, publish a player, or reuse this setup as a full-city catalog. This supports the existing #27/#29 native test follow-up without changing either PR branch or any assertion/runtime source.

Use a clean disposable Windows Unity Build Automation target with the same pinned Editor and unchanged EditMode test configuration. Set its repository-relative Pre-Build Script to `tools/cloud-fixture/prebuild.sh`. Do not use a pre-export callback: fixture setup must finish before tests. The hook runs before UBA launches its normal Unity process.

The Bash wrapper invokes Windows PowerShell, which downloads only the canonical public city-source-v2 `Manhattan_Source_and_Materials.zip` (81,434,566 bytes) and verifies SHA-256 `9d703c29ec6dc1192249a26e1d3577b1ca06beffddb61f12e43db1ded86f47b0`. It extracts only the exact building JSON and its original provenance to a new cache folder. Building JSON is 19,186,837 bytes with SHA-256 `9bd3a981f30e6efcc783325389211219a47d47730bfb50d3d83469ebd548d091`, matching the repository's pinned source manifest. The complete downloaded archive retains all original notices.

The helper uses the exact `CityBake.BakeBuildings` building-conversion loop at the base commit: source order, float32 values, first ring, duplicate endpoint removal, orientation, 0.22m ground, height and Bounds calculation. All 44,567 records are retained. `CityChainGeometry` selects its original region and constructs its original replay geometry unchanged. Tiles are intentionally empty. This supplies footprint-replay data, not rendered/streamed city coverage.

Setup refuses an existing CityCatalog or its metadata, and fails on download, integrity, duplicate/source count, Editor, readiness-marker or persistence errors. It does not delete existing files, change security settings, add credentials, skip tests, replace runtime DLLs, or suppress test failures. A separate invocation of the documented `UNITY_EXE` creates the catalog before the standard cloud test invocation. Source assets, generated catalog and logs remain uncommitted.

Local checks: Bash syntax PASS; source SHA-256/byte count and all 44,567 unique records PASS; conversion loop exact-text comparison PASS. Unity compilation and Windows PowerShell execution NOT RUN in the preparation environment. The same-process catalog checks are smoke checks, not an independent disk round-trip proof; the subsequent UBA test process must load the serialized catalog. Cloud execution must demonstrate actual setup and test results; a missing catalog becoming non-null is not proof that all replays pass.

Official interface documentation:
- https://docs.unity.com/en-us/build-automation/advanced-build-configuration/run-custom-scripts-during-the-build-process
- https://docs.unity.com/en-us/build-automation/reference/available-environment-variables

Canonical source release: https://github.com/sshs-ai-club/manhattan-headtracked-viewer/releases/tag/city-source-v2
