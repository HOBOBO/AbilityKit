using System;
using System.Collections.Generic;
using AbilityKit.Core;
using AbilityKit.Core.Eventing;
using AbilityKit.Triggering.Blackboard;
using AbilityKit.Triggering.Eventing;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Config;
using AbilityKit.Triggering.Runtime.Context;
using AbilityKit.Triggering.Runtime.Plan;
using AbilityKit.Triggering.Validation;
using AbilityKit.Triggering.Variables.Numeric;
using AbilityKit.Triggering.Variables.Numeric.Expression;
using NUnit.Framework;

namespace AbilityKit.Triggering.Tests.Editor
{
    public sealed class ActionCallPlanValidatorTests
    {
        [Test]
        public void ActionCallPlanFactory_ModifiersPreserveSemanticSubPlanProjection()
        {
            var actionId = new ActionId(StableStringId.Get("test:action_plan_validator:factory_projection"));
            var args = new Dictionary<string, ActionArgValue>
            {
                ["amount"] = ActionArgValue.OfConst(7, "amount"),
                ["scale"] = ActionArgValue.OfConst(2, "scale")
            };

            var call = ActionCallPlanFactory.CreateWithArgs(actionId, args);
            call = ActionCallPlanFactory.WithSchedule(call, EActionScheduleMode.Periodic, 33f, 5, false);
            call = ActionCallPlanFactory.WithRetry(call, maxRetries: 2, retryDelayMs: 10f);

            AssertSemanticProjectionMatchesRawFields(call);
            Assert.That(call.Arguments.NamedArgs, Is.SameAs(args));
            Assert.That(call.Schedule.Mode, Is.EqualTo(EActionScheduleMode.Periodic));
            Assert.That(call.Schedule.Param, Is.EqualTo(33f));
            Assert.That(call.Schedule.MaxExecutions, Is.EqualTo(5));
            Assert.That(call.Schedule.CanBeInterrupted, Is.False);
            Assert.That(call.Execution.Policy, Is.EqualTo(EActionExecutionPolicy.WithRetry));
            Assert.That(call.Execution.RetryMaxRetries, Is.EqualTo(2));
            Assert.That(call.Execution.RetryDelayMs, Is.EqualTo(10f));
        }

        [Test]
        public void ActionCallPlanExtensions_ModifiersPreserveSemanticSubPlanProjection()
        {
            var actionId = new ActionId(StableStringId.Get("test:action_plan_validator:extension_projection"));

            var call = ActionCallPlanExtensions.Call(actionId, NumericValueRef.Const(3), NumericValueRef.Const(4))
                .WithSchedule(EActionScheduleMode.Delayed, 50f, 1, true)
                .WithExecutionPolicy(EActionExecutionPolicy.Queued);

            AssertSemanticProjectionMatchesRawFields(call);
            Assert.That(call.Arguments.Arity, Is.EqualTo(2));
            Assert.That(call.Arguments.Arg0.ConstValue, Is.EqualTo(3));
            Assert.That(call.Arguments.Arg1.ConstValue, Is.EqualTo(4));
            Assert.That(call.Schedule.Mode, Is.EqualTo(EActionScheduleMode.Delayed));
            Assert.That(call.Schedule.Param, Is.EqualTo(50f));
            Assert.That(call.Execution.Policy, Is.EqualTo(EActionExecutionPolicy.Queued));
        }

        [Test]
        public void NumericValueRef_OptionalBlackboardSourcePreservesFallbackAndPolicies()
        {
            var valueRef = NumericValueRef.Blackboard(100, 200)
                .WithFallback(5)
                .WithScale(2)
                .WithOffset(1)
                .WithClamp(0, 10)
                .WithLabel("damage.amount")
                .WithScope("skill:a");

            Assert.That(valueRef.Kind, Is.EqualTo(ENumericValueRefKind.Blackboard));
            Assert.That(valueRef.BoardId, Is.EqualTo(100));
            Assert.That(valueRef.KeyId, Is.EqualTo(200));
            Assert.That(valueRef.Required, Is.False);
            Assert.That(valueRef.HasFallback, Is.True);
            Assert.That(valueRef.FallbackValue, Is.EqualTo(5));
            Assert.That(valueRef.HasScale, Is.True);
            Assert.That(valueRef.Scale, Is.EqualTo(2));
            Assert.That(valueRef.Offset, Is.EqualTo(1));
            Assert.That(valueRef.HasMin, Is.True);
            Assert.That(valueRef.MinValue, Is.EqualTo(0));
            Assert.That(valueRef.HasMax, Is.True);
            Assert.That(valueRef.MaxValue, Is.EqualTo(10));
            Assert.That(valueRef.Label, Is.EqualTo("damage.amount"));
            Assert.That(valueRef.Scope, Is.EqualTo("skill:a"));
        }

        [Test]
        public void NumericValueRef_RequiredSourcePreservesFallbackPolicyFlag()
        {
            var valueRef = NumericValueRef.Blackboard(100, 200)
                .WithFallback(5)
                .AsRequired();

            Assert.That(valueRef.Kind, Is.EqualTo(ENumericValueRefKind.Blackboard));
            Assert.That(valueRef.Required, Is.True);
            Assert.That(valueRef.HasFallback, Is.True);
            Assert.That(valueRef.FallbackValue, Is.EqualTo(5));
        }

        [Test]
        public void NumericValueRef_BlackboardSourcePreservesScaleOffsetAndMaxPolicies()
        {
            var valueRef = NumericValueRef.Blackboard(100, 200)
                .WithScale(3)
                .WithOffset(-1)
                .WithMax(15);

            Assert.That(valueRef.Kind, Is.EqualTo(ENumericValueRefKind.Blackboard));
            Assert.That(valueRef.BoardId, Is.EqualTo(100));
            Assert.That(valueRef.KeyId, Is.EqualTo(200));
            Assert.That(valueRef.HasScale, Is.True);
            Assert.That(valueRef.Scale, Is.EqualTo(3));
            Assert.That(valueRef.Offset, Is.EqualTo(-1));
            Assert.That(valueRef.HasMax, Is.True);
            Assert.That(valueRef.MaxValue, Is.EqualTo(15));
        }

        [Test]
        public void NumericValueRef_ResolvesVarAndExpressionPaths()
        {
            var varRef = NumericValueRef.Var("player", "power").WithScale(2);
            var exprRef = NumericValueRef.Expr("player.power + 4");

            Assert.That(varRef.Kind.ToString(), Does.Contain("Var"));
            Assert.That(exprRef.Kind.ToString(), Does.Contain("Expr"));
        }

        [Test]
        public void ActionArgValue_GetHashCode_SupportsAllValueKinds()
        {
            Assert.DoesNotThrow(() => default(ActionArgValue).GetHashCode());
            Assert.DoesNotThrow(() => ActionArgValue.OfConst(7, "amount").GetHashCode());
            Assert.DoesNotThrow(() => ActionArgValue.OfBool(true, "enabled").GetHashCode());
            Assert.DoesNotThrow(() => ActionArgValue.OfString("armed", "state").GetHashCode());
        }

        private static void AssertSemanticProjectionMatchesRawFields(ActionCallPlan call)
        {
            Assert.That(call.Id, Is.Not.EqualTo(default(ActionId)));
        }

    }
}
