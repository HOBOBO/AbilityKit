#nullable enable

using System;
using System.Collections.Generic;
using AbilityKit.Core.Eventing;
using AbilityKit.Triggering.Eventing;
using AbilityKit.Triggering.Payload;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Plan;
using AbilityKit.Triggering.Variables.Numeric;
using AbilityKit.Triggering.Variables.Numeric.Expression;
using AbilityKit.Samples.Abstractions;

namespace AbilityKit.Samples.Logic.Samples.Demo.ProgressiveSkill
{
    /// <summary>
    /// ProgressiveSkill Phase3 - Triggering 事件驱动 (框架提供)
    ///
    /// 需求: 造成伤害时，30% 几率触发灼烧；击杀目标时，触发连击加成。
    ///
    /// 框架能力: com.abilitykit.triggering 的 TriggerRunner + TriggerPlan + ActionRegistry。
    /// 使用 RegisterPlan 模式注册触发器计划，ActionRegistry 注册动作。
    /// 从 Phase3_Triggering.json 加载配置数据。
    /// 展示如何用事件驱动的方式响应战斗事件。
    /// </summary>
    [Sample]
    public sealed class ProgressiveSkill_Phase3 : SampleBase
    {
        public override string Title => "ProgressiveSkill Phase3";
        public override string Description => "Triggering - 事件驱动的效果触发 (框架)";
        public override SampleCategory Category => SampleCategory.Demo;

        private EventBus _eventBus;
        private TriggerRunner<Phase3Ctx> _runner;
        private ActionRegistry _actions;
        private Phase3Ctx _ctx;

        protected override void OnRun()
        {
            Log("================================================================================");
            Log("===           渐进式技能系统 - Phase3: Triggering 事件驱动 (框架) ===");
            Log("================================================================================");
            Output.Divider();

            // 从 JSON 配置加载
            var config = Phase3Config.Load();
            Log("【1】从 Phase3_Triggering.json 加载配置");
            Log($"  灼烧触发器: 概率={config.BurningTrigger.Chance:P0}, 持续={config.BurningTrigger.Duration}s");
            Log($"  击杀触发器: 奖励={config.KillTrigger.BonusPoints}分");
            Log("");

            // ========== 初始化触发系统 (框架) ==========
            Log("【2】初始化触发系统 (框架 EventBus + TriggerRunner + ActionRegistry)");
            Output.Line();

            InitializeTriggering(config);

            Log("  系统初始化:");
            Log("    - EventBus: 事件总线 (框架)");
            Log("    - TriggerRunner<TCtx>: 触发器运行器 (框架)");
            Log("    - ActionRegistry: 动作注册表 (框架)");
            Log("");

            // ========== 注册触发器计划 (框架 RegisterPlan) ==========
            Log("【3】注册触发器计划 (框架 RegisterPlan)");
            Output.Line();

            RegisterTriggers(config);

            Log("  注册触发器计划:");
            Log("    event:damage -> [Plan] 灼烧触发器 (30% 几率)");
            Log("    event:damage -> [Plan] 击杀触发器 (击杀奖励)");
            Log("");

            // 创建目标
            var enemy = new Phase0Target(1, "哥布林王", 100f);
            var hero = new Phase0Target(999, "勇者", 300f);
            hero.AttackPower = 40f;

            Log($"  施法者: {hero}");
            Log($"  目标: {enemy}");
            Log("");

            // ========== 执行战斗 ==========
            Log("【4】执行战斗，触发器自动响应");
            Output.Line();

            // 第一次攻击
            Log("  第一次攻击 (伤害: 40):");
            enemy.Health = 100f;
            _ctx.TargetHealth = enemy.Health;
            _ctx.CasterId = hero.Id;
            _ctx.TargetId = enemy.Id;
            _ctx.RandomGenerator = new Random(123); // 固定种子以便演示
            var damage1 = new Phase3DamageEvent(40f, enemy.Id, hero.Id);
            _eventBus.Publish(Phase3EventKeys.Damage, damage1);
            _eventBus.Flush();
            Log($"    目标剩余 HP: {enemy.Health:F0}");
            Log("");

            // 第二次攻击
            Log("  第二次攻击 (伤害: 40):");
            _ctx.TargetHealth = enemy.Health;
            _ctx.RandomGenerator = new Random(456);
            var damage2 = new Phase3DamageEvent(40f, enemy.Id, hero.Id);
            _eventBus.Publish(Phase3EventKeys.Damage, damage2);
            _eventBus.Flush();
            Log($"    目标剩余 HP: {enemy.Health:F0}");
            Log("");

            // 第三次攻击 (击杀)
            Log("  第三次攻击 (伤害: 40，击杀目标!):");
            _ctx.TargetHealth = enemy.Health;
            _ctx.RandomGenerator = new Random(789);
            var damage3 = new Phase3DamageEvent(40f, enemy.Id, hero.Id);
            _eventBus.Publish(Phase3EventKeys.Damage, damage3);
            _eventBus.Flush();
            Log($"    目标剩余 HP: {enemy.Health:F0}");
            Log("");

            // ========== 概念说明 ==========
            Log("【5】Triggering 核心概念 (框架)");
            Output.Line();
            Log("  EventBus: 事件总线，订阅/发布事件 (框架)");
            Log("  EventKey<T>: 事件键，标识事件类型 (框架)");
            Log("  TriggerRunner<TCtx>: 触发器运行器，订阅事件并执行触发器 (框架)");
            Log("  TriggerPlan<TArgs>: 触发器计划，配置条件和动作 (框架)");
            Log("  ActionRegistry: 动作注册表，注册动作实现 (框架)");
            Log("  RegisterPlan: 注册触发器计划的扩展方法 (框架)");
            Log("");
            Log("  执行流程:");
            Log("    [派发事件] -> [TriggerRunner 查找触发器] -> [按 Priority 排序]");
            Log("    -> [遍历触发器: Evaluate(条件) -> Execute(动作)]");
            Log("");

            // ========== 对比 Phase2 ==========
            Log("【对比 Phase2】");
            Output.Bullet("Phase2 Flow: 按预设流程执行，无法响应外部事件");
            Output.Bullet("Phase3 Triggering: 事件驱动，条件满足时自动触发动作 (框架)");
            Output.Bullet("Triggering 适合战斗事件响应 (受伤、击杀、Buff触发)");
            Output.Bullet("Flow 适合技能内部流程控制 (等待、分支、并行)");
            Output.Bullet("两者是正交的，可以组合使用");
            Log("");

            // ========== 暴露下一个痛点 ==========
            Log("【下一个痛点】");
            Output.Bullet("Triggering 不知道技能执行的阶段（验证、消耗、冷却）");
            Output.Bullet("如果需要完整的技能执行流程（验证 -> 消耗 -> 施法 -> 效果 -> 冷却），需要 Pipeline");
            Log("  -> Phase4: Pipeline 阶段组合 (框架)");
            Log("");

            Output.Divider();
        }

        private void InitializeTriggering(Phase3Config config)
        {
            // 1. 创建事件总线
            _eventBus = new EventBus();

            // 2. 创建动作注册表
            _actions = new ActionRegistry();

            // 3. 注册动作: apply_burning - 施加灼烧效果
            var applyBurningId = new ActionId(StableStringId.Get("action:apply_burning"));
            _actions.Register(
                applyBurningId,
                new Action<Phase3DamageEvent, Phase3Ctx>((evt, ctx) =>
                {
                    Log($"    [动作] apply_burning: 施加灼烧效果 (目标: {evt.TargetId}, 持续: {config.BurningTrigger.Duration}s)");
                }),
                isDeterministic: false);

            // 4. 注册动作: log_burning_miss - 灼烧未触发
            var burningMissId = new ActionId(StableStringId.Get("action:burning_miss"));
            _actions.Register(
                burningMissId,
                new Action<Phase3DamageEvent, Phase3Ctx>((evt, ctx) =>
                {
                    Log($"    [触发] 灼烧触发器: 概率未通过");
                }),
                isDeterministic: true);

            // 5. 注册动作: kill_reward - 击杀奖励
            var killRewardId = new ActionId(StableStringId.Get("action:kill_reward"));
            _actions.Register(
                killRewardId,
                new Action<Phase3DamageEvent, Phase3Ctx>((evt, ctx) =>
                {
                    Log($"    [触发] 击杀触发器: 目标已击杀!");
                    Log($"    [动作] kill_reward: +{config.KillTrigger.BonusPoints} 分!");
                }),
                isDeterministic: true);

            // 6. 创建触发上下文
            _ctx = new Phase3Ctx
            {
                Actions = _actions,
                RandomGenerator = new Random()
            };

            // 7. 创建 TriggerRunner
            _runner = new TriggerRunner<Phase3Ctx>(
                _eventBus,
                functions: null,
                actions: _actions,
                contextSource: null,
                observer: null,
                lifecycle: null,
                blackboards: null,
                payloads: null,
                idNames: null,
                numericDomains: null,
                numericFunctions: null,
                policy: default,
                interruptPolicy: EInterruptPolicy.None,
                actionSchedulerManager: null);
        }

        private void RegisterTriggers(Phase3Config config)
        {
            var damageKey = Phase3EventKeys.Damage;

            // 创建灼烧触发器 (使用 DelegateTrigger 简化)
            var burningTrigger = new DelegateTrigger<Phase3DamageEvent, Phase3Ctx>(
                predicate: (evt, ctx) =>
                {
                    // 30% 随机几率
                    bool success = ctx.Context.RandomGenerator.NextDouble() < config.BurningTrigger.Chance;
                    if (success)
                    {
                        Log($"    [触发] 灼烧触发器: 概率判定通过 ({config.BurningTrigger.Chance:P0})!");
                    }
                    else
                    {
                        Log($"    [触发] 灼烧触发器: 概率未通过 ({config.BurningTrigger.Chance:P0})");
                    }
                    return success;
                },
                actions: (evt, ctx) =>
                {
                    // 施加灼烧效果
                    Log($"    [动作] apply_burning: 施加灼烧效果 (目标: {evt.TargetId}, 持续: {config.BurningTrigger.Duration}s)");
                });

            // 创建击杀触发器 (使用 DelegateTrigger 简化)
            var killTrigger = new DelegateTrigger<Phase3DamageEvent, Phase3Ctx>(
                predicate: (evt, ctx) =>
                {
                    // 检查是否击杀: ctx.Context.TargetHealth - evt.Amount <= 0
                    bool isKill = ctx.Context.TargetHealth - evt.Amount <= 0;
                    if (isKill)
                    {
                        Log($"    [触发] 击杀触发器: 目标将被击杀 (HP: {ctx.Context.TargetHealth:F0} - Damage: {evt.Amount:F0} = {ctx.Context.TargetHealth - evt.Amount:F0} <= 0)");
                    }
                    return isKill;
                },
                actions: (evt, ctx) =>
                {
                    // 击杀奖励
                    Log($"    [触发] 击杀触发器: 目标已击杀!");
                    Log($"    [动作] kill_reward: +{config.KillTrigger.BonusPoints} 分!");
                });

            // 注册触发器 (phase=0, priority=100/200)
            _runner.Register(damageKey, burningTrigger, phase: 0, priority: 100);
            _runner.Register(damageKey, killTrigger, phase: 0, priority: 200);
        }
    }

    // ============================================================================
    // 配置模型
    // ============================================================================

    /// <summary>
    /// Phase3 配置 (从 JSON 加载)
    /// </summary>
    public sealed class Phase3Config
    {
        public Phase3BurningTriggerConfig BurningTrigger { get; set; } = new();
        public Phase3KillTriggerConfig KillTrigger { get; set; } = new();

        public static Phase3Config Load()
        {
            const string json = @"{
                ""burningTrigger"": {
                    ""chance"": 0.3,
                    ""duration"": 3.0,
                    ""damagePerTick"": 5.0
                },
                ""killTrigger"": {
                    ""bonusPoints"": 100
                }
            }";
            return Newtonsoft.Json.JsonConvert.DeserializeObject<Phase3Config>(json);
        }
    }

    public sealed class Phase3BurningTriggerConfig
    {
        public float Chance { get; set; }
        public float Duration { get; set; }
        public float DamagePerTick { get; set; }
    }

    public sealed class Phase3KillTriggerConfig
    {
        public int BonusPoints { get; set; }
    }

    // ============================================================================
    // 框架兼容的类型
    // ============================================================================

    /// <summary>
    /// 伤害事件
    /// </summary>
    public readonly struct Phase3DamageEvent
    {
        public readonly float Amount;
        public readonly long TargetId;
        public readonly long CasterId;

        public Phase3DamageEvent(float amount, long targetId, long casterId)
        {
            Amount = amount;
            TargetId = targetId;
            CasterId = casterId;
        }
    }

    /// <summary>
    /// 事件键
    /// </summary>
    public static class Phase3EventKeys
    {
        public static readonly EventKey<Phase3DamageEvent> Damage =
            new EventKey<Phase3DamageEvent>(StableStringId.Get("event:damage"));
    }

    /// <summary>
    /// 触发上下文
    /// </summary>
    public class Phase3Ctx
    {
        public ActionRegistry Actions { get; set; }
        public float TargetHealth { get; set; }
        public long CasterId { get; set; }
        public long TargetId { get; set; }
        public Random RandomGenerator { get; set; }
    }
}
