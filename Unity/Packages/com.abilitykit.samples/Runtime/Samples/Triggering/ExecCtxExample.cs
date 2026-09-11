#nullable enable

using System;
using AbilityKit.Samples.Abstractions;

namespace AbilityKit.Samples.Logic.Samples.Triggering
{
    /// <summary>
    /// ExecCtxExample - 执行上下文示例
    /// 演示 Triggering 模块中的 ExecCtx 执行上下文核心概念
    /// </summary>
    [Sample]
    public sealed class ExecCtxExample : SampleBase
    {
        public override string Title => "ExecCtx";
        public override string Description => "演示执行上下文 ExecCtx 的核心概念、组件和使用方式";
        public override SampleCategory Category => SampleCategory.Triggering;

        protected override void OnRun()
        {
            Log("=== ExecCtx 执行上下文示例 ===");
            Output.Divider();

            // 1. 核心概念
            Log("【1】核心概念");
            Output.Bullet("ExecCtx<TCtx> - 执行上下文，封装运行时环境");
            Output.Bullet("TCtx - 业务上下文类型（如 BattleContext）");
            Output.Bullet("提供对各种系统服务的访问");
            Output.Bullet("在 Evaluate 和 Execute 阶段传递");
            Log("");

            // 2. ExecCtx 包含的服务
            Log("【2】ExecCtx 包含的服务");
            Output.Bullet("EventBus - 事件总线，用于派发事件");
            Output.Bullet("FunctionRegistry - 函数注册表，调用注册的函数");
            Output.Bullet("ActionRegistry - 动作注册表，执行动作");
            Output.Bullet("Blackboard - 黑板，存储运行时状态");
            Output.Bullet("PayloadAccessorRegistry - 载荷访问器，访问事件数据");
            Output.Bullet("NumericVarDomains - 数值变量域");
            Output.Bullet("NumericFunctions - 数值函数 (RPN)");
            Output.Bullet("ActionSchedulerManager - 动作调度管理器");
            Log("");

            // 3. ExecutionControl
            Log("【3】ExecutionControl 执行控制");
            Output.Bullet("StopPropagation - 阻止后续触发器执行");
            Output.Bullet("Cancel - 取消当前触发器");
            Output.Bullet("ShouldBlock - 检查是否应被打断");
            Output.Bullet("InterruptSourceName - 中断源名称");
            Output.Bullet("InterruptConditionPassed - 中断条件是否通过");
            Log("");

            // 4. 核心组件详解
            Log("【4】核心组件详解");
            Log("");
            Log("  EventBus (事件总线):");
            Log("    - Dispatch<T>(key, args) - 派发事件");
            Log("    - Subscribe/Unsubscribe - 订阅/取消订阅");
            Log("");
            Log("  FunctionRegistry (函数注册表):");
            Log("    - Register(name, func) - 注册函数");
            Log("    - Call<T>(name, args) - 调用函数");
            Log("    - 支持延迟调用");
            Log("");
            Log("  ActionRegistry (动作注册表):");
            Log("    - Execute(action, args) - 执行动作");
            Log("    - 支持动作组合");
            Log("");

            // 5. Blackboard 使用
            Log("【5】Blackboard 使用");
            Log("");
            Log("  // 设置值");
            Log("  ctx.Blackboard.SetInt(\"combo_count\", 5);");
            Log("  ctx.Blackboard.SetFloat(\"damage_multiplier\", 1.5f);");
            Log("");
            Log("  // 获取值");
            Log("  int combo = ctx.Blackboard.GetInt(\"combo_count\", 0);");
            Log("  float dmg = ctx.Blackboard.GetFloat(\"damage_multiplier\", 1.0f);");
            Log("");
            Log("  // 检查键存在");
            Log("  if (ctx.Blackboard.HasKey(\"target\"))");
            Log("  {");
            Log("      var target = ctx.Blackboard.GetObject(\"target\");");
            Log("  }");
            Log("");

            // 6. Payload 访问
            Log("【6】Payload 访问");
            Log("");
            Log("  // 假设 DamageEvent 有 Amount 和 Target 字段");
            Log("  var amount = ctx.Payload.Access<int>(args, \"Amount\");");
            Log("  var target = ctx.Payload.Access<object>(args, \"Target\");");
            Log("");
            Log("  // 使用强类型访问器");
            Log("  var accessor = ctx.Payload.GetAccessor<DamageEvent>();");
            Log("  int damage = accessor.GetAmount(args);");
            Log("");

            // 7. 调用函数
            Log("【7】调用函数");
            Log("");
            Log("  // 直接调用");
            Log("  ctx.Functions.Call(\"print_damage\", damage);");
            Log("");
            Log("  // 延迟调用（调度）");
            Log("  ctx.Functions.CallDelayed(\"spawn_effect\", 0.3f, effectId);");
            Log("");
            Log("  // 调用并获取返回值");
            Log("  var result = ctx.Functions.Call<int, float>(\"calculate_damage\", damage, multiplier);");
            Log("");

            // 8. 执行控制
            Log("【8】执行控制");
            Log("");
            Log("  // 阻止后续触发器");
            Log("  ctx.Control.StopPropagation = true;");
            Log("");
            Log("  // 取消当前触发器");
            Log("  ctx.Control.Cancel = true;");
            Log("");
            Log("  // 检查是否被打断");
            Log("  if (ctx.Control.ShouldBlock(phase, priority))");
            Log("  {");
            Log("      // 当前触发器被更高优先级打断");
            Log("  }");
            Log("");

            // 9. 动作调度
            Log("【9】动作调度");
            Log("");
            Log("  // 获取动作调度器");
            Log("  var scheduler = ctx.ActionSchedulerManager.GetOrCreate(\"default\");");
            Log("");
            Log("  // 调度动作");
            Log("  scheduler.Schedule(new ActionInstance(");
            Log("      () => ApplyDamage(target, damage),");
            Log("      delay: 0.1f");
            Log("  ));");
            Log("");

            // 10. 完整示例
            Log("【10】完整 Trigger 示例");
            Log("");
            Log("  public class DamageTrigger : ITrigger<DamageEvent, BattleContext>");
            Log("  {");
            Log("      public ITriggerCue Cue => NullTriggerCue.Instance;");
            Log("");
            Log("      public bool Evaluate(in DamageEvent args, in ExecCtx<BattleContext> ctx)");
            Log("      {");
            Log("          // 检查伤害值是否足够");
            Log("          var damage = ctx.Payload.Access<float>(args, \"Amount\");");
            Log("          return damage >= 10f;");
            Log("      }");
            Log("");
            Log("      public void Execute(in DamageEvent args, in ExecCtx<BattleContext> ctx)");
            Log("      {");
            Log("          var damage = ctx.Payload.Access<float>(args, \"Amount\");");
            Log("          var target = ctx.Payload.Access<object>(args, \"Target\");");
            Log("");
            Log("          // 更新连击数");
            Log("          int combo = ctx.Blackboard.GetInt(\"combo\", 0) + 1;");
            Log("          ctx.Blackboard.SetInt(\"combo\", combo);");
            Log("");
            Log("          // 应用伤害");
            Log("          ctx.Functions.Call(\"apply_damage\", target, damage);");
            Log("");
            Log("          // 连击数达到阈值时触发特殊效果");
            Log("          if (combo >= 5)");
            Log("          {");
            Log("              ctx.EventBus.Dispatch(\"OnComboThreshold\", combo);");
            Log("          }");
            Log("      }");
            Log("  }");
            Log("");

            // 11. API 参考
            Log("【11】关键 API 参考");
            Output.Bullet("AbilityKit.Triggering.Runtime.ExecCtx");
            Output.Bullet("AbilityKit.Triggering.Runtime.ExecutionControl");
            Output.Bullet("AbilityKit.Triggering.Blackboard");
            Output.Bullet("AbilityKit.Triggering.Registry.FunctionRegistry");
            Output.Bullet("AbilityKit.Triggering.Registry.ActionRegistry");
            Output.Bullet("AbilityKit.Triggering.Eventing.IEventBus");
            Log("");

            Output.Divider();
            Log("【总结】ExecCtx 是 Trigger 系统的运行时核心，封装了所有需要的服务和状态");
        }
    }
}
