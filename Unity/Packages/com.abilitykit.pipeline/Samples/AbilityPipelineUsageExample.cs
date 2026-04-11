using System;

namespace AbilityKit.Pipeline.Examples
{
    /// <summary>
    /// 示例：使用管线系统构建技能执行流程
    /// 展示如何组合使用延迟阶段、重复阶段和条件阶段
    /// </summary>
    public static class AbilityPipelineUsageExample
    {
        /// <summary>
        /// 构建一个典型的技能管线：
        /// 1. 前摇延迟 0.5s
        /// 2. 施法阶段
        /// 3. 重复执行3次效果，每次间隔0.2s
        /// 4. 后摇延迟 0.3s
        /// </summary>
        public static AbilityPipeline<ExampleAbilityPipelineContext> BuildSkillPipeline(
            Action<ExampleAbilityPipelineContext, int> onEffectExecute)
        {
            var pipeline = new AbilityPipeline<ExampleAbilityPipelineContext>();

            // 阶段1: 前摇延迟
            pipeline.AddPhase(new AbilityDelayPhase<ExampleAbilityPipelineContext>(0.5f));

            // 阶段2: 施法阶段
            var castPhase = new AbilitySequencePhase<ExampleAbilityPipelineContext>();
            castPhase.AddSubPhase(new AbilityDelayPhase<ExampleAbilityPipelineContext>(0.3f));
            pipeline.AddPhase(castPhase);

            // 阶段3: 重复执行效果
            var repeatPhase = new AbilityRepeatPhase<ExampleAbilityPipelineContext>(3);
            repeatPhase.SetRepeatAction((ctx, index) => onEffectExecute?.Invoke(ctx, index));
            repeatPhase.RepeatInterval = 0.2f;
            pipeline.AddPhase(repeatPhase);

            // 阶段4: 后摇延迟
            pipeline.AddPhase(new AbilityDelayPhase<ExampleAbilityPipelineContext>(0.3f));

            return pipeline;
        }

        /// <summary>
        /// 构建一个BUFF持续效果管线：
        /// 1. 立即生效
        /// 2. 每秒叠加一次伤害，持续5秒
        /// 3. 结束时移除BUFF效果
        /// </summary>
        public static AbilityPipeline<ExampleAbilityPipelineContext> BuildBuffPipeline(
            Action<ExampleAbilityPipelineContext> onBuffStart,
            Action<ExampleAbilityPipelineContext, int> onTickDamage,
            Action<ExampleAbilityPipelineContext> onBuffEnd)
        {
            var pipeline = new AbilityPipeline<ExampleAbilityPipelineContext>();

            // 阶段1: BUFF开始
            var startPhase = new AbilitySequencePhase<ExampleAbilityPipelineContext>(
                new AbilityPipelinePhaseId("BuffStart"));
            pipeline.AddPhase(startPhase);

            // 阶段2: 重复执行伤害（每秒一次，持续5秒）
            var repeatPhase = new AbilityRepeatPhase<ExampleAbilityPipelineContext>(5);
            repeatPhase.SetRepeatAction((ctx, index) => onTickDamage?.Invoke(ctx, index));
            repeatPhase.RepeatInterval = 1.0f;
            pipeline.AddPhase(repeatPhase);

            // 阶段3: BUFF结束
            var endPhase = new AbilitySequencePhase<ExampleAbilityPipelineContext>(
                new AbilityPipelinePhaseId("BuffEnd"));
            pipeline.AddPhase(endPhase);

            // 注册回调事件
            pipeline.Events.OnPhaseStart += (phase, ctx) =>
            {
                if (phase.PhaseId == new AbilityPipelinePhaseId("BuffStart"))
                    onBuffStart?.Invoke(ctx);
                else if (phase.PhaseId == new AbilityPipelinePhaseId("BuffEnd"))
                    onBuffEnd?.Invoke(ctx);
            };

            return pipeline;
        }
    }
}
