using System;
using System.Collections.Generic;

namespace AbilityKit.Pipeline
{
    public abstract partial class AbilityPipeline<TCtx>
        where TCtx : IAbilityPipelineContext
    {
        private sealed partial class Run
        {
            /// <inheritdoc />
            public PipelineDebugRunState CaptureDebugState()
            {
                return _terminalDebugState ?? CaptureDebugStateCore();
            }

            private PipelineDebugRunState CaptureDebugStateCore()
            {
                var result = new List<PipelinePhaseDebugState>(_phases.Count * 2);
                for (int i = 0; i < _phases.Count; i++)
                {
                    EPipelineDebugExecutionState state;
                    if (ReferenceEquals(_phases[i], _failedPhase)) state = EPipelineDebugExecutionState.Failed;
                    else if (i < _currentPhaseIndex || State == EAbilityPipelineState.Completed) state = EPipelineDebugExecutionState.Completed;
                    else if (ReferenceEquals(_phases[i], _currentPhase)) state = EPipelineDebugExecutionState.Active;
                    else state = EPipelineDebugExecutionState.Pending;
                    CapturePhaseState(_phases[i], i.ToString(), state, result);
                }
                return new PipelineDebugRunState(result);
            }

            private static void CapturePhaseState(
                IAbilityPipelinePhase<TCtx> phase,
                string nodeKey,
                EPipelineDebugExecutionState state,
                ICollection<PipelinePhaseDebugState> result)
            {
                int selectedChild = GetSelectedChildIndex(phase);
                IReadOnlyList<EPipelineDebugConditionResult> conditions = Array.Empty<EPipelineDebugConditionResult>();
                if (phase is AbilityConditionalPhase<TCtx> conditional)
                {
                    var copy = new EPipelineDebugConditionResult[conditional.DebugBranchResults.Count];
                    for (int i = 0; i < copy.Length; i++) copy[i] = conditional.DebugBranchResults[i];
                    conditions = copy;
                }

                result.Add(new PipelinePhaseDebugState(nodeKey, state, selectedChild, conditions));

                var children = phase.SubPhases;
                if (children == null) return;
                for (int i = 0; i < children.Count; i++)
                {
                    var child = children[i];
                    EPipelineDebugExecutionState childState;
                    if (state == EPipelineDebugExecutionState.Pending)
                    {
                        childState = EPipelineDebugExecutionState.Pending;
                    }
                    else if (state == EPipelineDebugExecutionState.Skipped)
                    {
                        childState = EPipelineDebugExecutionState.Skipped;
                    }
                    else if (child.IsComplete)
                    {
                        childState = EPipelineDebugExecutionState.Completed;
                    }
                    else if (IsChildActive(phase, i))
                    {
                        childState = state == EPipelineDebugExecutionState.Failed
                            ? EPipelineDebugExecutionState.Failed
                            : EPipelineDebugExecutionState.Active;
                    }
                    else if (state == EPipelineDebugExecutionState.Completed
                             || IsRejectedConditionalChild(phase, i))
                    {
                        childState = EPipelineDebugExecutionState.Skipped;
                    }
                    else
                    {
                        childState = EPipelineDebugExecutionState.Pending;
                    }

                    CapturePhaseState(child, nodeKey + "/" + i, childState, result);
                }
            }

            private static int GetSelectedChildIndex(IAbilityPipelinePhase<TCtx> phase)
            {
                if (phase is AbilityConditionalPhase<TCtx> conditional) return conditional.DebugCurrentBranchIndex;
                if (phase is AbilityCompositePhase<TCtx> composite) return composite.DebugCurrentSubPhaseIndex;
                return -1;
            }

            private static bool IsChildActive(IAbilityPipelinePhase<TCtx> phase, int childIndex)
            {
                if (phase is AbilityConditionalPhase<TCtx> conditional)
                {
                    return conditional.DebugCurrentBranchIndex == childIndex;
                }
                if (phase is AbilityParallelPhase<TCtx> parallel)
                {
                    return parallel.DebugIsChildActive(childIndex);
                }
                if (phase is AbilityRacePhase<TCtx> race)
                {
                    return race.DebugIsChildActive(childIndex);
                }
                return phase is AbilityCompositePhase<TCtx> composite
                    && composite.DebugCurrentSubPhaseIndex == childIndex;
            }

            private static bool IsRejectedConditionalChild(IAbilityPipelinePhase<TCtx> phase, int childIndex)
            {
                return phase is AbilityConditionalPhase<TCtx> conditional
                    && childIndex < conditional.DebugBranchResults.Count
                    && conditional.DebugBranchResults[childIndex] == EPipelineDebugConditionResult.Rejected;
            }
        }
    }
}
