#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using AbilityKit.BehaviorTree.Authoring;
using AbilityKit.Editor.Platform.Diagnostics;
using UnityEditor;

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
        public const string ProjectValidationErrorCode = "BTPRJ001";

        public static EditorDiagnosticCollection Analyze(
            TreeDefinition definition,
            NodeRegistry registry,
            Action<string>? locateNode = null)
            => Analyze(definition, registry, null, locateNode);

        public static EditorDiagnosticCollection Analyze(
            TreeDefinition definition,
            NodeRegistry registry,
            TreeDefinitionResolver? resolver,
            Action<string>? locateNode)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (registry == null) throw new ArgumentNullException(nameof(registry));
            var result = BehaviorTreeBuildPipeline.Build(definition, registry, resolver);
            return FromValidationDiagnostics(definition, result.Diagnostics, locateNode);
        }

        public static EditorDiagnosticCollection Analyze(
            AuthoringSourceDocument document,
            NodeRegistry registry,
            Action<string>? locateNode = null)
            => Analyze(document, registry, null, locateNode);

        public static EditorDiagnosticCollection Analyze(
            AuthoringSourceDocument document,
            NodeRegistry registry,
            TreeDefinitionResolver? resolver,
            Action<string>? locateNode)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));
            if (registry == null) throw new ArgumentNullException(nameof(registry));

            var diagnostics = Analyze(document.Tree, registry, resolver, locateNode);
            diagnostics.AddRange(EditorExtensionRegistry.Analyze(document, registry));
            return diagnostics;
        }

        public static EditorDiagnosticCollection FromValidationDiagnostics(
            TreeDefinition? definition,
            IEnumerable<ValidationDiagnostic> validationDiagnostics,
            Action<string>? locateNode = null)
        {
            if (validationDiagnostics == null) throw new ArgumentNullException(nameof(validationDiagnostics));
            var knownNodeIds = new HashSet<string>(
                (definition?.Nodes ?? new List<NodeDefinition>())
                    .Where(node => node != null && !string.IsNullOrWhiteSpace(node.Id))
                    .Select(node => node.Id),
                StringComparer.Ordinal);
            var diagnostics = new EditorDiagnosticCollection();
            foreach (var diagnostic in validationDiagnostics)
            {
                var nodeId = diagnostic.NodeId;
                if (nodeId != null && !knownNodeIds.Contains(nodeId)) nodeId = null;
                var targetNodeId = nodeId;
                Action? locate = targetNodeId != null && locateNode != null
                    ? () => locateNode(targetNodeId)
                    : null;
                diagnostics.Add(new EditorDiagnostic(
                    ValidationErrorCode,
                    diagnostic.Severity == ValidationSeverity.Warning
                        ? EditorDiagnosticSeverity.Warning
                        : EditorDiagnosticSeverity.Error,
                    $"[{diagnostic.Code}] {diagnostic.Message}",
                    targetNodeId == null ? "tree" : "nodes/" + targetNodeId,
                    locate: locate));
            }
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
                "观察采样数=" + controller.Timeline.Count
                + "，容量=" + controller.TimelineCapacity
                + "，采样间隔（秒）=" + controller.SampleIntervalSeconds.ToString("0.###"),
                "observation"));

            if (controller.State == ObservationSessionState.Disconnected)
            {
                diagnostics.Add(new EditorDiagnostic(
                    ObservationWarningCode,
                    EditorDiagnosticSeverity.Warning,
                    "选中的行为树实例已断开；保留的采样只能离线查看。",
                    "observation/connection"));
            }

            if (controller.TimelineCapacity == ObservationSettings.MaxTimelineCapacity)
            {
                diagnostics.Add(new EditorDiagnostic(
                    ObservationWarningCode,
                    EditorDiagnosticSeverity.Warning,
                    "观察时间线容量已达到包上限；长时间采集前请先导出记录。",
                    "observation/settings/timelineCapacity"));
            }

            return diagnostics;
        }

        public static EditorDiagnosticCollection AnalyzeProject(AuthoringProjectAsset project)
        {
            if (project == null) throw new ArgumentNullException(nameof(project));
            var diagnostics = new EditorDiagnosticCollection();
            var path = AssetDatabase.GetAssetPath(project);
            foreach (var message in project.Validate())
            {
                if (string.IsNullOrWhiteSpace(message)) continue;
                diagnostics.Add(new EditorDiagnostic(
                    ProjectValidationErrorCode,
                    EditorDiagnosticSeverity.Error,
                    message,
                    path,
                    target: project));
            }
            return diagnostics;
        }

        public static EditorDiagnosticCollection AnalyzeProjects(IEnumerable<AuthoringProjectAsset> projects)
        {
            if (projects == null) throw new ArgumentNullException(nameof(projects));
            var diagnostics = new EditorDiagnosticCollection();
            foreach (var project in projects)
            {
                if (project == null) continue;
                diagnostics.AddRange(AnalyzeProject(project).Items);
            }
            return diagnostics;
        }
    }
}
