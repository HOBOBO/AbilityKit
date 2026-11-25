using System.Collections.Generic;
using AbilityKit.Pipeline.Pooling;

namespace AbilityKit.Pipeline
{
    /// <summary>
    /// 管线阶段运行实例创建工具。
    /// </summary>
    internal static class AbilityPipelinePhaseRuntime
    {
        /// <summary>
        /// 创建单次运行使用的阶段实例；不支持工厂契约的阶段保持原实例兼容。
        /// </summary>
        public static IAbilityPipelinePhase<TCtx> CreateRunPhase<TCtx>(IAbilityPipelinePhase<TCtx> phase)
            where TCtx : IAbilityPipelineContext
        {
            if (phase is IAbilityPipelinePhaseInstanceFactory<TCtx> factory)
            {
                return factory.CreateRunPhase();
            }

            return phase;
        }

        /// <summary>
        /// 创建单次运行使用的阶段列表。
        /// </summary>
        public static List<IAbilityPipelinePhase<TCtx>> CreateRunPhases<TCtx>(IReadOnlyList<IAbilityPipelinePhase<TCtx>> phases)
            where TCtx : IAbilityPipelineContext
        {
            var result = PipelinePools.RentRunPhaseList<TCtx>();
            try
            {
                for (int i = 0; i < phases.Count; i++)
                {
                    var runPhase = CreateRunPhase(phases[i]);
                    runPhase.Reset();
                    result.Add(runPhase);
                }

                return result;
            }
            catch
            {
                PipelinePools.ReleaseRunPhaseList(result);
                throw;
            }
        }
    }
}
