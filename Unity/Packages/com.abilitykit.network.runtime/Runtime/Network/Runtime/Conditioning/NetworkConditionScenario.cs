#nullable enable

using System;
using System.Collections.Generic;

namespace AbilityKit.Network.Runtime.Conditioning
{
    public enum NetworkConditionDirection
    {
        Both,
        Inbound,
        Outbound
    }

    /// <summary>Time-based packet rules relative to an explicit origin, or the first observed packet.</summary>
    public sealed class NetworkConditionScenario
    {
        public readonly struct Phase
        {
            public Phase(long startMs, long endMs, NetworkConditionProfile profile,
                NetworkConditionDirection direction = NetworkConditionDirection.Both, uint? opCode = null)
            {
                if (startMs < 0 || endMs <= startMs) throw new ArgumentOutOfRangeException(nameof(endMs));
                StartMs = startMs;
                EndMs = endMs;
                Profile = profile;
                Direction = direction;
                OpCode = opCode;
            }

            public long StartMs { get; }
            public long EndMs { get; }
            public NetworkConditionProfile Profile { get; }
            public NetworkConditionDirection Direction { get; }
            public uint? OpCode { get; }
        }

        private readonly Phase[] _phases;

        public NetworkConditionScenario(NetworkConditionProfile baseline, params Phase[] phases)
        {
            Baseline = baseline;
            _phases = (Phase[])(phases ?? throw new ArgumentNullException(nameof(phases))).Clone();
        }

        public NetworkConditionProfile Baseline { get; }
        public IReadOnlyList<Phase> Phases => Array.AsReadOnly(_phases);

        internal NetworkConditionProfile Resolve(long elapsedMs, bool inbound, uint opCode)
        {
            // Later matching rules win, so a narrow opcode rule can override a broad phase.
            var profile = Baseline;
            foreach (var phase in _phases)
            {
                if (elapsedMs >= phase.StartMs && elapsedMs < phase.EndMs &&
                    (phase.Direction == NetworkConditionDirection.Both ||
                     (inbound ? phase.Direction == NetworkConditionDirection.Inbound : phase.Direction == NetworkConditionDirection.Outbound)) &&
                    (!phase.OpCode.HasValue || phase.OpCode.Value == opCode))
                    profile = phase.Profile;
            }

            return profile;
        }
    }
}
