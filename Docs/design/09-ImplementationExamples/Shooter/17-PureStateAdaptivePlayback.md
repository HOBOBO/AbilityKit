# Shooter PureState Adaptive Playback

## Goal

The thousand-entity profiles intentionally reduce the authoritative push rate.
For example, a 30 Hz battle with `DeltaIntervalFrames = 3` delivers a near-LOD
state block at roughly 10 Hz. Rendering each block immediately makes normal
gateway scheduling jitter visible as a stop-and-jump motion even when the
simulation and transport are otherwise healthy.

This stage improves presentation continuity without changing the wire format.
The controlled player continues to use immediate local prediction. Only remote
PureState transforms are delayed and interpolated.

## Previous limitation

`ShooterClientAuthoritativeInterpolationSyncController` previously clamped the
playback delay to at most one delta interval:

```text
effective delay <= DeltaIntervalFrames
```

At 30 Hz with a three-frame delta interval this left about 100 ms of buffered
playback. One delayed push, gateway queue spike, or long Unity frame exhausted
the buffer and exposed the latest authoritative pose directly.

## Current policy

The PureState playback policy now uses block depth rather than a single push
interval:

```text
base delay = max(server interpolation delay, 2 * delta interval)
starvation delay = max(base delay, 3 * delta interval)
```

For the thousand-entity profile (`30 Hz`, `DeltaIntervalFrames = 3`) this means:

| State | Delay | Time |
|---|---:|---:|
| Normal target | 6 frames | 200 ms |
| Starvation target | 9 frames | 300 ms |

The first state is displayed immediately. Playback then prebuffers until the
configured span is available. This avoids inventing samples before the first
authoritative frame while still establishing the two-block steady-state lead.

When playback catches the newest buffered frame, the target expands to three
blocks immediately. The active delay converges at 12.5% of normal playback
progress, so increasing delay slightly slows remote playback instead of moving
the clock backwards. After two continuous non-starved seconds, the target
returns to two blocks and playback catches up with the same bounded rate.

The playback clock is capped at the newest buffered frame. A long packet gap
therefore holds the last pose instead of advancing far beyond the server data
and remaining stuck after delivery resumes.

## Diagnostics

`ShooterPureStatePlaybackDiagnostics` is available from the authoritative
controller, `ShooterClientSession.TryGetPureStatePlaybackDiagnostics`, and
`ShooterRemoteStateSyncPlayModeHost.PureStatePlaybackDiagnostics`.
The multiplayer headless client also writes these fields into its JSON state
artifact so performance-matrix runs retain the playback evidence.

The snapshot reports:

- received snapshot count and render tick count;
- buffered snapshot count and oldest-to-newest frame span;
- current playback frame and lead behind the newest frame;
- current, target, base, and maximum delay frames;
- starvation and held-playback counts plus their ratios.

These values should be captured beside `LauncherTick`, `SessionTick`,
`PresentationBuild`, `ViewRender`, hitch count, and GC bytes. A rising
starvation ratio points to delivery cadence or main-thread scheduling. Low
starvation with high hitch or GC metrics points to local simulation,
presentation, or rendering cost instead.

## Verification

The automated tests cover:

- two-block minimum delay when the server advertises only one block;
- startup prebuffering without a false starvation event;
- promotion from six to nine frames after starvation;
- two-second recovery hysteresis and gradual return to six frames;
- monotonically increasing playback while either delay converges;
- diagnostics reset when leaving PureState mode;
- local controlled-player prediction remaining on the immediate path;
- bounded steady-state allocation after warmup.

Shooter Runtime result after this stage: `549/549` passing.

## Latency trade-off

This policy exchanges 200-300 ms of remote-state latency for continuous motion.
It does not add input delay to the locally controlled player. Combat authority,
hit confirmation, and remote entity state still arrive from the server, so UI
that communicates authoritative results must use server events rather than the
interpolated visual frame.

The extra delay masks short jitter but cannot reconstruct motion that was never
sampled. At 10 Hz, curved or rapidly changing movement is still described by
widely spaced endpoints.

## Next comparison stages

The recommended order remains:

1. The first multi-sample state block implementation is complete; see
   [18-PureState Multi-Sample Blocks](18-PureStateMultiSampleBlocks.md). The next
   bandwidth pass can vary sample density by near/mid/far LOD after collecting
   comparable payload and starvation evidence.
2. Expose a Shooter deterministic frame-sync profile beside PureState. Reuse the
   existing input history and `BattleFrameSyncGrain`, then close deterministic
   gaps in RVO/jobs and floating-point gameplay before treating it as a valid
   comparison.
3. Add server checkpoints plus an input write-ahead log. Client snapshots may be
   accepted only as validated witnesses, never as untrusted authority.
4. Define zero-client room policy: continue authoritative simulation, enter a
   bounded grace/hibernate state, or terminate at battle completion/TTL. A
   reconnect rebuilds from the latest checkpoint and subsequent inputs.

The acceptance matrix should compare at least immediate state blocks, adaptive
state playback, multi-sample state blocks, and deterministic frame sync under
the same entity count, RTT, jitter, packet loss, Unity frame spikes, and GC
injection profile.
