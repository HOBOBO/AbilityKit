extern alias Gateway;

using AbilityKit.GameFramework.Network;
using AbilityKit.Network.Runtime;
using AbilityKit.Orleans.Contracts.Battle;
using AbilityKit.Orleans.Contracts.Rooms;
using AbilityKit.Orleans.Contracts.Shooter;
using AbilityKit.Orleans.Grains.Battle;
using AbilityKit.Orleans.Grains.Persistence;
using AbilityKit.Orleans.Hosting;
using AbilityKit.Protocol.Moba;
using AbilityKit.Protocol.Moba.StateSync;
using AbilityKit.Protocol.Room;
using RoomSubscribeStateSyncReq = AbilityKit.Protocol.Room.WireSubscribeStateSyncReq;
using RoomSubscribeStateSyncRes = AbilityKit.Protocol.Room.WireSubscribeStateSyncRes;
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
        var result = await RunScenarioAsync(MobaSmokeConstants.HostAddress, connectPort, TimeSpan.FromSeconds(60));
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
        var result = await RunScenarioAsync(MobaSmokeConstants.HostAddress, tcpPort, TimeSpan.FromSeconds(60));
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

    var ownerBaseline = await ownerBattle.WaitForCharacterAsync(
        teamId: 1,
        modelId: 1001,
        _ => true,
        timeoutCts.Token);
    var memberBaseline = await memberBattle.WaitForCharacterAsync(
        teamId: 1,
        modelId: 1001,
        actor => actor.ActorId == ownerBaseline.ActorId,
        timeoutCts.Token);
    Require(
        MathF.Abs(ownerBaseline.X - memberBaseline.X) < 0.001f &&
        MathF.Abs(ownerBaseline.Z - memberBaseline.Z) < 0.001f,
        "Battle clients did not observe the same owner baseline position.");

    var movePayload = MobaMoveCodec.Serialize(1f, 0f);
    var ownerSubmit = await ownerBattle.SubmitBattleInputAsync(
        final.Snapshot.BattleId,
        final.Snapshot.WorldId,
        ownerPlayerId,
        frame: 0,
        inputOpCode: MobaOpCodes.Input.Move,
        movePayload,
        timeoutCts.Token);
    Require(
        ownerSubmit.Success && !ownerSubmit.ShouldResync,
        $"Owner authoritative move input rejected. Status={ownerSubmit.Status} " +
        $"AcceptedFrame={ownerSubmit.AcceptedFrame} CurrentFrame={ownerSubmit.CurrentFrame} " +
        $"Message={ownerSubmit.Message}.");

    const float movementEpsilonSquared = 0.0001f;
    var ownerMoved = await ownerBattle.WaitForCharacterAsync(
        teamId: 1,
        modelId: 1001,
        actor => PositionDistanceSquared(actor, ownerBaseline) > movementEpsilonSquared,
        timeoutCts.Token);
    var memberObservedMove = await memberBattle.WaitForCharacterAsync(
        teamId: 1,
        modelId: 1001,
        actor => PositionDistanceSquared(actor, memberBaseline) > movementEpsilonSquared,
        timeoutCts.Token);

    Require(ownerMoved.ActorId == ownerBaseline.ActorId,
        "Owner movement changed the authoritative actor identity.");
    Require(memberObservedMove.ActorId == ownerBaseline.ActorId,
        "Member observed movement for an unexpected actor identity.");
    Require(
        MathF.Abs(ownerMoved.X - memberObservedMove.X) < 0.001f &&
        MathF.Abs(ownerMoved.Z - memberObservedMove.Z) < 0.001f,
        "Battle clients did not converge on the same authoritative moved position.");
    Require(ownerBattle.MaxActorCount >= 2 && memberBattle.MaxActorCount >= 2,
        $"StateSync did not expose both heroes. OwnerActors={ownerBattle.MaxActorCount}, " +
        $"MemberActors={memberBattle.MaxActorCount}.");

    Console.WriteLine(
        $"MOBA_SMOKE_AUTHORITATIVE_INPUT_VERIFIED AcceptedFrame={ownerSubmit.AcceptedFrame} " +
        $"CurrentFrame={ownerSubmit.CurrentFrame} ActorId={ownerMoved.ActorId} " +
        $"From=({ownerBaseline.X:F3},{ownerBaseline.Z:F3}) " +
        $"To=({ownerMoved.X:F3},{ownerMoved.Z:F3}) " +
        $"OwnerPushes={ownerBattle.StateSyncPushCount} MemberPushes={memberBattle.StateSyncPushCount}");

    var recoveryBaseline = ownerBattle.CaptureRecoveryBaseline();
    var recoveryResponse = await ownerBattle.RequestFullStateSyncAsync(
        final.Snapshot.BattleId,
        created.RoomId,
        final.Snapshot.WorldId,
        recoveryBaseline.Frame,
        timeoutCts.Token);
    Require(
        recoveryResponse.Success && recoveryResponse.Accepted,
        $"Full StateSync recovery request was rejected: {recoveryResponse.Message}");

    var recovered = await ownerBattle.WaitForFullSnapshotAfterAsync(
        recoveryBaseline.FullSnapshotCount,
        timeoutCts.Token);
    Require(recovered.WorldId == final.Snapshot.WorldId,
        $"Recovered full snapshot world changed. Expected={final.Snapshot.WorldId}, Actual={recovered.WorldId}.");
    Require(recovered.SchemaVersion > 0,
        $"Recovered full snapshot has invalid schema version {recovered.SchemaVersion}.");
    Require(recovered.ActorCount >= 2,
        $"Recovered full snapshot omitted battle actors. ActorCount={recovered.ActorCount}.");
    Require(!string.IsNullOrWhiteSpace(recovered.EventEpoch),
        "Recovered full snapshot returned an empty reliable-event epoch.");
    Require(recovered.EventWatermark >= 0,
        $"Recovered full snapshot returned an invalid event watermark {recovered.EventWatermark}.");

    var ack = await ownerBattle.AcknowledgeReliableBattleEventsAsync(
        final.Snapshot.BattleId,
        created.RoomId,
        recovered.EventEpoch,
        recovered.EventWatermark,
        timeoutCts.Token);
    Require(ack.Success,
        $"Reliable battle event ACK failed: {ack.Message}");
    Require(ack.AcceptedAckSequence == recovered.EventWatermark,
        $"Reliable battle event ACK mismatch. Requested={recovered.EventWatermark}, " +
        $"Accepted={ack.AcceptedAckSequence}.");

    Console.WriteLine(
        $"MOBA_SMOKE_RECOVERY_VERIFIED Frame={recovered.Frame} WorldId={recovered.WorldId} " +
        $"SchemaVersion={recovered.SchemaVersion} Actors={recovered.ActorCount} " +
        $"EventEpoch={recovered.EventEpoch} EventAck={ack.AcceptedAckSequence} " +
        $"FullSnapshots={recovered.FullSnapshotCount}");

    return new MobaSmokeResult(
        created.RoomId,
        final.NumericRoomId,
        final.Snapshot.BattleId,
        final.Snapshot.WorldId,
        final.Snapshot.Phase,
        final.Snapshot.Players?.Count ?? 0,
        final.Snapshot.RoomRevision);
}

static float PositionDistanceSquared(
    WireStateSyncActorSnapshot current,
    WireStateSyncActorSnapshot baseline)
{
    var dx = current.X - baseline.X;
    var dz = current.Z - baseline.Z;
    return dx * dx + dz * dz;
}

static uint ResolvePlayerId(WireRoomSnapshot snapshot, int heroId)
{
    var player = snapshot.Players?.SingleOrDefault(candidate => candidate.HeroId == heroId) ?? default;
    Require(player.PlayerId != 0, $"Could not resolve player id for hero {heroId}.");
    return player.PlayerId;
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
    private readonly Channel<WireStateSyncSnapshotPush> _snapshots = Channel.CreateUnbounded<WireStateSyncSnapshotPush>();
    private string _sessionToken = string.Empty;
    private long _nextCommandSequence;

    // StateSync diagnostics distinguish transport, decoding, and projection failures.
    internal int StateSyncPushCount => Volatile.Read(ref _stateSyncPushCount);
    internal int LastStateSyncPushSize => Volatile.Read(ref _lastStateSyncPushSize);
    internal int MaxActorCount => Volatile.Read(ref _maxActorCount);
    private readonly object _recoveryGate = new();
    private int _stateSyncPushCount;
    private int _fullSnapshotPushCount;
    private int _decodedFullSnapshotPushCount;
    private int _deltaSnapshotPushCount;
    private int _decodeFailureCount;
    private int _lastStateSyncPushSize;
    private int _maxActorCount;
    private MobaSmokeRecoverySnapshot _lastFullSnapshot;
    private string _lastDecodeError = "none";
    private string _lastSnapshotSummary = "none";

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
        var request = new RoomSubscribeStateSyncReq
        {
            SessionToken = _sessionToken,
            BattleId = battleId,
            RoomId = roomId ?? string.Empty,
            EventEpoch = string.Empty,
            LastEventAck = 0
        };
        var response = await SendAsync<RoomSubscribeStateSyncReq, RoomSubscribeStateSyncRes>(
            RoomGatewayOpCodes.SubscribeStateSync,
            request,
            cancellationToken);
        if (!response.Success)
        {
            throw new InvalidOperationException($"SubscribeStateSync failed: {response.Message}");
        }
    }

    public MobaSmokeRecoverySnapshot CaptureRecoveryBaseline()
    {
        lock (_recoveryGate)
        {
            return _lastFullSnapshot with
            {
                FullSnapshotCount = Volatile.Read(ref _decodedFullSnapshotPushCount)
            };
        }
    }

    public Task<WireRequestFullStateSyncRes> RequestFullStateSyncAsync(
        string battleId,
        string roomId,
        ulong worldId,
        int lastAuthoritativeFrame,
        CancellationToken cancellationToken)
    {
        return SendAsync<WireRequestFullStateSyncReq, WireRequestFullStateSyncRes>(
            RoomGatewayOpCodes.RequestFullStateSync,
            new WireRequestFullStateSyncReq
            {
                SessionToken = _sessionToken,
                BattleId = battleId,
                RoomId = roomId,
                WorldId = worldId,
                ClientFrame = lastAuthoritativeFrame,
                LastAuthoritativeFrame = lastAuthoritativeFrame,
                ClientStateHash = 0,
                AuthoritativeStateHash = 0,
                Reason = "moba-smoke-recovery-verification"
            },
            cancellationToken);
    }

    public async Task<MobaSmokeRecoverySnapshot> WaitForFullSnapshotAfterAsync(
        int previousFullSnapshotCount,
        CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                if (Volatile.Read(ref _decodedFullSnapshotPushCount) > previousFullSnapshotCount)
                {
                    lock (_recoveryGate)
                    {
                        return _lastFullSnapshot with
                        {
                            FullSnapshotCount = Volatile.Read(ref _decodedFullSnapshotPushCount)
                        };
                    }
                }

                await Task.Delay(20, cancellationToken);
            }
        }
        catch (OperationCanceledException exception) when (cancellationToken.IsCancellationRequested)
        {
            throw new InvalidOperationException(
                $"Timed out waiting for a requested full StateSync snapshot. {CreateStateSyncDiagnostic()}",
                exception);
        }

        throw new InvalidOperationException(
            $"Full StateSync wait ended unexpectedly. {CreateStateSyncDiagnostic()}");
    }

    public Task<WireAckReliableBattleEventsRes> AcknowledgeReliableBattleEventsAsync(
        string battleId,
        string roomId,
        string epoch,
        long ackSequence,
        CancellationToken cancellationToken)
    {
        return SendAsync<WireAckReliableBattleEventsReq, WireAckReliableBattleEventsRes>(
            RoomGatewayOpCodes.AckReliableBattleEvents,
            new WireAckReliableBattleEventsReq
            {
                SessionToken = _sessionToken,
                BattleId = battleId,
                RoomId = roomId,
                Epoch = epoch,
                AckSequence = ackSequence
            },
            cancellationToken);
    }

    public Task<WireSubmitBattleInputRes> SubmitBattleInputAsync(
        string battleId,
        ulong worldId,
        uint playerId,
        int frame,
        int inputOpCode,
        byte[] payload,
        CancellationToken cancellationToken)
    {
        return SendAsync<WireSubmitBattleInputReq, WireSubmitBattleInputRes>(
            RoomGatewayOpCodes.SubmitBattleInput,
            new WireSubmitBattleInputReq
            {
                SessionToken = _sessionToken,
                BattleId = battleId,
                WorldId = worldId,
                Frame = frame,
                PlayerId = playerId,
                InputOpCode = inputOpCode,
                Payload = payload ?? Array.Empty<byte>(),
                CommandSequence = unchecked((ulong)Interlocked.Increment(ref _nextCommandSequence))
            },
            cancellationToken);
    }

    public async Task<WireStateSyncActorSnapshot> WaitForCharacterAsync(
        int teamId,
        int modelId,
        Func<WireStateSyncActorSnapshot, bool> predicate,
        CancellationToken cancellationToken)
    {
        try
        {
            while (await _snapshots.Reader.WaitToReadAsync(cancellationToken))
            {
                while (_snapshots.Reader.TryRead(out var snapshot))
                {
                    var actors = snapshot.Actors;
                    if (actors == null) continue;

                    for (var i = 0; i < actors.Count; i++)
                    {
                        var actor = actors[i];
                        if (actor.Kind == (int)SpawnEntityKind.Character &&
                            actor.TeamId == teamId &&
                            actor.Code == modelId &&
                            predicate(actor))
                        {
                            return actor;
                        }
                    }
                }
            }
        }
        catch (OperationCanceledException exception) when (cancellationToken.IsCancellationRequested)
        {
            throw new InvalidOperationException(
                $"Timed out waiting for character team={teamId}, model={modelId}. {CreateStateSyncDiagnostic()}",
                exception);
        }

        throw new InvalidOperationException(
            $"StateSync channel completed before character team={teamId}, model={modelId} arrived. {CreateStateSyncDiagnostic()}");
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
                ["gameplayId"] = "1",
                ["minPlayers"] = "2",
                ["tickRate"] = "30",
                [ShooterRoomTagKeys.SyncTemplateId] = "state-sync-authority"
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
        var loadout = ResolveHeroLoadout(heroId);
        var response = await SendAsync<WireRoomPickHeroReq, WireRoomSnapshotRes>(RoomGatewayOpCodes.PickHero, new WireRoomPickHeroReq
        {
            SessionToken = _sessionToken,
            RoomId = roomId,
            HeroId = heroId,
            TeamId = teamId,
            SpawnPointId = spawnPointId,
            Level = 1,
            AttributeTemplateId = loadout.AttributeTemplateId,
            BasicAttackSkillId = loadout.BasicAttackSkillId,
            SkillIds = loadout.SkillIds
        }, cancellationToken);
        if (!response.Success)
        {
            throw new InvalidOperationException($"PickHero failed: {response.Message}");
        }
    }

    private static MobaSmokeHeroLoadout ResolveHeroLoadout(int heroId)
    {
        return heroId switch
        {
            1001 => new MobaSmokeHeroLoadout(
                1001,
                10010001,
                new List<int> { 10010101, 10010201, 10010301 }),
            1002 => new MobaSmokeHeroLoadout(
                1002,
                10020001,
                new List<int> { 10020101, 10020201, 10020301 }),
            _ => throw new InvalidOperationException($"No production loadout configured for hero {heroId}.")
        };
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
        _snapshots.Writer.TryComplete();
        _requests.Dispose();
        _connection.Dispose();
        _channel.Dispose();
    }

    private void OnServerPushReceived(uint opCode, ArraySegment<byte> payload)
    {
        var isFullOpcode = opCode == RoomGatewayOpCodes.SnapshotPushed;
        var isDeltaOpcode = opCode == RoomGatewayOpCodes.DeltaSnapshotPushed;
        if (!isFullOpcode && !isDeltaOpcode)
        {
            return;
        }

        Interlocked.Increment(ref _stateSyncPushCount);
        Interlocked.Exchange(ref _lastStateSyncPushSize, payload.Count);
        if (isFullOpcode)
        {
            Interlocked.Increment(ref _fullSnapshotPushCount);
        }
        else
        {
            Interlocked.Increment(ref _deltaSnapshotPushCount);
        }

        try
        {
            // StateSync observer pushes use the room gateway MemoryPack envelope.
            var wire = WireRoomGatewayBinary.Deserialize<WireStateSyncSnapshotPush>(payload);
            _snapshots.Writer.TryWrite(wire);
            var actors = wire.Actors;
            var actorCount = actors?.Count ?? 0;
            if (wire.IsFullSnapshot)
            {
                var decodedFullCount = Interlocked.Increment(ref _decodedFullSnapshotPushCount);
                lock (_recoveryGate)
                {
                    _lastFullSnapshot = new MobaSmokeRecoverySnapshot(
                        decodedFullCount,
                        wire.WorldId,
                        wire.Frame,
                        wire.SchemaVersion,
                        actorCount,
                        wire.EventEpoch ?? string.Empty,
                        wire.EventWatermark);
                }
            }

            if (actorCount > 0)
            {
                Volatile.Write(
                    ref _lastSnapshotSummary,
                    $"opcode={opCode}, frame={wire.Frame}, world={wire.WorldId}, " +
                    $"isFull={wire.IsFullSnapshot}, actors={actorCount}, " +
                    $"actorIds={FormatActorSummary(actors)}");
            }

            var currentMax = Volatile.Read(ref _maxActorCount);
            while (actorCount > currentMax)
            {
                if (Interlocked.CompareExchange(ref _maxActorCount, actorCount, currentMax) == currentMax)
                {
                    break;
                }

                currentMax = Volatile.Read(ref _maxActorCount);
            }
        }
        catch (Exception exception)
        {
            Interlocked.Increment(ref _decodeFailureCount);
            Volatile.Write(
                ref _lastDecodeError,
                $"opcode={opCode}, bytes={payload.Count}, error={exception.GetType().Name}: {exception.Message}");
        }
    }

    private string CreateStateSyncDiagnostic()
    {
        return
            $"StateSync pushes={Volatile.Read(ref _stateSyncPushCount)}, " +
            $"full={Volatile.Read(ref _fullSnapshotPushCount)}, " +
            $"delta={Volatile.Read(ref _deltaSnapshotPushCount)}, " +
            $"decodeFailures={Volatile.Read(ref _decodeFailureCount)}, " +
            $"lastBytes={Volatile.Read(ref _lastStateSyncPushSize)}, " +
            $"maxActors={Volatile.Read(ref _maxActorCount)}, " +
            $"lastSnapshot=[{Volatile.Read(ref _lastSnapshotSummary)}], " +
            $"lastDecodeError=[{Volatile.Read(ref _lastDecodeError)}].";
    }

    private static string FormatActorSummary(List<WireStateSyncActorSnapshot>? actors)
    {
        if (actors == null || actors.Count == 0)
        {
            return "none";
        }

        return string.Join(
            ';',
            actors.Take(8).Select(actor =>
                $"{actor.ActorId}:{actor.TeamId}:{actor.Kind}:{actor.Code}:{actor.OwnerNetId}"));
    }

    private async Task<TResponse> SendAsync<TRequest, TResponse>(uint opCode, TRequest request, CancellationToken cancellationToken)
    {
        var payload = WireRoomGatewayBinary.Serialize(in request);
        var responsePayload = await _requests.SendRequestAsync(opCode, payload, TimeSpan.FromSeconds(30), cancellationToken);
        return WireRoomGatewayBinary.Deserialize<TResponse>(responsePayload);
    }
}

internal static class MobaSmokeConstants
{
    public const string HostAddress = "127.0.0.1";
    public const string Region = "local";
    public const string ServerId = "moba-smoke";
}

internal readonly record struct MobaSmokeHeroLoadout(
    int AttributeTemplateId,
    int BasicAttackSkillId,
    List<int> SkillIds);

internal readonly record struct MobaSmokeRecoverySnapshot(
    int FullSnapshotCount,
    ulong WorldId,
    int Frame,
    int SchemaVersion,
    int ActorCount,
    string EventEpoch,
    long EventWatermark);

internal readonly record struct MobaSmokeResult(
    string RoomId,
    ulong NumericRoomId,
    string BattleId,
    ulong WorldId,
    int Phase,
    int PlayerCount,
    long RoomRevision);
