# 01 Logic

This sample depends only on `com.abilitykit.demo.tiny.logic`. `TinyLogicExample.Run()` creates two players, advances a tick, restores a serialized checkpoint, then compares hashes for 40 further ticks. It throws on divergence and returns the final hash on success.

From the repository root, run `dotnet run --project src/AbilityKit.Demo.Tiny.Logic.Sample` to execute the same sample without Unity.
