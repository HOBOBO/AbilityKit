using System;
using AbilityKit.Core.Eventing;
using AbilityKit.Triggering.Eventing;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Plan;

namespace AbilityKit.Triggering.Runtime.Example
{
    /// <summary>
    /// TCtx 泛型灵活性示例
    /// 展示如何使用 DefaultTCtx、CompositeContextSource、ContextAccessor 等增强功能
    /// </summary>
    public static class TCtxFlexibilityExample
    {
        public readonly struct DamageEvent
        {
            public readonly int Amount;
            public readonly int TargetId;

            public DamageEvent(int amount, int targetId)
            {
                Amount = amount;
                TargetId = targetId;
            }
        }

        /// <summary>
        /// 自定义战斗上下文示例
        /// </summary>
        public readonly struct BattleCtx
        {
            public readonly int ActorId;
            public readonly IBattleService BattleService;

            public BattleCtx(int actorId, IBattleService battleService)
            {
                ActorId = actorId;
                BattleService = battleService;
            }
        }

        /// <summary>
        /// 战斗服务接口
        /// </summary>
        public interface IBattleService
        {
            bool IsAlive(int actorId);
            int GetHealth(int actorId);
        }

        /// <summary>
        /// 示例战斗服务
        /// </summary>
        public sealed class DemoBattleService : IBattleService
        {
            private readonly int _maxHealth = 100;

            public bool IsAlive(int actorId) => actorId > 0;
            public int GetHealth(int actorId) => _maxHealth - actorId;
        }

        /// <summary>
        /// 战斗上下文源
        /// </summary>
        public sealed class BattleContextSource : ITriggerContextSource<BattleCtx>
        {
            private BattleCtx _ctx;

            public BattleContextSource(IBattleService service)
            {
                _ctx = new BattleCtx(actorId: 1, battleService: service);
            }

            public void UpdateActor(int actorId)
            {
                _ctx = new BattleCtx(actorId, _ctx.BattleService);
            }

            public BattleCtx GetContext() => _ctx;
        }

        public static void RunOnce()
        {
            Console.WriteLine("=== TCtx 泛型灵活性示例 ===\n");

            // ========== 1. 使用 DefaultTCtx（不需要上下文时） ==========
            Console.WriteLine("--- 1. DefaultTCtx 示例 ---");
            RunDefaultTCtxExample();

            // ========== 2. 使用 CompositeContextSource（组合多个上下文源） ==========
            Console.WriteLine("\n--- 2. CompositeContextSource 示例 ---");
            RunCompositeContextSourceExample();

            // ========== 3. 使用 ContextAccessor（安全访问上下文属性） ==========
            Console.WriteLine("\n--- 3. ContextAccessor 示例 ---");
            RunContextAccessorExample();
        }

        private static void RunDefaultTCtxExample()
        {
            var bus = new EventBus();
            var functions = new FunctionRegistry();
            var actions = new ActionRegistry();

            // 使用 DefaultContextSource - 不需要自定义上下文
            var runner = new TriggerRunner<DefaultTCtx>(
                bus,
                functions,
                actions,
                contextSource: DefaultContextSource.Instance);

            // 注册一个无条件触发的 action
            var actionId = new ActionId(StableStringId.Get("action:simple_log"));
            actions.Register<PlannedTrigger<DamageEvent, DefaultTCtx>.Action0>(
                actionId,
                (evt, ctx) =>
                {
                    Console.WriteLine($"触发: Damage={evt.Amount} (无上下文)");
                },
                isDeterministic: true);

            var key = new EventKey<DamageEvent>(StableStringId.Get("event:simple_damage"));
            var plan = new TriggerPlan<DamageEvent>(0, 0, 0, 0, new ActionCallPlan[] { new ActionCallPlan(actionId) });
            runner.RegisterPlan<DamageEvent, DefaultTCtx>(key, plan);

            bus.Publish(key, new DamageEvent(50, 1));
            bus.Flush();

            // 使用 ExecCtxExtensions
            var execCtx = new ExecCtx<DefaultTCtx>(default, bus, functions, actions, null, null, null, null, null, null, ExecPolicy.Default, null);
            Console.WriteLine($"ExecCtx Extensions - IsDefault: {execCtx.IsDefault()}");
        }

        private static void RunCompositeContextSourceExample()
        {
            var bus = new EventBus();
            var functions = new FunctionRegistry();
            var actions = new ActionRegistry();

            var service = new DemoBattleService();
            var primarySource = new BattleContextSource(service);
            var compositeSource = CompositeContextSource.Create<BattleCtx>(primarySource);

            var runner = new TriggerRunner<BattleCtx>(
                bus,
                functions,
                actions,
                contextSource: compositeSource);

            // 注册条件：目标必须存活
            var predicateId = new FunctionId(StableStringId.Get("pred:target_alive"));
            functions.Register<PlannedTrigger<DamageEvent, BattleCtx>.Predicate0>(
                predicateId,
                (evt, ctx) =>
                {
                    return ctx.Context.BattleService != null && ctx.Context.BattleService.IsAlive(evt.TargetId);
                },
                isDeterministic: true);

            var actionId = new ActionId(StableStringId.Get("action:damage_dealt"));
            actions.Register<PlannedTrigger<DamageEvent, BattleCtx>.Action0>(
                actionId,
                (evt, ctx) =>
                {
                    var hp = ctx.Context.BattleService.GetHealth(evt.TargetId);
                    Console.WriteLine($"伤害造成: Target={evt.TargetId}, HP={hp}, 施法者={ctx.Context.ActorId}");
                },
                isDeterministic: true);

            var key = new EventKey<DamageEvent>(StableStringId.Get("event:battle_damage"));
            var plan = new TriggerPlan<DamageEvent>(0, 0, 0, predicateId, 0, new ActionCallPlan[] { new ActionCallPlan(actionId) });
            runner.RegisterPlan<DamageEvent, BattleCtx>(key, plan);

            // targetId=1 存活 -> 触发
            Console.WriteLine("targetId=1 存活:");
            bus.Publish(key, new DamageEvent(30, 1));
            bus.Flush();

            // targetId=-1 不存活 -> 不触发
            Console.WriteLine("targetId=-1 不存活:");
            bus.Publish(key, new DamageEvent(30, -1));
            bus.Flush();

            Console.WriteLine($"CompositeContextSource 源数量: {compositeSource.SourceCount}");
        }

        private static void RunContextAccessorExample()
        {
            var bus = new EventBus();
            var functions = new FunctionRegistry();
            var actions = new ActionRegistry();

            var service = new DemoBattleService();
            var contextSource = new BattleContextSource(service);

            var runner = new TriggerRunner<BattleCtx>(
                bus,
                functions,
                actions,
                contextSource: contextSource);

            // 使用 ContextAccessor 访问上下文
            var predicateId = new FunctionId(StableStringId.Get("pred:using_accessor"));
            functions.Register<PlannedTrigger<DamageEvent, BattleCtx>.Predicate0>(
                predicateId,
                (evt, ctx) =>
                {
                    // 使用 ContextAccessor 访问服务
                    var accessor = ctx.GetAccessor();
                    if (accessor.TryGetService<IBattleService>(out var battleService))
                    {
                        return battleService.IsAlive(evt.TargetId);
                    }

                    // 使用 ContextAccessor 属性访问
                    if (accessor.TryGetProperty("ActorId", out int actorId))
                    {
                        Console.WriteLine($"使用 ContextAccessor 获取 ActorId: {actorId}");
                    }

                    return true;
                },
                isDeterministic: true);

            var actionId = new ActionId(StableStringId.Get("action:accessor_demo"));
            actions.Register<PlannedTrigger<DamageEvent, BattleCtx>.Action0>(
                actionId,
                (evt, ctx) =>
                {
                    var accessor = ctx.GetAccessor();
                    var actorId = accessor.GetPropertyOrDefault("ActorId", 0);
                    Console.WriteLine($"ContextAccessor Demo: Damage={evt.Amount}, Actor={actorId}");
                },
                isDeterministic: true);

            var key = new EventKey<DamageEvent>(StableStringId.Get("event:accessor_damage"));
            var plan = new TriggerPlan<DamageEvent>(0, 0, 0, predicateId, 0, new ActionCallPlan[] { new ActionCallPlan(actionId) });
            runner.RegisterPlan<DamageEvent, BattleCtx>(key, plan);

            bus.Publish(key, new DamageEvent(100, 5));
            bus.Flush();
        }
    }
}
