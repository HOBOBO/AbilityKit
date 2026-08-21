#if UNITY_EDITOR

#nullable enable

using System;
using System.Collections.Generic;
using System.Reflection;

namespace AbilityKit.Pipeline.Editor
{
    /// <summary>
    /// Editor 侧诊断存储。通过运行时调试钩子旁路观察，不替换运行时注册表或追踪器。
    /// </summary>
    public sealed class EditorPipelineRegistry
    {
        public static readonly EditorPipelineRegistry Instance = new EditorPipelineRegistry();

        private readonly List<DebugEntry> _entries = new List<DebugEntry>(64);
        private readonly object _lock = new object();
        private bool _isInitialized;

        private EditorPipelineRegistry()
        {
        }

        public event Action? Changed;

        public int HistoryCapacity { get; set; } = 128;
        public int TraceCapacity { get; set; } = 2048;
        public bool IsCaptureEnabled { get; set; } = true;
        public int? SelectedRunId { get; set; }

        public int ActiveCount
        {
            get
            {
                lock (_lock)
                {
                    int count = 0;
                    for (int i = 0; i < _entries.Count; i++)
                    {
                        if (_entries[i].IsActive) count++;
                    }
                    return count;
                }
            }
        }

        public readonly struct DebugStats
        {
            public DebugStats(int total, int active, int history, int failed, int pinned)
            {
                Total = total;
                Active = active;
                History = history;
                Failed = failed;
                Pinned = pinned;
            }

            public int Total { get; }
            public int Active { get; }
            public int History { get; }
            public int Failed { get; }
            public int Pinned { get; }
        }

        public sealed class DebugEntry
        {
            private readonly WeakReference _ownerRef;
            private readonly WeakReference _pipelineRef;
            private readonly WeakReference _configRef;
            private readonly WeakReference _runRef;
            private readonly WeakReference _contextRef;

            internal DebugEntry(PipelineRunStartedData data, int traceCapacity)
            {
                OwnerId = data.Owner.OwnerId;
                OwnerName = data.Owner.OwnerName ?? string.Empty;
                PipelineType = data.Pipeline.GetType().FullName ?? data.Pipeline.GetType().Name;
                ConfigType = data.Config.GetType().FullName ?? data.Config.GetType().Name;
                ContextType = data.Context.GetType().FullName ?? data.Context.GetType().Name;
                RegisteredAtUtc = data.UtcTime;
                LastState = data.Owner.State;
                LastPhaseId = data.Owner.CurrentPhaseId;
                IsPaused = data.Owner.IsPaused;
                IsActive = true;
                _ownerRef = new WeakReference(data.Owner);
                _pipelineRef = new WeakReference(data.Pipeline);
                _configRef = new WeakReference(data.Config);
                _runRef = new WeakReference(data.Run);
                _contextRef = new WeakReference(data.Context);
                Trace = new EditorPipelineRunTrace(traceCapacity);
                Graph = CaptureGraph(data.Pipeline);
                GraphLayout = CaptureGraphLayout(data.Config, data.Pipeline, Graph.StructureId, out bool layoutMismatch);
                HasGraphLayoutMismatch = layoutMismatch;
                TryRefreshLiveSnapshot();
                InitialContextValues = ContextValues;
            }

            public int OwnerId { get; }
            public string OwnerName { get; }
            public string PipelineType { get; }
            public string ConfigType { get; }
            public string ContextType { get; }
            public DateTime RegisteredAtUtc { get; }
            public DateTime? EndedAtUtc { get; internal set; }
            public EAbilityPipelineState LastState { get; internal set; }
            public AbilityPipelinePhaseId LastPhaseId { get; internal set; }
            public bool IsPaused { get; internal set; }
            public bool IsActive { get; internal set; }
            public bool IsPinned { get; internal set; }
            public float ElapsedTime { get; internal set; }
            public IReadOnlyList<AbilityPipelinePhaseId> ActivePhases { get; internal set; } = Array.Empty<AbilityPipelinePhaseId>();
            public PipelineDebugGraphSnapshot Graph { get; }
            public IReadOnlyList<PipelinePhaseDebugNode> PhaseTree => Graph.Roots;
            public PipelineDebugGraphLayout? GraphLayout { get; }
            public bool HasGraphLayoutMismatch { get; }
            public IReadOnlyList<PipelinePhaseDebugState> PhaseStates { get; internal set; } = Array.Empty<PipelinePhaseDebugState>();
            public IReadOnlyList<DebugValue> InitialContextValues { get; }
            public IReadOnlyList<DebugValue> ContextValues { get; internal set; } = Array.Empty<DebugValue>();
            public EditorPipelineRunTrace Trace { get; }

            public double WallDurationSeconds =>
                ((EndedAtUtc ?? DateTime.UtcNow) - RegisteredAtUtc).TotalSeconds;

            public bool TryGetOwner(out IPipelineLifeOwner? owner)
            {
                owner = _ownerRef.Target as IPipelineLifeOwner;
                return owner != null;
            }

            public bool TryGetControl(out IPipelineRunControl? control)
            {
                control = _runRef.Target as IPipelineRunControl;
                return control != null;
            }

            public bool TryGetContext(out IAbilityPipelineContext? context)
            {
                context = _contextRef.Target as IAbilityPipelineContext;
                return context != null;
            }

            public object? GetPipeline() => _pipelineRef.Target;
            public object? GetConfig() => _configRef.Target;
            public object? GetContext() => _contextRef.Target;
            public object? GetRun() => _runRef.Target;

            internal void RefreshLiveSnapshot()
            {
                if (TryGetOwner(out var owner) && owner != null)
                {
                    LastState = owner.State;
                    LastPhaseId = owner.CurrentPhaseId;
                    IsPaused = owner.IsPaused;
                    var active = owner.ActivePhases;
                    if (active == null || active.Count == 0)
                    {
                        ActivePhases = Array.Empty<AbilityPipelinePhaseId>();
                    }
                    else
                    {
                        var copy = new AbilityPipelinePhaseId[active.Count];
                        for (int i = 0; i < active.Count; i++) copy[i] = active[i];
                        ActivePhases = copy;
                    }
                }

                if (TryGetContext(out var context) && context != null)
                {
                    ElapsedTime = context.ElapsedTime;
                    ContextValues = CaptureContext(context);
                }

                if (_runRef.Target is IPipelineDebugStateProvider stateProvider)
                {
                    PhaseStates = stateProvider.CaptureDebugState().Nodes;
                }
            }

            internal void TryRefreshLiveSnapshot()
            {
                try { RefreshLiveSnapshot(); }
                catch { }
            }

            internal void Complete(PipelineRunEndedData data)
            {
                TryRefreshLiveSnapshot();
                LastState = data.State;
                LastPhaseId = data.LastPhaseId;
                EndedAtUtc = data.UtcTime;
                IsActive = false;
            }
        }

        public readonly struct DebugValue
        {
            public DebugValue(string name, string value)
            {
                Name = name ?? string.Empty;
                Value = value ?? string.Empty;
            }

            public string Name { get; }
            public string Value { get; }
        }

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
                SelectedRunId = null;
            }
            NotifyChanged();
        }

        public void CaptureRunStarted(PipelineRunStartedData data)
        {
            if (!_isInitialized || !IsCaptureEnabled || data.Owner == null) return;
            var entry = new DebugEntry(data, TraceCapacity);
            lock (_lock)
            {
                int index = FindEntryIndexUnsafe(entry.OwnerId);
                if (index >= 0) _entries[index] = entry;
                else _entries.Add(entry);
                SelectedRunId = entry.OwnerId;
                PruneHistoryUnsafe();
            }
            NotifyChanged();
        }

        public void CaptureTrace(IPipelineLifeOwner owner, PipelineTraceData data)
        {
            if (!_isInitialized || !IsCaptureEnabled || owner == null) return;
            lock (_lock)
            {
                int index = FindEntryIndexUnsafe(owner.OwnerId);
                if (index < 0) return;
                var entry = _entries[index];
                entry.Trace.AddTrace(data);
                entry.LastState = data.State;
                if (!string.IsNullOrEmpty(data.PhaseId.Value)) entry.LastPhaseId = data.PhaseId;
            }
            NotifyChanged();
        }

        public void CaptureRunEnded(PipelineRunEndedData data)
        {
            if (!_isInitialized || data.Owner == null) return;
            lock (_lock)
            {
                int index = FindEntryIndexUnsafe(data.Owner.OwnerId);
                if (index < 0) return;
                _entries[index].Complete(data);
                PruneHistoryUnsafe();
            }
            NotifyChanged();
        }

        public void Refresh()
        {
            bool changed = false;
            lock (_lock)
            {
                for (int i = 0; i < _entries.Count; i++)
                {
                    var entry = _entries[i];
                    if (!entry.IsActive) continue;
                    if (!entry.TryGetOwner(out _))
                    {
                        entry.IsActive = false;
                        entry.EndedAtUtc = DateTime.UtcNow;
                        changed = true;
                        continue;
                    }
                    entry.TryRefreshLiveSnapshot();
                }
                if (changed) PruneHistoryUnsafe();
            }
            if (changed) NotifyChanged();
        }

        public IReadOnlyList<DebugEntry> GetEntries()
        {
            lock (_lock)
            {
                var result = _entries.ToArray();
                Array.Sort(result, (left, right) => right.RegisteredAtUtc.CompareTo(left.RegisteredAtUtc));
                return result;
            }
        }

        public DebugStats GetStats()
        {
            lock (_lock)
            {
                int active = 0;
                int failed = 0;
                int pinned = 0;
                for (int i = 0; i < _entries.Count; i++)
                {
                    if (_entries[i].IsActive) active++;
                    if (_entries[i].LastState == EAbilityPipelineState.Failed) failed++;
                    if (_entries[i].IsPinned) pinned++;
                }

                return new DebugStats(_entries.Count, active, _entries.Count - active, failed, pinned);
            }
        }

        public bool TryGetEntry(int ownerId, out DebugEntry? entry)
        {
            lock (_lock)
            {
                int index = FindEntryIndexUnsafe(ownerId);
                entry = index >= 0 ? _entries[index] : null;
                return entry != null;
            }
        }

        public IReadOnlyList<PipelineTraceEvent> GetTraceSnapshot(int ownerId)
        {
            lock (_lock)
            {
                int index = FindEntryIndexUnsafe(ownerId);
                return index >= 0
                    ? _entries[index].Trace.GetSnapshot()
                    : Array.Empty<PipelineTraceEvent>();
            }
        }

        public void ClearTrace(int ownerId)
        {
            lock (_lock)
            {
                int index = FindEntryIndexUnsafe(ownerId);
                if (index >= 0) _entries[index].Trace.Clear();
            }
            NotifyChanged();
        }

        public void ClearHistory()
        {
            lock (_lock)
            {
                for (int i = _entries.Count - 1; i >= 0; i--)
                {
                    if (!_entries[i].IsActive && !_entries[i].IsPinned) _entries.RemoveAt(i);
                }
                if (SelectedRunId.HasValue && FindEntryIndexUnsafe(SelectedRunId.Value) < 0)
                {
                    SelectedRunId = null;
                }
            }
            NotifyChanged();
        }

        public void SetPinned(int ownerId, bool pinned)
        {
            bool changed = false;
            lock (_lock)
            {
                int index = FindEntryIndexUnsafe(ownerId);
                if (index >= 0 && _entries[index].IsPinned != pinned)
                {
                    _entries[index].IsPinned = pinned;
                    changed = true;
                }
            }
            if (changed) NotifyChanged();
        }

        public void ConfigureStorage(int historyCapacity, int traceCapacity)
        {
            HistoryCapacity = Math.Max(0, historyCapacity);
            TraceCapacity = Math.Max(16, traceCapacity);
            lock (_lock)
            {
                PruneHistoryUnsafe();
            }
            NotifyChanged();
        }

        public void MarkActiveRunsEnded()
        {
            lock (_lock)
            {
                for (int i = 0; i < _entries.Count; i++)
                {
                    if (!_entries[i].IsActive) continue;
                    _entries[i].TryRefreshLiveSnapshot();
                    _entries[i].IsActive = false;
                    _entries[i].EndedAtUtc = DateTime.UtcNow;
                }
                PruneHistoryUnsafe();
            }
            NotifyChanged();
        }

        private int FindEntryIndexUnsafe(int ownerId)
        {
            for (int i = 0; i < _entries.Count; i++)
            {
                if (_entries[i].OwnerId == ownerId) return i;
            }
            return -1;
        }

        private void PruneHistoryUnsafe()
        {
            int completedCount = 0;
            for (int i = 0; i < _entries.Count; i++)
            {
                if (!_entries[i].IsActive && !_entries[i].IsPinned) completedCount++;
            }

            int capacity = Math.Max(0, HistoryCapacity);
            while (completedCount > capacity)
            {
                int oldestIndex = -1;
                DateTime oldest = DateTime.MaxValue;
                for (int i = 0; i < _entries.Count; i++)
                {
                    if (_entries[i].IsActive || _entries[i].IsPinned || _entries[i].RegisteredAtUtc >= oldest) continue;
                    oldest = _entries[i].RegisteredAtUtc;
                    oldestIndex = i;
                }
                if (oldestIndex < 0) break;
                _entries.RemoveAt(oldestIndex);
                completedCount--;
            }
        }

        private void NotifyChanged()
        {
            var handlers = Changed;
            if (handlers == null) return;
            foreach (Action handler in handlers.GetInvocationList())
            {
                try { handler(); }
                catch { }
            }
        }

        private static IReadOnlyList<DebugValue> CaptureContext(IAbilityPipelineContext context)
        {
            var values = new List<DebugValue>(24)
            {
                new DebugValue(nameof(context.PipelineState), context.PipelineState.ToString()),
                new DebugValue(nameof(context.CurrentPhaseId), context.CurrentPhaseId.ToString()),
                new DebugValue(nameof(context.IsPaused), context.IsPaused.ToString()),
                new DebugValue(nameof(context.IsAborted), context.IsAborted.ToString()),
                new DebugValue(nameof(context.ElapsedTime), context.ElapsedTime.ToString("0.000")),
                new DebugValue(nameof(context.AbilityInstance), FormatValue(context.AbilityInstance))
            };

            var sharedData = context.SharedData;
            if (sharedData != null)
            {
                try
                {
                    foreach (var pair in sharedData)
                    {
                        values.Add(new DebugValue("SharedData." + pair.Key, FormatValue(pair.Value)));
                    }
                }
                catch (Exception exception)
                {
                    values.Add(new DebugValue("SharedData", "<" + exception.GetType().Name + ">"));
                }
            }

            var properties = context.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public);
            for (int i = 0; i < properties.Length && values.Count < 64; i++)
            {
                var property = properties[i];
                if (!property.CanRead || property.GetIndexParameters().Length != 0 || IsBuiltInContextProperty(property.Name)) continue;
                try
                {
                    values.Add(new DebugValue(property.Name, FormatValue(property.GetValue(context))));
                }
                catch (Exception exception)
                {
                    values.Add(new DebugValue(property.Name, "<" + exception.GetType().Name + ">"));
                }
            }
            return values;
        }

        private static bool IsBuiltInContextProperty(string name)
        {
            return name == nameof(IAbilityPipelineContext.PipelineState)
                || name == nameof(IAbilityPipelineContext.CurrentPhaseId)
                || name == nameof(IAbilityPipelineContext.IsPaused)
                || name == nameof(IAbilityPipelineContext.IsAborted)
                || name == nameof(IAbilityPipelineContext.ElapsedTime)
                || name == nameof(IAbilityPipelineContext.AbilityInstance)
                || name == nameof(IAbilityPipelineContext.SharedData);
        }

        private static string FormatValue(object? value)
        {
            if (value == null) return "null";
            try
            {
                var text = value.ToString() ?? value.GetType().Name;
                return text.Length <= 240 ? text : text.Substring(0, 237) + "...";
            }
            catch
            {
                return "<" + value.GetType().Name + ">";
            }
        }

        private static PipelineDebugGraphSnapshot CaptureGraph(object pipeline)
        {
            if (pipeline is IPipelineDebugGraphProvider graphProvider)
            {
                try
                {
                    return graphProvider.CaptureDebugGraph() ?? PipelineDebugGraphSnapshot.Empty;
                }
                catch
                {
                    return PipelineDebugGraphSnapshot.Empty;
                }
            }

            if (pipeline is IPipelineDebugStructureProvider structureProvider)
            {
                try
                {
                    var roots = structureProvider.CaptureDebugStructure() ?? Array.Empty<PipelinePhaseDebugNode>();
                    return BuildFallbackGraph(roots);
                }
                catch
                {
                    return PipelineDebugGraphSnapshot.Empty;
                }
            }

            return PipelineDebugGraphSnapshot.Empty;
        }

        private static PipelineDebugGraphSnapshot BuildFallbackGraph(IReadOnlyList<PipelinePhaseDebugNode> roots)
        {
            var normalizedRoots = new PipelinePhaseDebugNode[roots.Count];
            var edges = new List<PipelinePhaseDebugEdge>(roots.Count * 2);
            for (int i = 0; i < roots.Count; i++)
            {
                normalizedRoots[i] = NormalizeFallbackNode(roots[i], i.ToString(), edges);
                if (i > 0)
                {
                    edges.Add(new PipelinePhaseDebugEdge(
                        normalizedRoots[i - 1].NodeKey,
                        normalizedRoots[i].NodeKey,
                        EPipelineDebugEdgeKind.Flow));
                }
            }
            return new PipelineDebugGraphSnapshot(normalizedRoots, edges, string.Empty);
        }

        private static PipelinePhaseDebugNode NormalizeFallbackNode(
            PipelinePhaseDebugNode node,
            string nodeKey,
            ICollection<PipelinePhaseDebugEdge> edges)
        {
            var children = new PipelinePhaseDebugNode[node.Children.Count];
            for (int i = 0; i < node.Children.Count; i++)
            {
                string childKey = nodeKey + "/" + i;
                children[i] = NormalizeFallbackNode(node.Children[i], childKey, edges);
                edges.Add(new PipelinePhaseDebugEdge(nodeKey, childKey, EPipelineDebugEdgeKind.Child, string.Empty, i));
            }
            return new PipelinePhaseDebugNode(
                nodeKey,
                node.PhaseId,
                node.PhaseType,
                node.Kind,
                node.Summary,
                children);
        }

        private static PipelineDebugGraphLayout? CaptureGraphLayout(
            object config,
            object pipeline,
            string structureId,
            out bool mismatch)
        {
            mismatch = false;
            var provider = config as IPipelineDebugGraphLayoutProvider
                ?? pipeline as IPipelineDebugGraphLayoutProvider;
            if (provider == null) return null;

            try
            {
                var layout = provider.CaptureDebugGraphLayout();
                if (layout == null || layout.Nodes.Count == 0) return null;
                if (string.IsNullOrEmpty(structureId)
                    || string.IsNullOrEmpty(layout.StructureId)
                    || !string.Equals(structureId, layout.StructureId, StringComparison.Ordinal))
                {
                    mismatch = true;
                    return null;
                }
                return layout;
            }
            catch
            {
                mismatch = true;
                return null;
            }
        }
    }
}

#endif
