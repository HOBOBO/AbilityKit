using System.Text.Json;
using AbilityKit.Demo.Tiny.Client;
using AbilityKit.Demo.Tiny.Samples;
using AbilityKit.Demo.Tiny.Turn;
using AbilityKit.Network.Room;
using AbilityKit.Protocol.Room;

if (args.Length != 5 || !int.TryParse(args[1], out var port) ||
    args[3] is not ("owner" or "guest"))
{
    Console.Error.WriteLine("Usage: dotnet run --project src/AbilityKit.Demo.Tiny.Turn.StateSample -- <host> <port> <account-prefix> <owner|guest> <evidence-directory>");
    return 2;
}

using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(90));
try
{
    Directory.CreateDirectory(args[4]);
    if (args[3] == "owner") await RunOwnerAsync(args[0], port, args[2], args[4], deadline.Token);
    else await RunGuestAsync(args[0], port, args[2], args[4], deadline.Token);
    return 0;
}
catch (Exception exception)
{
    Console.Error.WriteLine($"Tiny Turn {args[3]} failed: {exception}");
    return 1;
}

static async Task RunOwnerAsync(string host, int port, string prefix,
    string directory, CancellationToken token)
{
    using var owner = await TinyNetworkClient.ConnectAsync(host, port, prefix + "-owner", token);
    var created = await owner.Rooms.CreateRoomAsync(new RoomGatewayCreateRequest(
        owner.SessionToken, "dev", "local", TinyTurnBattle.RoomType, "Tiny Turn", false, 2,
        commandId: Guid.NewGuid().ToString("N")), cancellationToken: token);
    Require(created.Success && !string.IsNullOrEmpty(created.RoomId), "Create turn room failed.");
    var roomId = created.RoomId!;
    var joined = await owner.Rooms.JoinRoomAsync(new RoomGatewayJoinRequest(
        owner.SessionToken, "dev", "local", roomId), cancellationToken: token);
    Require(joined.Success, "Owner could not join turn room.");
    Write(directory, "room.json", new RoomNotice(roomId));
    await WaitFileAsync(directory, "guest-ready.json", token);
    var ready = await owner.Rooms.SetReadyAsync(new RoomGatewayReadyRequest(
        owner.SessionToken, roomId, true), cancellationToken: token);
    Require(ready.Success, "Owner ready failed.");
    var flow = new RoomGatewaySessionFlow(owner.Rooms);
    var loading = await flow.BeginLoadingAsync(new RoomGatewayBeginLoadingRequest(
        owner.SessionToken, roomId, null, Guid.NewGuid().ToString("N")),
        cancellationToken: token);
    Require(loading.Success && loading.Snapshot != null, "Turn loading did not start.");
    var manifest = loading.Snapshot!;
    Write(directory, "loading.json", new LoadingNotice(manifest.LaunchGeneration,
        manifest.LaunchManifestVersion, manifest.LaunchManifestHash));
    var loaded = await owner.Rooms.ReportAssetsLoadedAsync(new RoomGatewayReportAssetsLoadedRequest(
        owner.SessionToken, roomId, manifest.LaunchGeneration,
        manifest.LaunchManifestVersion, manifest.LaunchManifestHash,
        Guid.NewGuid().ToString("N")), cancellationToken: token);
    Require(loaded.Success, "Owner loading acknowledgement failed.");
    var battle = await WaitBattleAsync(flow, owner.SessionToken, roomId, token);
    await TinyStateExample.SubscribeAsync(owner.Rooms, owner.SessionToken,
        roomId, battle.BattleId, token);
    var ownerId = PlayerId(battle, owner.AccountId);
    Require(ownerId == 1, "Owner turn slot is not first.");
    var baseline = await WaitStateAsync(owner, battle.WorldId, 0,
        state => state.CurrentPlayerId == ownerId && state.Turn == 0, token);
    await WaitFileAsync(directory, "rejected.json", token);

    var first = await SubmitAsync(owner, battle.BattleId, battle.WorldId, ownerId, 1, token);
    Require(first.Success, "Owner's first action was not queued.");
    await WaitStateAsync(owner, battle.WorldId, baseline.Frame,
        state => state.Turn == 1 && state.CurrentPlayerId == 2 && state.PlayerTwoHp == 1, token);
    Write(directory, "owner-attack.json", new { accepted = true });
    await WaitFileAsync(directory, "guest-attack.json", token);
    var second = await WaitStateAsync(owner, battle.WorldId, baseline.Frame,
        state => state.Turn == 2 && state.CurrentPlayerId == 1 && state.PlayerOneHp == 1, token);
    var final = await SubmitAsync(owner, battle.BattleId, battle.WorldId, ownerId, 2, token);
    Require(final.Success, "Owner's winning action was not queued.");
    var result = await WaitStateAsync(owner, battle.WorldId, second.Frame,
        state => state.Turn == 3 && state.WinnerId == ownerId &&
                 state.PlayerTwoHp == 0 && state.CurrentPlayerId == 0, token);
    var rejectedAfterFinish = await SubmitAsync(owner, battle.BattleId,
        battle.WorldId, ownerId, 3, token);
    Require(!rejectedAfterFinish.Success, "Finished battle accepted an action.");
    Write(directory, "owner.json", new TurnEvidence("owner", Environment.ProcessId,
        roomId, battle.BattleId, result.Frame, result.Turn, result.WinnerId,
        result.PlayerOneHp, result.PlayerTwoHp, true, false));
}

static async Task RunGuestAsync(string host, int port, string prefix,
    string directory, CancellationToken token)
{
    await WaitFileAsync(directory, "room.json", token);
    var roomId = Read<RoomNotice>(directory, "room.json").RoomId;
    var guest = await TinyNetworkClient.ConnectAsync(host, port, prefix + "-guest", token);
    try
    {
        var joined = await guest.Rooms.JoinRoomAsync(new RoomGatewayJoinRequest(
            guest.SessionToken, "dev", "local", roomId), cancellationToken: token);
        Require(joined.Success, "Guest could not join turn room.");
        var ready = await guest.Rooms.SetReadyAsync(new RoomGatewayReadyRequest(
            guest.SessionToken, roomId, true), cancellationToken: token);
        Require(ready.Success, "Guest ready failed.");
        Write(directory, "guest-ready.json", new { ready = true });
        await WaitFileAsync(directory, "loading.json", token);
        var manifest = Read<LoadingNotice>(directory, "loading.json");
        var loaded = await guest.Rooms.ReportAssetsLoadedAsync(new RoomGatewayReportAssetsLoadedRequest(
            guest.SessionToken, roomId, manifest.Generation, manifest.Version,
            manifest.Hash, Guid.NewGuid().ToString("N")), cancellationToken: token);
        Require(loaded.Success, "Guest loading acknowledgement failed.");
        var battle = await WaitBattleAsync(new RoomGatewaySessionFlow(guest.Rooms),
            guest.SessionToken, roomId, token);
        await TinyStateExample.SubscribeAsync(guest.Rooms, guest.SessionToken,
            roomId, battle.BattleId, token);
        var guestId = PlayerId(battle, guest.AccountId);
        Require(guestId == 2, "Guest turn slot is not second.");
        var baseline = await WaitStateAsync(guest, battle.WorldId, 0,
            state => state.Turn == 0 && state.CurrentPlayerId == 1, token);
        var premature = await SubmitAsync(guest, battle.BattleId, battle.WorldId, guestId, 1, token);
        Require(!premature.Success, "Out-of-turn action was accepted.");
        Write(directory, "rejected.json", new { rejected = true });
        await WaitFileAsync(directory, "owner-attack.json", token);
        await WaitStateAsync(guest, battle.WorldId, baseline.Frame,
            state => state.Turn == 1 && state.CurrentPlayerId == guestId, token);

        guest.Dispose();
        using var restored = await TinyNetworkClient.ConnectAsync(
            host, port, prefix + "-guest", token);
        var restore = await restored.Rooms.RestoreRoomAsync(new RoomGatewayRestoreRoomRequest(
            restored.SessionToken, "dev", "local"), cancellationToken: token);
        Require(restore.Success && restore.IsInBattle && restore.BattleId == battle.BattleId,
            "Guest did not restore the same turn battle.");
        await TinyStateExample.SubscribeAsync(restored.Rooms, restored.SessionToken,
            roomId, battle.BattleId, token);
        var recovered = await WaitStateAsync(restored, battle.WorldId, 0,
            state => state.Turn == 1 && state.CurrentPlayerId == guestId &&
                     state.PlayerTwoHp == 1, token);
        var action = await SubmitAsync(restored, battle.BattleId, battle.WorldId,
            guestId, 2, token);
        Require(action.Success, "Guest action after reconnect was not queued.");
        await WaitStateAsync(restored, battle.WorldId, recovered.Frame,
            state => state.Turn == 2 && state.PlayerOneHp == 1, token);
        Write(directory, "guest-attack.json", new { accepted = true });
        var result = await WaitStateAsync(restored, battle.WorldId, recovered.Frame,
            state => state.WinnerId == 1 && state.Turn == 3, token);
        Write(directory, "guest.json", new TurnEvidence("guest", Environment.ProcessId,
            roomId, battle.BattleId, result.Frame, result.Turn, result.WinnerId,
            result.PlayerOneHp, result.PlayerTwoHp, true, true));
    }
    finally { guest.Dispose(); }
}

static async Task<RoomGatewaySnapshot> WaitBattleAsync(RoomGatewaySessionFlow flow,
    string token, string roomId, CancellationToken cancellationToken)
{
    var result = await flow.WaitForBattleStartAsync(token, roomId,
        TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(20), cancellationToken);
    Require(result.Success && result.Snapshot != null, "Turn battle did not start.");
    TinyStateExample.RequireStateCapability(result.Snapshot!);
    return result.Snapshot!;
}

static uint PlayerId(RoomGatewaySnapshot room, string accountId) =>
    room.Players.Single(player => player.AccountId == accountId).PlayerId;

static async Task<WireSubmitBattleInputRes> SubmitAsync(TinyNetworkClient client,
    string battleId, ulong worldId, uint playerId, ulong sequence, CancellationToken token) =>
    await client.WireRooms.SubmitBattleInputAsync(new WireSubmitBattleInputReq
    {
        SessionToken = client.SessionToken, BattleId = battleId, WorldId = worldId,
        Frame = 0, PlayerId = playerId, InputOpCode = TinyTurnBattle.InputOpCode,
        Payload = new byte[] { 1 }, CommandSequence = sequence
    }, TimeSpan.FromSeconds(10), token);

static async Task<TinyTurnState> WaitStateAsync(TinyNetworkClient client, ulong worldId,
    int afterFrame, Func<TinyTurnState, bool> predicate, CancellationToken token)
{
    while (true)
    {
        token.ThrowIfCancellationRequested();
        var frame = client.LatestFrame;
        if (frame > afterFrame && client.TryGetSnapshot(frame, out var snapshot) &&
            snapshot.WorldId == worldId && snapshot.IsFullSnapshot &&
            snapshot.PayloadOpCode == TinyTurnBattle.SnapshotOpCode && snapshot.Payload != null)
        {
            var state = TinyTurnStateCodec.Decode(snapshot.Payload);
            if (predicate(state)) return state;
        }
        await Task.Delay(25, token);
    }
}

static async Task WaitFileAsync(string directory, string name, CancellationToken token)
{
    while (!File.Exists(Path.Combine(directory, name)))
    {
        token.ThrowIfCancellationRequested();
        await Task.Delay(25, token);
    }
}

static T Read<T>(string directory, string name) =>
    JsonSerializer.Deserialize<T>(File.ReadAllText(Path.Combine(directory, name)))
    ?? throw new InvalidDataException($"Missing turn evidence: {name}");

static void Write<T>(string directory, string name, T value)
{
    var path = Path.Combine(directory, name);
    var temporary = path + "." + Environment.ProcessId + ".tmp";
    File.WriteAllText(temporary, JsonSerializer.Serialize(value));
    File.Move(temporary, path, overwrite: true);
}

static void Require(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

internal sealed record RoomNotice(string RoomId);
internal sealed record LoadingNotice(long Generation, int Version, string Hash);
internal sealed record TurnEvidence(string Role, int ProcessId, string RoomId,
    string BattleId, int Frame, int Turn, uint WinnerId, int OwnerHp,
    int GuestHp, bool OutOfTurnRejected, bool Recovered);
