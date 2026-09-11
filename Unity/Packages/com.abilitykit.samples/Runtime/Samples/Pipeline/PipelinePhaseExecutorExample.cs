#nullable enable

using System;
using System.Collections.Generic;
using AbilityKit.Pipeline;
using AbilityKit.Samples.Abstractions;

namespace AbilityKit.Samples.Logic.Samples.Pipeline
{
    /// <summary>
    /// PipelinePhaseExecutorExample - Pipeline 自定义 Phase 执行器进阶示例
    /// 演示自定义 Phase 类型、PhaseExecutor、自定义条件节点、Pipeline 扩展点
    /// </summary>
    [Sample]
    public sealed class PipelinePhaseExecutorExample : SampleBase
    {
        public override string Title => "Pipeline Phase Executors";
        public override string Description => "演示自定义 Phase、执行器注册、PhaseExecutorRegistry、高级 Phase 组合";
        public override SampleCategory Category => SampleCategory.Pipeline;

        protected override void OnRun()
        {
            Log("=== Pipeline 自定义 Phase 执行器进阶示例 ===");
            Output.Divider();

            // 1. Phase 执行器架构
            Log("【1】Phase 执行器架构");
            Output.Bullet("PhaseExecutor - 执行 Phase 的组件");
            Output.Bullet("PhaseExecutorRegistry - 执行器注册表");
            Output.Bullet("PhaseExecutorContext - 执行上下文");
            Output.Bullet("支持按名称/类型查找执行器");
            Log("");
            Log("  ┌─────────────────────────────────────────────┐");
            Log("  │              AbilityPipeline                 │");
            Log("  │  ┌─────┐  ┌─────┐  ┌─────┐  ┌─────┐      │");
            Log("  │  │Phase│→ │Phase│→ │Phase│→ │Phase│      │");
            Log("  │  └──┬──┘  └──┬──┘  └──┬──┘  └──┬──┘      │");
            Log("  └─────┼────────┼────────┼────────┼───────────┘");
            Log("        │        │        │        │");
            Log("        ↓        ↓        ↓        ↓");
            Log("  ┌─────────────────────────────────────────┐");
            Log("  │         PhaseExecutorRegistry             │");
            Log("  │  ┌─────────┐  ┌─────────┐  ┌─────────┐│");
            Log("  │  │Instant  │  │Duration │  │Composite││");
            Log("  │  │Executor │  │ Executor│  │ Executor││");
            Log("  │  └─────────┘  └─────────┘  └─────────┘│");
            Log("  └─────────────────────────────────────────┘");
            Log("");

            // 2. 内置 Phase 类型
            Log("【2】内置 Phase 类型");
            Log("");
            Log("  // 即时 Phase - 一帧内完成");
            Log("  AbilityInstantPhase<TContext>");
            Log("  AbilityInstantPhaseBase<TContext>");
            Log("");
            Log("  // 持续 Phase - 需要多帧完成");
            Log("  AbilityDurationalPhase<TContext>");
            Log("  AbilityDurationalPhaseBase<TContext>");
            Log("");
            Log("  // 延迟 Phase - 等待指定时间");
            Log("  AbilityDelayPhase<TContext>");
            Log("");
            Log("  // 序列 Phase - 顺序执行子 Phase");
            Log("  AbilitySequencePhase<TContext>");
            Log("");
            Log("  // 并行 Phase - 同时执行子 Phase");
            Log("  AbilityParallelPhase<TContext>");
            Log("");

            // 3. 创建自定义 Phase
            Log("【3】创建自定义 Phase");
            Log("");
            Log("  // 自定义即时 Phase");
            Log("  public class DamagePhase : AbilityInstantPhaseBase<BattleContext>");
            Log("  {");
            Log("      public float DamageAmount { get; set; }");
            Log("      ");
            Log("      protected override void OnInstantExecute(BattleContext context)");
            Log("      {");
            Log("          context.Target.ApplyDamage(DamageAmount);");
            Log("          Log($\"造成 {DamageAmount} 点伤害\");");
            Log("      }");
            Log("  }");
            Log("");

            // 4. 创建自定义执行器
            Log("【4】创建自定义执行器");
            Log("");
            Log("  public class DamagePhaseExecutor : PhaseExecutorBase<DamagePhase, BattleContext>");
            Log("  {");
            Log("      protected override void OnExecute(DamagePhase phase, BattleContext context)");
            Log("      {");
            Log("          // 执行前的验证");
            Log("          if (!ValidatePhase(phase, context)) return;");
            Log("          ");
            Log("          // 执行逻辑");
            Log("          ExecutePhase(phase, context);");
            Log("          ");
            Log("          // 执行后的处理");
            Log("          OnPhaseExecuted(phase, context);");
            Log("      }");
            Log("      ");
            Log("      private bool ValidatePhase(DamagePhase phase, BattleContext ctx)");
            Log("      {");
            Log("          return ctx.Target != null && !ctx.Target.IsDead;");
            Log("      }");
            Log("  }");
            Log("");

            // 5. 注册执行器
            Log("【5】执行器注册");
            Log("");
            Log("  // 在静态构造函数或初始化时注册");
            Log("  PhaseExecutorRegistry.Instance.Register<DamagePhase, DamagePhaseExecutor>(\"Damage\");");
            Log("");
            Log("  // 注册多个同名执行器（最后一个生效）");
            Log("  PhaseExecutorRegistry.Instance.Register<DamagePhase, DamagePhaseExecutor>(\"Default\");");
            Log("  ");
            Log("  // 按类型获取执行器");
            Log("  var executor = PhaseExecutorRegistry.Instance.GetExecutor<DamagePhase>();");
            Log("  ");
            Log("  // 按名称获取执行器");
            Log("  var executor = PhaseExecutorRegistry.Instance.GetExecutor(\"Damage\");");
            Log("");

            // 6. 高级 Phase 组合
            Log("【6】高级 Phase 组合");
            Log("");
            Log("  // 序列 Phase - 顺序执行多个子 Phase");
            Log("  var sequence = new AbilitySequencePhase<BattleContext>();");
            Log("  sequence.AddPhase(new DamagePhase { DamageAmount = 50 });");
            Log("  sequence.AddPhase(new DelayPhase { Duration = 0.5f });");
            Log("  sequence.AddPhase(new EffectPhase { EffectId = \"hit_vfx\" });");
            Log("");
            Log("  // 并行 Phase - 同时执行多个子 Phase");
            Log("  var parallel = new AbilityParallelPhase<BattleContext>();");
            Log("  parallel.AddPhase(new AnimationPhase { AnimName = \"attack\" });");
            Log("  parallel.AddPhase(new SoundPhase { SoundId = \"slash\" });");
            Log("  parallel.AddPhase(new DamagePhase { DamageAmount = 100 });");
            Log("");

            // 7. 自定义条件节点
            Log("【7】自定义条件节点 (IAbilityConditionNode)");
            Log("");
            Log("  // 创建条件节点");
            Log("  public class ManaCostCondition : AbilityConditionNodeBase<BattleContext>");
            Log("  {");
            Log("      public float RequiredMana { get; set; }");
            Log("      ");
            Log("      protected override bool OnCheck(BattleContext context)");
            Log("      {");
            Log("          return context.Caster.Mana >= RequiredMana;");
            Log("      }");
            Log("  }");
            Log("");
            Log("  // 使用条件节点");
            Log("  var condition = new ManaCostCondition { RequiredMana = 30f };");
            Log("  var conditionalPhase = new AbilityConditionalPhase(condition,");
            Log("      new DamagePhase { DamageAmount = 100 });");
            Log("");

            // 8. 条件组合
            Log("【8】条件组合");
            Log("");
            Log("  // AND 条件 - 所有条件都满足才通过");
            Log("  var andCondition = new AbilityAndCondition();");
            Log("  andCondition.AddCondition(new ManaCostCondition { RequiredMana = 30 });");
            Log("  andCondition.AddCondition(new CooldownCondition { CooldownId = 1 });");
            Log("  andCondition.AddCondition(new TargetValidCondition());");
            Log("");
            Log("  // OR 条件 - 任一条件满足就通过");
            Log("  var orCondition = new AbilityOrCondition();");
            Log("  orCondition.AddCondition(new HealthBelowCondition { Threshold = 0.3f });");
            Log("  orCondition.AddCondition(new BuffPresentCondition { BuffId = \"rage\" });");
            Log("");
            Log("  // NOT 条件 - 取反");
            Log("  var notCondition = new AbilityNotCondition();");
            Log("  notCondition.Inner = new SilencedCondition();");
            Log("");

            // 9. Phase 执行器上下文
            Log("【9】PhaseExecutorContext");
            Log("");
            Log("  // 创建执行上下文");
            Log("  var execContext = PhaseExecutorContext.Create(context, Log);");
            Log("  ");
            Log("  // 设置数据");
            Log("  execContext.SetData(\"damage\", 100f);");
            Log("  execContext.SetData(\"target\", target);");
            Log("  ");
            Log("  // 获取数据");
            Log("  float damage = execContext.GetData<float>(\"damage\");");
            Log("  ");
            Log("  // 执行追踪");
            Log("  execContext.OnPhaseStart += (p) => Log($\"Phase {p} started\");");
            Log("  execContext.OnPhaseComplete += (p) => Log($\"Phase {p} completed\");");
            Log("");

            // 10. Pipeline 扩展点
            Log("【10】Pipeline 扩展点");
            Log("");
            Log("  // Extension Point - 在特定时机插入自定义逻辑");
            Log("  pipeline.AddExtensionPoint(AbilityPipelinePhaseId.Validation, (ctx, phase) =>");
            Log("  {");
            Log("      Log(\"Validation extension point\");");
            Log("  });");
            Log("");
            Log("  // 可用的扩展点:");
            Log("  // - OnBeforePhase");
            Log("  // - OnAfterPhase");
            Log("  // - OnPipelineStart");
            Log("  // - OnPipelineComplete");
            Log("  // - OnPipelineInterrupt");
            Log("");

            // 11. 完整技能 Pipeline 示例
            Log("【11】完整技能 Pipeline 示例");
            Log("");
            Log("  // 构建技能管线");
            Log("  var pipeline = new AbilityPipeline<SkillContext>();");
            Log("  ");
            Log("  // 1. 验证阶段");
            Log("  pipeline.AddPhase(new InstantPhase(\"Validation\", ctx => {");
            Log("      if (!ctx.Skill.CanCast()) ctx.IsAborted = true;");
            Log("  }));");
            Log("  ");
            Log("  // 2. 消耗阶段");
            Log("  pipeline.AddPhase(new InstantPhase(\"Consume\", ctx => {");
            Log("      ctx.Caster.Mana -= ctx.Skill.ManaCost;");
            Log("  }));");
            Log("  ");
            Log("  // 3. 前摇阶段（延迟）");
            Log("  pipeline.AddPhase(new DelayPhase(\"Cast\", ctx.Skill.CastTime));");
            Log("  ");
            Log("  // 4. 效果阶段");
            Log("  pipeline.AddPhase(new CompositePhase(\"Effect\", new IAbilityPhase[]");
            Log("  {");
            Log("      new DamagePhase { DamageAmount = ctx.Skill.Damage },");
            Log("      new EffectPhase { EffectId = ctx.Skill.EffectId },");
            Log("      new SoundPhase { SoundId = ctx.Skill.SoundId }");
            Log("  }));");
            Log("  ");
            Log("  // 5. 后摇阶段（延迟）");
            Log("  pipeline.AddPhase(new DelayPhase(\"Recovery\", ctx.Skill.RecoveryTime));");
            Log("");

            // 12. 执行器最佳实践
            Log("【12】执行器最佳实践");
            Output.Bullet("每个 Phase 只负责单一职责");
            Output.Bullet("在 Execute 前进行参数验证");
            Output.Bullet("使用上下文传递 Phase 间共享数据");
            Output.Bullet("在 OnPhaseComplete 中清理状态");
            Output.Bullet("实现错误处理和恢复逻辑");
            Output.Bullet("支持 Phase 的暂停和恢复");
            Log("");

            // 13. API 参考
            Log("【13】关键 API 参考");
            Output.Bullet("AbilityKit.Pipeline.Phase");
            Output.Bullet("AbilityKit.Pipeline.Phase.AbilityInstantPhaseBase");
            Output.Bullet("AbilityKit.Pipeline.Phase.AbilityDurationalPhaseBase");
            Output.Bullet("AbilityKit.Pipeline.Phase.AbilityConditionalPhase");
            Output.Bullet("AbilityKit.Pipeline.Phase.AbilitySequencePhase");
            Output.Bullet("AbilityKit.Pipeline.Phase.AbilityParallelPhase");
            Output.Bullet("AbilityKit.Pipeline.Lifecycle.PhaseExecutorRegistry");
            Log("");

            Output.Divider();
            Log("【总结】Phase Executor 模式提供灵活的扩展能力，支持复杂的技能流程编排");
        }
    }
}
