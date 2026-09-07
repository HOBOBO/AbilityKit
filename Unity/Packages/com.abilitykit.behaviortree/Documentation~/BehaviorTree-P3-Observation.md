# Behavior Tree P3 Observation And Offline Debugging

## Scope

P3 extends the existing V2 observation/controller/timeline/layout work without changing runtime behavior tree semantics. The editor remains a pull-based observer over `IBtTreeDebugView`; runtime trees do not know about windows, recorders, or replay UI.

## Sampling Bounds

`BtObservationSettings` owns shared defaults and limits:

- Default sample interval: `0.2` seconds.
- Allowed sample interval: `0.01` to `10` seconds.
- Default timeline capacity: `200` samples.
- Allowed timeline capacity: `1` to `10000` samples.

`BtObservationController.SampleIntervalSeconds` and `TimelineCapacity` can be changed at runtime. Invalid non-positive values throw; over-large values are clamped to keep editor memory growth bounded.

## Recording DTO

Observation recordings serialize through stable DTO classes rather than runtime/editor object graphs:

- `BtObservationRecordingDto`
- `BtObservationSnapshotDto`
- `BtObservationNodeDto`
- `BtObservationBlackboardDto`
- `BtObservationStringMapEntryDto`

The DTO stores enum values as integers, maps as sorted key/value arrays, and blackboard values as parallel primitive arrays. This keeps the JSON shape deterministic and avoids relying on dictionary serialization.

## Core APIs

Use `BtObservationRecording.ToJson`, `TimelineFromJson`, `SnapshotToJson`, `SnapshotFromJson`, `ExportToFile`, and `ImportTimelineFromFile` for capture exchange. Use `BtObservationOfflineReplay` for offline navigation over imported sessions via `Seek`, `StepPrevious`, `StepNext`, and `JumpToLatest`.

The observation window exposes this with toolbar entries:

- `Interval` and `Capacity` fields for live sampling settings.
- `Export Recording` for timeline JSON export.
- `Import Replay` for offline loading.
- `Previous`, `Next`, and `Latest` controls while viewing a replay.

## Scale Notes

The P3 path reduces repeated state projection by reusing GraphView projection sets and overlay buffers, and by reusing controller registry lists during polling. Snapshots remain immutable deep copies by design so exported recordings and frozen/history views cannot be mutated by later runtime frames.

Performance budget tests cover 100, 500, and 1000 node samples with large blackboard key sets. They record timing metrics through `TestContext` and assert only broad ceilings so the tests catch accidental quadratic regressions without binding CI to tight absolute timings.

## Diagnostics

`BtEditorDiagnostics.AnalyzeObservation` reports observation sample count, capacity, sample interval, disconnected retained-history state, and maximum-capacity risk. These diagnostics are intentionally informational/warning-level because observation settings do not affect exported behavior tree runtime semantics.
