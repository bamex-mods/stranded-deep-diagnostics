# Architecture

## Responsibility boundary

```text
Stranded Deep / Unity runtime
        ↓
controlled observation
        ↓
plain snapshot / event data
        ↓
overlay + history + diff + report
```

Historical snapshots should not retain live Unity object references longer than required for inspection.

## Identity domains

Diagnostics treats these as separate domains:

```text
Runtime identity
  InstanceID / type / hierarchy / scene / epochs

Persistent identity
  ReferenceId / MiniGuid / PrefabId / CraftingType when empirically valid

Network identity
  Bolt entity / attached state / ownership / NetworkId
```

`InstanceID` is never treated as cross-session persistence identity.

## Epochs

The core tracks process, world, and scene epochs. Scene/world transitions invalidate scoped caches and disable targeted instrumentation/recorders that should not survive the transition.

## Reflection policy

Default policy:

```text
MetadataOnly
FieldsSafe
PropertiesAllowlisted
```

Generic inspection must not call arbitrary property getters, arbitrary methods, or unknown `ToString()` implementations for presentation.

## Split-screen

Player identity is not inferred from `Camera.main` or one global controller index. Diagnostics resolves player/camera/input relationships per local player and keeps P1/P2 context explicit.

## Harmony

Harmony is an instrumentation mechanism, not the primary scheduler. The plugin uses its own Unity callbacks for normal observation.

Targeted trace rules:

- opt-in;
- explicit runtime method discovery;
- Prefix/Postfix only where practical;
- no result/argument/control-flow mutation in the stable diagnostics line;
- unique Harmony IDs;
- cleanup on disable/lifecycle teardown.

## Adapters

Core game diagnostics must remain usable without any BamEx gameplay mod.

Optional adapters are one-way:

```text
Diagnostics → inspect optional mod
```

Never:

```text
production mod → depend on Diagnostics
```

Current BamEx-specific adapter source is isolated under `Adapters/BamEx/`.

## Capability degradation

Version-sensitive features must fail closed at the feature level:

```text
AVAILABLE
PARTIAL
UNAVAILABLE
```

A changed/missing signature should disable or reduce the affected capability rather than causing a startup failure for unrelated modules.
