# Stranded Deep Diagnostics — User Guide

This guide applies to **Stranded Deep Diagnostics 1.0.0-rc1**.

Diagnostics is a developer and troubleshooting toolkit for Stranded Deep. It can inspect runtime objects, physics, local split-screen players, cameras, input, UI, storage, world state, construction state and loaded BepInEx plugins. It also supports snapshots, diffs, bounded incident capture and targeted opt-in tracing.

The public line is **inspection-first**. Normal inspection/reporting is not intended to modify game saves, Transform state, Rigidbody state, collision state or gameplay objects.

## 1. Quick start

1. Start Stranded Deep and load a world.
2. Press `F8` to open the Diagnostics overlay.
3. In local split-screen, use `Alt+F8` to select P1 or P2.
4. Press `F9` to cycle modules.
5. Aim the selected player's gameplay camera at the object you want to inspect.
6. Press `F10` if you want to pin that target.
7. Press `F12` to export the current module's report/snapshot/incident marker.
8. Reports are written by default to:

```text
BepInEx/config/StrandedDeepDiagnostics/Reports/
```

The `Reports` directory is created on the first actual report write.

## 2. Controls

```text
F8          Diagnostics ON/OFF
Alt+F8      switch active local split-screen player
F9          next module
F10         pin/unpin current target
F11         module-specific trace/recorder when supported
F12         report / snapshot / incident marker depending on module
Shift+F12   diff or special immediate status report where supported
Ctrl+F12    deep safe-field dump in OBJECT / PHYSICS
```

Turning Diagnostics off disables active traces and AUDIO/RAFT recorders. Scene/world lifecycle changes also disable them.

## 3. Target selection

Diagnostics resolves the selected player's camera rather than relying on global `Camera.main`.

The overlay may show:

```text
RAW#0
PICK
PRIMARY
```

- `RAW#0` is the first physical raycast hit.
- `PICK` is the selected hit after filtering helper/self hits where a more meaningful target exists behind them.
- `PRIMARY` is the semantic inspection object.

`F10` pins the current live target so it can continue to be inspected while you look elsewhere. Press `F10` again to unpin.

Switching P1/P2 with `Alt+F8` clears live and pinned targets.

## 4. Capability states

The active module displays one of:

```text
AVAILABLE
PARTIAL
UNAVAILABLE
```

- `AVAILABLE`: the main runtime types/capabilities were found.
- `PARTIAL`: only part of the capability was found, or some runtime-dependent part is not currently available.
- `UNAVAILABLE`: the required game/runtime type or method was not found.

Optional adapters are allowed to be unavailable without disabling the Diagnostics core.

## 5. Module reference

### OBJECT

General target inspection: hierarchy, components, runtime identity, available saveable identity and bounded safe-field inspection.

- `F12`: snapshot
- `Shift+F12`: snapshot diff; first press creates the baseline
- `Ctrl+F12`: deep safe-field dump

### PHYSICS

Rigidbody/Collider inspection, velocity/angular velocity, constraints, kinematic/gravity state, bounds and collider-to-Rigidbody relationships.

- `F12`: physics snapshot
- `Shift+F12`: snapshot diff
- `Ctrl+F12`: deep safe-field dump

Diagnostics does not call `Physics.SyncTransforms()` or write Rigidbody state.

### CAMERA

Selected P1/P2 camera report, including viewport and camera hierarchy/state.

- `F12`: camera report

### INPUT

Selected player's Rewired/controller/joystick context and available input state.

- `F12`: input report

### UI

Unity Canvas/UI census, including split-screen UI context.

- `F12`: UI Canvas report

### INTERACTION

Interaction state for the selected target.

- `F11`: targeted interaction trace
- `F12`: interaction report
- `Shift+F12`: text report diff

### SAVEABLE

Saveable/native identity inspection.

- `F11`: targeted saveable trace
- `F12`: saveable report
- `Shift+F12`: text diff

### STORAGE

`SlotStorage` / `Interactive_STORAGE` inspection where available.

- `F11`: targeted storage trace
- `F12`: storage report
- `Shift+F12`: text diff

### INVENTORY

Player holder/inventory context and selected target inspection.

- `F11`: targeted inventory trace
- `F12`: inventory report
- `Shift+F12`: text diff

### WORLD

World/zone runtime state.

- `F11`: targeted world trace
- `F12`: world/zones report
- `Shift+F12`: text diff

### CONSTRUCTION

Construction and raft-related construction state.

- `F11`: targeted construction trace
- `F12`: construction report
- `Shift+F12`: text diff

### AUDIO

Unity audio diagnostics plus an optional BamEx Boombox adapter.

When `SplitStereoProcessor` is available, `F11` enables dedicated numeric DSP telemetry around `OnAudioFilterRead` without normal logging/reflection inside the audio callback.

`F12` marks an incident. Diagnostics keeps roughly five seconds before the marker and five seconds after it, then automatically writes an `audio-incident` report.

Typical workflow:

```text
AUDIO
→ F11
→ reproduce the audible issue
→ press F12 when it happens
→ wait about 5 seconds
→ inspect the newest audio-incident report
```

### RAFT

Composite raft diagnostics using shared object/physics/construction/event infrastructure.

- `F11`: enable/disable read-only raft recorder
- `F12`: incident marker with roughly 5 seconds before + 5 seconds after
- `Shift+F12`: immediate current RAFT status report

Evidence flags can include:

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

These are observations, not automatic root-cause verdicts.

### TRACE

General targeted Harmony trace event view.

- `F11`: toggle TRACE profile
- `F12`: export trace events
- `Shift+F12`: text diff

### PLUGINS

Loaded BepInEx plugins and optional adapter status.

- `F12`: plugin/adapters report
- `Shift+F12`: text diff

## 6. Where F11 is supported

Normal targeted trace profiles are available for:

```text
INTERACTION
SAVEABLE
STORAGE
INVENTORY
WORLD
CONSTRUCTION
TRACE
```

`AUDIO` and `RAFT` have their own specialized F11 behavior.

For modules such as `OBJECT`, `PHYSICS`, `CAMERA`, `INPUT`, `UI` and `PLUGINS`, F11 may report:

```text
No targeted trace profile for <MODULE>
```

That is expected.

## 7. Snapshot/diff workflow

For `OBJECT` and `PHYSICS`:

```text
Shift+F12
→ capture baseline
→ change/wait for state change
→ Shift+F12 again
→ export diff
```

Most text-report modules use the same two-step baseline/diff pattern.

Runtime `InstanceID` is not persistent identity across unload/reload. Use available ReferenceId/saveable identity and surrounding context when investigating persistence.

## 8. Reports and configuration

Default report root:

```text
BepInEx/config/StrandedDeepDiagnostics/Reports/
```

Each runtime session creates a folder similar to:

```text
<timestamp>-v1.0.0-rc1/
```

`session.txt` contains the capability manifest.

The BepInEx config is:

```text
BepInEx/config/com.bamex.strandeddeep.diagnostics.cfg
```

Relevant entries:

```ini
[Input]
ToggleKey = F8

[Inspection]
MaxRayDistance = 100
MaxHierarchyDepth = 32
MaxFieldsPerComponent = 48

[Reports]
ReportRoot =
```

An empty `ReportRoot` uses the portable default.

## 9. Useful workflows

### Placement/construction problem

```text
F8
→ select P1/P2
→ CONSTRUCTION
→ aim at the relevant object/area
→ F12
→ enable F11 targeted trace if more lifecycle detail is needed
```

### Physics/state change

```text
OBJECT or PHYSICS
→ F10 pin
→ Shift+F12 baseline
→ perform the action
→ Shift+F12 diff
```

### Identify P1/P2 controller ownership

```text
Alt+F8 select player
→ INPUT
→ F12
```

### Check loaded plugins/adapters

```text
PLUGINS
→ F12
```

### Unexpected raft movement

```text
RAFT
→ F11 before the issue
→ play normally
→ F12 when the suspicious event occurs
→ wait ~5 seconds
→ inspect raft-incident report
```

### Short audio gap

```text
AUDIO
→ F11 when the DSP adapter is AVAILABLE
→ wait for the issue
→ F12
→ wait ~5 seconds
→ inspect audio-incident report
```

## 10. What to attach to a bug report

Minimum useful bundle:

```text
BepInEx/LogOutput.log
+
newest StrandedDeepDiagnostics Reports session folder
```

For AUDIO/RAFT issues, include the matching incident report when available.

A game save is not required unless specifically requested.

## 11. Troubleshooting

### Overlay does not appear

Confirm BepInEx loaded `StrandedDeepDiagnostics.dll`, then inspect `BepInEx/LogOutput.log`.

### No target / `PRIMARY <none>`

Aim the selected gameplay camera at an object within `MaxRayDistance`. In split-screen, verify the active player with `Alt+F8`.

### Capability is PARTIAL / UNAVAILABLE

Export `PLUGINS` and the affected module report. This can indicate an API/version mismatch, an unavailable optional adapter, or simply a required runtime object that is not present in the current scene.

### Reports directory does not exist yet

It is created on the first actual report write. Press `F12` in a supported module.

### F11 does not arm anything

Not every module has a trace profile. See “Where F11 is supported”.

## 12. Safety boundary

The public line is not intended to:

```text
invoke SaveGame
spawn gameplay objects
write Transform state
write Rigidbody velocity/angularVelocity
change isKinematic/useGravity/constraints
call Physics.SyncTransforms()
change IgnoreCollision state
force placement validity
repair attachments
rewrite ReferenceId values
modify world sidecars
```

If a future investigation requires mutation, it should be implemented as a separate, explicit, opt-in lab feature. `1.0.0-rc1` does not expose such a public mode.

## 13. Compatibility

`1.0.0-rc1` is a release candidate. Runtime capability detection is designed so missing types degrade toward `PARTIAL/UNAVAILABLE` instead of crashing the entire tool where possible.

Stranded Deep internals can still change between game versions. When reporting a compatibility problem, include `session.txt` and `LogOutput.log`.

## 14. Developer documentation

- Architecture: `ARCHITECTURE.md`
- Optional adapters: `ADAPTERS.md`
- Build instructions: `README.md`
- Russian usage guide: `USER_GUIDE_RU.md`

Licensed under the MIT License.
