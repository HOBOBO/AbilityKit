#nullable enable

using System;
using AbilityKit.Samples.Abstractions;

namespace AbilityKit.Samples.Logic.Samples.Triggering
{
    /// <summary>
    /// ContinuousEffectsExample - 持续效果示例
    /// 演示 Triggering 模块中的 Continuous Effects（持续效果）系统
    /// </summary>
    [Sample]
    public sealed class ContinuousEffectsExample : SampleBase
    {
        public override string Title => "Continuous Effects";
        public override string Description => "演示持续效果、ContinuousExecutor、Ongoing Effect 管理";
        public override SampleCategory Category => SampleCategory.Triggering;

        protected override void OnRun()
        {
            Log("=== Continuous Effects 持续效果示例 ===");
            Output.Divider();

            // 1. 核心概念
            Log("【1】核心概念");
            Output.Bullet("Continuous Effect - 持续效果，随时间持续生效");
            Output.Bullet("Ongoing Effect - 进行中的效果，需要定期更新");
            Output.Bullet("IContinuousTriggerInstance - 持续触发器实例");
            Output.Bullet("ContinuousExecutor - 持续效果执行器");
            Output.Bullet("Effect Lifecycle - 效果的创建、更新、结束");
            Log("");

            // 2. 持续效果类型
            Log("【2】持续效果类型");
            Output.Bullet("DOT (Damage Over Time) - 持续伤害");
            Output.Bullet("HOT (Heal Over Time) - 持续治疗");
            Output.Bullet("Buff/Debuff - 属性增益/减益");
            Output.Bullet("Periodic Effect - 周期效果（定时触发）");
            Output.Bullet("Channeling - 引导效果");
            Output.Bullet("Area Effect - 区域效果");
            Log("");

            // 3. ContinuousExecutor
            Log("【3】ContinuousExecutor");
            Output.Bullet("管理多个持续效果的更新");
            Output.Bullet("处理效果的添加、移除、更新");
            Output.Bullet("提供 Tick 接口驱动所有效果");
            Output.Bullet("支持效果的暂停和恢复");
            Log("");

            // 4. IContinuousTriggerInstance
            Log("【4】IContinuousTriggerInstance");
            Output.Bullet("Duration - 效果持续时间");
            Output.Bullet("Elapsed - 已经过时间");
            Output.Bullet("Interval - 触发间隔");
            Output.Bullet("TickCount - 已触发次数");
            Output.Bullet("IsActive - 是否激活");
            Output.Bullet("Source - 效果来源");
            Log("");

            // 5. Effect 配置
            Log("【5】Effect 配置结构");
            Output.Bullet("EffectId - 效果唯一标识");
            Output.Bullet("Duration - 持续时间（秒）");
            Output.Bullet("Interval - 触发间隔（秒）");
            Output.Bullet("Magnitude - 效果强度");
            Output.Bullet("StackPolicy - 叠加策略");
            Output.Bullet("Tags - 关联的标签");
            Log("");

            // 6. 代码示例 - 创建持续伤害效果
            Log("【6】代码示例 - DOT 持续伤害");
            Log("");
            Log("  // 创建 DOT 配置");
            Log("  var dotConfig = new ContinuousEffectConfig");
            Log("  {");
            Log("      EffectId = 1001,");
            Log("      Duration = 5.0f,        // 持续 5 秒");
            Log("      Interval = 1.0f,        // 每秒触发");
            Log("      Magnitude = 10f,        // 每次 10 点伤害");
            Log("      StackPolicy = StackPolicy.Refresh  // 刷新持续时间");
            Log("  };");
            Log("");
            Log("  // 应用效果");
            Log("  var instance = executor.Apply(target, dotConfig);");
            Log("");
            Log("  // 更新效果（每帧调用）");
            Log("  executor.Tick(deltaTime);");
            Log("");

            // 7. 代码示例 - 叠加策略
            Log("【7】叠加策略 (StackPolicy)");
            Log("");
            Log("  // Refresh - 刷新持续时间");
            Log("  dot.StackPolicy = StackPolicy.Refresh;");
            Log("  // 再次应用时重置计时器");
            Log("");
            Log("  // Stack - 叠加效果");
            Log("  dot.StackPolicy = StackPolicy.Stack;");
            Log("  // 再次应用时增加叠加层数");
            Log("");
            Log("  // Replace - 替换效果");
            Log("  dot.StackPolicy = StackPolicy.Replace;");
            Log("  // 再次应用时替换原有效果");
            Log("");
            Log("  // Ignore - 忽略重复");
            Log("  dot.StackPolicy = StackPolicy.Ignore;");
            Log("  // 再次应用时忽略");
            Log("");

            // 8. 与 Trigger 系统集成
            Log("【8】与 Trigger 系统集成");
            Log("");
            Log("  // ApplyOngoingEffectAction - 应用持续效果动作");
            Log("  public class ApplyDOTAction : ActionBehavior");
            Log("  {");
            Log("      public void Execute(DamageEvent args, ExecCtx ctx)");
            Log("      {");
            Log("          var config = new ContinuousEffectConfig");
            Log("          config.Duration = 5f;");
            Log("          config.Interval = 1f;");
            Log("          config.Magnitude = args.Amount;");
            Log("");
            Log("          ctx.ContinuousExecutor.Apply(args.Target, config);");
            Log("      }");
            Log("  }");
            Log("");

            // 9. 周期触发
            Log("【9】周期触发机制");
            Output.Bullet("基于固定间隔的触发");
            Output.Bullet("基于游戏时间（非真实时间）");
            Output.Bullet("支持同步和异步触发模式");
            Output.Bullet("提供 OnTick 回调");
            Log("");
            Log("  // 每次触发时调用");
            Log("  instance.OnTick += (effect, deltaTime) =>");
            Log("  {");
            Log("      ApplyPeriodicDamage(effect.Target, effect.Magnitude);");
            Log("  };");
            Log("");

            // 10. 效果结束处理
            Log("【10】效果结束处理");
            Output.Bullet("DurationExpired - 持续时间结束");
            Output.Bullet("ManuallyRemoved - 手动移除");
            Output.Bullet("TargetDied - 目标死亡");
            Output.Bullet("OwnerDied - 来源死亡");
            Output.Bullet("WorldDestroyed - World 销毁");
            Log("");
            Log("  // 注册结束回调");
            Log("  instance.OnExpire += (effect) =>");
            Log("  {");
            Log("      Log($\"Effect {effect.EffectId} expired\");");
            Log("      // 清理状态");
            Log("  };");
            Log("");

            // 11. 典型使用场景
            Log("【11】典型使用场景");
            Output.Bullet("DOT 技能 - 中毒、燃烧、流血");
            Output.Bullet("HOT 技能 - 持续治疗");
            Output.Bullet("护盾 - 吸收伤害");
            Output.Bullet("Buff 系统 - 属性加成");
            Output.Bullet("状态效果 - 沉默、眩晕、减速");
            Output.Bullet("引导技能 - 持续施法");
            Log("");

            // 12. API 参考
            Log("【12】关键 API 参考");
            Output.Bullet("AbilityKit.Triggering.Continuous");
            Output.Bullet("AbilityKit.Triggering.ContinuousExecutor");
            Output.Bullet("AbilityKit.Triggering.Continuous.ContinuousEffectConfig");
            Output.Bullet("AbilityKit.Triggering.Continuous.IContinuousTriggerInstance");
            Log("");

            Output.Divider();
            Log("【总结】Continuous Effects 是实现 DOT/HOT、Buff/Debuff 等时间相关效果的核心系统");
        }
    }
}
