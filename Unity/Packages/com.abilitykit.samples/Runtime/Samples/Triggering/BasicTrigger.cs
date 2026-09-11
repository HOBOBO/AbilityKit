#nullable enable

using System.Collections.Generic;
using AbilityKit.Core.Eventing;
using AbilityKit.Samples.Abstractions;
using AbilityKit.Triggering.Eventing;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Variables.Numeric;
using AbilityKit.Triggering.Variables.Numeric.Expression;

namespace AbilityKit.Samples.Logic.Samples.Triggering
{
    /// <summary>
    /// 演示 TriggerRunner 注册触发器、发布事件、Evaluate 条件和 Execute 行为的最小闭环。
    /// </summary>
    [Sample(401, "triggering", "event", "runner", "package-api", "web", "deterministic")]
    public sealed class BasicTrigger : SampleBase
    {
        public override string Title => "Basic Trigger";
        public override string Description => "使用 EventBus、TriggerRunner、ITrigger 和 ExecCtx 实现最小事件触发闭环";
        public override SampleCategory Category => SampleCategory.Triggering;

        protected override void OnRun()
        {
            var hitEvent = new EventKey<HitEvent>("sample.hit");
            var eventBus = new EventBus();
            var context = new TriggerSampleContext(targetHp: 120);
            var runner = new TriggerRunner<TriggerSampleContext>(
                eventBus,
                new FunctionRegistry(),
                new ActionRegistry(),
                new InlineContextSource(context),
                numericDomains: new NumericVarDomainRegistry(),
                numericFunctions: new NumericRpnFunctionRegistry());

            using var registration = runner.Register(hitEvent, new DamageOnHitTrigger(minDamage: 20), phase: 0, priority: 10);
            KeyValue("Trigger.MinDamage", "20");
            KeyValue("Trigger.Phase", "0");
            KeyValue("Trigger.Priority", "10");

            Section("发布命中事件");
            PublishHit(eventBus, hitEvent, damage: 12, source: "weak-hit");
            PublishHit(eventBus, hitEvent, damage: 35, source: "heavy-hit");
            PublishHit(eventBus, hitEvent, damage: 24, source: "follow-up-hit");

            Divider();
            Section("触发结果");
            KeyValue("Evaluated", context.Evaluated.ToString());
            KeyValue("Executed", context.Executed.ToString());
            KeyValue("TargetHp", context.TargetHp.ToString());
            KeyValue("DamageLog", string.Join(" | ", context.DamageLog));

            Divider();
            Section("这个示例实际接入的包能力");
            Bullet("EventBus：负责发布 sample.hit 事件并驱动订阅者。");
            Bullet("TriggerRunner：负责按 phase / priority 管理触发器并构造 ExecCtx。");
            Bullet("ITrigger.Evaluate：把条件判断留在触发器内部，失败时跳过 Execute。");
            Bullet("ITrigger.Execute：只在条件通过后修改样例上下文。");
        }

        private void PublishHit(EventBus eventBus, EventKey<HitEvent> key, int damage, string source)
        {
            var hit = new HitEvent(damage, source);
            Log($"Publish {source}: damage={damage}");
            eventBus.Publish(key, in hit);
        }

        private sealed class DamageOnHitTrigger : ITrigger<HitEvent, TriggerSampleContext>
        {
            private readonly int _minDamage;

            public DamageOnHitTrigger(int minDamage)
            {
                _minDamage = minDamage;
            }

            public bool Evaluate(in HitEvent args, in ExecCtx<TriggerSampleContext> ctx)
            {
                ctx.Context.Evaluated++;
                var passed = args.Damage >= _minDamage;
                ctx.Context.DamageLog.Add($"eval({args.Source})={passed}");
                return passed;
            }

            public void Execute(in HitEvent args, in ExecCtx<TriggerSampleContext> ctx)
            {
                ctx.Context.Executed++;
                ctx.Context.TargetHp -= args.Damage;
                ctx.Context.DamageLog.Add($"apply({args.Source})=-{args.Damage}");
            }
        }

        private readonly struct HitEvent
        {
            public HitEvent(int damage, string source)
            {
                Damage = damage;
                Source = source;
            }

            public int Damage { get; }
            public string Source { get; }
        }

        private sealed class TriggerSampleContext
        {
            public TriggerSampleContext(int targetHp)
            {
                TargetHp = targetHp;
            }

            public int TargetHp { get; set; }
            public int Evaluated { get; set; }
            public int Executed { get; set; }
            public List<string> DamageLog { get; } = new List<string>();
        }

        private sealed class InlineContextSource : ITriggerContextSource<TriggerSampleContext>
        {
            private readonly TriggerSampleContext _context;

            public InlineContextSource(TriggerSampleContext context)
            {
                _context = context;
            }

            public TriggerSampleContext GetContext()
            {
                return _context;
            }
        }
    }
}
