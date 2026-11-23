using System;
using System.Collections.Generic;
using AbilityKit.Pipeline.Pooling;

namespace AbilityKit.Pipeline
{
    /// <summary>
    /// 抽象核心管线流程。
    /// </summary>
    public abstract partial class AbilityPipeline<TCtx> : IAbilityPipeline<TCtx>, IPipelineDebugStructureProvider, IPipelineDebugGraphProvider
        where TCtx : IAbilityPipelineContext
    {
        /// <summary>
        /// 管线事件集合。
        /// </summary>
        public AbilityPipelineEvents<TCtx> Events { get; } = new AbilityPipelineEvents<TCtx>();

        /// <summary>
        /// 当前管线使用的运行时上下文。
        /// </summary>
        public PipelineRuntime Runtime { get; set; } = Pipeline.DefaultRuntime;

        private readonly List<IAbilityPipelinePhase<TCtx>> _phases = new List<IAbilityPipelinePhase<TCtx>>(8);

        /// <summary>
        /// 启动一次管线运行。
        /// </summary>
        public IAbilityPipelineRun<TCtx> Start(IAbilityPipelineConfig config, TCtx context)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (context == null) throw new ArgumentNullException(nameof(context));

            var runPhases = AbilityPipelinePhaseRuntime.CreateRunPhases(_phases);
            return new Run(this, Runtime ?? Pipeline.DefaultRuntime, config, context, runPhases);
        }

        /// <summary>
        /// 附着一次运行，在登记前由调用方初始化运行状态，跳过普通启动事件。
        /// 调用方负责校验和恢复业务数据；登记及调试观察仍会执行。
        /// </summary>
        public IAbilityPipelineRun<TCtx> AttachRun(IAbilityPipelineConfig config, TCtx context,
            Action<IAbilityPipelineRun<TCtx>> initialize)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (initialize == null) throw new ArgumentNullException(nameof(initialize));
            var runPhases = AbilityPipelinePhaseRuntime.CreateRunPhases(_phases);
            try
            {
                return new Run(this, Runtime ?? Pipeline.DefaultRuntime, config, context, runPhases, initialize);
            }
            catch
            {
                PipelinePools.ReleaseRunPhaseList(runPhases);
                throw;
            }
        }

        /// <summary>
        /// 重置管线内所有阶段运行态。
        /// </summary>
        public virtual void Reset()
        {
            for (int i = 0; i < _phases.Count; i++)
            {
                _phases[i].Reset();
            }
        }

        /// <summary>
        /// 将阶段追加到管线末尾。
        /// </summary>
        public void AddPhase(IAbilityPipelinePhase<TCtx> phase)
        {
            if (phase == null) throw new ArgumentNullException(nameof(phase));
            _phases.Add(phase);
        }

        /// <summary>
        /// 将阶段插入到指定索引位置。
        /// </summary>
        public void InsertPhase(int index, IAbilityPipelinePhase<TCtx> phase)
        {
            if (phase == null) throw new ArgumentNullException(nameof(phase));
            _phases.Insert(index, phase);
        }

        /// <summary>
        /// 按阶段 ID 移除第一个匹配阶段。
        /// </summary>
        public void RemovePhase(AbilityPipelinePhaseId phaseId)
        {
            for (int i = 0; i < _phases.Count; i++)
            {
                if (_phases[i].PhaseId == phaseId)
                {
                    _phases.RemoveAt(i);
                    return;
                }
            }
        }

        /// <summary>
        /// 释放管线运行绑定的上下文。
        /// </summary>
        protected abstract void ReleaseContext(TCtx context);
    }
}
