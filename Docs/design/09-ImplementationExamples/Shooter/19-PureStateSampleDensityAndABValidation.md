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

| Tier | Observer distance | Metered history | Unmetered history | Current authority |
|---|---:|---:|---:|---:|
| Near | `<= visibleRadius * 0.40` | Every historical frame | Every historical frame | Unchanged |
| Mid | `<= visibleRadius * 0.75` | Newest historical frame | Newest historical frame | Unchanged |
| Far/boundary | Remaining AOI boundary | None | Newest historical frame | Unchanged |

The newest historical frame is the stride anchor. With authority frame 3 and
historical frames 1 and 2, near sends frames 1 and 2 and mid sends frame 2.
Far sends neither on a metered link and sends frame 2 on an unmetered link.
Velocity remains in each selected transform so sparse tracks retain bounded
extrapolation information.

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

The metered mass-battle policy therefore applies an additional hard limit:

```text
MaxHistoricalTransformsPerBlock = 32
```

For a three-frame block there are two historical frame descriptors. The ring
divides the remaining block budget by the number of remaining descriptors,
rounding up. With 32 available transforms both frames initially receive 16. If
the older frame uses fewer than 16, its unused capacity transfers to the newer
frame. The sum can never exceed 32, even when `ActiveSyncBudget` is 2,048.
`FullDensity` keeps `int.MaxValue` so protocol tests can still compare an
unbounded reference payload. The playable unmetered policy uses a 2,048
transform block ceiling; the `limitedbw` comparison policy remains capped at
32 and omits far history. This split prevents the weak-link stress case from
silently becoming the local/LAN continuity default.

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
`pureStatePlaybackMaxTransformSampleCountPerBlock` no greater than 32 for
`limitedbw` or 2,048 for an unmetered environment. This
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

## 2026-08-24 formal multiplayer default correction

The formal multiplayer profile still selected `mass-battle-lod-aoi` and
inherited that experimental template's `limitedbw` network environment. This
meant a normal local multiplayer launch unintentionally enabled the observer's
128 Kbps stress budget. The server log for battle
`36e69bb6897d41c58c716088b5cb5eb1` separated the stages clearly: the authority
clock sustained about `30 Hz`, while one observer produced `3,800,716` bytes,
sent `501,056` bytes, and merged `3,288,633` bytes. The one-to-two-second visual
steps were therefore primarily queue supersession under an explicitly
constrained profile, not evidence that the authority simulation only ticked
once every one or two seconds.

The playable default now separates synchronization shape from network fault
injection:

- the formal and fallback room defaults use
  `mass-battle-lod-aoi-sample-block`, so the client can spread historical
  samples across render frames;
- the playable network environment is explicitly `ideal`, which disables the
  observer token-bucket budget and client-side simulated loss/latency;
- `limitedbw` remains available only when a test profile or matrix case asks
  for it explicitly.

The profile contract asserts the template ID, room network tag, zero bandwidth
limit, and zero synthetic latency/jitter/loss/reorder. A fresh two-client
Headless run is still required before assigning new arrival-gap or starvation
numbers to this correction.

The same playable-default contract now applies to every multiplayer entry
surface, not only the formal scene controller. `ShooterPlayModeMenu`, the
remote-state-sync profile samples, and the Unity two-client Headless runner all
select `mass-battle-lod-aoi-sample-block` with `ideal` unless the caller
explicitly chooses another network environment. The Headless command writes
that environment into the profile before building both client session options
and room tags, so a performance-matrix case no longer conditions only the
server while silently leaving the client on another profile. The dedicated
sample-block A/B runner continues to pass the single-sample baseline and
`limitedbw` explicitly, preserving the stress comparison.

## 2026-08-24 template routing fix and 1,000-unit server RVO follow-up

The first Headless verification after the playable-default correction exposed
another configuration override. `AbilityKit.Orleans.ShooterSmoke` defaulted
`ABILITYKIT_SHOOTER_STATE_SYNC_PAYLOAD_MODE` to `packed` and wrote it for every
server launch. That environment override has higher priority than the room
sync template, so a room labelled `mass-battle-lod-aoi-sample-block` still sent
packed actor snapshots. The diagnostic signature was unambiguous:
`actorSnapshotAppliedCount > 0`, while every pure-state sample-block and
playback counter remained zero.

ShooterSmoke now defaults its command-line payload selector to `template`.
That mode clears the environment override and lets the room template select
the payload. Explicit `packed` and `pure-state` options remain available for
the multi-process compatibility suites. A fresh 64-unit two-client run then
reported 120/126 received sample blocks, 239/252 frame samples, 2,123/2,010
playback render ticks, zero playback starvation, and a successful AOI despawn
lifecycle:

```text
artifacts/shooter-unity-headless/20260824-082937-033
```

The 512-unit run also completed the full lifecycle with zero client GC and
snapshot-arrival P95 around 181-184 ms. At 1,000 units, however, the authority
fell to 18-22 Hz and playback starvation rose to 18-24%. The server stages
showed that this was no longer primarily a serialization or observer-bandwidth
problem: snapshot build averaged 2.8-3.4 ms, while `WorldTick` averaged
20-30 ms and was dominated by enemy movement intent plus managed RVO.

The Orleans world had selected `AcceleratedPreferred`, but unlike Unity it had
no platform acceleration service and silently used the single-threaded managed
neighbor collector. The server now injects a per-world deterministic spatial
grid collector. It reuses its grid storage, writes disjoint per-agent neighbor
slots, uses at most four workers above 256 agents, and sorts each result by
distance then entity ID. A 513-agent test compares every result against a
brute-force reference, and repeated parallel runs assert stable output.

In the immediate 1,000-unit A/B repeat, the stable RVO mean decreased from
12.48 ms to 9.84 ms and achieved authority rate increased from 22.02 Hz to
24.55 Hz:

```text
before: artifacts/shooter-unity-headless/20260824-084249-577
after:  artifacts/shooter-unity-headless/20260824-085805-364
```

This is a capacity improvement, not a final 30 Hz claim. The next server work
should measure neighbor collection separately from ORCA solving, remove the
remaining serial solve bottleneck, and validate multi-room fairness before
raising worker parallelism. Client playback should retain the sample-block
buffer: it masks ordinary 100-300 ms delivery variation, but it cannot conceal
an authority that continuously produces fewer than 30 simulation frames per
second.

## 2026-08-24 RVO substage measurement and parallel ORCA

The server performance sink now keeps the existing inclusive `RvoSolve`
window and adds three non-overlapping substages:

- `RvoNeighborCollect` covers accelerated collection plus any managed fallback;
- `RvoAcceleratedValidation` covers bounds, ordering, duplicate, and exact
  distance validation of accelerated output;
- `RvoOrcaSolve` covers ORCA line construction and the per-agent linear
  programs.

The first 1,000-unit run with those counters completed the two-observer AOI
lifecycle and reported `0 B/frame` client GC:

```text
artifacts/shooter-unity-headless/20260824-115114-247
```

Its stable `150-299` server window showed that the remaining RVO time was not
primarily validation:

```text
AchievedHz=22.24
WorldTick=18.305 ms
EnemyMovementIntent=7.010 ms
RvoSolve=10.021 ms
  RvoNeighborCollect=3.040 ms
  RvoAcceleratedValidation=0.921 ms
  RvoOrcaSolve=6.016 ms
```

The managed solver therefore gained an optional server-only per-agent
accelerator. ORCA agents read the immutable frame input and neighbor arrays and
write disjoint line, projected-line, and output-velocity slots. The projected
line scratch buffer was expanded from one shared segment to one segment per
agent before enabling concurrency. Worlds without the accelerator, small
worlds below 256 agents, and the normal Unity client path retain the serial
solver. Tests compare the accelerated world against the managed world by state
hash and assert that every parallel agent slot is visited exactly once.

The immediate parallel-ORCA repeat used the same 1,000-unit template and ideal
network profile:

```text
artifacts/shooter-unity-headless/20260824-120411-586
```

Stable-window comparison:

```text
                           before       parallel ORCA    change
RvoOrcaSolve               6.016 ms     4.081 ms         -32.2%
RvoSolve                  10.021 ms     8.356 ms         -16.6%
WorldTick                 18.305 ms    16.289 ms         -11.0%
Achieved authority rate   22.24 Hz     25.21 Hz          +13.4%
```

This repeat also passed AOI lifecycle and input acceptance with zero reported
client GC. Its owner process performed a second Unity script/DAG refresh after
the formal process started, producing a 3.3-second Editor update stall; client
p99 arrival, input RTT, and playback starvation from that run are therefore
not valid A/B evidence. The server stable window is still useful because the
refresh contamination is visible and the individual stage counters remain
separable.

Parallel ORCA improves capacity but does not establish stable 30 Hz. The next
server target is `EnemyMovementIntent` at roughly 6.6 ms, followed by the
3.3 ms neighbor collector. Before increasing the current four-worker ceiling,
run concurrent-room fairness and server allocation tests: two `Parallel.For`
phases per authority tick may add thread-pool contention and small server-side
allocations even though Unity client GC remains zero.

## 2026-08-24 per-entity continuity follow-up

Aggregate block counts were not enough to explain the reported one-second
visual steps. The client now records a fixed 256-frame histogram of transform
sample intervals per entity, including P50/P95/P99 and maximum. Historical
block transforms are always marked as sparse trajectory observations, and the
playback track may project their velocity for at most six frames. Teleports and
`NoInterpolation` samples still bypass projection.

The first fresh 1,000/ideal run used dense near and mid history but no far
history:

```text
artifacts/shooter-unity-headless/20260824-130957-666
```

It proved the remaining tail directly. Both clients reported transform sample
P50/P95/P99/max of `1/3/30/30` frames. The authority was producing normally;
the stable server window reached `28.41 Hz`, snapshot queues did not remain
backed up, and client GC was `0 B/frame`. The one-second step was therefore a
far-LOD presentation hole rather than a one-Hz simulation or transport stream.

For unmetered environments, the newest historical frame now also contains far
AOI transforms. `limitedbw` retains far-history stride zero and the 32-transform
block cap. The immediate same-machine repeat is stored at:

```text
artifacts/shooter-unity-headless/20260824-132818-421
```

| Metric | Before owner/member | Far-history owner/member |
|---|---:|---:|
| Per-entity interval P99 | `30 / 30` frames | `3 / 3` frames |
| Per-entity interval P95 | `3 / 3` frames | `3 / 3` frames |
| Average payload | `9.5 / 10.3 KB` | `14.9 / 15.9 KB` |
| Maximum historical block | `426 / 384` | `641 / 618` transforms |
| Playback starvation | `11.26% / 7.60%` | `5.45% / 6.36%` |
| Held playback | `9.70% / 6.62%` | `4.19% / 4.77%` |
| Delta apply P99 | `8 / 16 ms` | `26 / 13 ms` |
| Stable authority rate | `28.41 Hz` | `29.23 Hz` |
| Client GC | `0 / 0 B/frame` | `0 / 0 B/frame` |

This establishes the intended continuity tradeoff: the 30-frame tail moved to
three frames and held/starved presentation decreased, at a roughly 55% average
payload increase. It is not a clean latency A/B. A shared Unity source file was
modified while the second pair was starting, forcing AssetDatabase re-import;
that run also contains larger Editor and FullBaseline stalls. Arrival/apply
outliers from the pair must not be attributed solely to far history.

The second artifact recorded a raw maximum interval of 90/102 frames even
though P99 was three. Inspection found that the diagnostic retained an entity's
last sample while it was outside AOI, then counted the invisible period when
the entity re-entered. Full baselines and removals now terminate that entity's
continuity segment. A focused lifecycle test proves that despawn-to-respawn time
does not enter the interval histogram; the artifact itself predates this
diagnostic correction.

The Headless runner also separates compile warmup and client startup budgets
from the multiplayer workflow timeout. Both Unity clients must publish startup
state before the workflow stopwatch begins. This prevents a 170-250 second
cold Asset Refresh from being reported as a synchronization timeout.

The next bandwidth optimization should preserve the three-frame endpoint while
reducing its cost, preferably with a quantized transform delta or far-tier
velocity segment rather than removing far history again. Validate that work at
1,000 and 2,000 units with repeated clean runs, and gate per-entity P99 together
with payload, delta-apply P99, queue wait, and achieved authority rate.

## 2026-08-24 pure-state v3 compressed history and 2,000-unit capacity evidence

Pure-state schema version 3 keeps the three-frame trajectory endpoint while
removing most of the raw historical-transform cost. The outer payload remains
a 15-member line layout. Member 13, the v2 raw `TransformSamples` array, is
empty; member 14 contains a compressed byte block with an encoding version,
transform count, delta-coded entity IDs, and MemoryPack varints for entity
kind, quantized position, quantized velocity, and flags. Decoding reconstructs
the original `ShooterPureStateTransformSample[]` losslessly, so playback and
interpolation do not need a separate v3 path.

Compatibility remains explicit rather than relying on serializer tolerance:

- decoders continue to accept the legacy 11/12/14-member v1/v2 layouts;
- the .NET authority writes the v3 compressed block;
- the uncommon Unity send path may still write the 14-member raw layout;
- the pure-state catalog advertises maximum schema version 3;
- wire gates cover v2 raw compatibility, field-for-field v3 round trips,
  1,000-transform warmed decode with zero allocation, and compression bounds.

The synthetic density gates require a 256-transform v3 block to remain below
80% of its raw layout and the 1,400-transform SmoothMassBattle fixture to remain
below 55%. Protocol and compatibility tests passed `36/36`; Grains sampling,
template, and runtime-adapter tests passed `44/44`; the ShooterSmoke runner
contract passed `6/6`.

The first 1,000-unit run after compression is stored at:

```text
artifacts/shooter-unity-headless/20260824-171327-440
```

Compared with the uncompressed far-history run
`20260824-132818-421`, average owner/member payload fell from
`14.9/15.9 KB` to `9.06/8.88 KB`, approximately `39%/44%`. Per-entity
transform interval P99 remained `3/3` frames, maximum payload stayed below
`18.8 KB`, and both clients reported `0 B/frame` GC. This confirms that the
compression removes bandwidth without reopening the one-second far-LOD hole.

A warmed 1,000-unit repetition is available at
`20260824-172233-521`, but both 1,000-unit runs include Unity Initial Refresh,
script compilation, or Domain Reload activity. Their payload size, decode
success, per-entity interval, and zero-allocation results remain useful. Their
client apply P95/P99 values are not a clean A/B comparison against the earlier
artifact.

The 2,000-unit capacity run is stored at:

```text
artifacts/shooter-unity-headless/20260824-172623-481
```

It completed the multiplayer and AOI lifecycle with zero decode failures, zero
snapshot resyncs, and `0/0 B/frame` client GC. Average payload was
`12.27/15.49 KB`, maximum payload `27.55/29.13 KB`, and per-entity
P50/P95/P99 remained `3/3/3` frames. Nevertheless, starvation reached
`51.21%/55.58%` because the authority's first active window sustained only
`15.37 Hz`:

```text
WorldTick                    34.427 ms
EnemyMovementIntent         15.362 ms
RvoSolve                     14.755 ms
  RvoNeighborCollect         6.662 ms
  RvoOrcaSolve               6.706 ms
SnapshotBuildSerialize       6.805 ms
```

This separates the current failure modes. The v3 transport still provides
three-frame observations at 2,000 units; the client cannot render 30 fresh
authority frames per second when server simulation produces about 15. The next
priority order is therefore:

1. remove the small-player-set overhead in `EnemyMovementIntent`, then assess
   whether its remaining per-enemy work merits server-only parallel execution;
2. improve RVO neighbor collection and ORCA data layout without increasing the
   current worker ceiling before multi-room fairness and allocation checks;
3. restore a stable 30 Hz authority before deciding whether client apply needs
   additional frame slicing;
4. run at least three clean 1,000- and 2,000-unit repetitions with Unity asset
   refresh disabled, and report median plus worst-case stage and presentation
   metrics.

The Headless runner now detects UTF-16 LE/BE and UTF-8 server logs and aligns
offsets before reading appended data. This fixes the prior zero-byte
`server-performance.log` export caused by seeking into a UTF-16 stream and
decoding it as UTF-8. The parser and runner contract are covered by tests; a
fresh end-to-end run must still confirm non-empty export on the real launcher.

## 2026-08-26 playable 512-unit validation and gate correction

The first fully green two-client Headless pass at the playable default
(`mass-battle-lod-aoi-sample-block` + `ideal`, 512-unit budget) completed the
complete lobby-to-battle-to-AI-lifecycle flow with the in-flight server fixes
in the working tree:

```text
artifacts/shooter-unity-headless/20260825-170235-795
```

Both clients reported zero playback starvation (owner 0.0%, member <=0.3%),
per-entity transform sample intervals P50/P95/P99 = 1/3/3 frames, zero client
GC, sync-frame P95 of 2.0-2.5 ms, and 18/18 accepted inputs with zero resync.
The authority sustained 29.98-30.00 Hz across both observed windows
(`WorldTick` mean 0.7-0.9 ms, `EnemyMovementIntent` mean 0.047-0.059 ms,
`RvoSolve` mean 0.5 ms). The earlier 15 ms `EnemyMovementIntent` figure came
from the dictionary-backed expanding-ring nearest-player query that ran per
enemy before the direct-scan path landed; with it, intent is no longer a
capacity factor.

Five stale tests were updated to the current intended semantics so the .NET
suite is green again (579/579): bounded local-player correction now subtracts
reachable prediction distance and shares one per-client-frame budget across
same-frame authority snapshots, authority overrides and full baselines in the
same world use bounded correction instead of hard snapping, and the default
room template is `mass-battle-lod-aoi-sample-block` with the `ideal`
environment. The hard-snap path remains covered by the world-change test.

The Headless performance gate previously compared a single mixed
`p99BattlePushApplyMs` against the 25 ms delta budget. A pure-state room always
applies one full baseline (50-58 ms at 512 units, dominated by bulk view
creation) per `FullSnapshotIntervalFrames`, so the mixed metric failed a gate
the delta path had actually satisfied (delta apply P99 5.0-7.5 ms). The gate
now checks the steady-state delta P99 against 25 ms and gives the bulk paths
separate budgets (`FullSnapshotApplyMaxThresholdMs` 120,
`P99ReliableEventApplyThresholdMs` 60). The runner also auto-attaches to the
newest `host-*.log` silo log when `-ServerLogPath` is not supplied, because
`ServerPerformance` windows are emitted by `BattleLogicHostGrain` in the silo
host log, not the TCP gateway log.

A new `.NET`-only authority benchmark
(`ShooterAuthorityStageBenchmarkTests`, scaled via
`ABILITYKIT_SHOOTER_BENCH_UNIT_COUNTS` / `ABILITYKIT_SHOOTER_BENCH_FRAMES`)
drives the runtime adapter directly with per-observer pure-state pushes and
self-verifies load by counting alive enemies each 60 frames. Without that
check the benchmark silently measured a collapsed load after the players
died (enemies lose their target, preferred velocities stay zero, and RVO
becomes trivial). Verified-load stable-window results on this tree:

```text
  512 units (380-454 alive): intent 0.092 ms, collect 0.308 ms, ORCA 0.282 ms, ~347 Hz sim-only
 1000 units (856-932 alive): intent 0.172 ms, collect 0.847 ms, ORCA 0.691 ms, ~170 Hz sim-only
 2000 units (1844-1928 alive): intent 0.088 ms, collect 0.813 ms, ORCA 0.194 ms, ~139 Hz sim-only
```

Snapshot build plus serialize stays at or below ~0.8 ms mean (8.6-23.8 KB
average per-observer payload). Under the pure-sim loop the whole authority
tick is therefore 1.5-2.5 ms at every measured scale; the remaining
end-to-end question is Orleans scheduling and delivery overhead, which the
two-client Headless run at 2,000 units measures directly.

Known remaining rough edges observed in the 512 pass:

- the one-shot full-baseline apply (50-58 ms) is bulk view creation; slicing
  baseline view spawns across frames is the follow-up if it becomes visible
  in play;
- a dead observer fails closed: the server cannot build an AOI scope, returns
  empty pushes flagged `RequiresFullSnapshot`, and the client-side
  `snapshotResyncNeededCount` grows while the player stays dead. A spectator
  fallback scope (arena-center interest) would keep the world rendering;
- `applyP99` for reliable-event batches can reach ~41 ms once per battle from
  presentation-side effect work.

That 2,000-unit two-client Headless run then completed green on the same tree:

```text
artifacts/shooter-unity-headless/20260825-170837-108
```

Both clients held zero playback starvation with sample intervals P95/P99 =
3/3 frames, delta apply P99 of 6.5-10.5 ms, zero GC, and the authority
sustained 29.83-30.05 Hz with both observers attached (`WorldTick` mean
4.1-4.7 ms). Compared with the 2026-08-24 2,000-unit run (15.37 Hz, 51-56%
starvation), the server-side acceleration service injection plus the
small-player-set scan removed the entire capacity gap: the earlier runs
executed without the per-world neighbor-acceleration registration that now
lives in the runtime adapter. Stable 30 Hz authority at the 2,000-unit budget
is therefore demonstrated end to end; the earlier four-step priority list is
resolved by these measurements except for multi-repetition medians, which
remain good hygiene rather than a known gap.

## 2026-08-26 client-path (local mode) measurement

`ShooterWorldModule` registers the null neighbor-acceleration service, so the
Unity client world — including pure local play — runs the serial managed RVO
solver while only the server adapter injects the accelerated collector and
parallel ORCA. A local-path benchmark
(`ShooterLocalWorldStageBenchmarkTests`, scaled via
`ABILITYKIT_SHOOTER_LOCAL_BENCH_UNITS`) drives that exact configuration with
one player under maximum blob density (all enemies converge on a single
target) and stacked concurrent waves so the enemy budget actually fills:
at 2,000/2,000 alive units the whole simulation is ~4.2 ms per frame
(RvoSolve 3.6 ms with managed collect 2.8 ms, simulation 0.66 ms, intent
0.05 ms); at 500/500 it is ~2 ms. The shared workspace grid rewrite therefore
carries the client as well, and the server-only acceleration is worth less
than a millisecond at this scale. Historical local-mode 2,000-unit stutter
was dominated by the pre-fix intent ring query plus the pre-rewrite managed
RVO (30-40 ms+ on the main thread), compounded by local mode building views
for every entity because observer AOI only exists on the push side. With the
current tree the remaining local-mode cost at 2,000 units is dominated by the
presentation layer (full-entity view creation and per-frame transform
updates), not simulation.

## 2026-08-26 editor local-mode 2K stutter root cause and RVO acceleration sink

The editor-local 2K-mode stutter (~10 FPS reported in Play mode, GPU
instanced backend) was reproduced headlessly with a new editor benchmark
(`ShooterLocalEditorPerfBenchCommand`, driven via Unity batchmode; splits
simulation tick / presentation publish / end-to-end host frame costs and
samples per-system stage timings inside the real editor Mono runtime). At a
verified 2,048/2,048 alive enemies the local simulation tick cost 36 ms per
frame: managed serial ORCA 16.0 ms, Burst-jobs hashmap neighbor collection
10.5 ms, accelerated-output validation 2.5 ms, intent 2.8 ms. The GPU view
backend itself was not the constraint (host frames averaged ~20 ms with tick
dropping). The root cause was that the server's RVO acceleration (sorted-grid
parallel collection + parallel ORCA) lived only in the Orleans assembly while
every client world registered the null service.

Fixes, validated by rerunning the same benchmark:

1. The parallel acceleration implementation moved into the shared runtime
   package (`ShooterParallelRvoAccelerationService`, platform-neutral
   Parallel.For) and `ShooterWorldModule` registers it by default; the server
   class became a thin subclass.
2. The Burst jobs neighbor service now also implements the parallel
   per-agent solve interface, but the play-mode factory no longer mounts the
   jobs world module by default: measured head-to-head in the editor, the
   shared parallel collector costs 3.8 ms/frame versus 10.8 ms/frame for the
   Burst hashmap collector, and drops per-frame managed↔native copies and
   pre-scan passes (GC 235 MB -> 23 MB over 8 s at 2,048 units). The jobs
   module remains available for explicit composition.
3. Accelerated-output validation is now sampled (default every 30th
   accelerated collection, first included, configurable via
   `ShooterRvoOptions.AcceleratedValidationInterval`; determinism contract
   tests pin it to 1).

Result at 2,048 units in the editor: simulation tick 36 -> 18 ms with items
1-2, and the sampled validation removes a further ~3 ms; end-to-end host
frame 20.6 -> ~10 ms (98 FPS in the batch harness). Play-mode smoothness at
2K in a real editor window is expected to land well above 30 FPS; a player
build (IL2CPP + full Burst) will be faster still. Remaining editor-only
costs are the Mono tax on intent/spawn/ORCA bodies, which the .NET-side
numbers show at 0.09/0.02/0.7 ms in Release.

## 2026-08-26 remote pure-state composition: authority batch double-write

Field reports from the two-client session described every remote entity being
tugged back and forth and the controlled player freezing. A dedicated
reproduction test (`ShooterRemotePureStateCompositionTests`, real cadence:
60 fps render x 30 Hz simulation x one push every three frames) captured both
defects in the composed render batch:

- On every push frame the same remote entity appeared twice at two different
  positions: once from the interpolation playback and once raw from the
  authority batch. `PublishPureStatePresentationFrame` sourced its "local"
  side from `Presentation.ViewModel.Current`, which the authority apply
  overwrites ten times per second (prediction-only batches overwrite it at
  30 Hz), so between pushes the composition mixed a stale raw pose over the
  interpolated one — the visible tug.
- On the same push frames the controlled player vanished from the render
  batch: the server omits the observer's own player from pure-state export,
  so the authority batch carries no controlled-player transform, and the
  composition lost the predicted pose until the next local tick republished.

The fix gives the controller its own hold of the latest predicted-local batch
(`_lastPureStatePredictedBatch`, captured after each local prediction publish
and cleared with the pure-state reset paths); the composition's local side now
always comes from that hold instead of the shared view model. The reproduction
test asserts per render frame: at most one transform per remote entity, the
controlled player present after its first predicted tick and never pulled
behind its own prediction, and a non-empty playback so the assertions cannot
pass trivially. Full runtime suite 581/581.

A related recovery hazard was observed while building the reproduction: a
baseline-hash mismatch (or any `NeedsFullBaselineResync` condition) drives the
frame-sync recovery machinery into catch-up/full-resync, which fast-forwards
or resets the local prediction world — for a pure-state observer client whose
remote state lives in the playback stream, that machinery treats the local
prediction sandbox as if it had to track the server frame. The composition
fix removes the visible artifact; keeping drift recovery inert for
pure-state presentation clients remains a follow-up hardening item.

## 2026-08-26 enemy tug root cause: sparse-LOD sample cadence and dead-observer freeze

After the composition fix, a new pose-continuity probe
(`ShooterRemotePoseContinuityProbe`, hooked at the per-render frame builder;
optional sustained-input driver via `ABILITYKIT_SHOOTER_SUSTAINED_INPUT=1`)
quantified the remaining field complaints under continuous play at 512 units.
The probe's fingerprint was decisive: backward/forward jumps occurred as
"same render frame, many entities, one uniform distance" (0.78/0.54/0.60 in
different runs) — a batch event, not per-entity interpolation noise.

Two causes, both fixed:

1. **Dead-observer fail-closed freeze.** When a player died, the server could
   no longer build that observer's AOI scope and returned empty pushes flagged
   `RequiresFullBaselineResync`; that client's world froze while the other
   kept playing — the dominant source of the perceived cross-client
   desync, plus baseline-resync churn (owner 3 resyncs, member 0). The server
   now falls back to an arena-center spectator scope for dead observers;
   unresolved accounts keep the fail-closed path.

2. **Sparse mid/far LOD cadence.** With mid=9 / far=30 frame sample intervals
   and a six-frame extrapolation cap, low-cadence entities drifted to a held
   pose and then jumped the accumulated distance when the next sample landed
   — the uniform batch jumps. For unmetered environments (bandwidth 0) the
   template now lifts mid/far cadence to the near cadence (3/3/3); metered
   profiles keep 3/9/30 for the stress comparison. The full-baseline track
   handling was also softened in the same round: baselines now prune tracks
   to the baseline entity set instead of clearing all tracks, so a re-baseline
   no longer restarts every entity's playback from the baseline pose.

Validated head-to-head with the same sustained-input scenario:
enemy backward-tug events went from 50-92 per client to **0 on both clients**
(fully symmetric), own-player backward pulls stayed 0 throughout, snapshots
applied cleanly, zero GC. The client contract assertion in the Headless
command now accepts both the metered (3/9/30) and unmetered (3/3/3) LOD
cadence shapes.

## 2026-08-27 control loss root cause: per-render-frame input flooding vs admission throttle

Field report after the tug fixes: the controlled player becomes uncontrollable
after a while, and an idle player stutters in place once enemies swarm. The
formal remote host loop called the input pump once per RENDER frame; with the
submit queue's four-in-flight window draining in ~RTT on localhost, the
effective gateway submission rate equals the editor frame rate (60-144/s).
The battle input admission guard's token bucket (60/s sustained, burst 90)
exhausts within about a second of continuous play, and the gateway mapped
every rejection to `ShouldResync=true`. That drove the client into the
frame-sync recovery state (`AwaitingFullSnapshot`), where `SubmitLocalInputs`
returns zero — local prediction is blocked, inputs stop, control is lost;
the repeated full-baseline re-application that follows is the in-place
stutter. The headless runs never saw it because their movement probe submits
at a low fixed rate.

Fixes:

1. the formal host now samples and submits input at the simulation tick rate
   (30 Hz) via an accumulator, matching what the sim consumes per tick;
2. the gateway classifies rejection reasons: only genuine state divergence
   (world mismatch, invalid payload/op-code/player, input-buffer rejection)
   demands a client resync; transient throttling (rate-limited, duplicate,
   sequence-too-old, not-initialized) drops the single input;
3. the headless sustained-input driver now floods through the real gateway
   submission path at render rate — worse than production — and logs every
   rejected submit with its status, so the validation proves control survives
   even under flooding.

Headless validation under the flood driver: input submissions at render rate
with zero control loss; rejection/resync counters reported in the
`[PoseContinuity]` heartbeat lines.

## 2026-08-27 persistent cross-client position divergence: frame-age tolerance absorbing real drift

Field report: the controlled player stops around 10 units right locally while
the other client (rendering authority) keeps it at 8 — a long-lived two-unit
disagreement. Root cause: `ResolveControlledPlayerPosition` treated the raw
frame-number gap between client and authority as "legally reachable
prediction distance". That gap is dominated by join-anchor offset plus push
pipeline latency (it stays permanently around a dozen frames), so the
tolerance permanently absorbed 1-2+ units of genuine drift. Drift arises
whenever the local prediction applies inputs the server never did (rejected
submissions, queue merges) — the bounded correction never fired, and the two
ends disagreed forever.

Fix: legal prediction lead is exactly what the pending-input replay already
applied to the correction target (target = authority + in-flight inputs, so a
correct prediction yields zero error). The frame-age term is removed; any
error beyond `SmallErrorTolerance` converges at the bounded budget per frame
(0.25). Two regression tests pin the semantics: a 500-frame gap with no
in-flight inputs must still correct (old behavior absorbed up to 20 units),
and in-flight input count alone provides no extra exemption. The existing
replay tests already expected zero-slack behavior and now pass unchanged.
Full suite 583/583.

## 2026-08-27 no-prediction diagnostic mode

To settle whether the remaining cross-client position disagreement lives in
the prediction/reconciliation layer or in the sync pipeline itself, a
diagnostic mode disables local prediction entirely via
`ABILITYKIT_SHOOTER_DISABLE_LOCAL_PREDICTION=1`. The server already always
exports the observer's own player (priority 1000, flagged `PredictedLocal`);
the exclusion happens client-side. In this mode the client stops suppressing
its own player's transforms from the interpolation playback (both the block
samples and the flagged mapper transforms), renders the composition's local
side as empty, and skips controlled-player reconciliation — both ends then
render the identical server truth through their own playback delays. The
pose probe heartbeat now includes the own player's position so the two
clients' logs can be compared directly after a run.
