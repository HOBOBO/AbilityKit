using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.Host.Extensions.Moba.Runtime;
using AbilityKit.Ability.Host.Extensions.Moba.Snapshot;
using AbilityKit.Ability.Host.Extensions.Moba.StartSources;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.LogicWorld;
using AbilityKit.Demo.Moba.Session;
using AbilityKit.Protocol.Moba;
using AbilityKit.Protocol.Moba.CreateWorld;
using AbilityKit.Protocol.Moba.StateSync;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.Smoke;

public sealed class MobaRuntimeFirstFrameSnapshotAcceptanceTests
{
    [Fact]
    public void Runtime_start_can_collect_first_frame_enter_game_and_spawn_snapshots()
    {
        var enterGameSnapshots = new MobaEnterGameSnapshotService();
        var actorSpawnSnapshots = new MobaActorSpawnSnapshotService();
        var output = new FirstFrameSnapshotOutputPort(enterGameSnapshots, actorSpawnSnapshots);
        var start = new PublishingGameStartPort(enterGameSnapshots, actorSpawnSnapshots);
        var runtime = new MobaBattleRuntimePort(
            start,
            AcceptingInputPort.Instance,
            output,
            EmptyStateReadModel.Instance);

        Assert.True(runtime.Status.IsReadyForGameStart);
        Assert.True(runtime.Status.IsReadyForBattleLoop);

        var spec = CreateStartSpec();
        var result = runtime.TryStartGame(in spec);

        Assert.True(result.Succeeded, result.ToString());

        var snapshots = new List<WorldStateSnapshot>();
        var count = runtime.CollectSnapshots(new FrameIndex(0), snapshots);

        Assert.Equal(2, count);
        Assert.Contains(snapshots, snapshot => snapshot.OpCode == MobaOpCodes.Snapshot.EnterGame && snapshot.Payload.Length > 0);
        Assert.Contains(snapshots, snapshot => snapshot.OpCode == MobaOpCodes.Snapshot.ActorSpawn && snapshot.Payload.Length > 0);

        var repeatedSnapshots = new List<WorldStateSnapshot>();
        var repeatedCount = runtime.CollectSnapshots(new FrameIndex(0), repeatedSnapshots);

        Assert.Equal(0, repeatedCount);
        Assert.Empty(repeatedSnapshots);
    }

    [Fact]
    public void Runtime_port_reports_missing_capabilities_for_host_contract_acceptance()
    {
        var runtime = new MobaBattleRuntimePort(
            gameStart: null,
            input: null,
            output: null,
            stateReadModel: null);

        var status = runtime.Status;

        Assert.False(status.IsReadyForGameStart);
        Assert.False(status.IsReadyForBattleLoop);
        Assert.False(status.Has(MobaBattleRuntimeCapability.GameStart));
        Assert.False(status.Has(MobaBattleRuntimeCapability.Input));
        Assert.False(status.Has(MobaBattleRuntimeCapability.SnapshotOutput));
        Assert.False(status.Has(MobaBattleRuntimeCapability.StateReadModel));
        Assert.Contains(nameof(IMobaGameStartPort), status.MissingServices);
        Assert.Contains(nameof(IMobaBattleInputPort), status.MissingServices);
        Assert.Contains(nameof(IMobaBattleOutputPort), status.MissingServices);
        Assert.Contains(nameof(IMobaLogicWorldStateReadModel), status.MissingServices);
    }

    [Fact]
    public void Runtime_port_submit_returns_stable_missing_input_code()
    {
        var runtime = new MobaBattleRuntimePort(
            StaticGameStartPort.Instance,
            input: null,
            output: null,
            stateReadModel: null);

        var result = runtime.Submit(new FrameIndex(1), new[] { CreateInputCommand(1) });

        Assert.False(result.Succeeded);
        Assert.Equal(MobaInputSubmitFailureCode.MissingInputPort, result.FailureCode);
    }

    [Fact]
    public void Runtime_io_port_maps_input_submit_boundaries_to_stable_codes()
    {
        var io = new MobaBattleIOPort(
            new ScriptedInputCoordinator(),
            EmptySnapshotProvider.Instance);

        Assert.Equal(
            MobaInputSubmitFailureCode.NullOrEmptyCommands,
            io.Submit(new FrameIndex(1), Array.Empty<PlayerInputCommand>()).FailureCode);

        Assert.Equal(
            MobaInputSubmitFailureCode.InvalidFrame,
            io.Submit(new FrameIndex(-1), new[] { CreateInputCommand(-1) }).FailureCode);

        Assert.Equal(
            MobaInputSubmitFailureCode.NoCommandHandled,
            io.Submit(new FrameIndex(10), new[] { CreateInputCommand(10) }).FailureCode);

        Assert.Equal(
            MobaInputSubmitFailureCode.PartialCommandHandled,
            io.Submit(new FrameIndex(11), new[] { CreateInputCommand(11), CreateInputCommand(11) }).FailureCode);

        var accepted = io.Submit(new FrameIndex(12), new[] { CreateInputCommand(12) });
        Assert.True(accepted.Succeeded);
        Assert.Equal(MobaInputSubmitFailureCode.None, accepted.FailureCode);
        Assert.Equal(1, accepted.CommandCount);
    }

    [Fact]
    public void Runtime_io_port_rejects_invalid_snapshot_arguments_consistently()
    {
        var io = new MobaBattleIOPort(
            new ScriptedInputCoordinator(),
            EmptySnapshotProvider.Instance);
        var snapshots = new List<WorldStateSnapshot>();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            io.TryGetSnapshot(new FrameIndex(-1), out _));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            io.CollectSnapshots(new FrameIndex(-1), snapshots));
        Assert.Throws<ArgumentNullException>(() =>
            io.CollectSnapshots(new FrameIndex(0), null!));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            io.CollectSnapshots(new FrameIndex(0), snapshots, 0));
    }

    [Fact]
    public void Default_input_contracts_are_complete_and_have_explicit_handler_factories()
    {
        var registry = MobaInputCommandContractRegistry.CreateDefault();
        var validation = registry.Validate();
        var expectedOpCodes = new[]
        {
            MobaOpCodes.Input.Move,
            MobaOpCodes.Input.SkillInput,
            MobaOpCodes.Input.DebugSpawnUnit,
            MobaOpCodes.Input.DebugReplaceHero,
        };

        Assert.True(validation.Succeeded, string.Join(Environment.NewLine, validation.Errors));
        Assert.Equal(expectedOpCodes.Length, registry.ContractCount);
        foreach (var opCode in expectedOpCodes)
        {
            Assert.True(registry.TryGetContract(opCode, out var contract));
            Assert.True(contract.Required);
            Assert.Equal(MobaInputCommandAuthority.BattlePlayer, contract.Authority);
            Assert.Equal(MobaInputCommandFramePolicy.ExactBatchFrame, contract.FramePolicy);
            Assert.False(string.IsNullOrWhiteSpace(contract.PayloadSchema));
            Assert.NotNull(contract.PayloadValidator);
            Assert.NotNull(contract.HandlerFactory);

            Assert.True(registry.HandlerRegistry.TryGetHandlerDescriptor(opCode, out var descriptor));
            Assert.Equal(contract.HandlerType, descriptor.HandlerType);
            Assert.NotNull(descriptor.HandlerFactory);
            Assert.IsAssignableFrom<IMobaInputCommandHandler>(descriptor.HandlerFactory());
        }
    }

    [Fact]
    public void Battle_driver_rejects_operations_before_binding()
    {
        var driver = new MobaBattleDriverHost();

        Assert.Throws<InvalidOperationException>(() => driver.Start());
        Assert.Throws<InvalidOperationException>(() => driver.GetAllEntityStates());
        Assert.Throws<InvalidOperationException>(() => driver.GetLogicWorldEntityStates());
        Assert.Throws<InvalidOperationException>(() =>
            driver.FillLogicWorldEntityStates(new List<LogicWorldEntityState>()));
    }

    [Fact]
    public void Logic_world_input_coordinator_rejects_negative_frame_with_stable_code()
    {
        var coordinator = new NegativeFrameInputCoordinator();

        var result = coordinator.TrySubmit(new FrameIndex(-1), new[] { CreateInputCommand(-1) });

        Assert.False(result.Succeeded);
        Assert.Equal(LogicWorldInputSubmitFailureCode.FrameRejected, result.FailureCode);
        Assert.Contains("targetFrame=-1", result.Message);
    }

    [Fact]
    public void Runtime_port_fill_entity_states_uses_buffer_read_model_boundary()
    {
        var stateReadModel = new ScriptedStateReadModel();
        var runtime = new MobaBattleRuntimePort(
            StaticGameStartPort.Instance,
            AcceptingInputPort.Instance,
            output: null,
            stateReadModel);
        var buffer = new List<LogicWorldEntityState>();

        var count = runtime.FillAllEntityStates(buffer);

        Assert.Equal(2, count);
        Assert.Equal(2, buffer.Count);
        Assert.Equal(1001, buffer[0].EntityId);
        Assert.Equal(1002, buffer[1].EntityId);
        Assert.Equal(1, stateReadModel.FillAllEntityStatesCallCount);
        Assert.Equal(0, stateReadModel.GetAllEntityStatesCallCount);
    }

    [Fact]
    public void Enter_game_snapshot_service_exposes_host_snapshot_contracts()
    {
        var service = new MobaEnterGameSnapshotService();
        IMobaEnterGameSnapshotSink sink = service;
        IMobaEnterGameSnapshotSource source = service;
        var payload = new byte[] { 1, 2, 3 };

        sink.PublishEnterGameResPayload(payload);

        Assert.True(source.TryGetEnterGameSnapshot(out var snapshot));
        Assert.Equal(MobaOpCodes.Snapshot.EnterGame, snapshot.OpCode);
        Assert.Equal(payload, snapshot.Payload);
        Assert.False(source.TryGetEnterGameSnapshot(out _));

        sink.PublishEnterGameResPayload(payload);
        Assert.True(service.TryGetSnapshot(new FrameIndex(0), out var routedSnapshot));
        Assert.Equal(MobaOpCodes.Snapshot.EnterGame, routedSnapshot.OpCode);
        Assert.Equal(payload, routedSnapshot.Payload);
    }

    [Fact]
    public void Game_start_spec_service_exposes_host_pending_store_contract()
    {
        var service = new MobaGameStartSpecService();
        IMobaPendingGameStartSpecStore store = service;
        var spec = CreateStartSpec();

        store.Set(in spec);

        Assert.True(store.HasSpec);
        Assert.True(store.HasPlan);
        Assert.True(store.TryGet(out var storedSpec));
        Assert.Equal(spec.EnterReq.MatchId, storedSpec.EnterReq.MatchId);
        Assert.True(store.TryGetPlan(out var plan));
        Assert.Equal(spec.EnterReq.PlayerId.Value, plan.LocalPlayerId.Value);
        Assert.True(store.ValidatePendingSpec().Succeeded);
        Assert.True(store.ValidatePendingPlan().Succeeded);

        store.Clear();

        Assert.False(store.HasSpec);
        Assert.False(store.HasPlan);
        Assert.False(store.TryGet(out _));
        Assert.False(store.TryGetPlan(out _));
    }

    private static PlayerInputCommand CreateInputCommand(int frame)
    {
        return new PlayerInputCommand(new FrameIndex(frame), new PlayerId("p1"), MobaOpCodes.Input.Ready, Array.Empty<byte>());
    }

    private static MobaGameStartSpec CreateStartSpec()
    {
        var playerId = new PlayerId("p1");
        var loadout = new MobaPlayerLoadout(
            playerId,
            teamId: 1,
            heroId: 1001,
            attributeTemplateId: 2001,
            level: 1,
            basicAttackSkillId: 3001,
            skillIds: new[] { 3002 },
            spawnIndex: 1,
            hasSpawnPosition: 1,
            spawnX: 1f,
            spawnY: 0f,
            spawnZ: 2f);

        var req = new EnterMobaGameReq(
            playerId,
            matchId: "acceptance-match",
            mapId: 1,
            randomSeed: 12345,
            tickRate: 30,
            inputDelayFrames: 2,
            players: new[] { loadout });

        return new MobaGameStartSpec(in req);
    }

    private sealed class StaticGameStartPort : IMobaGameStartPort
    {
        public static readonly StaticGameStartPort Instance = new();

        public MobaGameStartResult TryStartGame(in MobaGameStartSpec spec)
        {
            return MobaGameStartResult.Success;
        }

        public void Dispose()
        {
        }
    }

    private sealed class PublishingGameStartPort : IMobaGameStartPort
    {
        private readonly MobaEnterGameSnapshotService _enterGameSnapshots;
        private readonly MobaActorSpawnSnapshotService _actorSpawnSnapshots;

        public PublishingGameStartPort(MobaEnterGameSnapshotService enterGameSnapshots, MobaActorSpawnSnapshotService actorSpawnSnapshots)
        {
            _enterGameSnapshots = enterGameSnapshots;
            _actorSpawnSnapshots = actorSpawnSnapshots;
        }

        public MobaGameStartResult TryStartGame(in MobaGameStartSpec spec)
        {
            var player = spec.EnterReq.Players[0];
            var enterGameRes = new EnterMobaGameRes(
                new WorldId("acceptance-world"),
                spec.EnterReq.PlayerId,
                localActorId: 1,
                spec.EnterReq.RandomSeed,
                spec.EnterReq.TickRate,
                spec.EnterReq.InputDelayFrames,
                players: new[] { new MobaPlayerEntry(player.PlayerId, player.TeamId, player.HeroId, player.SpawnIndex) },
                playersLoadout: spec.EnterReq.Players);

            _enterGameSnapshots.PublishEnterGameResPayload(EnterMobaGameCodec.SerializeRes(enterGameRes));

            var spawnEntries = new[]
            {
                new MobaActorSpawnSnapshotEntry(
                    netId: 1,
                    kind: (int)SpawnEntityKind.Character,
                    code: player.HeroId,
                    ownerNetId: 0,
                    x: player.SpawnX,
                    y: player.SpawnY,
                    z: player.SpawnZ)
            };

            _actorSpawnSnapshots.PublishSpawnPayload(MobaActorSpawnSnapshotCodec.Serialize(spawnEntries));
            return MobaGameStartResult.Success;
        }

        public void Dispose()
        {
        }
    }

    private sealed class FirstFrameSnapshotOutputPort : IMobaBattleOutputPort
    {
        private readonly MobaEnterGameSnapshotService _enterGameSnapshots;
        private readonly MobaActorSpawnSnapshotService _actorSpawnSnapshots;

        public FirstFrameSnapshotOutputPort(MobaEnterGameSnapshotService enterGameSnapshots, MobaActorSpawnSnapshotService actorSpawnSnapshots)
        {
            _enterGameSnapshots = enterGameSnapshots;
            _actorSpawnSnapshots = actorSpawnSnapshots;
        }

        public bool TryGetSnapshot(FrameIndex frame, out WorldStateSnapshot snapshot)
        {
            if (_enterGameSnapshots.TryGetSnapshot(frame, out snapshot)) return true;
            return _actorSpawnSnapshots.TryGetSnapshot(frame, out snapshot);
        }

        public int CollectSnapshots(FrameIndex frame, IList<WorldStateSnapshot> snapshots, int maxSnapshots = 32)
        {
            if (snapshots == null) throw new ArgumentNullException(nameof(snapshots));
            if (maxSnapshots <= 0) throw new ArgumentOutOfRangeException(nameof(maxSnapshots), maxSnapshots, "maxSnapshots must be positive.");

            var count = 0;
            if (count < maxSnapshots && _enterGameSnapshots.TryGetSnapshot(frame, out var enterGameSnapshot))
            {
                snapshots.Add(enterGameSnapshot);
                count++;
            }

            if (count < maxSnapshots && _actorSpawnSnapshots.TryGetSnapshot(frame, out var actorSpawnSnapshot))
            {
                snapshots.Add(actorSpawnSnapshot);
                count++;
            }

            return count;
        }
    }

    private sealed class EmptySnapshotProvider : IWorldStateSnapshotProvider
    {
        public static readonly EmptySnapshotProvider Instance = new();

        public bool TryGetSnapshot(FrameIndex frame, out WorldStateSnapshot snapshot)
        {
            snapshot = default;
            return false;
        }

        public void Dispose()
        {
        }
    }

    private sealed class ScriptedInputCoordinator : IMobaInputCoordinator
    {
        public void Submit(FrameIndex frame, IReadOnlyList<PlayerInputCommand> inputs)
        {
        }

        public LogicWorldInputSubmitResult TrySubmit(FrameIndex frame, IReadOnlyList<PlayerInputCommand> inputs)
        {
            return frame.Value switch
            {
                10 => LogicWorldInputSubmitResult.Accepted(inputs.Count, handledCount: 0),
                11 => LogicWorldInputSubmitResult.Accepted(inputs.Count, handledCount: 1),
                _ => LogicWorldInputSubmitResult.Accepted(inputs.Count, inputs.Count),
            };
        }
    }

    private sealed class NegativeFrameInputCoordinator : LogicWorldInputCoordinatorBase<object>
    {
        protected override object CreateContext(FrameIndex frame, IReadOnlyList<PlayerInputCommand> inputs)
        {
            return new object();
        }

        protected override bool Dispatch(object context, FrameIndex frame, PlayerInputCommand command, out MobaInputCommandResult result)
        {
            result = MobaInputCommandResult.Accepted(command);
            return true;
        }
    }

    private sealed class AcceptingInputPort : IMobaBattleInputPort
    {
        public static readonly AcceptingInputPort Instance = new();

        public MobaInputSubmitResult Submit(FrameIndex frame, IReadOnlyList<PlayerInputCommand> inputs)
        {
            return MobaInputSubmitResult.Accepted(inputs?.Count ?? 0);
        }
    }

    private sealed class EmptyStateReadModel : IMobaLogicWorldStateReadModel
    {
        public static readonly EmptyStateReadModel Instance = new();

        public MobaDiagnosticEntityState[] GetDiagnosticEntityStates()
        {
            return Array.Empty<MobaDiagnosticEntityState>();
        }

        public int FillDiagnosticEntityStates(IList<MobaDiagnosticEntityState> buffer)
        {
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
            return 0;
        }

        public LogicWorldEntityState[] GetAllEntityStates()
        {
            return Array.Empty<LogicWorldEntityState>();
        }

        public int FillAllEntityStates(IList<LogicWorldEntityState> buffer)
        {
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
            return 0;
        }
    }

    private sealed class ScriptedStateReadModel : IMobaLogicWorldStateReadModel
    {
        public int GetAllEntityStatesCallCount { get; private set; }
        public int FillAllEntityStatesCallCount { get; private set; }

        public MobaDiagnosticEntityState[] GetDiagnosticEntityStates()
        {
            return Array.Empty<MobaDiagnosticEntityState>();
        }

        public int FillDiagnosticEntityStates(IList<MobaDiagnosticEntityState> buffer)
        {
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
            return 0;
        }

        public LogicWorldEntityState[] GetAllEntityStates()
        {
            GetAllEntityStatesCallCount++;
            return new[]
            {
                new LogicWorldEntityState(1001),
                new LogicWorldEntityState(1002),
            };
        }

        public int FillAllEntityStates(IList<LogicWorldEntityState> buffer)
        {
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));

            FillAllEntityStatesCallCount++;
            buffer.Add(new LogicWorldEntityState(1001));
            buffer.Add(new LogicWorldEntityState(1002));
            return 2;
        }
    }
}
