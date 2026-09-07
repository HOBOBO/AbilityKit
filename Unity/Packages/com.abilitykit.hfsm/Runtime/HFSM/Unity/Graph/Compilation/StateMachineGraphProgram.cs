using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using AbilityKit.HFSM.Graph.Conditions;

namespace AbilityKit.HFSM.Graph.Compilation
{

    public sealed class StateMachineGraphProgram
    {
        private readonly IReadOnlyDictionary<string, GraphNodeProgram> _nodes;
        private readonly IReadOnlyDictionary<string, MachineProgram> _machines;

        internal StateMachineGraphProgram(
            string graphName,
            string rootMachineId,
            IDictionary<string, GraphNodeProgram> nodes,
            IDictionary<string, MachineProgram> machines,
            IReadOnlyList<ParameterProgram> parameters)
        {
            GraphName = graphName ?? string.Empty;
            RootMachineId = rootMachineId;
            _nodes = new ReadOnlyDictionary<string, GraphNodeProgram>(nodes);
            _machines = new ReadOnlyDictionary<string, MachineProgram>(machines);
            Parameters = parameters == null
                ? Array.Empty<ParameterProgram>()
                : new ReadOnlyCollection<ParameterProgram>(new List<ParameterProgram>(parameters));
        }

        public string GraphName { get; }
        public string RootMachineId { get; }
        public MachineProgram RootMachine => GetMachine(RootMachineId);
        public IReadOnlyDictionary<string, GraphNodeProgram> Nodes => _nodes;
        public IReadOnlyDictionary<string, MachineProgram> Machines => _machines;
        public IReadOnlyList<ParameterProgram> Parameters { get; }

        public GraphNodeProgram GetNode(string sourceNodeId)
        {
            return sourceNodeId != null && _nodes.TryGetValue(sourceNodeId, out var node) ? node : null;
        }

        public MachineProgram GetMachine(string sourceNodeId)
        {
            return sourceNodeId != null && _machines.TryGetValue(sourceNodeId, out var machine) ? machine : null;
        }
    }
}
