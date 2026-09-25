using System.Text.Json;
using AbilityKit.Orleans.Contracts.Battle;
using AbilityKit.Orleans.Contracts.Rooms;
using AbilityKit.Orleans.Grains.Persistence;
using AbilityKit.Orleans.Grains.Rooms;
using AbilityKit.Orleans.Grains.Rooms.Gameplay;

namespace AbilityKit.Orleans.Grains.Gameplays.Tiny;

public sealed class TinyRoomGameplayAdapter : IRoomGameplayAdapter
{
    private const string Format = "tiny.room.v1";

    public string RoomType => TinyGameplay.RoomType;

    public object CreateState(RoomSummary summary) => new State();

    public RoomGameplayPersistentState ExportPersistentState(object state)
    {
        return new RoomGameplayPersistentState(Format, 1, JsonSerializer.SerializeToUtf8Bytes(Require(state)));
    }

    public object RestorePersistentState(RoomSummary summary, RoomGameplayPersistentState persistentState)
    {
        if (persistentState.Format != Format || persistentState.Version != 1)
        {
            throw new InvalidOperationException("Unsupported Tiny room state format.");
        }

        return JsonSerializer.Deserialize<State>(persistentState.Payload)
            ?? throw new InvalidOperationException("Tiny room state is empty.");
    }

    public void Join(object state, RoomSummary summary, IReadOnlyCollection<string> members, string accountId)
    {
        var room = Require(state);
        if (string.IsNullOrWhiteSpace(accountId) || room.Players.ContainsKey(accountId)) return;
        if (room.Players.Count >= 2) throw new InvalidOperationException("Tiny room is full.");
        var id = room.Players.Values.Any(player => player.PlayerId == 1) ? 2u : 1u;
        room.Players.Add(accountId, new Player { PlayerId = id });
    }

    public void Leave(object state, string accountId) => Require(state).Players.Remove(accountId);

    public void SetReady(object state, RoomReadyRequest request)
    {
        if (Require(state).Players.TryGetValue(request.AccountId, out var player)) player.Ready = request.Ready;
    }

    public RoomGameplayCommandResult SubmitCommand(object state, RoomGameplayCommandRequest request) =>
        RoomGameplayCommandResult.Rejected(RoomOperationErrorCode.InvalidGameplayCommand, "Tiny has no room commands.");

    public bool CanStart(object state)
    {
        var players = Require(state).Players;
        return players.Count == 2 && players.Values.All(player => player.Ready);
    }

    public bool ValidateBeginLoading(object state) => CanStart(state);

    public RoomLaunchManifest BuildLaunchManifest(object state, RoomSummary summary)
    {
        return RoomLaunchManifestBuilder.Build(
            RoomLaunchManifestBuilder.CurrentManifestVersion,
            new[] { "tiny:arena", "tiny:rules.v1" },
            new Dictionary<string, string> { ["players"] = Require(state).Players.Count.ToString() });
    }

    public List<RoomPlayerSnapshot> BuildPlayerSnapshots(object state)
    {
        return OrderedPlayers(Require(state)).Select(pair => new RoomPlayerSnapshot(
            pair.Key, TeamId: (int)pair.Value.PlayerId, Ready: pair.Value.Ready,
            HeroId: (int)pair.Value.PlayerId, SpawnPointId: (int)pair.Value.PlayerId,
            Level: 1, AttributeTemplateId: 0, BasicAttackSkillId: 0,
            SkillIds: null, PlayerId: pair.Value.PlayerId)).ToList();
    }

    public BattleInitParams BuildBattleInitParams(object state, RoomSummary summary, StartRoomBattleRequest request)
    {
        return new BattleInitParams
        {
            WorldId = StableWorldId(summary.RoomId),
            TickRate = TinyGameplay.TickRate,
            Players = OrderedPlayers(Require(state)).Select(pair => CreatePlayer(pair.Key, pair.Value.PlayerId)).ToList(),
            WorldType = TinyGameplay.WorldType,
            RoomType = TinyGameplay.RoomType,
            SyncOptions = RoomBattleSyncOptionsMapper.Resolve(summary, request)
        };
    }

    public PlayerInitInfo? BuildLateJoinPlayer(object state, RoomSummary summary, string accountId)
    {
        return Require(state).Players.TryGetValue(accountId, out var player)
            ? CreatePlayer(accountId, player.PlayerId)
            : null;
    }

    private static PlayerInitInfo CreatePlayer(string accountId, uint id) => new()
    {
        PlayerId = id,
        ActorId = (int)id,
        AccountId = accountId,
        TeamId = (int)id,
        PosX = id == 1 ? -1 : 1,
        PosZ = 0,
        Level = 1
    };

    private static State Require(object state) => state as State
        ?? throw new InvalidOperationException("Expected Tiny room state.");

    private static IEnumerable<KeyValuePair<string, Player>> OrderedPlayers(State state) =>
        state.Players.OrderBy(pair => pair.Value.PlayerId);

    private static ulong StableWorldId(string value)
    {
        var hash = 14695981039346656037UL;
        foreach (var character in value) hash = (hash ^ character) * 1099511628211UL;
        return hash == 0 ? 1 : hash;
    }

    internal sealed class State
    {
        public Dictionary<string, Player> Players { get; set; } = new(StringComparer.Ordinal);
    }

    internal sealed class Player
    {
        public uint PlayerId { get; set; }
        public bool Ready { get; set; }
    }
}
