using AbilityKit.Demo.Tiny.Samples;
using AbilityKit.Network.Room;

if (args.Length is < 2 or > 3 || !int.TryParse(args[1], out var port) ||
    port is < 1 or > 65535)
{
    Console.Error.WriteLine(
        "Usage: dotnet run --project src/AbilityKit.Demo.Tiny.RoomSample -- <host> <gateway-port> [account-prefix]");
    return 2;
}

var host = args[0];
var prefix = args.Length == 3 ? args[2] : $"tiny-room-{Guid.NewGuid():N}";
using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(40));
try
{
    using var alice = await RoomGatewayConnectionSession.ConnectAsync(
        host, port, timeout: TimeSpan.FromSeconds(10), cancellationToken: timeout.Token);
    using var bob = await RoomGatewayConnectionSession.ConnectAsync(
        host, port, timeout: TimeSpan.FromSeconds(10), cancellationToken: timeout.Token);

    var owner = await alice.RoomClient.AccountLoginAsync(
        $"{prefix}-alice", kickExisting: true, TimeSpan.FromSeconds(10), timeout.Token);
    var guest = await bob.RoomClient.AccountLoginAsync(
        $"{prefix}-bob", kickExisting: true, TimeSpan.FromSeconds(10), timeout.Token);
    Require(owner.Success && guest.Success,
        $"Login: {owner.Message} / {guest.Message}");

    var createCommandId = Guid.NewGuid().ToString("N");
    var roomId = await TinyRoomExample.CreateRoomAsync(
        alice.RoomClient, owner.SessionToken, "dev", "local", createCommandId, timeout.Token);
    var replayedRoomId = await TinyRoomExample.CreateRoomAsync(
        alice.RoomClient, owner.SessionToken, "dev", "local", createCommandId, timeout.Token);
    Require(replayedRoomId == roomId, "Create retry returned another room.");
    await TinyRoomExample.JoinAndReadyAsync(
        alice.RoomClient, owner.SessionToken, "dev", "local", roomId, timeout.Token);
    await TinyRoomExample.JoinAndReadyAsync(
        bob.RoomClient, guest.SessionToken, "dev", "local", roomId, timeout.Token);
    var battle = await TinyRoomExample.StartBattleAsync(
        alice.RoomClient, owner.SessionToken, bob.RoomClient, guest.SessionToken,
        roomId, timeout.Token);

    Require(battle.Players.Count == 2, "Battle did not contain two players.");
    Require(battle.Players.Any(player => player.AccountId == owner.AccountId) &&
        battle.Players.Any(player => player.AccountId == guest.AccountId),
        "Battle player slots do not match the logged-in accounts.");
    Require(!string.IsNullOrWhiteSpace(battle.BattleId), "Battle ID is missing.");
    Console.WriteLine($"Tiny Room sample passed: room={roomId}, battle={battle.BattleId}, players=2, loading=complete");
    return 0;
}
catch (Exception exception)
{
    Console.Error.WriteLine($"Tiny Room sample failed: {exception}");
    return 1;
}

static void Require(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}
