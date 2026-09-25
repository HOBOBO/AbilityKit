# 05 Hybrid

`TinyHybridExample.Run()` predicts the local attack before confirmation, confirms the matching authoritative frame, then replays a remote attack that arrived later. This is the local rule/rollback portion of Hybrid mode; the network session and periodic full snapshots are provided by the formal Tiny runtime.

Run `dotnet run --project src/AbilityKit.Demo.Tiny.HybridSample -- 127.0.0.1 4058 my-run` with a Tiny Host and Gateway, or use `./tools/verify-tiny-starter.ps1 -SkipUnity` to start isolated services. The project compiles this UPM source and runs a two-client formal Hybrid session; inspect `hybrid-sample.log` for the local hash, authoritative frame, prediction, and remote rollback counts. `hybrid.log` retains the network hash convergence check.
