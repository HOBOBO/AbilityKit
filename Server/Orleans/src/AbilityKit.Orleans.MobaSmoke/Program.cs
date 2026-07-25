extern alias Gateway;

using AbilityKit.GameFramework.Network;
using AbilityKit.Network.Runtime;
using AbilityKit.Orleans.Contracts.Battle;
using AbilityKit.Orleans.Contracts.Rooms;
using AbilityKit.Orleans.Grains.Battle;
using AbilityKit.Orleans.Grains.Persistence;
using AbilityKit.Orleans.Hosting;
using AbilityKit.Protocol.Moba.Generated.GatewayFrameSync;
using AbilityKit.Protocol.Room;
using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using GatewayNetworking = Gateway::AbilityKit.Orleans.Gateway.Networking;

var tcpPort = ParseIntArgument(args, "--tcp-port", 41101, 1, 65535);
var hostOnly = HasArgument(args, "--host-only");
var clientOnly = HasArgument(args, "--client-only");
var connectPort = ParseIntArgument(args, "--connect-port", tcpPort, 1, 65535);
var hostTimeoutSeconds = ParseIntArgument(args, "--host-timeout-seconds", 180, 1, 3600);

// Client-only mode: skip local silo, connect directly to an external silo.
// Used by run_moba_multiprocess_smoke.ps1 to run independent client processes.
if (clientOnly)
{
    try
    {
        Console.WriteLine($"MOBA_SMOKE_CLIENT_ONLY connecting to {MobaSmokeConstants.HostAddress}:{connectPort}");
        var result = await RunScenarioAsync(MobaSmokeConstants.HostAddress, connectPort, TimeSpan.FromSeconds(30));
        Console.WriteLine(
            $"MOBA_SMOKE_PASSED RoomId={result.RoomId} NumericRoomId={result.NumericRoomId} " +
            $"BattleId={result.BattleId} WorldId={result.WorldId} Phase={result.Phase} " +
            $"Players={result.PlayerCount} Revision={result.RoomRevision}");
    }
    catch (Exception exception)
    {
        Console.Error.WriteLine($"MOBA_SMOKE_FAILED {exception}");
        Environment.ExitCode = 1;
    }
    return;
}
var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddAbilityKitServerOptions(builder.Configuration);
builder.Services.AddStateSyncObserverOptions(builder.Configuration);
builder.Services.AddBattleInputSecurityOptions(builder.Configuration);
builder.Logging.AddAbilityKitServerLogging(builder.Configuration, "AbilityKit.Orleans.MobaSmoke");

var storageOptions = builder.Configuration.GetAbilityKitStorageOptions();
builder.Services.AddAbilityKitGrainStateStorage(
    storageOptions.SessionStateProvider,
    storageOptions.RoomStateProvider,
    storageOptions.AllowInMemoryFallbackForUnsupportedProviders);
builder.Services.AddSingleton<ServerBattleWorldManager>(sp =>
    new ServerBattleWorldManager(sp.GetRequiredService<ILogger<ServerBattleWorldManager>>()));
builder.Services.AddShooterSmokeGateway(tcpPort);
builder.UseAbilityKitLocalOrleansSilo();

using var host = builder.Build();
using var transportCancellation = new CancellationTokenSource();
Task? transportTask = null;

try
{
    await host.StartAsync();
    var transport = host.Services.GetRequiredService<GatewayNetworking.TcpTransportServer>();
    transportTask = transport.StartAsync(transportCancellation.Token);
    await WaitForTcpAsync(MobaSmokeConstants.HostAddress, tcpPort, TimeSpan.FromSeconds(10));

    if (hostOnly)
    {
        Console.WriteLine(
            $"MOBA_SMOKE_HOST_READY Host={MobaSmokeConstants.HostAddress} Port={tcpPort} " +
            $"MaxPlayers=1 MinPlayers=1 TimeoutSeconds={hostTimeoutSeconds}");
        await Task.Delay(TimeSpan.FromSeconds(hostTimeoutSeconds));
        Console.WriteLine("MOBA_SMOKE_HOST_TIMEOUT");
    }
    else
    {
        var result = await RunScenarioAsync(MobaSmokeConstants.HostAddress, tcpPort, TimeSpan.FromSeconds(20));
        Console.WriteLine(
            $"MOBA_SMOKE_PASSED RoomId={result.RoomId} NumericRoomId={result.NumericRoomId} " +
            $"BattleId={result.BattleId} WorldId={result.WorldId} Phase={result.Phase} " +
            $"Players={result.PlayerCount} Revision={result.RoomRevision}");
    }
}
catch (Exception exception)
{
    Console.Error.WriteLine($"MOBA_SMOKE_FAILED {exception}");
    Environment.ExitCode = 1;
}
finally
{
    transportCancellation.Cancel();
    var transport = host.Services.GetService<GatewayNetworking.TcpTransportServer>();
    if (transport != null)
    {
        await transport.StopAsync();
    }

    if (transportTask != null)
    {
        try
        {
            await transportTask;
        }
        catch (OperationCanceledException)
        {
        }
    }

    await host.StopAsync();
}

static async Task<MobaSmokeResult> RunScenarioAsync(string host, int port, TimeSpan timeout)
{
    using var timeoutCts = new CancellationTokenSource(timeout);
    using var owner = new MobaSmokeClient("owner", host, port);
    using var member = new MobaSmokeClient("member", host, port);

    await owner.LoginAsync(timeoutCts.Token);
    await member.LoginAsync(timeoutCts.Token);

    var created = await owner.CreateRoomAsync(timeoutCts.Token);
    Require(created.Success, $"CreateRoom failed: {created.Message}");
    Require(!string.IsNullOrWhiteSpace(created.RoomId), "CreateRoom returned an empty room id.");
    Require(created.NumericRoomId != 0, "CreateRoom returned a zero numeric room id.");

    var joined = await member.JoinRoomAsync(created.RoomId, timeoutCts.Token);
    Require(joined.Success, $"JoinRoom failed: {joined.Message}");
    Require(joined.NumericRoomId == created.NumericRoomId, "CreateRoom and JoinRoom numeric ids differ.");

    await owner.PickHeroAsync(created.RoomId, heroId: 1001, teamId: 1, spawnPointId: 1, timeoutCts.Token);
    await member.PickHeroAsync(created.RoomId, heroId: 1002, teamId: 2, spawnPointId: 2, timeoutCts.Token);
    await owner.SetReadyAsync(created.RoomId, timeoutCts.Token);
    var readySnapshot = await member.SetReadyAsync(created.RoomId, timeoutCts.Token);
    Require(readySnapshot.Snapshot.CanStart, "Room did not become startable after both players configured loadouts and readied.");

    var loading = await owner.BeginLoadingAsync(created.RoomId, timeoutCts.Token);
    Require(loading.Success && loading.Applied, $"BeginLoading failed: {loading.Message}");
    Require(loading.Snapshot.Phase == (int)RoomPhase.Loading, $"Expected Loading phase, actual={loading.Snapshot.Phase}.");
    Require(loading.Snapshot.LaunchGeneration > 0, "BeginLoading returned an invalid launch generation.");
    Require(loading.Snapshot.LaunchManifestVersion > 0, "BeginLoading returned an invalid manifest version.");
    Require(!string.IsNullOrWhiteSpace(loading.Snapshot.LaunchManifestHash), "BeginLoading returned an empty manifest hash.");

    await owner.ReportAssetsLoadedAsync(created.RoomId, loading.Snapshot, timeoutCts.Token);
    await member.ReportAssetsLoadedAsync(created.RoomId, loading.Snapshot, timeoutCts.Token);

    WireRoomSnapshotRes final = default;
    while (!timeoutCts.IsCancellationRequested)
    {
        final = await owner.GetSnapshotAsync(created.RoomId, timeoutCts.Token);
        Require(final.Success, $"GetSnapshot failed: {final.Message}");
        if (final.Snapshot.Phase == (int)RoomPhase.InBattle)
        {
            break;
        }

        await Task.Delay(100, timeoutCts.Token);
    }

    Require(final.Snapshot.Phase == (int)RoomPhase.InBattle, $"Room did not reach InBattle. Phase={final.Snapshot.Phase}, Reason={final.Snapshot.PhaseReason}");
    Require(final.NumericRoomId == created.NumericRoomId, "Final snapshot numeric room id changed.");
    Require(!string.IsNullOrWhiteSpace(final.Snapshot.BattleId), "InBattle snapshot returned an empty battle id.");
    Require(final.Snapshot.WorldId != 0, "InBattle snapshot returned a zero world id.");
    Require(final.Snapshot.Players?.Count == 2, $"Expected two players, actual={final.Snapshot.Players?.Count ?? 0}.");

    var ownerPlayerId = ResolvePlayerId(final.Snapshot, heroId: 1001);
    var memberPlayerId = ResolvePlayerId(final.Snapshot, heroId: 1002);
    using var ownerBattle = new MobaSmokeClient("owner-battle", host, port);
    using var memberBattle = new MobaSmokeClient("member-battle", host, port);
    await ownerBattle.BindSessionAsync(owner.SessionToken, timeoutCts.Token);
    await memberBattle.BindSessionAsync(member.SessionToken, timeoutCts.Token);

    // Subscribe both battle clients to StateSync so they receive actor snapshots.
    // Must be called on the battle connection (not lobby) — the gateway binds the
    // subscription to context.ConnectionId, and pushes are keyed by accountId.
    await ownerBattle.SubscribeStateSyncAsync(final.Snapshot.BattleId, created.RoomId, timeoutCts.Token);
    await memberBattle.SubscribeStateSyncAsync(final.Snapshot.BattleId, created.RoomId, timeoutCts.Token);

    var ownerProbe = await ownerBattle.SubmitFrameInputAsync(
        final.NumericRoomId,
        final.Snapshot.WorldId,
        ownerPlayerId,
        frame: 0,
        inputOpCode: 1,
        new byte[] { 0x10 },
        timeoutCts.Token);
    var memberProbe = await memberBattle.SubmitFrameInputAsync(
        final.NumericRoomId,
        final.Snapshot.WorldId,
        memberPlayerId,
        frame: 0,
        inputOpCode: 1,
        new byte[] { 0x20 },
        timeoutCts.Token);

    var targetFrame = Math.Max(ownerProbe.ServerFrame, memberProbe.ServerFrame) + 5;
    var ownerInput = new byte[] { 0xA1, 0x01 };
    var memberInput = new byte[] { 0xB2, 0x02 };
    var ownerSubmit = await ownerBattle.SubmitFrameInputAsync(
        final.NumericRoomId,
        final.Snapshot.WorldId,
        ownerPlayerId,
        targetFrame,
        inputOpCode: 7,
        ownerInput,
        timeoutCts.Token);
    var memberSubmit = await memberBattle.SubmitFrameInputAsync(
        final.NumericRoomId,
        final.Snapshot.WorldId,
        memberPlayerId,
        targetFrame,
        inputOpCode: 8,
        memberInput,
        timeoutCts.Token);
    Require(ownerSubmit.Accepted, $"Owner frame input rejected. Reason={ownerSubmit.ReasonCode} ServerFrame={ownerSubmit.ServerFrame}.");
    Require(memberSubmit.Accepted, $"Member frame input rejected. Reason={memberSubmit.ReasonCode} ServerFrame={memberSubmit.ServerFrame}.");

    var ownerAggregated = await ownerBattle.WaitForFrameAsync(
        frame => frame.Frame == targetFrame && frame.Inputs.Length == 2,
        timeoutCts.Token);
    var memberAggregated = await memberBattle.WaitForFrameAsync(
        frame => frame.Frame == targetFrame && frame.Inputs.Length == 2,
        timeoutCts.Token);
    ValidateAggregatedFrame(ownerAggregated, final.NumericRoomId, final.Snapshot.WorldId, ownerPlayerId, memberPlayerId, ownerInput, memberInput);
    ValidateAggregatedFrame(memberAggregated, final.NumericRoomId, final.Snapshot.WorldId, ownerPlayerId, memberPlayerId, ownerInput, memberInput);

    var ownerEmpty = await ownerBattle.WaitForFrameAsync(
        frame => frame.Frame > targetFrame && frame.Inputs.Length == 0,
        timeoutCts.Token);
    var memberEmpty = await memberBattle.WaitForFrameAsync(
        frame => frame.Frame == ownerEmpty.Frame && frame.Inputs.Length == 0,
        timeoutCts.Token);
    Require(memberEmpty.Frame == ownerEmpty.Frame, "Battle clients did not receive the same continuous empty authoritative frame.");

    // === Entity-level multiplayer validation ===
    // Both clients subscribed to StateSync above. Wait up to 3 seconds for at least
    // one StateSyncPush containing 2+ actors (both player heroes), then hard-assert.
    // This closes the loop from "input aggregation" to "entity visibility": it proves
    // the server broadcasts actor snapshots containing both players' heroes and both
    // clients receive and decode them.
    var stateSyncDeadline = DateTime.UtcNow + TimeSpan.FromSeconds(3);
    while (DateTime.UtcNow < stateSyncDeadline)
    {
        if (ownerBattle.StateSyncPushCount > 0 && memberBattle.StateSyncPushCount > 0 &&
            ownerBattle.MaxActorCount >= 2 && memberBattle.MaxActorCount >= 2)
        {
            break;
        }
        await Task.Delay(100, timeoutCts.Token);
    }

    Require(ownerBattle.StateSyncPushCount > 0,
        $"Owner did not receive any StateSyncPush (opcode 9002) after SubscribeStateSync.");
    Require(memberBattle.StateSyncPushCount > 0,
        $"Member did not receive any StateSyncPush (opcode 9002) after SubscribeStateSync.");
    Require(ownerBattle.MaxActorCount >= 2,
        $"Owner StateSyncPush did not contain both player heroes. MaxActorCount={ownerBattle.MaxActorCount}, expected >= 2.");
    Require(memberBattle.MaxActorCount >= 2,
        $"Member StateSyncPush did not contain both player heroes. MaxActorCount={memberBattle.MaxActorCount}, expected >= 2.");

    Console.WriteLine(
        $"MOBA_SMOKE_ENTITY_SYNC_VERIFIED OwnerPushes={ownerBattle.StateSyncPushCount} OwnerActors={ownerBattle.MaxActorCount} " +
        $"MemberPushes={memberBattle.StateSyncPushCount} MemberActors={memberBattle.MaxActorCount}");

    return new MobaSmokeResult(
        created.RoomId,
        final.NumericRoomId,
        final.Snapshot.BattleId,
        final.Snapshot.WorldId,
        final.Snapshot.Phase,
        final.Snapshot.Players?.Count ?? 0,
        final.Snapshot.RoomRevision);
}

static uint ResolvePlayerId(WireRoomSnapshot snapshot, int heroId)
{
    var player = snapshot.Players?.SingleOrDefault(candidate => candidate.HeroId == heroId) ?? default;
    Require(player.PlayerId != 0, $"Could not resolve player id for hero {heroId}.");
    return player.PlayerId;
}

static void ValidateAggregatedFrame(
    WireFramePushedPush frame,
    ulong roomId,
    ulong worldId,
    uint ownerPlayerId,
    uint memberPlayerId,
    byte[] ownerInput,
    byte[] memberInput)
{
    Require(frame.RoomId == roomId, "Authoritative frame room id mismatch.");
    Require(frame.WorldId == worldId, "Authoritative frame world id mismatch.");
    var owner = frame.Inputs.SingleOrDefault(input => input.PlayerId == ownerPlayerId);
    var member = frame.Inputs.SingleOrDefault(input => input.PlayerId == memberPlayerId);
    Require(owner.PlayerId == ownerPlayerId && owner.OpCode == 7 && owner.Payload.SequenceEqual(ownerInput), "Owner authoritative input mismatch.");
    Require(member.PlayerId == memberPlayerId && member.OpCode == 8 && member.Payload.SequenceEqual(memberInput), "Member authoritative input mismatch.");
}

static async Task WaitForTcpAsync(string host, int port, TimeSpan timeout)
{
    using var timeoutCts = new CancellationTokenSource(timeout);
    while (!timeoutCts.IsCancellationRequested)
    {
        try
        {
            using var client = new System.Net.Sockets.TcpClient();
            await client.ConnectAsync(host, port, timeoutCts.Token);
            return;
        }
        catch when (!timeoutCts.IsCancellationRequested)
        {
            await Task.Delay(50, timeoutCts.Token);
        }
    }

    throw new TimeoutException($"TCP Gateway did not listen on {host}:{port} in time.");
}

static bool HasArgument(string[] arguments, string name)
{
    return arguments.Any(argument =>
        string.Equals(argument, name, StringComparison.OrdinalIgnoreCase));
}

static int ParseIntArgument(
    string[] arguments,
    string name,
    int fallback,
    int minimum,
    int maximum)
{
    for (var i = 0; i + 1 < arguments.Length; i++)
    {
        if (string.Equals(arguments[i], name, StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(arguments[i + 1], out var value) &&
            value >= minimum &&
            value <= maximum)
        {
            return value;
        }
    }

    return fallback;
}

static void Require(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

internal sealed class MobaSmokeClient : IDisposable
{
    private readonly SmokeTcpGameFrameworkNetworkChannel _channel;
    private readonly AbilityKit.Network.Abstractions.IConnection _connection;
    private readonly RequestClient _requests;
    private readonly Channel<WireFramePushedPush> _frames = Channel.CreateUnbounded<WireFramePushedPush>();
    private string _sessionToken = string.Empty;

    // StateSync push tracking (opcode 9002). Non-zero after battle start proves the
    // server is broadcasting actor snapshots through the gateway.
    internal int StateSyncPushCount => Volatile.Read(ref _stateSyncPushCount);
    internal int LastStateSyncPushSize => Volatile.Read(ref _lastStateSyncPushSize);
    internal int MaxActorCount => Volatile.Read(ref _maxActorCount);
    private int _stateSyncPushCount;
    private int _lastStateSyncPushSize;
    private int _maxActorCount;

    public string SessionToken => _sessionToken;

    public MobaSmokeClient(string name, string host, int port)
    {
        _channel = new SmokeTcpGameFrameworkNetworkChannel($"MobaSmoke-{name}");
        _connection = GameFrameworkGatewayConnectionFactory.Wrap(_channel);
        _requests = new RequestClient(_connection);
        _connection.ServerPushReceived += OnServerPushReceived;
        _connection.Open(host, port);
        _connection.Tick(0f);
    }

    public async Task LoginAsync(CancellationToken cancellationToken)
    {
        var request = new WireRoomGuestLoginReq { GuestId = $"moba-smoke-{Guid.NewGuid():N}" };
        var response = await SendAsync<WireRoomGuestLoginReq, WireRoomGuestLoginRes>(RoomGatewayOpCodes.GuestLogin, request, cancellationToken);
        if (!response.Success || string.IsNullOrWhiteSpace(response.SessionToken))
        {
            throw new InvalidOperationException($"GuestLogin failed: {response.Message}");
        }

        _sessionToken = response.SessionToken;
    }

    public async Task BindSessionAsync(string sessionToken, CancellationToken cancellationToken)
    {
        var response = await SendAsync<WireRenewSessionReq, WireRenewSessionRes>(
            RoomGatewayOpCodes.RenewSession,
            new WireRenewSessionReq
            {
                SessionToken = sessionToken,
                ExtendSeconds = 300,
                RotateToken = false
            },
            cancellationToken);
        if (!response.Success || string.IsNullOrWhiteSpace(response.AccountId))
        {
            throw new InvalidOperationException($"RenewSession failed: {response.Message}");
        }

        _sessionToken = response.SessionToken;
    }

    public async Task SubscribeStateSyncAsync(string battleId, string roomId, CancellationToken cancellationToken)
    {
        var request = new WireSubscribeStateSyncReq
        {
            SessionToken = _sessionToken,
            BattleId = battleId,
            RoomId = roomId ?? string.Empty,
            EventEpoch = string.Empty,
            LastEventAck = 0
        };
        var response = await SendAsync<WireSubscribeStateSyncReq, WireSubscribeStateSyncRes>(
            RoomGatewayOpCodes.SubscribeStateSync,
            request,
            cancellationToken);
        if (!response.Success)
        {
            throw new InvalidOperationException($"SubscribeStateSync failed: {response.Message}");
        }
    }

    public async Task<WireSubmitFrameInputRes> SubmitFrameInputAsync(
        ulong roomId,
        ulong worldId,
        uint playerId,
        int frame,
        int inputOpCode,
        byte[] payload,
        CancellationToken cancellationToken)
    {
        var request = new WireSubmitFrameInputReq(roomId, worldId, playerId, frame, inputOpCode, payload);
        var requestPayload = WireCustomBinary.Serialize(in request);
        var responsePayload = await _requests.SendRequestAsync(
            OpCodes.SubmitFrameInput,
            requestPayload,
            TimeSpan.FromSeconds(10),
            cancellationToken);
        return WireCustomBinary.DeserializeSubmitFrameInputRes(responsePayload);
    }

    public async Task<WireFramePushedPush> WaitForFrameAsync(
        Func<WireFramePushedPush, bool> predicate,
        CancellationToken cancellationToken)
    {
        while (await _frames.Reader.WaitToReadAsync(cancellationToken))
        {
            while (_frames.Reader.TryRead(out var frame))
            {
                if (predicate(frame))
                {
                    return frame;
                }
            }
        }

        throw new InvalidOperationException("Frame push channel completed before a matching frame arrived.");
    }

    public Task<WireCreateRoomRes> CreateRoomAsync(CancellationToken cancellationToken)
    {
        return SendAsync<WireCreateRoomReq, WireCreateRoomRes>(RoomGatewayOpCodes.CreateRoom, new WireCreateRoomReq
        {
            SessionToken = _sessionToken,
            Region = MobaSmokeConstants.Region,
            ServerId = MobaSmokeConstants.ServerId,
            RoomType = GameplayRoomTypes.Moba,
            Title = "MOBA headless smoke",
            IsPublic = false,
            MaxPlayers = 2,
            Tags = new Dictionary<string, string>
            {
                ["mapId"] = "1",
                ["minPlayers"] = "2",
                ["tickRate"] = "30"
            }
        }, cancellationToken);
    }

    public Task<WireJoinRoomRes> JoinRoomAsync(string roomId, CancellationToken cancellationToken)
    {
        return SendAsync<WireJoinRoomReq, WireJoinRoomRes>(RoomGatewayOpCodes.JoinRoom, new WireJoinRoomReq
        {
            SessionToken = _sessionToken,
            Region = MobaSmokeConstants.Region,
            ServerId = MobaSmokeConstants.ServerId,
            RoomId = roomId
        }, cancellationToken);
    }

    public async Task PickHeroAsync(string roomId, int heroId, int teamId, int spawnPointId, CancellationToken cancellationToken)
    {
        var response = await SendAsync<WireRoomPickHeroReq, WireRoomSnapshotRes>(RoomGatewayOpCodes.PickHero, new WireRoomPickHeroReq
        {
            SessionToken = _sessionToken,
            RoomId = roomId,
            HeroId = heroId,
            TeamId = teamId,
            SpawnPointId = spawnPointId,
            Level = 1,
            AttributeTemplateId = 100,
            BasicAttackSkillId = 200,
            SkillIds = new List<int> { 201, 202 }
        }, cancellationToken);
        if (!response.Success)
        {
            throw new InvalidOperationException($"PickHero failed: {response.Message}");
        }
    }

    public async Task<WireRoomSnapshotRes> SetReadyAsync(string roomId, CancellationToken cancellationToken)
    {
        var response = await SendAsync<WireRoomReadyReq, WireRoomSnapshotRes>(RoomGatewayOpCodes.SetReady, new WireRoomReadyReq
        {
            SessionToken = _sessionToken,
            RoomId = roomId,
            Ready = true
        }, cancellationToken);
        if (!response.Success)
        {
            throw new InvalidOperationException($"SetReady failed: {response.Message}");
        }

        return response;
    }

    public Task<WireRoomOperationRes> BeginLoadingAsync(string roomId, CancellationToken cancellationToken)
    {
        return SendAsync<WireBeginLoadingReq, WireRoomOperationRes>(RoomGatewayOpCodes.BeginLoading, new WireBeginLoadingReq
        {
            SessionToken = _sessionToken,
            RoomId = roomId,
            ExpectedRevision = null,
            CommandId = $"begin-{Guid.NewGuid():N}"
        }, cancellationToken);
    }

    public async Task ReportAssetsLoadedAsync(string roomId, WireRoomSnapshot loadingSnapshot, CancellationToken cancellationToken)
    {
        var response = await SendAsync<WireReportAssetsLoadedReq, WireRoomOperationRes>(RoomGatewayOpCodes.ReportAssetsLoaded, new WireReportAssetsLoadedReq
        {
            SessionToken = _sessionToken,
            RoomId = roomId,
            LaunchGeneration = loadingSnapshot.LaunchGeneration,
            ManifestVersion = loadingSnapshot.LaunchManifestVersion,
            ManifestHash = loadingSnapshot.LaunchManifestHash,
            CommandId = $"loaded-{Guid.NewGuid():N}"
        }, cancellationToken);
        if (!response.Success)
        {
            throw new InvalidOperationException($"ReportAssetsLoaded failed: {response.Message}");
        }
    }

    public Task<WireRoomSnapshotRes> GetSnapshotAsync(string roomId, CancellationToken cancellationToken)
    {
        return SendAsync<WireGetSnapshotReq, WireRoomSnapshotRes>(RoomGatewayOpCodes.GetSnapshot, new WireGetSnapshotReq
        {
            SessionToken = _sessionToken,
            RoomId = roomId
        }, cancellationToken);
    }

    public void Dispose()
    {
        _connection.ServerPushReceived -= OnServerPushReceived;
        _frames.Writer.TryComplete();
        _requests.Dispose();
        _connection.Dispose();
        _channel.Dispose();
    }

    private void OnServerPushReceived(uint opCode, ArraySegment<byte> payload)
    {
        if (opCode == OpCodes.FramePushed)
        {
            _frames.Writer.TryWrite(WireCustomBinary.DeserializeFramePushedPush(payload));
        }

        // Track StateSync pushes (opcode 9002) and decode actor count to verify the
        // server broadcasts actor snapshots containing both player heroes.
        // IMPORTANT: use WireRoomGatewayBinary + WireStateSyncSnapshotPush (MemoryPack),
        // NOT MobaWorldSnapshotCodec (BinaryObjectCodec) — the latter is incompatible
        // with the server's wire encoding.
        if (opCode == AbilityKit.Protocol.Moba.StateSync.OpCodes.SnapshotPushed)
        {
            Interlocked.Increment(ref _stateSyncPushCount);
            _lastStateSyncPushSize = payload.Count;

            try
            {
                var wire = WireRoomGatewayBinary.Deserialize<WireStateSyncSnapshotPush>(payload);
                var actorCount = wire.Actors?.Count ?? 0;
                // Track max actor count seen (atomsafe CAS loop)
                var currentMax = Volatile.Read(ref _maxActorCount);
                while (actorCount > currentMax)
                {
                    if (Interlocked.CompareExchange(ref _maxActorCount, actorCount, currentMax) == currentMax)
                        break;
                    currentMax = Volatile.Read(ref _maxActorCount);
                }
            }
            catch
            {
                // Decoding failure is non-fatal; the push count still proves the channel is wired.
            }
        }
    }

    private async Task<TResponse> SendAsync<TRequest, TResponse>(uint opCode, TRequest request, CancellationToken cancellationToken)
    {
        var payload = WireRoomGatewayBinary.Serialize(in request);
        var responsePayload = await _requests.SendRequestAsync(opCode, payload, TimeSpan.FromSeconds(10), cancellationToken);
        return WireRoomGatewayBinary.Deserialize<TResponse>(responsePayload);
    }
}

internal static class MobaSmokeConstants
{
    public const string HostAddress = "127.0.0.1";
    public const string Region = "local";
    public const string ServerId = "moba-smoke";
}

internal readonly record struct MobaSmokeResult(
    string RoomId,
    ulong NumericRoomId,
    string BattleId,
    ulong WorldId,
    int Phase,
    int PlayerCount,
    long RoomRevision);
