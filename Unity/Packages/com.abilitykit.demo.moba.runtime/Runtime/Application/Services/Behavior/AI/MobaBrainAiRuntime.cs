using System;
using System.Collections.Generic;
using AbilityKit.AI.Abstractions;
using AbilityKit.Ability.Behavior;
using AbilityKit.Ability.Host.Extensions.Moba.Runtime;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Input;
using Newtonsoft.Json;

namespace AbilityKit.Demo.Moba.Services.Behavior.AI
{
    public readonly struct MobaBrainObservationOptions
    {
        public MobaBrainObservationOptions(
            int maxObservedEntities = 8,
            float positionExtent = 64f,
            float hitPointScale = 1000f)
        {
            MaxObservedEntities = maxObservedEntities < 1 ? 1 : maxObservedEntities;
            PositionExtent = positionExtent > 0f ? positionExtent : 64f;
            HitPointScale = hitPointScale > 0f ? hitPointScale : 1000f;
        }

        public int MaxObservedEntities { get; }
        public float PositionExtent { get; }
        public float HitPointScale { get; }
        public static MobaBrainObservationOptions Default =>
            new MobaBrainObservationOptions(8, 64f, 1000f);
    }

    /// <summary>
    /// Versioned encoder shared by offline training and live actor-brain inference.
    /// Entity order is supplied by the caller and is part of the observation contract.
    /// </summary>
    public sealed class MobaBrainObservationEncoder
    {
        private const int EntityValueCount = 10;
        private const int GlobalValueCount = 4;
        private readonly MobaBrainObservationOptions _options;

        public MobaBrainObservationEncoder(MobaBrainObservationOptions options)
        {
            _options = Normalize(in options);
        }

        public AiObservationSpec ObservationSpec => CreateSpec(in _options);

        public static AiObservationSpec CreateSpec(in MobaBrainObservationOptions options)
        {
            var normalized = Normalize(in options);
            return new AiObservationSpec(
                "moba.runtime-state.v1",
                normalized.MaxObservedEntities * EntityValueCount + GlobalValueCount);
        }

        public void Write(
            IReadOnlyList<LogicWorldEntityState> states,
            int ownerActorId,
            long frame,
            bool inputReady,
            bool inMatch,
            AiObservationBuffer buffer)
        {
            if (states == null) throw new ArgumentNullException(nameof(states));
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
            if (!Matches(buffer.Spec, ObservationSpec))
                throw new ArgumentException("MOBA observation buffer does not match the encoder spec.", nameof(buffer));

            buffer.Clear();
            var owner = FindOwner(states, ownerActorId);
            var index = 0;
            var written = 0;
            for (var i = 0; i < states.Count && written < _options.MaxObservedEntities; i++)
            {
                var state = states[i];
                buffer[index++] = NormalizeDelta(state.X - owner.X, _options.PositionExtent);
                buffer[index++] = NormalizeDelta(state.Y - owner.Y, _options.PositionExtent);
                buffer[index++] = NormalizeDelta(state.Z - owner.Z, _options.PositionExtent);
                buffer[index++] = NormalizePositive(state.Hp, state.HpMax > 0f ? state.HpMax : _options.HitPointScale);
                buffer[index++] = NormalizePositive(state.HpMax, _options.HitPointScale);
                buffer[index++] = state.TeamId == owner.TeamId && state.TeamId != 0 ? 1f : -1f;
                buffer[index++] = state.IsDead ? 0f : 1f;
                buffer[index++] = state.HasSkillLoadout ? 1f : 0f;
                buffer[index++] = NormalizePositive(state.ActiveSkillCount, 8f);
                buffer[index++] = state.EntityId == ownerActorId ? 1f : 0f;
                written++;
            }

            index += (_options.MaxObservedEntities - written) * EntityValueCount;
            buffer[index++] = NormalizePositive(frame, 36000f);
            buffer[index++] = NormalizePositive(states.Count, _options.MaxObservedEntities);
            buffer[index++] = inputReady ? 1f : 0f;
            buffer[index] = inMatch ? 1f : 0f;
        }

        private static LogicWorldEntityState FindOwner(
            IReadOnlyList<LogicWorldEntityState> states,
            int ownerActorId)
        {
            for (var i = 0; i < states.Count; i++)
            {
                if (states[i].EntityId == ownerActorId) return states[i];
            }

            return states.Count > 0 ? states[0] : LogicWorldEntityState.Empty(ownerActorId);
        }

        private static bool Matches(in AiObservationSpec left, in AiObservationSpec right) =>
            string.Equals(left.Id, right.Id, StringComparison.Ordinal) && left.Length == right.Length;

        private static MobaBrainObservationOptions Normalize(in MobaBrainObservationOptions options) =>
            options.MaxObservedEntities > 0 ? options : MobaBrainObservationOptions.Default;

        private static float NormalizeDelta(float value, float extent) =>
            IsFinite(value) ? ClampUnit(value / extent) : 0f;

        private static float NormalizePositive(float value, float max) =>
            !IsFinite(value) || !IsFinite(max) || max <= 0f ? 0f : Clamp01(value / max);

        private static float NormalizePositive(long value, float max) =>
            value <= 0L ? 0f : Clamp01(value / max);

        private static float ClampUnit(float value) => value < -1f ? -1f : value > 1f ? 1f : value;
        private static float Clamp01(float value) => value < 0f ? 0f : value > 1f ? 1f : value;
        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }

    /// <summary>
    /// Canonical action space shared by model training and live inference.
    /// </summary>
    public static class MobaBrainActionCodec
    {
        public static AiActionSpec ActionSpec { get; } =
            new AiActionSpec("moba.input.v1", continuousLength: 2, discreteLength: 1);

        public static MobaActorIntent Decode(AiActionBuffer action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            if (!Matches(action.Spec, ActionSpec))
                throw new ArgumentException("MOBA action buffer does not match the canonical action spec.", nameof(action));

            var moveX = action.Continuous.Length > 0 ? action.Continuous[0] : 0f;
            var moveZ = action.Continuous.Length > 1 ? action.Continuous[1] : 0f;
            var intent = MobaActorIntent.MoveDirection(moveX, moveZ);
            var slot = action.Discrete.Length > 0 ? action.Discrete[0] : 0;
            slot = slot < 0 ? 0 : slot > 3 ? 3 : slot;
            return slot > 0 ? intent.WithCast(slot) : intent;
        }

        public static bool Matches(in AiActionSpec left, in AiActionSpec right) =>
            string.Equals(left.Id, right.Id, StringComparison.Ordinal)
            && left.ContinuousLength == right.ContinuousLength
            && left.DiscreteLength == right.DiscreteLength;
    }

    public interface IMobaActorIntentDecision
    {
        MobaActorIntent CurrentIntent { get; }
    }

    public interface IMobaBrainObservationProvider
    {
        AiObservationSpec ObservationSpec { get; }

        void Write(int ownerActorId, long frame, AiObservationBuffer buffer);
    }

    public sealed class MobaRegistryBrainObservationProvider : IMobaBrainObservationProvider
    {
        private readonly MobaBattleStateQueryService _stateQuery;
        private readonly MobaBrainObservationEncoder _encoder;
        private readonly List<LogicWorldEntityState> _states = new(16);

        public MobaRegistryBrainObservationProvider(
            MobaActorRegistry actors,
            MobaBrainObservationOptions options = default)
        {
            _stateQuery = new MobaBattleStateQueryService(
                actors ?? throw new ArgumentNullException(nameof(actors)));
            _encoder = new MobaBrainObservationEncoder(options.MaxObservedEntities > 0
                ? options
                : MobaBrainObservationOptions.Default);
        }

        public AiObservationSpec ObservationSpec => _encoder.ObservationSpec;

        public void Write(int ownerActorId, long frame, AiObservationBuffer buffer)
        {
            _states.Clear();
            _stateQuery.FillAllEntityStates(_states);
            _states.Sort(static (left, right) => left.EntityId.CompareTo(right.EntityId));
            _encoder.Write(_states, ownerActorId, frame, inputReady: true, inMatch: true, buffer);
        }
    }

    public delegate IAiPolicy MobaAiPolicyFactory(in MobaBrainDecisionCreateContext context);

    public sealed class MobaMlBrainDecisionDriver :
        IMobaBrainDecisionDriver,
        IMobaBrainDecisionDriverValidator
    {
        private readonly Dictionary<string, MobaAiPolicyFactory> _factories =
            new(StringComparer.Ordinal);

        public string Kind => MobaBrainDriverKeys.MachineLearning;

        public void Register(string policyName, MobaAiPolicyFactory factory)
        {
            if (string.IsNullOrWhiteSpace(policyName))
                throw new ArgumentException("An ML policy name is required.", nameof(policyName));
            if (factory == null) throw new ArgumentNullException(nameof(factory));
            _factories[policyName] = factory;
        }

        public bool Contains(string policyName) =>
            !string.IsNullOrWhiteSpace(policyName) && _factories.ContainsKey(policyName);

        public void ValidateDefinition(
            in MobaActorBrainDefinition definition,
            ICollection<string> errors)
        {
            if (errors == null) throw new ArgumentNullException(nameof(errors));
            if (!Contains(definition.DecisionName))
            {
                errors.Add(
                    $"Brain '{definition.BrainId}' references missing ML policy '{definition.DecisionName}'.");
            }
        }

        public bool TryCreate(
            in MobaBrainDecisionCreateContext context,
            out IBehaviorDecision decision)
        {
            decision = null;
            if (context.Registry == null
                || string.IsNullOrWhiteSpace(context.Definition.DecisionName)
                || !_factories.TryGetValue(context.Definition.DecisionName, out var factory))
                return false;

            var policy = factory(in context);
            if (policy == null || !MobaBrainActionCodec.Matches(policy.ActionSpec, MobaBrainActionCodec.ActionSpec))
            {
                (policy as IDisposable)?.Dispose();
                return false;
            }

            var observations = new MobaRegistryBrainObservationProvider(context.Registry);
            decision = new MobaMlBehaviorDecision(policy, observations, (int)context.OwnerActorId);
            return true;
        }
    }

    internal sealed class MobaMlBehaviorDecision :
        IBehaviorDecision,
        IMobaActorIntentDecision,
        IBehaviorRuntimeSnapshot,
        IDisposable
    {
        private readonly IAiPolicy _policy;
        private readonly IMobaBrainObservationProvider _observations;
        private readonly AiObservationBuffer _observation;
        private readonly AiActionBuffer _action;
        private readonly int _ownerActorId;
        private bool _disposed;

        public MobaMlBehaviorDecision(
            IAiPolicy policy,
            IMobaBrainObservationProvider observations,
            int ownerActorId)
        {
            _policy = policy ?? throw new ArgumentNullException(nameof(policy));
            _observations = observations ?? throw new ArgumentNullException(nameof(observations));
            _ownerActorId = ownerActorId;
            _observation = new AiObservationBuffer(observations.ObservationSpec);
            _action = new AiActionBuffer(MobaBrainActionCodec.ActionSpec);
        }

        public string DecisionType => "MobaMachineLearning";
        public string CurrentState { get; private set; } = "Holding";
        public MobaActorIntent CurrentIntent { get; private set; } = MobaActorIntent.Hold;
        public string SnapshotType => _policy is IAiPolicyRuntimeSnapshot stateful
            ? "MobaML.Runtime.v1:" + stateful.SnapshotType
            : "MobaML.Stateless.v1";

        public DecisionResult Decide(IBehaviorContext context, IWorldQuery world)
        {
            if (_disposed || context == null) return DecisionResult.Continue(CurrentState);

            _observations.Write(_ownerActorId, context.CurrentFrame, _observation);
            _action.Clear();
            var observation = _observation;
            _policy.Decide(in observation, _action);
            CurrentIntent = MobaBrainActionCodec.Decode(_action);
            var currentIntent = CurrentIntent;
            CurrentState = ResolveState(in currentIntent);
            return DecisionResult.Continue(CurrentState);
        }

        public byte[] CaptureSnapshot()
        {
            var payload = new MlRuntimeSnapshot
            {
                PolicySnapshot = _policy is IAiPolicyRuntimeSnapshot stateful
                    ? stateful.CaptureSnapshot() ?? Array.Empty<byte>()
                    : Array.Empty<byte>(),
                MovementKind = (int)CurrentIntent.MovementKind,
                MoveX = CurrentIntent.MoveX,
                MoveZ = CurrentIntent.MoveZ,
                MoveTargetX = CurrentIntent.MoveTarget.X,
                MoveTargetY = CurrentIntent.MoveTarget.Y,
                MoveTargetZ = CurrentIntent.MoveTarget.Z,
                HasCast = CurrentIntent.HasCast,
                SkillId = CurrentIntent.SkillId,
                SkillSlot = CurrentIntent.SkillSlot,
                TargetActorId = CurrentIntent.TargetActorId,
                AimPositionX = CurrentIntent.AimPosition.X,
                AimPositionY = CurrentIntent.AimPosition.Y,
                AimPositionZ = CurrentIntent.AimPosition.Z,
                AimDirectionX = CurrentIntent.AimDirection.X,
                AimDirectionY = CurrentIntent.AimDirection.Y,
                AimDirectionZ = CurrentIntent.AimDirection.Z,
            };
            return System.Text.Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(payload));
        }

        public void RestoreSnapshot(byte[] payload)
        {
            if (payload == null || payload.Length == 0)
                throw new InvalidOperationException("ML brain snapshot payload is empty.");
            var json = System.Text.Encoding.UTF8.GetString(payload);
            var snapshot = JsonConvert.DeserializeObject<MlRuntimeSnapshot>(json)
                ?? throw new InvalidOperationException("ML brain snapshot payload is invalid.");
            if (snapshot.Version != 1)
                throw new InvalidOperationException($"Unsupported ML brain snapshot version '{snapshot.Version}'.");

            if (_policy is IAiPolicyRuntimeSnapshot stateful)
            {
                stateful.RestoreSnapshot(snapshot.PolicySnapshot ?? Array.Empty<byte>());
            }

            var moveTarget = new Vec3(
                snapshot.MoveTargetX,
                snapshot.MoveTargetY,
                snapshot.MoveTargetZ);
            var intent = snapshot.MovementKind switch
            {
                (int)MobaActorMovementIntentKind.Direction =>
                    MobaActorIntent.MoveDirection(snapshot.MoveX, snapshot.MoveZ),
                (int)MobaActorMovementIntentKind.TargetPosition =>
                    MobaActorIntent.MoveTo(in moveTarget),
                _ => MobaActorIntent.Hold,
            };
            if (snapshot.HasCast)
            {
                var aimPosition = new Vec3(
                    snapshot.AimPositionX,
                    snapshot.AimPositionY,
                    snapshot.AimPositionZ);
                var aimDirection = new Vec3(
                    snapshot.AimDirectionX,
                    snapshot.AimDirectionY,
                    snapshot.AimDirectionZ);
                intent = intent.WithCast(
                    snapshot.SkillSlot,
                    snapshot.SkillId,
                    snapshot.TargetActorId,
                    in aimPosition,
                    in aimDirection);
            }
            if (!intent.IsValid())
                throw new InvalidOperationException("ML brain snapshot contains an invalid actor intent.");
            CurrentIntent = intent;
            CurrentState = ResolveState(in intent);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            (_policy as IDisposable)?.Dispose();
        }

        private static string ResolveState(in MobaActorIntent intent)
        {
            if (intent.HasCast) return "Casting";
            return intent.MovementKind == MobaActorMovementIntentKind.Direction
                   && (Math.Abs(intent.MoveX) > 0.0001f || Math.Abs(intent.MoveZ) > 0.0001f)
                ? "Moving"
                : "Holding";
        }

        private sealed class MlRuntimeSnapshot
        {
            public int Version { get; set; } = 1;
            public byte[] PolicySnapshot { get; set; }
            public int MovementKind { get; set; }
            public float MoveX { get; set; }
            public float MoveZ { get; set; }
            public float MoveTargetX { get; set; }
            public float MoveTargetY { get; set; }
            public float MoveTargetZ { get; set; }
            public bool HasCast { get; set; }
            public int SkillId { get; set; }
            public int SkillSlot { get; set; }
            public int TargetActorId { get; set; }
            public float AimPositionX { get; set; }
            public float AimPositionY { get; set; }
            public float AimPositionZ { get; set; }
            public float AimDirectionX { get; set; }
            public float AimDirectionY { get; set; }
            public float AimDirectionZ { get; set; }
        }
    }
}
