using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using AbilityKit.HFSM.Graph.Conditions;

namespace AbilityKit.HFSM.Graph.Compilation
{

    public sealed class TransitionProgram
    {
        internal TransitionProgram(
            string sourceEdgeId,
            string ownerMachineId,
            string sourceNodeId,
            string targetNodeId,
            int priority,
            bool isFromAnyState,
            bool isExitTransition,
            bool forceInstantly,
            string triggerId,
            string actionKey,
            bool useAndLogic,
            IReadOnlyList<TransitionCondition> conditions)
        {
            SourceEdgeId = sourceEdgeId;
            OwnerMachineId = ownerMachineId;
            SourceNodeId = sourceNodeId;
            TargetNodeId = targetNodeId;
            Priority = priority;
            IsFromAnyState = isFromAnyState;
            IsExitTransition = isExitTransition;
            ForceInstantly = forceInstantly;
            TriggerId = triggerId ?? string.Empty;
            ActionKey = actionKey ?? string.Empty;
            UseAndLogic = useAndLogic;
            Conditions = conditions == null
                ? Array.Empty<TransitionCondition>()
                : new ReadOnlyCollection<TransitionCondition>(new List<TransitionCondition>(conditions));
        }

        public string SourceEdgeId { get; }
        public string OwnerMachineId { get; }
        public string SourceNodeId { get; }
        public string TargetNodeId { get; }
        public int Priority { get; }
        public bool IsFromAnyState { get; }
        public bool IsExitTransition { get; }
        public bool ForceInstantly { get; }
        public string TriggerId { get; }
        public string ActionKey { get; }
        public bool UseAndLogic { get; }
        public IReadOnlyList<TransitionCondition> Conditions { get; }
    }
}
