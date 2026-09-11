#nullable enable

using System;
using System.Collections.Generic;
using AbilityKit.Ability.Flow;
using AbilityKit.Ability.Flow.Blocks;
using AbilityKit.Ability.Flow.Nodes;
using AbilityKit.Samples.Abstractions;

namespace AbilityKit.Samples.Logic.Samples.Demo.ProgressiveSkill
{
    /// <summary>
    /// ProgressiveSkill Phase2 - Flow 异步编排 (框架提供)
    ///
    /// 需求: 引导技能（等待 1.5 秒引导）+ 并行播放特效 + 连击分支。
    ///
    /// 框架能力: com.abilitykit.flow 的 FlowSession + IFlowNode。
    /// 从 Phase2_Flow.json 加载配置数据。
    /// 展示如何用 Flow 编排异步流程。
    /// </summary>
    [Sample]
    public sealed class ProgressiveSkill_Phase2 : SampleBase
    {
        public override string Title => "ProgressiveSkill Phase2";
        public override string Description => "Flow - 异步流程编排 (框架)";
        public override SampleCategory Category => SampleCategory.Demo;

        protected override void OnRun()
        {
            Log("================================================================================");
            Log("===           渐进式技能系统 - Phase2: Flow 异步编排 (框架)       ===");
            Log("================================================================================");
            Output.Divider();

            // 从 JSON 配置加载
            var config = Phase2Config.Load();
            Log("【1】从 Phase2_Flow.json 加载配置");
            Log($"  火球术: castTime={config.Fireball.CastTime}s, damage={config.Fireball.Damage}");
            Log($"  连击阈值: {config.Combo.HealthThreshold:P0}");
            Log("");

            // 创建目标
            var enemy = new Phase0Target(1, "哥布林王", 500f);
            var hero = new Phase0Target(999, "勇者", 300f);
            hero.AttackPower = config.Fireball.Damage;

            Log($"  施法者: {hero}");
            Log($"  目标: {enemy}");
            Log("");

            // Flow 编排：火球术
            Log("【2】Flow 编排火球术");
            Log($"  流程: 等待{config.Fireball.CastTime}秒(引导) -> 造成伤害 -> 检测连击");
            Output.Line();

            // Step 1: 等待引导
            // Step 2: 造成伤害
            // Step 3: 连击检测
            var fireballFlow = new SequenceNode(
                new WaitSecondsNode(config.Fireball.CastTime),

                new ActionNode(onEnter: ctx =>
                {
                    float damage = hero.AttackPower;
                    enemy.Health -= damage;
                    Log($"    [伤害] 造成 {damage:F0} 点火焰伤害! (目标剩余: {enemy.Health:F0} HP)");
                }),

                new IfNode(
                    ctx => enemy.Health <= 0,
                    new ActionNode(onEnter: ctx => Log($"    [连击] 击杀目标！触发终结技!")),
                    new ActionNode(onEnter: ctx => Log($"    [连击] 未击杀，连击数+1"))
                )
            );

            // 执行 Flow
            Log("  --- 执行 Flow ---");
            using var session = new FlowSession();
            session.Start(fireballFlow);

            while (session.Status == FlowStatus.Running)
            {
                var status = session.Step(0.1f);
            }
            Log("  --- Flow 完成 ---");
            Log("");

            // 演示并行 Flow
            Log("【3】Flow 编排：引导 + 特效并行 (RaceNode)");
            Log("  流程: 同时开始引导和播放特效，先完成一个就继续");
            Output.Line();

            Log("  --- 执行引导技能 Flow ---");
            var channelFlow = new SequenceNode(
                new ActionNode(
                    onEnter: ctx => Log($"    [施法] 开始施法...")),
                new RaceNode(
                    new WaitSecondsNode(config.Fireball.CastTime),
                    new ActionNode(
                        onEnter: ctx => Log($"    [特效] 播放火焰特效..."),
                        onTick: (ctx, dt) =>
                        {
                            Log($"    [特效] 特效播放中... ({dt:F1}s)");
                            return FlowStatus.Running;
                        },
                        onExit: ctx => Log($"    [特效] 特效播放完成")
                    )
                ),
                new ActionNode(
                    onEnter: ctx => Log($"    [施法] 施法完成! 造成伤害!"))
            );

            using var channelSession = new FlowSession();
            channelSession.Start(channelFlow);
            while (channelSession.Status == FlowStatus.Running)
            {
                channelSession.Step(0.5f);
            }
            Log("  --- 引导完成 ---");
            Log("");

            // 演示条件分支
            Log("【4】Flow 条件分支：HP 低于阈值触发终结技");
            Log($"  流程: 检测目标 HP -> 低于 {config.Combo.HealthThreshold:P0} -> 终结技 | 否则 -> 普通攻击");
            Output.Line();

            enemy.Health = config.Combo.HealthThreshold * enemy.MaxHealth - 1; // 略低于阈值
            Log($"  设置目标 HP = {enemy.Health:F0} (低于{config.Combo.HealthThreshold:P0})");

            var ultimateCheck = new IfNode(
                ctx => enemy.Health < enemy.MaxHealth * config.Combo.HealthThreshold,
                new ActionNode(onEnter: ctx => Log($"    [决策] 目标血量低！释放终结技!")),
                new ActionNode(onEnter: ctx => Log($"    [决策] 目标血量高！普通攻击!"))
            );

            using var checkSession = new FlowSession();
            checkSession.Start(ultimateCheck);
            checkSession.Step(0f);
            Log("");

            // 对比 Phase1
            Log("【对比 Phase1】");
            Output.Bullet("Phase1 Continuous: 只管理\"持续\"，不处理\"时序\"");
            Output.Bullet("Phase2 Flow: 处理\"等待/分支/并行\"等时序逻辑 (框架)");
            Output.Bullet("Continuous 管理生命周期，Flow 管理执行顺序");
            Output.Bullet("两者是正交的，可以组合使用");
            Log("");

            // 暴露下一个痛点
            Log("【下一个痛点】");
            Output.Bullet("如果需要响应战斗事件（受伤、击杀、Buff触发），Flow 无法处理");
            Output.Bullet("Flow 只能按预设流程执行，无法响应外部事件");
            Log("  -> Phase3: Triggering 事件驱动 (框架)");
            Log("");

            Output.Divider();
        }
    }

    // ============================================================================
    // 配置模型
    // ============================================================================

    /// <summary>
    /// Phase2 配置 (从 JSON 加载)
    /// </summary>
    public sealed class Phase2Config
    {
        public Phase2FireballConfig Fireball { get; set; } = new();
        public Phase2ComboConfig Combo { get; set; } = new();

        public static Phase2Config Load()
        {
            const string json = @"{
                ""fireball"": {
                    ""name"": ""火球术"",
                    ""castTime"": 1.5,
                    ""damage"": 80.0,
                    ""manaCost"": 30.0,
                    ""cooldown"": 5.0
                },
                ""combo"": {
                    ""healthThreshold"": 0.3,
                    ""ultimateSkillId"": ""ultimate_01""
                }
            }";
            return Newtonsoft.Json.JsonConvert.DeserializeObject<Phase2Config>(json);
        }
    }

    public sealed class Phase2FireballConfig
    {
        public string Name { get; set; }
        public float CastTime { get; set; }
        public float Damage { get; set; }
        public float ManaCost { get; set; }
        public float Cooldown { get; set; }
    }

    public sealed class Phase2ComboConfig
    {
        public float HealthThreshold { get; set; }
        public string UltimateSkillId { get; set; }
    }
}
