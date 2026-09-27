using System.Text.Json;
using AbilityKit.Demo.Common.Rooms;
using AbilityKit.Demo.Tiny.View;
using AbilityKit.Protocol.Room;

namespace AbilityKit.Demo.Tiny.Client;

internal static class TinyProcessSmoke
{
    private sealed record RoomNotice(string RoomId);

    internal sealed record ProcessEvidence(
        string Role, string Mode, int ProcessId, string RoomId, string BattleId,
        int AuthoritativeFrame, uint StateHash, float TargetHp, float OwnerX,
        int LocalPredictions, bool Recovered);

    public static async Task<int> RunAsync(
        string host, int port, string prefix, string command, string directory)
    {
        var parts = command.Split('-');
        if (parts.Length != 3 || parts[0] != "process" ||
            parts[1] is not ("owner" or "guest") ||
            !Enum.TryParse<TinySyncMode>(parts[2], true, out var mode)) return 2;
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(75));
        try
        {
            Directory.CreateDirectory(directory);
            if (parts[1] == "owner")
                await RunOwnerAsync(host, port, prefix, mode, directory, deadline.Token);
            else
                await RunGuestAsync(host, port, prefix, mode, directory, deadline.Token);
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"Tiny {command} failed: {exception}");
            return 1;
        }
    }

    private static async Task RunOwnerAsync(
        string host, int port, string prefix, TinySyncMode mode, string directory,
        CancellationToken token)
    {
        using var login = await TinyNetworkClient.ConnectAsync(
            host, port, prefix + "-owner", token);
        using var gateway = await TinyGatewayClient.ConnectAsync(host, port, token);
        using var session = new TinyBattleSession(Launch(host, port, login), gateway);
        await session.RestoreAsync(token);
        await session.CreateRoomAsync(mode, token);
        WriteJson(Path.Combine(directory, "room.json"), new RoomNotice(session.RoomId));
        await WaitFileAsync(Path.Combine(directory, "guest-ready.json"), token);
        await session.SetReadyAsync(token);
        await WaitUntilAsync(async () =>
        {
            await session.PollAsync(token);
            return session.CanStart;
        }, token);
        await session.BeginLoadingAsync(token);
        await WaitForBaselineAsync(session, token);

        var submission = session.SubmitInputAsync(new TinyInput(1, 0, true), token);
        var predictedBeforeConfirmation = session.Telemetry.LocalPredictions > 0;
        if (predictedBeforeConfirmation != (mode == TinySyncMode.Hybrid))
            throw new InvalidOperationException("Unexpected local prediction policy.");
        await submission;
        var observed = await WaitForAttackAsync(session, session.PlayerId,
            OtherPlayerId(session), token);
        await WaitFileAsync(Path.Combine(directory, "guest.json"), token);
        var telemetry = session.Telemetry;
        WriteJson(Path.Combine(directory, "owner.json"), new ProcessEvidence(
            "owner", mode.ToString(), Environment.ProcessId, session.RoomId,
            session.BattleId, telemetry.AuthoritativeFrame, telemetry.StateHash,
            observed.TargetHp, observed.OwnerX, telemetry.LocalPredictions, false));
    }

    private static async Task RunGuestAsync(
        string host, int port, string prefix, TinySyncMode mode, string directory,
        CancellationToken token)
    {
        var noticePath = Path.Combine(directory, "room.json");
        await WaitFileAsync(noticePath, token);
        var notice = JsonSerializer.Deserialize<RoomNotice>(
            await File.ReadAllTextAsync(noticePath, token))
            ?? throw new InvalidOperationException("Room notice is missing.");
        using var login = await TinyNetworkClient.ConnectAsync(
            host, port, prefix + "-guest", token);
        using var gateway = await TinyGatewayClient.ConnectAsync(host, port, token);
        var launch = Launch(host, port, login);
        using (var session = new TinyBattleSession(launch, gateway))
        {
            await session.RestoreAsync(token);
            await session.JoinRoomAsync(notice.RoomId, token);
            await session.SetReadyAsync(token);
            WriteJson(Path.Combine(directory, "guest-ready.json"), new { ready = true });
            await WaitForBaselineAsync(session, token);
            var observed = await WaitForAttackAsync(session, OtherPlayerId(session),
                session.PlayerId, token);
            var battleId = session.BattleId;
            session.Dispose();
            login.Dispose();

            using var restoredLogin = await TinyNetworkClient.ConnectAsync(
                host, port, prefix + "-guest", token);
            using var restoredGateway = await TinyGatewayClient.ConnectAsync(host, port, token);
            using var restored = new TinyBattleSession(
                Launch(host, port, restoredLogin), restoredGateway);
            await restored.RestoreAsync(token);
            if (restored.RoomId != notice.RoomId || restored.BattleId != battleId)
                throw new InvalidOperationException("Restored client changed battle binding.");
            var recovered = await WaitForAttackAsync(restored,
                OtherPlayerId(restored), restored.PlayerId, token);
            if (!restored.CanSubmitInput || recovered.TargetHp != observed.TargetHp)
                throw new InvalidOperationException("Restored baseline is incomplete.");
            var telemetry = restored.Telemetry;
            WriteJson(Path.Combine(directory, "guest.json"), new ProcessEvidence(
                "guest", mode.ToString(), Environment.ProcessId, restored.RoomId,
                restored.BattleId, telemetry.AuthoritativeFrame, telemetry.StateHash,
                recovered.TargetHp, recovered.OwnerX, telemetry.LocalPredictions, true));
        }
    }

    private static DemoMultiplayerLaunchRequest Launch(
        string host, int port, TinyNetworkClient login) => new(
            host, port, "dev", "local", login.AccountId, login.SessionToken,
            TimeSpan.FromSeconds(10));

    private static async Task WaitForBaselineAsync(TinyBattleSession session,
        CancellationToken token) => await WaitUntilAsync(async () =>
    {
        await session.PollAsync(token);
        session.Tick(0.025f);
        session.TryGetNewSnapshot(out _);
        return session.CanSubmitInput;
    }, token);

    private static uint OtherPlayerId(TinyBattleSession session)
    {
        if (session.Room?.Players == null)
            throw new InvalidOperationException("Battle players are missing.");
        foreach (var player in session.Room.Players)
            if (player.PlayerId != session.PlayerId) return player.PlayerId;
        throw new InvalidOperationException("The other player is missing.");
    }

    private static async Task<(float TargetHp, float OwnerX)> WaitForAttackAsync(
        TinyBattleSession session, uint ownerId, uint targetId, CancellationToken token)
    {
        while (true)
        {
            token.ThrowIfCancellationRequested();
            session.Tick(0.025f);
            if (session.TryGetNewSnapshot(out var snapshot) &&
                snapshot.Frame <= session.Telemetry.AuthoritativeFrame &&
                snapshot.Actors != null)
            {
                var target = FindActor(snapshot, targetId);
                var owner = FindActor(snapshot, ownerId);
                if (target.HasValue && owner.HasValue && target.Value.Hp == 90 &&
                    owner.Value.X == 0)
                    return (target.Value.Hp, owner.Value.X);
            }
            await Task.Delay(25, token);
        }
    }

    private static WireStateSyncActorSnapshot? FindActor(
        WireStateSyncSnapshotPush snapshot, uint playerId)
    {
        foreach (var actor in snapshot.Actors)
            if (actor.ActorId == playerId) return actor;
        return null;
    }

    private static async Task WaitUntilAsync(Func<Task<bool>> condition,
        CancellationToken token)
    {
        while (!await condition())
        {
            token.ThrowIfCancellationRequested();
            await Task.Delay(25, token);
        }
    }

    private static async Task WaitFileAsync(string path, CancellationToken token)
    {
        while (!File.Exists(path))
        {
            token.ThrowIfCancellationRequested();
            await Task.Delay(25, token);
        }
    }

    private static void WriteJson<T>(string path, T value)
    {
        var temporary = path + "." + Environment.ProcessId + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(value));
        File.Move(temporary, path, overwrite: true);
    }
}
