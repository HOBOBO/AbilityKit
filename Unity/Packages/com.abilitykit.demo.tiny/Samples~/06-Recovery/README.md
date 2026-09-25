# 06 Recovery

`TinyRecoveryExample.Run()` intentionally exhausts frame history, observes `NeedsFullSnapshot`, restores an authoritative state, and verifies that frame prediction resumes with the same hash. It uses only the public Tiny rule and frame-sync APIs.

The Unity battle session also keeps a pending create command ID across a timeout and an empty room restore. A retry with the original sync mode addresses the same server room even when the first response was lost. A restored active room clears the pending create attempt; a closed room rejects replay of its old create ID.

From the repository root, run `dotnet run --project src/AbilityKit.Demo.Tiny.ChapterSamples` for the importable example. The independent `RecoverySample` can run `history`, `overflow`, or `mismatch` separately without a server. Run `./tools/verify-tiny-starter.ps1 -SkipUnity` for the complete chapter: `recovery-sample.log` records all three local faults and a two-client disconnect and full restore. The overflow and mismatch drivers reuse the production Room and Tiny replication sources; the disconnect driver uses the production battle session.
