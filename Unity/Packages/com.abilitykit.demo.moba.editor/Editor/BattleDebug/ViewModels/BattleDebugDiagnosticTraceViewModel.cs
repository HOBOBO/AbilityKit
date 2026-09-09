using System;
using System.Collections.Generic;
using AbilityKit.Demo.Moba.Diagnostics;

namespace AbilityKit.Game.Editor
{
    internal enum BattleDebugTraceViewMode
    {
        Flow = 0,
        Issues = 1,
        Effects = 2,
        Active = 3
    }

    internal readonly struct BattleDebugDiagnosticTraceRow
    {
        public BattleDebugDiagnosticTraceRow(
            in BattleDiagnosticTraceNodeSummary node,
            int depth,
            bool isOrphan)
        {
            Node = node;
            Depth = depth < 0 ? 0 : depth;
            IsOrphan = isOrphan;
        }

        public BattleDiagnosticTraceNodeSummary Node { get; }
        public int Depth { get; }
        public bool IsOrphan { get; }
    }

    internal readonly struct BattleDebugDiagnosticTraceSummary
    {
        public BattleDebugDiagnosticTraceSummary(
            int nodeCount,
            int effectCount,
            int actionCount,
            int issueCount,
            int activeCount,
            int maximumDepth,
            int firstFrame,
            int lastFrame)
        {
            NodeCount = nodeCount;
            EffectCount = effectCount;
            ActionCount = actionCount;
            IssueCount = issueCount;
            ActiveCount = activeCount;
            MaximumDepth = maximumDepth;
            FirstFrame = firstFrame;
            LastFrame = lastFrame;
        }

        public int NodeCount { get; }
        public int EffectCount { get; }
        public int ActionCount { get; }
        public int IssueCount { get; }
        public int ActiveCount { get; }
        public int MaximumDepth { get; }
        public int FirstFrame { get; }
        public int LastFrame { get; }
    }

    internal sealed class BattleDebugDiagnosticTraceViewModel
    {
        private long _lastRequestId;
        private BattleDiagnosticSessionScope _lastScope;
        private long _lastStoreRevision = -1;
        private long _lastRootContextId;
        private bool _hasCachedResult;
        private IReadOnlyList<BattleDebugDiagnosticTraceRow> _rows =
            Array.Empty<BattleDebugDiagnosticTraceRow>();
        private IReadOnlyList<BattleDebugDiagnosticTraceRow> _visibleRows =
            Array.Empty<BattleDebugDiagnosticTraceRow>();
        private IReadOnlyList<BattleDiagnosticTraceNodeSummary> _selectedPath =
            Array.Empty<BattleDiagnosticTraceNodeSummary>();
        private Dictionary<long, BattleDiagnosticTraceNodeSummary> _nodesById =
            new Dictionary<long, BattleDiagnosticTraceNodeSummary>();
        private Dictionary<long, int> _childCounts = new Dictionary<long, int>();
        private readonly HashSet<long> _selectedPathIds = new HashSet<long>();
        private readonly HashSet<long> _collapsedContextIds = new HashSet<long>();
        private string _searchText = string.Empty;
        private IReadOnlyList<BattleDiagnosticTraceRootSummary> _rootSummaries =
            Array.Empty<BattleDiagnosticTraceRootSummary>();
        private BattleDiagnosticSessionScope _lastRootIndexScope;
        private long _lastRootIndexRevision = -1;
        private bool _hasCachedRootIndex;

        public IReadOnlyList<BattleDebugDiagnosticTraceRow> Rows => _rows;
        public IReadOnlyList<BattleDebugDiagnosticTraceRow> VisibleRows => _visibleRows;
        public IReadOnlyList<BattleDiagnosticTraceRootSummary> RootSummaries => _rootSummaries;
        public IReadOnlyList<BattleDiagnosticTraceNodeSummary> SelectedPath => _selectedPath;
        public BattleDiagnosticQueryStatus QueryStatus { get; private set; }
        public string StatusMessage { get; private set; } = string.Empty;
        public long StoreRevision => _lastStoreRevision;
        public long RootContextId => _lastRootContextId;
        public long SelectedContextId { get; private set; }
        public long PinnedContextId { get; private set; }
        public bool IsPinnedContextAvailable =>
            PinnedContextId != 0 && _nodesById.ContainsKey(PinnedContextId);
        public string SearchText => _searchText;
        public int SearchMatchCount { get; private set; }
        public int CollapsedBranchCount => _collapsedContextIds.Count;
        public bool FocusSelectedFlow { get; private set; }
        public BattleDebugTraceViewMode ViewMode { get; private set; }
        public BattleDebugDiagnosticTraceSummary Summary { get; private set; }
        public BattleDebugDiagnosticTraceSummary VisibleSummary { get; private set; }
        public BattleDiagnosticQueryStatus RootQueryStatus { get; private set; }

        public void InvalidateCache()
        {
            QueryStatus = default;
            StatusMessage = string.Empty;
            _lastStoreRevision = -1;
            _hasCachedResult = false;
        }

        public void InvalidateRootIndex()
        {
            _lastRootIndexRevision = -1;
            _hasCachedRootIndex = false;
            RootQueryStatus = default;
        }

        public void RefreshRootIndexIfNeeded(IBattleDiagnosticReadOnlySession session)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            if (!(session is IBattleDiagnosticTraceRootSession rootSession))
            {
                _rootSummaries = Array.Empty<BattleDiagnosticTraceRootSummary>();
                RootQueryStatus = BattleDiagnosticQueryStatus.Unavailable(
                    0,
                    session.TraceStoreRevision,
                    BattleDiagnosticDataAvailability.Unsupported,
                    "当前会话未提供 Trace 根节点发现能力。");
                return;
            }

            var scope = session.SessionInfo.Scope;
            var revision = session.TraceStoreRevision;
            if (_hasCachedRootIndex &&
                _lastRootIndexScope == scope &&
                _lastRootIndexRevision == revision)
            {
                return;
            }

            _lastRequestId++;
            if (_lastRequestId <= 0L) _lastRequestId = 1L;
            var result = rootSession.QueryTraceRoots(new BattleDiagnosticTraceRootQuery(
                _lastRequestId,
                new BattleDiagnosticPageRequest(0L, 0, 100)));
            _lastRootIndexScope = scope;
            _lastRootIndexRevision = revision;
            _hasCachedRootIndex = true;
            RootQueryStatus = result.Status;
            _rootSummaries = result.Status.CanDisplayResults
                ? result.Items
                : (IReadOnlyList<BattleDiagnosticTraceRootSummary>)Array.Empty<BattleDiagnosticTraceRootSummary>();
        }

        public void Clear()
        {
            _rows = Array.Empty<BattleDebugDiagnosticTraceRow>();
            _visibleRows = Array.Empty<BattleDebugDiagnosticTraceRow>();
            _selectedPath = Array.Empty<BattleDiagnosticTraceNodeSummary>();
            _nodesById = new Dictionary<long, BattleDiagnosticTraceNodeSummary>();
            _childCounts = new Dictionary<long, int>();
            _selectedPathIds.Clear();
            _collapsedContextIds.Clear();
            _searchText = string.Empty;
            _lastRootContextId = 0;
            SelectedContextId = 0;
            PinnedContextId = 0;
            SearchMatchCount = 0;
            FocusSelectedFlow = false;
            ViewMode = BattleDebugTraceViewMode.Flow;
            Summary = default;
            VisibleSummary = default;
            QueryStatus = default;
            StatusMessage = string.Empty;
            InvalidateCache();
        }

        public void RefreshIfNeeded(
            IBattleDiagnosticReadOnlySession session,
            long rootContextId)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            if (rootContextId <= 0) throw new ArgumentOutOfRangeException(nameof(rootContextId));

            var scope = session.SessionInfo.Scope;
            var revision = session.TraceStoreRevision;
            if (_hasCachedResult &&
                _lastScope == scope &&
                _lastStoreRevision == revision &&
                _lastRootContextId == rootContextId)
            {
                return;
            }

            _lastRequestId++;
            if (_lastRequestId <= 0) _lastRequestId = 1;

            var result = session.QueryTrace(_lastRequestId, rootContextId);
            QueryStatus = result.Status;
            _lastScope = scope;
            _lastStoreRevision = revision;
            _lastRootContextId = rootContextId;
            _hasCachedResult = true;

            if (!result.Status.CanDisplayResults)
            {
                _rows = Array.Empty<BattleDebugDiagnosticTraceRow>();
                _visibleRows = Array.Empty<BattleDebugDiagnosticTraceRow>();
                _nodesById = new Dictionary<long, BattleDiagnosticTraceNodeSummary>();
                _childCounts = new Dictionary<long, int>();
                _selectedPathIds.Clear();
                Summary = default;
                VisibleSummary = default;
                SelectedContextId = 0;
                _selectedPath = Array.Empty<BattleDiagnosticTraceNodeSummary>();
                SearchMatchCount = 0;
                StatusMessage = BuildStatusMessage(result.Status);
                return;
            }

            ProjectRows(result.Items);
            StatusMessage = result.Status.HasMore
                ? "Trace 数据已截断，当前树可能不完整。"
                : string.Empty;

            if (SelectedContextId == 0 || !_nodesById.ContainsKey(SelectedContextId))
            {
                SelectedContextId = _nodesById.ContainsKey(rootContextId)
                    ? rootContextId
                    : (_rows.Count > 0 ? _rows[0].Node.ContextId : 0);
            }

            RebuildSelectedPath();
            RebuildVisibleRows();
        }

        public bool SelectContext(long contextId)
        {
            if (contextId == 0 || !_nodesById.ContainsKey(contextId)) return false;
            if (SelectedContextId == contextId) return true;

            SelectedContextId = contextId;
            RebuildSelectedPath();
            if (FocusSelectedFlow) RebuildVisibleRows();
            return true;
        }

        public void SetFocusSelectedFlow(bool value)
        {
            if (FocusSelectedFlow == value) return;
            FocusSelectedFlow = value;
            RebuildVisibleRows();
        }

        public void SetViewMode(BattleDebugTraceViewMode value)
        {
            if (!Enum.IsDefined(typeof(BattleDebugTraceViewMode), value))
                throw new ArgumentOutOfRangeException(nameof(value));
            if (ViewMode == value) return;
            ViewMode = value;
            RebuildVisibleRows();
        }

        public void SetSearchText(string searchText)
        {
            searchText = searchText?.Trim() ?? string.Empty;
            if (string.Equals(_searchText, searchText, StringComparison.Ordinal)) return;

            _searchText = searchText;
            RebuildVisibleRows();
        }

        public bool IsSearchMatch(long contextId)
        {
            return _nodesById.TryGetValue(contextId, out var node) && MatchesSearch(in node);
        }

        public bool SelectSearchMatch(int direction)
        {
            if (SearchMatchCount == 0 || direction == 0) return false;

            var selectedMatchIndex = -1;
            var matchCount = 0;
            for (var i = 0; i < _visibleRows.Count; i++)
            {
                var node = _visibleRows[i].Node;
                if (!MatchesSearch(in node)) continue;
                if (node.ContextId == SelectedContextId) selectedMatchIndex = matchCount;
                matchCount++;
            }

            var targetMatchIndex = selectedMatchIndex < 0
                ? (direction > 0 ? 0 : matchCount - 1)
                : (selectedMatchIndex + (direction > 0 ? 1 : -1) + matchCount) % matchCount;
            for (var i = 0; i < _visibleRows.Count; i++)
            {
                var node = _visibleRows[i].Node;
                if (!MatchesSearch(in node)) continue;
                if (targetMatchIndex-- == 0) return SelectContext(node.ContextId);
            }

            return false;
        }

        public bool HasChildren(long contextId)
        {
            return GetChildCount(contextId) > 0;
        }

        public int GetChildCount(long contextId)
        {
            return _childCounts.TryGetValue(contextId, out var count) ? count : 0;
        }

        public bool IsOnSelectedPath(long contextId) => _selectedPathIds.Contains(contextId);

        public bool IsCollapsed(long contextId) => _collapsedContextIds.Contains(contextId);

        public void ToggleCollapsed(long contextId)
        {
            if (!HasChildren(contextId)) return;
            if (!_collapsedContextIds.Add(contextId)) _collapsedContextIds.Remove(contextId);
            RebuildVisibleRows();
        }

        public void ExpandAll()
        {
            if (_collapsedContextIds.Count == 0) return;
            _collapsedContextIds.Clear();
            RebuildVisibleRows();
        }

        public void CollapseAllPreservingSelection()
        {
            _collapsedContextIds.Clear();
            var selectedPathIds = new HashSet<long>();
            for (var i = 0; i < _selectedPath.Count - 1; i++)
            {
                selectedPathIds.Add(_selectedPath[i].ContextId);
            }

            for (var i = 0; i < _rows.Count; i++)
            {
                var contextId = _rows[i].Node.ContextId;
                if (!selectedPathIds.Contains(contextId) && HasChildren(contextId))
                {
                    _collapsedContextIds.Add(contextId);
                }
            }

            RebuildVisibleRows();
        }

        public int GetVisibleRowIndex(long contextId)
        {
            for (var i = 0; i < _visibleRows.Count; i++)
            {
                if (_visibleRows[i].Node.ContextId == contextId) return i;
            }

            return -1;
        }

        public void PinSelection()
        {
            if (SelectedContextId != 0) PinnedContextId = SelectedContextId;
        }

        public void ClearPin()
        {
            PinnedContextId = 0;
        }

        public bool SelectPinned()
        {
            return IsPinnedContextAvailable && SelectContext(PinnedContextId);
        }

        private void ProjectRows(IReadOnlyList<BattleDiagnosticTraceNodeSummary> nodes)
        {
            _nodesById = new Dictionary<long, BattleDiagnosticTraceNodeSummary>(nodes?.Count ?? 0);
            _childCounts = new Dictionary<long, int>();
            if (nodes != null)
            {
                for (var i = 0; i < nodes.Count; i++)
                {
                    var node = nodes[i];
                    _nodesById[node.ContextId] = node;
                    if (node.ParentContextId != 0)
                    {
                        _childCounts.TryGetValue(node.ParentContextId, out var childCount);
                        _childCounts[node.ParentContextId] = childCount + 1;
                    }
                }
            }

            if (nodes == null || nodes.Count == 0)
            {
                _rows = Array.Empty<BattleDebugDiagnosticTraceRow>();
                Summary = default;
                return;
            }

            var rows = new List<BattleDebugDiagnosticTraceRow>(nodes.Count);
            var depthCache = new Dictionary<long, int>(nodes.Count);
            for (var i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                var orphan = node.ParentContextId != 0 && !_nodesById.ContainsKey(node.ParentContextId);
                var depth = ResolveDepth(node.ContextId, depthCache, new HashSet<long>());
                rows.Add(new BattleDebugDiagnosticTraceRow(in node, depth, orphan));
            }

            _rows = rows;
            Summary = BuildSummary(rows);
        }

        private int ResolveDepth(
            long contextId,
            IDictionary<long, int> depthCache,
            ISet<long> visiting)
        {
            if (depthCache.TryGetValue(contextId, out var cached)) return cached;
            if (!_nodesById.TryGetValue(contextId, out var node)) return 0;
            if (!visiting.Add(contextId)) return 0;

            var depth = 0;
            if (node.ParentContextId != 0 && _nodesById.ContainsKey(node.ParentContextId))
            {
                depth = ResolveDepth(node.ParentContextId, depthCache, visiting) + 1;
            }

            visiting.Remove(contextId);
            depthCache[contextId] = depth;
            return depth;
        }

        private void RebuildSelectedPath()
        {
            if (SelectedContextId == 0 || !_nodesById.ContainsKey(SelectedContextId))
            {
                _selectedPath = Array.Empty<BattleDiagnosticTraceNodeSummary>();
                _selectedPathIds.Clear();
                return;
            }

            var reversed = new List<BattleDiagnosticTraceNodeSummary>();
            var visited = new HashSet<long>();
            var currentId = SelectedContextId;
            while (currentId != 0 &&
                   visited.Add(currentId) &&
                   _nodesById.TryGetValue(currentId, out var node))
            {
                reversed.Add(node);
                currentId = node.ParentContextId;
            }

            reversed.Reverse();
            _selectedPath = reversed;
            _selectedPathIds.Clear();
            for (var i = 0; i < reversed.Count; i++)
            {
                _selectedPathIds.Add(reversed[i].ContextId);
            }
        }

        private void RebuildVisibleRows()
        {
            if (_rows.Count == 0)
            {
                _visibleRows = Array.Empty<BattleDebugDiagnosticTraceRow>();
                SearchMatchCount = 0;
                VisibleSummary = default;
                return;
            }

            var hasSearch = !string.IsNullOrEmpty(_searchText);
            var includedByMode = ViewMode == BattleDebugTraceViewMode.Flow
                ? null
                : BuildIncludedWithAncestors(MatchesViewMode);
            HashSet<long> includedBySearch = null;
            if (hasSearch)
            {
                includedBySearch = new HashSet<long>();
                SearchMatchCount = 0;
                for (var i = 0; i < _rows.Count; i++)
                {
                    var node = _rows[i].Node;
                    if (!MatchesSearch(in node)) continue;

                    var currentId = node.ContextId;
                    var visited = new HashSet<long>();
                    while (currentId != 0 &&
                           visited.Add(currentId) &&
                           _nodesById.TryGetValue(currentId, out var current))
                    {
                        includedBySearch.Add(currentId);
                        currentId = current.ParentContextId;
                    }
                }
            }
            else
            {
                SearchMatchCount = 0;
            }

            var visible = new List<BattleDebugDiagnosticTraceRow>(_rows.Count);
            for (var i = 0; i < _rows.Count; i++)
            {
                var row = _rows[i];
                if (hasSearch)
                {
                    if (includedBySearch.Contains(row.Node.ContextId) &&
                        (includedByMode == null || includedByMode.Contains(row.Node.ContextId)) &&
                        (!FocusSelectedFlow || IsInSelectedFlow(row.Node.ContextId)))
                    {
                        visible.Add(row);
                    }
                    continue;
                }

                if (includedByMode != null && !includedByMode.Contains(row.Node.ContextId)) continue;
                if (FocusSelectedFlow && !IsInSelectedFlow(row.Node.ContextId)) continue;
                if (!HasCollapsedAncestor(row.Node.ParentContextId)) visible.Add(row);
            }

            _visibleRows = visible;
            if (hasSearch)
            {
                SearchMatchCount = 0;
                for (var i = 0; i < visible.Count; i++)
                {
                    var node = visible[i].Node;
                    if (MatchesSearch(in node)) SearchMatchCount++;
                }
            }
            VisibleSummary = visible.Count > 0 ? BuildSummary(visible) : default;
        }

        private HashSet<long> BuildIncludedWithAncestors(
            Func<BattleDiagnosticTraceNodeSummary, bool> predicate)
        {
            var included = new HashSet<long>();
            for (var i = 0; i < _rows.Count; i++)
            {
                var node = _rows[i].Node;
                if (!predicate(node)) continue;

                var currentId = node.ContextId;
                var visited = new HashSet<long>();
                while (currentId != 0 &&
                       visited.Add(currentId) &&
                       _nodesById.TryGetValue(currentId, out var current))
                {
                    included.Add(currentId);
                    currentId = current.ParentContextId;
                }
            }
            return included;
        }

        private bool MatchesViewMode(BattleDiagnosticTraceNodeSummary node)
        {
            switch (ViewMode)
            {
                case BattleDebugTraceViewMode.Issues:
                    return node.State == BattleDiagnosticTraceNodeState.Failed ||
                           node.State == BattleDiagnosticTraceNodeState.ForceEnded;
                case BattleDebugTraceViewMode.Effects:
                    return string.Equals(node.Kind, "SkillCast", StringComparison.Ordinal) ||
                           string.Equals(node.Kind, "SkillPhase", StringComparison.Ordinal) ||
                           string.Equals(node.Kind, "SkillEffect", StringComparison.Ordinal) ||
                           string.Equals(node.Kind, "EffectExecution", StringComparison.Ordinal) ||
                           string.Equals(node.Kind, "EffectAction", StringComparison.Ordinal);
                case BattleDebugTraceViewMode.Active:
                    return node.State == BattleDiagnosticTraceNodeState.Active;
                default:
                    return true;
            }
        }

        private bool HasCollapsedAncestor(long contextId)
        {
            var visited = new HashSet<long>();
            while (contextId != 0 &&
                   visited.Add(contextId) &&
                   _nodesById.TryGetValue(contextId, out var node))
            {
                if (_collapsedContextIds.Contains(contextId)) return true;
                contextId = node.ParentContextId;
            }

            return false;
        }

        private bool IsInSelectedFlow(long contextId)
        {
            if (SelectedContextId == 0 || _selectedPathIds.Contains(contextId)) return true;

            var visited = new HashSet<long>();
            while (contextId != 0 &&
                   visited.Add(contextId) &&
                   _nodesById.TryGetValue(contextId, out var node))
            {
                if (contextId == SelectedContextId) return true;
                contextId = node.ParentContextId;
            }

            return false;
        }

        private bool MatchesSearch(in BattleDiagnosticTraceNodeSummary node)
        {
            if (string.IsNullOrEmpty(_searchText)) return false;

            return Contains(node.Kind, _searchText) ||
                   Contains(node.State.ToString(), _searchText) ||
                   Contains(node.EndReason, _searchText) ||
                   Contains(node.ContextId.ToString(), _searchText) ||
                   (node.ActorId != 0 && Contains(node.ActorId.ToString(), _searchText)) ||
                   (node.TargetActorId != 0 && Contains(node.TargetActorId.ToString(), _searchText)) ||
                   (node.ConfigId != 0 && Contains(node.ConfigId.ToString(), _searchText)) ||
                   (node.TriggerId != 0 && Contains(node.TriggerId.ToString(), _searchText)) ||
                   (node.SkillId != 0 && Contains(node.SkillId.ToString(), _searchText)) ||
                   (node.CastFlowId != 0 && Contains(node.CastFlowId.ToString(), _searchText)) ||
                   Contains(node.PhaseId, _searchText);
        }

        private static BattleDebugDiagnosticTraceSummary BuildSummary(
            IReadOnlyList<BattleDebugDiagnosticTraceRow> rows)
        {
            var effects = 0;
            var actions = 0;
            var issues = 0;
            var active = 0;
            var maximumDepth = 0;
            var firstFrame = rows[0].Node.StartFrame;
            var lastFrame = rows[0].Node.EndFrame;
            for (var i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                var node = row.Node;
                if (string.Equals(node.Kind, "SkillEffect", StringComparison.Ordinal) ||
                    string.Equals(node.Kind, "EffectExecution", StringComparison.Ordinal))
                {
                    effects++;
                }
                if (string.Equals(node.Kind, "EffectAction", StringComparison.Ordinal)) actions++;
                if (node.State == BattleDiagnosticTraceNodeState.Failed ||
                    node.State == BattleDiagnosticTraceNodeState.ForceEnded)
                {
                    issues++;
                }
                if (node.State == BattleDiagnosticTraceNodeState.Active) active++;
                if (row.Depth > maximumDepth) maximumDepth = row.Depth;
                if (node.StartFrame < firstFrame) firstFrame = node.StartFrame;
                if (node.EndFrame > lastFrame) lastFrame = node.EndFrame;
            }

            return new BattleDebugDiagnosticTraceSummary(
                rows.Count,
                effects,
                actions,
                issues,
                active,
                maximumDepth,
                firstFrame,
                lastFrame);
        }

        private static bool Contains(string value, string searchText)
        {
            return !string.IsNullOrEmpty(value) &&
                   value.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string BuildStatusMessage(BattleDiagnosticQueryStatus status)
        {
            if (status.Phase == BattleDiagnosticQueryPhase.Empty)
            {
                return "Trace 树为空。";
            }

            return $"Trace 数据不可用：{BattleDebugDisplayText.Availability(status.Availability)} {status.Message}";
        }
    }
}
