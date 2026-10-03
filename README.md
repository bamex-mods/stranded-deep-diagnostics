# Stranded Deep Diagnostics

`Stranded Deep Diagnostics` is a BepInEx developer tool for runtime inspection and targeted tracing in **Stranded Deep**.

Current public release candidate: **1.0.0-rc1**.

## Usage guides

- [User Guide (English)](USER_GUIDE.md)
- [Памятка пользователя (Русский)](USER_GUIDE_RU.md)

This is an inspection-first toolkit for mod development, reverse engineering, split-screen troubleshooting, physics/state inspection, and reproducible incident capture. It is not a gameplay mod and does not intentionally repair or mutate game state.

## Core modules

```text
OBJECT
PHYSICS
CAMERA
INPUT
UI
INTERACTION
SAVEABLE
STORAGE
INVENTORY
WORLD
CONSTRUCTION
RAFT
AUDIO
TRACE
PLUGINS
```

The core provides:

- split-screen-aware P1/P2 player and camera resolution;
- semantic center-view target selection;
- Rigidbody / Collider inspection;
- bounded safe reflection;
- runtime versus persistent identity inspection;
- storage/inventory/construction/world inspectors;
- snapshot, history, diff and report export;
- targeted opt-in Harmony tracing;
- bounded event/incident recording;
- capability detection with `AVAILABLE`, `PARTIAL`, and `UNAVAILABLE` states.

## Safety model

The public line is inspection-first.

Diagnostics does **not** intentionally:

```text
invoke SaveGame
spawn gameplay objects
write Transform state
write Rigidbody velocity/angularVelocity
change isKinematic / gravity / constraints
call Physics.SyncTransforms()
change IgnoreCollision state
force placement validity
repair attachments
rewrite ReferenceId values
modify world sidecars
```

Targeted Harmony instrumentation is opt-in and is removed when disabled or when the relevant lifecycle ends.

## Controls

```text
F8          Diagnostics ON/OFF
Alt+F8      switch active local split-screen player
F9          next module
F10         pin/unpin current target
F11         module-specific opt-in trace/recorder
F12         report / incident marker depending on module
Shift+F12   text diff or immediate status report where supported
Ctrl+F12    deep safe-field dump in OBJECT/PHYSICS
```

## Capability manifest

Every report session starts with a capability manifest. It records the runtime environment and probes important Stranded Deep types and subsystems.

Capabilities use three states:

```text
AVAILABLE
PARTIAL
UNAVAILABLE
```

A missing game type or optional adapter should degrade the affected feature instead of crashing the whole tool.

The active module also shows its capability state in the overlay.

## Optional adapters

Game-specific core inspection and third-party/custom mod adapters are separate concepts.

Current optional BamEx adapters live under:

```text
Adapters/BamEx/
```

They currently include:

- Boombox audio discovery / `SplitStereoProcessor` integration;
- Raft Furniture attachment/follower inspection.

If those mods are absent, the Diagnostics core remains usable. Production gameplay mods do not reference Diagnostics.

See `ADAPTERS.md`.

## Reports

Default report location:

```text
BepInEx/config/StrandedDeepDiagnostics/Reports/
```

A custom report path can be supplied through the BepInEx config entry:

```text
[Reports]
ReportRoot =
```

Leaving it empty uses the portable default above.

Each session receives its own folder and a `session.txt` capability manifest.

## AUDIO

The AUDIO module contains generic Unity audio/voice/frame inspection plus an optional BamEx Boombox DSP adapter.

When the Boombox adapter is available, `F11` can instrument only `SplitStereoProcessor.OnAudioFilterRead`, using numeric audio-thread telemetry rather than logging/reflection in the callback. `F12` captures a bounded before/after incident window.

Without that adapter, the rest of Diagnostics remains available.

## RAFT

RAFT is a composite module built on the existing physics/object/construction/event infrastructure rather than a separate diagnostics framework.

It can watch loaded raft structures, record bounded physics history and flag evidence such as:

```text
RAFT_TILT
RAFT_ANGULAR_SPIKE
RAFT_LINEAR_SPIKE
RAFT_POSITION_JUMP
RAFT_RB_STATE_CHANGED
RAFT_CENTER_OF_MASS_CHANGED
RAFT_COLLIDER_SET_CHANGED
RAFT_CHILD_RB_CHANGED
```

The BamEx Raft Furniture adapter is optional and read-only.

## Building from source

Requirements:

- Windows;
- Stranded Deep with BepInEx 5 installed;
- the .NET Framework C# compiler available on the machine.

Build:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass `
  -File .\build.ps1 `
  -GameRoot "<path to Stranded Deep>"
```

Build and deploy:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass `
  -File .\build.ps1 `
  -GameRoot "<path to Stranded Deep>" `
  -Deploy
```

Instead of `-GameRoot`, the environment variable `STRANDED_DEEP_GAME_ROOT` can be used.

## Status

`1.0.0-rc1` is the first public release candidate. It is based on the accepted 0.10.1 runtime line and adds release metadata/licensing plus public-source cleanup; it does not intentionally expand the gameplay-observation surface.

## License

Stranded Deep Diagnostics is licensed under the **MIT License**. See `LICENSE`.

The MIT license applies to this project's own source code. Stranded Deep, Unity, BepInEx, Harmony/HarmonyX, Rewired, and other third-party components remain subject to their respective licenses.

Stranded Deep Diagnostics is an unofficial third-party developer tool and is not affiliated with or endorsed by Beam Team Games.
