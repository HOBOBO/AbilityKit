using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using AbilityKit.HFSM.Graph.Conditions;

namespace AbilityKit.HFSM.Graph.Compilation
{

    public abstract class GraphNodeProgram
    {
        protected GraphNodeProgram(string sourceNodeId, string runtimeName)
        {
            SourceNodeId = sourceNodeId;
            RuntimeName = runtimeName;
        }

        public string SourceNodeId { get; }
        public string RuntimeName { get; }
    }
}
