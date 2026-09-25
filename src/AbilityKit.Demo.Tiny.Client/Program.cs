using AbilityKit.Demo.Tiny;
using AbilityKit.Demo.Tiny.Client;
using AbilityKit.Demo.Tiny.Samples;
using AbilityKit.Network.Room;

if (args.Length is < 2 or > 5 || !int.TryParse(args[1], out var port))
{
    Console.Error.WriteLine("Usage: dotnet run --project src/AbilityKit.Demo.Tiny.Client -- <host> <gateway-port> [account-prefix] [state|frame|hybrid|session-state|session-frame|session-hybrid|session-hybrid-mismatch] [evidence.json]");
    return 2;
}

var host = args[0];
var prefix = args.Length >= 3 ? args[2] : $"tiny-{Guid.NewGuid():N}";
var mode = args.Length >= 4 ? args[3] : "state";
if (mode == "session-hybrid-mismatch")
{
    if (args.Length != 5) return 2;
    return await TinySessionSmoke.RunHybridMismatchAsync(host, port, prefix, args[4]);
}
if (args.Length == 5) return 2;
if (mode.StartsWith("session-", StringComparison.Ordinal))
    return await TinySessionSmoke.RunAsync(host, port, prefix, mode.Substring("session-".Length));
if (mode != "state")
    return await TinyFrameSmoke.RunAsync(host, port, prefix, mode);
using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(40));
try
{
    using var alice = await TinyNetworkClient.ConnectAsync(host, port, $"{prefix}-alice", timeout.Token);
    using var bob = await TinyNetworkClient.ConnectAsync(host, port, $"{prefix}-bob", timeout.Token);

    var roomId = await TinyRoomExample.CreateRoomAsync(
        alice.Rooms, alice.SessionToken, "dev", "local", Guid.NewGuid().ToString("N"), timeout.Token);
    await TinyRoomExample.JoinAndReadyAsync(
        alice.Rooms, alice.SessionToken, "dev", "local", roomId, timeout.Token);
    await TinyRoomExample.JoinAndReadyAsync(
        bob.Rooms, bob.SessionToken, "dev", "local", roomId, timeout.Token);

    var battle = await TinyRoomExample.StartBattleAsync(
        alice.Rooms, alice.SessionToken, bob.Rooms, bob.SessionToken, roomId, timeout.Token);
    TinyStateExample.RequireStateCapability(battle);
    await TinyStateExample.SubscribeAsync(
        alice.Rooms, alice.SessionToken, roomId, battle.BattleId, timeout.Token);
    await TinyStateExample.SubscribeAsync(
        bob.Rooms, bob.SessionToken, roomId, battle.BattleId, timeout.Token);

    var roomSnapshot = await alice.Rooms.GetSnapshotAsync(
        new RoomGatewayGetSnapshotRequest(alice.SessionToken, roomId), cancellationToken: timeout.Token);
    Require(roomSnapshot.Success && roomSnapshot.Snapshot is not null, "Room snapshot unavailable.");
    var players = roomSnapshot.Snapshot?.Players
        ?? throw new InvalidOperationException("Room players are missing.");
    var playerId = players.Single(player => player.AccountId == alice.AccountId).PlayerId;
    var targetId = players.Single(player => player.AccountId == bob.AccountId).PlayerId;

    var submitted = await alice.SubmitAsync(battle.BattleId, battle.WorldId, playerId,
        new TinyInput(1, 0, true), sequence: 1, timeout.Token);
    Require(submitted.Success, $"Submit input: {submitted.Message}");

    while (!timeout.IsCancellationRequested)
    {
        var through = Math.Min(alice.LatestFrame, bob.LatestFrame);
        for (var frame = submitted.AcceptedFrame; frame <= through; frame++)
        {
            if (!alice.TryGetSnapshot(frame, out var a) || !bob.TryGetSnapshot(frame, out var b)) continue;
            if (a.Actors?.Any(actor => actor.ActorId == targetId) != true ||
                b.Actors?.Any(actor => actor.ActorId == targetId) != true) continue;
            var aTarget = TinyStateExample.ReadActor(in a, targetId);
            var bTarget = TinyStateExample.ReadActor(in b, targetId);
            var aPlayer = TinyStateExample.ReadActor(in a, playerId);
            var bPlayer = TinyStateExample.ReadActor(in b, playerId);
            if (aTarget.Hp != 90 || bTarget.Hp != 90) continue;
            Require(a.WorldId == b.WorldId && a.Frame == b.Frame, "Client snapshots disagree.");
            Require(aPlayer.X == 0 && bPlayer.X == 0, "Client snapshots disagree on movement.");
            bob.Dispose();
            using var recovered = await TinyNetworkClient.ConnectAsync(host, port, $"{prefix}-bob", timeout.Token);
            var restore = await recovered.Rooms.RestoreRoomAsync(new RoomGatewayRestoreRoomRequest(
                recovered.SessionToken, "dev", "local"), cancellationToken: timeout.Token);
            Require(restore.Success && restore.IsInBattle && restore.BattleId == battle.BattleId,
                $"Restore room: {restore.Message}");
            await TinyStateExample.SubscribeAsync(
                recovered.Rooms, recovered.SessionToken, roomId, battle.BattleId, timeout.Token);
            while (recovered.LatestFrame < 0 && !timeout.IsCancellationRequested)
                await Task.Delay(25, timeout.Token);
            var recoveryFrame = recovered.LatestFrame;
            Require(recovered.TryGetSnapshot(recoveryFrame, out var recovery) && recovery.IsFullSnapshot,
                "Reconnect did not receive a full authoritative snapshot.");
            Require(recovery.Actors?.Single(actor => actor.ActorId == targetId).Hp == 90,
                "Recovered snapshot lost the authoritative attack result.");
            Require(recovery.Actors?.Single(actor => actor.ActorId == playerId).X == 0,
                "Recovered snapshot lost the authoritative movement result.");
            Console.WriteLine($"Tiny TCP smoke passed: room={roomId}, frame={frame}, playerX=0, targetHp={aTarget.Hp}, clients=2, reconnect=full");
            return 0;
        }

        await Task.Delay(25, timeout.Token);
    }

    throw new TimeoutException("Two clients did not receive the same authoritative attack result.");
}
catch (Exception exception)
{
    Console.Error.WriteLine($"Tiny TCP smoke failed: {exception.Message}");
    return 1;
}

static void Require(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}
