using AbilityKit.Deterministic;
using AbilityKit.HFSM;

namespace AbilityKit.HFSM.Core.Tests;

internal sealed class TestOwner
{
    public bool Finish { get; set; }
    public bool AllowExit { get; set; }
    public List<string> Trace { get; } = new();
}

internal sealed class TraceState : HfsmStateBase<TestOwner>
{
    private readonly string _id;

    public TraceState(string id)
    {
        _id = id;
    }

    public override void OnEnter(TestOwner owner, in HfsmTickContext context)
        => owner.Trace.Add("enter:" + _id);

    public override void OnTick(TestOwner owner, in HfsmTickContext context)
        => owner.Trace.Add("tick:" + _id);

    public override void OnExitRequested(TestOwner owner, in HfsmTickContext context)
        => owner.Trace.Add("exit-requested:" + _id);

    public override bool CanExit(TestOwner owner, in HfsmTickContext context) => owner.AllowExit;

    public override void OnExit(TestOwner owner, in HfsmTickContext context)
        => owner.Trace.Add("exit:" + _id);
}

internal sealed class OwnerFlagCondition : IHfsmTransitionCondition<TestOwner>
{
    private readonly Func<TestOwner, bool> _predicate;

    public OwnerFlagCondition(Func<TestOwner, bool> predicate)
    {
        _predicate = predicate;
    }

    public bool Evaluate(TestOwner owner, in HfsmTransitionContext context) => _predicate(owner);
}

internal sealed class CounterState : HfsmStateBase<TestOwner>, IHfsmStateSnapshotParticipant
{
    public int Count { get; private set; }
    public int EnterCount { get; private set; }
    public int ExitCount { get; private set; }

    public int SnapshotVersion => 1;

    public override void OnEnter(TestOwner owner, in HfsmTickContext context) => EnterCount++;

    public override void OnTick(TestOwner owner, in HfsmTickContext context) => Count++;

    public override void OnExit(TestOwner owner, in HfsmTickContext context) => ExitCount++;

    public string CaptureSnapshot() => Count.ToString(System.Globalization.CultureInfo.InvariantCulture);

    public void ValidateSnapshot(int version, string payload)
    {
        if (version != SnapshotVersion || !int.TryParse(payload, out _))
            throw new InvalidOperationException("Invalid counter snapshot.");
    }

    public void RestoreSnapshot(int version, string payload)
    {
        Count = int.Parse(payload, System.Globalization.CultureInfo.InvariantCulture);
    }
}

internal sealed class ThrowingObserver : IHfsmRuntimeObserver
{
    public void OnRuntimeEvent(in HfsmRuntimeEvent runtimeEvent) =>
        throw new InvalidOperationException("Observer failure must be isolated.");
}

internal sealed class ThrowingState : HfsmStateBase<TestOwner>
{
    public override void OnTick(TestOwner owner, in HfsmTickContext context) =>
        throw new InvalidOperationException("State failure.");
}

internal static class HfsmFixtures
{
    public static Fixed64 Time(int value) => Fixed64.FromInt32(value);

    public static HfsmDefinition Flat(params HfsmStateDefinition[] states)
    {
        return new HfsmDefinition
        {
            DefinitionId = "test",
            RootMachineId = "root",
            Machines =
            {
                new HfsmMachineDefinition
                {
                    Id = "root",
                    InitialStateId = states[0].Id,
                    States = states.ToList(),
                },
            },
        };
    }

    public static HfsmStateDefinition State(
        string id,
        string behavior = "",
        bool requiresExitApproval = false,
        string childMachine = "")
    {
        return new HfsmStateDefinition
        {
            Id = id,
            BehaviorKey = behavior,
            RequiresExitApproval = requiresExitApproval,
            ChildMachineId = childMachine,
        };
    }

    public static HfsmTransitionDefinition Transition(
        string id,
        string from,
        string to,
        string trigger = "",
        string condition = "",
        int priority = 0,
        bool fromAny = false,
        bool force = false,
        long minimumDurationRaw = 0)
    {
        return new HfsmTransitionDefinition
        {
            Id = id,
            FromStateId = from,
            ToStateId = to,
            TriggerId = trigger,
            ConditionKey = condition,
            Priority = priority,
            FromAnyState = fromAny,
            ForceImmediate = force,
            MinimumActiveDurationRaw = minimumDurationRaw,
        };
    }
}
