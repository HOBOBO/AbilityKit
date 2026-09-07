using System;
using System.Collections.Generic;
using System.Reflection;
using AbilityKit.Deterministic;

namespace AbilityKit.BehaviorTree.Diagnostics
{
    using AbilityKit.BehaviorTree.Blackboard;
    using AbilityKit.BehaviorTree.Definition;
    using AbilityKit.BehaviorTree.Execution;
    using AbilityKit.BehaviorTree.Registry;

    public static class DebugRegistry
    {
        private static readonly object Gate = new();
        private static readonly Dictionary<long, WeakReference<TreeDebugView>> Views = new();
        private static long _nextId = 1;

        public static int Count
        {
            get { lock (Gate) { return CollectEntriesLocked().Count; } }
        }

        public static void ClearForTests()
        {
            lock (Gate)
            {
                Views.Clear();
            }
        }

        public static DebugHandle Register(TreeDebugView view)
        {
            if (view == null) throw new ArgumentNullException(nameof(view));
            lock (Gate)
            {
                var handle = new DebugHandle(_nextId++);
                Views.Add(handle.Id, new WeakReference<TreeDebugView>(view));
                return handle;
            }
        }

        public static void Unregister(DebugHandle handle)
        {
            if (handle == null) return;
            lock (Gate)
            {
                Views.Remove(handle.Id);
            }
        }

        public static List<TreeDebugView> GetViews()
        {
            lock (Gate)
            {
                var entries = CollectEntriesLocked();
                var result = new List<TreeDebugView>(entries.Count);
                foreach (var entry in entries) result.Add(entry.View);
                return result;
            }
        }

        public static List<DebugRegistryEntry> GetEntries()
        {
            lock (Gate)
            {
                return CollectEntriesLocked();
            }
        }

        public static void CopyEntries(List<DebugRegistryEntry> target)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            lock (Gate)
            {
                target.Clear();
                CollectEntriesLocked(target);
            }
        }

        private static List<DebugRegistryEntry> CollectEntriesLocked()
        {
            var entries = new List<DebugRegistryEntry>(Views.Count);
            CollectEntriesLocked(entries);
            return entries;
        }

        private static void CollectEntriesLocked(List<DebugRegistryEntry> entries)
        {
            List<long>? dead = null;
            foreach (var pair in Views)
            {
                if (pair.Value.TryGetTarget(out var view))
                {
                    entries.Add(new DebugRegistryEntry(pair.Key, view));
                    continue;
                }
                dead ??= new List<long>();
                dead.Add(pair.Key);
            }
            if (dead != null)
            {
                foreach (var id in dead) Views.Remove(id);
            }
            entries.Sort(static (a, b) => a.Id.CompareTo(b.Id));
        }
    }
}
