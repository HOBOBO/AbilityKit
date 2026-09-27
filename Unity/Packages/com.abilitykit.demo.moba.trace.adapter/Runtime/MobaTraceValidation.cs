using System.Collections.Generic;
using AbilityKit.Trace;

namespace AbilityKit.Demo.Moba.Services
{
    public enum MobaTraceValidationError
    {
        None = 0,
        RootNotFound = 1,
        InvalidRoot = 2,
        DuplicateContext = 3,
        RootMismatch = 4,
        ParentNotFound = 5,
        ParentRootMismatch = 6,
        CycleDetected = 7,
        ChildCountMismatch = 8
    }

    public readonly struct MobaTraceValidationResult
    {
        public MobaTraceValidationResult(
            MobaTraceValidationError error,
            long contextId = 0L)
        {
            Error = error;
            ContextId = contextId;
        }

        public MobaTraceValidationError Error { get; }
        public long ContextId { get; }
        public bool IsValid => Error == MobaTraceValidationError.None;

        public static MobaTraceValidationResult Valid =>
            new MobaTraceValidationResult(MobaTraceValidationError.None);
    }

    internal static class MobaTraceValidation
    {
        public static MobaTraceValidationResult Validate(MobaTraceRegistry registry, long rootId)
        {
            if (!registry.TryGetNodeSnapshot(rootId, out var root))
            {
                return Invalid(MobaTraceValidationError.RootNotFound, rootId);
            }

            if (!root.IsRoot || root.ParentId != 0L)
            {
                return Invalid(MobaTraceValidationError.InvalidRoot, rootId);
            }

            var nodes = new Dictionary<long, TraceNodeSnapshot>();
            foreach (var node in registry.GetNodeSnapshotsByRoot(rootId))
            {
                if (!nodes.TryAdd(node.ContextId, node))
                {
                    return Invalid(MobaTraceValidationError.DuplicateContext, node.ContextId);
                }

                if (node.RootId != rootId)
                {
                    return Invalid(MobaTraceValidationError.RootMismatch, node.ContextId);
                }
            }

            if (!nodes.ContainsKey(rootId))
            {
                return Invalid(MobaTraceValidationError.RootMismatch, rootId);
            }

            var childCounts = new Dictionary<long, int>();
            foreach (var pair in nodes)
            {
                var node = pair.Value;
                if (node.ContextId == rootId) continue;

                if (!nodes.TryGetValue(node.ParentId, out var parent))
                {
                    return Invalid(MobaTraceValidationError.ParentNotFound, node.ContextId);
                }

                if (parent.RootId != rootId)
                {
                    return Invalid(MobaTraceValidationError.ParentRootMismatch, node.ContextId);
                }

                childCounts.TryGetValue(node.ParentId, out var count);
                childCounts[node.ParentId] = count + 1;

                var visited = new HashSet<long>();
                var current = node;
                while (current.ContextId != rootId)
                {
                    if (!visited.Add(current.ContextId))
                    {
                        return Invalid(MobaTraceValidationError.CycleDetected, node.ContextId);
                    }

                    if (!nodes.TryGetValue(current.ParentId, out current))
                    {
                        return Invalid(MobaTraceValidationError.ParentNotFound, node.ContextId);
                    }
                }
            }

            foreach (var pair in nodes)
            {
                childCounts.TryGetValue(pair.Key, out var actualChildCount);
                if (pair.Value.ChildCount != actualChildCount)
                {
                    return Invalid(MobaTraceValidationError.ChildCountMismatch, pair.Key);
                }
            }

            return MobaTraceValidationResult.Valid;
        }

        private static MobaTraceValidationResult Invalid(
            MobaTraceValidationError error,
            long contextId)
        {
            return new MobaTraceValidationResult(error, contextId);
        }
    }
}
