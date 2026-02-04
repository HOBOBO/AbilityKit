using System;
using AbilityKit.Core.Eventing;
using AbilityKit.Triggering.Blackboard;
using AbilityKit.Triggering.Eventing;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Variables.Numeric;
using AbilityKit.Triggering.Variables.Numeric.Domains;
using AbilityKit.Triggering.Variables.Numeric.Expression;
using AbilityKit.Triggering.Variables.Numeric.Json;
using Newtonsoft.Json;

namespace AbilityKit.Triggering.Runtime.Example
{
    public static class NumericExpression_BlackboardDomainExample
    {
        public static void RunOnce()
        {
            var bus = new EventBus();
            var functions = new FunctionRegistry();
            var actions = new ActionRegistry();

            var blackboards = new DictionaryBlackboardResolver();
            var bb = new DictionaryBlackboard();
            var boardId = StableStringId.Get("bb:numeric");
            blackboards.Register(boardId, bb);

            var domainResolver = new DictionaryBlackboardDomainResolver();
            domainResolver.Register("actor", boardId);

            var numericDomains = new NumericVarDomainRegistry();
            numericDomains.Register(new BlackboardNumericVarDomain("actor", domainResolver));

            var runner = new TriggerRunner<TriggerContext>(bus, functions, actions, blackboards: blackboards, numericDomains: numericDomains, numericFunctions: null);

            var control = new ExecutionControl();
            var execCtx = new ExecCtx<TriggerContext>(default, bus, functions, actions, blackboards, payloads: null, idNames: null, numericDomains: numericDomains, numericFunctions: null, policy: ExecPolicy.Default, control: control);

            bb.SetDouble(BlackboardIdMapper.KeyId("actor.hp"), 100d);
            bb.SetDouble(BlackboardIdMapper.KeyId("actor.atk"), 12d);

            var srcConst = NumericValueSourceRuntime.Const(3d);
            var srcVar = NumericValueSourceRuntime.Var("actor", "hp");
            var srcExpr = NumericValueSourceRuntime.ExprText("actor.hp + actor.atk * 2");

            if (!srcConst.TryEvaluate(in execCtx, out var v0))
            {
                Console.WriteLine("const evaluate failed");
                return;
            }

            if (!srcVar.TryEvaluate(in execCtx, out var v1))
            {
                Console.WriteLine("var evaluate failed");
                return;
            }

            if (!srcExpr.TryEvaluate(in execCtx, out var v2))
            {
                Console.WriteLine("expr evaluate failed");
                return;
            }

            Console.WriteLine("value const=" + v0);
            Console.WriteLine("value var=" + v1);
            Console.WriteLine("value expr=" + v2);

            var dto = NumericValueSourceRuntimeDtoBuilder.ToDto(in srcExpr);
            var json = JsonConvert.SerializeObject(dto);
            var dto2 = JsonConvert.DeserializeObject<NumericValueSourceRuntimeDto>(json);
            if (!NumericValueSourceRuntimeDtoBuilder.TryBuild(dto2, out var srcExpr2))
            {
                Console.WriteLine("dto build failed");
                return;
            }
            if (!srcExpr2.TryEvaluate(in execCtx, out var v3))
            {
                Console.WriteLine("dto expr evaluate failed");
                return;
            }
            Console.WriteLine("value dto expr=" + v3);

            if (!NumericExpressionCompiler.TryCompile("actor.hp + actor.atk * 2", out var program))
            {
                Console.WriteLine("Compile failed");
                return;
            }

            if (!NumericExpressionEvaluator.TryEvaluate(in execCtx, program, out var value))
            {
                Console.WriteLine("Evaluate failed");
                return;
            }

            Console.WriteLine("expr value=" + value);

            _ = runner;
        }
    }
}
