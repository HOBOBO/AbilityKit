#nullable enable

using System;
using System.Collections.Generic;
using AbilityKit.Pipeline;
using AbilityKit.Samples.Abstractions;

namespace AbilityKit.Samples.Logic.Samples.Demo.ProgressiveSkill
{
    /// <summary>
    /// ProgressiveSkill Phase4 - Pipeline 阶段组合 (框架提供)
    ///
    /// 需求: 完整技能执行流程：验证 -> 消耗 -> 引导 -> 效果 -> 冷却，支持打断。
    ///
    /// 框架能力: com.abilitykit.pipeline 的 AbilityPipeline + AbilityPhase。
    /// 从 Phase4_Pipeline.json 加载配置数据。
    /// 展示如何用 Pipeline 编排技能执行的各个阶段。
    /// </summary>
    [Sample]
    public sealed class ProgressiveSkill_Phase4 : SampleBase
    {
        public override string Title => "ProgressiveSkill Phase4";
        public override string Description => "Pipeline - 阶段化技能执行 (框架)";
        public override SampleCategory Category => SampleCategory.Demo;

        protected override void OnRun()
        {
            Log("================================================================================");
            Log("===           渐进式技能系统 - Phase4: Pipeline 阶段组合 (框架)   ===");
            Log("================================================================================");
            Output.Divider();

            // 从 JSON 配置加载
            var config = Phase4Config.Load();
            Log("【1】从 Phase4_Pipeline.json 加载配置");
            Log($"  火球术: castTime={config.Skill.CastTime}s, damage={config.Skill.Damage}, manaCost={config.Skill.ManaCost}");
            Log($"  冷却: {config.Skill.Cooldown}s");
            Log("");

            // 创建目标
            var enemy = new Phase0Target(1, "哥布林王", 500f);
            var hero = new Phase0Target(999, "勇者", 300f);
            hero.Mana = 100f;
            hero.AttackPower = config.Skill.Damage;

            Log($"  施法者: {hero}");
            Log($"  目标: {enemy}");
            Log("");

            // ========== 构建火球术 Pipeline ==========
            Log("【2】构建火球术 Pipeline (框架)");
            Output.Line();

            var pipeline = new InstantAbilityPipeline<Phase4SkillContext>();
            var skill = new Phase4SkillDef(config.Skill);

            // 阶段1: 验证
            pipeline.AddPhase(new Phase4InstantPhase("Validation", ctx =>
            {
                Log($"  [Validation] 检查施法条件...");

                if (!ctx.Caster.IsAlive)
                {
                    ctx.IsAborted = true;
                    Log($"  [Validation] 失败: 施法者已死亡");
                    return;
                }

                if (ctx.Target == null || !ctx.Target.IsAlive)
                {
                    ctx.IsAborted = true;
                    Log($"  [Validation] 失败: 目标无效");
                    return;
                }

                if (ctx.Caster.Mana < skill.ManaCost)
                {
                    ctx.IsAborted = true;
                    Log($"  [Validation] 失败: 魔法值不足 ({ctx.Caster.Mana:F0} < {skill.ManaCost})");
                    return;
                }

                Log($"  [Validation] 通过!");
            }));

            // 阶段2: 消耗
            pipeline.AddPhase(new Phase4InstantPhase("Consume", ctx =>
            {
                Log($"  [Consume] 消耗魔法值 -{skill.ManaCost}");
                ctx.Caster.Mana -= skill.ManaCost;
            }));

            // 阶段3: 引导 (模拟延迟)
            pipeline.AddPhase(new Phase4InstantPhase("Channeling", ctx =>
            {
                Log($"  [Channeling] 开始引导 (模拟 {config.Skill.CastTime} 秒)...");
                for (int i = 0; i < 3; i++)
                {
                    Log($"    引导中... {i + 1}/3");
                }
                Log($"  [Channeling] 引导完成!");
            }));

            // 阶段4: 效果
            pipeline.AddPhase(new Phase4InstantPhase("Effect", ctx =>
            {
                Log($"  [Effect] 造成伤害!");

                float damage = ctx.Caster.AttackPower;
                ctx.Target.Health -= damage;

                Log($"    造成 {damage:F0} 点火焰伤害! 目标剩余: {ctx.Target.Health:F0} HP");
                Log($"    添加灼烧效果: 每秒 5 伤害，持续 3 秒");
            }));

            // 阶段5: 冷却
            pipeline.AddPhase(new Phase4InstantPhase("Cooldown", ctx =>
            {
                Log($"  [Cooldown] 设置冷却: {config.Skill.Cooldown}s");
            }));

            Log("  Pipeline 阶段:");
            Log("    1. Validation (验证) - 检查施法条件");
            Log("    2. Consume (消耗) - 消耗魔法值");
            Log("    3. Channeling (引导) - 延迟引导");
            Log("    4. Effect (效果) - 造成伤害");
            Log("    5. Cooldown (冷却) - 设置冷却");
            Log("");

            // ========== 执行 Pipeline ==========
            Log("【3】执行火球术 Pipeline");
            Output.Line();

            var context = new Phase4SkillContext(hero, enemy, skill);
            var result = pipeline.RunToCompletion(new Phase4PipelineConfig(config.Skill.Cooldown), context);

            Log("");
            Log($"  Pipeline 结果: {result.State}");
            Log($"  目标最终 HP: {enemy.Health:F0}");
            Log("");

            // ========== 演示打断 ==========
            Log("【4】演示 Pipeline 打断");
            Output.Line();

            var enemy2 = new Phase0Target(2, "哥布林", 300f);
            hero.Mana = 100f;

            var pipeline2 = new InstantAbilityPipeline<Phase4SkillContext>();
            var skill2 = new Phase4SkillDef(config.Skill);

            pipeline2.AddPhase(new Phase4InstantPhase("Validation", ctx =>
            {
                Log($"  [Validation] 检查施法条件...");
                if (ctx.Caster.Mana < skill2.ManaCost)
                {
                    ctx.IsAborted = true;
                    Log($"  [Validation] 失败: 魔法值不足");
                }
                else
                {
                    Log($"  [Validation] 通过!");
                }
            }));

            pipeline2.AddPhase(new Phase4InstantPhase("Channeling", ctx =>
            {
                Log($"  [Channeling] 引导中...");
            }));

            pipeline2.AddPhase(new Phase4InstantPhase("Effect", ctx =>
            {
                Log($"  [Effect] 造成伤害!");
            }));

            var context2 = new Phase4SkillContext(hero, enemy2, skill2);

            Log("  假设施法过程中被眩晕打断...");
            Log("  Pipeline 检测 IsAborted = true，跳过剩余阶段");
            Log("");

            context2.IsAborted = true;
            var result2 = pipeline2.RunToCompletion(new Phase4PipelineConfig(config.Skill.Cooldown), context2);

            Log($"  Pipeline 结果: {result2.State}");
            Log($"  目标剩余 HP: {enemy2.Health:F0} (未受伤)");
            Log("");

            // ========== Pipeline 概念说明 ==========
            Log("【5】Pipeline 核心概念 (框架)");
            Output.Line();
            Log("  AbilityPipeline: 技能管线，管理多个阶段的执行 (框架)");
            Log("  AbilityInstantPhaseBase: 瞬时阶段，一帧内完成 (框架)");
            Log("  IAbilityPipelineContext: 上下文，传递阶段间共享数据 (框架)");
            Log("  IsAborted: 打断标志，true 时跳过剩余阶段");
            Log("");

            // ========== 对比 Phase3 ==========
            Log("【对比 Phase3】");
            Output.Bullet("Phase3 Triggering: 响应战斗事件，不关心技能执行流程");
            Output.Bullet("Phase4 Pipeline: 管理技能执行流程，不关心事件响应 (框架)");
            Output.Bullet("两者是正交的，可以组合使用");
            Output.Bullet("Triggering 负责事件响应，Pipeline 负责流程控制");
            Log("");

            // ========== 暴露下一个痛点 ==========
            Log("【下一个痛点】");
            Output.Bullet("如何把 Pipeline + Triggering + Continuous 整合在一起？");
            Output.Bullet("Pipeline 执行技能 -> 触发 Trigger -> Trigger 添加 Continuous Buff");
            Output.Bullet("需要统一管理这些系统");
            Log("  -> Phase5: HFSM 角色状态机 (框架)");
            Log("");

            Output.Divider();
        }
    }

    // ============================================================================
    // 配置模型
    // ============================================================================

    /// <summary>
    /// Phase4 配置 (从 JSON 加载)
    /// </summary>
    public sealed class Phase4Config
    {
        public Phase4SkillConfig Skill { get; set; } = new();

        public static Phase4Config Load()
        {
            const string json = @"{
                ""skill"": {
                    ""name"": ""火球术"",
                    ""castTime"": 1.5,
                    ""damage"": 80.0,
                    ""manaCost"": 30.0,
                    ""cooldown"": 5.0,
                    ""range"": 30.0
                }
            }";
            return Newtonsoft.Json.JsonConvert.DeserializeObject<Phase4Config>(json);
        }
    }

    public sealed class Phase4SkillConfig
    {
        public string Name { get; set; }
        public float CastTime { get; set; }
        public float Damage { get; set; }
        public float ManaCost { get; set; }
        public float Cooldown { get; set; }
        public float Range { get; set; }
    }

    // ============================================================================
    // Pipeline 相关类型
    // ============================================================================

    /// <summary>
    /// 技能上下文 - 实现 IAbilityPipelineContext (框架接口)
    /// </summary>
    public class Phase4SkillContext : IAbilityPipelineContext
    {
        public Phase0Target Caster { get; }
        public Phase0Target Target { get; }
        public Phase4SkillDef Skill { get; }
        public object AbilityInstance { get; set; }
        public AbilityPipelinePhaseId CurrentPhaseId { get; set; }
        public EAbilityPipelineState PipelineState { get; set; }
        public bool IsAborted { get; set; }
        public bool IsPaused { get; set; }
        public float StartTime { get; set; }
        public float ElapsedTime => 0f; // 简化版本
        private readonly Dictionary<string, object> _sharedData = new();

        public Dictionary<string, object> SharedData => _sharedData;

        public Phase4SkillContext(Phase0Target caster, Phase0Target target, Phase4SkillDef skill)
        {
            Caster = caster;
            Target = target;
            Skill = skill;
        }

        public T GetData<T>(string key, T defaultValue = default)
        {
            if (_sharedData.TryGetValue(key, out var value) && value is T typedValue)
                return typedValue;
            return defaultValue;
        }

        public void SetData<T>(string key, T value)
        {
            _sharedData[key] = value;
        }

        public bool TryGetData<T>(string key, out T value)
        {
            if (_sharedData.TryGetValue(key, out var obj) && obj is T typedValue)
            {
                value = typedValue;
                return true;
            }
            value = default;
            return false;
        }

        public bool RemoveData(string key) => _sharedData.Remove(key);
        public void ClearData() => _sharedData.Clear();

        public void Reset()
        {
            AbilityInstance = null;
            CurrentPhaseId = default;
            PipelineState = EAbilityPipelineState.Ready;
            IsAborted = false;
            IsPaused = false;
            StartTime = 0;
            _sharedData.Clear();
        }
    }

    /// <summary>
    /// Pipeline 配置
    /// </summary>
    public class Phase4PipelineConfig : IAbilityPipelineConfig
    {
        public Phase4PipelineConfig(float cooldown)
        {
            Cooldown = cooldown;
        }

        public float Cooldown { get; }
        public int ConfigId => 0;
        public string ConfigName => "FireballSkill";
        public IReadOnlyList<IAbilityPhaseConfig> PhaseConfigs => Array.Empty<IAbilityPhaseConfig>();
        public bool AllowInterrupt => true;
        public bool AllowPause => true;
    }

    /// <summary>
    /// 瞬时阶段 - 继承 AbilityInstantPhaseBase (框架)
    /// </summary>
    public class Phase4InstantPhase : AbilityInstantPhaseBase<Phase4SkillContext>
    {
        private readonly Action<Phase4SkillContext> _action;

        public Phase4InstantPhase(string name, Action<Phase4SkillContext> action) : base(name)
        {
            _action = action;
        }

        protected override void OnInstantExecute(Phase4SkillContext context)
        {
            _action?.Invoke(context);
        }
    }

    /// <summary>
    /// 技能定义
    /// </summary>
    public class Phase4SkillDef
    {
        private readonly Phase4SkillConfig _config;

        public Phase4SkillDef(Phase4SkillConfig config)
        {
            _config = config;
        }

        public string Name => _config.Name;
        public float ManaCost => _config.ManaCost;
        public float Cooldown => _config.Cooldown;
        public float CastTime => _config.CastTime;
        public float Damage => _config.Damage;
        public float Range => _config.Range;
    }
}
