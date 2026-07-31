using System.Text.Json;
using AbilityKit.Demo.Shooter;
using AbilityKit.Orleans.Contracts.Battle;
using AbilityKit.Orleans.Contracts.Rooms;
using AbilityKit.Orleans.Contracts.Shooter;
using AbilityKit.Orleans.Grains.Persistence;
using AbilityKit.Orleans.Grains.Rooms;
using AbilityKit.Orleans.Grains.Rooms.Gameplay;

namespace AbilityKit.Orleans.Grains.Gameplays.Shooter.Rooms;

internal sealed class ShooterRoomGameplayAdapter : IRoomGameplayAdapter
{
    private const string PersistentFormat = "shooter.room.v1";

    public string RoomType => ShooterGameplay.RoomType;

    public object CreateState(RoomSummary summary)
    {
        var maxPlayers = summary.MaxPlayers > 0 ? summary.MaxPlayers : ShooterGameplay.DefaultMaxPlayers;
        var minPlayers = ReadIntTag(summary, ShooterRoomTagKeys.MinPlayers, 1);
        return new ShooterRoomState(maxPlayers, minPlayers);
    }

    public RoomGameplayPersistentState ExportPersistentState(object state)
    {
        var roomState = RequireState(state);
        var players = BuildOrderedPlayerSlots(roomState)
            .Select(pair => new ShooterPersistentPlayer(pair.Key, pair.Value.PlayerId, pair.Value.Ready))
            .ToList();
        var snapshot = new ShooterPersistentSnapshot(
            roomState.MaxPlayers,
            roomState.MinPlayers,
            roomState.NextPlayerId,
            roomState.ReleasedPlayerIds.ToList(),
            players);
        return new RoomGameplayPersistentState(PersistentFormat, 1, JsonSerializer.SerializeToUtf8Bytes(snapshot));
    }

    public object RestorePersistentState(RoomSummary summary, RoomGameplayPersistentState persistentState)
    {
        if (persistentState is null || persistentState.Version != 1 ||
            !string.Equals(persistentState.Format, PersistentFormat, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Unsupported Shooter room persistent state format.");
        }

        var snapshot = JsonSerializer.Deserialize<ShooterPersistentSnapshot>(persistentState.Payload)
            ?? throw new InvalidOperationException("Shooter room persistent state payload is empty.");
        return ShooterRoomState.Restore(snapshot);
    }

    public void Join(object state, RoomSummary summary, IReadOnlyCollection<string> members, string accountId)
    {
        var roomState = RequireState(state);
        roomState.Join(accountId);
    }

    public void Leave(object state, string accountId)
    {
        RequireState(state).Leave(accountId);
    }

    public void SetReady(object state, RoomReadyRequest request)
    {
        RequireState(state).SetReady(request.AccountId, request.Ready);
    }

    public RoomGameplayCommandResult SubmitCommand(object state, RoomGameplayCommandRequest request)
    {
        // Shooter 刻意保持房间配置极简；该玩法会忽略房间玩法命令。
        return RoomGameplayCommandResult.Accepted();
    }

    public bool CanStart(object state)
    {
        return RequireState(state).CanStart();
    }

    public bool ValidateBeginLoading(object state)
    {
        // Shooter 玩法极简，委托 CanStart。
        return RequireState(state).CanStart();
    }

    public RoomLaunchManifest BuildLaunchManifest(object state, RoomSummary summary)
    {
        var roomState = RequireState(state);
        var references = new List<string>();

        var mapId = ReadIntTag(summary, ShooterRoomTagKeys.MapId, 1);
        references.Add($"map:{mapId}");

        foreach (var kv in roomState.Players)
        {
            references.Add($"player:{kv.Value.PlayerId}");
        }

        var metadata = new Dictionary<string, string>
        {
            ["mapId"] = mapId.ToString(),
            ["players"] = roomState.Players.Count.ToString()
        };

        return RoomLaunchManifestBuilder.Build(RoomLaunchManifestBuilder.CurrentManifestVersion, references, metadata);
    }

    public List<RoomPlayerSnapshot> BuildPlayerSnapshots(object state)
    {
        var roomState = RequireState(state);
        var slots = BuildOrderedPlayerSlots(roomState);
        var players = new List<RoomPlayerSnapshot>(slots.Count);
        foreach (var kv in slots)
        {
            var slot = kv.Value;
            players.Add(new RoomPlayerSnapshot(
                kv.Key,
                TeamId: 1,
                Ready: slot.Ready,
                HeroId: slot.PlayerId,
                SpawnPointId: slot.PlayerId,
                Level: 1,
                AttributeTemplateId: 0,
                BasicAttackSkillId: 0,
                SkillIds: null,
                PlayerId: (uint)slot.PlayerId));
        }

        return players;
    }

    public PlayerInitInfo? BuildLateJoinPlayer(object state, RoomSummary summary, string accountId)
    {
        var roomState = RequireState(state);
        if (string.IsNullOrWhiteSpace(accountId) || !roomState.Players.TryGetValue(accountId, out var slot))
        {
            return null;
        }

        return CreatePlayerInitInfo(slot.PlayerId, accountId);
    }

    public BattleInitParams BuildBattleInitParams(object state, RoomSummary summary, StartRoomBattleRequest request)
    {
        var roomState = RequireState(state);
        var slots = BuildOrderedPlayerSlots(roomState);
        var players = new List<PlayerInitInfo>(slots.Count);
        foreach (var kv in slots)
        {
            players.Add(CreatePlayerInitInfo(kv.Value.PlayerId, kv.Key));
        }

        var syncOptions = RoomBattleSyncOptionsMapper.Resolve(summary, request);
        return new BattleInitParams
        {
            WorldId = CreateNumericWorldId(summary.RoomId),
            TickRate = ReadIntTag(summary, ShooterRoomTagKeys.TickRate, ShooterGameplay.DefaultTickRate),
            MapId = ReadIntTag(summary, ShooterRoomTagKeys.MapId, 1),
            RandomSeed = ReadIntTag(summary, ShooterRoomTagKeys.RandomSeed, Environment.TickCount),
            InputDelayFrames = syncOptions.InputDelayFrames,
            DurationFrames = ReadIntTag(summary, ShooterRoomTagKeys.DurationFrames, 0),
            VictoryTargetDefeats = ReadIntTag(summary, ShooterRoomTagKeys.VictoryTargetDefeats, 0),
            ContinueAfterAllPlayersDefeated = ReadBoolTag(summary, ShooterRoomTagKeys.ContinueAfterAllPlayersDefeated, false),
            Players = players,
            GameplayId = request.GameplayId > 0 ? request.GameplayId : ShooterGameplay.GameplayId,
            RuleSetId = request.RuleSetId,
            ConfigVersion = request.ConfigVersion,
            ProtocolVersion = request.ProtocolVersion,
            WorldType = string.IsNullOrWhiteSpace(request.WorldType) ? ShooterGameplay.WorldType : request.WorldType,
            ClientId = request.ClientId,
            RoomType = summary.RoomType,
            SyncOptions = syncOptions
        };
    }

    private static PlayerInitInfo CreatePlayerInitInfo(int playerId, string accountId)
    {
        return new PlayerInitInfo
        {
            PlayerId = (uint)playerId,
            AccountId = accountId,
            ActorId = playerId,
            HeroId = playerId,
            PosX = (playerId - 1) * 2f,
            PosY = 0f,
            PosZ = 0f,
            TeamId = 1,
            Level = 1,
            AttributeTemplateId = 1,
            BasicAttackSkillId = 1,
            SkillIds = new List<int> { 1 }
        };
    }

    private static List<KeyValuePair<string, ShooterRoomPlayer>> BuildOrderedPlayerSlots(ShooterRoomState roomState)
    {
        var slots = new List<KeyValuePair<string, ShooterRoomPlayer>>(roomState.Players);
        slots.Sort(static (left, right) => left.Value.PlayerId.CompareTo(right.Value.PlayerId));
        return slots;
    }

    private static bool ReadBoolTag(RoomSummary summary, string key, bool fallback)
    {
        if (summary.Tags is null || !summary.Tags.TryGetValue(key, out var value))
        {
            return fallback;
        }

        return bool.TryParse(value, out var parsed) ? parsed : fallback;
    }

    private static ShooterRoomState RequireState(object state)
    {
        return state as ShooterRoomState
            ?? throw new InvalidOperationException("Room gameplay state is not a Shooter room state.");
    }

    private static int ReadIntTag(RoomSummary summary, string key, int fallback)
    {
        if (summary.Tags != null && summary.Tags.TryGetValue(key, out var value) && int.TryParse(value, out var parsed))
        {
            return parsed;
        }

        return fallback;
    }

    private static ulong CreateNumericWorldId(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return 0;

        const ulong offset = 14695981039346656037UL;
        const ulong prime = 1099511628211UL;
        var hash = offset;
        for (int i = 0; i < value.Length; i++)
        {
            hash ^= value[i];
            hash *= prime;
        }

        return hash == 0 ? 1 : hash;
    }

    private sealed class ShooterRoomState
    {
        public ShooterRoomState(int maxPlayers, int minPlayers)
        {
            MaxPlayers = Math.Max(1, maxPlayers);
            MinPlayers = Math.Max(1, Math.Min(MaxPlayers, minPlayers));
        }

        public int MaxPlayers { get; }

        public int MinPlayers { get; }

        public Dictionary<string, ShooterRoomPlayer> Players { get; } = new(StringComparer.Ordinal);

        internal SortedSet<int> ReleasedPlayerIds { get; } = new();

        internal int NextPlayerId { get; private set; } = 1;

        public static ShooterRoomState Restore(ShooterPersistentSnapshot snapshot)
        {
            var state = new ShooterRoomState(snapshot.MaxPlayers, snapshot.MinPlayers <= 0 ? 1 : snapshot.MinPlayers)
            {
                NextPlayerId = Math.Max(1, snapshot.NextPlayerId)
            };
            foreach (var playerId in snapshot.ReleasedPlayerIds ?? new List<int>())
            {
                state.ReleasedPlayerIds.Add(playerId);
            }
            foreach (var player in snapshot.Players ?? new List<ShooterPersistentPlayer>())
            {
                state.Players[player.AccountId] = new ShooterRoomPlayer(player.PlayerId) { Ready = player.Ready };
            }
            return state;
        }

        public void Join(string accountId)
        {
            if (string.IsNullOrWhiteSpace(accountId)) return;
            if (Players.ContainsKey(accountId)) return;
            if (Players.Count >= MaxPlayers) throw new InvalidOperationException("Shooter room is full.");

            Players[accountId] = new ShooterRoomPlayer(AllocatePlayerId());
        }

        public void Leave(string accountId)
        {
            if (string.IsNullOrWhiteSpace(accountId)) return;
            if (Players.Remove(accountId, out var player))
            {
                ReleasedPlayerIds.Add(player.PlayerId);
            }
        }

        public void SetReady(string accountId, bool ready)
        {
            if (string.IsNullOrWhiteSpace(accountId)) return;
            if (Players.TryGetValue(accountId, out var player))
            {
                player.Ready = ready;
            }
        }

        public bool CanStart()
        {
            if (Players.Count < MinPlayers) return false;
            foreach (var kv in Players)
            {
                if (!kv.Value.Ready) return false;
            }

            return true;
        }

        private int AllocatePlayerId()
        {
            if (ReleasedPlayerIds.Count > 0)
            {
                var playerId = ReleasedPlayerIds.Min;
                ReleasedPlayerIds.Remove(playerId);
                return playerId;
            }

            return NextPlayerId++;
        }

    }

    internal sealed record ShooterPersistentSnapshot(
        int MaxPlayers,
        int MinPlayers,
        int NextPlayerId,
        List<int> ReleasedPlayerIds,
        List<ShooterPersistentPlayer> Players);

    internal sealed record ShooterPersistentPlayer(string AccountId, int PlayerId, bool Ready);

    private sealed class ShooterRoomPlayer
    {
        public ShooterRoomPlayer(int playerId)
        {
            PlayerId = playerId;
        }

        public int PlayerId { get; }

        public bool Ready;
    }
}
