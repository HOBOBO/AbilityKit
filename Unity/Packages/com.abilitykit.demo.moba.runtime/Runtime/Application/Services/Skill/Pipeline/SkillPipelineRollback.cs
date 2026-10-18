using System;
using System.Collections.Generic;
using AbilityKit.Pipeline;

namespace AbilityKit.Demo.Moba.Services
{
    internal static class SkillPipelineRollback
    {
        public static PipelineRunRollbackState Capture(IAbilityPipelineRun<SkillPipelineContext> run,
            IAbilityPipelineConfig config)
        {
            if (run == null || config == null || run.State != EAbilityPipelineState.Executing)
                throw new InvalidOperationException("Only an executing skill run can be captured.");
            var phases = new PipelinePhaseRollbackState[run.Phases.Count];
            for (int i = 0; i < phases.Length; i++) phases[i] = CapturePhase(run.Phases[i]);
            return new PipelineRunRollbackState
            {
                ConfigId = config.ConfigId,
                ConfigName = config.ConfigName ?? string.Empty,
                CurrentPhaseIndex = run.CurrentPhaseIndex,
                HasCurrentPhase = run.HasCurrentPhase,
                IsPaused = run.IsPaused,
                IsCancelled = run.IsCancelled,
                Phases = phases
            };
        }

        public static IAbilityPipelineRun<SkillPipelineContext> Restore(SkillCastPipeline pipeline,
            IAbilityPipelineConfig config, SkillPipelineContext context, PipelineRunRollbackState state)
        {
            var run = pipeline.AttachRun(config, context, attached => Apply(attached, config, state));
            try
            {
                for (int i = 0; i < run.Phases.Count; i++) Rebind(run.Phases[i], context);
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
            ValidateDefinition(config, run.Phases, state);
            for (int i = 0; i < run.Phases.Count; i++) DetachPhase(run.Phases[i]);
            Apply(run, config, state);
            for (int i = 0; i < run.Phases.Count; i++) Rebind(run.Phases[i], run.Context);
        }

        public static void Detach(IAbilityPipelineRun<SkillPipelineContext> run)
        {
            if (run == null || run.State != EAbilityPipelineState.Executing) return;
            for (int i = 0; i < run.Phases.Count; i++) DetachPhase(run.Phases[i]);
            run.Release();
        }

        public static void ValidateDefinition(IAbilityPipelineConfig config,
            IReadOnlyList<IAbilityPipelinePhase<SkillPipelineContext>> phases, PipelineRunRollbackState state)
        {
            if (config == null || phases == null || state == null ||
                state.Version != PipelineRunRollbackState.CurrentVersion ||
                state.ConfigId != config.ConfigId || state.ConfigName != (config.ConfigName ?? string.Empty) ||
                state.Phases == null || state.Phases.Length != phases.Count ||
                state.CurrentPhaseIndex < 0 || state.CurrentPhaseIndex >= phases.Count)
                throw new InvalidOperationException("Skill run definition does not match its saved state.");
            for (int i = 0; i < phases.Count; i++) ValidatePhase(phases[i], state.Phases[i]);
        }

        private static void Apply(IAbilityPipelineRun<SkillPipelineContext> run,
            IAbilityPipelineConfig config, PipelineRunRollbackState state)
        {
            ValidateDefinition(config, run.Phases, state);
            for (int i = 0; i < run.Phases.Count; i++) RestorePhase(run.Phases[i], state.Phases[i]);
            run.SetExecutionPosition(state.CurrentPhaseIndex, state.HasCurrentPhase,
                state.IsPaused, state.IsCancelled);
        }

        private static PipelinePhaseRollbackState CapturePhase(IAbilityPipelinePhase<SkillPipelineContext> phase)
        {
            PipelinePhaseRollbackState state;
            if (phase is IAbilityPhaseRollbackState skillPhase)
                state = skillPhase.CaptureRollbackState();
            else if (phase is AbilityPipelinePhaseBase<SkillPipelineContext> ||
                     phase is AbilityCompositePhase<SkillPipelineContext>)
                state = new PipelinePhaseRollbackState
                {
                    PhaseId = phase.PhaseId.Value,
                    PhaseType = phase.GetType().FullName ?? phase.GetType().Name,
                    IsComplete = phase.IsComplete
                };
            else
                throw new InvalidOperationException($"Skill phase '{phase.PhaseId}' has no state accessors.");

            if (phase is AbilityRepeatPhase<SkillPipelineContext> repeat)
                state.Integers = new[] { repeat.CurrentRepeatIndex, repeat.IsWaitingInterval ? 1 : 0,
                    repeat.HasCurrentSubPhase ? 1 : 0 };
            else if (phase is AbilityConditionalPhase<SkillPipelineContext> conditional)
            {
                state.Integers = new int[conditional.BranchResults.Count + 2];
                state.Integers[0] = conditional.CurrentSubPhaseIndex;
                state.Integers[1] = conditional.CurrentBranchIndex;
                for (int i = 0; i < conditional.BranchResults.Count; i++)
                    state.Integers[i + 2] = (int)conditional.BranchResults[i];
            }
            else if (phase is AbilityParallelPhase<SkillPipelineContext> parallel)
                state.Integers = CaptureActive(parallel.CurrentSubPhaseIndex, parallel.ActivePhaseIndices);
            else if (phase is AbilityRacePhase<SkillPipelineContext> race)
                state.Integers = CaptureActive(race.CurrentSubPhaseIndex, race.ActivePhaseIndices);
            else if (phase is AbilityCompositePhase<SkillPipelineContext> composite)
                state.Integers = new[] { composite.CurrentSubPhaseIndex };

            if (phase is AbilityDurationalPhaseBase<SkillPipelineContext> duration)
                state.Numbers = phase is AbilityRepeatPhase<SkillPipelineContext> timedRepeat
                    ? new[] { (double)duration.ElapsedTime, timedRepeat.IntervalTimer }
                    : new[] { (double)duration.ElapsedTime };

            var children = phase.SubPhases;
            state.Children = new PipelinePhaseRollbackState[children.Count];
            for (int i = 0; i < children.Count; i++) state.Children[i] = CapturePhase(children[i]);
            return state;
        }

        private static int[] CaptureActive(int cursor, IReadOnlyList<int> active)
        {
            var result = new int[active.Count + 1];
            result[0] = cursor;
            for (int i = 0; i < active.Count; i++) result[i + 1] = active[i];
            return result;
        }

        private static void ValidatePhase(IAbilityPipelinePhase<SkillPipelineContext> phase,
            PipelinePhaseRollbackState state)
        {
            if (state == null || state.PhaseId != phase.PhaseId.Value ||
                state.PhaseType != (phase.GetType().FullName ?? phase.GetType().Name) ||
                state.Children == null || state.Children.Length != phase.SubPhases.Count ||
                state.Integers == null || state.Longs == null || state.Numbers == null)
                throw new InvalidOperationException("Skill phase definition changed.");
            if (phase is IAbilityPhaseRollbackState skillPhase)
                skillPhase.ValidateRollbackState(state);
            else
            {
                Require(phase is AbilityPipelinePhaseBase<SkillPipelineContext> ||
                    phase is AbilityCompositePhase<SkillPipelineContext>);
                Require(state.Longs.Length == 0);
                if (phase is AbilityRepeatPhase<SkillPipelineContext> repeat)
                {
                    Require(state.Integers.Length == 3 && state.Integers[0] >= 0 &&
                        state.Integers[1] is >= 0 and <= 1 && state.Integers[2] is >= 0 and <= 1 &&
                        (state.Integers[2] == 0 || repeat.SubPhases.Count == 1));
                }
                else if (phase is AbilityConditionalPhase<SkillPipelineContext> conditional)
                {
                    Require(state.Integers.Length == conditional.Branches.Count + 2 &&
                        state.Integers[0] >= 0 && state.Integers[0] <= phase.SubPhases.Count &&
                        state.Integers[1] >= -1 && state.Integers[1] < conditional.Branches.Count);
                    for (int i = 2; i < state.Integers.Length; i++)
                        Require(state.Integers[i] >= (int)EPipelineDebugConditionResult.Unknown &&
                            state.Integers[i] <= (int)EPipelineDebugConditionResult.Rejected);
                }
                else if (phase is AbilityParallelPhase<SkillPipelineContext> ||
                         phase is AbilityRacePhase<SkillPipelineContext>)
                {
                    Require(state.Integers.Length >= 1 && state.Integers[0] >= 0 &&
                        state.Integers[0] <= phase.SubPhases.Count);
                    var active = new HashSet<int>();
                    for (int i = 1; i < state.Integers.Length; i++)
                        Require(state.Integers[i] >= 0 && state.Integers[i] < phase.SubPhases.Count &&
                            active.Add(state.Integers[i]));
                }
                else if (phase is AbilityCompositePhase<SkillPipelineContext>)
                    Require(state.Integers.Length == 1 && state.Integers[0] >= 0 &&
                        state.Integers[0] <= phase.SubPhases.Count);
                else
                    Require(state.Integers.Length == 0);

                if (phase is AbilityDurationalPhaseBase<SkillPipelineContext>)
                {
                    Require(state.Numbers.Length == (phase is AbilityRepeatPhase<SkillPipelineContext> ? 2 : 1));
                    for (int i = 0; i < state.Numbers.Length; i++)
                        Require(!double.IsNaN(state.Numbers[i]) && !double.IsInfinity(state.Numbers[i]) &&
                            state.Numbers[i] >= 0 && state.Numbers[i] <= float.MaxValue);
                }
                else
                    Require(state.Numbers.Length == 0);
            }
            for (int i = 0; i < state.Children.Length; i++)
                ValidatePhase(phase.SubPhases[i], state.Children[i]);
        }

        private static void RestorePhase(IAbilityPipelinePhase<SkillPipelineContext> phase,
            PipelinePhaseRollbackState state)
        {
            for (int i = 0; i < state.Children.Length; i++) RestorePhase(phase.SubPhases[i], state.Children[i]);
            if (phase is IAbilityPhaseRollbackState skillPhase)
                skillPhase.RestoreRollbackState(state);
            else if (phase is AbilityCompositePhase<SkillPipelineContext> composite)
                composite.SetComplete(state.IsComplete);
            else if (phase is AbilityPipelinePhaseBase<SkillPipelineContext> simple)
                simple.SetComplete(state.IsComplete);
            else
                throw new InvalidOperationException($"Skill phase '{phase.PhaseId}' has no state accessors.");

            if (phase is AbilityRepeatPhase<SkillPipelineContext> repeat)
            {
                Require(state.Integers.Length == 3 && state.Numbers.Length == 2 &&
                    state.Integers[1] is >= 0 and <= 1 && state.Integers[2] is >= 0 and <= 1);
                repeat.SetRepeatProgress(state.Integers[0], (float)state.Numbers[1],
                    state.Integers[1] == 1, state.Integers[2] == 1);
            }
            else if (phase is AbilityConditionalPhase<SkillPipelineContext> conditional)
            {
                Require(state.Integers.Length == conditional.Branches.Count + 2);
                conditional.CurrentSubPhaseIndex = state.Integers[0];
                var results = new EPipelineDebugConditionResult[conditional.Branches.Count];
                for (int i = 0; i < results.Length; i++)
                {
                    Require(state.Integers[i + 2] >= (int)EPipelineDebugConditionResult.Unknown &&
                        state.Integers[i + 2] <= (int)EPipelineDebugConditionResult.Rejected);
                    results[i] = (EPipelineDebugConditionResult)state.Integers[i + 2];
                }
                conditional.SetBranchProgress(state.Integers[1], results);
            }
            else if (phase is AbilityParallelPhase<SkillPipelineContext> parallel)
            {
                Require(state.Integers.Length >= 1);
                parallel.CurrentSubPhaseIndex = state.Integers[0];
                parallel.SetActivePhases(Tail(state.Integers));
            }
            else if (phase is AbilityRacePhase<SkillPipelineContext> race)
            {
                Require(state.Integers.Length >= 1);
                race.CurrentSubPhaseIndex = state.Integers[0];
                race.SetActivePhases(Tail(state.Integers));
            }
            else if (phase is AbilityCompositePhase<SkillPipelineContext> composite)
            {
                Require(state.Integers.Length == 1);
                composite.CurrentSubPhaseIndex = state.Integers[0];
            }

            if (phase is AbilityDurationalPhaseBase<SkillPipelineContext> duration)
            {
                Require(state.Numbers.Length >= 1 && state.Numbers[0] <= float.MaxValue);
                duration.ElapsedTime = (float)state.Numbers[0];
            }
        }

        private static int[] Tail(int[] values)
        {
            var tail = new int[values.Length - 1];
            Array.Copy(values, 1, tail, 0, tail.Length);
            return tail;
        }

        private static void Require(bool condition)
        {
            if (!condition) throw new InvalidOperationException("Invalid skill phase state.");
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
