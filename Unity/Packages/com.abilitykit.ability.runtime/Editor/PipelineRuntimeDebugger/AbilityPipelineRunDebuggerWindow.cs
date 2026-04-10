#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using AbilityKit.Core.Generic;
using AbilityKit.Pipeline;
using AbilityKit.Pipeline.Editor;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Ability.Editor
{
    public sealed class AbilityPipelineRunDebuggerWindow : EditorWindow
    {
        [MenuItem("Window/AbilityKit/Ability Pipeline Run Debugger")]
        static void Open()
        {
            GetWindow<AbilityPipelineRunDebuggerWindow>(utility: false, title: "Pipeline Run Debugger");
        }

        private int _selectedIndex;
        private readonly List<PipelineTraceEvent> _trace = new List<PipelineTraceEvent>(256);
        private Vector2 _scroll;

        private bool _followSelectedRun = true;
        private bool _autoFocusPhase = true;
        private object _lastRunObj;
        private int _lastTraceCount;
        private string _lastPhaseKey = string.Empty;
        private int _pendingScrollToIndex = -1;
        private int _selectedTraceIndex = -1;

        private bool _lockGlobalSelectedRun;

        private bool _autoScroll = true;
        private bool _filterRunStart = true;
        private bool _filterRunEnd = true;
        private bool _filterPhaseStart = true;
        private bool _filterPhaseComplete = true;
        private bool _filterPhaseError = true;
        private bool _filterTick;
        private string _textFilter = string.Empty;

        private PipelineGraphAsset _graphAsset;
        private Vector2 _graphScroll;
        private string _nodeFilter = string.Empty;
        private string _focusRuntimeKey = string.Empty;

        private Vector2 _graphPan;
        private float _graphZoom = 1f;
        private bool _isPanning;
        private bool _showRuntimeKey;
        private bool _showOnlyConnectedToFocus;
        private Vector2 _lastCanvasSize;

        void OnEnable()
        {
            AbilityPipelineLiveRegistry.Changed += Repaint;
        }

        void OnDisable()
        {
            AbilityPipelineLiveRegistry.Changed -= Repaint;
        }

        void OnGUI()
        {
            using (new EditorGUI.DisabledScope(!EditorApplication.isPlaying))
            {
                EditorGUILayout.LabelField("Play Mode Only", EditorStyles.boldLabel);
                EditorGUILayout.Space(6);

                var entries = AbilityPipelineLiveRegistry.GetEntries();
                if (entries.Count == 0)
                {
                    EditorGUILayout.HelpBox("No running pipeline runs registered.", MessageType.Info);
                    return;
                }

                var names = new string[entries.Count];
                for (int i = 0; i < entries.Count; i++)
                {
                    names[i] = $"{i}: {entries[i].Name}";
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    _followSelectedRun = EditorGUILayout.ToggleLeft("Follow Selected Run", _followSelectedRun, GUILayout.Width(160));
                    _autoFocusPhase = EditorGUILayout.ToggleLeft("Auto Focus Phase", _autoFocusPhase, GUILayout.Width(140));
                }

                _lockGlobalSelectedRun = EditorGUILayout.ToggleLeft("Lock Global SelectedRun", _lockGlobalSelectedRun);
                if (_lockGlobalSelectedRun)
                {
                    _followSelectedRun = true;
                }

                if (_followSelectedRun)
                {
                    var desired = AbilityPipelineLiveRegistry.SelectedRun;
                    if (desired != null)
                    {
                        for (int i = 0; i < entries.Count; i++)
                        {
                            if (ReferenceEquals(entries[i].Run.Target, desired))
                            {
                                _selectedIndex = i;
                                break;
                            }
                        }
                    }
                    else
                    {
                        // Default to the most recently registered run.
                        _selectedIndex = Mathf.Clamp(_selectedIndex, 0, entries.Count - 1);
                    }
                }

                _selectedIndex = Mathf.Clamp(_selectedIndex, 0, names.Length - 1);
                var nextIndex = EditorGUILayout.Popup("Running Run", _selectedIndex, names);
                if (nextIndex != _selectedIndex)
                {
                    _selectedIndex = nextIndex;
                    var nextRun = entries[_selectedIndex].Run.Target;
                    if (_followSelectedRun && nextRun != null)
                    {
                        AbilityPipelineLiveRegistry.SelectedRun = nextRun;
                    }

                    _lastRunObj = null;
                    _lastTraceCount = 0;
                    _lastPhaseKey = string.Empty;
                    _pendingScrollToIndex = -1;
                    _selectedTraceIndex = -1;
                }

                var selected = entries[_selectedIndex];
                var runObj = selected.Run.Target;
                if (runObj == null)
                {
                    EditorGUILayout.HelpBox("Selected run instance is no longer alive.", MessageType.Warning);
                    return;
                }

                // Global selection info.
                var global = AbilityPipelineLiveRegistry.SelectedRun;
                var globalIndex = -1;
                string globalName = string.Empty;
                if (global != null)
                {
                    for (int i = 0; i < entries.Count; i++)
                    {
                        if (ReferenceEquals(entries[i].Run.Target, global))
                        {
                            globalIndex = i;
                            globalName = entries[i].Name;
                            break;
                        }
                    }
                }

                EditorGUILayout.LabelField("Global SelectedRun", globalIndex >= 0 ? $"{globalIndex}: {globalName}" : (global != null ? "(Not in list)" : "(None)"));

                if (_lockGlobalSelectedRun && !ReferenceEquals(AbilityPipelineLiveRegistry.SelectedRun, runObj))
                {
                    AbilityPipelineLiveRegistry.SelectedRun = runObj;
                }

                var s = selected.LastSnapshot;
                EditorGUILayout.LabelField("State", s.State.ToString());
                EditorGUILayout.LabelField("CurrentPhaseId", s.CurrentPhaseId.ToString());
                EditorGUILayout.LabelField("PhaseIndex", s.PhaseIndex.ToString());
                EditorGUILayout.LabelField("Paused", s.IsPaused ? "Yes" : "No");

                _graphAsset = (PipelineGraphAsset)EditorGUILayout.ObjectField("Graph Asset", _graphAsset, typeof(PipelineGraphAsset), allowSceneObjects: false);

                if (_graphAsset != null)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button("Sync Graph From Selected Run"))
                        {
                            try
                            {
                                SyncGraphFromSelectedRun(_graphAsset, selected);
                            }
                            catch (Exception ex)
                            {
                                Debug.LogError($"[AbilityPipelineRunDebuggerWindow] SyncGraph failed: {ex}");
                            }
                        }
                    }
                }

                if (_graphAsset != null)
                {
                    EditorGUILayout.Space(6);
                    using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                    {
                        EditorGUILayout.LabelField("Graph (Graph-lite)", EditorStyles.boldLabel);
                        var currentKey = s.CurrentPhaseId.ToString();

                        var canvasRect = GUILayoutUtility.GetRect(10f, 260f, GUILayout.ExpandWidth(true));
                        _lastCanvasSize = canvasRect.size;

                        using (new EditorGUILayout.HorizontalScope())
                        {
                            _nodeFilter = EditorGUILayout.TextField("Search", _nodeFilter ?? string.Empty);
                            if (GUILayout.Button("Focus Current", GUILayout.Width(100)))
                            {
                                _focusRuntimeKey = currentKey;
                            }

                            if (GUILayout.Button("Focus Current + View", GUILayout.Width(140)))
                            {
                                _focusRuntimeKey = currentKey;
                                FocusViewToKey(_graphAsset, currentKey, canvasRect);
                            }

                            if (GUILayout.Button("Focus View", GUILayout.Width(90)))
                            {
                                FocusViewToKey(_graphAsset, string.IsNullOrEmpty(_focusRuntimeKey) ? currentKey : _focusRuntimeKey, canvasRect);
                            }

                            if (GUILayout.Button("Reset View", GUILayout.Width(90)))
                            {
                                _graphPan = Vector2.zero;
                                _graphZoom = 1f;
                            }
                        }

                        _showRuntimeKey = EditorGUILayout.ToggleLeft("Show RuntimeKey", _showRuntimeKey);
                        _showOnlyConnectedToFocus = EditorGUILayout.ToggleLeft("Show Only Connected To Focus", _showOnlyConnectedToFocus);

                        if (string.IsNullOrEmpty(_focusRuntimeKey))
                        {
                            _focusRuntimeKey = currentKey;
                        }
                        DrawGraphCanvas(canvasRect, _graphAsset, currentKey);

                        EditorGUILayout.Space(6);
                        EditorGUILayout.LabelField("Graph Nodes (List)", EditorStyles.boldLabel);
                        _graphScroll = EditorGUILayout.BeginScrollView(_graphScroll, GUILayout.MinHeight(120));
                        var visibleNodeIds = _showOnlyConnectedToFocus
                            ? BuildVisibleNodeIdSet(_graphAsset, string.IsNullOrEmpty(_focusRuntimeKey) ? currentKey : _focusRuntimeKey)
                            : null;
                        DrawGraphNodesList(_graphAsset, currentKey, visibleNodeIds);
                        EditorGUILayout.EndScrollView();
                    }
                }

                if (GUILayout.Button("Select This Run (global focus)"))
                {
                    AbilityPipelineLiveRegistry.SelectedRun = runObj;
                }

                if (AbilityPipelineLiveRegistry.TryGetTrace(runObj, out var trace) && trace != null)
                {
                    trace.CopyTo(_trace);
                }
                else
                {
                    _trace.Clear();
                }

                if (!ReferenceEquals(_lastRunObj, runObj))
                {
                    _lastRunObj = runObj;
                    _lastTraceCount = _trace.Count;
                    _lastPhaseKey = s.CurrentPhaseId.ToString();
                    _pendingScrollToIndex = -1;
                }
                else
                {
                    // New events: scroll to bottom.
                    if (_autoScroll && _trace.Count > _lastTraceCount)
                    {
                        _pendingScrollToIndex = _trace.Count - 1;
                    }

                    // Phase changed: focus and locate the latest event for that phase.
                    var phaseKey = s.CurrentPhaseId.ToString();
                    if (_autoFocusPhase && !string.Equals(_lastPhaseKey, phaseKey, System.StringComparison.Ordinal))
                    {
                        _lastPhaseKey = phaseKey;
                        if (!string.IsNullOrEmpty(phaseKey))
                        {
                            _focusRuntimeKey = phaseKey;
                            _pendingScrollToIndex = FindLastTraceIndexForPhase(_trace, phaseKey);
                        }
                    }

                    _lastTraceCount = _trace.Count;
                }

                EditorGUILayout.Space(6);
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField("Trace Filters", EditorStyles.boldLabel);

                    _autoScroll = EditorGUILayout.ToggleLeft("Auto Scroll", _autoScroll);

                    EditorGUILayout.LabelField("Type", EditorStyles.boldLabel);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button("All", GUILayout.Width(60))) SetTypePresetAll();
                        if (GUILayout.Button("Lifecycle", GUILayout.Width(80))) SetTypePresetLifecycle();
                        if (GUILayout.Button("Errors", GUILayout.Width(70))) SetTypePresetErrors();
                        if (GUILayout.Button("Ticks", GUILayout.Width(60))) SetTypePresetTicks();
                    }

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        _filterRunStart = EditorGUILayout.ToggleLeft("RunStart", _filterRunStart, GUILayout.Width(90));
                        _filterRunEnd = EditorGUILayout.ToggleLeft("RunEnd", _filterRunEnd, GUILayout.Width(80));
                        _filterTick = EditorGUILayout.ToggleLeft("Tick", _filterTick, GUILayout.Width(60));
                    }

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        _filterPhaseStart = EditorGUILayout.ToggleLeft("PhaseStart", _filterPhaseStart, GUILayout.Width(100));
                        _filterPhaseComplete = EditorGUILayout.ToggleLeft("PhaseComplete", _filterPhaseComplete, GUILayout.Width(120));
                        _filterPhaseError = EditorGUILayout.ToggleLeft("PhaseError", _filterPhaseError, GUILayout.Width(100));
                    }

                    _textFilter = EditorGUILayout.TextField("Text", _textFilter ?? string.Empty);

                    if (GUILayout.Button("Copy Trace Report"))
                    {
                        EditorGUIUtility.systemCopyBuffer = BuildTraceReport(selected, _trace,
                            _filterRunStart, _filterRunEnd, _filterPhaseStart, _filterPhaseComplete, _filterPhaseError, _filterTick,
                            _textFilter);
                    }
                }

                EditorGUILayout.Space(6);
                EditorGUILayout.LabelField($"Trace (last {_trace.Count} events)", EditorStyles.boldLabel);

                if (_trace.Count > 0)
                {
                    EditorGUILayout.LabelField("Last", _trace[_trace.Count - 1].ToString());
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    var key = !string.IsNullOrEmpty(_focusRuntimeKey) ? _focusRuntimeKey : s.CurrentPhaseId.ToString();
                    using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(key) || _trace.Count == 0))
                    {
                        if (GUILayout.Button("Locate Trace For Focus", GUILayout.Width(170)))
                        {
                            var idx = FindLastTraceIndexForPhase(_trace, key);
                            if (idx >= 0)
                            {
                                _selectedTraceIndex = idx;
                                _pendingScrollToIndex = idx;
                            }
                        }
                    }
                }

                _scroll = EditorGUILayout.BeginScrollView(_scroll);
                for (int i = 0; i < _trace.Count; i++)
                {
                    var e = _trace[i];
                    if (!PassTypeFilter(e.Type)) continue;

                    if (!string.IsNullOrEmpty(_textFilter))
                    {
                        var ok = false;
                        if (e.PhaseId.ToString().IndexOf(_textFilter, System.StringComparison.OrdinalIgnoreCase) >= 0) ok = true;
                        else if (e.Message != null && e.Message.IndexOf(_textFilter, System.StringComparison.OrdinalIgnoreCase) >= 0) ok = true;
                        if (!ok) continue;
                    }

                    var wasSelected = (i == _selectedTraceIndex);
                    var oldColor = GUI.color;
                    if (wasSelected) GUI.color = Color.yellow;

                    if (GUILayout.Button(e.ToString(), EditorStyles.miniLabel))
                    {
                        _selectedTraceIndex = i;
                        _pendingScrollToIndex = i;

                        if (_graphAsset != null)
                        {
                            if (e.Type == PipelineTraceEventType.PhaseStart
                                || e.Type == PipelineTraceEventType.PhaseComplete
                                || e.Type == PipelineTraceEventType.PhaseError)
                            {
                                _focusRuntimeKey = e.PhaseId.ToString();

                                if (_lastCanvasSize.x > 1f && _lastCanvasSize.y > 1f)
                                {
                                    FocusViewToKey(_graphAsset, _focusRuntimeKey, new Rect(0f, 0f, _lastCanvasSize.x, _lastCanvasSize.y));
                                }
                            }
                        }
                    }

                    GUI.color = oldColor;
                }
                EditorGUILayout.EndScrollView();

                if (Event.current.type == EventType.Repaint)
                {
                    if (_pendingScrollToIndex >= 0)
                    {
                        // Approximate row height (EditorStyles.miniLabel).
                        _scroll.y = Mathf.Max(0f, _pendingScrollToIndex * 18f);
                        _pendingScrollToIndex = -1;
                    }
                }
            }

            if (!EditorApplication.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play Mode to use the run debugger.", MessageType.Info);
            }
        }

        private static string BuildTraceReport(AbilityPipelineLiveRegistry.Entry entry,
            List<PipelineTraceEvent> trace,
            bool runStart,
            bool runEnd,
            bool phaseStart,
            bool phaseComplete,
            bool phaseError,
            bool tick,
            string textFilter)
        {
            var sb = new System.Text.StringBuilder(2048);
            sb.AppendLine("=== AbilityPipeline Run Trace ===");
            sb.AppendLine($"Name: {entry.Name}");
            sb.AppendLine($"ConfigId: {entry.ConfigId}");
            sb.AppendLine($"State: {entry.LastSnapshot.State}");
            sb.AppendLine($"PhaseId: {entry.LastSnapshot.CurrentPhaseId}");
            sb.AppendLine($"PhaseIndex: {entry.LastSnapshot.PhaseIndex}");
            sb.AppendLine($"Paused: {entry.LastSnapshot.IsPaused}");
            sb.AppendLine($"TypeFilter: RunStart={runStart} RunEnd={runEnd} PhaseStart={phaseStart} PhaseComplete={phaseComplete} PhaseError={phaseError} Tick={tick}");
            sb.AppendLine($"TextFilter: {textFilter}");
            sb.AppendLine("---");

            for (int i = 0; i < trace.Count; i++)
            {
                var e = trace[i];
                if (!PassTypeFilterStatic(e.Type, runStart, runEnd, phaseStart, phaseComplete, phaseError, tick)) continue;
                if (!string.IsNullOrEmpty(textFilter))
                {
                    var ok = false;
                    if (e.PhaseId.ToString().IndexOf(textFilter, System.StringComparison.OrdinalIgnoreCase) >= 0) ok = true;
                    else if (e.Message != null && e.Message.IndexOf(textFilter, System.StringComparison.OrdinalIgnoreCase) >= 0) ok = true;
                    if (!ok) continue;
                }
                sb.AppendLine(e.ToString());
            }
            return sb.ToString();
        }

        private bool PassTypeFilter(PipelineTraceEventType type)
        {
            return PassTypeFilterStatic(type, _filterRunStart, _filterRunEnd, _filterPhaseStart, _filterPhaseComplete, _filterPhaseError, _filterTick);
        }

        private static bool PassTypeFilterStatic(
            PipelineTraceEventType type,
            bool runStart,
            bool runEnd,
            bool phaseStart,
            bool phaseComplete,
            bool phaseError,
            bool tick)
        {
            switch (type)
            {
                case PipelineTraceEventType.RunStart: return runStart;
                case PipelineTraceEventType.RunEnd: return runEnd;
                case PipelineTraceEventType.PhaseStart: return phaseStart;
                case PipelineTraceEventType.PhaseComplete: return phaseComplete;
                case PipelineTraceEventType.PhaseError: return phaseError;
                case PipelineTraceEventType.Tick: return tick;
                default: return true;
            }
        }

        private void SetTypePresetAll()
        {
            _filterRunStart = true;
            _filterRunEnd = true;
            _filterPhaseStart = true;
            _filterPhaseComplete = true;
            _filterPhaseError = true;
            _filterTick = true;
        }

        private void SetTypePresetLifecycle()
        {
            _filterRunStart = true;
            _filterRunEnd = true;
            _filterPhaseStart = true;
            _filterPhaseComplete = true;
            _filterPhaseError = true;
            _filterTick = false;
        }

        private void SetTypePresetErrors()
        {
            _filterRunStart = false;
            _filterRunEnd = false;
            _filterPhaseStart = false;
            _filterPhaseComplete = false;
            _filterPhaseError = true;
            _filterTick = false;
        }

        private void SetTypePresetTicks()
        {
            _filterRunStart = false;
            _filterRunEnd = false;
            _filterPhaseStart = false;
            _filterPhaseComplete = false;
            _filterPhaseError = false;
            _filterTick = true;
        }

        private static void SyncGraphFromSelectedRun(PipelineGraphAsset asset, AbilityPipelineLiveRegistry.Entry entry)
        {
            if (asset == null) throw new ArgumentNullException(nameof(asset));
            if (entry == null) throw new ArgumentNullException(nameof(entry));

            var pipelineObj = entry.Pipeline != null ? entry.Pipeline.Target : null;
            if (pipelineObj == null)
            {
                Debug.LogError("[AbilityPipelineRunDebuggerWindow] Selected entry pipeline instance is null.");
                return;
            }

            PipelineGraphSyncUtility.SyncLinearFromPipelinePhases(asset, pipelineObj, entry.ConfigId.ToString());
        }

        private static int FindLastTraceIndexForPhase(List<PipelineTraceEvent> trace, string phaseKey)
        {
            if (trace == null || trace.Count == 0) return -1;
            if (string.IsNullOrEmpty(phaseKey)) return -1;

            // Prefer phase lifecycle events.
            for (int i = trace.Count - 1; i >= 0; i--)
            {
                var e = trace[i];
                if (e.Type != PipelineTraceEventType.PhaseStart
                    && e.Type != PipelineTraceEventType.PhaseComplete
                    && e.Type != PipelineTraceEventType.PhaseError) continue;

                if (string.Equals(e.PhaseId.ToString(), phaseKey, System.StringComparison.Ordinal))
                {
                    return i;
                }
            }

            // Fallback: any event mentioning this phase id.
            for (int i = trace.Count - 1; i >= 0; i--)
            {
                var e = trace[i];
                if (string.Equals(e.PhaseId.ToString(), phaseKey, System.StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return -1;
        }

        private void DrawGraphNodesList(PipelineGraphAsset graph, string currentKey, HashSet<string> visibleNodeIds)
        {
            if (graph == null)
            {
                EditorGUILayout.LabelField("(No graph)");
                return;
            }

            var nodes = graph.Nodes;
            if (nodes == null || nodes.Count == 0)
            {
                EditorGUILayout.LabelField("(No nodes)");
                return;
            }

            for (int i = 0; i < nodes.Count; i++)
            {
                var n = nodes[i];
                if (n == null) continue;

                if (visibleNodeIds != null && !string.IsNullOrEmpty(n.NodeId) && !visibleNodeIds.Contains(n.NodeId))
                    continue;

                if (!string.IsNullOrEmpty(_nodeFilter))
                {
                    var ok = false;
                    if (!string.IsNullOrEmpty(n.NodeId) && n.NodeId.IndexOf(_nodeFilter, System.StringComparison.OrdinalIgnoreCase) >= 0) ok = true;
                    else if (!string.IsNullOrEmpty(n.RuntimeKey) && n.RuntimeKey.IndexOf(_nodeFilter, System.StringComparison.OrdinalIgnoreCase) >= 0) ok = true;
                    else if (!string.IsNullOrEmpty(n.DisplayName) && n.DisplayName.IndexOf(_nodeFilter, System.StringComparison.OrdinalIgnoreCase) >= 0) ok = true;
                    if (!ok) continue;
                }

                var isCurrent = !string.IsNullOrEmpty(currentKey)
                    && !string.IsNullOrEmpty(n.RuntimeKey)
                    && string.Equals(n.RuntimeKey, currentKey, System.StringComparison.Ordinal);

                var isFocus = !string.IsNullOrEmpty(_focusRuntimeKey)
                    && !string.IsNullOrEmpty(n.RuntimeKey)
                    && string.Equals(n.RuntimeKey, _focusRuntimeKey, System.StringComparison.Ordinal);

                var old = GUI.color;
                if (isCurrent || isFocus) GUI.color = Color.yellow;
                if (GUILayout.Button($"{n.NodeId}  key={n.RuntimeKey}  name={n.DisplayName}", EditorStyles.miniButton))
                {
                    if (!string.IsNullOrEmpty(n.RuntimeKey)) _focusRuntimeKey = n.RuntimeKey;
                }
                GUI.color = old;
            }
        }

        private void DrawGraphCanvas(Rect rect, PipelineGraphAsset graph, string currentKey)
        {
            if (graph == null)
            {
                EditorGUI.HelpBox(rect, "No Graph Asset", MessageType.Info);
                return;
            }

            EditorGUI.DrawRect(rect, new Color(0.12f, 0.12f, 0.12f, 1f));

            var e = Event.current;
            HandleGraphCanvasInput(rect, e);

            GUI.BeginGroup(rect);
            try
            {
                Handles.BeginGUI();
                try
                {
                    HashSet<string> visibleNodeIds = null;
                    if (_showOnlyConnectedToFocus)
                    {
                        var key = string.IsNullOrEmpty(_focusRuntimeKey) ? currentKey : _focusRuntimeKey;
                        visibleNodeIds = BuildVisibleNodeIdSet(graph, key);
                    }

                    DrawEdges(rect, graph, currentKey, _focusRuntimeKey, visibleNodeIds);
                    DrawNodes(rect, graph, currentKey, visibleNodeIds);
                }
                finally
                {
                    Handles.EndGUI();
                }
            }
            finally
            {
                GUI.EndGroup();
            }
        }

        private void HandleGraphCanvasInput(Rect rect, Event e)
        {
            if (e == null) return;
            if (!rect.Contains(e.mousePosition))
            {
                if (e.type == EventType.MouseUp) _isPanning = false;
                return;
            }

            if (e.type == EventType.ScrollWheel)
            {
                var delta = -e.delta.y * 0.03f;
                var next = Mathf.Clamp(_graphZoom * (1f + delta), 0.2f, 2.5f);
                if (!Mathf.Approximately(next, _graphZoom))
                {
                    _graphZoom = next;
                    e.Use();
                    Repaint();
                }
                return;
            }

            if (e.type == EventType.MouseDown && e.button == 2)
            {
                _isPanning = true;
                e.Use();
                return;
            }

            if (e.type == EventType.MouseUp && e.button == 2)
            {
                _isPanning = false;
                e.Use();
                return;
            }

            if (e.type == EventType.MouseDrag && e.button == 2 && _isPanning)
            {
                _graphPan += e.delta;
                e.Use();
                Repaint();
            }
        }

        private Vector2 ToCanvasPos(Vector2 nodePos)
        {
            return nodePos * _graphZoom + _graphPan;
        }

        private void DrawEdges(Rect rect, PipelineGraphAsset graph, string currentKey, string focusKey, HashSet<string> visibleNodeIds)
        {
            var edges = graph.Edges;
            var nodes = graph.Nodes;
            if (edges == null || edges.Count == 0) return;
            if (nodes == null || nodes.Count == 0) return;

            for (int i = 0; i < edges.Count; i++)
            {
                var ed = edges[i];
                if (ed == null) continue;

                var from = FindNodeById(nodes, ed.FromNodeId);
                var to = FindNodeById(nodes, ed.ToNodeId);
                if (from == null || to == null) continue;

                if (visibleNodeIds != null)
                {
                    if (!string.IsNullOrEmpty(from.NodeId) && !visibleNodeIds.Contains(from.NodeId)) continue;
                    if (!string.IsNullOrEmpty(to.NodeId) && !visibleNodeIds.Contains(to.NodeId)) continue;
                }

                var isFromCurrent = !string.IsNullOrEmpty(currentKey)
                    && !string.IsNullOrEmpty(from.RuntimeKey)
                    && string.Equals(from.RuntimeKey, currentKey, System.StringComparison.Ordinal);

                var isToCurrent = !string.IsNullOrEmpty(currentKey)
                    && !string.IsNullOrEmpty(to.RuntimeKey)
                    && string.Equals(to.RuntimeKey, currentKey, System.StringComparison.Ordinal);

                var isFromFocus = !string.IsNullOrEmpty(focusKey)
                    && !string.IsNullOrEmpty(from.RuntimeKey)
                    && string.Equals(from.RuntimeKey, focusKey, System.StringComparison.Ordinal);

                var isToFocus = !string.IsNullOrEmpty(focusKey)
                    && !string.IsNullOrEmpty(to.RuntimeKey)
                    && string.Equals(to.RuntimeKey, focusKey, System.StringComparison.Ordinal);

                var isHot = isFromCurrent || isToCurrent || isFromFocus || isToFocus;

                var edgeColor = new Color(1f, 1f, 1f, 0.35f);
                if (!string.IsNullOrEmpty(ed.FromPortId))
                {
                    if (ed.FromPortId.StartsWith("branch[", System.StringComparison.Ordinal)) edgeColor = new Color(0.25f, 0.65f, 1f, 0.45f);
                    else if (ed.FromPortId.StartsWith("par[", System.StringComparison.Ordinal)) edgeColor = new Color(0.75f, 0.35f, 1f, 0.45f);
                }

                // Path highlight: dim unrelated edges.
                if (isHot) edgeColor.a = 0.9f;
                else edgeColor.a *= 0.35f;

                var p0 = ToCanvasPos(from.Position) + new Vector2(160f, 20f);
                var p1 = ToCanvasPos(to.Position) + new Vector2(0f, 20f);
                var t0 = p0 + Vector2.right * 50f;
                var t1 = p1 + Vector2.left * 50f;

                Handles.DrawBezier(p0, p1, t0, t1, edgeColor, null, isHot ? 4.0f : 1.6f);

                var dir = (p1 - p0);
                if (dir.sqrMagnitude > 0.001f)
                {
                    dir.Normalize();
                    var arrowPos = p1 - dir * 10f;
                    var angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                    Handles.color = new Color(edgeColor.r, edgeColor.g, edgeColor.b, isHot ? 0.95f : 0.25f);
                    Handles.ArrowHandleCap(0, arrowPos, Quaternion.Euler(0f, 0f, angle), 10f, EventType.Repaint);
                    Handles.color = Color.white;
                }

                if (!string.IsNullOrEmpty(ed.FromPortId) || !string.IsNullOrEmpty(ed.ToPortId))
                {
                    var mid = (p0 + p1) * 0.5f;
                    var label = $"{ed.FromPortId}->{ed.ToPortId}";
                    var old = GUI.color;
                    GUI.color = new Color(edgeColor.r, edgeColor.g, edgeColor.b, isHot ? 0.95f : 0.25f);
                    Handles.Label(mid + new Vector2(0f, -10f), label);
                    GUI.color = old;
                }
            }
        }

        private void DrawNodes(Rect rect, PipelineGraphAsset graph, string currentKey, HashSet<string> visibleNodeIds)
        {
            var nodes = graph.Nodes;
            if (nodes == null || nodes.Count == 0) return;

            const float w = 160f;
            const float h = 40f;

            // Click handling: do it in repaint/layout cycles as GUI.Button.
            for (int i = 0; i < nodes.Count; i++)
            {
                var n = nodes[i];
                if (n == null) continue;

                if (visibleNodeIds != null && !string.IsNullOrEmpty(n.NodeId) && !visibleNodeIds.Contains(n.NodeId))
                    continue;

                if (!string.IsNullOrEmpty(_nodeFilter))
                {
                    var ok = false;
                    if (!string.IsNullOrEmpty(n.NodeId) && n.NodeId.IndexOf(_nodeFilter, System.StringComparison.OrdinalIgnoreCase) >= 0) ok = true;
                    else if (!string.IsNullOrEmpty(n.RuntimeKey) && n.RuntimeKey.IndexOf(_nodeFilter, System.StringComparison.OrdinalIgnoreCase) >= 0) ok = true;
                    else if (!string.IsNullOrEmpty(n.DisplayName) && n.DisplayName.IndexOf(_nodeFilter, System.StringComparison.OrdinalIgnoreCase) >= 0) ok = true;
                    if (!ok) continue;
                }

                var pos = ToCanvasPos(n.Position);
                var r = new Rect(pos.x, pos.y, w, h);

                var isCurrent = !string.IsNullOrEmpty(currentKey)
                    && !string.IsNullOrEmpty(n.RuntimeKey)
                    && string.Equals(n.RuntimeKey, currentKey, System.StringComparison.Ordinal);

                var isFocus = !string.IsNullOrEmpty(_focusRuntimeKey)
                    && !string.IsNullOrEmpty(n.RuntimeKey)
                    && string.Equals(n.RuntimeKey, _focusRuntimeKey, System.StringComparison.Ordinal);

                var bg = new Color(0.22f, 0.22f, 0.22f, 1f);
                var border = new Color(0.45f, 0.45f, 0.45f, 1f);
                if (isFocus) { bg = new Color(0.45f, 0.35f, 0.05f, 1f); border = Color.yellow; }
                else if (isCurrent) { bg = new Color(0.35f, 0.35f, 0.08f, 1f); border = new Color(1f, 0.92f, 0.5f, 1f); }

                EditorGUI.DrawRect(r, bg);
                Handles.DrawSolidRectangleWithOutline(r, Color.clear, border);

                var title = string.IsNullOrEmpty(n.DisplayName) ? n.NodeId : n.DisplayName;
                if (_showRuntimeKey && !string.IsNullOrEmpty(n.RuntimeKey))
                {
                    GUI.Label(new Rect(r.x + 6, r.y + 2, r.width - 12, r.height - 4), $"{title}\n[{n.RuntimeKey}]");
                }
                else
                {
                    GUI.Label(new Rect(r.x + 6, r.y + 4, r.width - 12, r.height - 8), title);
                }

                if (GUI.Button(r, GUIContent.none, GUIStyle.none))
                {
                    if (!string.IsNullOrEmpty(n.RuntimeKey)) _focusRuntimeKey = n.RuntimeKey;
                }
            }
        }

        private static PipelineGraphAsset.Node FindNodeById(List<PipelineGraphAsset.Node> nodes, string nodeId)
        {
            if (nodes == null) return null;
            if (string.IsNullOrEmpty(nodeId)) return null;
            for (int i = 0; i < nodes.Count; i++)
            {
                var n = nodes[i];
                if (n == null) continue;
                if (string.Equals(n.NodeId, nodeId, System.StringComparison.Ordinal)) return n;
            }
            return null;
        }

        private static HashSet<string> BuildVisibleNodeIdSet(PipelineGraphAsset graph, string focusRuntimeKey)
        {
            if (graph == null) return null;
            var nodes = graph.Nodes;
            var edges = graph.Edges;
            if (nodes == null || nodes.Count == 0) return null;
            if (edges == null || edges.Count == 0) return null;

            var focusNode = FindNodeByRuntimeKey(nodes, focusRuntimeKey);
            if (focusNode == null || string.IsNullOrEmpty(focusNode.NodeId)) return null;

            var visible = new HashSet<string>(System.StringComparer.Ordinal);
            var q = new Queue<string>();
            visible.Add(focusNode.NodeId);
            q.Enqueue(focusNode.NodeId);

            // Undirected connectivity on edges.
            while (q.Count > 0)
            {
                var id = q.Dequeue();
                for (int i = 0; i < edges.Count; i++)
                {
                    var e = edges[i];
                    if (e == null) continue;
                    if (string.IsNullOrEmpty(e.FromNodeId) || string.IsNullOrEmpty(e.ToNodeId)) continue;

                    if (string.Equals(e.FromNodeId, id, System.StringComparison.Ordinal))
                    {
                        if (visible.Add(e.ToNodeId)) q.Enqueue(e.ToNodeId);
                    }
                    else if (string.Equals(e.ToNodeId, id, System.StringComparison.Ordinal))
                    {
                        if (visible.Add(e.FromNodeId)) q.Enqueue(e.FromNodeId);
                    }
                }
            }

            return visible;
        }

        private static PipelineGraphAsset.Node FindNodeByRuntimeKey(List<PipelineGraphAsset.Node> nodes, string runtimeKey)
        {
            if (nodes == null) return null;
            if (string.IsNullOrEmpty(runtimeKey)) return null;
            for (int i = 0; i < nodes.Count; i++)
            {
                var n = nodes[i];
                if (n == null) continue;
                if (string.IsNullOrEmpty(n.RuntimeKey)) continue;
                if (string.Equals(n.RuntimeKey, runtimeKey, System.StringComparison.Ordinal)) return n;
            }
            return null;
        }

        private void FocusViewToKey(PipelineGraphAsset graph, string runtimeKey, Rect canvasRect)
        {
            if (graph == null) return;
            var node = FindNodeByRuntimeKey(graph.Nodes, runtimeKey);
            if (node == null) return;

            const float w = 160f;
            const float h = 40f;
            var nodeCenter = (Vector2)node.Position + new Vector2(w * 0.5f, h * 0.5f);
            var targetCenter = new Vector2(canvasRect.width * 0.5f, canvasRect.height * 0.5f);
            _graphPan = targetCenter - nodeCenter * _graphZoom;
            Repaint();
        }
    }
}

#endif
