using System;
using System.Collections.Generic;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;

namespace AbilityKit.Demo.Moba.Services
{
    [WorldService(typeof(MobaDerivedSkillService))]
    public sealed class MobaDerivedSkillService : IService, IMobaSkillRuntimeLifecycleHook, IDisposable
    {
        private readonly MobaSkillCastRuntimeService _runtimes;
        private readonly Dictionary<long, LinkState> _linksByChild = new Dictionary<long, LinkState>();

        private readonly struct LinkState
        {
            public LinkState(in MobaSkillRuntimeRetainHandle retain, int depth)
            {
                Retain = retain;
                Depth = depth;
            }

            public MobaSkillRuntimeRetainHandle Retain { get; }
            public int Depth { get; }
        }

        public MobaDerivedSkillService(MobaSkillCastRuntimeService runtimes)
        {
            _runtimes = runtimes ?? throw new ArgumentNullException(nameof(runtimes));
            _runtimes.LifecycleHooks.Register(this);
        }

        public bool CanStart(in MobaSkillCastRuntimeHandle parent, int maxDepth, out string failure)
        {
            failure = null;
            if (!_runtimes.TryGet(in parent, out _))
            {
                failure = "Derived skill requires a live parent runtime.";
                return false;
            }

            var parentDepth = _linksByChild.TryGetValue(parent.RuntimeId, out var parentLink)
                ? parentLink.Depth
                : 0;
            var limit = Math.Max(1, Math.Min(16, maxDepth));
            if (parentDepth + 1 <= limit) return true;
            failure = $"Derived skill depth {parentDepth + 1} exceeds limit {limit}.";
            return false;
        }

        public bool Link(
            in MobaSkillCastRuntimeHandle parent,
            in MobaSkillCastRuntimeHandle child,
            int maxDepth,
            out string failure)
        {
            failure = null;
            if (!CanStart(in parent, maxDepth, out failure)) return false;
            if (!_runtimes.TryGet(in child, out _))
            {
                failure = "Derived skill returned an inactive child runtime.";
                return false;
            }
            if (_linksByChild.ContainsKey(child.RuntimeId))
            {
                failure = "Derived skill runtime is already linked.";
                return false;
            }

            var depth = _linksByChild.TryGetValue(parent.RuntimeId, out var parentLink)
                ? parentLink.Depth + 1
                : 1;
            var childRef = new MobaSkillRuntimeChildRef(
                MobaSkillRuntimeChildKind.SkillRuntime,
                child.RuntimeId,
                child.RootTraceContextId);
            if (!_runtimes.RetainChild(in parent, in childRef, out var retain))
            {
                failure = "Failed to retain derived skill runtime on its parent.";
                return false;
            }
            _linksByChild.Add(child.RuntimeId, new LinkState(in retain, depth));
            return true;
        }

        public bool IsRunning(in MobaSkillCastRuntimeHandle child)
        {
            return _runtimes.TryGet(in child, out _);
        }

        public void TerminateUnlinkedChild(in MobaSkillCastRuntimeHandle child)
        {
            if (child.IsValid)
                _runtimes.ForceTerminate(in child, MobaSkillRuntimeEndReason.RollbackCleanup);
        }

        internal MobaDerivedSkillServiceSnapshot CaptureRollbackSnapshot()
        {
            var entries = new List<MobaDerivedSkillLinkSnapshot>(_linksByChild.Count);
            foreach (var pair in _linksByChild)
                entries.Add(new MobaDerivedSkillLinkSnapshot(pair.Key, pair.Value.Retain, pair.Value.Depth));
            entries.Sort((left, right) => left.ChildRuntimeId.CompareTo(right.ChildRuntimeId));
            return new MobaDerivedSkillServiceSnapshot(entries.ToArray());
        }

        internal void RestoreRollbackSnapshot(in MobaDerivedSkillServiceSnapshot snapshot)
        {
            _linksByChild.Clear();
            var entries = snapshot.Entries;
            if (entries == null) return;
            for (var i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                if (entry.ChildRuntimeId <= 0 || !entry.Retain.IsValid || entry.Depth <= 0) continue;
                var retain = entry.Retain;
                var parent = retain.Runtime;
                if (!_runtimes.TryGet(entry.ChildRuntimeId, out _) || !_runtimes.TryGet(in parent, out _)) continue;
                _linksByChild[entry.ChildRuntimeId] = new LinkState(in retain, entry.Depth);
            }
        }

        public void OnSkillRuntimeLifecycle(in MobaSkillRuntimeLifecycleEvent lifecycleEvent)
        {
            if (lifecycleEvent.Runtime == null) return;
            switch (lifecycleEvent.Kind)
            {
                case MobaSkillRuntimeLifecycleEventKind.Finalized:
                case MobaSkillRuntimeLifecycleEventKind.ForceTerminated:
                case MobaSkillRuntimeLifecycleEventKind.Cleared:
                    Release(lifecycleEvent.Runtime.RuntimeId);
                    break;
            }
        }

        private void Release(long childRuntimeId)
        {
            if (!_linksByChild.TryGetValue(childRuntimeId, out var link)) return;
            _linksByChild.Remove(childRuntimeId);
            var retain = link.Retain;
            _runtimes.ReleaseChild(in retain);
        }

        public void Dispose()
        {
            _runtimes.LifecycleHooks.Unregister(this);
            _linksByChild.Clear();
        }
    }

    internal readonly struct MobaDerivedSkillServiceSnapshot
    {
        public MobaDerivedSkillServiceSnapshot(MobaDerivedSkillLinkSnapshot[] entries)
        {
            Entries = entries ?? Array.Empty<MobaDerivedSkillLinkSnapshot>();
        }

        public MobaDerivedSkillLinkSnapshot[] Entries { get; }
    }

    internal readonly struct MobaDerivedSkillLinkSnapshot
    {
        public MobaDerivedSkillLinkSnapshot(long childRuntimeId, MobaSkillRuntimeRetainHandle retain, int depth)
        {
            ChildRuntimeId = childRuntimeId;
            Retain = retain;
            Depth = depth;
        }

        public long ChildRuntimeId { get; }
        public MobaSkillRuntimeRetainHandle Retain { get; }
        public int Depth { get; }
    }
}
