using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host;
using AbilityKit.Core.Recording.FrameRecord;
using AbilityKit.Demo.Tiny;

if (args.Length > 1)
    throw new ArgumentException("Usage: dotnet run --project src/AbilityKit.Demo.Tiny.Record.Sample -- [output.bin]");
var path = args.Length == 1 ? args[0] : Path.Combine(Path.GetTempPath(),
    $"tiny-record-{Guid.NewGuid():N}.bin");
var removeAfterRun = args.Length == 0;
try
{
    var codec = new FrameRecordBinaryCodec();
    var battle = CreateBattle();
    using (var writer = codec.CreateWriter(path, new FrameRecordMeta
    {
        WorldId = "tiny-record-sample",
        WorldType = TinySyncTemplates.WorldType,
        TickRate = TinySyncSettings.TickRate,
        PlayerId = "1",
        StartedAtUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
    }))
    {
        writer.AppendSnapshot(0, TinyBattleStateCodec.PayloadOpCode,
            TinyBattleStateCodec.Encode(battle.CaptureState()));
        for (var frame = 1; frame <= 40; frame++)
        {
            var input = new TinyInput((sbyte)(frame == 1 ? 1 : 0), 0, frame == 1);
            var command = new PlayerInputCommand(new FrameIndex(frame), new PlayerId("1"),
                TinyBattle.InputOpCode, input.Encode());
            writer.Append(in command);
            battle.Submit(1, input);
            battle.Tick();
            writer.AppendStateHash(frame, 1, battle.ComputeHash());
        }
    }

    var record = codec.Load(path);
    if (record.Meta.WorldType != TinySyncTemplates.WorldType ||
        record.Inputs.Count != 40 || record.StateHashes.Count != 40 ||
        record.Snapshots.Count != 1)
        throw new InvalidDataException("Tiny record tracks are incomplete.");
    var baseline = record.Snapshots.Single();
    if (baseline.Frame != 0 || baseline.OpCode != TinyBattleStateCodec.PayloadOpCode)
        throw new InvalidDataException("Unexpected Tiny baseline snapshot.");
    var replay = new TinyBattle();
    replay.RestoreState(TinyBattleStateCodec.Decode(Convert.FromBase64String(
        baseline.PayloadBase64)));
    for (var index = 0; index < record.Inputs.Count; index++)
    {
        var command = record.Inputs[index];
        var expected = record.StateHashes[index];
        if (command.Frame != replay.Frame + 1 || command.PlayerId != "1" ||
            command.OpCode != TinyBattle.InputOpCode || expected.Frame != command.Frame ||
            expected.Version != 1)
            throw new InvalidDataException($"Unsupported record command at index {index}.");
        replay.Submit(1, TinyInput.Decode(Convert.FromBase64String(command.PayloadBase64)));
        replay.Tick();
        if (replay.ComputeHash() != expected.Hash)
            throw new InvalidDataException($"Replay diverged at frame {command.Frame}.");
    }
    if (replay.ComputeHash() != battle.ComputeHash() ||
        replay.CaptureState().Actors.Single(actor => actor.PlayerId == 2).Hp != 90)
        throw new InvalidDataException("Tiny replay final state diverged.");
    Console.WriteLine($"Tiny Record/Replay passed: frames={replay.Frame}, hash={replay.ComputeHash()}, path={path}");
    return 0;
}
finally
{
    if (removeAfterRun && File.Exists(path)) File.Delete(path);
}

static TinyBattle CreateBattle()
{
    var battle = new TinyBattle();
    battle.AddPlayer(1, -1, 0);
    battle.AddPlayer(2, 1, 0);
    return battle;
}
