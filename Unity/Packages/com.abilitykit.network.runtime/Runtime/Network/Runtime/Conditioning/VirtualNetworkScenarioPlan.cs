#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;

namespace AbilityKit.Network.Runtime.Conditioning
{
    /// <summary>
    /// Immutable precompiled network plan. Phase commands configure the link before playback;
    /// the remaining commands are executed by <see cref="VirtualNetworkScenarioPlayer"/>.
    /// </summary>
    public sealed class VirtualNetworkScenarioPlan
    {
        private readonly VirtualNetworkCommand[] _commands;

        private VirtualNetworkScenarioPlan(
            NetworkConditionScenario scenario,
            VirtualNetworkCommand[] commands)
        {
            Scenario = scenario;
            _commands = commands;
        }

        public NetworkConditionScenario Scenario { get; }
        public IReadOnlyList<VirtualNetworkCommand> Commands => Array.AsReadOnly(_commands);

        public static VirtualNetworkScenarioPlan Compile(
            IEnumerable<VirtualNetworkCommand> commands,
            NetworkConditionProfile baseline)
        {
            if (commands == null) throw new ArgumentNullException(nameof(commands));

            var phases = new List<NetworkConditionScenario.Phase>();
            var playback = new List<VirtualNetworkCommand>();
            foreach (var command in commands)
            {
                if (command == null) throw new ArgumentException("Commands cannot contain null.", nameof(commands));
                if (string.Equals(command.Name, VirtualNetworkScenarioPlayer.PhaseCommand,
                        StringComparison.Ordinal))
                    phases.Add(ParsePhase(command, baseline));
                else
                    playback.Add(command);
            }

            return new VirtualNetworkScenarioPlan(
                new NetworkConditionScenario(baseline, phases.ToArray()),
                playback.ToArray());
        }

        private static NetworkConditionScenario.Phase ParsePhase(
            VirtualNetworkCommand command,
            NetworkConditionProfile baseline)
        {
            var endMs = ParseLong(command, "until");
            var latency = ParseOptionalInt(command, "latency", baseline.BaseLatencyMs);
            var jitter = ParseOptionalInt(command, "jitter", baseline.JitterMs);
            var loss = ParseOptionalRate(command, "loss", baseline.PacketLossRate);
            var reorder = ParseOptionalRate(command, "reorder", baseline.ReorderRate);
            var bandwidth = ParseOptionalInt(command, "bandwidth", baseline.BandwidthKbps);
            var direction = ParseDirection(GetOptional(command.Parameters, "direction"));
            var opCodeText = GetOptional(command.Parameters, "opCode");
            uint? opCode = null;
            if (opCodeText != null)
            {
                if (!uint.TryParse(opCodeText, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed))
                    throw Invalid(command, "opCode", opCodeText);
                opCode = parsed;
            }

            return new NetworkConditionScenario.Phase(
                command.AtMs,
                endMs,
                new NetworkConditionProfile(latency, jitter, loss, reorder, bandwidth),
                direction,
                opCode);
        }

        private static NetworkConditionDirection ParseDirection(string? text)
        {
            if (text == null || string.Equals(text, "both", StringComparison.OrdinalIgnoreCase))
                return NetworkConditionDirection.Both;
            if (string.Equals(text, "inbound", StringComparison.OrdinalIgnoreCase))
                return NetworkConditionDirection.Inbound;
            if (string.Equals(text, "outbound", StringComparison.OrdinalIgnoreCase))
                return NetworkConditionDirection.Outbound;
            throw new InvalidOperationException($"Invalid network phase direction '{text}'.");
        }

        private static long ParseLong(VirtualNetworkCommand command, string name)
        {
            var text = command.RequireParameter(name);
            if (!long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var value))
                throw Invalid(command, name, text);
            return value;
        }

        private static int ParseOptionalInt(VirtualNetworkCommand command, string name, int fallback)
        {
            var text = GetOptional(command.Parameters, name);
            if (text == null) return fallback;
            if (!int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var value) || value < 0)
                throw Invalid(command, name, text);
            return value;
        }

        private static double ParseOptionalRate(VirtualNetworkCommand command, string name, double fallback)
        {
            var text = GetOptional(command.Parameters, name);
            if (text == null) return fallback;
            if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ||
                value < 0d || value > 1d)
                throw Invalid(command, name, text);
            return value;
        }

        private static string? GetOptional(
            IReadOnlyDictionary<string, string> parameters,
            string name)
        {
            foreach (var pair in parameters)
            {
                if (string.Equals(pair.Key, name, StringComparison.OrdinalIgnoreCase))
                    return pair.Value;
            }

            return null;
        }

        private static InvalidOperationException Invalid(
            VirtualNetworkCommand command,
            string name,
            string value)
        {
            return new InvalidOperationException(
                $"Invalid {name} '{value}' in {command.Name} atMs={command.AtMs}.");
        }
    }
}
