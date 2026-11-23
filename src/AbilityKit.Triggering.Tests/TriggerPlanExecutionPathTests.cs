using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Context;
using AbilityKit.Triggering.Runtime.Plan;
using Xunit;

namespace AbilityKit.Triggering.Tests;

public sealed class TriggerPlanExecutionPathTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Predicate_expression_returns_the_same_result_on_both_paths(bool expected)
    {
        var expression = new PredicateExprPlan(new[] { BoolExprNode.Const(expected) });
        var trigger = new PlannedTrigger<object, ObservingContext>(new TriggerPlan<object>(0, 0, 1, predicateExpr: expression));
        var ctx = CreateContext(new ActionRegistry());

        Assert.Equal(expected, trigger.Evaluate(new object(), in ctx));
        Assert.Equal(expected, new PredicateExprTriggerPlanCondition(expression).Evaluate(new object(), in ctx));
        Assert.True(new PredicateExprTriggerPlanCondition(default).Evaluate(new object(), in ctx));
    }

    [Fact]
    public void Predicate_expression_enforces_determinism_on_both_paths()
    {
        var functionId = new FunctionId(17);
        var functions = new FunctionRegistry();
        functions.Register<Predicate0<object, ObservingContext>>(functionId, (_, _) => true, isDeterministic: false);
        var expression = new PredicateExprPlan(new[] { BoolExprNode.Function(functionId, 0) });
        var trigger = new PlannedTrigger<object, ObservingContext>(new TriggerPlan<object>(0, 0, 1, predicateExpr: expression));
        var condition = new PredicateExprTriggerPlanCondition(expression);
        var ctx = CreateContext(new ActionRegistry(), functions, requireDeterministic: true);

        var flatError = Assert.Throws<InvalidOperationException>(() => trigger.Evaluate(new object(), in ctx));
        var treeError = Assert.Throws<InvalidOperationException>(() => condition.Evaluate(new object(), in ctx));
        Assert.Contains("Non-deterministic", flatError.Message);
        Assert.Equal(flatError.Message, treeError.Message);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Action_arguments_are_shared_but_binding_lifetimes_remain_distinct(bool named)
    {
        var actionId = new ActionId(21);
        var args = new object();
        var argumentName = named ? "amount" : "0";
        var call = named
            ? ActionCallPlan.WithArgs(actionId, new Dictionary<string, ActionArgValue> { [argumentName] = ActionArgValue.OfConst(12, argumentName) })
            : new ActionCallPlan(actionId, 12d);
        var flat = CreateTrigger(call);
        var leaf = new ActionCallTriggerPlanExecutable(call, originalActionIndex: 7);
        var actions = new ActionRegistry();
        var ctx = CreateContext(actions);
        var calls = new List<string>();

        void Register(string version) => actions.Register<NamedAction1<object, object, ObservingContext>>(
            actionId, (payload, actionArgs, current) =>
            {
                Assert.Same(args, payload);
                Assert.Same(ctx.Context, current.Context);
                var values = Assert.IsType<NamedArgsDict>(actionArgs);
                Assert.True(values.TryGetValue(argumentName, out var value));
                Assert.Equal(12d, value.Ref.ConstValue);
                calls.Add(version);
            }, isDeterministic: true);

        Register("original");
        flat.Execute(in args, in ctx);
        Assert.Equal(1, leaf.Execute(args, in ctx).ExecutedCount);
        Register("replacement");
        flat.Execute(in args, in ctx);
        Assert.Equal(1, leaf.Execute(args, in ctx).ExecutedCount);

        Assert.Equal(new[] { "original", "original", "original", "replacement" }, calls);
        Assert.Equal(new[] { (0, actionId.Value, true), (7, actionId.Value, true), (0, actionId.Value, true), (7, actionId.Value, true) }, ctx.Context.Exited);
    }

    [Fact]
    public void Action_failure_exits_each_scope_once_and_propagates_the_original_exception()
    {
        var actionId = new ActionId(22);
        var call = new ActionCallPlan(actionId);
        var flat = CreateTrigger(call);
        var leaf = new ActionCallTriggerPlanExecutable(call, originalActionIndex: 9);
        var error = new InvalidOperationException("fixture action failure");
        var actions = new ActionRegistry();
        actions.Register<NamedAction0<object, object, ObservingContext>>(actionId, (_, _, _) => throw error, isDeterministic: true);
        var ctx = CreateContext(actions);

        Assert.Same(error, Assert.Throws<InvalidOperationException>(() => flat.Execute(new object(), in ctx)));
        Assert.Same(error, Assert.Throws<InvalidOperationException>(() => { leaf.Execute(new object(), in ctx); }));
        Assert.Equal(new[] { (0, actionId.Value), (9, actionId.Value) }, ctx.Context.Entered);
        Assert.Equal(new[] { (0, actionId.Value, false), (9, actionId.Value, false) }, ctx.Context.Exited);
    }

    private static PlannedTrigger<object, ObservingContext> CreateTrigger(ActionCallPlan call) =>
        new(new TriggerPlan<object>(0, 0, 2, actions: new[] { call }));

    private static ExecCtx<ObservingContext> CreateContext(ActionRegistry actions, FunctionRegistry? functions = null, bool requireDeterministic = false) =>
        new(new ObservingContext(), eventBus: new AbilityKit.Triggering.Eventing.EventBus(), functions: functions ?? new FunctionRegistry(), actions: actions,
            blackboards: null!, payloads: null!, idNames: null!, numericDomains: null!, numericFunctions: null!,
            policy: new ExecPolicy(requireDeterministic), control: new ExecutionControl());

    private sealed class ObservingContext : System.IServiceProvider, ITriggerActionExecutionScopeObserver
    {
        public List<(int Index, int Id)> Entered { get; } = new();
        public List<(int Index, int Id, bool Succeeded)> Exited { get; } = new();

        public object? GetService(Type serviceType) => serviceType == typeof(ITriggerActionExecutionScopeObserver) ? this : null;

        public void EnterActionExecution(int actionIndex, long actionId) => Entered.Add((actionIndex, (int)actionId));

        public void ExitActionExecution(int actionIndex, long actionId, bool succeeded) => Exited.Add((actionIndex, (int)actionId, succeeded));
    }
}
