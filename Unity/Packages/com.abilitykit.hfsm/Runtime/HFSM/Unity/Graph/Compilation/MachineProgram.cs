using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using AbilityKit.HFSM.Graph.Conditions;

namespace AbilityKit.HFSM.Graph.Compilation
{

    public sealed class MachineProgram : GraphNodeProgram
    {
        internal MachineProgram(
            string sourceNodeId,
            string runtimeName,
            string parentMachineId,
            string defaultChildNodeId,
            bool rememberLastState,
            IReadOnlyList<string> childNodeIds,
            IReadOnlyList<TransitionProgram> transitions)
            : base(sourceNodeId, runtimeName)
        {
            ParentMachineId = parentMachineId ?? string.Empty;
            DefaultChildNodeId = defaultChildNodeId ?? string.Empty;
            RememberLastState = rememberLastState;
            ChildNodeIds = childNodeIds == null
                ? Array.Empty<string>()
                : new ReadOnlyCollection<string>(new List<string>(childNodeIds));
            Transitions = transitions == null
                ? Array.Empty<TransitionProgram>()
                : new ReadOnlyCollection<TransitionProgram>(new List<TransitionProgram>(transitions));
        }

        public string ParentMachineId { get; }
        public string DefaultChildNodeId { get; }
        public bool RememberLastState { get; }
        public IReadOnlyList<string> ChildNodeIds { get; }
        public IReadOnlyList<TransitionProgram> Transitions { get; }
    }
}
