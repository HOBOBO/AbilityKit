#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using AbilityKit.Editor.Platform.Diagnostics;

namespace AbilityKit.BehaviorTree.Editor
{
    /// <summary>
    /// Behavior Tree editor adapter that gives legacy runtime validation messages
    /// stable diagnostic metadata and explicit node-location actions.
    /// Runtime validation remains the semantic authority.
    /// </summary>
    public static class BtEditorDiagnostics
    {
        public const string ValidationErrorCode = "BTVAL001";

        public static EditorDiagnosticCollection Analyze(
            BtTreeDefinition definition,
            BtNodeRegistry registry,
            Action<string>? locateNode = null)
        {
            var messages = BtTreeValidator.Validate(definition, registry);
            return FromValidationMessages(definition, messages, locateNode);
        }

        public static EditorDiagnosticCollection FromValidationMessages(
            BtTreeDefinition? definition,
            IEnumerable<string> messages,
            Action<string>? locateNode = null)
        {
            if (messages == null) throw new ArgumentNullException(nameof(messages));

            var nodeIds = (definition?.Nodes ?? new List<BtNodeDefinition>())
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
    }
}
