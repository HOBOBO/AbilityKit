# 04 Frame

`TinyFrameExample.Run()` predicts an empty frame, then applies a late authoritative attack. The public `TinyFrameSyncSession` restores the preceding state, replays the input, and returns `Replayed`. The sample fails if its final hash differs from the authoritative battle.

Run `dotnet run --project src/AbilityKit.Demo.Tiny.FrameSample -- 127.0.0.1 4058 my-run` with a Tiny Host and Gateway, or use `./tools/verify-tiny-starter.ps1 -SkipUnity` to start isolated services. The project compiles this UPM source and runs a two-client formal Frame session; inspect `frame-sample.log` for the local hash, authoritative frame, and rollback counts. `frame.log` retains the network hash convergence check.
