using System;
using System.Collections.Generic;
using AbilityKit.Demo.Moba.Diagnostics;

namespace AbilityKit.Game.Editor
{
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
        private readonly HashSet<long> _collapsedContextIds = new HashSet<long>();
        private string _searchText = string.Empty;

        public IReadOnlyList<BattleDebugDiagnosticTraceRow> Rows => _rows;
        public IReadOnlyList<BattleDebugDiagnosticTraceRow> VisibleRows => _visibleRows;
        public IReadOnlyList<BattleDiagnosticTraceNodeSummary> SelectedPath => _selectedPath;
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

        public void InvalidateCache()
        {
            _lastStoreRevision = -1;
            _hasCachedResult = false;
        }

        public void Clear()
        {
            _rows = Array.Empty<BattleDebugDiagnosticTraceRow>();
            _visibleRows = Array.Empty<BattleDebugDiagnosticTraceRow>();
            _selectedPath = Array.Empty<BattleDiagnosticTraceNodeSummary>();
            _nodesById = new Dictionary<long, BattleDiagnosticTraceNodeSummary>();
            _collapsedContextIds.Clear();
            _searchText = string.Empty;
            _lastRootContextId = 0;
            SelectedContextId = 0;
            PinnedContextId = 0;
            SearchMatchCount = 0;
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
            _lastScope = scope;
            _lastStoreRevision = revision;
            _lastRootContextId = rootContextId;
            _hasCachedResult = true;

            if (!result.Status.CanDisplayResults)
            {
                _rows = Array.Empty<BattleDebugDiagnosticTraceRow>();
                _visibleRows = Array.Empty<BattleDebugDiagnosticTraceRow>();
                _nodesById = new Dictionary<long, BattleDiagnosticTraceNodeSummary>();
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
            return true;
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
            for (var i = 0; i < _rows.Count; i++)
            {
                var node = _rows[i].Node;
                if (!MatchesSearch(in node)) continue;
                if (node.ContextId == SelectedContextId) selectedMatchIndex = matchCount;
                matchCount++;
            }

            var targetMatchIndex = selectedMatchIndex < 0
                ? (direction > 0 ? 0 : matchCount - 1)
                : (selectedMatchIndex + (direction > 0 ? 1 : -1) + matchCount) % matchCount;
            for (var i = 0; i < _rows.Count; i++)
            {
                var node = _rows[i].Node;
                if (!MatchesSearch(in node)) continue;
                if (targetMatchIndex-- == 0) return SelectContext(node.ContextId);
            }

            return false;
        }

        public bool HasChildren(long contextId)
        {
            for (var i = 0; i < _rows.Count; i++)
            {
                if (_rows[i].Node.ParentContextId == contextId) return true;
            }

            return false;
        }

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
            if (nodes != null)
            {
                for (var i = 0; i < nodes.Count; i++)
                {
                    var node = nodes[i];
                    _nodesById[node.ContextId] = node;
                }
            }

            if (nodes == null || nodes.Count == 0)
            {
                _rows = Array.Empty<BattleDebugDiagnosticTraceRow>();
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
        }

        private void RebuildVisibleRows()
        {
            if (_rows.Count == 0)
            {
                _visibleRows = Array.Empty<BattleDebugDiagnosticTraceRow>();
                SearchMatchCount = 0;
                return;
            }

            var hasSearch = !string.IsNullOrEmpty(_searchText);
            HashSet<long> includedBySearch = null;
            if (hasSearch)
            {
                includedBySearch = new HashSet<long>();
                SearchMatchCount = 0;
                for (var i = 0; i < _rows.Count; i++)
                {
                    var node = _rows[i].Node;
                    if (!MatchesSearch(in node)) continue;

                    SearchMatchCount++;
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
                    if (includedBySearch.Contains(row.Node.ContextId)) visible.Add(row);
                    continue;
                }

                if (!HasCollapsedAncestor(row.Node.ParentContextId)) visible.Add(row);
            }

            _visibleRows = visible;
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

        private bool MatchesSearch(in BattleDiagnosticTraceNodeSummary node)
        {
            if (string.IsNullOrEmpty(_searchText)) return false;

            return Contains(node.Kind, _searchText) ||
                   Contains(node.State.ToString(), _searchText) ||
                   Contains(node.EndReason, _searchText) ||
                   Contains(node.ContextId.ToString(), _searchText) ||
                   Contains(node.ActorId.ToString(), _searchText) ||
                   Contains(node.ConfigId.ToString(), _searchText);
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

            return $"Trace 数据不可用：{status.Availability} {status.Message}";
        }
    }
}
