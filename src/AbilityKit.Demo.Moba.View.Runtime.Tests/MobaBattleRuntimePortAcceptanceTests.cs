using System;
using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.Host.Extensions.Moba.Runtime;
using AbilityKit.Ability.World.Services;
using AbilityKit.Game.Battle;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.LogicWorld;
using AbilityKit.Protocol.Moba;
using Xunit;

namespace AbilityKit.Demo.Moba.View.Runtime.Tests;

/// <summary>
/// Acceptance tests for <see cref="MobaBattleRuntimePort"/>.
/// Pins the stable behaviour any host (ET / Orleans / Unity View) can rely on:
/// - capability bits map 1:1 to injected ports
/// - missing ports surface as typed failure codes instead of exceptions
/// - snapshot / state read models use buffer-fill, never array allocation
/// </summary>
public sealed class MobaBattleRuntimePortAcceptanceTests
{
    [Fact]
    public void Status_reports_all_capabilities_when_all_ports_are_resolved()
    {
        var port = new MobaBattleRuntimePort(
            new FakeGameStartPort(),
            new FakeInputPort(),
            new FakeOutputPort(),
            new FakeStateReadModel());

        var status = port.Status;
        var statusProvider = Assert.IsAssignableFrom<IBattleRuntimeStatusProvider>(port);
        var battleStatus = statusProvider.BattleStatus;

        Assert.True(status.Has(MobaBattleRuntimeCapability.GameStart));
        Assert.True(status.Has(MobaBattleRuntimeCapability.Input));
        Assert.True(status.Has(MobaBattleRuntimeCapability.SnapshotOutput));
        Assert.True(status.Has(MobaBattleRuntimeCapability.StateReadModel));
        Assert.True(status.IsReadyForGameStart);
        Assert.True(status.IsReadyForBattleLoop);
        Assert.Equal(string.Empty, status.MissingServices);

        Assert.Equal(BattleRuntimeState.Ready, battleStatus.State);
        Assert.True(battleStatus.Has(
            BattleRuntimeCapability.GameStart |
            BattleRuntimeCapability.Input |
            BattleRuntimeCapability.SnapshotOutput |
            BattleRuntimeCapability.StateReadModel));
        Assert.False(battleStatus.Has(BattleRuntimeCapability.Simulation));
        Assert.False(battleStatus.Meets(BattleReadinessRequirement.BattleLoop));
        Assert.Equal(string.Empty, battleStatus.MissingDependencies);
    }

    [Fact]
    public void Status_lists_missing_services_when_ports_are_not_resolved()
    {
        var port = new MobaBattleRuntimePort(
            gameStart: null,
            input: null,
            output: null,
            stateReadModel: null);

        var status = port.Status;
        var battleStatus = ((IBattleRuntimeStatusProvider)port).BattleStatus;

        Assert.Equal(MobaBattleRuntimeCapability.None, status.Capabilities);
        Assert.False(status.IsReadyForGameStart);
        Assert.False(status.IsReadyForBattleLoop);
        Assert.Contains(nameof(IMobaGameStartPort), status.MissingServices);
        Assert.Contains(nameof(IMobaBattleInputPort), status.MissingServices);
        Assert.Contains(nameof(IMobaBattleOutputPort), status.MissingServices);
        Assert.Contains(nameof(IMobaLogicWorldStateReadModel), status.MissingServices);

        Assert.Equal(BattleRuntimeCapability.None, battleStatus.Capabilities);
        Assert.Equal(BattleRuntimeState.Created, battleStatus.State);
        Assert.Equal(status.MissingServices, battleStatus.MissingDependencies);
    }

    [Fact]
    public void Try_start_game_returns_missing_port_failure_when_game_start_is_null()
    {
        var port = new MobaBattleRuntimePort(
            gameStart: null,
            new FakeInputPort(),
            new FakeOutputPort(),
            new FakeStateReadModel());

        var result = port.TryStartGame(default);

        Assert.False(result.Succeeded);
        Assert.Equal(MobaGameStartFailureCode.MissingGameStartPort, result.FailureCode);
    }

    [Fact]
    public void Submit_returns_missing_input_port_failure_when_input_port_is_null()
    {
        var port = new MobaBattleRuntimePort(
            new FakeGameStartPort(),
            input: null,
            new FakeOutputPort(),
            new FakeStateReadModel());

        var result = port.Submit(new FrameIndex(1), new List<PlayerInputCommand>());

        Assert.False(result.Succeeded);
        Assert.Equal(MobaInputSubmitFailureCode.MissingInputPort, result.FailureCode);
    }

    [Fact]
    public void Submit_propagates_typed_input_failure_code_from_input_port()
    {
        var port = new MobaBattleRuntimePort(
            new FakeGameStartPort(),
            new FakeInputPort(MobaInputSubmitFailureCode.PartialCommandHandled, "only 2 of 3"),
            new FakeOutputPort(),
            new FakeStateReadModel());

        var result = port.Submit(new FrameIndex(7), new List<PlayerInputCommand>());

        Assert.False(result.Succeeded);
        Assert.Equal(MobaInputSubmitFailureCode.PartialCommandHandled, result.FailureCode);
        Assert.Equal("only 2 of 3", result.Message);
    }

    [Fact]
    public void Collect_snapshots_uses_caller_buffer_and_returns_sink_count()
    {
        var sink = new FakeOutputPort();
        sink.QueueSnapshot(new FrameIndex(11), new WorldStateSnapshot());
        sink.QueueSnapshot(new FrameIndex(11), new WorldStateSnapshot());
        var port = new MobaBattleRuntimePort(
            new FakeGameStartPort(),
            new FakeInputPort(),
            sink,
            new FakeStateReadModel());

        var buffer = new List<WorldStateSnapshot>();
        var count = port.CollectSnapshots(new FrameIndex(11), buffer, maxSnapshots: 4);

        Assert.Equal(2, count);
        Assert.Equal(2, buffer.Count);
        Assert.Equal(sink.Snapshots[0], buffer[0]);
        Assert.Equal(sink.Snapshots[1], buffer[1]);
    }

    [Fact]
    public void Fill_all_entity_states_fills_caller_buffer_from_read_model()
    {
        var readModel = new FakeStateReadModel();
        readModel.QueueEntityState(new LogicWorldEntityState());
        readModel.QueueEntityState(new LogicWorldEntityState());
        var port = new MobaBattleRuntimePort(
            new FakeGameStartPort(),
            new FakeInputPort(),
            new FakeOutputPort(),
            readModel);

        var buffer = new List<LogicWorldEntityState>();

        var filled = port.FillAllEntityStates(buffer);

        Assert.Equal(2, filled);
        Assert.Equal(2, buffer.Count);
        Assert.Empty(port.GetAllEntityStates());
    }

    [Fact]
    public void Fill_diagnostic_entity_states_fills_caller_buffer_from_read_model()
    {
        var readModel = new FakeStateReadModel();
        readModel.QueueDiagnosticState(new MobaDiagnosticEntityState());
        var port = new MobaBattleRuntimePort(
            new FakeGameStartPort(),
            new FakeInputPort(),
            new FakeOutputPort(),
            readModel);

        var buffer = new List<MobaDiagnosticEntityState>();

        var filled = port.FillDiagnosticEntityStates(buffer);

        Assert.Equal(1, filled);
        Assert.Single(buffer);
    }

    private sealed class FakeGameStartPort : IMobaGameStartPort
    {
        public bool Disposed { get; private set; }

        public MobaGameStartResult TryStartGame(in MobaGameStartSpec spec) => MobaGameStartResult.Success;

        public void Dispose() => Disposed = true;
    }

    private sealed class FakeInputPort : IMobaBattleInputPort
    {
        private readonly MobaInputSubmitFailureCode _code;
        private readonly string _message;

        public FakeInputPort(MobaInputSubmitFailureCode code = MobaInputSubmitFailureCode.None, string message = null)
        {
            _code = code;
            _message = message;
        }

        public MobaInputSubmitResult Submit(FrameIndex frame, IReadOnlyList<PlayerInputCommand> inputs)
        {
            return _code == MobaInputSubmitFailureCode.None
                ? MobaInputSubmitResult.Accepted(inputs?.Count ?? 0)
                : MobaInputSubmitResult.Fail(_code, _message);
        }
    }

    private sealed class FakeOutputPort : IMobaBattleOutputPort
    {
        private readonly Dictionary<long, Queue<WorldStateSnapshot>> _queue = new Dictionary<long, Queue<WorldStateSnapshot>>();

        public IReadOnlyList<WorldStateSnapshot> Snapshots { get; private set; } = Array.Empty<WorldStateSnapshot>();

        public void QueueSnapshot(FrameIndex frame, WorldStateSnapshot snapshot)
        {
            if (!_queue.TryGetValue(frame.Value, out var queue))
            {
                queue = new Queue<WorldStateSnapshot>();
                _queue[frame.Value] = queue;
            }

            queue.Enqueue(snapshot);
        }

public bool TryGetSnapshot(FrameIndex frame, out WorldStateSnapshot snapshot)
    {
        if (_queue.TryGetValue(frame.Value, out var queue) && queue.Count > 0)
        {
            snapshot = queue.Peek();
            return true;
        }

        snapshot = default;
        return false;
    }

        public int CollectSnapshots(FrameIndex frame, IList<WorldStateSnapshot> snapshots, int maxSnapshots = 32)
        {
            if (snapshots == null) throw new ArgumentNullException(nameof(snapshots));
            if (!_queue.TryGetValue(frame.Value, out var queue) || queue.Count == 0)
            {
                Snapshots = Array.Empty<WorldStateSnapshot>();
                return 0;
            }

            var limit = Math.Min(queue.Count, maxSnapshots);
            var collected = new List<WorldStateSnapshot>(limit);
            for (var i = 0; i < limit; i++)
            {
                collected.Add(queue.Dequeue());
            }

            Snapshots = collected;
            for (var i = 0; i < collected.Count; i++)
            {
                snapshots.Add(collected[i]);
            }

            return collected.Count;
        }
    }

    private sealed class FakeStateReadModel : IMobaLogicWorldStateReadModel
    {
        private readonly Queue<LogicWorldEntityState> _states = new Queue<LogicWorldEntityState>();
        private readonly Queue<MobaDiagnosticEntityState> _diagnostics = new Queue<MobaDiagnosticEntityState>();

        public void QueueEntityState(LogicWorldEntityState state) => _states.Enqueue(state);

        public void QueueDiagnosticState(MobaDiagnosticEntityState state) => _diagnostics.Enqueue(state);

        public LogicWorldEntityState[] GetAllEntityStates() => _states.ToArray();

        public int FillAllEntityStates(IList<LogicWorldEntityState> buffer)
        {
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
            var count = _states.Count;
            while (_states.Count > 0)
            {
                buffer.Add(_states.Dequeue());
            }

            return count;
        }

        public MobaDiagnosticEntityState[] GetDiagnosticEntityStates() => _diagnostics.ToArray();

        public int FillDiagnosticEntityStates(IList<MobaDiagnosticEntityState> buffer)
        {
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
            var count = _diagnostics.Count;
            while (_diagnostics.Count > 0)
            {
                buffer.Add(_diagnostics.Dequeue());
            }

            return count;
        }
    }
}