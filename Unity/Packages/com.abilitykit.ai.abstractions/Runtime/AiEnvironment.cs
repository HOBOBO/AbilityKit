using System;

namespace AbilityKit.AI.Abstractions
{
    public enum AiSpaceValueType
    {
        Float32 = 0,
        Int32 = 1,
        Boolean = 2
    }

    public enum AiActionValueKind
    {
        Continuous = 0,
        Discrete = 1
    }

    public readonly struct AiObservationSpec
    {
        public AiObservationSpec(string id, int length, AiSpaceValueType valueType = AiSpaceValueType.Float32)
        {
            Id = string.IsNullOrWhiteSpace(id) ? "observation" : id;
            Length = length < 0 ? 0 : length;
            ValueType = valueType;
        }

        public string Id { get; }

        public int Length { get; }

        public AiSpaceValueType ValueType { get; }
    }

    public sealed class AiObservationBuffer
    {
        private readonly float[] _values;

        public AiObservationBuffer(AiObservationSpec spec)
        {
            Spec = spec;
            _values = spec.Length == 0 ? Array.Empty<float>() : new float[spec.Length];
        }

        public AiObservationSpec Spec { get; }

        public int Length => _values.Length;

        public float[] Values => _values;

        public void Clear()
        {
            Array.Clear(_values, 0, _values.Length);
        }

        public float this[int index]
        {
            get => _values[index];
            set => _values[index] = value;
        }
    }

    public readonly struct AiActionSpec
    {
        public AiActionSpec(string id, int continuousLength, int discreteLength)
        {
            Id = string.IsNullOrWhiteSpace(id) ? "action" : id;
            ContinuousLength = continuousLength < 0 ? 0 : continuousLength;
            DiscreteLength = discreteLength < 0 ? 0 : discreteLength;
        }

        public string Id { get; }

        public int ContinuousLength { get; }

        public int DiscreteLength { get; }
    }

    public sealed class AiActionBuffer
    {
        private readonly float[] _continuous;
        private readonly int[] _discrete;

        public AiActionBuffer(AiActionSpec spec)
        {
            Spec = spec;
            _continuous = spec.ContinuousLength == 0 ? Array.Empty<float>() : new float[spec.ContinuousLength];
            _discrete = spec.DiscreteLength == 0 ? Array.Empty<int>() : new int[spec.DiscreteLength];
        }

        public AiActionSpec Spec { get; }

        public float[] Continuous => _continuous;

        public int[] Discrete => _discrete;

        public void Clear()
        {
            Array.Clear(_continuous, 0, _continuous.Length);
            Array.Clear(_discrete, 0, _discrete.Length);
        }
    }

    public readonly struct AiEpisodeOptions
    {
        public AiEpisodeOptions(int seed, int maxSteps, float fixedDeltaSeconds = 1f / 30f)
        {
            Seed = seed;
            MaxSteps = maxSteps < 1 ? 1 : maxSteps;
            FixedDeltaSeconds = fixedDeltaSeconds > 0f ? fixedDeltaSeconds : 1f / 30f;
        }

        public int Seed { get; }

        public int MaxSteps { get; }

        public float FixedDeltaSeconds { get; }
    }

    public readonly struct AiStepResult
    {
        public AiStepResult(AiObservationBuffer observation, float reward, bool done, bool truncated, int stepIndex, uint stateHash = 0)
        {
            Observation = observation ?? throw new ArgumentNullException(nameof(observation));
            Reward = reward;
            Done = done;
            Truncated = truncated;
            StepIndex = stepIndex < 0 ? 0 : stepIndex;
            StateHash = stateHash;
        }

        public AiObservationBuffer Observation { get; }

        public float Reward { get; }

        public bool Done { get; }

        public bool Truncated { get; }

        public int StepIndex { get; }

        public uint StateHash { get; }
    }

    public interface IAiPolicy
    {
        AiActionSpec ActionSpec { get; }

        void Decide(in AiObservationBuffer observation, AiActionBuffer action);
    }

    /// <summary>
    /// Optional policy-owned state used by recurrent models and rollback-capable runtimes.
    /// Stateless policies do not need to implement this contract.
    /// </summary>
    public interface IAiPolicyRuntimeSnapshot
    {
        string SnapshotType { get; }

        byte[] CaptureSnapshot();

        void RestoreSnapshot(byte[] payload);
    }

    public interface IAiEnvironment
    {
        AiObservationSpec ObservationSpec { get; }

        AiActionSpec ActionSpec { get; }

        AiStepResult Reset(in AiEpisodeOptions options);

        AiStepResult Step(in AiActionBuffer action);
    }
}
