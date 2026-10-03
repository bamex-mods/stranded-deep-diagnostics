# v1.0.0-rc1 — public RC verification

The runtime behavior is inherited from the accepted `0.10.1` line. The RC changes are release-facing: version metadata, MIT licensing, distribution metadata, and clearer environment labeling.

## Accepted baseline

The `0.10.1` acceptance established:

- build/deploy success with matching DLL SHA-256;
- local split-screen world load and module cycling;
- adaptive overlay layout without right-edge text clipping;
- capability states visible in the overlay;
- portable report root under `BepInEx/config/StrandedDeepDiagnostics/Reports`;
- successful report creation;
- clean Diagnostics runtime log audit with no Diagnostics exception/fatal/error;
- optional BamEx adapters present without creating hard dependencies.

## RC verification

1. Build and deploy succeed; build/runtime SHA-256 match.
2. Launch the same two-player local split-screen world.
3. `F8` opens Diagnostics; `F9` cycles modules.
4. Confirm overlay text remains fully visible.
5. `F12` writes at least one report to the portable report path.
6. Confirm the session manifest labels the executable version as `Unity Player executable FileVersion`, not as the Stranded Deep game version.
7. Exit/reload the world once; Diagnostics reopens without stale-reference exceptions.
8. Run `tools/public-source-audit.ps1` against the canonical source.
9. Package and verify through the shared Distribution Tool; require `VERIFY OK` and `Runtime: MATCH`.

No full gameplay regression matrix is required unless one of these checks reveals a regression.
