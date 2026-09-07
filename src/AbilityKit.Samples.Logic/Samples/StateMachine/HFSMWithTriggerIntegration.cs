using System;
using AbilityKit.HFSM;
using AbilityKit.Samples.Abstractions;

namespace AbilityKit.Samples.Logic.Samples.StateMachine
{
    /// <summary>
    /// HFSMWithTriggerIntegration - HFSM 与 Trigger 系统集成进阶示例
    /// 演示如何在 HFSM 状态转换中集成 Trigger 系统，以及双向数据同步
    /// </summary>
    [Sample]
    public sealed class HFSMWithTriggerIntegration : SampleBase
    {
        public override string Title => "HFSM + Trigger Integration";
        public override string Description => "演示 HFSM 与 Trigger 系统深度集成、状态转换触发、行为执行";
        public override SampleCategory Category => SampleCategory.StateMachine;

        protected override void OnRun()
        {
            Log("=== HFSM 与 Trigger 系统集成进阶示例 ===");
            Output.Divider();

            // 1. 集成架构
            Log("【1】集成架构");
            Output.Bullet("HFSM 控制高层状态流转");
            Output.Bullet("Trigger 系统处理事件响应");
            Output.Bullet("两者共享 Blackboard 作为数据桥梁");
            Output.Bullet("HFSM 可触发 Trigger，Trigger 可请求 HFSM 状态转换");
            Log("");
            Log("  ┌─────────────────────────────────────┐");
            Log("  │              HFSM                    │");
            Log("  │  ┌─────┐   ┌─────┐   ┌─────┐     │");
            Log("  │  │Idle │ → │Move │ → │Atk  │     │");
            Log("  │  └─────┘   └─────┘   └─────┘     │");
            Log("  └───────────────┬─────────────────────┘");
            Log("                  │ 状态事件");
            Log("                  ↓");
            Log("  ┌─────────────────────────────────────┐");
            Log("  │           Trigger System             │");
            Log("  │  ┌─────────────────────────────────┐│");
            Log("  │  │  OnStateEnter → Execute Actions  ││");
            Log("  │  │  OnStateLogic  → Update Logic    ││");
            Log("  │  │  OnStateExit   → Cleanup         ││");
            Log("  │  └─────────────────────────────────┘│");
            Log("  └─────────────────────────────────────┘");
            Log("");

            // 2. 状态转换触发
            Log("【2】状态转换触发方式");
            Log("");
            Log("  方式一：HFSM 内部转换条件触发 Trigger");
            Log("  ────────────────────────────────");
            Log("  fsm.AddTransition(new Transition<string>(");
            Log("      from: \"Idle\",");
            Log("      to: \"Combat\",");
            Log("      condition: t => detected,");
            Log("      onTransition: t => EventBus.Dispatch(\"OnEnterCombat\")");
            Log("  ));");
            Log("");
            Log("  方式二：Trigger 回调请求 HFSM 状态转换");
            Log("  ────────────────────────────────────");
            Log("  // 在 Trigger 的 Action 中请求转换");
            Log("  ctx.Blackboard.SetString(\"requestedState\", \"Retreat\");");
            Log("  ctx.EventBus.Dispatch(\"OnStateChangeRequested\");");
            Log("");

            // 3. 共享数据结构
            Log("【3】共享数据结构 (SharedContext)");
            Log("");
            Log("  public class BattleSharedContext");
            Log("  {");
            Log("      // 实体标识");
            Log("      public long EntityId;");
            Log("");
            Log("      // HFSM 状态");
            Log("      public string CurrentState;");
            Log("      public string PreviousState;");
            Log("");
            Log("      // 战斗数据");
            Log("      public float Health;");
            Log("      public float Mana;");
            Log("      public float DistanceToTarget;");
            Log("");
            Log("      // 事件标记");
            Log("      public bool HasTarget;");
            Log("      public bool IsInCombat;");
            Log("      public int ComboCount;");
            Log("  }");
            Log("");

            // 4. 状态事件定义
            Log("【4】StateMachine 事件键");
            Output.Bullet("EventKey<StateChangeEvent> OnStateChanging");
            Output.Bullet("EventKey<StateChangeEvent> OnStateChanged");
            Output.Bullet("EventKey<TransitionEvent> OnTransition");
            Output.Bullet("EventKey<TickEvent> OnStateLogic");
            Log("");

            // 5. 完整集成示例
            Log("【5】完整集成示例");
            Log("");
            Log("  // 1. 创建共享上下文");
            Log("  var shared = new BattleSharedContext();");
            Log("  shared.EntityId = entityId;");
            Log("  shared.Health = 100f;");
            Log("");
            Log("  // 2. 创建 HFSM");
            Log("  var fsm = new StateMachine<string, string, BattleSharedContext>();");
            Log("  ");
            Log("  // 3. 添加带 Trigger 集成的状态");
            Log("  fsm.AddState(\"Idle\", new TriggerIntegratedState(");
            Log("      onEnter: (s, ctx) => {");
            Log("          ctx.EventBus.Dispatch(\"OnEnterIdle\");");
            Log("      },");
            Log("      onLogic: (s, ctx) => {");
            Log("          // 检查是否应该进入战斗");
            Log("          if (ctx.HasTarget) {");
            Log("              fsm.RequestStateChange(\"Combat\");");
            Log("          }");
            Log("      }");
            Log("  ));");
            Log("");
            Log("  // 4. 配置转换（由 Trigger 条件触发）");
            Log("  fsm.AddTransition(new Transition<string>(");
            Log("      from: \"Idle\",");
            Log("      to: \"Combat\",");
            Log("      condition: t => shared.HasTarget");
            Log("  ));");
            Log("");

            // 6. TriggerIntegratedState 实现
            Log("【6】TriggerIntegratedState 实现");
            Log("");
            Log("  public class TriggerIntegratedState : State");
            Log("  {");
            Log("      private readonly Action<TriggerIntegratedState, T ctx> _onEnter;");
            Log("      private readonly Action<TriggerIntegratedState, T ctx> _onLogic;");
            Log("      private readonly Action<TriggerIntegratedState, T ctx> _onExit;");
            Log("");
            Log("      public TriggerIntegratedState(");
            Log("          Action<TriggerIntegratedState, T> onEnter = null,");
            Log("          Action<TriggerIntegratedState, T> onLogic = null,");
            Log("          Action<TriggerIntegratedState, T> onExit = null)");
            Log("      {");
            Log("          _onEnter = onEnter;");
            Log("          _onLogic = onLogic;");
            Log("          _onExit = onExit;");
            Log("      }");
            Log("");
            Log("      public override void OnEnter() => _onEnter?.Invoke(this, SharedContext);");
            Log("      public override void OnLogic() => _onLogic?.Invoke(this, SharedContext);");
            Log("      public override void OnExit() => _onExit?.Invoke(this, SharedContext);");
            Log("  }");
            Log("");

            // 7. Trigger 回调状态转换
            Log("【7】Trigger 回调状态转换");
            Log("");
            Log("  // Trigger 定义");
            Log("  public class RetreatTrigger : ITrigger<HealthChangedEvent, BattleCtx>");
            Log("  {");
            Log("      public bool Evaluate(in HealthChangedEvent args, in ExecCtx ctx)");
            Log("      {");
            Log("          return args.NewHealth < 20f;  // 生命低于 20%");
            Log("      }");
            Log("");
            Log("      public void Execute(in HealthChangedEvent args, in ExecCtx ctx)");
            Log("      {");
            Log("          // 请求 HFSM 转换到 Retreat 状态");
            Log("          ctx.Blackboard.SetString(\"requestedState\", \"Retreat\");");
            Log("          ctx.EventBus.Dispatch(\"OnRetreatRequested\", new RetreatEvent {");
            Log("              Reason = \"LowHealth\"");
            Log("          });");
            Log("      }");
            Log("  }");
            Log("");

            // 8. 监听 HFSM 事件
            Log("【8】监听 HFSM 事件");
            Log("");
            Log("  // 监听状态转换事件");
            Log("  EventBus.Subscribe(\"OnStateChanged\", (StateChangeEvent e) => {");
            Log("      Log($\"State: {e.From} → {e.To}\");");
            Log("      ");
            Log("      // 根据状态更新 Trigger 上下文");
            Log("      switch (e.To)");
            Log("      {");
            Log("          case \"Combat\":");
            Log("              EnableCombatTriggers();");
            Log("              break;");
            Log("          case \"Retreat\":");
            Log("              DisableOffensiveTriggers();");
            Log("              break;");
            Log("      }");
            Log("  });");
            Log("");

            // 9. 双向数据同步
            Log("【9】双向数据同步");
            Log("");
            Log("  // HFSM → Trigger (通过 Blackboard)");
            Log("  shared.Blackboard.SetFloat(\"health\", health);");
            Log("  shared.Blackboard.SetFloat(\"mana\", mana);");
            Log("  shared.Blackboard.SetBool(\"hasTarget\", hasTarget);");
            Log("  ");
            Log("  // Trigger → HFSM (通过事件请求)");
            Log("  ctx.EventBus.Dispatch(\"OnHFSM_RequestState\", new Request {");
            Log("      TargetState = \"SpecialAttack\",");
            Log("      Priority = 100");
            Log("  });");
            Log("");

            // 10. 典型应用场景
            Log("【10】典型应用场景");
            Output.Bullet("AI 状态机 + 技能触发 - 敌人进入攻击状态时触发技能");
            Output.Bullet("Buff 状态 + 效果触发 - 被眩晕时触发反击技能");
            Output.Bullet("连击系统 + HFSM - 连击数达到阈值时触发特殊状态");
            Output.Bullet("Boss 机制 + Trigger - Boss 血量变化触发阶段转换");
            Output.Bullet("宠物/召唤物 AI - 状态机控制移动，Trigger 控制技能释放");
            Log("");

            // 11. 最佳实践
            Log("【11】最佳实践");
            Output.Bullet("保持状态机简洁，复杂逻辑放到 Trigger 中");
            Output.Bullet("使用 SharedContext 统一管理共享数据");
            Output.Bullet("避免 Trigger 和 HFSM 之间的循环依赖");
            Output.Bullet("状态转换使用请求-确认模式，避免竞态");
            Output.Bullet("为 Trigger 分配优先级，避免冲突");
            Log("");

            Output.Divider();
            Log("【总结】HFSM + Trigger 集成是复杂游戏逻辑的标准模式，HFSM 控制宏观流程，Trigger 处理微观响应");
        }
    }
}
