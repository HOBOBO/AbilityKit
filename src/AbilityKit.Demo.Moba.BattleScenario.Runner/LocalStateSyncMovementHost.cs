using AbilityKit.Ability.StateSync.Prediction.Reconciliation;
using AbilityKit.Ability.World;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.World.DI;
using AbilityKit.Attributes.Core;
using AbilityKit.Combat.MotionSystem.Core;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Attributes;
using AbilityKit.Demo.Moba.EntitasAdapters;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.Motion;
using AbilityKit.Demo.Moba.Systems.Motion;
using AbilityKit.Network.Runtime.Sync;

namespace AbilityKit.Demo.Moba.BattleScenario.Runner;

/// <summary>
/// 本地验收宿主，仅预测单个存活主控的普通移动，不包含技能、碰撞或动态移速。
/// 权威值携带位置、朝向和持续移动输入；重演使用隔离世界，成功追平才写入预测世界。
/// </summary>
internal sealed class LocalStateSyncMovementHost : IDisposable
{
    internal enum Comparison { Match, Mismatch, MissingHistory }

    internal readonly record struct Input(long Sequence, float X, float Z);
    internal readonly record struct State(Transform3 Pose, float MoveX, float MoveZ);
    internal readonly record struct Snapshot(long Revision, int Tick, State State, long? TerminalThrough);
    internal readonly record struct ReplaySample(int Tick, State Before, State After, int InputCount);
    internal readonly record struct Delivery(Comparison Comparison, int ReplayedTicks, bool RebasedLiveWorld,
        State Authority, ReplaySample[] Steps, ReconciliationStatus Status);

    private readonly ReconciliationScope _scope = new(1, 1, Guid.NewGuid(), MovementWorld.ActorId, 1, 1, 1);
    private readonly MovementWorld _live;
    private readonly MovementWorld _replay;
    private readonly ClientSnapshotSyncPipeline<Snapshot> _snapshots;
    private readonly LocalMovementReconciler<State, Input> _reconciler;
    private readonly (int Tick, State State)?[] _history;
    private readonly List<ReplaySample> _replayed = new();
    private State _imported;
    private long _lastInput;

    internal LocalStateSyncMovementHost(float deltaTime, int historyCapacity = 64)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(historyCapacity);
        _live = new MovementWorld("local-statesync-live", deltaTime);
        _replay = new MovementWorld("local-statesync-replay", deltaTime);
        _history = new (int, State)?[historyCapacity];
        _reconciler = new LocalMovementReconciler<State, Input>(_scope, ReplayTick);
        _snapshots = new ClientSnapshotSyncPipeline<Snapshot>(new ClientSnapshotSyncOptions<Snapshot>(1, 1,
            (in Snapshot value) => new SnapshotStreamEnvelope(1, 1, value.Revision, value.Tick,
                SnapshotStreamSnapshotKind.FullBaseline, value.Tick, 1, 1),
            (in Snapshot value) =>
            {
                _replay.Import(value.State);
                _imported = _replay.Capture();
            }));
        Remember(0, Current);
    }

    internal int Tick { get; private set; }
    internal int StateTick { get; private set; }
    internal bool IsPredicting { get; private set; } = true;
    internal State Current => _live.Capture();
    internal long TerminalSequence => _reconciler.TerminalSequence;
    internal long[] PendingSequences => Enumerable.Range(0, _reconciler.PendingCount)
        .Select(_reconciler.GetPendingSequence).ToArray();

    internal void Advance(params Input[] inputs)
    {
        var lastSequence = _lastInput;
        foreach (var input in inputs)
        {
            if (input.Sequence <= lastSequence || !float.IsFinite(input.X) || !float.IsFinite(input.Z))
                throw new ArgumentException("Expected finite, strictly ordered local movement inputs.", nameof(inputs));
            lastSequence = input.Sequence;
        }
        _lastInput = lastSequence;

        Tick++;
        foreach (var input in inputs)
            IsPredicting &= _reconciler.TryRecordInput(_scope, input.Sequence, Tick, input);
        _reconciler.SealTick(_scope, Tick);
        if (!IsPredicting) return;

        _live.Step(inputs);
        StateTick = Tick;
        Remember(Tick, Current);
    }

    internal Delivery? Receive(in Snapshot snapshot)
    {
        if (snapshot.Tick < 0 || snapshot.Tick > Tick || snapshot.Revision <= 0 ||
            snapshot.TerminalThrough > _lastInput)
            throw new ArgumentOutOfRangeException(nameof(snapshot));

        var comparison = _history[snapshot.Tick % _history.Length] is { } sample && sample.Tick == snapshot.Tick
            ? sample.State == snapshot.State ? Comparison.Match : Comparison.Mismatch
            : Comparison.MissingHistory;
        if (!_snapshots.Apply(in snapshot).Applied) return null;

        var stream = _snapshots.State;
        var committed = new CommittedMovement<State>(_scope, stream.LastAppliedSequence,
            stream.LastAppliedFrame, _imported, isFullBaseline: true);
        InputTerminalProof? proof = snapshot.TerminalThrough is { } sequence
            ? new InputTerminalProof(_scope, committed.Revision, committed.Tick, sequence) : null;
        _replayed.Clear();
        var reconciled = _reconciler.Reconcile(in committed, proof, Tick);
        var hasReplay = _reconciler.TryGetPrediction(out var corrected);
        var result = hasReplay ? corrected : _imported;
        var resultTick = hasReplay ? Tick : committed.Tick;
        var rebased = comparison != Comparison.Match || StateTick != resultTick || Current != result;
        if (rebased) _live.Import(result);
        StateTick = resultTick;
        IsPredicting = hasReplay || reconciled.Status == ReconciliationStatus.AuthoritativeOnly;
        for (var index = 0; index < _history.Length; index++)
            if (_history[index] is { } old && old.Tick > committed.Tick) _history[index] = null;
        Remember(committed.Tick, _imported);
        if (hasReplay)
            foreach (var entry in _replayed) Remember(entry.Tick, entry.After);
        return new Delivery(comparison, reconciled.AttemptedTicks, rebased, _imported,
            hasReplay ? _replayed.ToArray() : Array.Empty<ReplaySample>(), reconciled.Status);
    }

    private void Remember(int tick, State state) => _history[tick % _history.Length] = (tick, state);

    private bool ReplayTick(int tick, ReadOnlySpan<Input> inputs, ref State candidate)
    {
        var before = candidate;
        _replay.Step(inputs);
        candidate = _replay.Capture();
        _replayed.Add(new ReplaySample(tick, before, candidate, inputs.Length));
        return true;
    }

    public void Dispose()
    {
        _replay.Dispose();
        _live.Dispose();
    }

    /// <summary>
    /// 与正式世界共用普通移动 System；仅持有可安全重建的单 Actor 和固定依赖。
    /// 每步重新取样持续 MoveInput；MotionOutput、速度与时间累积不参与该规则的下一步。
    /// </summary>
    internal sealed class MovementWorld : IDisposable
    {
        internal const int ActorId = 1;
        private readonly EntitasWorld _world;
        private readonly ActorEntity _actor;
        private readonly MotionPipeline _pipeline = new();
        private readonly float _deltaTime;

        internal MovementWorld(string id, float deltaTime)
        {
            if (!float.IsFinite(deltaTime) || deltaTime <= 0f) throw new ArgumentOutOfRangeException(nameof(deltaTime));
            _deltaTime = deltaTime;
            var options = new WorldCreateOptions(new WorldId(id), "local-statesync-movement")
                .SetEntitasContextsFactory(new MobaEntitasContextsFactory());
            options.Modules.Add(new MovementModule());
            _world = new EntitasWorld(options);
            _world.Initialize();
            _actor = ((Contexts)_world.Contexts).actor.CreateEntity();
            _actor.AddActorId(ActorId);
            _world.Services.Resolve<MobaActorRegistry>().Register(ActorId, _actor);
            var attributes = new AttributeContext();
            attributes.SetBase(MobaAttributeIds.MOVE_SPEED, 7.5f);
            _actor.AddAttributeGroup(attributes.GetGroupFor(MobaAttributeIds.MOVE_SPEED), attributes);
            Import(new State(new Transform3(new Vec3(3, 0, 8), Quat.Identity, Vec3.One), 0f, 0f));
        }

        internal State Capture() => new(_actor.transform.Value, _actor.moveInput.Dx, _actor.moveInput.Dz);

        internal void Import(in State state)
        {
            _actor.ReplaceTransform(state.Pose);
            _actor.ReplaceMoveInput(state.MoveX, state.MoveZ);
            var motion = new MotionState(state.Pose.Position) { Forward = state.Pose.Forward };
            _actor.ReplaceMotion(_pipeline, motion, default, _pipeline.Solver, null, null, true, default);
        }

        internal void Step(ReadOnlySpan<Input> inputs)
        {
            foreach (var input in inputs) _actor.ReplaceMoveInput(input.X, input.Z);
            _world.Tick(_deltaTime);
        }

        public void Dispose()
        {
            _world.Dispose();
            _pipeline.Dispose();
        }

        private sealed class MovementModule : IWorldModule, IEntitasSystemsInstaller
        {
            public void Configure(WorldContainerBuilder builder)
            {
                builder.Register(WorldLifetime.Scoped, _ => new MobaActorRegistry());
                builder.Register(WorldLifetime.Scoped, _ => new MobaLocomotionRuntimeService());
            }

            public void Install(Entitas.IContexts contexts, Entitas.Systems systems, IWorldResolver services)
            {
                systems.Add(new MobaMotionLocomotionInputSystem(contexts, services));
                systems.Add(new MobaMotionTickSystem(contexts, services));
            }
        }
    }
}
