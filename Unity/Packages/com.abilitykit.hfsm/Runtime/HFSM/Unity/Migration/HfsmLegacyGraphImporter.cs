#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using AbilityKit.HFSM;
using UnityHFSM.Graph;

namespace AbilityKit.HFSM.Unity.Migration
{
    public enum HfsmLegacyImportSeverity
    {
        Warning = 0,
        Error = 1,
    }

    public sealed class HfsmLegacyImportIssue
    {
        public HfsmLegacyImportIssue(
            string code,
            HfsmLegacyImportSeverity severity,
            string sourcePath,
            string message)
        {
            Code = code ?? string.Empty;
            Severity = severity;
            SourcePath = sourcePath ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public string Code { get; }

        public HfsmLegacyImportSeverity Severity { get; }

        public string SourcePath { get; }

        public string Message { get; }

        public override string ToString() => $"{Code} {Severity} at {SourcePath}: {Message}";
    }

    /// <summary>
    /// Explicit mapping from legacy executable payloads to stable Next-runtime binding keys.
    /// Import never derives keys from CLR method or behavior type names.
    /// </summary>
    public sealed class HfsmLegacyImportBindings
    {
        private readonly Dictionary<string, string> _stateKeys =
            new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _conditionKeys =
            new Dictionary<string, string>(StringComparer.Ordinal);

        public HfsmLegacyImportBindings RegisterState(string nodeId, string behaviorKey)
        {
            Register(_stateKeys, nodeId, behaviorKey, "state");
            return this;
        }

        public HfsmLegacyImportBindings RegisterCondition(string edgeId, string conditionKey)
        {
            Register(_conditionKeys, edgeId, conditionKey, "condition");
            return this;
        }

        internal bool TryGetState(string nodeId, out string key) => _stateKeys.TryGetValue(nodeId, out key);

        internal bool TryGetCondition(string edgeId, out string key) =>
            _conditionKeys.TryGetValue(edgeId, out key);

        private static void Register(Dictionary<string, string> target, string sourceId, string key, string kind)
        {
            if (string.IsNullOrWhiteSpace(sourceId))
                throw new ArgumentException($"Legacy HFSM {kind} source id is required.", nameof(sourceId));
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException($"Legacy HFSM {kind} binding key is required.", nameof(key));
            if (!target.TryAdd(sourceId, key))
                throw new InvalidOperationException($"Legacy HFSM {kind} mapping '{sourceId}' is already registered.");
        }
    }

    public sealed class HfsmLegacyImportResult
    {
        internal HfsmLegacyImportResult(HfsmDefinition? definition, List<HfsmLegacyImportIssue> issues)
        {
            Definition = definition;
            Issues = issues.AsReadOnly();
        }

        /// <summary>Null when any error was found; warnings do not block import.</summary>
        public HfsmDefinition? Definition { get; }

        public IReadOnlyList<HfsmLegacyImportIssue> Issues { get; }

        public bool IsSuccess => Definition != null && Issues.All(issue => issue.Severity != HfsmLegacyImportSeverity.Error);
    }

    /// <summary>
    /// Imports only the legacy Graph subset that has a direct semantic equivalent. Unsupported
    /// executable payloads require explicit mappings, and unsupported control flow is rejected.
    /// </summary>
    public static class HfsmLegacyGraphImporter
    {
        public static HfsmLegacyImportResult Import(
            HfsmGraphAsset? graph,
            HfsmLegacyImportBindings? bindings = null)
        {
            var issues = new List<HfsmLegacyImportIssue>();
            if (graph == null)
            {
                Error(issues, "HFSMLEG001", "$", "Legacy graph is null.");
                return new HfsmLegacyImportResult(null, issues);
            }

            bindings ??= new HfsmLegacyImportBindings();
            var nodesById = BuildNodeIndex(graph, issues);
            if (!nodesById.TryGetValue(graph.RootStateMachineId ?? string.Empty, out var rootNode) ||
                !(rootNode is HfsmStateMachineNode))
            {
                Error(issues, "HFSMLEG002", "$.rootStateMachineId", "Root must reference a state-machine node.");
            }

            var machines = graph.GetNodesOfType<HfsmStateMachineNode>().
                OrderBy(machine => machine.Id, StringComparer.Ordinal).ToArray();
            var definition = new HfsmDefinition
            {
                DefinitionId = graph.GraphName ?? string.Empty,
                RootMachineId = graph.RootStateMachineId ?? string.Empty,
            };
            var consumedEdges = new HashSet<string>(StringComparer.Ordinal);

            for (var machineIndex = 0; machineIndex < machines.Length; machineIndex++)
            {
                definition.Machines.Add(ConvertMachine(
                    graph,
                    machines[machineIndex],
                    nodesById,
                    bindings,
                    consumedEdges,
                    issues));
            }

            foreach (var edge in graph.Edges)
            {
                if (edge != null && !consumedEdges.Contains(edge.Id))
                {
                    Warning(issues, "HFSMLEG030", $"$.edges['{edge.Id}']",
                        "Edge is not owned by a state-machine transition list and was not imported.");
                }
            }

            if (issues.Any(issue => issue.Severity == HfsmLegacyImportSeverity.Error))
                return new HfsmLegacyImportResult(null, issues);

            var validation = HfsmDefinitionValidator.Validate(definition);
            foreach (var issue in validation.Issues)
            {
                Error(issues, "HFSMLEG090", issue.Path, issue.ToString());
            }

            return new HfsmLegacyImportResult(validation.IsValid ? definition : null, issues);
        }

        private static Dictionary<string, HfsmNodeBase> BuildNodeIndex(
            HfsmGraphAsset graph,
            List<HfsmLegacyImportIssue> issues)
        {
            var result = new Dictionary<string, HfsmNodeBase>(StringComparer.Ordinal);
            for (var index = 0; index < graph.Nodes.Count; index++)
            {
                var node = graph.Nodes[index];
                if (node == null)
                {
                    Error(issues, "HFSMLEG003", $"$.nodes[{index}]", "Node is null.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(node.Id))
                {
                    Error(issues, "HFSMLEG004", $"$.nodes[{index}].id", "Node id is required.");
                }
                else if (!result.TryAdd(node.Id, node))
                {
                    Error(issues, "HFSMLEG005", $"$.nodes[{index}].id", $"Duplicate node id '{node.Id}'.");
                }
            }

            return result;
        }

        private static HfsmMachineDefinition ConvertMachine(
            HfsmGraphAsset graph,
            HfsmStateMachineNode source,
            Dictionary<string, HfsmNodeBase> nodesById,
            HfsmLegacyImportBindings bindings,
            HashSet<string> consumedEdges,
            List<HfsmLegacyImportIssue> issues)
        {
            var path = $"$.nodes['{source.Id}']";
            var machine = new HfsmMachineDefinition
            {
                Id = source.Id,
                RememberLastState = source.RememberLastState,
            };

            for (var index = 0; index < source.ChildNodeIds.Count; index++)
            {
                var childId = source.ChildNodeIds[index];
                if (!nodesById.TryGetValue(childId, out var child))
                {
                    Error(issues, "HFSMLEG010", $"{path}.childNodeIds[{index}]",
                        $"Child node '{childId}' does not exist.");
                    continue;
                }

                machine.States.Add(ConvertState(child, bindings, issues));
            }

            machine.InitialStateId = ResolveInitialState(source, nodesById, issues);
            AddTransitions(graph, source, source.TransitionIds, false, machine, nodesById, bindings, consumedEdges, issues);
            AddTransitions(graph, source, source.AnyStateTransitionIds, true, machine, nodesById, bindings, consumedEdges, issues);
            return machine;
        }

        private static HfsmStateDefinition ConvertState(
            HfsmNodeBase source,
            HfsmLegacyImportBindings bindings,
            List<HfsmLegacyImportIssue> issues)
        {
            var path = $"$.nodes['{source.Id}']";
            if (source is HfsmStateMachineNode childMachine)
            {
                return new HfsmStateDefinition
                {
                    Id = source.Id,
                    ChildMachineId = childMachine.Id,
                };
            }

            if (!(source is HfsmStateNode state))
            {
                Error(issues, "HFSMLEG011", path, $"Unsupported node type '{source.GetType().Name}'.");
                return new HfsmStateDefinition { Id = source.Id };
            }

            if (!bindings.TryGetState(state.Id, out var behaviorKey))
                behaviorKey = state.NextBehaviorKey;
            if (state.IsGhostState)
            {
                Error(issues, "HFSMLEG012", path + ".isGhostState",
                    "Ghost-state chaining has no equivalent in the Next runtime.");
            }

            var hasExecutablePayload = state.EntryActionMethodNames.Count > 0 ||
                                       state.LogicActionMethodNames.Count > 0 ||
                                       state.ExitActionMethodNames.Count > 0 ||
                                       state.CanExitMethodNames.Count > 0 ||
                                       state.HasBehaviors;
            if ((hasExecutablePayload || state.NeedsExitTime) && string.IsNullOrEmpty(behaviorKey))
            {
                Error(issues, "HFSMLEG013", path,
                    "State behavior/exit semantics require an explicit stable behavior binding key.");
            }

            return new HfsmStateDefinition
            {
                Id = state.Id,
                BehaviorKey = behaviorKey ?? string.Empty,
                RequiresExitApproval = state.NeedsExitTime,
            };
        }

        private static string ResolveInitialState(
            HfsmStateMachineNode source,
            Dictionary<string, HfsmNodeBase> nodesById,
            List<HfsmLegacyImportIssue> issues)
        {
            if (!string.IsNullOrWhiteSpace(source.DefaultStateId)) return source.DefaultStateId;

            for (var index = 0; index < source.ChildNodeIds.Count; index++)
            {
                if (nodesById.TryGetValue(source.ChildNodeIds[index], out var node) &&
                    node is HfsmStateNode &&
                    node.isDefault)
                    return node.Id;
            }

            if (source.ChildNodeIds.Count > 0)
            {
                Warning(issues, "HFSMLEG014", $"$.nodes['{source.Id}'].defaultStateId",
                    "No explicit default state; preserving legacy first-child fallback.");
                return source.ChildNodeIds[0];
            }

            Error(issues, "HFSMLEG015", $"$.nodes['{source.Id}'].childNodeIds",
                "State machine has no child states.");
            return string.Empty;
        }

        private static void AddTransitions(
            HfsmGraphAsset graph,
            HfsmStateMachineNode owner,
            IReadOnlyList<string> edgeIds,
            bool fromAny,
            HfsmMachineDefinition target,
            Dictionary<string, HfsmNodeBase> nodesById,
            HfsmLegacyImportBindings bindings,
            HashSet<string> consumedEdges,
            List<HfsmLegacyImportIssue> issues)
        {
            var childIds = new HashSet<string>(owner.ChildNodeIds, StringComparer.Ordinal);
            for (var index = 0; index < edgeIds.Count; index++)
            {
                var edgeId = edgeIds[index];
                var path = $"$.nodes['{owner.Id}'].{(fromAny ? "anyStateTransitionIds" : "transitionIds")}[{index}]";
                var edge = graph.GetEdgeById(edgeId);
                if (edge == null)
                {
                    Error(issues, "HFSMLEG020", path, $"Transition edge '{edgeId}' does not exist.");
                    continue;
                }

                if (!consumedEdges.Add(edge.Id))
                {
                    Error(issues, "HFSMLEG021", path, $"Transition edge '{edge.Id}' has multiple owners.");
                    continue;
                }

                if (edge.IsExitTransition)
                {
                    Error(issues, "HFSMLEG022", $"$.edges['{edge.Id}'].isExitTransition",
                        "Vertical exit transitions require an explicit migration design.");
                    continue;
                }

                if (!childIds.Contains(edge.TargetNodeId) || !nodesById.ContainsKey(edge.TargetNodeId))
                {
                    Error(issues, "HFSMLEG023", $"$.edges['{edge.Id}'].targetNodeId",
                        "Transition target must be a direct child of its owning machine.");
                }

                if (!fromAny && (!childIds.Contains(edge.SourceNodeId) || !nodesById.ContainsKey(edge.SourceNodeId)))
                {
                    Error(issues, "HFSMLEG024", $"$.edges['{edge.Id}'].sourceNodeId",
                        "Transition source must be a direct child of its owning machine.");
                }

                if (!bindings.TryGetCondition(edge.Id, out var conditionKey))
                    conditionKey = edge.NextConditionKey;
                if (HasConditionPayload(edge) && string.IsNullOrEmpty(conditionKey))
                {
                    Error(issues, "HFSMLEG025", $"$.edges['{edge.Id}'].conditionConfigJson",
                        "Legacy polymorphic conditions require an explicit stable condition binding key.");
                }

                target.Transitions.Add(new HfsmTransitionDefinition
                {
                    Id = edge.Id,
                    FromAnyState = fromAny,
                    FromStateId = fromAny ? string.Empty : edge.SourceNodeId,
                    ToStateId = edge.TargetNodeId,
                    TriggerId = edge.NextTriggerId,
                    ConditionKey = conditionKey ?? string.Empty,
                    ActionKey = edge.NextActionKey,
                    Priority = edge.Priority,
                    ForceImmediate = edge.ForceInstantly,
                    MinimumActiveDurationRaw = edge.NextMinimumActiveDurationRaw,
                });
            }
        }

        private static bool HasConditionPayload(HfsmTransitionEdge edge)
        {
            var json = edge.ConditionConfigJson;
            return !string.IsNullOrWhiteSpace(json) &&
                   !string.Equals(json.Trim(), "{}", StringComparison.Ordinal);
        }

        private static void Warning(List<HfsmLegacyImportIssue> issues, string code, string path, string message)
        {
            issues.Add(new HfsmLegacyImportIssue(code, HfsmLegacyImportSeverity.Warning, path, message));
        }

        private static void Error(List<HfsmLegacyImportIssue> issues, string code, string path, string message)
        {
            issues.Add(new HfsmLegacyImportIssue(code, HfsmLegacyImportSeverity.Error, path, message));
        }
    }
}
