# Shooter Authoritative Remote Client Hot Path

## Problem

The multiplayer client previously created the same gameplay scenario as the
server. At 512, 2048, or 8192 active enemies this caused the client to run
enemy waves, enemy movement, enemy combat, and RVO in parallel with the server.
The resulting local poses could then be overwritten by authoritative PureState
updates, which appeared as latency and visible pulling.

## Current design

For `AuthoritativeInterpolation`, `BatchStateSync`, and `MassBattleLodSync`,
`ShooterGameplayScenarioWorldHostFactory.CreateBattleWorld` injects
`ShooterEnemySimulationOverride(false)`. The local world keeps player input,
prediction, arena clamping, and snapshot presentation support, while enemy
simulation remains server-only. AOI PureState deltas still create and update
the visible remote entities.

The same profiles default to
`ShooterClientPredictionBufferOptions.Disabled`. Input history, rollback
snapshots, and state-hash history are still available when explicitly supplied,
but the normal authoritative client no longer allocates the 240-frame rings.
Per-tick result hashes are disabled in this mode; snapshot import and resync
paths continue to compute hashes when reconciliation requires them.

PureState lifecycle batches are ordered before rendering. A later spawn removes
a queued despawn for the same key. A later despawn removes the queued entity
and its transforms. This makes the final projection deterministic when several
spawn/despawn deltas arrive during one Unity frame.

## Verification

- Shooter Runtime: `543/543` passing.
- Focused authoritative interpolation, assembly-options, and lightweight-client
  world tests: `40/40` passing.
- Existing snapshot allocation diagnostics remain passing; transient PureState
  serialization remains allocation-free after warmup.

## Follow-up measurement

Use the Unity multiplayer performance collector to compare `LauncherTick`,
`SessionTick`, `ViewRender`, and GC/frame before and after this change at 512,
2048, and 8192 entities. The expected result is that the client `SessionTick`
and `LauncherTick` no longer contain a second enemy/RVO simulation, while the
remote view continues to receive the same AOI payload and lifecycle events.
