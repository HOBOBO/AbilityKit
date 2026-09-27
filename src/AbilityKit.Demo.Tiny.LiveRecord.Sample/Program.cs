using System.Text.Json;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host;
using AbilityKit.Core.Recording.FrameRecord;
using AbilityKit.Demo.Tiny;
using AbilityKit.Demo.Tiny.Client;
using AbilityKit.Demo.Tiny.Samples;
using AbilityKit.Network.Room;
using AbilityKit.Protocol.Room;

if (args.Length is < 4 or > 5 || !int.TryParse(args[1], out var port))
{
    Console.Error.WriteLine("Usage: dotnet run --project src/AbilityKit.Demo.Tiny.LiveRecord.Sample -- <host> <port> <account-prefix> <record.bin> [evidence.json]");
    return 2;
}

using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(60));
var stage = "connect";
TinyNetworkClient? observedOwner = null;
TinyNetworkClient? observedGuest = null;
try
{
    using var owner = await TinyNetworkClient.ConnectAsync(
        args[0], port, args[2] + "-owner", deadline.Token);
    using var guest = await TinyNetworkClient.ConnectAsync(
        args[0], port, args[2] + "-guest", deadline.Token);
    observedOwner = owner;
    observedGuest = guest;
    stage = "start State room";
    var roomId = await TinyRoomExample.CreateRoomAsync(owner.Rooms, owner.SessionToken,
        "dev", "local", Guid.NewGuid().ToString("N"), deadline.Token);
    await TinyRoomExample.JoinAndReadyAsync(owner.Rooms, owner.SessionToken,
        "dev", "local", roomId, deadline.Token);
    await TinyRoomExample.JoinAndReadyAsync(guest.Rooms, guest.SessionToken,
        "dev", "local", roomId, deadline.Token);
    var battle = await TinyRoomExample.StartBattleAsync(owner.Rooms, owner.SessionToken,
        guest.Rooms, guest.SessionToken, roomId, deadline.Token);
    TinyStateExample.RequireStateCapability(battle);
    await TinyStateExample.SubscribeAsync(owner.Rooms, owner.SessionToken,
        roomId, battle.BattleId, deadline.Token);
    await TinyStateExample.SubscribeAsync(guest.Rooms, guest.SessionToken,
        roomId, battle.BattleId, deadline.Token);
    var ownerId = battle.Players.Single(player => player.AccountId == owner.AccountId).PlayerId;
    var guestId = battle.Players.Single(player => player.AccountId == guest.AccountId).PlayerId;

    stage = "find shared baseline";
    WireStateSyncSnapshotPush baseline;
    while (true)
    {
        deadline.Token.ThrowIfCancellationRequested();
        var through = Math.Min(owner.LatestFrame, guest.LatestFrame);
        baseline = default;
        for (var frame = through; frame >= Math.Max(1, through - 127); frame--)
        {
            if (!TrySharedSnapshot(owner, guest, battle.WorldId, frame,
                    out var shared) || !shared.IsFullSnapshot) continue;
            baseline = shared;
            break;
        }
        if (baseline.Payload != null) break;
        await Task.Delay(25, deadline.Token);
    }
    var initial = TinyBattleStateCodec.Decode(baseline.Payload!);
    var initialOwner = initial.Actors.Single(actor => actor.PlayerId == ownerId);
    var initialGuest = initial.Actors.Single(actor => actor.PlayerId == guestId);
    var snapshots = new SortedDictionary<int, WireStateSyncSnapshotPush>();
    snapshots.Add(baseline.Frame, baseline);
    var actions = new List<(string Name, uint PlayerId, TinyInput Input, int Accepted, int Effective)>();
    var sequence = new (string Name, TinyNetworkClient Client, uint PlayerId, TinyInput Input, ulong CommandSequence)[]
    {
        ("owner-attack", owner, ownerId, new TinyInput(1, 0, true), 1),
        ("guest-counter", guest, guestId, new TinyInput(0, 0, true), 1),
        ("owner-move", owner, ownerId, new TinyInput(1, 0, false), 2),
        ("guest-move", guest, guestId, new TinyInput(-1, 0, false), 2),
        ("owner-second-attack", owner, ownerId, new TinyInput(0, 0, true), 3),
        ("guest-second-counter", guest, guestId, new TinyInput(0, 0, true), 3)
    };
    var lastObservedFrame = baseline.Frame;
    var checkpointFrames = new List<int>();
    foreach (var action in sequence)
    {
        if (action.Name is "owner-second-attack" or "guest-second-counter")
            while (Math.Min(owner.LatestFrame, guest.LatestFrame) <
                   lastObservedFrame + TinyBattle.AttackCooldownFrames + 2)
            {
                deadline.Token.ThrowIfCancellationRequested();
                await Task.Delay(25, deadline.Token);
            }
        stage = "submit " + action.Name;
        var submission = await action.Client.SubmitAsync(battle.BattleId, battle.WorldId,
            action.PlayerId, action.Input, action.CommandSequence, deadline.Token);
        Require(submission.Success && submission.AcceptedFrame >= lastObservedFrame,
            $"{action.Name} was not scheduled after the previous result: " +
            $"accepted={submission.AcceptedFrame}, previous={lastObservedFrame}, " +
            $"status={submission.Status}, message={submission.Message}.");
        var accepted = submission.AcceptedFrame;
        var effective = accepted + 1;
        actions.Add((action.Name, action.PlayerId, action.Input, accepted, effective));
        stage = "collect " + action.Name;
        while (true)
        {
            deadline.Token.ThrowIfCancellationRequested();
            var through = Math.Min(owner.LatestFrame, guest.LatestFrame);
            for (var frame = lastObservedFrame + 1; frame <= through; frame++)
                if (!snapshots.ContainsKey(frame) &&
                    TrySharedSnapshot(owner, guest, battle.WorldId, frame, out var shared))
                    snapshots.Add(frame, shared);
            if (snapshots.TryGetValue(lastObservedFrame, out var before) &&
                snapshots.Values.LastOrDefault(snapshot => snapshot.Frame >= effective) is
                    { Payload: not null } after)
            {
                var beforeState = TinyBattleStateCodec.Decode(before.Payload!);
                var afterState = TinyBattleStateCodec.Decode(after.Payload!);
                var beforeOwner = beforeState.Actors.Single(actor => actor.PlayerId == ownerId);
                var beforeGuest = beforeState.Actors.Single(actor => actor.PlayerId == guestId);
                var afterOwner = afterState.Actors.Single(actor => actor.PlayerId == ownerId);
                var afterGuest = afterState.Actors.Single(actor => actor.PlayerId == guestId);
                if (action.Input.Attack)
                {
                    var targetBefore = action.PlayerId == ownerId ? beforeGuest : beforeOwner;
                    var targetAfter = action.PlayerId == ownerId ? afterGuest : afterOwner;
                    Require(targetAfter.Hp == targetBefore.Hp - TinyBattle.AttackDamage,
                        $"{action.Name} did not damage its target on the effective frame.");
                }
                else
                {
                    var actorBefore = action.PlayerId == ownerId ? beforeOwner : beforeGuest;
                    var actorAfter = action.PlayerId == ownerId ? afterOwner : afterGuest;
                    Require(actorAfter.X == actorBefore.X + action.Input.MoveX &&
                            afterOwner.Hp == beforeOwner.Hp && afterGuest.Hp == beforeGuest.Hp,
                        $"{action.Name} did not change only its position on the effective frame.");
                }
                lastObservedFrame = after.Frame;
                break;
            }
            await Task.Delay(25, deadline.Token);
        }
        if (actions.Count == 2 || actions.Count == 4)
            checkpointFrames.Add(lastObservedFrame);
    }

    stage = "write record";
    var codec = new FrameRecordBinaryCodec();
    using (var writer = codec.CreateWriter(args[3], new FrameRecordMeta
    {
        WorldId = battle.WorldId.ToString(), WorldType = TinySyncTemplates.WorldType,
        TickRate = TinySyncSettings.TickRate, PlayerId = ownerId.ToString(),
        StartedAtUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
    }))
    {
        writer.AppendSnapshot(baseline.Frame, TinyBattleStateCodec.PayloadOpCode,
            baseline.Payload!);
        foreach (var checkpointFrame in checkpointFrames)
            writer.AppendSnapshot(checkpointFrame, TinyBattleStateCodec.PayloadOpCode,
                snapshots[checkpointFrame].Payload!);
        foreach (var action in actions)
        {
            var command = new PlayerInputCommand(new FrameIndex(action.Effective),
                new PlayerId(action.PlayerId.ToString()), TinyBattle.InputOpCode,
                action.Input.Encode());
            writer.Append(in command);
        }
        foreach (var snapshot in snapshots.Values.Where(snapshot => snapshot.Frame > baseline.Frame))
        {
            var authority = new TinyBattle();
            authority.RestoreState(TinyBattleStateCodec.Decode(snapshot.Payload!));
            writer.AppendStateHash(snapshot.Frame, 1, authority.ComputeHash());
        }
    }

    stage = "replay from baseline and checkpoints";
    var record = codec.Load(args[3]);
    Require(record.Inputs.Count == sequence.Length && record.Snapshots.Count == 3 &&
            record.StateHashes.Count == snapshots.Count - 1 &&
            record.Meta.WorldId == battle.WorldId.ToString(),
        "Live Tiny record tracks are incomplete.");
    var baselineHash = ReplayFrom(record, baseline.Frame);
    var checkpointHashes = checkpointFrames.Select(frame => ReplayFrom(record, frame)).ToArray();
    Require(checkpointHashes.All(hash => hash == baselineHash),
        "Checkpoint replay diverged from baseline replay.");
    var final = TinyBattleStateCodec.Decode(snapshots[lastObservedFrame].Payload!);
    Require(final.Actors.Single(actor => actor.PlayerId == ownerId).Hp == 80 &&
            final.Actors.Single(actor => actor.PlayerId == guestId).Hp == 80 &&
            final.Actors.Single(actor => actor.PlayerId == ownerId).X == initialOwner.X + 2,
        "Live result lost an attack or movement.");
    var evidencePath = args.Length == 5 ? args[4] : Path.ChangeExtension(args[3], ".json");
    File.WriteAllText(evidencePath, JsonSerializer.Serialize(new
    {
        passed = true, roomId, worldId = battle.WorldId,
        baselineFrame = baseline.Frame, checkpointFrames, finalFrame = lastObservedFrame,
        inputCount = record.Inputs.Count, snapshotCount = record.Snapshots.Count,
        hashCount = record.StateHashes.Count, baselineHash, checkpointHashes,
        ownerHp = final.Actors.Single(actor => actor.PlayerId == ownerId).Hp,
        guestHp = final.Actors.Single(actor => actor.PlayerId == guestId).Hp,
        actions = actions.Select(action => new
        {
            name = action.Name, playerId = action.PlayerId,
            acceptedFrame = action.Accepted, effectiveFrame = action.Effective
        }).ToArray()
    }, new JsonSerializerOptions { WriteIndented = true }));
    Console.WriteLine($"Tiny live Record/Replay passed: room={roomId}, inputs={sequence.Length}, " +
        $"checkpoints={string.Join(',', checkpointFrames)}, final={lastObservedFrame}, hash={baselineHash}");
    return 0;
}
catch (Exception exception)
{
    Console.Error.WriteLine($"Tiny live Record/Replay failed at {stage} " +
        $"(ownerFrame={observedOwner?.LatestFrame}, guestFrame={observedGuest?.LatestFrame}): {exception}");
    return 1;
}

static uint ReplayFrom(FrameRecordFile record, int startFrame)
{
    var source = record.Snapshots.Single(snapshot => snapshot.Frame == startFrame);
    Require(source.OpCode == TinyBattleStateCodec.PayloadOpCode,
        "Unexpected Tiny snapshot opcode.");
    var replay = new TinyBattle();
    replay.RestoreState(TinyBattleStateCodec.Decode(Convert.FromBase64String(source.PayloadBase64)));
    var inputs = record.Inputs.ToDictionary(input => input.Frame);
    foreach (var expected in record.StateHashes.Where(hash => hash.Frame > startFrame))
    {
        Require(expected.Frame > replay.Frame && expected.Version == 1,
            "Live record hash frames are not increasing.");
        while (replay.Frame < expected.Frame)
        {
            if (inputs.TryGetValue(replay.Frame + 1, out var command))
            {
                var parsed = uint.TryParse(command.PlayerId, out var playerId);
                Require(command.OpCode == TinyBattle.InputOpCode && parsed,
                    "Live record contains an unsupported command.");
                replay.Submit(playerId, TinyInput.Decode(Convert.FromBase64String(command.PayloadBase64)));
            }
            replay.Tick();
        }
        Require(replay.ComputeHash() == expected.Hash,
            $"Live Tiny replay diverged at frame {expected.Frame}: " +
            $"expected={expected.Hash}, actual={replay.ComputeHash()}.");
    }
    Require(replay.Frame == record.StateHashes.Last().Frame,
        "Live replay ended before the final authoritative frame.");
    return replay.ComputeHash();
}

static bool TrySharedSnapshot(TinyNetworkClient owner, TinyNetworkClient guest,
    ulong worldId, int frame, out WireStateSyncSnapshotPush shared)
{
    if (owner.TryGetSnapshot(frame, out var authority) &&
        guest.TryGetSnapshot(frame, out var peer) &&
        authority.WorldId == worldId && peer.WorldId == worldId &&
        authority.PayloadOpCode == TinyBattleStateCodec.PayloadOpCode &&
        peer.PayloadOpCode == TinyBattleStateCodec.PayloadOpCode &&
        authority.Payload != null && peer.Payload != null &&
        authority.Payload.AsSpan().SequenceEqual(peer.Payload))
    {
        shared = authority;
        return true;
    }
    shared = default;
    return false;
}

static void Require(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}
