# Disposable PR24 footprint test setup

Scratch-only hydration of PR #24 at 11e428399c9135f37219a7d19ff645b36832dace. Do not merge or publish a player. No original source file, test, assertion, runtime behavior, scene, package, or project setting is changed.

The four executable/helper files are copied byte-for-byte from successful PR28 fixture setup commit 51034834617e72c1bb8da4d94d2c1b38b8dea007. CityBake, CityCatalog, CityChainGeometry and source-manifest blobs are identical between PR24 and PR28. This setup supplies all 44,567 canonical original building records through the exact CityBake record-conversion loop; tiles remain empty, so this is footprint-replay rather than full rendering/streaming coverage.

For the observed Windows Unity Build Automation target whose project folder is ManhattanViewer, set Pre-Build Script to `../tools/cloud-fixture/prebuild.sh`. The service resolved scripts relative to that project folder despite repository-relative wording in its documentation. Use a clean disposable checkout: setup refuses an existing CityCatalog asset or metadata.

The setup downloads the canonical public 81,434,566-byte source ZIP and verifies archive SHA-256 9d703c29ec6dc1192249a26e1d3577b1ca06beffddb61f12e43db1ded86f47b0, then verifies 19,186,837-byte building JSON SHA-256 9bd3a981f30e6efcc783325389211219a47d47730bfb50d3d83469ebd548d091. It retains provenance, creates a footprint-only catalog in a separate Editor invocation using UNITY_EXE, and requires the readiness marker before the ordinary unchanged test run.

The known-good PR28 hook completed successfully, including licensing, and its full EditMode run reported 1,132 total / 1,126 passed / 6 failed / 0 skipped; all 66 formerly catalog-null cases passed. Those results are historical evidence for the copied setup, not PR24 test results. Initial preparation can take about 15 minutes; no claim of a quick startup or new test pass is made. This branch must be tested independently. No license/security settings or credentials are changed. No assertions are skipped or relaxed.

Canonical source: https://github.com/sshs-ai-club/manhattan-headtracked-viewer/releases/tag/city-source-v2
