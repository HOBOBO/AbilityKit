using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Demo.Tiny;
using AbilityKit.Orleans.Contracts.Battle;
using AbilityKit.Orleans.Grains.Battle;
using AbilityKit.Orleans.Grains.Battle.Gameplay;

namespace AbilityKit.Orleans.Grains.Gameplays.Tiny;

public sealed class TinyBattleRuntimeAdapter : IBattleRuntimeAdapter
{
    private readonly ServerBattleWorldManager _worldManager;

    public TinyBattleRuntimeAdapter(ServerBattleWorldManager worldManager) =>
        _worldManager = worldManager ?? throw new ArgumentNullException(nameof(worldManager));

    public string RoomType => TinyGameplay.RoomType;

    public IBattleRuntimeSession CreateSession(string battleId) => new Session(battleId, _worldManager);

    private sealed class Session : IBattleRuntimeSession, IBattleRuntimeStateHashProvider, IBattleRuntimeInputDiagnostics
    {
        private readonly string _battleId;
        private readonly ServerBattleWorldManager _worldManager;
        private readonly TinyBattle _battle = new();
        private IWorld? _world;
        private ulong _worldId;
        private string _lastInputSubmitDiagnostic = string.Empty;

        public Session(string battleId, ServerBattleWorldManager worldManager)
        {
            _battleId = battleId;
            _worldManager = worldManager;
        }

        public BattleRuntimeStartResult Start(BattleInitParams initParams)
        {
            if (initParams?.Players is not { Count: 2 })
                return BattleRuntimeStartResult.Fail("Tiny requires exactly two players.");

            if (initParams.Players[0].PlayerId == initParams.Players[1].PlayerId)
                return BattleRuntimeStartResult.Fail("Tiny player ids must be distinct.");

            _worldId = initParams.WorldId;
            _world = _worldManager.CreateBattleWorld(_battleId, TinyGameplay.WorldType, initParams.TickRate);
            foreach (var player in initParams.Players)
                _battle.AddPlayer(player.PlayerId, (int)player.PosX, (int)player.PosZ);
            return BattleRuntimeStartResult.Success();
        }

        public BattlePlayerJoinResult JoinPlayer(BattlePlayerJoinRequest request, int currentFrame)
        {
            if (request?.Player is null)
                return new BattlePlayerJoinResult(false, 0, currentFrame, "InvalidPlayer", "Player is required.");
            var player = request.Player;
            if (!_battle.ContainsPlayer(player.PlayerId))
                return new BattlePlayerJoinResult(false, player.PlayerId, currentFrame, "RoomFull", "Tiny has two fixed slots.");
            return new BattlePlayerJoinResult(true, player.PlayerId, currentFrame, "AlreadyJoined", string.Empty);
        }

        public BattleInputValidationResult ValidateInput(BattleInputItem input)
        {
            if (input is null || input.OpCode != TinyBattle.InputOpCode || !_battle.ContainsPlayer(input.PlayerId))
                return BattleInputValidationResult.Reject("InvalidInput", "Unknown Tiny player or opcode.");
            try
            {
                TinyInput.Decode(input.Payload ?? Array.Empty<byte>());
                return BattleInputValidationResult.Valid;
            }
            catch (ArgumentException)
            {
                return BattleInputValidationResult.Reject("InvalidPayload", "Invalid Tiny input payload.");
            }
        }

        /// <summary>服务端在输入批次未被全量接受时读取，用于把拒绝原因写进告警日志。</summary>
        public string LastInputSubmitDiagnostic => _lastInputSubmitDiagnostic;

        public int SubmitInputs(int frame, IReadOnlyList<BattleInputItem> inputs)
        {
            if (inputs is null || inputs.Count == 0)
            {
                _lastInputSubmitDiagnostic = string.Empty;
                return 0;
            }

            var accepted = 0;
            var rejected = 0;
            var firstReason = string.Empty;
            foreach (var item in inputs)
            {
                var validation = ValidateInput(item);
                if (!validation.Accepted)
                {
                    rejected++;
                    if (firstReason.Length == 0)
                    {
                        firstReason = validation.Status + ": " + validation.Message;
                    }

                    continue;
                }

                _battle.Submit(item.PlayerId, TinyInput.Decode(item.Payload!));
                accepted++;
            }

            _lastInputSubmitDiagnostic = rejected == 0
                ? string.Empty
                : $"frame={frame} accepted={accepted} rejected={rejected} first={firstReason}";
            return accepted;
        }

        public BattleBotAiMountResult MountBotAi(BattleBotAiMountRequest request, int currentFrame) =>
            new(false, request.PlayerId, currentFrame, "Unsupported", "Tiny has no bots.");

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
            Actors = ActorSnapshots()
        };

        public StateSyncPush CreateStateSyncPush(ulong worldId, int frame, bool isFullSnapshot) => new()
        {
            WorldId = worldId == 0 ? _worldId : worldId,
            Frame = _battle.Frame,
            Timestamp = DateTime.UtcNow.Ticks,
            ServerTicks = DateTime.UtcNow.Ticks,
            Actors = ActorSnapshots(),
            PayloadOpCode = TinyBattleStateCodec.PayloadOpCode,
            Payload = TinyBattleStateCodec.Encode(_battle.CaptureState()),
            IsFullSnapshot = true,
            SchemaVersion = 1
        };

        public uint ComputeStateHash() => _battle.ComputeHash();

        public BattleWorldDiagnostics? GetWorldDiagnostics(ulong worldId, int frame) => new()
        {
            BattleId = _battleId,
            WorldType = TinyGameplay.WorldType,
            WorldId = _worldId,
            Frame = _battle.Frame,
            StateHash = _battle.ComputeHash(),
            EntityCount = _battle.Actors.Count
        };

        public BattleDiagnosticEventsResult QueryDiagnosticEvents(BattleDiagnosticEventsQuery query) => new(
            "Unavailable", "NotProduced", 0, false, BattleDiagnosticContractConstants.SchemaVersion,
            System.Diagnostics.Stopwatch.Frequency, _battleId, _worldId.ToString(), 0,
            "Tiny does not emit diagnostic events.", Array.Empty<BattleDiagnosticEventRecord>(),
            query.Offset, query.Limit);

        public void Dispose()
        {
            if (_world is null) return;
            _worldManager.DestroyBattleWorld(_battleId);
            _world = null;
        }

        private List<ActorSnapshot> ActorSnapshots() => _battle.Actors.Select(actor => new ActorSnapshot
        {
            ActorId = (int)actor.PlayerId,
            X = actor.X,
            Z = actor.Y,
            Hp = actor.Hp,
            HpMax = TinyBattle.MaxHp,
            TeamId = (int)actor.PlayerId,
            Kind = 1,
            Code = 1
        }).ToList();
    }
}
