using System;
using System.Collections.Generic;

namespace AbilityKit.Trace
{
    public enum TraceRegistryEventKind
    {
        RootCreated = 0,
        ChildCreated = 1,
        NodeEnded = 2,
        RootEnded = 3,
        RootRetained = 4,
        RootReleased = 5,
        RootPurged = 6,
        RegistryCleared = 7,
        LifecycleRestored = 8,
        PredictionRetracted = 9
    }

    public readonly struct TraceRegistryEvent
    {
        public readonly TraceRegistryEventKind Kind;
        public readonly long ContextId;
        public readonly long RootId;
        public readonly long ParentId;
        public readonly int NodeKind;
        public readonly int Frame;
        public readonly int Reason;

        public TraceRegistryEvent(
            TraceRegistryEventKind kind,
            long contextId,
            long rootId,
            long parentId,
            int nodeKind,
            int frame,
            int reason = 0)
        {
            Kind = kind;
            ContextId = contextId;
            RootId = rootId;
            ParentId = parentId;
            NodeKind = nodeKind;
            Frame = frame;
            Reason = reason;
        }
    }

    public readonly struct TraceNodeSnapshot
    {
        public readonly long ContextId;
        public readonly long RootId;
        public readonly long ParentId;
        public readonly int Kind;
        public readonly int CreatedFrame;
        public readonly int EndedFrame;
        public readonly int EndReason;
        public readonly int ChildCount;
        public readonly object Metadata;
        private readonly bool _isEnded;

        public TraceNodeSnapshot(
            long contextId,
            long rootId,
            long parentId,
            int kind,
            int endedFrame,
            int endReason,
            int childCount,
            object metadata)
            : this(
                contextId,
                rootId,
                parentId,
                kind,
                0,
                endedFrame,
                endReason,
                childCount,
                metadata,
                endedFrame != 0)
        {
        }

        public TraceNodeSnapshot(
            long contextId,
            long rootId,
            long parentId,
            int kind,
            int createdFrame,
            int endedFrame,
            int endReason,
            int childCount,
            object metadata,
            bool isEnded)
        {
            ContextId = contextId;
            RootId = rootId;
            ParentId = parentId;
            Kind = kind;
            CreatedFrame = createdFrame;
            EndedFrame = endedFrame;
            EndReason = endReason;
            ChildCount = childCount;
            Metadata = metadata;
            _isEnded = isEnded;
        }

        public bool IsEnded => _isEnded;
        public bool IsRoot => ContextId == RootId;
        public bool IsLeaf => ChildCount == 0;
        public bool IsValid => ContextId != 0;
    }

    public static class TraceRegistryDirectory
    {
        private static readonly List<TraceTreeRegistryBase> s_registries = new List<TraceTreeRegistryBase>(16);

        public static IReadOnlyList<TraceTreeRegistryBase> Registries => s_registries;

        public static bool Register(TraceTreeRegistryBase registry)
        {
            if (registry == null) throw new ArgumentNullException(nameof(registry));
            if (s_registries.Contains(registry)) return false;

            s_registries.Add(registry);
            return true;
        }

        public static bool Unregister(TraceTreeRegistryBase registry)
        {
            if (registry == null) return false;
            return s_registries.Remove(registry);
        }

        public static void Clear()
        {
            s_registries.Clear();
        }

        public static void CopyTo(List<TraceTreeRegistryBase> output)
        {
            if (output == null) throw new ArgumentNullException(nameof(output));
            output.Clear();
            output.AddRange(s_registries);
        }
    }
}
