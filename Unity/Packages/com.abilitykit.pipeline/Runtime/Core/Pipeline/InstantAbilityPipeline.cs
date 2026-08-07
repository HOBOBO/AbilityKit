using System;
using System.Collections.Generic;

namespace AbilityKit.Pipeline
{
    /// <summary>
    /// 即时型管线。
    /// </summary>
    /// <typeparam name="TCtx">管线上下文类型。</typeparam>
    /// <remarks>
    /// 仅接受瞬时阶段，并且会同步运行到完成，不需要外部逐帧驱动。
    /// </remarks>
    public sealed class InstantAbilityPipeline<TCtx>
        where TCtx : IAbilityPipelineContext
    {
        private readonly List<IAbilityInstantPhase<TCtx>> _phases = new List<IAbilityInstantPhase<TCtx>>(8);

        /// <summary>
        /// 管线事件集合。
        /// </summary>
        public AbilityPipelineEvents<TCtx> Events { get; } = new AbilityPipelineEvents<TCtx>();

        /// <summary>
        /// 添加一个瞬时阶段。
        /// </summary>
        public void AddPhase(IAbilityInstantPhase<TCtx> phase)
        {
            if (phase == null) throw new ArgumentNullException(nameof(phase));
            _phases.Add(phase);
        }

        /// <summary>
        /// 在指定索引插入一个瞬时阶段。
        /// </summary>
        public void InsertPhase(int index, IAbilityInstantPhase<TCtx> phase)
        {
            if (phase == null) throw new ArgumentNullException(nameof(phase));
            _phases.Insert(index, phase);
        }

        /// <summary>
        /// 重置所有阶段状态。
        /// </summary>
        public void Reset()
        {
            for (int i = 0; i < _phases.Count; i++)
            {
                _phases[i].Reset();
            }
        }

        /// <summary>
        /// 执行管线直到完成。
        /// </summary>
        /// <param name="config">管线配置。</param>
        /// <param name="context">管线上下文。</param>
        /// <returns>统一执行结果。</returns>
        public PipelineExecutionResult RunToCompletion(IAbilityPipelineConfig config, TCtx context)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (context == null) throw new ArgumentNullException(nameof(context));

            for (int i = 0; i < _phases.Count; i++)
            {
                _phases[i].Reset();
            }

            context.PipelineState = EAbilityPipelineState.Executing;
            context.IsPaused = false;
            context.IsAborted = false;

            Events?.OnPipelineStart?.Invoke(context);

            for (int i = 0; i < _phases.Count; i++)
            {
                if (context.IsAborted)
                {
                    context.PipelineState = EAbilityPipelineState.Failed;
                    var failure = CreatePipelineFailureException(context);
                    Events?.OnPipelineInterrupt?.Invoke(context, true);
                    Events?.OnPipelineError?.Invoke(context, null);
                    Events?.OnPipelineFailed?.Invoke(context, failure);
                    return new PipelineExecutionResult(EAbilityPipelineState.Failed, lastPhaseId: context.CurrentPhaseId, exception: failure);
                }

                var phase = _phases[i];
                if (!phase.ShouldExecute(context)) continue;

                try
                {
                    context.CurrentPhaseId = phase.PhaseId;
                    Events?.OnPhaseStart?.Invoke(phase, context);

                    phase.Execute(context);

                    if (!phase.IsComplete)
                    {
                        context.PipelineState = EAbilityPipelineState.Failed;
                        var failure = new InvalidOperationException($"Instant phase did not complete synchronously: {phase.GetType().Name} (phaseId={phase.PhaseId})");
                        Events?.OnPhaseError?.Invoke(phase, context, failure);
                        Events?.OnPipelineError?.Invoke(context, failure);
                        Events?.OnPipelineFailed?.Invoke(context, failure);
                        return new PipelineExecutionResult(EAbilityPipelineState.Failed, lastPhaseId: phase.PhaseId, exception: failure);
                    }

                    Events?.OnPhaseComplete?.Invoke(phase, context);
                }
                catch (Exception ex)
                {
                    Exception failure = ex;
                    try { phase.HandleError(context, ex); }
                    catch (Exception handlerException)
                    {
                        failure = new AggregateException(
                            "The phase and its error handler both failed.",
                            ex,
                            handlerException);
                    }

                    context.PipelineState = EAbilityPipelineState.Failed;
                    Events?.OnPhaseError?.Invoke(phase, context, failure);
                    Events?.OnPipelineError?.Invoke(context, failure);
                    Events?.OnPipelineFailed?.Invoke(context, failure);
                    return new PipelineExecutionResult(EAbilityPipelineState.Failed, lastPhaseId: phase.PhaseId, exception: failure);
                }
            }

            context.PipelineState = EAbilityPipelineState.Completed;
            Events?.OnPipelineComplete?.Invoke(context);
            return new PipelineExecutionResult(EAbilityPipelineState.Completed, lastPhaseId: context.CurrentPhaseId);
        }

        private static Exception CreatePipelineFailureException(TCtx context)
        {
            if (context != null && context.TryGetData<string>("FailReason", out var failReason) && !string.IsNullOrEmpty(failReason))
            {
                return new InvalidOperationException(failReason);
            }

            var phaseId = context != null ? context.CurrentPhaseId.ToString() : string.Empty;
            return new InvalidOperationException($"Pipeline failed without exception (state={EAbilityPipelineState.Failed}, phaseId={phaseId}).");
        }
    }
}
