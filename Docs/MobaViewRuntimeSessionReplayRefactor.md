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

`FrameRecordChunkIndex` remains a seek anchor index rather than a restorable world checkpoint. The checkpoint protocol is available to deterministic runtime owners, but `BattleReplaySessionOwner` does not yet consume it. Replay backward seek therefore still restarts the isolated runtime and replays recorded input to the target frame; skipping prior input requires an explicit checkpoint-to-replay integration contract.

## P3: Replay Ownership

`BattleReplayManifest` validates replay schema, world identity, world type, effective tick rate, frame range, and sorted seek anchors before startup.

`BattleReplaySessionOwner` receives testable runtime/session creation boundaries and owns a separate local `BattleLogicSession`, replay driver, fixed-step accumulator, and `IBattleLogicSessionRegistry`. Startup failure rolls back partially created resources. Backward seek recreates only the owner runtime and replays to the target. Disposal clears owner-visible state before releasing resources and continues convergence if an individual disposal action fails.

The owner uses a non-publishing registry, so replay startup and shutdown cannot replace or clear the live battle debug facade. Loading replay through `BattleSessionFeature` does not mutate the live start plan, handles, registry, world resources, or shared `BattleContext`. A `Replay` start plan starts the isolated owner directly and does not start the live pipeline.

The live replay subfeature is record-only: it may initialize a `FrameRecordWriter` in `Record` mode, but cannot assign a replay driver to live `BattleSessionHandles`. Replay drivers are owner-owned.

## Presentation Boundary

The current replay owner is logic-only. `TryLoad(path, renderPresentation: true, ...)` is rejected because the view runtime still has one shared `BattleContext` and presentation resource set. Isolated replay rendering requires a separate context/presentation factory and explicit output routing; it must not stop the live session or rebind the live context.

## Verification

The runtime and runtime-test .NET projects build successfully with single-node MSBuild. Single-node execution is required in this workspace because a parallel build previously lost an MSBuild child node with `MSB4166`; source compilation itself reported no errors.

Focused .NET verification passed 13 of 13 P1 tests covering replication diagnostics/reset, reliable-event epoch behavior, retention gaps, monotonic acknowledgements, health hysteresis/reset, and replay manifest contracts. The deterministic checkpoint suite passed 4 of 4 P2 tests covering ordered capture/stable hash, equivalent restore, transactional rollback, and world-identity rejection.

The combined Unity 2022.3.62f1 EditMode gate produced a non-empty 17,543-byte NUnit XML result and passed 20 of 20 tests with zero failures and zero skips. It covers `SessionOrchestratorLifecycleTests`, `BattleReplaySessionOwnerTests`, `FrameReplayDriverTests`, and `BattleLogicSessionRegistryIsolationTests`. The previously focused XML evidence also records 10 of 10 P0 lifecycle tests and 5 of 5 P3 owner tests.

The .NET project intentionally does not compile the Unity session-feature graph, so Unity editor compilation and a non-empty NUnit XML remain required integration gates. The current focused lifecycle tests inject host-contract failures; end-to-end fault injection for concrete gateway/world teardown remains a residual boundary.
