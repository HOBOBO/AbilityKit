# Shooter PureState Sample Density and A/B Validation

## Scope

PureState has two independent density decisions. The authoritative exporter
decides when gameplay state is synchronized by near/mid/far LOD cadence. The
sample-block ring decides how many transform-only presentation samples are
added between authoritative pushes. This stage changes only the second
decision. Spawns, despawns, health, score, ownership, hashes, baselines,
acknowledgements, and recovery remain unchanged.

## Mass-battle policy

`mass-battle-lod-aoi-sample-block` keeps the same 30 Hz simulation, three-frame
push cadence, PureState settings, AOI radii, active budget, and network profile
as `mass-battle-lod-aoi`. Its only differences are the three-frame presentation
block and this density policy:

| Tier | Observer distance | Historical cadence | Current authority |
|---|---:|---:|---:|
| Near | `<= visibleRadius * 0.40` | Every historical frame | Unchanged |
| Mid | `<= visibleRadius * 0.75` | Newest, then every second historical slot | Unchanged |
| Far/boundary | Remaining AOI boundary | None | Unchanged |

The newest historical frame is the stride anchor. With authority frame 3 and
historical frames 1 and 2, near sends frames 1 and 2, mid sends frame 2, and
far sends neither. Velocity remains in each selected transform so sparse mid
and far tracks retain extrapolation information.

AOI is evaluated before density. A transform outside the observer boundary is
never emitted. The observer-controlled player is also omitted because local
prediction owns its presentation. When no observer scope exists, all visible
entities are treated as near rather than measured from an arbitrary origin.

## Budget behavior

`ActiveSyncBudget` remains the per-frame safety ceiling, but it is not a safe
sample-block budget for a mass battle. If two historical frames can each spend
that budget, one push may carry roughly twice the current authoritative budget
in presentation-only transforms. The initial Headless runs demonstrated that
this increases packet size and queue pressure enough to trigger sequence gaps,
resync requests, and more full baselines.

The mass-battle policy therefore applies an additional hard limit:

```text
MaxHistoricalTransformsPerBlock = 32
```

For a three-frame block there are two historical frame descriptors. The ring
divides the remaining block budget by the number of remaining descriptors,
rounding up. With 32 available transforms both frames initially receive 16. If
the older frame uses fewer than 16, its unused capacity transfers to the newer
frame. The sum can never exceed 32, even when `ActiveSyncBudget` is 2,048.
`FullDensity` keeps `int.MaxValue` so protocol tests can still compare an
unbounded reference payload; the production mass-battle template uses 32.

Inside each frame, selection performs stable near, mid, and far passes over the
captured transform order. This gives near entities first access to the frame's
share without sorting, dictionaries, or per-push allocation. Frame descriptors
stay in ascending order and keep valid flat offsets, including a zero transform
count when a selected timeline frame has no eligible samples.

A finite block budget also rotates the starting transform across authority
blocks. Block 0 starts at capture index 0, block 1 advances by the 32-transform
budget, and the index wraps by the captured count. Both historical frames in a
three-frame block use the same start, so their samples describe the same entity
window rather than two unrelated subsets. This removes permanent first-32
monopolization while keeping tier priority, determinism, the hard budget, and
the zero-allocation steady state. `FullDensity` does not rotate and therefore
retains its original protocol-reference order.

`ShooterPureStateFrameSampleDiagnostics` records eligible and selected counts
per tier, observer-controlled omissions, rejected transforms, total selected
transforms, and the selection ratio. These diagnostics are returned directly
by the ring for deterministic tests and profiling; they are not added to the
wire format.

## A/B evidence

The deterministic protocol test constructs 1,000 authoritative units with a
fixed 40/30/30 near/mid/far distribution and compares:

| Variant | Historical transforms for two frames | Required ordering |
|---|---:|---:|
| Single sample | 0 | Smallest |
| Mass-battle density block | 32 | Between single and full |
| Full-density block | 2,000 | Largest |

The density payload must be at most 80% of the full-density payload while both
contain the same 1,000 current authoritative deltas, and its historical sample
count must be below 5% of the full-density count. A separate 1,200-unit test
warms the ring and then requires less than 256 bytes of thread allocation over
64 capture/attach cycles. These tests deliberately avoid timing assertions,
which are unstable on shared CI workers.

## Headless metrics

Use the two templates under the same client count, unit distribution, duration,
and network condition. Compare these JSON fields together:

- `battlePushPayloadBytes`, `battlePushMaxPayloadBytes`, and
  `battlePushAveragePayloadBytes`;
- `pureStatePlaybackReceivedSampleBlockCount`;
- `pureStatePlaybackReceivedFrameSampleCount`;
- `pureStatePlaybackReceivedTransformSampleCount`;
- `pureStatePlaybackMaxTransformSampleCountPerBlock`;
- `pureStatePlaybackReceivedAuthoritativeTransformCount`;
- `pureStatePlaybackAverageTransformSamplesPerFrame`;
- `pureStatePlaybackHistoricalTransformAmplificationRatio`;
- `pureStatePlaybackStaleFrameSampleCount` and
  `pureStatePlaybackInvalidFrameSampleCount`;
- `snapshotResyncNeededCount` and `pureStateFullAppliedCount`;
- starvation and held-playback ratios;
- p95/p99 arrival gap, source age, queue wait, and apply time;
- movement reconciliation and GC metrics.

A useful result must show both sides of the tradeoff. Lower starvation with an
unbounded payload increase is not acceptable, and lower bytes with repeated
held playback is not a continuity improvement. Thresholds should be promoted
to a gate only after repeated runs on the target topology and hardware.

## Paired headless runner

`tools/run_shooter_pure_state_sample_block_ab.ps1` runs every case in a fixed
baseline-then-candidate order. This keeps the enemy budget, network
environment, sync model, view backend, and repetition aligned while limiting
compile warmup to the first run. Start by inspecting the matrix without
launching Unity:

```powershell
& tools/run_shooter_pure_state_sample_block_ab.ps1 `
  -EnemyBudgets 1000,2000 `
  -NetworkEnvironments ideal,limitedbw `
  -Repetitions 3 `
  -PlanOnly
```

Run the same matrix without `-PlanOnly` when the Gateway is available and both
dedicated Headless Unity projects are free:

```powershell
& tools/run_shooter_pure_state_sample_block_ab.ps1 `
  -OwnerProject <owner-project> `
  -MemberProject <member-project> `
  -EnemyBudgets 1000,2000 `
  -NetworkEnvironments ideal,limitedbw `
  -Repetitions 3
```

Each case stores the original owner/member Headless results and a
`comparison.json`. The run root stores `ab-summary.json`; its `groups` array
reports medians by enemy budget and network environment for payload
amplification, starvation, held playback, p99 arrival gap, unexplained
backward movement, GC delta, resync-needed count, full-snapshot applications,
and coalesced automatic recovery triggers. A zero-valued single-run median is
preserved as `0`, not serialized as `null`. Only comparisons that pass the
artifact contract enter those medians. Every failed, missing, or mismatched
case still makes the complete run fail.

The comparator is fail-closed about experiment identity. Both sides must use
the expected template, and sync model, network environment, enemy budget, and
view backend must match. The baseline must contain no sample block or
historical transform, while the candidate must contain both and reject zero
invalid frame samples. The candidate must also report a positive
`pureStatePlaybackMaxTransformSampleCountPerBlock` no greater than 32. This
prevents a successful process exit, an old artifact without the block-budget
field, or a stale result file from being mistaken for a valid A/B pair.

Stale and invalid samples are intentionally separated. A stale sample is a
well-formed sample that arrived after its playback point or duplicates already
accepted time; it is rejected locally but does not prove a protocol defect. An
invalid sample has malformed offsets, counts, ordering, or another structural
violation and fails the artifact contract. `RejectedFrameSampleCount` remains
the total, while stale and invalid counts explain its cause.

By default the runner is report-only: artifact contracts are enforced, but
performance thresholds are not. Add `-EnableGate` only after the target
hardware and topology have enough repeated evidence. The initial configurable
limits are 1.75x average payload amplification, +0.02 starvation, +0.02 held
playback, +50 ms p99 arrival gap, +0.10 unexplained backward movement, and
+65,536 average GC bytes per frame, plus at most four additional resync-needed
events and four additional full snapshots. The 32-transform block maximum is
always an artifact contract, not an optional performance threshold. All
performance limits are provisional guardrails rather than demonstrated
production budgets.

`tools/compare_shooter_pure_state_sample_block_ab.ps1` can also compare one
existing owner/member pair directly. This is useful for inspecting a failed
case without relaunching Unity.

## Real Headless evidence (2026-08-20)

The first real matrix used dedicated owner/member Headless projects against the
same local Orleans/Gateway stack. Every row is one repetition, so the numbers
are diagnostic evidence rather than stable percentiles.

Before the block-level limit, historical density scaled with unit count and
link behavior instead of staying bounded:

| Enemies / network | Payload amplification | Starvation delta | Held delta | Historical transforms / received blocks |
|---|---:|---:|---:|---:|
| 1,000 / ideal | `2.250784x` | `+1.7520 pp` | `+1.7681 pp` | `9,943 / 125` |
| 1,000 / limitedbw | `2.728976x` | `+0.6537 pp` | `+1.2371 pp` | `9,365 / 99` |
| 2,000 / ideal | `3.096309x` | `+3.9275 pp` | `+3.8290 pp` | `9,586 / 95` |
| 2,000 / limitedbw | `1.894878x` | `-0.9897 pp` | `-0.7692 pp` | `13,751 / 94` |

Per-client blocks in the worst 2,000/limited-bandwidth run carried about 141 to
151 historical transforms. Large pushes were more likely to be superseded or
dropped, which widened sequence gaps and requested new full baselines. The
sample block then amplified the recovery traffic it had helped trigger.

After applying the 32-transform limit, representative reruns were:

| Enemies / network | Payload amplification | Starvation delta | Held delta | Historical transforms / received blocks |
|---|---:|---:|---:|---:|
| 1,000 / ideal | `1.204224x` | `+2.3391 pp` | `+2.4122 pp` | `1,411 / 102` |
| 2,000 / limitedbw | `1.209429x` | `-1.6335 pp` | `-1.7384 pp` | `1,293 / 95` |

The latest 1,000/ideal artifact also contains the strengthened artifact fields:

```text
artifacts/shooter-pure-state-sample-block-ab/
  20260820-headless-ab-budget32-contract-1000-ideal
```

It passed every structural contract. Both clients reported a maximum of 32
transforms per block, aggregate invalid samples were zero, average payload
amplification was `1.698963x`, starvation changed by `-6.7175 pp`, held
playback by `-6.1591 pp`, and p99 apply time by `-55.5 ms`. However, p99 arrival
gap regressed by `+673 ms`, resync-needed count increased by 4, and full
snapshots increased by 10. The run was report-only, so its serialized
`gatePassed=true` means no performance assertions were requested; it must not
be read as a production performance pass.

Applying the current optional gate to that artifact fails exactly the p99
arrival-gap and full-snapshot assertions. The gated comparison is stored beside
the original case as `comparison-gated.json`. That is useful evidence, not a
reason to widen the limits: it identifies the sequence-gap/full-baseline
feedback path as the next bottleneck. The variation between the two 1,000/ideal
runs also confirms that at least three repetitions per matrix cell are needed
before medians can support a production gate.

The next client-side recovery pass now coalesces automatic FullBaseline
requests by recovery episode. Once a request is accepted, changing gap
frame/hash values do not emit another request while the same recovery remains
unresolved. A successfully applied baseline clears the episode; if no baseline
arrives, a five-second timeout permits a retry. Explicit initial-baseline calls
retain their previous exact-request semantics. Headless output exposes
`automaticFullStateSyncCoalescedRequestCount`, and the A/B comparator reports
its aggregate and delta so a later run can distinguish repeated gap detection
from actual recovery traffic. The 2026-08-20 artifacts predate this field and
therefore do not prove its E4 effect.

## Real Headless evidence (2026-08-21)

The first fresh 1,000/ideal pair after recovery-episode coalescing is stored at:

```text
artifacts/shooter-pure-state-sample-block-ab/
  20260821-headless-ab-recovery-coalescing-1000-ideal
```

Its structural contract passed: the candidate received 113 blocks and 1,604
historical transforms, never exceeded 32 transforms per block, and rejected
zero structurally invalid samples. Three well-formed samples were stale. The
paired deltas were:

| Metric | Candidate delta |
|---|---:|
| Average payload amplification | `1.032244x` |
| Starvation | `+2.4962 pp` |
| Held playback | `+2.3542 pp` |
| p99 arrival gap | `0 ms` |
| p99 queue wait | `+75.5 ms` |
| p99 apply time | `+22 ms` |
| Resync-needed events | `-7` |
| Applied FullBaseline snapshots | `-4` |
| Coalesced automatic recovery triggers | `-256` (`813` to `557`) |

Applying the optional gate fails only starvation and held playback, both just
above the provisional +2 percentage-point limit. Payload, arrival gap,
reconciliation, GC, resync, and FullBaseline gates pass. Compared with the
2026-08-20 diagnostic, the earlier +673 ms arrival-gap and +10 FullBaseline
failures are absent, but this remains one repetition and cannot establish
causality or a production threshold. The baseline itself reported a 2,048.5 ms
p99 arrival gap, so absolute continuity is still poor even though the paired
arrival-gap delta is zero.

This artifact predates cross-block entity-window rotation. A new run is needed
to verify the fairness change; the rotation is covered separately by a
deterministic 1,000-unit test that checks block windows `1..16` and `33..48`.

### Rotation, drain, and controlled-player correction follow-up

Cross-block rotation was exercised by a fresh 1,000/ideal pair at:

```text
artifacts/shooter-pure-state-sample-block-ab/
  20260821-headless-ab-entity-window-rotation-1000-ideal
```

Payload amplification was `0.908142x`, starvation improved by `7.3786 pp`, and
held playback improved by `8.0965 pp`, but p99 queue wait regressed by
`1,355.5 ms` and p99 apply time by `1,146.5 ms`. The older optional gate passed
this run because it did not gate queue wait or apply time. Both metrics are now
first-class optional gates with a default maximum increase of 50 ms.

The attempted three-repetition follow-up at
`20260821-headless-ab-rotation-1000-ideal-r3` produced only one valid paired
comparison. One candidate caught a real movement assertion
(`maxUnexplained=0.833`), one pair completed, and the third owner was lost to a
concurrent `Unity.ILPP.Trigger.exe` APPCRASH. It is not three-run median
evidence and must not be summarized as such.

The next diagnostic pair is stored at:

```text
artifacts/shooter-pure-state-sample-block-ab/
  20260821-headless-ab-drain-and-tug-diagnostics-1000-ideal
```

It passed the structural contract and all current optional gates. Candidate
deltas included payload `1.128442x`, starvation `-4.2521 pp`, held playback
`-4.3117 pp`, p99 arrival gap `-27.5 ms`, p99 queue wait `-874.5 ms`, and p99
apply time `-945 ms`. Baseline processed/enqueued counts were `198/198` and
candidate counts were `204/204`; neither side coalesced a snapshot. Their
budget-limited drain ratios were `0.000897` and `0.000446`. Sustained client
drain throughput is therefore not the explanation for the observed second-long
spikes. Peak queue depth instead coincided with large Editor update gaps, which
points to an intermittent client main-thread stall.

Movement diagnostics also showed that ordinary backward samples arrived with
an advancing PureState authority frame, an empty queue, and no resync request.
Code inspection found that the controlled-player runtime could be tens of
frames away from the authority timeline under 1,000-unit load. The old
reconciliation treated the entire position difference as drift, corrected up
to 0.25 units for every snapshot, and could apply several corrections in one
network drain. This produced the visible authority sawtooth even though
historical transform samples explicitly exclude the controlled player.

The controlled-player policy now:

- treats distance reachable across the absolute client/authority frame gap as
  valid prediction uncertainty, after subtracting pending frames already
  replayed onto the authority pose;
- corrects only error outside that envelope and shares a 0.25-unit correction
  budget across all snapshots applied in one client simulation frame;
- clears pending inputs on a world transition and preserves a hard reset only
  for that transition;
- does not hard-snap same-world FullBaseline or authority-override recovery
  traffic.

The last rule prevents replayed recovery input from placing the player outside
the circular arena. A failed diagnostic run at
`20260821-headless-controlled-reconciliation-1000-ideal-i2` captured the old
chain directly: a replay snap put the member at progress `40.5`, then the next
gameplay tick clamped it to the arena radius `34`, producing a 6.5-unit backward
event without an advancing authority frame. The same run also recorded
0.33-3.5-unit same-world recovery corrections, so it is failure evidence, not
validation of the revised policy.

Six reconciliation-policy EditMode tests passed for the first revision,
covering prediction preservation, excess drift, pending-input replay, the
per-frame budget, small-error tolerance, and forced world reset. The subsequent
absolute-frame-gap revision compiled the Shooter runtime assembly; its added
client-behind test and the next real Headless rerun are currently blocked by
unrelated workspace Moba test compile errors in
`NetworkTransportAuthenticationGateTests.cs` (calls to
`InputSubmissionDiagnosticsBinding.Bind` are missing the new `scope`
argument). That blocker must be cleared before this section can claim an E4
improvement.

The Headless warmup runner also now opens completed Unity logs with
`FileShare.ReadWrite | FileShare.Delete` and retries for up to 30 seconds. This
addresses a real false infrastructure failure where Unity exited successfully
but a child process retained the log handle beyond the previous ten-second
`ReadAllText` window.

### Authoritative server clock and stage diagnostics

`BattleLogicHostGrain` now records a fixed-memory 150-tick performance window
for both timer-driven state sync and externally driven frame sync. It reports
the negotiated and achieved tick rate together with fixed-bucket
P50/P95/P99/max timings for:

- timer interval and total battle callback;
- input submission and authoritative world tick;
- reliable-event capture;
- per-observer snapshot build, gameplay serialization, and enqueue;
- asynchronous observer delivery latency.

The same window counts snapshots queued behind an in-flight delivery. That
counter separates server-side supersession pressure from client queue wait.
Each completed window emits one structured
`[BattleLogicHost] ServerPerformance` log entry containing battle/template,
frame range, observer count, all stage distributions, `TargetHz`, and
`AchievedHz`. The per-tick path stores no samples or strings; it only updates
fixed histogram arrays.

The Headless, A/B, and performance-matrix runners accept an optional
`-ServerLogPath`. They snapshot the file offset before launching clients and
always export only newly appended `ServerPerformance` entries to
`server-performance.log` in the case artifact, including failed cases. This
keeps authority-clock evidence beside the owner/member client metrics without
copying or scanning the server's complete historical log.

Four focused .NET tests pass for rate arithmetic, percentile buckets, window
reset semantics, and a 1,000-iteration zero-allocation recording gate. This is
implementation-level validation only. The currently running server processes
predate this build, and the Unity compiler blocker above prevents a fresh
1,000/2,000-unit run, so no real server timing numbers are claimed yet.

The full Grains regression exposed and then verified two adjacent transport
defects. First, `ShooterPureStateFrameSampleRing` replaced its transform array
when growing from the first 16-sample historical frame to the second one but
did not copy the existing prefix. The older frame therefore contained 16
zero-valued transforms even though frame counts and block structure remained
valid. Buffer growth now preserves the populated prefix. Second, an oversized
FullBaseline was admitted against the configured burst size but deducted its
entire payload size from the token bucket, creating bandwidth debt larger than
the burst and unnecessarily holding later snapshots. It now consumes at most
the same burst budget used by admission. The stale room-flow assertion was
also changed from an obsolete fixed total entity count to player/enemy chunk
invariants. The focused set passes 24/24 and the complete Grains suite passes
259/259 after these fixes.

## Evidence boundary

`-PlanOnly` proves only matrix construction. PowerShell contract tests use
synthetic JSON to prove fail-closed validation and metric arithmetic. Protocol
tests prove deterministic selection, wire size, compatibility, and steady-state
allocation behavior. None of these is a real multiplayer performance pass.

A Headless E4 result can be claimed only from fresh baseline and candidate
artifacts produced by the paired runner on the declared machine and network
profile. The artifacts above satisfy that boundary for their exact one-run
configurations. They do not establish hardware-independent limits, a three-run
median, a production performance gate, or behavior under a remote deployment.

## Recommended next optimization

Do not simply raise the 32-transform cap. The next iteration should:

1. clear the unrelated Unity compilation blocker, rerun the revised controlled
   correction policy at 1,000/ideal, and require raw and unexplained backward
   events to fall rather than relaxing their thresholds;
2. deploy the new server battle-clock instrumentation and capture its achieved
   tick rate and per-stage histograms under 1,000/2,000 units. A client
   prediction clock cannot remain smooth indefinitely if the authority world
   itself cannot sustain the negotiated tick rate;
3. distinguish server production stalls, transport supersession, client
   queueing, and Editor update stalls in every p99 queue/apply outlier;
4. measure per-entity sample age after cross-block rotation, not only aggregate
   block counts, and derive any adaptive sample budget from that fairness data;
5. only after a clean single pair, rerun three repetitions for 1,000/2,000
   units under ideal and limited-bandwidth profiles and gate grouped medians.

## 2026-08-21 E4 follow-up: battle clock and observer budget

The first real 1,000-unit run exposed two independent causes of apparent client stutter:

1. Orleans `RegisterTimer` schedules the next callback only after the async callback completes. A 30 Hz period therefore became `16.05-17.63 Hz` when the world tick took `15-20 ms`. `BattleLogicHostGrain` now advances a monotonic absolute deadline with a one-shot timer and skips expired deadlines instead of accumulating callback time.
2. `StateSyncObserverGrain` applied its default `128 Kbps` queue budget even when the case requested `ideal`. This duplicated transport conditioning, dropped/merged pure-state deltas, and caused repeated full-baseline requests. `StateSyncPush.NetworkEnvironmentId` now selects an unlimited observer queue for `ideal`, `lan`, `mobile4g`, `crossregion`, and `poorwifi`; `limitedbw` keeps the explicit 128 Kbps budget.

Fresh artifacts:

```text
artifacts/shooter-controlled-reconciliation-e4-1000-ideal/20260821-053810-231
artifacts/shooter-controlled-reconciliation-e4-1000-ideal-deadline/20260821-055416-394
artifacts/shooter-controlled-reconciliation-e4-1000-ideal-budget/20260821-060835-244
artifacts/shooter-controlled-reconciliation-e4-1000-ideal-budget-repeat/20260821-061213-514
artifacts/shooter-controlled-reconciliation-e4-1000-ideal-budget-repeat/20260821-061334-265
```

The fixed-budget correction reduced the maximum snapshot arrival gap from `2.01 s` to `0.24-0.55 s`, reduced full baselines to one per observer in the repeat runs, and reduced pure-state playback starvation from roughly `54-56%` to `16-20%`. All three budget-corrected runs passed the structural AOI acceptance with `0 B/frame` reported GC. The server windows still report only `23-24 Hz` under this shared development machine; the remaining gap is a server scheduling/WorldTick capacity issue, not a client interpolation claim.

The server artifact now also preserves low-frequency `[StateSyncObserver] DeliveryPerformance` records with produced/sent/dropped/merged bytes, queue age, and resync count. This is required to distinguish expected `limitedbw` backpressure from an unintended queue regression.

## Next comparison

The next synchronization-model branch is deterministic frame sync as a
separate template. It should first prove deterministic RVO/job output and
input replay. Client-uploaded checkpoints, snapshot plus input reconstruction,
disconnect recovery, and zero-client server takeover are later reliability
layers; none should be inferred from the PureState presentation block.

## 2026-08-21 hash/stage E4 repeat and input transport diagnostics

The lightweight state-hash path and one-time Shooter stage sink installation
were validated in a fresh two-observer run:

```text
artifacts/shooter-worldtick-hash-stage-e4-1000-ideal-repeat/20260821-080113-258
```

The structural acceptance passed for 1,000 enemies with the multi-sample-block
template. The stable server window was `28.77 Hz`, with `WorldTick` mean
`5.493 ms`, `EnemyMovementIntent` mean `1.874 ms`, `RvoSolve` mean `2.997 ms`,
and `SnapshotBuild` mean `1.739 ms`. The first window of the same run was a
cold-start outlier (`18.33 Hz`); it must be reported separately rather than
averaged away. Owner and member both reported `0 B/frame` GC, zero unexplained
backward movement, and zero resync requests. Snapshot-gap P95/P99 remained
roughly `220-245/374 ms`, and playback starvation remained `23-25%`, so the
sample-block buffer is still the next continuity target.

The first artifact in the pair is deliberately retained as a negative sample:
member input timed out during startup and the acceptance failed. The failure
identified a diagnostics hole: `NetworkTransport.SendInputAsync` used to swallow
transport exceptions into an empty default response, and the Shooter adapter did
not pass its timeout/cancellation arguments. The adapter now forwards both;
non-cancellation failures return `Status=TransportError` plus the exception
message, while caller cancellation remains cancellable. Future E4 reports should
include transport status counts so cold-start failures cannot be mistaken for
authoritative input rejection.

Two follow-up runs completed the same structural acceptance but were correctly
kept outside the default performance gate:

```text
artifacts/shooter-worldtick-hash-stage-e4-1000-ideal-r3/20260821-082518-175
artifacts/shooter-worldtick-hash-stage-e4-1000-ideal-r3-tuned/20260821-082737-192
```

The first had `0.24-0.55%` playback starvation and a `39.5 ms` P99 apply outlier.
The second had `0.73-1.30%` starvation, up to `49 ms` P99 apply, and a peak
client queue depth of `18`, even though input success was `18/18`, resync count was zero, and GC was
`0 B/frame`. This supports the current diagnosis: sample-block buffering has
substantially reduced persistent starvation, while intermittent main-thread or
queue spikes still need a separate investigation. Do not weaken the gate or
declare a stable 30 Hz result from these single repetitions.
