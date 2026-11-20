using System;
using System.Collections.Generic;

namespace AbilityKit.Pipeline
{
    /// <summary>
    /// 捕获及恢复执行中管线的通用进度，不拥有配置、上下文或历史。
    /// 节点类型由调用方选择，兼容层可复用自己的值载荷而不复制整棵树。
    /// </summary>
    public static class PipelineExecutionProgress<TCtx, TNode>
        where TCtx : IAbilityPipelineContext
        where TNode : PipelinePhaseProgress<TNode>, new()
    {
        /// <summary>
        /// 捕获独立值。自定义捕获器应返回独立节点，返回空表示只保存内置字段。
        /// 继承阶段基类只覆盖通用字段，不证明任意业务字段可以恢复。
        /// </summary>
        public static PipelineRunProgress<TNode> Capture(IAbilityPipelineRun<TCtx> run,
            IAbilityPipelineConfig config, Func<IAbilityPipelinePhase<TCtx>, TNode?>? captureCustom = null)
        {
            if (run == null || config == null || run.State != EAbilityPipelineState.Executing)
                throw new InvalidOperationException("Only an executing pipeline run can be captured.");
            var phases = new TNode[run.Phases.Count];
            for (int index = 0; index < phases.Length; index++) phases[index] = CapturePhase(run.Phases[index], captureCustom);
            return new PipelineRunProgress<TNode>(config.ConfigId, config.ConfigName ?? string.Empty,
                run.CurrentPhaseIndex, run.HasCurrentPhase, run.IsPaused, run.IsCancelled, phases);
        }

        /// <summary>
        /// 按配置和有序拓扑只读验证状态。自定义校验返回真表示节点业务载荷由调用方校验。
        /// </summary>
        public static void Validate(IAbilityPipelineConfig config,
            IReadOnlyList<IAbilityPipelinePhase<TCtx>> phases, in PipelineRunProgress<TNode> state,
            Func<IAbilityPipelinePhase<TCtx>, TNode, bool>? validateCustom = null)
        {
            if (config == null || phases == null || state.ConfigId != config.ConfigId ||
                state.ConfigName != (config.ConfigName ?? string.Empty) || state.Phases == null ||
                state.Phases.Length != phases.Count || state.CurrentPhaseIndex < 0 || state.CurrentPhaseIndex >= phases.Count)
                throw new InvalidOperationException("Pipeline definition does not match its saved state.");
            for (int index = 0; index < phases.Count; index++) ValidatePhase(phases[index], state.Phases[index], validateCustom);
        }

        /// <summary>
        /// 全树验证后静默写回进度。准备回调在验证后、写入前执行，可解除业务绑定。
        /// 不调用正常阶段或管线生命周期；自定义写入失败不提供事务补偿。
        /// </summary>
        public static void Restore(IAbilityPipelineRun<TCtx> run, IAbilityPipelineConfig config,
            in PipelineRunProgress<TNode> state,
            Func<IAbilityPipelinePhase<TCtx>, TNode, bool>? validateCustom = null,
            Func<IAbilityPipelinePhase<TCtx>, TNode, bool>? restoreCustom = null,
            Action<IAbilityPipelineRun<TCtx>>? prepare = null)
        {
            if (run == null || run.State != EAbilityPipelineState.Executing)
                throw new InvalidOperationException("Only an executing pipeline run can be restored.");
            Validate(config, run.Phases, state, validateCustom);
            prepare?.Invoke(run);
            for (int index = 0; index < run.Phases.Count; index++) RestorePhase(run.Phases[index], state.Phases[index], restoreCustom);
            run.SetExecutionPosition(state.CurrentPhaseIndex, state.HasCurrentPhase, state.IsPaused, state.IsCancelled);
        }

        private static TNode CapturePhase(IAbilityPipelinePhase<TCtx> phase,
            Func<IAbilityPipelinePhase<TCtx>, TNode?>? captureCustom)
        {
            var state = captureCustom?.Invoke(phase);
            if (state == null && (phase is AbilityPipelinePhaseBase<TCtx> || phase is AbilityCompositePhase<TCtx>))
                state = new TNode
                {
                    PhaseId = phase.PhaseId.Value,
                    PhaseType = phase.GetType().FullName ?? phase.GetType().Name,
                    IsComplete = phase.IsComplete
                };
            else if (state == null)
                throw new InvalidOperationException($"Pipeline phase '{phase.PhaseId}' has no state accessors.");

            if (phase is AbilityRepeatPhase<TCtx> repeat)
                state.Integers = new[] { repeat.CurrentRepeatIndex, repeat.IsWaitingInterval ? 1 : 0,
                    repeat.HasCurrentSubPhase ? 1 : 0 };
            else if (phase is AbilityConditionalPhase<TCtx> conditional)
            {
                state.Integers = new int[conditional.BranchResults.Count + 2];
                state.Integers[0] = conditional.CurrentSubPhaseIndex;
                state.Integers[1] = conditional.CurrentBranchIndex;
                for (int index = 0; index < conditional.BranchResults.Count; index++)
                    state.Integers[index + 2] = (int)conditional.BranchResults[index];
            }
            else if (phase is AbilityParallelPhase<TCtx> parallel)
                state.Integers = CaptureActive(parallel.CurrentSubPhaseIndex, parallel.ActivePhaseIndices);
            else if (phase is AbilityRacePhase<TCtx> race)
                state.Integers = CaptureActive(race.CurrentSubPhaseIndex, race.ActivePhaseIndices);
            else if (phase is AbilityCompositePhase<TCtx> composite)
                state.Integers = new[] { composite.CurrentSubPhaseIndex };

            if (phase is AbilityDurationalPhaseBase<TCtx> duration)
                state.Numbers = phase is AbilityRepeatPhase<TCtx> timedRepeat
                    ? new[] { (double)duration.ElapsedTime, timedRepeat.IntervalTimer }
                    : new[] { (double)duration.ElapsedTime };

            var children = phase.SubPhases;
            state.Children = new TNode[children.Count];
            for (int index = 0; index < children.Count; index++) state.Children[index] = CapturePhase(children[index], captureCustom);
            return state;
        }

        private static int[] CaptureActive(int cursor, IReadOnlyList<int> active)
        {
            var result = new int[active.Count + 1];
            result[0] = cursor;
            for (int index = 0; index < active.Count; index++) result[index + 1] = active[index];
            return result;
        }

        private static void ValidatePhase(IAbilityPipelinePhase<TCtx> phase,
            TNode state, Func<IAbilityPipelinePhase<TCtx>, TNode, bool>? handleCustom)
        {
            if (state == null || state.PhaseId != phase.PhaseId.Value ||
                state.PhaseType != (phase.GetType().FullName ?? phase.GetType().Name) ||
                state.Children == null || state.Children.Length != phase.SubPhases.Count ||
                state.Integers == null || state.Longs == null || state.Numbers == null)
                throw new InvalidOperationException("Pipeline phase definition changed.");
            if (handleCustom?.Invoke(phase, state) != true)
            {
                Require(phase is AbilityPipelinePhaseBase<TCtx> ||
                    phase is AbilityCompositePhase<TCtx>);
                Require(state.Longs.Length == 0);
                if (phase is AbilityRepeatPhase<TCtx> repeat)
                {
                    Require(state.Integers.Length == 3 && state.Integers[0] >= 0 &&
                        state.Integers[1] is >= 0 and <= 1 && state.Integers[2] is >= 0 and <= 1 &&
                        (state.Integers[2] == 0 || repeat.SubPhases.Count == 1));
                }
                else if (phase is AbilityConditionalPhase<TCtx> conditional)
                {
                    Require(state.Integers.Length == conditional.Branches.Count + 2 &&
                        state.Integers[0] >= 0 && state.Integers[0] <= phase.SubPhases.Count &&
                        state.Integers[1] >= -1 && state.Integers[1] < conditional.Branches.Count);
                    for (int index = 2; index < state.Integers.Length; index++)
                        Require(state.Integers[index] >= (int)EPipelineDebugConditionResult.Unknown &&
                            state.Integers[index] <= (int)EPipelineDebugConditionResult.Rejected);
                }
                else if (phase is AbilityParallelPhase<TCtx> ||
                         phase is AbilityRacePhase<TCtx>)
                {
                    Require(state.Integers.Length >= 1 && state.Integers[0] >= 0 &&
                        state.Integers[0] <= phase.SubPhases.Count);
                    var active = new HashSet<int>();
                    for (int index = 1; index < state.Integers.Length; index++)
                        Require(state.Integers[index] >= 0 && state.Integers[index] < phase.SubPhases.Count &&
                            active.Add(state.Integers[index]));
                }
                else if (phase is AbilityCompositePhase<TCtx>)
                    Require(state.Integers.Length == 1 && state.Integers[0] >= 0 &&
                        state.Integers[0] <= phase.SubPhases.Count);
                else
                    Require(state.Integers.Length == 0);

                if (phase is AbilityDurationalPhaseBase<TCtx>)
                {
                    Require(state.Numbers.Length == (phase is AbilityRepeatPhase<TCtx> ? 2 : 1));
                    for (int index = 0; index < state.Numbers.Length; index++)
                        Require(!double.IsNaN(state.Numbers[index]) && !double.IsInfinity(state.Numbers[index]) &&
                            state.Numbers[index] >= 0 && state.Numbers[index] <= float.MaxValue);
                }
                else
                    Require(state.Numbers.Length == 0);
            }
            for (int index = 0; index < state.Children.Length; index++)
                ValidatePhase(phase.SubPhases[index], state.Children[index], handleCustom);
        }

        private static void RestorePhase(IAbilityPipelinePhase<TCtx> phase,
            TNode state, Func<IAbilityPipelinePhase<TCtx>, TNode, bool>? handleCustom)
        {
            for (int index = 0; index < state.Children.Length; index++) RestorePhase(phase.SubPhases[index], state.Children[index], handleCustom);
            if (handleCustom?.Invoke(phase, state) != true)
            {
                if (phase is AbilityCompositePhase<TCtx> compositePhase)
                    compositePhase.SetComplete(state.IsComplete);
                else if (phase is AbilityPipelinePhaseBase<TCtx> simple)
                    simple.SetComplete(state.IsComplete);
                else
                    throw new InvalidOperationException($"Pipeline phase '{phase.PhaseId}' has no state accessors.");
            }

            if (phase is AbilityRepeatPhase<TCtx> repeat)
            {
                Require(state.Integers.Length == 3 && state.Numbers.Length == 2 &&
                    state.Integers[1] is >= 0 and <= 1 && state.Integers[2] is >= 0 and <= 1);
                repeat.SetRepeatProgress(state.Integers[0], (float)state.Numbers[1],
                    state.Integers[1] == 1, state.Integers[2] == 1);
            }
            else if (phase is AbilityConditionalPhase<TCtx> conditional)
            {
                Require(state.Integers.Length == conditional.Branches.Count + 2);
                conditional.CurrentSubPhaseIndex = state.Integers[0];
                var results = new EPipelineDebugConditionResult[conditional.Branches.Count];
                for (int index = 0; index < results.Length; index++)
                {
                    Require(state.Integers[index + 2] >= (int)EPipelineDebugConditionResult.Unknown &&
                        state.Integers[index + 2] <= (int)EPipelineDebugConditionResult.Rejected);
                    results[index] = (EPipelineDebugConditionResult)state.Integers[index + 2];
                }
                conditional.SetBranchProgress(state.Integers[1], results);
            }
            else if (phase is AbilityParallelPhase<TCtx> parallel)
            {
                Require(state.Integers.Length >= 1);
                parallel.CurrentSubPhaseIndex = state.Integers[0];
                parallel.SetActivePhases(Tail(state.Integers));
            }
            else if (phase is AbilityRacePhase<TCtx> race)
            {
                Require(state.Integers.Length >= 1);
                race.CurrentSubPhaseIndex = state.Integers[0];
                race.SetActivePhases(Tail(state.Integers));
            }
            else if (phase is AbilityCompositePhase<TCtx> composite)
            {
                Require(state.Integers.Length == 1);
                composite.CurrentSubPhaseIndex = state.Integers[0];
            }

            if (phase is AbilityDurationalPhaseBase<TCtx> duration)
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
            if (!condition) throw new InvalidOperationException("Invalid pipeline phase state.");
        }

    }
}
