# Shooter PureState Multi-Sample Blocks

## Goal

The mass-battle PureState profile publishes every three server frames. A
single endpoint transform per push is sufficient for linear interpolation, but
it discards the two intermediate simulation poses. Curved movement, avoidance,
and rapid direction changes therefore still look like 10 Hz motion even when
the Unity render loop runs at 30 or 60 Hz.

This stage adds an explicit comparison mode in which one authoritative push
carries the missing server-frame transform samples. The client applies the
latest authoritative state once, then plays the transform samples across the
server frame timeline. Controlled-player prediction, state hashes, lifecycle,
health, score, acknowledgements, and recovery remain on the existing authority
path.

## Comparison templates

Both templates use the same 30 Hz simulation, three-frame push cadence, AOI,
LOD intervals, active budget, and adaptive playback policy.

| Template | Payload presentation | Purpose |
|---|---|---|
| `mass-battle-lod-aoi` | Current authoritative sample only | Existing baseline |
| `mass-battle-lod-aoi-sample-block` | Two historical samples plus current authority | Multi-sample comparison |

The second template sets `PlaybackPayloadMode = MultiSampleBlock`,
`SampleBlockFrameCount = 3`, and the mass-battle presentation density policy.
The original template remains unchanged so test runs can compare both paths
without changing the synchronization model.

## Wire format

PureState wire version 2 appends two unmanaged arrays to the version 1
12-member envelope:

```text
FrameSamples[]
  Frame
  ServerTick
  TransformOffset
  TransformCount

TransformSamples[]
  EntityId
  EntityKind
  QuantizedX / QuantizedY
  QuantizedVelocityX / QuantizedVelocityY
  Flags
```

Transforms are flattened instead of stored as an array inside every frame.
This allows the custom decoder to retain two capacity-backed arrays and copy
unmanaged spans directly. After warmup, decoding a stable sample block does not
allocate. Frame descriptors must be strictly increasing, must not exceed the
authoritative frame, and must reference valid non-overlapping transform ranges.

The reusable decoder accepts both layouts:

- 14 members: version 2 authority plus presentation samples;
- 12 members: version 1 payload, decoded with empty sample arrays;
- 11 members and the old six-field settings layout: existing legacy fallback.

Historical samples are presentation-only. They never enter the PureState
baseline/hash controller and cannot spawn, despawn, acknowledge commands, or
restore authority.

## Server sampling path

After every successful battle tick, the runtime exporter copies only the
already quantized transform fields into a reusable transient buffer. It does
not compute a state hash, build a delta, sort candidates, or serialize a full
snapshot.

The Orleans battle session copies that prefix into an eight-slot fixed ring.
At push time it selects the intermediate frames immediately before the current
authority frame and flattens them into reusable output buffers. Observer pushes
apply the observer AOI boundary while flattening. The per-frame ceiling is still
the PureState active synchronization budget, but the mass-battle template also
caps the complete historical block at 32 transforms. This second limit is
independent of the number of units and prevents two historical frames from each
consuming the full active budget. The template then selects presentation
samples by distance from the observer:

- near, at or below `visibleRadius * 0.40`: every historical frame;
- mid, at or below `visibleRadius * 0.75`: every second historical slot,
  anchored at the newest historical frame;
- far and AOI-boundary retention: no historical transform; the current
  authoritative envelope remains the keyframe.

The observer-controlled player is omitted from historical samples because the
client already predicts it. Without a valid observer AOI, every entity is
treated as near; the server never invents a distance from the world origin.
Selection scans near, then mid, then far, so an active-budget cap cannot let an
earlier far entity displace a near entity. Empty frame descriptors remain on
the timeline when density removes every transform from a selected frame. The
remaining block budget is divided fairly across the remaining historical frame
descriptors; unused capacity from an older frame is available to a newer frame.
The full-density policy retains an unlimited block cap for protocol comparison
tests and is not used by the mass-battle template.

Fail-closed AOI pushes carry no historical samples. Starting, disposing, or
changing a battle session clears the ring so samples cannot cross worlds.

This design adds one O(N) quantized transform copy per simulation frame when
the sample-block template is enabled. Single-sample templates do not execute
the capture path. The cost is deliberate: intermediate poses have to be
sampled somewhere, but the path avoids hash, delta, dictionary, nested-array,
and serialization work until a push is actually produced.

## Client playback

For an accepted PureState payload the client performs these operations in
order:

1. Apply the current authoritative envelope through the existing PureState
   controller.
2. Update lifecycle suppression from the current authoritative batch.
3. Publish valid historical transform-only batches in ascending frame order.
4. Publish the current authoritative transform batch.
5. Advance the existing adaptive playback clock on render ticks.

The controlled player is filtered from every historical sample and continues
to use immediate prediction. A current despawn suppresses older transforms in
the same block, so delayed playback cannot recreate an entity. Duplicate,
stale, malformed, or backward samples are rejected without rewinding the
playback clock.

Pooled transform lists own samples while they are retained by
`ShooterSnapshotStream`; eviction and reset return them to the existing list
pool. This avoids retaining references to the decoder's transient arrays after
the next network packet overwrites them.

## Diagnostics and acceptance

`ShooterPureStatePlaybackDiagnostics` and the multiplayer headless JSON now
include:

- `ReceivedSampleBlockCount`;
- `ReceivedFrameSampleCount`;
- `RejectedFrameSampleCount`;
- `AverageFrameSamplesPerBlock`;
- `ReceivedTransformSampleCount`;
- `MaxTransformSampleCountPerBlock`;
- `ReceivedAuthoritativeTransformCount`;
- `AverageTransformSamplesPerFrame`;
- `HistoricalTransformAmplificationRatio`.

Compare these beside buffered frame span, playback lead, starvation ratio,
held-playback ratio, payload bytes, queue wait, presentation time, view render
time, and GC bytes. A healthy three-frame block run should approach two
historical samples per received block after startup, have a zero rejection
count, and reduce starvation/held ratios relative to the single-sample
template under the same network condition.

Automated coverage includes:

- version 2 owned and reusable round trips;
- allocation-free reusable frame-sample decode after warmup;
- version 1 12-member compatibility;
- historical frame order and flattened offsets;
- AOI filtering and world reset of the server ring;
- near/mid/far density, observer-player omission, and near-first budget use;
- 1,200-unit steady-state sampling without allocation after warmup;
- a deterministic 1,000-unit payload comparison between single sample, full
  multi-sample, and density-limited multi-sample payloads;
- explicit single-sample versus sample-block template selection;
- client playback of frames 1, 2, and 3 across render ticks;
- duplicate historical sample rejection without time reversal.

The 1,000-unit protocol gate uses a fixed 40/30/30 near/mid/far distribution.
For two historical frames it expects 2,000 transforms in the full-density
block and 32 in the mass-battle density block. It also requires the complete
density-block payload, including the same 1,000 current authoritative entity
deltas, to be no more than 80% of the full-density payload and the density
sample count to remain below 5% of full density. This is a stable wire-size
assertion, not a wall-clock benchmark.

See [19-PureState Sample Density and A/B Validation](19-PureStateSampleDensityAndABValidation.md)
for the policy contract, metrics, and acceptance matrix.

## Remaining work

The paired Headless runner and artifact comparator are now available, and the
first real 1K/2K runs are recorded in
[19-PureState Sample Density and A/B Validation](19-PureStateSampleDensityAndABValidation.md).
Those single repetitions establish a diagnostic baseline but still do not
establish profile-specific production limits. At least three repetitions per
target topology are required before promoting payload, starvation, held
playback, reconciliation movement, p95/p99 arrival gaps, recovery, and GC
thresholds to a performance gate.

The next synchronization-model comparison remains deterministic frame sync.
It should be exposed as a separate Shooter template and validated for RVO/job
determinism before checkpoint, input WAL, reconnect reconstruction, and
zero-client server takeover are treated as production-capable behavior.
