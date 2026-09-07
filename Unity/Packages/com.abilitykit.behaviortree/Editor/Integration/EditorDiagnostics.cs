#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using AbilityKit.BehaviorTree.Authoring;
using AbilityKit.Editor.Platform.Diagnostics;

using AbilityKit.BehaviorTree.Editor.Authoring.Extensions;
using AbilityKit.BehaviorTree.Editor.Debugging.Observation;
using UnityEngine.Scripting.APIUpdating;
using AbilityKit.BehaviorTree.Authoring.Model;
using AbilityKit.BehaviorTree.Blackboard;
using AbilityKit.BehaviorTree.Definition;
using AbilityKit.BehaviorTree.Diagnostics;
using AbilityKit.BehaviorTree.Execution;
using AbilityKit.BehaviorTree.Nodes;
using AbilityKit.BehaviorTree.Registry;
using AbilityKit.BehaviorTree.Serialization;
using ValueType = AbilityKit.BehaviorTree.Definition.ValueType;
namespace AbilityKit.BehaviorTree.Editor
{
    /// <summary>
    /// Behavior Tree editor adapter that gives legacy runtime validation messages
    /// stable diagnostic metadata and explicit node-location actions.
    /// Runtime validation remains the semantic authority.
    /// </summary>
    [MovedFrom(true, "AbilityKit.BehaviorTree.Editor", "AbilityKit.BehaviorTree.Editor", "BtEditorDiagnostics")]
    public static class EditorDiagnostics
    {
        public const string ValidationErrorCode = "BTVAL001";
        public const string ObservationInfoCode = "BTOBS001";
        public const string ObservationWarningCode = "BTOBS002";

        public static EditorDiagnosticCollection Analyze(
            TreeDefinition definition,
            NodeRegistry registry,
            Action<string>? locateNode = null)
        {
            var messages = TreeValidator.Validate(definition, registry);
            return FromValidationMessages(definition, messages, locateNode);
        }

        public static EditorDiagnosticCollection Analyze(
            AuthoringSourceDocument document,
            NodeRegistry registry,
            Action<string>? locateNode = null)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));
            if (registry == null) throw new ArgumentNullException(nameof(registry));

            var diagnostics = Analyze(document.Tree, registry, locateNode);
            diagnostics.AddRange(EditorExtensionRegistry.Analyze(document, registry));
            return diagnostics;
        }

        public static EditorDiagnosticCollection FromValidationMessages(
            TreeDefinition? definition,
            IEnumerable<string> messages,
            Action<string>? locateNode = null)
        {
            if (messages == null) throw new ArgumentNullException(nameof(messages));

            var nodeIds = (definition?.Nodes ?? new List<NodeDefinition>())
                .Where(node => node != null && !string.IsNullOrWhiteSpace(node.Id))
                .Select(node => node.Id)
                .OrderByDescending(id => id.Length)
                .ThenBy(id => id, StringComparer.Ordinal)
                .ToArray();
            var diagnostics = new EditorDiagnosticCollection();

            foreach (var message in messages)
            {
                if (string.IsNullOrWhiteSpace(message)) continue;
                var nodeId = ResolveQuotedNodeId(message, nodeIds);
                var targetNodeId = nodeId;
                Action? locate = targetNodeId != null && locateNode != null
                    ? () => locateNode(targetNodeId)
                    : null;

                diagnostics.Add(new EditorDiagnostic(
                    ValidationErrorCode,
                    EditorDiagnosticSeverity.Error,
                    message,
                    targetNodeId == null ? "tree" : "nodes/" + targetNodeId,
                    locate: locate));
            }

            return diagnostics;
        }

        internal static string? ResolveQuotedNodeId(
            string message,
            IEnumerable<string> nodeIds)
        {
            if (string.IsNullOrEmpty(message) || nodeIds == null) return null;
            foreach (var nodeId in nodeIds)
            {
                if (message.Contains("'" + nodeId + "'", StringComparison.Ordinal))
                    return nodeId;
            }
            return null;
        }

        public static EditorDiagnosticCollection AnalyzeObservation(ObservationController controller)
        {
            if (controller == null) throw new ArgumentNullException(nameof(controller));
            var diagnostics = new EditorDiagnosticCollection();
            diagnostics.Add(new EditorDiagnostic(
                ObservationInfoCode,
                EditorDiagnosticSeverity.Info,
                "Observation samples=" + controller.Timeline.Count
                + ", capacity=" + controller.TimelineCapacity
                + ", intervalSeconds=" + controller.SampleIntervalSeconds.ToString("0.###"),
                "observation"));

            if (controller.State == ObservationSessionState.Disconnected)
            {
                diagnostics.Add(new EditorDiagnostic(
                    ObservationWarningCode,
                    EditorDiagnosticSeverity.Warning,
                    "Selected behavior tree instance is disconnected; retained samples are offline-only.",
                    "observation/connection"));
            }

            if (controller.TimelineCapacity == ObservationSettings.MaxTimelineCapacity)
            {
                diagnostics.Add(new EditorDiagnostic(
                    ObservationWarningCode,
                    EditorDiagnosticSeverity.Warning,
                    "Observation timeline capacity is at the package maximum; export recordings before long captures.",
                    "observation/settings/timelineCapacity"));
            }

            return diagnostics;
        }
    }
}
