using AbilityKit.Demo.Tiny;
using AbilityKit.Demo.Tiny.Client;
using AbilityKit.Demo.Tiny.Samples;
using AbilityKit.Network.Room;

if (args.Length is < 2 or > 3 || !int.TryParse(args[1], out var port) ||
    port is < 1 or > 65535)
{
    Console.Error.WriteLine(
        "Usage: dotnet run --project src/AbilityKit.Demo.Tiny.StateSample -- <host> <gateway-port> [account-prefix]");
    return 2;
}

var host = args[0];
var prefix = args.Length == 3 ? args[2] : $"tiny-state-{Guid.NewGuid():N}";
using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(40));
try
{
    using var alice = await TinyNetworkClient.ConnectAsync(
        host, port, $"{prefix}-alice", timeout.Token);
    using var bob = await TinyNetworkClient.ConnectAsync(
        host, port, $"{prefix}-bob", timeout.Token);

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

    while (!alice.TryGetFullSnapshot(battle.WorldId, out _) ||
           !bob.TryGetFullSnapshot(battle.WorldId, out _))
    {
        timeout.Token.ThrowIfCancellationRequested();
        await Task.Delay(25, timeout.Token);
    }

    var playerId = battle.Players.Single(player => player.AccountId == alice.AccountId).PlayerId;
    var targetId = battle.Players.Single(player => player.AccountId == bob.AccountId).PlayerId;
    var submitted = await alice.SubmitAsync(battle.BattleId, battle.WorldId, playerId,
        new TinyInput(1, 0, true), sequence: 1, timeout.Token);
    Require(submitted.Success, $"Submit input: {submitted.Message}");

    while (true)
    {
        timeout.Token.ThrowIfCancellationRequested();
        var through = Math.Min(alice.LatestFrame, bob.LatestFrame);
        for (var frame = submitted.AcceptedFrame; frame <= through; frame++)
        {
            if (!alice.TryGetSnapshot(frame, out var a) ||
                !bob.TryGetSnapshot(frame, out var b) ||
                a.WorldId != battle.WorldId || b.WorldId != battle.WorldId ||
                a.Actors?.Any(actor => actor.ActorId == targetId) != true ||
                b.Actors?.Any(actor => actor.ActorId == targetId) != true) continue;

            var aTarget = TinyStateExample.ReadActor(in a, targetId);
            var bTarget = TinyStateExample.ReadActor(in b, targetId);
            var aPlayer = TinyStateExample.ReadActor(in a, playerId);
            var bPlayer = TinyStateExample.ReadActor(in b, playerId);
            if (aTarget.Hp != 90 || bTarget.Hp != 90) continue;
            Require(a.Frame == b.Frame && aPlayer.X == 0 && bPlayer.X == 0,
                "Client snapshots disagree on the authoritative result.");
            Console.WriteLine($"Tiny State sample passed: room={roomId}, baseline=full, " +
                $"frame={frame}, clients=2, targetHp={aTarget.Hp}");
            return 0;
        }
        await Task.Delay(25, timeout.Token);
    }
}
catch (Exception exception)
{
    Console.Error.WriteLine($"Tiny State sample failed: {exception}");
    return 1;
}

static void Require(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}
