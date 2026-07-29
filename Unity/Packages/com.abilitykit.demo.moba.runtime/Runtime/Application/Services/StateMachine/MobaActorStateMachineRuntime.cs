using System;
using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;
using AbilityKit.Demo.Moba.Services.Projectile;
using UnityHFSM;
using UnityHFSM.Extension;

namespace AbilityKit.Demo.Moba.Services.StateMachine
{
    public readonly struct MobaHfsmActionSpec
    {
        public MobaHfsmActionSpec(string type, string argument = null)
        {
            Type = type ?? string.Empty;
            Argument = argument ?? string.Empty;
        }

        public string Type { get; }

        public string Argument { get; }
    }

    public sealed class MobaActorStateMachineBlackboard
    {
        public MobaActorStateMachineBlackboard(global::ActorEntity actor, IWorldResolver services)
        {
            Actor = actor ?? throw new ArgumentNullException(nameof(actor));
            Services = services;
        }

        public global::ActorEntity Actor { get; }

        public IWorldResolver Services { get; }
    }

    public interface IMobaActorStateMachineProfileCatalog : IService
    {
        bool TryGet(string profileId, out HfsmHierarchicalRuntimeProfile<MobaHfsmActionSpec> profile);
    }

    [WorldService(typeof(IMobaActorStateMachineProfileCatalog))]
    public sealed class MobaActorStateMachineProfileCatalog : IMobaActorStateMachineProfileCatalog
    {
        private readonly Dictionary<string, HfsmHierarchicalRuntimeProfile<MobaHfsmActionSpec>> _profiles =
            new Dictionary<string, HfsmHierarchicalRuntimeProfile<MobaHfsmActionSpec>>(StringComparer.Ordinal);

        public void Register(HfsmHierarchicalRuntimeProfile<MobaHfsmActionSpec> profile)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            if (string.IsNullOrWhiteSpace(profile.Id))
                throw new ArgumentException("A state-machine profile id is required.", nameof(profile));

            _profiles[profile.Id] = profile;
        }

        public bool TryGet(string profileId, out HfsmHierarchicalRuntimeProfile<MobaHfsmActionSpec> profile)
        {
            profile = null;
            return !string.IsNullOrWhiteSpace(profileId) && _profiles.TryGetValue(profileId, out profile);
        }

        public void Dispose()
        {
            _profiles.Clear();
        }
    }

    public delegate IActionBehaviour MobaHfsmActionFactory(
        MobaActorStateMachineBlackboard blackboard,
        string argument);

    public delegate bool MobaHfsmConditionEvaluator(
        MobaActorStateMachineBlackboard blackboard,
        string argument);

    [WorldService(typeof(MobaActorStateMachineRuntimeRegistry))]
    public sealed class MobaActorStateMachineRuntimeRegistry : IService
    {
        private readonly Dictionary<string, MobaHfsmActionFactory> _actions =
            new Dictionary<string, MobaHfsmActionFactory>(StringComparer.Ordinal);
        private readonly Dictionary<string, MobaHfsmConditionEvaluator> _conditions =
            new Dictionary<string, MobaHfsmConditionEvaluator>(StringComparer.Ordinal);

        public MobaActorStateMachineRuntimeRegistry()
        {
            RegisterAction("noop", (_, __) => new CallbackBehaviour(null));
            RegisterCondition("always", (_, __) => true);
            RegisterCondition("never", (_, __) => false);
            MobaProjectileHfsmActions.Register(this);
        }

        public void RegisterAction(string type, MobaHfsmActionFactory factory)
        {
            if (string.IsNullOrWhiteSpace(type)) throw new ArgumentException("An action type is required.", nameof(type));
            _actions[type] = factory ?? throw new ArgumentNullException(nameof(factory));
        }

        public void RegisterCondition(string type, MobaHfsmConditionEvaluator evaluator)
        {
            if (string.IsNullOrWhiteSpace(type)) throw new ArgumentException("A condition type is required.", nameof(type));
            _conditions[type] = evaluator ?? throw new ArgumentNullException(nameof(evaluator));
        }

        public IActionBehaviour CreateAction(MobaActorStateMachineBlackboard blackboard, MobaHfsmActionSpec spec)
        {
            if (!_actions.TryGetValue(spec.Type ?? string.Empty, out var factory))
                throw new InvalidOperationException($"MOBA HFSM action '{spec.Type}' is not registered.");

            return factory(blackboard, spec.Argument);
        }

        public bool EvaluateCondition(MobaActorStateMachineBlackboard blackboard, string expression)
        {
            SplitExpression(expression, out var type, out var argument);
            if (!_conditions.TryGetValue(type, out var evaluator))
                throw new InvalidOperationException($"MOBA HFSM condition '{type}' is not registered.");

            return evaluator(blackboard, argument);
        }

        public void Dispose()
        {
            _actions.Clear();
            _conditions.Clear();
        }

        private static void SplitExpression(string expression, out string type, out string argument)
        {
            expression ??= string.Empty;
            var separator = expression.IndexOf(':');
            if (separator < 0)
            {
                type = expression;
                argument = string.Empty;
                return;
            }

            type = expression.Substring(0, separator);
            argument = expression.Substring(separator + 1);
        }
    }

    internal sealed class MobaActorStateMachineTimeSource : IActionTimeSource
    {
        public float DeltaTime { get; set; }

        public float UnscaledDeltaTime => DeltaTime;
    }

    public sealed class MobaActorStateMachineRuntime : IDisposable
    {
        private readonly MobaActorStateMachineTimeSource _timeSource;
        private MobaActorStateMachineState _state;
        private bool _disposed;

        internal MobaActorStateMachineRuntime(
            string profileId,
            MobaActorStateMachineBlackboard blackboard,
            StateMachine<string> stateMachine,
            MobaActorStateMachineTimeSource timeSource)
        {
            ProfileId = profileId ?? string.Empty;
            Blackboard = blackboard ?? throw new ArgumentNullException(nameof(blackboard));
            StateMachine = stateMachine ?? throw new ArgumentNullException(nameof(stateMachine));
            _timeSource = timeSource ?? throw new ArgumentNullException(nameof(timeSource));
            _state = new MobaActorStateMachineState(
                stateMachine.GetActiveHierarchyPath(),
                enteredFrame: -1,
                lastUpdatedFrame: -1,
                durationFrames: 0,
                durationSeconds: 0f);
        }

        public string ProfileId { get; }

        public MobaActorStateMachineBlackboard Blackboard { get; }

        public StateMachine<string> StateMachine { get; }

        public float DeltaTime => _timeSource.DeltaTime;

        public MobaActorStateMachineState State => _state;

        public bool IsDisposed => _disposed;

        public void Tick(float deltaTime)
        {
            var nextFrame = _state.LastUpdatedFrame < 0 ? 0 : _state.LastUpdatedFrame + 1;
            Tick(new FrameIndex(nextFrame), deltaTime);
        }

        public void Tick(FrameIndex frame, float deltaTime)
        {
            if (_disposed || deltaTime <= 0f) return;

            var statePathBeforeTick = StateMachine.GetActiveHierarchyPath();
            EnsureStateInitialized(statePathBeforeTick, frame.Value);

            _timeSource.DeltaTime = deltaTime;
            StateMachine.OnLogic();

            var statePathAfterTick = StateMachine.GetActiveHierarchyPath();
            if (!string.Equals(statePathBeforeTick, statePathAfterTick, StringComparison.Ordinal))
            {
                _state = new MobaActorStateMachineState(
                    statePathAfterTick,
                    frame.Value,
                    frame.Value,
                    durationFrames: 0,
                    durationSeconds: 0f);
                return;
            }

            var durationFrames = frame.Value >= _state.EnteredFrame
                ? frame.Value - _state.EnteredFrame
                : _state.DurationFrames + 1;
            _state = new MobaActorStateMachineState(
                statePathAfterTick,
                _state.EnteredFrame,
                frame.Value,
                durationFrames,
                _state.DurationSeconds + deltaTime);
        }

        public MobaActorStateMachineRuntimeSnapshot CaptureSnapshot()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(MobaActorStateMachineRuntime));
            return new MobaActorStateMachineRuntimeSnapshot(
                ProfileId,
                _timeSource.DeltaTime,
                _state,
                HfsmRuntimeSnapshotUtility.Capture(StateMachine));
        }

        public void RestoreSnapshot(MobaActorStateMachineRuntimeSnapshot snapshot)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(MobaActorStateMachineRuntime));
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (!string.Equals(ProfileId, snapshot.ProfileId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"State-machine snapshot profile '{snapshot.ProfileId}' does not match runtime profile '{ProfileId}'.");
            }

            HfsmRuntimeSnapshotUtility.Restore(StateMachine, snapshot.Root);
            _timeSource.DeltaTime = snapshot.DeltaTime;
            _state = snapshot.State;
        }

        private void EnsureStateInitialized(string activeStatePath, int frame)
        {
            if (_state.EnteredFrame >= 0
                && string.Equals(_state.ActiveStatePath, activeStatePath, StringComparison.Ordinal))
            {
                return;
            }

            _state = new MobaActorStateMachineState(
                activeStatePath,
                frame,
                frame,
                durationFrames: 0,
                durationSeconds: 0f);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _timeSource.DeltaTime = 0f;
            StateMachine.OnExit();
        }
    }

    public readonly struct MobaActorStateMachineState
    {
        public MobaActorStateMachineState(
            string activeStatePath,
            int enteredFrame,
            int lastUpdatedFrame,
            int durationFrames,
            float durationSeconds)
        {
            ActiveStatePath = activeStatePath ?? string.Empty;
            EnteredFrame = enteredFrame;
            LastUpdatedFrame = lastUpdatedFrame;
            DurationFrames = durationFrames;
            DurationSeconds = durationSeconds;
        }

        public string ActiveStatePath { get; }
        public int EnteredFrame { get; }
        public int LastUpdatedFrame { get; }
        public int DurationFrames { get; }
        public float DurationSeconds { get; }
    }

    public sealed class MobaActorStateMachineRuntimeSnapshot
    {
        public MobaActorStateMachineRuntimeSnapshot(
            string profileId,
            float deltaTime,
            MobaActorStateMachineState state,
            HfsmRuntimeSnapshot root)
        {
            ProfileId = profileId ?? string.Empty;
            DeltaTime = deltaTime;
            State = state;
            Root = root ?? throw new ArgumentNullException(nameof(root));
        }

        public string ProfileId { get; }
        public float DeltaTime { get; }
        public MobaActorStateMachineState State { get; }
        public HfsmRuntimeSnapshot Root { get; }
    }

    [WorldService(typeof(MobaActorStateMachineFactory), WorldLifetime.Scoped)]
    public sealed class MobaActorStateMachineFactory : IService
    {
        private readonly IWorldResolver _services;
        private readonly IMobaActorStateMachineProfileCatalog _profiles;
        private readonly MobaActorStateMachineRuntimeRegistry _registry;

        public MobaActorStateMachineFactory(
            IWorldResolver services,
            IMobaActorStateMachineProfileCatalog profiles,
            MobaActorStateMachineRuntimeRegistry registry)
        {
            _services = services;
            _profiles = profiles ?? throw new ArgumentNullException(nameof(profiles));
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        }

        public bool TryCreate(global::ActorEntity actor, string profileId, out MobaActorStateMachineRuntime runtime)
        {
            runtime = null;
            if (actor == null || !_profiles.TryGet(profileId, out var profile) || profile == null) return false;

            var blackboard = new MobaActorStateMachineBlackboard(actor, _services);
            var timeSource = new MobaActorStateMachineTimeSource();
            var builder = new HfsmHierarchicalRuntimeProfileBuilder<MobaActorStateMachineBlackboard, MobaHfsmActionSpec>(
                _registry.CreateAction,
                _registry.EvaluateCondition);

            var stateMachine = builder.Build(timeSource, blackboard, profile);
            runtime = new MobaActorStateMachineRuntime(profileId, blackboard, stateMachine, timeSource);
            return true;
        }

        public void Dispose()
        {
        }
    }
}
