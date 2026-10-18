# MOBA client session

`BattleClientSessionDriver` owns client start, stop, and host ticks. Its
`IBattleClientSessionHost` implementation owns the concrete network and battle
resources. `BattleClientSnapshotStream` owns snapshot admission, actor state,
the bounded ingress queue, and optional presentation projections.

Network callbacks may call `QueueSnapshot` and `NotifyConnectionLost` from any
thread. `Tick`, direct presentation methods, and projection attach/detach run
on the host's presentation thread. A session may have zero projections or
multiple independent projections; attaching one rebuilds it from the current
full actor state.

Set `BattleClientSessionOptions.MaxPendingSnapshots` for the expected receive
rate. Queue overflow discards pending snapshots and requires a new full
baseline. Subscribe to `FullSnapshotRequired` to request that baseline through
the host's network SDK. The event runs on the presentation thread. A full
snapshot already queued before the next tick can satisfy recovery without an
extra request. `DiscardedPendingSnapshotCount` counts queue pressure and
connection-loss discards, not snapshots rejected by frame or baseline checks.

The package builds for .NET 8 and .NET 10 and has no Unity dependency. The
Godot sample currently exercises a fixture projection; live Godot gateway
entry still needs a .NET 8 compatible gateway SDK and an end-to-end test.
