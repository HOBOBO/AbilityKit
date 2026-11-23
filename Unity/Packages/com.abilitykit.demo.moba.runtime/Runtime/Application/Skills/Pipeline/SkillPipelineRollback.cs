using System;
using System.Collections.Generic;
using AbilityKit.Pipeline;
using ExecutionProgress = AbilityKit.Pipeline.PipelineExecutionProgress<AbilityKit.Demo.Moba.Services.SkillPipelineContext, AbilityKit.Demo.Moba.Services.PipelinePhaseRollbackState>;

namespace AbilityKit.Demo.Moba.Services
{
    internal static class SkillPipelineRollback
    {
        public static PipelineRunRollbackState Capture(IAbilityPipelineRun<SkillPipelineContext> run,
            IAbilityPipelineConfig config)
        {
            var progress = ExecutionProgress.Capture(run, config, CaptureCustom);
            return new PipelineRunRollbackState
            {
                ConfigId = progress.ConfigId,
                ConfigName = progress.ConfigName,
                CurrentPhaseIndex = progress.CurrentPhaseIndex,
                HasCurrentPhase = progress.HasCurrentPhase,
                IsPaused = progress.IsPaused,
                IsCancelled = progress.IsCancelled,
                Phases = progress.Phases
            };
        }

        public static IAbilityPipelineRun<SkillPipelineContext> Restore(SkillCastPipeline pipeline,
            IAbilityPipelineConfig config, SkillPipelineContext context, PipelineRunRollbackState state)
        {
            var progress = state.ToProgress();
            var run = pipeline.AttachRun(config, context,
                attached => ExecutionProgress.Restore(attached, config, progress, ValidateCustom, RestoreCustom));
            try
            {
                Rebind(run);
                return run;
            }
            catch
            {
                Detach(run);
                throw;
            }
        }

        public static void RestoreInPlace(IAbilityPipelineRun<SkillPipelineContext> run,
            IAbilityPipelineConfig config, PipelineRunRollbackState state)
        {
            ExecutionProgress.Restore(run, config, state.ToProgress(), ValidateCustom, RestoreCustom, DetachPhases);
            Rebind(run);
        }

        public static void Detach(IAbilityPipelineRun<SkillPipelineContext> run)
        {
            if (run == null || run.State != EAbilityPipelineState.Executing) return;
            DetachPhases(run);
            run.Release();
        }

        public static void ValidateDefinition(IAbilityPipelineConfig config,
            IReadOnlyList<IAbilityPipelinePhase<SkillPipelineContext>> phases, PipelineRunRollbackState state)
        {
            if (state == null) throw new InvalidOperationException("Missing skill run state.");
            ExecutionProgress.Validate(config, phases, state.ToProgress(), ValidateCustom);
        }

        private static PipelinePhaseRollbackState CaptureCustom(IAbilityPipelinePhase<SkillPipelineContext> phase) =>
            (phase as IAbilityPhaseRollbackState)?.CaptureRollbackState();

        private static bool ValidateCustom(IAbilityPipelinePhase<SkillPipelineContext> phase,
            PipelinePhaseRollbackState state)
        {
            if (!(phase is IAbilityPhaseRollbackState custom)) return false;
            custom.ValidateRollbackState(state);
            return true;
        }

        private static bool RestoreCustom(IAbilityPipelinePhase<SkillPipelineContext> phase,
            PipelinePhaseRollbackState state)
        {
            if (!(phase is IAbilityPhaseRollbackState custom)) return false;
            custom.RestoreRollbackState(state);
            return true;
        }

        private static void DetachPhases(IAbilityPipelineRun<SkillPipelineContext> run)
        {
            for (int index = 0; index < run.Phases.Count; index++) DetachPhase(run.Phases[index]);
        }

        private static void Rebind(IAbilityPipelineRun<SkillPipelineContext> run)
        {
            for (int index = 0; index < run.Phases.Count; index++) Rebind(run.Phases[index], run.Context);
        }

        private static void DetachPhase(IAbilityPipelinePhase<SkillPipelineContext> phase)
        {
            if (phase is IAbilityPhaseRollbackDetach detach) detach.DetachForRollback();
            foreach (var child in phase.SubPhases) DetachPhase(child);
        }

        private static void Rebind(IAbilityPipelinePhase<SkillPipelineContext> phase,
            SkillPipelineContext context)
        {
            if (phase is IAbilityPhaseRollbackRebind<SkillPipelineContext> rebind)
                rebind.RebindAfterRollback(context);
            foreach (var child in phase.SubPhases) Rebind(child, context);
        }
    }
}
