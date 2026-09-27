using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Demo.Tiny.Turn;
using AbilityKit.Orleans.Contracts.Battle;
using AbilityKit.Orleans.Grains.Battle;
using AbilityKit.Orleans.Grains.Battle.Gameplay;

namespace AbilityKit.Orleans.Grains.Gameplays.Tiny;

public sealed class TinyTurnBattleRuntimeAdapter : IBattleRuntimeAdapter
{
    private readonly ServerBattleWorldManager _worldManager;

    public TinyTurnBattleRuntimeAdapter(ServerBattleWorldManager worldManager) =>
        _worldManager = worldManager ?? throw new ArgumentNullException(nameof(worldManager));

    public string RoomType => TinyTurnBattle.RoomType;

    public IBattleRuntimeSession CreateSession(string battleId) => new Session(battleId, _worldManager);

    private sealed class Session : IBattleRuntimeSession, IBattleRuntimeStateHashProvider
    {
        private readonly string _battleId;
        private readonly ServerBattleWorldManager _worldManager;
        private readonly TinyTurnBattle _battle = new();
        private IWorld? _world;
        private ulong _worldId;

        public Session(string battleId, ServerBattleWorldManager worldManager)
        {
            _battleId = battleId;
            _worldManager = worldManager;
        }

        public BattleRuntimeStartResult Start(BattleInitParams initParams)
        {
            if (initParams?.Players is not { Count: 2 } ||
                initParams.Players[0].PlayerId != 1 || initParams.Players[1].PlayerId != 2)
                return BattleRuntimeStartResult.Fail("Tiny Turn requires ordered player slots 1 and 2.");
            _worldId = initParams.WorldId;
            _world = _worldManager.CreateBattleWorld(
                _battleId, TinyTurnBattle.WorldType, initParams.TickRate);
            return BattleRuntimeStartResult.Success();
        }

        public BattlePlayerJoinResult JoinPlayer(BattlePlayerJoinRequest request, int currentFrame)
        {
            if (request?.Player is null)
                return new BattlePlayerJoinResult(false, 0, currentFrame,
                    "InvalidPlayer", "Player is required.");
            var playerId = request.Player.PlayerId;
            return playerId is 1 or 2
                ? new BattlePlayerJoinResult(true, playerId, currentFrame,
                    "AlreadyJoined", string.Empty)
                : new BattlePlayerJoinResult(false, playerId, currentFrame,
                    "RoomFull", "Tiny Turn has two fixed slots.");
        }

        public BattleInputValidationResult ValidateInput(BattleInputItem input)
        {
            if (input is null || input.OpCode != TinyTurnBattle.InputOpCode ||
                input.Payload is not { Length: 1 } || input.Payload[0] != 1)
                return BattleInputValidationResult.Reject("InvalidInput", "Invalid Tiny Turn command.");
            return _battle.CanSubmit(input.PlayerId)
                ? BattleInputValidationResult.Valid
                : BattleInputValidationResult.Reject("NotYourTurn", "The turn is owned by another player or the battle has ended.");
        }

        public int SubmitInputs(int frame, IReadOnlyList<BattleInputItem> inputs)
        {
            var accepted = 0;
            foreach (var input in inputs)
            {
                if (!ValidateInput(input).Accepted) continue;
                if (_battle.Submit(input.PlayerId, input.Payload!)) accepted++;
            }
            return accepted;
        }

        public BattleBotAiMountResult MountBotAi(BattleBotAiMountRequest request, int currentFrame) =>
            new(false, request.PlayerId, currentFrame, "Unsupported", "Tiny Turn has no bots.");

        public bool Tick(int frame, int tickRate, float deltaTime)
        {
            if (_world is null) return false;
            _world.Tick(deltaTime);
            _battle.Tick();
            return _battle.Frame >= frame;
        }

        public BattleSnapshot? GetSnapshot(int frame) => _world is null ? null : new BattleSnapshot
        {
            Frame = _battle.Frame,
            Actors = ActorSnapshots(),
            MatchState = (int)_battle.WinnerId,
            MatchFinal = _battle.WinnerId != 0,
            MatchVictory = _battle.WinnerId != 0,
            MatchCompletedFrame = _battle.WinnerId == 0 ? 0 : _battle.Frame
        };

        public StateSyncPush CreateStateSyncPush(ulong worldId, int frame, bool isFullSnapshot) => new()
        {
            WorldId = worldId == 0 ? _worldId : worldId,
            Frame = _battle.Frame,
            Timestamp = DateTime.UtcNow.Ticks,
            ServerTicks = DateTime.UtcNow.Ticks,
            Actors = ActorSnapshots(),
            PayloadOpCode = TinyTurnBattle.SnapshotOpCode,
            Payload = TinyTurnStateCodec.Encode(_battle.CaptureState()),
            IsFullSnapshot = true,
            SchemaVersion = 1
        };

        public uint ComputeStateHash() => _battle.ComputeHash();

        public BattleWorldDiagnostics? GetWorldDiagnostics(ulong worldId, int frame) => new()
        {
            BattleId = _battleId,
            WorldType = TinyTurnBattle.WorldType,
            WorldId = _worldId,
            Frame = _battle.Frame,
            StateHash = _battle.ComputeHash(),
            EntityCount = 2
        };

        public BattleDiagnosticEventsResult QueryDiagnosticEvents(BattleDiagnosticEventsQuery query) => new(
            "Unavailable", "NotProduced", 0, false, BattleDiagnosticContractConstants.SchemaVersion,
            System.Diagnostics.Stopwatch.Frequency, _battleId, _worldId.ToString(), 0,
            "Tiny Turn does not emit diagnostic events.", Array.Empty<BattleDiagnosticEventRecord>(),
            query.Offset, query.Limit);

        public void Dispose()
        {
            if (_world is null) return;
            _worldManager.DestroyBattleWorld(_battleId);
            _world = null;
        }

        private List<ActorSnapshot> ActorSnapshots() => new()
        {
            new ActorSnapshot { ActorId = 1, X = -1, Hp = _battle.PlayerOneHp,
                HpMax = TinyTurnBattle.MaxHp, TeamId = 1, Kind = 1, Code = 1 },
            new ActorSnapshot { ActorId = 2, X = 1, Hp = _battle.PlayerTwoHp,
                HpMax = TinyTurnBattle.MaxHp, TeamId = 2, Kind = 1, Code = 1 }
        };
    }
}
