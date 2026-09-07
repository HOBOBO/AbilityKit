using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;
using AbilityKit.Demo.Moba.Services.Projectile;
using AbilityKit.HFSM;
using AbilityKit.HFSM.Extension;

namespace AbilityKit.Demo.Moba.Services.StateMachine
{
    public readonly struct MobaActorStateMachineBinding : IEquatable<MobaActorStateMachineBinding>
    {
        public MobaActorStateMachineBinding(
            int actorId,
            int brainId,
            int ownerActorId,
            int sourceKind,
            int sourceId,
            string profileId)
        {
            ActorId = actorId;
            BrainId = brainId;
            OwnerActorId = ownerActorId;
            SourceKind = sourceKind;
            SourceId = sourceId;
            ProfileId = profileId ?? string.Empty;
        }

        public int ActorId { get; }
        public int BrainId { get; }
        public int OwnerActorId { get; }
        public int SourceKind { get; }
        public int SourceId { get; }
        public string ProfileId { get; }

        public static MobaActorStateMachineBinding From(global::ActorEntity actor, string profileId)
        {
            if (actor == null)
                return new MobaActorStateMachineBinding(0, 0, 0, 0, 0, profileId);

            var actorId = actor.hasActorId ? actor.actorId.Value : 0;
            if (!actor.hasActorBrain)
                return new MobaActorStateMachineBinding(actorId, 0, 0, 0, 0, profileId);

            var brain = actor.actorBrain;
            return new MobaActorStateMachineBinding(
                actorId,
                brain.BrainId,
                brain.OwnerActorId,
                brain.SourceKind,
                brain.SourceId,
                profileId);
        }

        public bool Equals(MobaActorStateMachineBinding other)
        {
            return ActorId == other.ActorId
                && BrainId == other.BrainId
                && OwnerActorId == other.OwnerActorId
                && SourceKind == other.SourceKind
                && SourceId == other.SourceId
                && string.Equals(ProfileId, other.ProfileId, StringComparison.Ordinal);
        }

        public override bool Equals(object obj) => obj is MobaActorStateMachineBinding other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = ActorId;
                hash = (hash * 397) ^ BrainId;
                hash = (hash * 397) ^ OwnerActorId;
                hash = (hash * 397) ^ SourceKind;
                hash = (hash * 397) ^ SourceId;
                hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(ProfileId ?? string.Empty);
                return hash;
            }
        }
    }

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
        IReadOnlyList<HierarchicalProfile<MobaHfsmActionSpec>> Profiles { get; }

        bool TryGet(string profileId, out HierarchicalProfile<MobaHfsmActionSpec> profile);

        bool TryGetContentHash(string profileId, out string contentHash);
    }

    public sealed class MobaActorStateMachineProfileCatalog : IMobaActorStateMachineProfileCatalog
    {
        private readonly Dictionary<string, HierarchicalProfile<MobaHfsmActionSpec>> _profiles =
            new Dictionary<string, HierarchicalProfile<MobaHfsmActionSpec>>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _contentHashes =
            new Dictionary<string, string>(StringComparer.Ordinal);

        public IReadOnlyList<HierarchicalProfile<MobaHfsmActionSpec>> Profiles
        {
            get
            {
                var profiles = new List<HierarchicalProfile<MobaHfsmActionSpec>>(_profiles.Values);
                profiles.Sort((left, right) => StringComparer.Ordinal.Compare(left.Id, right.Id));
                return profiles;
            }
        }

        public void Register(HierarchicalProfile<MobaHfsmActionSpec> profile)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            if (string.IsNullOrWhiteSpace(profile.Id))
                throw new ArgumentException("A state-machine profile id is required.", nameof(profile));
            if (_profiles.ContainsKey(profile.Id))
                throw new InvalidOperationException($"MOBA state-machine profile id '{profile.Id}' is duplicated.");

            _profiles.Add(profile.Id, profile);
            _contentHashes.Add(profile.Id, MobaActorStateMachineProfileContentHash.Compute(profile));
        }

        public bool TryGet(string profileId, out HierarchicalProfile<MobaHfsmActionSpec> profile)
        {
            profile = null;
            return !string.IsNullOrWhiteSpace(profileId) && _profiles.TryGetValue(profileId, out profile);
        }

        public bool TryGetContentHash(string profileId, out string contentHash)
        {
            contentHash = string.Empty;
            return !string.IsNullOrWhiteSpace(profileId)
                && _contentHashes.TryGetValue(profileId, out contentHash);
        }

        public void Dispose()
        {
            _profiles.Clear();
            _contentHashes.Clear();
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

        public bool ContainsAction(string type)
        {
            return !string.IsNullOrWhiteSpace(type) && _actions.ContainsKey(type);
        }

        public bool ContainsCondition(string expression)
        {
            SplitExpression(expression, out var type, out _);
            return !string.IsNullOrWhiteSpace(type) && _conditions.ContainsKey(type);
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

    public static class MobaActorStateMachineProfileContentHash
    {
        public static string Compute(HierarchicalProfile<MobaHfsmActionSpec> profile)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));

            var canonical = new StringBuilder(512);
            AppendString(canonical, profile.Id);
            AppendString(canonical, profile.StartState);
            AppendStates(canonical, profile.States);
            AppendTransitions(canonical, profile.Transitions);

            using (var sha256 = SHA256.Create())
            {
                var bytes = Encoding.UTF8.GetBytes(canonical.ToString());
                var hash = sha256.ComputeHash(bytes);
                var result = new StringBuilder(hash.Length * 2);
                for (var i = 0; i < hash.Length; i++) result.Append(hash[i].ToString("x2", CultureInfo.InvariantCulture));
                return result.ToString();
            }
        }

        private static void AppendStates(
            StringBuilder target,
            IReadOnlyList<NodeSpec<MobaHfsmActionSpec>> states)
        {
            target.Append(states?.Count ?? 0).Append(';');
            if (states == null) return;
            for (var i = 0; i < states.Count; i++)
            {
                var state = states[i];
                if (state == null)
                {
                    target.Append("null;");
                    continue;
                }

                target.Append((int)state.Kind).Append(';');
                AppendString(target, state.Id);
                AppendString(target, state.StartState);
                target.Append((int)state.CompletionPolicy).Append(';')
                    .Append(state.NeedsExitTime ? 1 : 0).Append(';')
                    .Append(state.RememberLastState ? 1 : 0).Append(';');
                AppendBehaviour(target, state.BehaviourRoot);
                AppendStates(target, state.Children);
                AppendTransitions(target, state.Transitions);
            }
        }

        private static void AppendTransitions(
            StringBuilder target,
            IReadOnlyList<TransitionSpec> transitions)
        {
            target.Append(transitions?.Count ?? 0).Append(';');
            if (transitions == null) return;
            for (var i = 0; i < transitions.Count; i++)
            {
                var transition = transitions[i];
                AppendString(target, transition.From);
                AppendString(target, transition.To);
                AppendString(target, transition.Condition);
                target.Append((int)transition.Mode).Append(';')
                    .Append(transition.Priority).Append(';')
                    .Append(transition.ForceInstantly ? 1 : 0).Append(';');
            }
        }

        private static void AppendBehaviour(
            StringBuilder target,
            BehaviourSpec<MobaHfsmActionSpec> behaviour)
        {
            if (behaviour == null)
            {
                target.Append("null;");
                return;
            }

            target.Append((int)behaviour.Kind).Append(';');
            AppendString(target, behaviour.Action.Type);
            AppendString(target, behaviour.Action.Argument);
            target.Append(behaviour.RepeatCount).Append(';');
            AppendString(target, behaviour.DurationSeconds.ToString("R", CultureInfo.InvariantCulture));
            target.Append(behaviour.UseUnscaledTime ? 1 : 0).Append(';')
                .Append((int)behaviour.ParallelSuccessPolicy).Append(';')
                .Append((int)behaviour.ParallelFailurePolicy).Append(';');
            AppendString(target, behaviour.Condition);
            target.Append(behaviour.Children?.Count ?? 0).Append(';');
            if (behaviour.Children == null) return;
            for (var i = 0; i < behaviour.Children.Count; i++)
                AppendBehaviour(target, behaviour.Children[i]);
        }

        private static void AppendString(StringBuilder target, string value)
        {
            value ??= string.Empty;
            target.Append(value.Length).Append(':').Append(value).Append(';');
        }
    }

    public sealed class MobaActorStateMachineRuntime : IDisposable
    {
        private readonly MobaActorStateMachineTimeSource _timeSource;
        private MobaActorStateMachineState _state;
        private bool _disposed;

        internal MobaActorStateMachineRuntime(
            string profileId,
            string profileContentHash,
            MobaActorStateMachineBinding binding,
            MobaActorStateMachineBlackboard blackboard,
            StateMachine<string> stateMachine,
            MobaActorStateMachineTimeSource timeSource)
        {
            ProfileId = profileId ?? string.Empty;
            ProfileContentHash = profileContentHash ?? string.Empty;
            Binding = binding;
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

        public string ProfileContentHash { get; }

        public MobaActorStateMachineBinding Binding { get; }

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
                ProfileContentHash,
                _timeSource.DeltaTime,
                _state,
                RuntimeSnapshotUtility.Capture(StateMachine));
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
            if (!string.IsNullOrEmpty(snapshot.ProfileContentHash)
                && !string.Equals(ProfileContentHash, snapshot.ProfileContentHash, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"State-machine snapshot profile '{snapshot.ProfileId}' content hash does not match the runtime profile.");
            }

            RuntimeSnapshotUtility.Restore(StateMachine, snapshot.Root);
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
            string profileContentHash,
            float deltaTime,
            MobaActorStateMachineState state,
            RuntimeSnapshot root)
        {
            ProfileId = profileId ?? string.Empty;
            ProfileContentHash = profileContentHash ?? string.Empty;
            DeltaTime = deltaTime;
            State = state;
            Root = root ?? throw new ArgumentNullException(nameof(root));
        }

        public string ProfileId { get; }
        public string ProfileContentHash { get; }
        public float DeltaTime { get; }
        public MobaActorStateMachineState State { get; }
        public RuntimeSnapshot Root { get; }
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

        public bool TryGetProfileContentHash(string profileId, out string contentHash)
        {
            return _profiles.TryGetContentHash(profileId, out contentHash);
        }

        public bool TryCreate(global::ActorEntity actor, string profileId, out MobaActorStateMachineRuntime runtime)
        {
            runtime = null;
            if (actor == null || !_profiles.TryGet(profileId, out var profile) || profile == null) return false;
            if (!_profiles.TryGetContentHash(profileId, out var profileContentHash))
                throw new InvalidOperationException($"MOBA state-machine profile '{profileId}' has no content hash.");

            var blackboard = new MobaActorStateMachineBlackboard(actor, _services);
            var timeSource = new MobaActorStateMachineTimeSource();
            var builder = new HierarchicalProfileBuilder<MobaActorStateMachineBlackboard, MobaHfsmActionSpec>(
                _registry.CreateAction,
                _registry.EvaluateCondition);

            var stateMachine = builder.Build(timeSource, blackboard, profile);
            runtime = new MobaActorStateMachineRuntime(
                profileId,
                profileContentHash,
                MobaActorStateMachineBinding.From(actor, profileId),
                blackboard,
                stateMachine,
                timeSource);
            return true;
        }

        public void Dispose()
        {
        }
    }
}
