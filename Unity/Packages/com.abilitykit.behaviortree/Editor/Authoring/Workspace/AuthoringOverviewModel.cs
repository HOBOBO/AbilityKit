#nullable enable

using System;
using System.Collections.Generic;

namespace AbilityKit.BehaviorTree.Editor.Authoring.Workspace
{
    internal sealed class AuthoringOverviewModel
    {
        public AuthoringOverviewModel(
            int nodeCount,
            int edgeCount,
            int groupCount,
            int noteCount,
            int blackboardKeyCount,
            string rootNodeId,
            string rootDisplayName,
            IReadOnlyList<string> orphanNodeIds,
            IReadOnlyList<string> subtreeReferences,
            AuthoringSearchResult search,
            int diagnosticErrorCount,
            bool clipboardAvailable)
        {
            NodeCount = Math.Max(0, nodeCount);
            EdgeCount = Math.Max(0, edgeCount);
            GroupCount = Math.Max(0, groupCount);
            NoteCount = Math.Max(0, noteCount);
            BlackboardKeyCount = Math.Max(0, blackboardKeyCount);
            RootNodeId = rootNodeId ?? string.Empty;
            RootDisplayName = rootDisplayName ?? string.Empty;
            OrphanNodeIds = orphanNodeIds ?? Array.Empty<string>();
            SubtreeReferences = subtreeReferences ?? Array.Empty<string>();
            Search = search ?? new AuthoringSearchResult(string.Empty, Array.Empty<AuthoringNodeSearchHit>(), 0);
            DiagnosticErrorCount = Math.Max(0, diagnosticErrorCount);
            ClipboardAvailable = clipboardAvailable;
        }

        public int NodeCount { get; }
        public int EdgeCount { get; }
        public int GroupCount { get; }
        public int NoteCount { get; }
        public int BlackboardKeyCount { get; }
        public string RootNodeId { get; }
        public string RootDisplayName { get; }
        public IReadOnlyList<string> OrphanNodeIds { get; }
        public IReadOnlyList<string> SubtreeReferences { get; }
        public AuthoringSearchResult Search { get; }
        public int DiagnosticErrorCount { get; }
        public bool ClipboardAvailable { get; }
    }
}
