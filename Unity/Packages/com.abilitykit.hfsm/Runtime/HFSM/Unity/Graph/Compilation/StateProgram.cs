using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using AbilityKit.HFSM.Graph.Conditions;

namespace AbilityKit.HFSM.Graph.Compilation
{

    public sealed class StateProgram : GraphNodeProgram
    {
        internal StateProgram(string sourceNodeId, string runtimeName, StateNode template)
            : base(sourceNodeId, runtimeName)
        {
            Template = template;
            var behaviorIds = new string[template.BehaviorItems.Count];
            for (var index = 0; index < behaviorIds.Length; index++)
                behaviorIds[index] = template.BehaviorItems[index].id;
            BehaviorIds = Array.AsReadOnly(behaviorIds);
        }

        internal StateNode Template { get; }
        public bool NeedsExitTime => Template.NeedsExitTime;
        public bool IsGhostState => Template.IsGhostState;
        public string BehaviorKey => Template.NextBehaviorKey;
        public IReadOnlyList<string> BehaviorIds { get; }
    }
}
