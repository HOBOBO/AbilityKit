using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityHFSM.Graph.Conditions;

namespace UnityHFSM.Graph.Compilation
{
    public enum GraphDiagnosticSeverity
    {
        Warning,
        Error
    }

    public sealed class GraphCompilationDiagnostic
    {
        public GraphCompilationDiagnostic(
            string code,
            string message,
            string elementId,
            GraphDiagnosticSeverity severity = GraphDiagnosticSeverity.Error)
        {
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
            ElementId = elementId ?? string.Empty;
            Severity = severity;
        }

        public string Code { get; }
        public string Message { get; }
        public string ElementId { get; }
        public GraphDiagnosticSeverity Severity { get; }

        public override string ToString()
        {
            return string.IsNullOrEmpty(ElementId)
                ? $"{Code}: {Message}"
                : $"{Code} [{ElementId}]: {Message}";
        }
    }

    public sealed class GraphCompilationException : Exception
    {
        public GraphCompilationException(IReadOnlyList<GraphCompilationDiagnostic> diagnostics)
            : base(CreateMessage(diagnostics))
        {
            Diagnostics = diagnostics == null
                ? Array.Empty<GraphCompilationDiagnostic>()
                : new ReadOnlyCollection<GraphCompilationDiagnostic>(new List<GraphCompilationDiagnostic>(diagnostics));
        }

        public IReadOnlyList<GraphCompilationDiagnostic> Diagnostics { get; }

        private static string CreateMessage(IReadOnlyList<GraphCompilationDiagnostic> diagnostics)
        {
            if (diagnostics == null || diagnostics.Count == 0)
                return "State machine graph compilation failed.";

            var messages = new string[diagnostics.Count];
            for (var index = 0; index < diagnostics.Count; index++)
                messages[index] = diagnostics[index].ToString();
            return "State machine graph compilation failed: " + string.Join("; ", messages);
        }
    }

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

    public sealed class StateProgram : GraphNodeProgram
    {
        internal StateProgram(string sourceNodeId, string runtimeName, HfsmStateNode template)
            : base(sourceNodeId, runtimeName)
        {
            Template = template;
            var behaviorIds = new string[template.BehaviorItems.Count];
            for (var index = 0; index < behaviorIds.Length; index++)
                behaviorIds[index] = template.BehaviorItems[index].id;
            BehaviorIds = Array.AsReadOnly(behaviorIds);
        }

        internal HfsmStateNode Template { get; }
        public bool NeedsExitTime => Template.NeedsExitTime;
        public bool IsGhostState => Template.IsGhostState;
        public string BehaviorKey => Template.NextBehaviorKey;
        public IReadOnlyList<string> BehaviorIds { get; }
    }

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
            IReadOnlyList<HfsmTransitionCondition> conditions)
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
                ? Array.Empty<HfsmTransitionCondition>()
                : new ReadOnlyCollection<HfsmTransitionCondition>(new List<HfsmTransitionCondition>(conditions));
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
        public IReadOnlyList<HfsmTransitionCondition> Conditions { get; }
    }

    public sealed class ParameterProgram
    {
        public ParameterProgram(string name, HfsmParameterType parameterType, object defaultValue)
        {
            Name = name ?? string.Empty;
            ParameterType = parameterType;
            DefaultValue = defaultValue;
        }

        public string Name { get; }
        public HfsmParameterType ParameterType { get; }
        public object DefaultValue { get; }
    }

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
