#nullable enable
using System;
using System.Collections.Generic;
using AbilityKit.Deterministic;

namespace AbilityKit.HFSM
{
    public readonly struct HfsmTickContext
    {
        public HfsmTickContext(int frame, Fixed64 time, Fixed64 deltaTime)
        {
            Frame = frame;
            TimeRaw = time.RawValue;
            DeltaTimeRaw = deltaTime.RawValue;
        }

        public int Frame { get; }

        public long TimeRaw { get; }

        public long DeltaTimeRaw { get; }

        public Fixed64 Time => Fixed64.FromRaw(TimeRaw);

        public Fixed64 DeltaTime => Fixed64.FromRaw(DeltaTimeRaw);
    }

    public readonly struct HfsmTransitionContext
    {
        internal HfsmTransitionContext(
            HfsmTickContext tick,
            string machineId,
            string fromStateId,
            HfsmTransitionDefinition transition,
            string triggerId,
            long activeSinceRaw)
        {
            Tick = tick;
            MachineId = machineId;
            FromStateId = fromStateId;
            ToStateId = transition.ToStateId;
            TransitionId = transition.Id;
            TriggerId = triggerId;
            ActiveDurationRaw = checked(tick.TimeRaw - activeSinceRaw);
        }

        public HfsmTickContext Tick { get; }

        public string MachineId { get; }

        public string FromStateId { get; }

        public string ToStateId { get; }

        public string TransitionId { get; }

        public string TriggerId { get; }

        public long ActiveDurationRaw { get; }

        public Fixed64 ActiveDuration => Fixed64.FromRaw(ActiveDurationRaw);
    }

    public interface IHfsmState<in TOwner>
    {
        void OnEnter(TOwner owner, in HfsmTickContext context);

        void OnTick(TOwner owner, in HfsmTickContext context);

        void OnExitRequested(TOwner owner, in HfsmTickContext context);

        bool CanExit(TOwner owner, in HfsmTickContext context);

        void OnExit(TOwner owner, in HfsmTickContext context);
    }

    public abstract class HfsmStateBase<TOwner> : IHfsmState<TOwner>
    {
        public virtual void OnEnter(TOwner owner, in HfsmTickContext context)
        {
        }

        public virtual void OnTick(TOwner owner, in HfsmTickContext context)
        {
        }

        public virtual void OnExitRequested(TOwner owner, in HfsmTickContext context)
        {
        }

        public virtual bool CanExit(TOwner owner, in HfsmTickContext context) => true;

        public virtual void OnExit(TOwner owner, in HfsmTickContext context)
        {
        }
    }

    /// <summary>
    /// Optional state-owned rollback payload. Validation is called for every participant before
    /// the runtime mutates structural state during restore.
    /// </summary>
    public interface IHfsmStateSnapshotParticipant
    {
        int SnapshotVersion { get; }

        string CaptureSnapshot();

        void ValidateSnapshot(int version, string payload);

        void RestoreSnapshot(int version, string payload);
    }

    /// <summary>Transition conditions must be deterministic and side-effect free.</summary>
    public interface IHfsmTransitionCondition<in TOwner>
    {
        bool Evaluate(TOwner owner, in HfsmTransitionContext context);
    }

    public interface IHfsmTransitionAction<in TOwner>
    {
        void BeforeTransition(TOwner owner, in HfsmTransitionContext context);

        void AfterTransition(TOwner owner, in HfsmTransitionContext context);
    }

    public enum HfsmRuntimeEventType
    {
        Initialized = 0,
        StateEntered = 1,
        ExitRequested = 2,
        StateExited = 3,
        TransitionCompleted = 4,
        Shutdown = 5,
        Faulted = 6,
        Restored = 7,
    }

    public readonly struct HfsmRuntimeEvent
    {
        internal HfsmRuntimeEvent(
            HfsmRuntimeEventType type,
            HfsmTickContext tick,
            string machineId,
            string stateId,
            string transitionId,
            string triggerId)
        {
            Type = type;
            Tick = tick;
            MachineId = machineId;
            StateId = stateId;
            TransitionId = transitionId;
            TriggerId = triggerId;
        }

        public HfsmRuntimeEventType Type { get; }

        public HfsmTickContext Tick { get; }

        public string MachineId { get; }

        public string StateId { get; }

        public string TransitionId { get; }

        public string TriggerId { get; }
    }

    /// <summary>
    /// Read-only diagnostics hook. Observer exceptions are isolated and never affect simulation.
    /// </summary>
    public interface IHfsmRuntimeObserver
    {
        void OnRuntimeEvent(in HfsmRuntimeEvent runtimeEvent);
    }

    public sealed class HfsmRuntimeBindings<TOwner>
    {
        private readonly Dictionary<string, Func<IHfsmState<TOwner>>> _stateFactories =
            new Dictionary<string, Func<IHfsmState<TOwner>>>(StringComparer.Ordinal);

        private readonly Dictionary<string, Func<IHfsmTransitionCondition<TOwner>>> _conditionFactories =
            new Dictionary<string, Func<IHfsmTransitionCondition<TOwner>>>(StringComparer.Ordinal);

        private readonly Dictionary<string, Func<IHfsmTransitionAction<TOwner>>> _actionFactories =
            new Dictionary<string, Func<IHfsmTransitionAction<TOwner>>>(StringComparer.Ordinal);

        public HfsmRuntimeBindings<TOwner> RegisterState(
            string key,
            Func<IHfsmState<TOwner>> factory)
        {
            Register(_stateFactories, key, factory, "state");
            return this;
        }

        public HfsmRuntimeBindings<TOwner> RegisterCondition(
            string key,
            Func<IHfsmTransitionCondition<TOwner>> factory)
        {
            Register(_conditionFactories, key, factory, "condition");
            return this;
        }

        public HfsmRuntimeBindings<TOwner> RegisterAction(
            string key,
            Func<IHfsmTransitionAction<TOwner>> factory)
        {
            Register(_actionFactories, key, factory, "action");
            return this;
        }

        internal IHfsmState<TOwner> CreateState(string key)
        {
            if (string.IsNullOrEmpty(key)) return new NoOpState();
            return Create(_stateFactories, key, "state");
        }

        internal IHfsmTransitionCondition<TOwner>? CreateCondition(string key)
        {
            return string.IsNullOrEmpty(key) ? null : Create(_conditionFactories, key, "condition");
        }

        internal IHfsmTransitionAction<TOwner>? CreateAction(string key)
        {
            return string.IsNullOrEmpty(key) ? null : Create(_actionFactories, key, "action");
        }

        private static void Register<T>(Dictionary<string, Func<T>> factories, string key, Func<T> factory, string kind)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException($"HFSM {kind} binding key is required.", nameof(key));
            if (factory == null) throw new ArgumentNullException(nameof(factory));
            if (!factories.TryAdd(key, factory))
                throw new InvalidOperationException($"HFSM {kind} binding '{key}' is already registered.");
        }

        private static T Create<T>(Dictionary<string, Func<T>> factories, string key, string kind)
        {
            if (!factories.TryGetValue(key, out var factory))
                throw new InvalidOperationException($"HFSM {kind} binding '{key}' is not registered.");

            var instance = factory();
            if (instance == null)
                throw new InvalidOperationException($"HFSM {kind} binding '{key}' returned null.");
            return instance;
        }

        private sealed class NoOpState : HfsmStateBase<TOwner>
        {
        }
    }

    public sealed class HfsmRuntimeFaultedException : InvalidOperationException
    {
        public HfsmRuntimeFaultedException()
            : base("The HFSM runtime is faulted. Restore a valid snapshot or create a new runtime before continuing.")
        {
        }
    }
}
