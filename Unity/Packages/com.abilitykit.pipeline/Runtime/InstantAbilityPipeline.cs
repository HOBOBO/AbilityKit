using System;
using System.Collections.Generic;

namespace AbilityKit.Pipeline
{
    /// <summary>
    /// Instant-only pipeline.
    /// - Only accepts instant phases (compile-time enforced).
    /// - Runs synchronously to completion (no external Tick required).
    /// </summary>
    public sealed class InstantAbilityPipeline<TCtx>
        where TCtx : IAbilityPipelineContext
    {
        private readonly List<IAbilityInstantPhase<TCtx>> _phases = new List<IAbilityInstantPhase<TCtx>>(8);

        public AbilityPipelineEvents<TCtx> Events { get; } = new AbilityPipelineEvents<TCtx>();

        public void AddPhase(IAbilityInstantPhase<TCtx> phase)
        {
            if (phase == null) throw new ArgumentNullException(nameof(phase));
            _phases.Add(phase);
        }

        public void InsertPhase(int index, IAbilityInstantPhase<TCtx> phase)
        {
            if (phase == null) throw new ArgumentNullException(nameof(phase));
            _phases.Insert(index, phase);
        }

        public void Reset()
        {
            for (int i = 0; i < _phases.Count; i++)
            {
                _phases[i].Reset();
            }
        }

        public RunResult RunToCompletion(IAbilityPipelineConfig config, TCtx context)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (context == null) throw new ArgumentNullException(nameof(context));

            // Reset all phases before starting.
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
                    Events?.OnPipelineInterrupt?.Invoke(context, true);
                    Events?.OnPipelineError?.Invoke(context, null);
                    return new RunResult(EAbilityPipelineState.Failed, lastPhaseId: context.CurrentPhaseId);
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
                        // This should never happen for instant phases.
                        context.PipelineState = EAbilityPipelineState.Failed;
                        Events?.OnPhaseError?.Invoke(phase, context,
                            new InvalidOperationException($"Instant phase did not complete synchronously: {phase.GetType().Name} (phaseId={phase.PhaseId})"));
                        return new RunResult(EAbilityPipelineState.Failed, lastPhaseId: phase.PhaseId);
                    }

                    Events?.OnPhaseComplete?.Invoke(phase, context);
                }
                catch (Exception ex)
                {
                    try { phase.HandleError(context, ex); }
                    catch { }

                    context.PipelineState = EAbilityPipelineState.Failed;
                    Events?.OnPhaseError?.Invoke(phase, context, ex);
                    return new RunResult(EAbilityPipelineState.Failed, lastPhaseId: phase.PhaseId, exception: ex);
                }
            }

            context.PipelineState = EAbilityPipelineState.Completed;
            Events?.OnPipelineComplete?.Invoke(context);
            return new RunResult(EAbilityPipelineState.Completed, lastPhaseId: context.CurrentPhaseId);
        }

        public readonly struct RunResult
        {
            public readonly EAbilityPipelineState State;
            public readonly AbilityPipelinePhaseId LastPhaseId;
            public readonly Exception Exception;

            public RunResult(EAbilityPipelineState state, AbilityPipelinePhaseId lastPhaseId, Exception exception = null)
            {
                State = state;
                LastPhaseId = lastPhaseId;
                Exception = exception;
            }
        }
    }
}
