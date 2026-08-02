using System.Collections.Generic;
using AbilityKit.Triggering.Eventing;
using AbilityKit.Triggering.Payload;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Plan;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.Triggering;

public sealed class PlanActionRejectionTests
{
    [Fact]
    public void Rejected_action_fails_sequence_and_skips_remaining_actions()
    {
        var rejectActionId = new ActionId(910001);
        var trailingActionId = new ActionId(910002);
        var trailingExecutions = 0;
        var actions = new ActionRegistry();
        actions.Register<NamedAction0<object, object, object>>(
            rejectActionId,
            (_, _, ctx) => ctx.Control.RejectAction("resource service unavailable"),
            isDeterministic: true);
        actions.Register<NamedAction0<object, object, object>>(
            trailingActionId,
            (_, _, _) => trailingExecutions++,
            isDeterministic: true);

        var control = new ExecutionControl();
        control.Reset();
        var execCtx = new ExecCtx<object>(
            new object(),
            new EventBus(),
            new FunctionRegistry(),
            actions,
            blackboards: null,
            payloads: null,
            idNames: null,
            numericDomains: null,
            numericFunctions: null,
            policy: default,
            control: control);
        var root = TriggerPlanExecutableDsl.Sequence(
            TriggerPlanExecutableDsl.Action(rejectActionId),
            TriggerPlanExecutableDsl.Action(trailingActionId));

        var result = root.Execute(new object(), in execCtx);

        Assert.True(result.IsFailed);
        Assert.Equal("resource service unavailable", result.Reason);
        Assert.True(control.IsActionRejected);
        Assert.Equal("resource service unavailable", control.ActionRejectReason);
        Assert.Equal(0, trailingExecutions);
    }

    [Fact]
    public void Named_payload_argument_is_resolved_before_action_execution()
    {
        const int amountFieldId = 920011;
        var actionId = new ActionId(920012);
        NamedArgsDict observedArgs = null;
        var actions = new ActionRegistry();
        actions.Register<NamedAction1<object, object, object>>(
            actionId,
            (_, actionArgs, _) => observedArgs = Assert.IsType<NamedArgsDict>(actionArgs),
            isDeterministic: true);
        var payloads = new PayloadAccessorRegistry();
        payloads.RegisterDoubleAccessor<object>(new TestObjectPayloadAccessor(amountFieldId));
        var execCtx = new ExecCtx<object>(
            new object(),
            new EventBus(),
            new FunctionRegistry(),
            actions,
            blackboards: null,
            payloads,
            idNames: null,
            numericDomains: null,
            numericFunctions: null,
            policy: default,
            control: new ExecutionControl());
        var call = ActionCallPlan.WithArgs(
            actionId,
            new Dictionary<string, ActionArgValue>
            {
                ["amount"] = ActionArgValue.Of(
                    NumericValueRef.PayloadField(amountFieldId),
                    "amount")
            });
        var root = TriggerPlanExecutableDsl.Action(call);

        var result = root.Execute(new TestPayload(37.5d), in execCtx);

        Assert.True(result.IsSuccess);
        Assert.NotNull(observedArgs);
        Assert.True(observedArgs.TryGetValue("amount", out var amount));
        Assert.Equal(ENumericValueRefKind.Const, amount.Ref.Kind);
        Assert.Equal(37.5d, amount.Ref.ConstValue);
    }

    private sealed class TestPayload
    {
        public TestPayload(double amount)
        {
            Amount = amount;
        }

        public double Amount { get; }
    }

    private sealed class TestObjectPayloadAccessor : IPayloadDoubleAccessor<object>
    {
        private readonly int _amountFieldId;

        public TestObjectPayloadAccessor(int amountFieldId)
        {
            _amountFieldId = amountFieldId;
        }

        public bool TryGet(in object args, int fieldId, out double value)
        {
            if (fieldId == _amountFieldId && args is TestPayload payload)
            {
                value = payload.Amount;
                return true;
            }

            value = default;
            return false;
        }
    }
}
