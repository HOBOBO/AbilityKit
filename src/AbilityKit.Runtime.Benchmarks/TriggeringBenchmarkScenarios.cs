using AbilityKit.Benchmarking;
using AbilityKit.Core.Eventing;
using AbilityKit.Triggering.Eventing;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime;

namespace AbilityKit.Runtime.Benchmarks;

public sealed class TriggerDispatchScenario : BenchmarkScenarioBase
{
    private readonly int _fanout;
    private readonly bool _queued;
    private readonly bool _reuseControl;
    private readonly int _batchSize;
    private readonly BenchmarkDescriptor _descriptor;
    private readonly EventKey<BenchmarkTriggerEvent> _eventKey = new(0x5A17);
    private readonly List<IDisposable> _registrations = new();
    private readonly ExecutionControl _reusedControl = new();
    private EventBus? _eventBus;
    private long _executionCount;
    private long _checksum;
    private long _expectedExecutions;

    public TriggerDispatchScenario(int fanout, bool queued, bool reuseControl, int batchSize = 1)
    {
        if (fanout <= 0)
            throw new ArgumentOutOfRangeException(nameof(fanout));
        if (batchSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(batchSize));
        if (queued && reuseControl)
            throw new ArgumentException("Queued scenarios do not reuse one mutable execution control.");

        _fanout = fanout;
        _queued = queued;
        _reuseControl = reuseControl;
        _batchSize = batchSize;
        var mode = queued ? "queued" : "immediate";
        var control = reuseControl ? "control-reused" : "control-implicit";
        _descriptor = new BenchmarkDescriptor(
            $"triggering.dispatch.{mode}.{control}.fanout-{fanout}",
            "triggering",
            "trigger-evaluation",
            new Dictionary<string, string>
            {
                [BenchmarkWorkloadDimensions.Scope] = BenchmarkScenarioScopes.Capability,
                ["dispatchMode"] = mode,
                ["fanout"] = fanout.ToString(),
                ["executionControl"] = reuseControl ? "reused" : "implicit",
                ["batchSize"] = batchSize.ToString(),
                ["predicateResult"] = "true",
                ["trace"] = "disabled"
            });
    }

    public override BenchmarkDescriptor Descriptor => _descriptor;

    public override void Setup()
    {
        var options = new EventBusOptions(
            _queued ? EEventDispatchMode.Queued : EEventDispatchMode.Immediate,
            maxFlushPasses: 8);
        _eventBus = new EventBus(options);
        var runner = new TriggerRunner<TriggerBenchmarkContext>(
            _eventBus,
            new FunctionRegistry(),
            new ActionRegistry());

        for (var i = 0; i < _fanout; i++)
        {
            var trigger = new DelegateTrigger<BenchmarkTriggerEvent, TriggerBenchmarkContext>(
                static (_, _) => true,
                (evt, _) =>
                {
                    _executionCount++;
                    _checksum = unchecked((_checksum * 397) ^ evt.Value);
                });
            _registrations.Add(runner.Register(_eventKey, trigger, phase: i & 3, priority: i));
        }
    }

    public override void IterationSetup()
    {
        _executionCount = 0;
        _expectedExecutions = 0;
        _checksum = 17;
    }

    public override long Execute(int invocationCount)
    {
        var eventCount = checked(invocationCount * _batchSize);
        for (var i = 0; i < eventCount; i++)
        {
            var evt = new BenchmarkTriggerEvent(i);
            if (_reuseControl)
                _eventBus!.Publish(_eventKey, in evt, _reusedControl);
            else
                _eventBus!.Publish(_eventKey, in evt);
        }

        if (_queued)
            _eventBus!.Flush();

        _expectedExecutions = checked((long)eventCount * _fanout);
        return _expectedExecutions;
    }

    public override void Validate()
    {
        if (_expectedExecutions != 0 && _executionCount != _expectedExecutions)
        {
            throw new InvalidOperationException(
                $"Expected {_expectedExecutions} trigger executions but observed {_executionCount}.");
        }
    }

    public override string GetDeterminismDigest() =>
        $"executions={_executionCount};checksum={_checksum}";

    public override void Cleanup()
    {
        for (var i = _registrations.Count - 1; i >= 0; i--)
            _registrations[i].Dispose();
        _registrations.Clear();
    }
}

public readonly record struct BenchmarkTriggerEvent(int Value);

public sealed class TriggerBenchmarkContext
{
}
