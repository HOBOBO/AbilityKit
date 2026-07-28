# MOBA View Runtime Session and Replay Refactor

## Scope

This document records the completed P0-P3 refactor of MOBA view-runtime session lifecycle, replication timeline authority, deterministic checkpoints, and isolated replay ownership. The implementation preserves the live session contract while separating lifecycle resources, connection timelines, checkpoint state, and replay-owned runtime state.

## P0: Session Ownership and Lifecycle

`BattleSessionFeature` owns an injected `IBattleLogicSessionRegistry`; the legacy static session host is no longer the production feature's active session owner. `BattleSessionState` exposes created, starting, running, stopping, stopped, and faulted states and increments its lifecycle `Generation` for each start attempt.

`SessionOrchestrator` starts transactionally and cleans up in reverse ownership order. Its cleanup bitmask records each successful step, continues after individual cleanup failures, and retries only unfinished steps on a later stop. Stop is idempotent, a failed start converges through cleanup, and a fully cleaned faulted session can start again. Session handles are reset only after their owning cleanup action succeeds, so retryable resources are not lost before disposal.

`DefaultBattleLogicSessionRegistry` owns its `DefaultBattleDebugFacade`. It clears the global facade only when the published reference belongs to that registry and supports `publishDebugFacade: false` for isolated background and replay sessions.

## P1: Replication Timeline and Diagnostics

`MobaClientReplicationPipeline` owns common synchronization observations: submitted input, monotonic acknowledgements, remote samples, ticks, and diagnostics. Gateway authoritative interpolation uses the pipeline. `ResetDiagnostics` clears pipeline-owned observations without implicitly resetting a concrete synchronization strategy.

Reliable-event timeline authority is represented by the cursor epoch and cursor instance identity. An event batch from a different epoch is rejected until an authoritative snapshot adopts that epoch and watermark. Retention gaps request a full resynchronization; after baseline adoption, replay continues from the authoritative watermark. Asynchronous acknowledgements are accepted only when both the cursor instance and epoch still match, and confirmed sequence numbers never move backward.

This transport epoch is intentionally separate from `BattleSessionState.Generation`: the epoch identifies server reliable-event history, while the generation identifies a local session lifecycle attempt. Reconnection resets pipeline-owned diagnostics and uses authoritative baseline adoption instead of treating a local lifecycle generation as a network timeline identifier. Synchronization health uses sustained-pressure thresholds, immediate critical escalation, recovery hysteresis, reset defaults, and counter-reset protection.

## P2: Deterministic Checkpoints

The deterministic checkpoint coordinator captures a header containing world identity, world type, tick rate, frame, provider entries, and a stable state hash. Provider entries are ordered by provider key before hashing, making equivalent captures independent of registration order.

Restore validates checkpoint identity before importing provider state. Import is transactional: if any provider fails, every touched provider is restored to its pre-import state. A successful restore reproduces the captured provider hash.

`FrameRecordChunkIndex` remains a seek anchor index rather than a restorable world checkpoint. `BattleReplaySessionOwner` now consumes an explicit optional replay-runtime checkpoint capability: it captures runtime-owned opaque tokens at frame 0 and every 30 frames, restores the nearest checkpoint not newer than a backward-seek target, and replays only the remaining recorded input. Tokens have explicit release ownership. The owner retains at most 64 checkpoints, preserves the frame-0 baseline while evicting old non-baseline tokens, invalidates and releases future-branch tokens after a backward restore, and releases every retained token before disposing the runtime. Cleanup attempts all releases and runtime disposal in deterministic order; one failure is propagated unchanged and multiple failures are aggregated. Runtime disposal remains the final reclamation boundary for tokens whose explicit release failed.

The default production replay runtime does not declare checkpoint capability because its complete deterministic recovery-provider registry has not yet been established. It retains the isolated-runtime restart and replay fallback rather than exposing a partial checkpoint implementation as deterministic recovery.

## P3: Replay Ownership

`BattleReplayManifest` validates replay schema, world identity, world type, effective tick rate, frame range, and sorted seek anchors before startup.

`BattleReplaySessionOwner` receives testable runtime/session creation boundaries and owns a separate local `BattleLogicSession`, replay driver, fixed-step accumulator, and `IBattleLogicSessionRegistry`. Startup failure rolls back partially created resources. Backward seek uses the nearest runtime checkpoint when the optional capability is available and otherwise recreates only the owner runtime before replaying to the target. Checkpoint capture, restore, release, pump, or runtime-disposal failure follows the stop convergence path and is exposed through the owner's most recent terminal-failure diagnostic. A successfully established replacement session clears that diagnostic.

Continuous playback is budgeted to at most eight simulation frames per Unity tick and buffers at most 30 frames of playback debt, preventing a large render delta from causing an unbounded main-thread loop. NaN and infinity deltas contribute no time, and non-finite playback speeds normalize to 1x. Synchronous explicit seek remains an immediate operation and should not be used as an unbounded per-frame workload by presentation code.

`FrameReplayDriver` snapshots the input list during construction, removes null entries, orders records by frame, and preserves source order among records from the same frame. Binary seek therefore operates on a stable ordered data set, and later mutation of the deserialized source list cannot alter an active replay.

The owner uses a non-publishing registry, so replay startup and shutdown cannot replace or clear the live battle debug facade. Loading replay through `BattleSessionFeature` does not mutate the live start plan, handles, registry, world resources, or shared `BattleContext`. A `Replay` start plan starts the isolated owner directly and does not start the live pipeline.

The live replay subfeature is record-only: it may initialize a `FrameRecordWriter` in `Record` mode, but cannot assign a replay driver to live `BattleSessionHandles`. Replay drivers are owner-owned.

## Teardown Failure Contract

Concrete session teardown runs independent cleanup steps best-effort and preserves execution-order failure aggregation. Confirmed-view feature and context handles use commit-on-success ownership clearing: detach, entity-tree cleanup, lookup clearing, and pool return must complete before the owning handle is cleared. A failed context cleanup does not return the context to the pool while it is still owner-reachable. Session-owned input, snapshot, and view-event runtime handles propagate disposal failures and clear their references only after successful disposal, allowing the orchestrator to retry unfinished cleanup work.

## Presentation Boundary

The current replay owner is logic-only. `TryLoad(path, renderPresentation: true, ...)` is rejected because the view runtime still has one shared `BattleContext` and presentation resource set. Isolated replay rendering requires a separate context/presentation factory and explicit output routing; it must not stop the live session or rebind the live context.

## Verification

The runtime and runtime-test .NET projects previously built successfully with single-node MSBuild. Single-node execution is required in this workspace because a parallel build previously lost an MSBuild child node with `MSB4166`; that earlier source compilation reported no errors.

Focused .NET verification passed 13 of 13 P1 tests covering replication diagnostics/reset, reliable-event epoch behavior, retention gaps, monotonic acknowledgements, health hysteresis/reset, and replay manifest contracts. The deterministic checkpoint suite passed 4 of 4 P2 tests covering ordered capture/stable hash, equivalent restore, transactional rollback, and world-identity rejection.

The earlier focused XML evidence records 10 of 10 P0 lifecycle tests and 5 of 5 P3 owner tests. Those results predate the optional replay-checkpoint and concrete world-disposal tests described below and are retained only as historical context.

The remaining-logic change adds owner contract tests for checkpoint selection, residual replay, bounded retention, branch invalidation, explicit release ordering, capture/pump/release/dispose failures, cleanup aggregation, playback budgets, non-finite timing, and diagnostic recovery. Driver tests cover non-finite speed, unordered/null source data, source-list mutation isolation, and stable same-frame command order. Concrete teardown tests prove best-effort execution, unchanged single-failure propagation, and deterministic multi-failure aggregation. End-to-end injection through sealed gateway/world implementations remains outside this test boundary.

The current exclusive Unity 2022.3.62f1 EditMode gate produced `Unity/artifacts/moba-session-replay-p0-p3-integration-results.xml`, a non-empty 17,543-byte NUnit result. Its root result is `Passed`, with 20 total tests, 20 passed, zero failed, and zero skipped. The gate covers `SessionOrchestratorLifecycleTests`, `BattleReplaySessionOwnerTests`, `FrameReplayDriverTests`, and `BattleLogicSessionRegistryIsolationTests`, including the newly added checkpoint, deterministic-input, diagnostic-recovery, and teardown failure-contract cases. The repository Unity batch process exited after writing the result; the only subsequently observed Unity editor process belonged to a different project and was not modified by this verification.
