using AbilityKit.Demo.Shooter;
using AbilityKit.Orleans.Contracts.Automation;
using AbilityKit.Orleans.Contracts.Battle;
using AbilityKit.Orleans.Contracts.Rooms;
using AbilityKit.Orleans.Contracts.Shooter;
using Orleans;
using ContractCreateRoomRequest = AbilityKit.Orleans.Contracts.Rooms.CreateRoomRequest;

namespace AbilityKit.Orleans.Grains.Automation;

public sealed class ShooterSandboxGrain : Grain, IShooterSandboxGrain
{
    private const string OwnerAccountPrefix = "shooter-sandbox-owner";
    private const string BotAccountPrefix = "shooter-sandbox-bot";

    private ShooterSandboxState _state = EmptyState;
    private readonly List<string> _botAccounts = new();

    private static ShooterSandboxState EmptyState => new(
        Running: false,
        Region: string.Empty,
        ServerId: string.Empty,
        RoomId: string.Empty,
        BattleId: string.Empty,
        WorldId: 0UL,
        BotCount: 0,
        CurrentFrame: 0,
        ServerNowTicks: DateTime.UtcNow.Ticks,
        Snapshot: null);

    public override Task OnDeactivateAsync(DeactivationReason reason, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public async Task<ShooterSandboxState> StartAsync(StartShooterSandboxRequest request)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        var region = string.IsNullOrWhiteSpace(request.Region) ? ShooterServerProtocol.DefaultRegion : request.Region;
        var serverId = string.IsNullOrWhiteSpace(request.ServerId) ? ShooterServerProtocol.DefaultServerId : request.ServerId;
        var botCount = Math.Clamp(request.BotCount <= 0 ? 4 : request.BotCount, 1, 64);
        var maxPlayers = Math.Max(request.MaxPlayers, botCount + 16);
        var tickRate = request.TickRate > 0 ? request.TickRate : ShooterGameplay.DefaultTickRate;

        _botAccounts.Clear();

        var ownerAccount = CreateOwnerAccount();
        var directory = GrainFactory.GetGrain<IRoomDirectoryGrain>($"{region}:{serverId}");
        var tags = BuildRoomTags(request.Tags, tickRate);
        var create = await directory.CreateRoomAsync(new ContractCreateRoomRequest(
            ownerAccount,
            region,
            serverId,
            ShooterGameplay.RoomType,
            string.IsNullOrWhiteSpace(request.Title) ? ShooterServerProtocol.DefaultSandboxTitle : request.Title,
            IsPublic: true,
            maxPlayers,
            tags));

        var room = GrainFactory.GetGrain<IRoomGrain>(create.RoomId);
        await room.JoinMemberAsync(new JoinRoomMemberRequest(ownerAccount, IsBot: true));
        await room.SetReadyAsync(new RoomReadyRequest(ownerAccount, Ready: true));
        _botAccounts.Add(ownerAccount);

        for (var i = 1; i < botCount; i++)
        {
            var botAccount = CreateBotAccount(i);
            await room.JoinMemberAsync(new JoinRoomMemberRequest(botAccount, IsBot: true));
            await room.SetReadyAsync(new RoomReadyRequest(botAccount, Ready: true));
            _botAccounts.Add(botAccount);
        }

        var started = await room.StartBattleAsync(new StartRoomBattleRequest(
            ownerAccount,
            ShooterGameplay.GameplayId,
            RuleSetId: 0,
            ConfigVersion: 1,
            ProtocolVersion: 1,
            ShooterGameplay.WorldType,
            ClientId: ShooterServerProtocol.SandboxClientId,
            SyncOptions: new BattleSyncStartOptions(
                ShooterServerProtocol.PureStateAuthorityTemplate,
                SyncModel: 0,
                NetworkEnvironmentId: ShooterServerProtocol.SandboxNetworkEnvironmentId,
                CarrierName: ShooterServerProtocol.SandboxCarrierName,
                EnableAuthoritativeWorld: true,
                InterpolationEnabled: true,
                InputDelayFrames: 0)));

        var snapshot = await room.GetSnapshotAsync();
        _state = new ShooterSandboxState(
            Running: started.Started,
            region,
            serverId,
            create.RoomId,
            started.BattleId,
            started.WorldId,
            _botAccounts.Count,
            CurrentFrame: 0,
            ServerNowTicks: DateTime.UtcNow.Ticks,
            snapshot);

        if (started.Started)
        {
            await MountBotAiAsync(started.BattleId, started.WorldId);
        }

        return _state;
    }

    public async Task<ShooterSandboxState> GetStateAsync()
    {
        if (!_state.Running || string.IsNullOrWhiteSpace(_state.RoomId))
        {
            return _state with { ServerNowTicks = DateTime.UtcNow.Ticks };
        }

        var room = GrainFactory.GetGrain<IRoomGrain>(_state.RoomId);
        var snapshot = await room.GetSnapshotAsync();
        var currentFrame = 0;
        if (!string.IsNullOrWhiteSpace(_state.BattleId))
        {
            currentFrame = await GrainFactory.GetGrain<IBattleLogicHostGrain>(_state.BattleId).GetCurrentFrameAsync();
        }

        _state = _state with
        {
            CurrentFrame = currentFrame,
            ServerNowTicks = DateTime.UtcNow.Ticks,
            Snapshot = snapshot
        };
        return _state;
    }

    public async Task StopAsync()
    {
        if (!string.IsNullOrWhiteSpace(_state.BattleId))
        {
            await GrainFactory.GetGrain<IBattleLogicHostGrain>(_state.BattleId).DestroyAsync();
        }

        if (!string.IsNullOrWhiteSpace(_state.RoomId))
        {
            await GrainFactory.GetGrain<IRoomGrain>(_state.RoomId).CloseAsync(CreateOwnerAccount());
        }

        _botAccounts.Clear();
        _state = EmptyState;
    }

    private async Task MountBotAiAsync(string battleId, ulong worldId)
    {
        if (string.IsNullOrWhiteSpace(battleId) || worldId == 0UL || _botAccounts.Count == 0)
        {
            return;
        }

        var battle = GrainFactory.GetGrain<IBattleLogicHostGrain>(battleId);
        for (var i = 0; i < _botAccounts.Count; i++)
        {
            var playerId = (uint)(i + 1);
            await battle.MountBotAiAsync(new BattleBotAiMountRequest(worldId, playerId, "simple-battle"));
        }
    }

    private Dictionary<string, string> BuildRoomTags(Dictionary<string, string>? requestTags, int tickRate)
    {
        var tags = requestTags == null
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : new Dictionary<string, string>(requestTags, StringComparer.Ordinal);
        tags[ShooterRoomTagKeys.TickRate] = tickRate.ToString();
        tags[ShooterRoomTagKeys.Sandbox] = ShooterServerProtocol.SandboxTagValue;
        tags[ShooterRoomTagKeys.JoinMode] = ShooterServerProtocol.RunningBattleLateJoinMode;
        return tags;
    }

    private string CreateOwnerAccount()
    {
        return $"{OwnerAccountPrefix}:{this.GetPrimaryKeyString()}";
    }

    private string CreateBotAccount(int index)
    {
        return $"{BotAccountPrefix}:{this.GetPrimaryKeyString()}:{index}";
    }

}
