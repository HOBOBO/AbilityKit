#nullable enable

using System;
using System.Collections.Generic;

namespace AbilityKit.BehaviorTree.Editor.Authoring.Workspace
{
    internal sealed class AuthoringClipboardPasteResult
    {
        public bool Changed { get; set; }
        public Dictionary<string, string> NodeIdMap { get; } = new(StringComparer.Ordinal);
        public List<string> CreatedNodeIds { get; } = new();
        public List<string> CreatedGroupIds { get; } = new();
        public List<string> CreatedNoteIds { get; } = new();
    }
}
