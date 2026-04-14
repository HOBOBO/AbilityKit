#if UNITY_EDITOR

using System;
using System.Collections.Generic;

namespace AbilityKit.Pipeline.Editor
{
    /// <summary>
    /// 管线生命周期注册表 Editor 调试版
    /// 包含调试专用功能：追踪器、快照、选中状态
    /// </summary>
    public sealed class EditorPipelineRegistry : IPipelineRegistry
    {
        public static readonly EditorPipelineRegistry Instance = new EditorPipelineRegistry();

        private readonly List<DebugEntry> _entries = new List<DebugEntry>(64);
        private bool _isInitialized;
        private readonly object _lock = new object();

        /// <summary>
        /// 调试条目（包含追踪器）
        /// </summary>
        public sealed class DebugEntry
        {
            public readonly int OwnerId;
            public readonly string OwnerName;
            public readonly WeakReference OwnerRef;
            public readonly DateTime RegisteredAt;
            public readonly EditorPipelineRunTrace Trace;

            public EAbilityPipelineState LastState;
            public AbilityPipelinePhaseId LastPhaseId;

            public DebugEntry(IPipelineLifeOwner owner)
            {
                OwnerId = owner.OwnerId;
                OwnerName = owner.OwnerName ?? string.Empty;
                OwnerRef = new WeakReference(owner);
                RegisteredAt = DateTime.UtcNow;
                LastState = owner.State;
                LastPhaseId = owner.CurrentPhaseId;
                Trace = new EditorPipelineRunTrace(2048);
            }

            public bool IsAlive => OwnerRef.Target != null;

            public IPipelineLifeOwner GetOwner()
            {
                return OwnerRef.Target as IPipelineLifeOwner;
            }
        }

        public int ActiveCount => GetActiveEntries().Count;

        /// <summary>
        /// 当前选中的运行实例（调试专用）
        /// </summary>
        public object SelectedRun { get; set; }

        public void Initialize()
        {
            _isInitialized = true;
        }

        public void Shutdown()
        {
            _isInitialized = false;
            lock (_lock)
            {
                _entries.Clear();
            }
            SelectedRun = null;
        }

        public void Register(IPipelineLifeOwner owner)
        {
            if (!_isInitialized || owner == null) return;

            lock (_lock)
            {
                CleanupDeadEntries();

                int existingIndex = FindEntryIndex_Unsafe(owner.OwnerId);
                DebugEntry entry;

                if (existingIndex >= 0)
                {
                    entry = new DebugEntry(owner);
                    _entries[existingIndex] = entry;
                }
                else
                {
                    entry = new DebugEntry(owner);
                    _entries.Add(entry);
                }

                if (SelectedRun == null) SelectedRun = owner;
            }

            PipelineRegistryEvents.OnRunStarted?.Invoke(owner);
            PipelineRegistryEvents.OnChanged?.Invoke();
        }

        public void Unregister(IPipelineLifeOwner owner)
        {
            if (owner == null) return;

            DebugEntry entry = null;
            lock (_lock)
            {
                int index = FindEntryIndex_Unsafe(owner.OwnerId);
                if (index < 0) return;

                entry = _entries[index];
                _entries.RemoveAt(index);

                if (SelectedRun == owner) SelectedRun = null;
            }

            PipelineRegistryEvents.OnRunEnded?.Invoke(owner, entry.LastState);
            PipelineRegistryEvents.OnChanged?.Invoke();
        }

        public IReadOnlyList<IPipelineLifeOwner> GetActiveOwners()
        {
            var result = new List<IPipelineLifeOwner>();
            lock (_lock)
            {
                CleanupDeadEntries();
                for (int i = 0; i < _entries.Count; i++)
                {
                    if (_entries[i].IsAlive)
                    {
                        result.Add(_entries[i].GetOwner());
                    }
                }
            }
            return result;
        }

        public void InterruptAll()
        {
            PipelineRegistryEvents.OnGlobalInterrupt?.Invoke();

            lock (_lock)
            {
                for (int i = 0; i < _entries.Count; i++)
                {
                    if (_entries[i].IsAlive && _entries[i].GetOwner() is IPipelineInterruptible interruptible)
                    {
                        interruptible.Interrupt();
                    }
                }
            }
        }

        public IReadOnlyList<IPipelineLifeOwner> GetOwnersByPhase(AbilityPipelinePhaseId phaseId)
        {
            var result = new List<IPipelineLifeOwner>();
            lock (_lock)
            {
                for (int i = 0; i < _entries.Count; i++)
                {
                    if (_entries[i].IsAlive && _entries[i].LastPhaseId == phaseId)
                    {
                        result.Add(_entries[i].GetOwner());
                    }
                }
            }
            return result;
        }

        public IReadOnlyList<IPipelineLifeOwner> GetOwnersByState(EAbilityPipelineState state)
        {
            var result = new List<IPipelineLifeOwner>();
            lock (_lock)
            {
                for (int i = 0; i < _entries.Count; i++)
                {
                    if (_entries[i].IsAlive && _entries[i].LastState == state)
                    {
                        result.Add(_entries[i].GetOwner());
                    }
                }
            }
            return result;
        }

        public EditorPipelineRunTrace GetTrace(IPipelineLifeOwner owner)
        {
            lock (_lock)
            {
                int index = FindEntryIndex_Unsafe(owner.OwnerId);
                return index >= 0 ? _entries[index].Trace : null;
            }
        }

        public bool TryGetOwner(int ownerId, out IPipelineLifeOwner owner)
        {
            owner = null;
            lock (_lock)
            {
                int index = FindEntryIndex_Unsafe(ownerId);
                if (index < 0) return false;
                var entry = _entries[index];
                if (!entry.IsAlive) return false;
                owner = entry.GetOwner();
                return true;
            }
        }

        private int FindEntryIndex_Unsafe(int ownerId)
        {
            for (int i = 0; i < _entries.Count; i++)
            {
                if (_entries[i].OwnerId == ownerId)
                    return i;
            }
            return -1;
        }

        private void CleanupDeadEntries()
        {
            bool changed = false;
            for (int i = _entries.Count - 1; i >= 0; i--)
            {
                if (!_entries[i].IsAlive)
                {
                    _entries.RemoveAt(i);
                    changed = true;
                }
            }

            if (SelectedRun != null && !IsRunStillExists(SelectedRun))
            {
                SelectedRun = null;
                changed = true;
            }

            if (changed) PipelineRegistryEvents.OnChanged?.Invoke();
        }

        private bool IsRunStillExists(object run)
        {
            foreach (var entry in _entries)
            {
                if (ReferenceEquals(entry.OwnerRef.Target, run)) return true;
            }
            return false;
        }

        private List<DebugEntry> GetActiveEntries()
        {
            CleanupDeadEntries();
            return _entries;
        }
    }
}

#endif
