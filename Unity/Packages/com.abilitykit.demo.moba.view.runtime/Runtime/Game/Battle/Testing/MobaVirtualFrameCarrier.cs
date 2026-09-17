#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using AbilityKit.Ability.Host;
using AbilityKit.Network.Protocol;
using AbilityKit.Network.Runtime.Conditioning;

namespace AbilityKit.Game.Battle.Testing
{
    public sealed class MobaVirtualFrameSessionRunResult
    {
        public FramePacket[] DeliveredFrames { get; internal set; } = Array.Empty<FramePacket>();
        public VirtualNetworkLinkEvent[] LinkEvents { get; internal set; } = Array.Empty<VirtualNetworkLinkEvent>();
        public NetworkConditionDecision[] Decisions { get; internal set; } = Array.Empty<NetworkConditionDecision>();
        public NetworkConditioningStats Stats { get; internal set; }
        public string[] Trace { get; internal set; } = Array.Empty<string>();
        public string[] StateTrace { get; internal set; } = Array.Empty<string>();
        public long FinishedAtMs { get; internal set; }
        public long SimulationFinishedAtMs { get; internal set; }
        public string FinalStateFingerprint { get; internal set; } = string.Empty;
        public string DeterminismFingerprint { get; internal set; } = string.Empty;
    }

    /// <summary>
    /// Plays an authoritative-frame plan directly into a battle session on a virtual clock.
    /// It drains every scheduled delivery before returning and captures a stable replay fingerprint.
    /// </summary>
    public sealed class MobaVirtualFrameSessionRunner
    {
        public MobaVirtualFrameSessionRunResult Run(
            Action<FramePacket> injectRemoteFrame,
            VirtualNetworkScenarioPlan plan,
            Func<VirtualNetworkCommand, FramePacket> resolveFrame,
            int seed = 0,
            long timeoutMs = 30_000,
            Func<FramePacket, ArraySegment<byte>>? serializeFrame = null,
            Action<long>? advanceSimulationByMs = null,
            Func<string>? captureFinalState = null,
            Func<string>? captureSimulationState = null)
        {
            if (injectRemoteFrame == null) throw new ArgumentNullException(nameof(injectRemoteFrame));
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            if (resolveFrame == null) throw new ArgumentNullException(nameof(resolveFrame));
            if (timeoutMs < 0) throw new ArgumentOutOfRangeException(nameof(timeoutMs));

            foreach (var command in plan.Commands)
            {
                if (command.AtMs > timeoutMs)
                    throw new TimeoutException(
                        $"Virtual frame scenario exceeded timeoutMs={timeoutMs} at t={command.AtMs}ms.");
            }

            var delivered = new List<FramePacket>();
            var trace = new List<string>();
            var stateTrace = new List<string>();
            var canonical = new StringBuilder().Append("seed=").Append(seed).Append('\n');
            long simulationAtMs = 0;
            void CaptureSimulationState(string transition, long atMs)
            {
                if (captureSimulationState == null) return;
                var state = captureSimulationState() ?? string.Empty;
                var line = $"{transition}@{atMs}:{state}";
                stateTrace.Add(line);
                canonical.Append("state|").Append(transition)
                    .Append('|').Append(atMs)
                    .Append('|').Append(Convert.ToBase64String(Encoding.UTF8.GetBytes(state)))
                    .Append('\n');
            }

            void AdvanceSimulationTo(long atMs)
            {
                if (atMs <= simulationAtMs) return;
                var deltaMs = atMs - simulationAtMs;
                advanceSimulationByMs?.Invoke(deltaMs);
                simulationAtMs = atMs;
                CaptureSimulationState("simulation", atMs);
            }

            MobaVirtualFrameCarrier? carrier = null;
            carrier = new MobaVirtualFrameCarrier(
                plan,
                packet =>
                {
                    AdvanceSimulationTo(carrier!.Link.NowMs);
                    injectRemoteFrame(packet);
                    delivered.Add(packet);
                    var nowMs = carrier.Link.NowMs;
                    trace.Add($"frame:{packet.WorldId.Value}:{packet.Frame.Value}@{nowMs}");
                    AppendPacket(canonical, packet, nowMs);
                    CaptureSimulationState($"frame:{packet.Frame.Value}", nowMs);
                },
                seed);

            using (carrier)
            {
                var player = carrier.CreatePlayer(resolveFrame, serializeFrame);
                foreach (var item in OrderCommands(plan.Commands))
                {
                    carrier.Link.AdvanceTo(item.Command.AtMs);
                    AdvanceSimulationTo(item.Command.AtMs);
                    player.Execute(item.Command);
                }

                while (carrier.Link.Middleware.NextDeliveryAtMs is { } nextDelivery)
                {
                    if (nextDelivery > timeoutMs)
                        throw new TimeoutException(
                            $"Virtual frame scenario exceeded timeoutMs={timeoutMs} at t={nextDelivery}ms.");
                    carrier.Link.AdvanceTo(nextDelivery);
                    AdvanceSimulationTo(nextDelivery);
                }

                var events = CopyEvents(carrier.Link.Events);
                foreach (var entry in events)
                {
                    var line = $"link:{entry.Kind}:{entry.OpCode}:{entry.Sequence}@{entry.AtMs}";
                    trace.Add(line);
                    canonical.Append(line).Append('\n');
                }

                var decisions = carrier.Link.Middleware.SnapshotDecisions();
                foreach (var decision in decisions)
                {
                    var line = $"decision:{(decision.Inbound ? "in" : "out")}:" +
                               $"{decision.OpCode}:{decision.Sequence}:{decision.DropReason}:" +
                               $"{decision.DeliverAtMs}:{decision.Reordered}@{decision.ObservedAtMs}";
                    trace.Add(line);
                    canonical.Append(line).Append('\n');
                }

                var finalState = captureFinalState?.Invoke() ?? string.Empty;
                canonical.Append("final-state|")
                    .Append(Convert.ToBase64String(Encoding.UTF8.GetBytes(finalState)))
                    .Append('\n');

                return new MobaVirtualFrameSessionRunResult
                {
                    DeliveredFrames = delivered.ToArray(),
                    LinkEvents = events,
                    Decisions = decisions,
                    Stats = carrier.Link.Middleware.GetStats(),
                    Trace = trace.ToArray(),
                    StateTrace = stateTrace.ToArray(),
                    FinishedAtMs = carrier.Link.NowMs,
                    SimulationFinishedAtMs = simulationAtMs,
                    FinalStateFingerprint = finalState,
                    DeterminismFingerprint = ComputeSha256(canonical.ToString()),
                };
            }
        }

        private static List<IndexedCommand> OrderCommands(
            IReadOnlyList<VirtualNetworkCommand> commands)
        {
            var ordered = new List<IndexedCommand>(commands.Count);
            for (var i = 0; i < commands.Count; i++)
                ordered.Add(new IndexedCommand(i, commands[i]));
            ordered.Sort(IndexedCommand.Compare);
            return ordered;
        }

        private static VirtualNetworkLinkEvent[] CopyEvents(
            IReadOnlyList<VirtualNetworkLinkEvent> events)
        {
            var copy = new VirtualNetworkLinkEvent[events.Count];
            for (var i = 0; i < events.Count; i++) copy[i] = events[i];
            return copy;
        }

        private static void AppendPacket(StringBuilder target, FramePacket packet, long nowMs)
        {
            target.Append("frame|").Append(packet.WorldId.Value)
                .Append('|').Append(packet.Frame.Value)
                .Append('|').Append(nowMs)
                .Append('|').Append(packet.Inputs.Count);
            foreach (var input in packet.Inputs)
            {
                target.Append('|').Append(input.Frame.Value)
                    .Append('|').Append(input.Player.Value)
                    .Append('|').Append(input.OpCode)
                    .Append('|').Append(Convert.ToBase64String(input.Payload ?? Array.Empty<byte>()));
            }

            if (packet.Snapshot.HasValue)
            {
                var snapshot = packet.Snapshot.Value;
                target.Append("|snapshot|").Append(snapshot.OpCode)
                    .Append('|').Append(Convert.ToBase64String(snapshot.Payload ?? Array.Empty<byte>()));
            }
            else
            {
                target.Append("|snapshot:none");
            }

            target.Append('\n');
        }

        private static string ComputeSha256(string value)
        {
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(value));
            return BitConverter.ToString(bytes).Replace("-", string.Empty);
        }

        private readonly struct IndexedCommand
        {
            public IndexedCommand(int index, VirtualNetworkCommand command)
            {
                Index = index;
                Command = command;
            }

            public int Index { get; }
            public VirtualNetworkCommand Command { get; }

            public static int Compare(IndexedCommand left, IndexedCommand right)
            {
                var time = left.Command.AtMs.CompareTo(right.Command.AtMs);
                return time != 0 ? time : left.Index.CompareTo(right.Index);
            }
        }
    }

    /// <summary>
    /// Socket-free carrier that subjects authoritative battle frames to the deterministic virtual link.
    /// Bind <c>deliverFrame</c> to <c>BattleLogicSession.InjectRemoteFrame</c> so delivered and catch-up
    /// frames enter the normal prediction, reconciliation, and rollback pipeline.
    /// </summary>
    public sealed class MobaVirtualFrameCarrier : IDisposable
    {
        private static readonly ArraySegment<byte> EmptyPayload =
            new ArraySegment<byte>(Array.Empty<byte>());

        private readonly Action<FramePacket> _deliverFrame;
        private bool _disposed;

        public MobaVirtualFrameCarrier(
            NetworkConditionScenario scenario,
            Action<FramePacket> deliverFrame,
            int seed = 0,
            int decisionCapacity = 4096,
            int maxPendingPackets = 4096)
        {
            _deliverFrame = deliverFrame ?? throw new ArgumentNullException(nameof(deliverFrame));
            Link = new VirtualNetworkConditionLink(
                scenario ?? throw new ArgumentNullException(nameof(scenario)),
                seed,
                decisionCapacity,
                maxPendingPackets);
        }

        public MobaVirtualFrameCarrier(
            VirtualNetworkScenarioPlan plan,
            Action<FramePacket> deliverFrame,
            int seed = 0,
            int decisionCapacity = 4096,
            int maxPendingPackets = 4096)
            : this((plan ?? throw new ArgumentNullException(nameof(plan))).Scenario,
                deliverFrame, seed, decisionCapacity, maxPendingPackets)
        {
        }

        public VirtualNetworkConditionLink Link { get; }

        public VirtualNetworkScenarioPlayer CreatePlayer(
            Func<VirtualNetworkCommand, FramePacket> resolveFrame,
            Func<FramePacket, ArraySegment<byte>>? serializeFrame = null)
        {
            ThrowIfDisposed();
            if (resolveFrame == null) throw new ArgumentNullException(nameof(resolveFrame));
            return new VirtualNetworkScenarioPlayer(Link, (link, command) =>
                InjectCommand(link, command, resolveFrame, serializeFrame));
        }

        public void Play(
            IEnumerable<VirtualNetworkCommand> commands,
            Func<VirtualNetworkCommand, FramePacket> resolveFrame,
            long? finishAtMs = null,
            Func<FramePacket, ArraySegment<byte>>? serializeFrame = null)
        {
            CreatePlayer(resolveFrame, serializeFrame).Play(commands, finishAtMs);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Link.Dispose();
        }

        private void InjectCommand(
            VirtualNetworkConditionLink link,
            VirtualNetworkCommand command,
            Func<VirtualNetworkCommand, FramePacket> resolveFrame,
            Func<FramePacket, ArraySegment<byte>>? serializeFrame)
        {
            var direction = GetOptionalParameter(command.Parameters, "direction") ?? "inbound";
            if (!string.Equals(direction, "inbound", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    $"Moba authoritative frame command must be inbound atMs={command.AtMs}.");

            var opCode = ParseUInt(command, "opCode");
            var sequence = ParseUInt(command, "seq");
            var packet = resolveFrame(command) ?? throw new InvalidOperationException(
                $"Frame resolver returned null atMs={command.AtMs}.");
            var payload = serializeFrame?.Invoke(packet) ?? EmptyPayload;
            var header = new NetworkPacketHeader(
                NetworkPacketFlags.None,
                opCode,
                sequence,
                payloadLength: (uint)payload.Count);
            link.InjectInbound(header, payload, (_, _) => _deliverFrame(packet));
        }

        private static uint ParseUInt(VirtualNetworkCommand command, string name)
        {
            var text = command.RequireParameter(name);
            if (!uint.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var value))
                throw new InvalidOperationException(
                    $"Invalid {name} in {command.Name} atMs={command.AtMs}.");
            return value;
        }

        private static string? GetOptionalParameter(
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

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(MobaVirtualFrameCarrier));
        }
    }
}
