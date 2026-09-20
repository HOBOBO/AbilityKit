using System.Globalization;
using System.Text;
using AbilityKit.Game.Test.UnitTest;
using AbilityKit.Network.Protocol;
using AbilityKit.Network.Runtime.Conditioning;
using AbilityKit.Protocol.Moba;
using AbilityKit.Scenario;

namespace AbilityKit.Demo.Moba.Acceptance.LiveSim;

public sealed class LiveSimVirtualNetworkRunResult
{
    public string[] Trace { get; init; } = Array.Empty<string>();
    public NetworkConditioningStats Stats { get; init; }
    public long FinishedAtMs { get; init; }
    public double SimulationFinishedAtMs { get; init; }
    public int TimelinePacketsQueued { get; init; }
    public int TimelinePacketsDelivered { get; init; }
}

/// <summary>
/// Runs network commands and gameplay steps on one monotonic virtual clock. At each timestamp,
/// due packets arrive first, network controls execute second, and gameplay inputs are produced last.
/// </summary>
public sealed class LiveSimVirtualNetworkTimelineRunner
{
    private readonly LiveSimSetupActionExecutor _setup;
    private readonly LiveSimTimelineRunner _timeline;
    private readonly List<string> _trace = new();
    private double _simulationAtMs;
    private int _maxDurationMs;
    private int _timelineQueued;
    private int _timelineDelivered;

    public LiveSimVirtualNetworkTimelineRunner(
        LiveSimSetupActionExecutor setup,
        LiveSimTimelineRunner timeline)
    {
        _setup = setup ?? throw new ArgumentNullException(nameof(setup));
        _timeline = timeline ?? throw new ArgumentNullException(nameof(timeline));
    }

    public LiveSimVirtualNetworkRunResult Run(
        MobaAcceptanceTimelineStepExpectation[] timeline,
        IReadOnlyList<TestCommand> commands,
        int seed,
        int timeoutMs)
    {
        _trace.Clear();
        _simulationAtMs = 0;
        _maxDurationMs = timeoutMs;
        _timelineQueued = 0;
        _timelineDelivered = 0;

        var virtualCommands = commands.Select(command => new VirtualNetworkCommand(
            command.AtMs, command.Name, command.Parameters));
        var plan = VirtualNetworkScenarioPlan.Compile(virtualCommands, NetworkConditionProfile.Ideal);
        using var link = new VirtualNetworkConditionLink(plan.Scenario, seed);
        var player = new VirtualNetworkScenarioPlayer(link, HandleExplicitPacket);
        var orderedCommands = plan.Commands.Select((command, index) => (command, index))
            .OrderBy(item => item.command.AtMs).ThenBy(item => item.index).ToArray();
        var orderedSteps = (timeline ?? Array.Empty<MobaAcceptanceTimelineStepExpectation>())
            .Where(step => step != null)
            .Select((step, index) => (step, index))
            .OrderBy(item => Math.Max(0, item.step.atMs)).ThenBy(item => item.index).ToArray();

        var commandIndex = 0;
        var stepIndex = 0;
        while (commandIndex < orderedCommands.Length || stepIndex < orderedSteps.Length)
        {
            var commandAt = commandIndex < orderedCommands.Length
                ? orderedCommands[commandIndex].command.AtMs
                : long.MaxValue;
            var stepAt = stepIndex < orderedSteps.Length
                ? Math.Max(0, orderedSteps[stepIndex].step.atMs)
                : long.MaxValue;
            var atMs = Math.Min(commandAt, stepAt);
            EnsureWithinTimeout(atMs, timeoutMs);

            link.AdvanceTo(atMs);
            AdvanceSimulationTo(atMs);

            while (commandIndex < orderedCommands.Length &&
                   orderedCommands[commandIndex].command.AtMs == atMs)
            {
                var command = orderedCommands[commandIndex++].command;
                _trace.Add($"command:{command.Name}@{atMs}");
                player.Execute(command);
            }

            while (stepIndex < orderedSteps.Length &&
                   Math.Max(0, orderedSteps[stepIndex].step.atMs) == atMs)
            {
                var item = orderedSteps[stepIndex++];
                if (LiveSimTimelineRunner.IsSkillAction(item.step.action))
                    QueueTimelineInput(link, item.step, item.index);
                else
                {
                    _trace.Add($"timeline:local:{item.index}:{item.step.action}@{atMs}");
                    _simulationAtMs += _timeline.ExecuteStep(item.step);
                    EnsureSimulationWithinTimeout(timeoutMs);
                }
            }
        }

        while (link.Middleware.NextDeliveryAtMs is { } nextDelivery)
        {
            EnsureWithinTimeout(nextDelivery, timeoutMs);
            link.AdvanceTo(nextDelivery);
            AdvanceSimulationTo(nextDelivery);
        }

        AppendLinkTrace(link);
        return new LiveSimVirtualNetworkRunResult
        {
            Trace = _trace.ToArray(),
            Stats = link.Middleware.GetStats(),
            FinishedAtMs = link.NowMs,
            SimulationFinishedAtMs = _simulationAtMs,
            TimelinePacketsQueued = _timelineQueued,
            TimelinePacketsDelivered = _timelineDelivered,
        };
    }

    private void QueueTimelineInput(
        VirtualNetworkConditionLink link,
        MobaAcceptanceTimelineStepExpectation step,
        int sourceIndex)
    {
        var sequence = checked((uint)sourceIndex + 1u);
        var payload = Encoding.UTF8.GetBytes(string.Join("|",
            step.action ?? string.Empty,
            step.actorAlias ?? string.Empty,
            step.targetAlias ?? string.Empty,
            step.slot.ToString(CultureInfo.InvariantCulture),
            step.payload ?? string.Empty));
        var header = new NetworkPacketHeader(
            NetworkPacketFlags.None,
            MobaOpCodes.Input.SkillInput,
            sequence,
            (uint)payload.Length);
        _timelineQueued++;
        _trace.Add($"timeline:queued:{sourceIndex}:{sequence}@{link.NowMs}");
        link.InjectOutbound(header, new ArraySegment<byte>(payload), (_, _) =>
        {
            AdvanceSimulationTo(link.NowMs);
            _simulationAtMs += _timeline.ExecuteStep(step);
            EnsureSimulationWithinTimeout(_maxDurationMs);
            _timelineDelivered++;
            _trace.Add($"timeline:delivered:{sourceIndex}:{sequence}@{link.NowMs}");
        });
    }

    private void HandleExplicitPacket(
        VirtualNetworkConditionLink link,
        VirtualNetworkCommand command)
    {
        var opCode = ParseUInt(command, "opCode");
        var sequence = ParseUInt(command, "seq");
        var bytes = ParseOptionalInt(command, "bytes");
        var payload = bytes == 0 ? Array.Empty<byte>() : new byte[bytes];
        var header = new NetworkPacketHeader(
            NetworkPacketFlags.None, opCode, sequence, (uint)payload.Length);
        Action<NetworkPacketHeader, ArraySegment<byte>> delivered = (packet, _) =>
            _trace.Add($"packet:delivered:{packet.OpCode}:{packet.Seq}@{link.NowMs}");
        var direction = GetOptional(command.Parameters, "direction");
        if (string.Equals(direction, "outbound", StringComparison.OrdinalIgnoreCase))
            link.InjectOutbound(header, new ArraySegment<byte>(payload), delivered);
        else
            link.InjectInbound(header, new ArraySegment<byte>(payload), delivered);
    }

    private void AdvanceSimulationTo(long atMs)
    {
        if (atMs <= _simulationAtMs) return;
        _simulationAtMs += _setup.TickMilliseconds(atMs - _simulationAtMs);
    }

    private void AppendLinkTrace(VirtualNetworkConditionLink link)
    {
        foreach (var entry in link.Events)
            _trace.Add($"link:{entry.Kind}:{entry.OpCode}:{entry.Sequence}@{entry.AtMs}");
        foreach (var decision in link.Middleware.SnapshotDecisions())
            _trace.Add($"decision:{(decision.Inbound ? "in" : "out")}:{decision.OpCode}:{decision.Sequence}:" +
                       $"{decision.DropReason}:{decision.DeliverAtMs}:{decision.Reordered}@{decision.ObservedAtMs}");
    }

    private static uint ParseUInt(VirtualNetworkCommand command, string name)
    {
        var text = command.RequireParameter(name);
        if (!uint.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var value))
            throw new InvalidOperationException($"Invalid {name} in {command.Name} atMs={command.AtMs}.");
        return value;
    }

    private static int ParseOptionalInt(VirtualNetworkCommand command, string name)
    {
        var text = GetOptional(command.Parameters, name);
        if (text == null) return 0;
        if (!int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var value) || value < 0)
            throw new InvalidOperationException($"Invalid {name} in {command.Name} atMs={command.AtMs}.");
        return value;
    }

    private static string? GetOptional(IReadOnlyDictionary<string, string> values, string name)
    {
        foreach (var pair in values)
            if (string.Equals(pair.Key, name, StringComparison.OrdinalIgnoreCase)) return pair.Value;
        return null;
    }

    private static void EnsureWithinTimeout(long atMs, int timeoutMs)
    {
        if (atMs > timeoutMs)
            throw new TimeoutException($"Virtual scenario exceeded timeoutMs={timeoutMs} at t={atMs}ms.");
    }

    private void EnsureSimulationWithinTimeout(int timeoutMs)
    {
        if (_simulationAtMs > timeoutMs + 1e-6d)
            throw new TimeoutException(
                $"BattleFlow simulation exceeded maxDurationMs={timeoutMs} at t={_simulationAtMs:F3}ms.");
    }
}
