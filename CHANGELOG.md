# Changelog

## 1.0.0-rc1 — First public release candidate

- promoted the accepted 0.10.1 public-hardening line to the first public RC;
- added the MIT License;
- added canonical `dist/distribution.json` release metadata for the `stranded-deep-diagnostics` repository;
- clarified Unity Player executable version labels so they are not presented as the Stranded Deep game version;
- added RC source/release verification guidance;
- kept the diagnostics runtime behavior and inspection-first safety boundary otherwise unchanged.

## 0.10.1 — Acceptance fixes

- made the overlay width content-aware up to the active split-screen viewport limit;
- enabled wrapped line layout with per-line height calculation so long diagnostics are not silently cut at the right edge;
- split the hotkey footer into shorter rows;
- local bootstrap now migrates the exact legacy development `ReportRoot` value from pre-0.10 builds back to the portable default;
- preserved all 0.10.0 diagnostics, adapters, safety boundaries, and report formats.

## 0.10.0 — Public Hardening candidate

- changed report output to a portable BepInEx-config-relative default;
- made `build.ps1` portable through `-GameRoot` / `STRANDED_DEEP_GAME_ROOT`;
- expanded the capability manifest with `AVAILABLE`, `PARTIAL`, and `UNAVAILABLE` module states;
- surfaced active-module capability state in the overlay;
- separated BamEx-specific Boombox and Raft Furniture knowledge into `Adapters/BamEx`;
- reduced generic AUDIO discovery to generic runtime-type/object helpers;
- added public architecture and adapter documentation;
- added a public-source audit helper;
- preserved the existing inspection-first safety boundary and existing AUDIO/RAFT functionality.

This version is a development candidate until focused field acceptance is complete.
