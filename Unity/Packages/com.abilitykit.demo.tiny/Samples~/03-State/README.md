# 03 State

`TinyStateExample.RequireStateCapability` negotiates the server's authoritative State profile, `SubscribeAsync` subscribes through the public Room API, and `ReadActor` reads a typed actor from a snapshot after the caller has accepted a full baseline. The caller owns the Gateway connection, frame cursor, and presentation updates; the formal flow is `TinyBattleSession.TryGetNewSnapshot` and `TinyActorViewModule`.

The same sample source is compiled into `src/AbilityKit.Demo.Tiny.StateSample` and `src/AbilityKit.Demo.Tiny.Client`. Run `./tools/verify-tiny-starter.ps1 -SkipUnity` from the repository root. Inspect `state-sample.log` for the full baseline and two-client authoritative result. Reconnect is covered separately by `state.log` and `session-state.log`.
