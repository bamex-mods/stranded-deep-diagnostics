# Optional adapters

Adapters add knowledge about a specific mod or specialized runtime component without making that mod a dependency of the Diagnostics core.

## Rules

1. The adapter is read-only unless a future experimental branch explicitly says otherwise.
2. The target mod must not reference `StrandedDeepDiagnostics.dll`.
3. Absence of the target mod is normal and must not be logged as a fatal error.
4. Discovery should use loaded plugin metadata, exact known runtime types, or bounded reflection.
5. Adapter state must be converted into ordinary report/snapshot data rather than exposing mutable live state outside the inspection step.
6. Any Harmony instrumentation owned by an adapter is opt-in and must use a Diagnostics-owned Harmony ID.

## Current BamEx adapters

```text
Adapters/BamEx/BoomboxAudioAdapter.cs
Adapters/BamEx/RaftFurnitureAdapter.cs
```

### Boombox

Finds the optional `SplitStereoProcessor` and associated `AudioSource` for specialized AUDIO incident telemetry.

### Raft Furniture

Reads follower/reposition/attachment-related components and own-raft collision-ignore state for RAFT reports.

## Adding another adapter

Preferred structure:

```text
Adapters/<VendorOrProject>/<AdapterName>.cs
```

The adapter should expose small inspection/report functions and should not duplicate the generic OBJECT/PHYSICS/SAVEABLE/CONSTRUCTION engines.
