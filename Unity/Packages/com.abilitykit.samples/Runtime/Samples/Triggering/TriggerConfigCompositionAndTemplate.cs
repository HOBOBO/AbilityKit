#nullable enable

using System.Collections.Generic;
using AbilityKit.Core.Eventing;
using AbilityKit.Samples.Abstractions;
using AbilityKit.Triggering.Eventing;
using AbilityKit.Triggering.Payload;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Plan;
using AbilityKit.Triggering.Runtime.Plan.Json;
using AbilityKit.Triggering.Variables.Numeric;
using AbilityKit.Triggering.Variables.Numeric.Expression;

namespace AbilityKit.Samples.Logic.Samples.Triggering
{
    /// <summary>
    /// 展示触发器配置的组合能力: 内嵌行为树、ID 引用形态、条件节点和模板多值绑定。
    /// </summary>
    [Sample(400, "triggering", "config", "template", "package-api", "web")]
    public sealed class TriggerConfigCompositionAndTemplate : SampleBase
    {
        private static readonly int ApplyEffectActionValue = StableStringId.Get("action:sample_apply_effect");
        private static readonly int MarkActionValue = StableStringId.Get("action:sample_mark_step");
        private static readonly int BonusActionValue = StableStringId.Get("action:sample_bonus_tick");

        private const int TemplateLightTriggerId = 4101;
        private const int TemplateHeavyTriggerId = 4102;
        private const int NestedTriggerId = 4201;

        public override string Title => "Trigger Config Composition And Template";
        public override string Description => "演示触发器内嵌/引用配置、嵌套条件/行为和模板多值绑定";
        public override SampleCategory Category => SampleCategory.Triggering;

        protected override void OnRun()
        {
            Section("配置形态");
            Bullet("源配置支持 behaviorRefs 引用复用节点，也支持 children/then/else 内嵌节点。");
            Bullet("运行时配置使用 Template.Bindings 传入多组数值，同一类触发器可以产生不同效果。");
            Output.Line();
            Log(SourceShapeExample);

            var actions = CreateActions();
            var context = new SampleTriggerContext();
            var execCtx = CreateExecCtx(context, actions);
            var database = new TriggerPlanJsonDatabase();
            database.LoadFromJson(BuildRuntimePlanJson(), "trigger-config-composition-template.sample.json");

            Section("模板多值绑定");
            ExecutePlan(database, TemplateLightTriggerId, "light-burn", context, execCtx);
            ExecutePlan(database, TemplateHeavyTriggerId, "heavy-burn", context, execCtx);
            KeyValue("效果日志", string.Join(" | ", context.Effects));
            KeyValue("TriggerConfig.TemplateEffects", string.Join(" | ", context.Effects));
            KeyValue("累计数值", context.TotalAmount.ToString("0.##"));
            KeyValue("TriggerConfig.TemplateTotal", context.TotalAmount.ToString("0.##"));

            Section("嵌套行为和条件");
            context.Reset();
            ExecuteRoot(database, NestedTriggerId, "nested-tree", context, execCtx);
            KeyValue("执行路径", string.Join(" -> ", context.Effects));
            KeyValue("TriggerConfig.NestedPath", string.Join(" -> ", context.Effects));
            KeyValue("累计数值", context.TotalAmount.ToString("0.##"));
            KeyValue("TriggerConfig.NestedTotal", context.TotalAmount.ToString("0.##"));
        }

        private static ActionRegistry CreateActions()
        {
            var actions = new ActionRegistry();
            actions.Register<NamedAction2<object, object, SampleTriggerContext>>(
                new ActionId(ApplyEffectActionValue),
                (payload, rawArgs, ctx) =>
                {
                    var amount = ReadArg(rawArgs, "_0");
                    var duration = ReadArg(rawArgs, "_1");
                    ctx.Context.TotalAmount += amount;
                    ctx.Context.Effects.Add($"apply(amount={amount:0.##},duration={duration:0.##})");
                },
                isDeterministic: true);

            actions.Register<NamedAction1<object, object, SampleTriggerContext>>(
                new ActionId(MarkActionValue),
                (payload, rawArgs, ctx) =>
                {
                    var step = ReadArg(rawArgs, "_0");
                    ctx.Context.Effects.Add($"mark(step={step:0})");
                },
                isDeterministic: true);

            actions.Register<NamedAction1<object, object, SampleTriggerContext>>(
                new ActionId(BonusActionValue),
                (payload, rawArgs, ctx) =>
                {
                    var amount = ReadArg(rawArgs, "_0");
                    ctx.Context.TotalAmount += amount;
                    ctx.Context.Effects.Add($"bonus({amount:0.##})");
                },
                isDeterministic: true);

            return actions;
        }

        private static ExecCtx<SampleTriggerContext> CreateExecCtx(SampleTriggerContext context, ActionRegistry actions)
        {
            return new ExecCtx<SampleTriggerContext>(
                context,
                new EventBus(),
                new FunctionRegistry(),
                actions,
                blackboards: null,
                payloads: new PayloadAccessorRegistry(),
                idNames: null,
                numericDomains: new NumericVarDomainRegistry(),
                numericFunctions: new NumericRpnFunctionRegistry(),
                policy: default,
                control: null);
        }

        private void ExecutePlan(
            TriggerPlanJsonDatabase database,
            int triggerId,
            string label,
            SampleTriggerContext context,
            ExecCtx<SampleTriggerContext> execCtx)
        {
            if (!database.TryGetPlanByTriggerId(triggerId, out var plan))
            {
                Log($"未找到触发器: {triggerId}");
                return;
            }

            var before = context.Effects.Count;
            var trigger = new PlannedTrigger<object, SampleTriggerContext>(plan);
            trigger.Execute(new SamplePayload(label), execCtx);
            var executed = context.Effects.Count - before;
            KeyValue(label, $"actions={executed}, total={context.TotalAmount:0.##}");
            KeyValue($"TriggerConfig.Template[{label}]", $"actions={executed}, total={context.TotalAmount:0.##}");
        }

        private void ExecuteRoot(
            TriggerPlanJsonDatabase database,
            int triggerId,
            string label,
            SampleTriggerContext context,
            ExecCtx<SampleTriggerContext> execCtx)
        {
            if (!database.TryGetExecutionRootByTriggerId(triggerId, out var root))
            {
                Log($"未找到执行树: {triggerId}");
                return;
            }

            var result = root.Execute(new SamplePayload(label), execCtx);
            KeyValue(label, $"success={result.IsSuccess}, executed={result.ExecutedCount}, reason={result.Reason ?? string.Empty}");
            KeyValue($"TriggerConfig.Root[{label}]", $"success={result.IsSuccess}, executed={result.ExecutedCount}, reason={result.Reason ?? string.Empty}");
        }

        private static double ReadArg(object rawArgs, string key)
        {
            if (rawArgs is NamedArgsDict dict && dict.TryGetValue(key, out var value))
            {
                return value.Ref.Kind == ENumericValueRefKind.Const ? value.Ref.ConstValue : 0d;
            }

            return 0d;
        }

        private static string BuildRuntimePlanJson()
        {
            return @"
{
  ""FormatVersion"": 1,
  ""Triggers"": [
    {
      ""TriggerId"": 4101,
      ""EventName"": ""sample.hit"",
      ""Scope"": 0,
      ""Template"": {
        ""TemplateId"": ""burn-effect"",
        ""Bindings"": {
          ""amount"": { ""Kind"": ""Const"", ""ConstValue"": 12 },
          ""duration"": { ""Kind"": ""Const"", ""ConstValue"": 2 }
        }
      },
      ""Actions"": [{
        ""ActionId"": " + ApplyEffectActionValue + @",
        ""Arity"": 2,
        ""Arg0"": { ""Kind"": ""TemplateParam"", ""Key"": ""amount"" },
        ""Arg1"": { ""Kind"": ""TemplateParam"", ""Key"": ""duration"" }
      }]
    },
    {
      ""TriggerId"": 4102,
      ""EventName"": ""sample.hit"",
      ""Scope"": 0,
      ""Template"": {
        ""TemplateId"": ""burn-effect"",
        ""Bindings"": {
          ""amount"": { ""Kind"": ""Const"", ""ConstValue"": 36 },
          ""duration"": { ""Kind"": ""Const"", ""ConstValue"": 5 }
        }
      },
      ""Actions"": [{
        ""ActionId"": " + ApplyEffectActionValue + @",
        ""Arity"": 2,
        ""Arg0"": { ""Kind"": ""TemplateParam"", ""Key"": ""amount"" },
        ""Arg1"": { ""Kind"": ""TemplateParam"", ""Key"": ""duration"" }
      }]
    },
    {
      ""TriggerId"": 4201,
      ""EventName"": ""sample.hit"",
      ""Scope"": 0,
      ""Template"": {
        ""TemplateId"": ""nested-effect"",
        ""Bindings"": {
          ""amount"": { ""Kind"": ""Const"", ""ConstValue"": 18 },
          ""duration"": { ""Kind"": ""Const"", ""ConstValue"": 3 },
          ""bonus"": { ""Kind"": ""Const"", ""ConstValue"": 4 },
          ""threshold"": { ""Kind"": ""Const"", ""ConstValue"": 20 },
          ""power"": { ""Kind"": ""Const"", ""ConstValue"": 25 }
        }
      },
      ""ExecutionRoot"": {
        ""Kind"": ""Sequence"",
        ""SourceKind"": ""inline"",
        ""SourceId"": ""trigger:4201"",
        ""Children"": [
          {
            ""Kind"": ""Action"",
            ""SourceKind"": ""behavior"",
            ""SourceId"": ""mark-start"",
            ""Action"": {
              ""ActionId"": " + MarkActionValue + @",
              ""Arity"": 1,
              ""Arg0"": { ""Kind"": ""Const"", ""ConstValue"": 1 }
            }
          },
          {
            ""Kind"": ""If"",
            ""SourceKind"": ""inline"",
            ""SourceId"": ""if:enough-power"",
            ""Condition"": {
              ""Kind"": ""Expr"",
              ""Nodes"": [
                {
                  ""Kind"": ""CompareNumeric"",
                  ""CompareOp"": ""GreaterThanOrEqual"",
                  ""Left"": { ""Kind"": ""TemplateParam"", ""Key"": ""power"" },
                  ""Right"": { ""Kind"": ""TemplateParam"", ""Key"": ""threshold"" }
                }
              ]
            },
            ""Children"": [{
              ""Kind"": ""Action"",
              ""Action"": {
                ""ActionId"": " + ApplyEffectActionValue + @",
                ""Arity"": 2,
                ""Arg0"": { ""Kind"": ""TemplateParam"", ""Key"": ""amount"" },
                ""Arg1"": { ""Kind"": ""TemplateParam"", ""Key"": ""duration"" }
              }
            }],
            ""ElseChildren"": [{
              ""Kind"": ""Action"",
              ""Action"": {
                ""ActionId"": " + MarkActionValue + @",
                ""Arity"": 1,
                ""Arg0"": { ""Kind"": ""Const"", ""ConstValue"": 0 }
              }
            }]
          },
          {
            ""Kind"": ""Repeat"",
            ""SourceKind"": ""behavior"",
            ""SourceId"": ""bonus-tick"",
            ""Count"": 2,
            ""Children"": [{
              ""Kind"": ""Action"",
              ""Action"": {
                ""ActionId"": " + BonusActionValue + @",
                ""Arity"": 1,
                ""Arg0"": { ""Kind"": ""TemplateParam"", ""Key"": ""bonus"" }
              }
            }]
          }
        ]
      }
    }
  ],
  ""Strings"": {}
}";
        }

        private sealed class SampleTriggerContext
        {
            public readonly List<string> Effects = new List<string>();
            public double TotalAmount;

            public void Reset()
            {
                Effects.Clear();
                TotalAmount = 0d;
            }
        }

        private sealed class SamplePayload
        {
            public SamplePayload(string label)
            {
                Label = label;
            }

            public string Label { get; }
        }

        private static readonly string SourceShapeExample = @"source json shape:
{
  ""behaviors"": {
    ""mark-start"": { ""root"": { ""kind"": ""action"", ""type"": ""sample_mark_step"", ""step"": 1 } },
    ""bonus-tick"": { ""root"": { ""kind"": ""action"", ""type"": ""sample_bonus_tick"", ""amount"": ""@bonus"" } }
  },
  ""condition_groups"": {
    ""enough-power"": [{ ""type"": ""arg_gte"", ""left"": ""@power"", ""right"": ""@threshold"" }]
  },
  ""triggers"": [{
    ""id"": 4201,
    ""behavior"": {
      ""kind"": ""sequence"",
      ""behaviorRefs"": [""mark-start""],
      ""children"": [{
        ""kind"": ""if"",
        ""conditionRefs"": [""enough-power""],
        ""then"": [{ ""type"": ""sample_apply_effect"", ""amount"": ""@amount"", ""duration"": ""@duration"" }],
        ""else"": [{ ""type"": ""sample_mark_step"", ""step"": 0 }]
      }, {
        ""kind"": ""repeat"",
        ""times"": 2,
        ""behaviorRefs"": [""bonus-tick""]
      }]
    }
  }]
}";
    }
}
