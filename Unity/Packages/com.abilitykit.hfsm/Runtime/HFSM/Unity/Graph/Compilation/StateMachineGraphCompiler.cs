using System;
using System.Collections.Generic;
using System.Linq;
using UnityHFSM.Graph.Conditions;

namespace UnityHFSM.Graph.Compilation
{
    public sealed class StateMachineGraphCompiler
    {
        private readonly List<GraphCompilationDiagnostic> _diagnostics = new List<GraphCompilationDiagnostic>();
        private readonly Dictionary<string, HfsmNodeBase> _nodes = new Dictionary<string, HfsmNodeBase>(StringComparer.Ordinal);
        private readonly Dictionary<string, HfsmTransitionEdge> _edges = new Dictionary<string, HfsmTransitionEdge>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _nodeOwners = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Dictionary<string, EdgeOwner> _edgeOwners = new Dictionary<string, EdgeOwner>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _resolvedDefaults = new Dictionary<string, string>(StringComparer.Ordinal);

        public StateMachineGraphProgram Compile(HfsmGraphAsset graph)
        {
            if (graph == null)
                throw new ArgumentNullException(nameof(graph));

            Reset();
            IndexElements(graph);
            ValidateRoot(graph);
            ValidateParameters(graph);
            ValidateHierarchy(graph);
            ValidateEdgeOwnership(graph);
            ValidateReachability(graph);
            ThrowIfErrors();
            return BuildProgram(graph);
        }

        private void Reset()
        {
            _diagnostics.Clear();
            _nodes.Clear();
            _edges.Clear();
            _nodeOwners.Clear();
            _edgeOwners.Clear();
            _resolvedDefaults.Clear();
        }

        private void IndexElements(HfsmGraphAsset graph)
        {
            foreach (var node in graph.Nodes)
            {
                if (node == null)
                {
                    Error("NODE_NULL", "The graph contains a null node.", string.Empty);
                    continue;
                }

                if (string.IsNullOrEmpty(node.Id))
                {
                    Error("NODE_ID_EMPTY", "A node has an empty ID.", string.Empty);
                    continue;
                }

                if (!_nodes.TryAdd(node.Id, node))
                    Error("NODE_ID_DUPLICATE", $"Node ID '{node.Id}' is not unique.", node.Id);
            }

            foreach (var edge in graph.Edges)
            {
                if (edge == null)
                {
                    Error("EDGE_NULL", "The graph contains a null transition.", string.Empty);
                    continue;
                }

                if (string.IsNullOrEmpty(edge.Id))
                {
                    Error("EDGE_ID_EMPTY", "A transition has an empty ID.", string.Empty);
                    continue;
                }

                if (!_edges.TryAdd(edge.Id, edge))
                    Error("EDGE_ID_DUPLICATE", $"Transition ID '{edge.Id}' is not unique.", edge.Id);
            }
        }

        private void ValidateRoot(HfsmGraphAsset graph)
        {
            if (string.IsNullOrEmpty(graph.RootStateMachineId))
            {
                Error("ROOT_MISSING", "The graph does not define a root state machine.", string.Empty);
                return;
            }

            if (!_nodes.TryGetValue(graph.RootStateMachineId, out var root))
            {
                Error("ROOT_NOT_FOUND", "The root node does not exist.", graph.RootStateMachineId);
                return;
            }

            if (!(root is HfsmStateMachineNode))
                Error("ROOT_NOT_MACHINE", "The root node must be a state machine.", root.Id);
            if (!string.IsNullOrEmpty(root.ParentStateMachineId))
                Error("ROOT_HAS_PARENT", "The root state machine cannot have a parent.", root.Id);
        }

        private void ValidateParameters(HfsmGraphAsset graph)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var parameter in graph.Parameters)
            {
                if (parameter == null)
                {
                    Error("PARAMETER_NULL", "The graph contains a null parameter.", string.Empty);
                    continue;
                }

                if (string.IsNullOrWhiteSpace(parameter.Name))
                    Error("PARAMETER_NAME_EMPTY", "A parameter has an empty name.", string.Empty);
                else if (!names.Add(parameter.Name))
                    Error("PARAMETER_NAME_DUPLICATE", $"Parameter name '{parameter.Name}' is not unique.", parameter.Name);
            }
        }

        private void ValidateHierarchy(HfsmGraphAsset graph)
        {
            foreach (var machine in _nodes.Values.OfType<HfsmStateMachineNode>())
            {
                var runtimeNames = new HashSet<string>(StringComparer.Ordinal);
                var markedDefaults = new List<string>();
                foreach (var childId in machine.ChildNodeIds)
                {
                    if (!_nodes.TryGetValue(childId, out var child))
                    {
                        Error("CHILD_NOT_FOUND", $"Child node '{childId}' does not exist.", machine.Id);
                        continue;
                    }

                    if (_nodeOwners.TryGetValue(childId, out var existingOwner))
                        Error("CHILD_MULTIPLE_OWNERS", $"Node '{childId}' belongs to both '{existingOwner}' and '{machine.Id}'.", childId);
                    else
                        _nodeOwners[childId] = machine.Id;

                    if (!string.Equals(child.ParentStateMachineId, machine.Id, StringComparison.Ordinal))
                        Error("PARENT_MISMATCH", $"Node '{childId}' does not point back to owner '{machine.Id}'.", childId);

                    var runtimeName = child.GetName();
                    if (string.IsNullOrEmpty(runtimeName))
                        Error("RUNTIME_NAME_EMPTY", "A child node has an empty runtime name.", childId);
                    else if (!runtimeNames.Add(runtimeName))
                        Error("RUNTIME_NAME_DUPLICATE", $"Runtime name '{runtimeName}' is duplicated inside machine '{machine.GetName()}'.", childId);

                    if (child.isDefault)
                        markedDefaults.Add(childId);
                }

                if (!string.IsNullOrEmpty(machine.DefaultStateId) && !machine.ChildNodeIds.Contains(machine.DefaultStateId))
                    Error("DEFAULT_NOT_CHILD", "The default state must be a direct child of its state machine.", machine.Id);
                if (markedDefaults.Count > 1)
                    Error("DEFAULT_MULTIPLE", "A state machine cannot have more than one child marked as default.", machine.Id);
                if (!string.IsNullOrEmpty(machine.DefaultStateId) &&
                    markedDefaults.Count == 1 &&
                    !string.Equals(machine.DefaultStateId, markedDefaults[0], StringComparison.Ordinal))
                {
                    Error("DEFAULT_CONFLICT", "The explicit default state conflicts with the child marked as default.", machine.Id);
                }

                var resolvedDefault = !string.IsNullOrEmpty(machine.DefaultStateId)
                    ? machine.DefaultStateId
                    : markedDefaults.FirstOrDefault();
                _resolvedDefaults[machine.Id] = resolvedDefault ?? string.Empty;
            }

            foreach (var node in _nodes.Values)
            {
                if (node.Id == graph.RootStateMachineId)
                    continue;
                if (!_nodeOwners.ContainsKey(node.Id))
                    Error("NODE_ORPHANED", "The node is not owned by a state machine.", node.Id);
            }

            DetectHierarchyCycles(graph.RootStateMachineId);
        }

        private void DetectHierarchyCycles(string rootId)
        {
            var visiting = new HashSet<string>(StringComparer.Ordinal);
            var visited = new HashSet<string>(StringComparer.Ordinal);
            VisitMachine(rootId, visiting, visited);
        }

        private void VisitMachine(string machineId, HashSet<string> visiting, HashSet<string> visited)
        {
            if (string.IsNullOrEmpty(machineId) || visited.Contains(machineId))
                return;
            if (!visiting.Add(machineId))
            {
                Error("HIERARCHY_CYCLE", "The state machine hierarchy contains a cycle.", machineId);
                return;
            }

            if (_nodes.TryGetValue(machineId, out var node) && node is HfsmStateMachineNode machine)
            {
                foreach (var childId in machine.ChildNodeIds)
                {
                    if (_nodes.TryGetValue(childId, out var child) && child is HfsmStateMachineNode)
                        VisitMachine(childId, visiting, visited);
                }
            }

            visiting.Remove(machineId);
            visited.Add(machineId);
        }

        private void ValidateEdgeOwnership(HfsmGraphAsset graph)
        {
            foreach (var machine in _nodes.Values.OfType<HfsmStateMachineNode>())
            {
                foreach (var edgeId in machine.TransitionIds)
                    RegisterEdgeOwner(machine, edgeId, false);
                foreach (var edgeId in machine.AnyStateTransitionIds)
                    RegisterEdgeOwner(machine, edgeId, true);
            }

            foreach (var edge in _edges.Values)
            {
                if (!_edgeOwners.ContainsKey(edge.Id))
                    Error("EDGE_ORPHANED", "The transition is not owned by a state machine.", edge.Id);
            }
        }

        private void RegisterEdgeOwner(HfsmStateMachineNode machine, string edgeId, bool isFromAnyState)
        {
            if (!_edges.TryGetValue(edgeId, out var edge))
            {
                Error("EDGE_NOT_FOUND", $"Owned transition '{edgeId}' does not exist.", machine.Id);
                return;
            }

            if (_edgeOwners.TryGetValue(edgeId, out var existingOwner))
            {
                Error("EDGE_MULTIPLE_OWNERS", $"Transition '{edgeId}' belongs to both '{existingOwner.MachineId}' and '{machine.Id}'.", edgeId);
                return;
            }

            _edgeOwners[edgeId] = new EdgeOwner(machine.Id, isFromAnyState);
            var childIds = new HashSet<string>(machine.ChildNodeIds, StringComparer.Ordinal);
            if (isFromAnyState)
            {
                if (!string.Equals(edge.SourceNodeId, HfsmSpecialNodeIds.AnyState, StringComparison.Ordinal))
                    Error("ANY_STATE_SOURCE_INVALID", "An AnyState transition must use the AnyState pseudo node as its source.", edge.Id);
            }
            else if (!childIds.Contains(edge.SourceNodeId))
            {
                Error("EDGE_SOURCE_OUTSIDE_OWNER", "The transition source must be a direct child of its owner.", edge.Id);
            }

            if (!edge.IsExitTransition && !childIds.Contains(edge.TargetNodeId))
                Error("EDGE_TARGET_OUTSIDE_OWNER", "The transition target must be a direct child of its owner.", edge.Id);

            if (!edge.IsExitTransition && !_nodes.ContainsKey(edge.TargetNodeId))
                Error("EDGE_TARGET_NOT_FOUND", "The transition target does not exist.", edge.Id);
        }

        private void ValidateReachability(HfsmGraphAsset graph)
        {
            var reachable = new HashSet<string>(StringComparer.Ordinal);
            CollectReachable(graph.RootStateMachineId, reachable);
            foreach (var nodeId in _nodes.Keys)
            {
                if (!reachable.Contains(nodeId))
                    Error("NODE_UNREACHABLE", "The node is not reachable from the root state machine.", nodeId);
            }
        }

        private void CollectReachable(string nodeId, HashSet<string> reachable)
        {
            if (string.IsNullOrEmpty(nodeId) || !reachable.Add(nodeId))
                return;
            if (_nodes.TryGetValue(nodeId, out var node) && node is HfsmStateMachineNode machine)
            {
                foreach (var childId in machine.ChildNodeIds)
                    CollectReachable(childId, reachable);
            }
        }

        private StateMachineGraphProgram BuildProgram(HfsmGraphAsset graph)
        {
            var programs = new Dictionary<string, GraphNodeProgram>(StringComparer.Ordinal);
            var machines = new Dictionary<string, MachineProgram>(StringComparer.Ordinal);

            foreach (var state in _nodes.Values.OfType<HfsmStateNode>())
                programs.Add(state.Id, new StateProgram(state.Id, state.GetName(), CreateStateTemplate(state)));

            foreach (var machine in _nodes.Values.OfType<HfsmStateMachineNode>())
            {
                var transitions = BuildTransitions(machine);
                var program = new MachineProgram(
                    machine.Id,
                    machine.GetName(),
                    machine.ParentStateMachineId,
                    _resolvedDefaults[machine.Id],
                    machine.RememberLastState,
                    machine.ChildNodeIds.ToArray(),
                    transitions);
                programs.Add(machine.Id, program);
                machines.Add(machine.Id, program);
            }

            var parameters = graph.Parameters
                .Select(parameter => new ParameterProgram(
                    parameter.Name,
                    parameter.ParameterType,
                    parameter.GetSerializedDefaultValue()))
                .ToArray();
            return new StateMachineGraphProgram(graph.GraphName, graph.RootStateMachineId, programs, machines, parameters);
        }

        private static HfsmStateNode CreateStateTemplate(HfsmStateNode source)
        {
            var template = (HfsmStateNode)source.Clone();
            for (var index = 0; index < source.BehaviorItems.Count; index++)
            {
                var sourceItem = source.BehaviorItems[index];
                var templateItem = template.BehaviorItemsInternal[index];
                templateItem.id = sourceItem.id;
                templateItem.parentId = sourceItem.parentId;
                templateItem.childIds = new List<string>(sourceItem.childIds);
            }

            return template;
        }

        private IReadOnlyList<TransitionProgram> BuildTransitions(HfsmStateMachineNode machine)
        {
            var ownedIds = machine.TransitionIds.Concat(machine.AnyStateTransitionIds);
            var result = new List<TransitionProgram>();
            foreach (var edgeId in ownedIds)
            {
                var edge = _edges[edgeId];
                var conditions = new List<HfsmTransitionCondition>();
                try
                {
                    foreach (var condition in HfsmConditionSerializer.DeserializeStrict(edge.ConditionConfigJson))
                    {
                        if (condition == null)
                            throw new InvalidOperationException("A transition condition is null.");
                        conditions.Add(condition.Clone());
                    }
                }
                catch (Exception exception)
                {
                    Error("CONDITION_CONFIG_INVALID", exception.Message, edge.Id);
                    continue;
                }

                var owner = _edgeOwners[edgeId];
                result.Add(new TransitionProgram(
                    edge.Id,
                    machine.Id,
                    edge.SourceNodeId,
                    edge.TargetNodeId,
                    edge.Priority,
                    owner.IsFromAnyState,
                    edge.IsExitTransition,
                    edge.ForceInstantly,
                    edge.NextTriggerId,
                    edge.NextActionKey,
                    edge.UseAndLogic,
                    conditions.AsReadOnly()));
            }

            ThrowIfErrors();
            return result
                .OrderByDescending(transition => transition.Priority)
                .ThenBy(transition => transition.SourceEdgeId, StringComparer.Ordinal)
                .ToArray();
        }

        private void ThrowIfErrors()
        {
            if (_diagnostics.Any(diagnostic => diagnostic.Severity == GraphDiagnosticSeverity.Error))
                throw new GraphCompilationException(_diagnostics.ToArray());
        }

        private void Error(string code, string message, string elementId)
        {
            _diagnostics.Add(new GraphCompilationDiagnostic(code, message, elementId));
        }

        private readonly struct EdgeOwner
        {
            public EdgeOwner(string machineId, bool isFromAnyState)
            {
                MachineId = machineId;
                IsFromAnyState = isFromAnyState;
            }

            public string MachineId { get; }
            public bool IsFromAnyState { get; }
        }
    }
}
