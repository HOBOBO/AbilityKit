#if UNITY_EDITOR

#nullable enable

using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Pipeline.Editor
{
    public sealed class PipelineRuntimeDebuggerWindow : EditorWindow
    {
        private enum RunFilter { All, Active, History, Failed, Pinned }
        private enum DetailTab { Overview, Phases, Trace, Context }
        private enum TraceFilter { All, Lifecycle, Phases, Errors, Control }
        private const float MinRunPaneWidth = 220f;
        private const float MaxRunPaneWidth = 520f;
        private const float MinDetailPaneWidth = 360f;
        private const float PhaseNodeWidth = 176f;
        private const float PhaseNodeHeight = 68f;
        private const float PhaseNodeGap = 34f;
        private const float PhaseLevelGap = 54f;

        private readonly Dictionary<string, bool> _phaseFoldouts = new Dictionary<string, bool>();
        private readonly Dictionary<string, PipelinePhaseDebugState> _phaseStates = new Dictionary<string, PipelinePhaseDebugState>();
        private readonly Dictionary<string, PipelinePhaseDebugNode> _phaseNodes = new Dictionary<string, PipelinePhaseDebugNode>();
        private readonly Dictionary<string, Rect> _phaseNodeRects = new Dictionary<string, Rect>();
        private readonly List<PipelinePhaseDebugNode> _phaseNodeOrder = new List<PipelinePhaseDebugNode>();
        private readonly List<EditorPipelineRegistry.DebugEntry> _visibleEntries = new List<EditorPipelineRegistry.DebugEntry>();
        private readonly List<string> _contextNames = new List<string>();

        private Vector2 _runScroll;
        private Vector2 _detailScroll;
        private Vector2 _traceScroll;
        private Vector2 _contextScroll;
        private string _runSearch = string.Empty;
        private string _traceSearch = string.Empty;
        private string _contextSearch = string.Empty;
        private RunFilter _runFilter;
        private DetailTab _detailTab;
        private TraceFilter _traceFilter;
        private bool _followLatest = true;
        private bool _relativeTraceTime = true;
        private bool _confirmInterrupt = true;
        private bool _showOnlyChangedContext;
        private bool _showTechnicalDetails;
        private bool _showPhaseGraph = true;
        private bool _phaseGraphNeedsFit = true;
        private bool _isPhaseGraphPanning;
        private float _runPaneWidth = 300f;
        private float _refreshIntervalSeconds = 0.1f;
        private float _phaseGraphZoom = 1f;
        private Vector2 _phaseGraphPan = new Vector2(24f, 24f);
        private Vector2 _phaseGraphDragMouse;
        private float _splitStartMouseX;
        private float _splitStartWidth;
        private int? _selectedRunId;
        private int? _selectedTraceSequence;
        private string? _selectedPhaseNodeKey;
        private string? _phaseGraphFocusNodeKey;
        private double _nextRefreshAt;
        private volatile bool _registryChanged;

        private GUIStyle? _runTitleStyle;
        private GUIStyle? _mutedStyle;
        private GUIStyle? _sectionStyle;
        private GUIStyle? _monoStyle;
        private GUIStyle? _wrappedValueStyle;
        private GUIStyle? _centeredMutedStyle;
        private GUIStyle? _graphNodeTitleStyle;
        private GUIStyle? _graphNodeMetaStyle;
        private GUIStyle? _graphNodeStatusStyle;
        private GUIStyle? _graphEdgeLabelStyle;

        [MenuItem("Window/AbilityKit/Pipeline Runtime Debugger")]
        public static void Open()
        {
            var window = GetWindow<PipelineRuntimeDebuggerWindow>();
            window.titleContent = new GUIContent("Pipeline Debugger");
            window.minSize = new Vector2(640f, 420f);
            window.Show();
        }

        private void OnEnable()
        {
            RestoreUserState();
            EditorApplication.update += OnEditorUpdate;
            EditorPipelineRegistry.Instance.Changed += OnRegistryChanged;
        }

        private void OnDisable()
        {
            PersistUserState();
            EditorApplication.update -= OnEditorUpdate;
            EditorPipelineRegistry.Instance.Changed -= OnRegistryChanged;
        }

        private void OnEditorUpdate()
        {
            if (!_registryChanged && EditorApplication.timeSinceStartup < _nextRefreshAt) return;
            _registryChanged = false;
            _nextRefreshAt = EditorApplication.timeSinceStartup + _refreshIntervalSeconds;
            EditorPipelineRegistry.Instance.Refresh();
            Repaint();
        }

        private void OnRegistryChanged()
        {
            _registryChanged = true;
        }

        private void OnGUI()
        {
            EnsureStyles();
            BuildVisibleEntries();
            ResolveSelection();
            DrawMainToolbar();

            EditorGUILayout.BeginHorizontal();
            DrawRunPane();
            DrawPaneSplitter();
            DrawDetailPane();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawMainToolbar()
        {
            var registry = EditorPipelineRegistry.Instance;
            var stats = registry.GetStats();
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar, GUILayout.Height(23f));

            bool captureEnabled = GUILayout.Toggle(
                registry.IsCaptureEnabled,
                IconText(
                    registry.IsCaptureEnabled ? "d_Record On" : "d_Record Off",
                    "Capture",
                    "Capture newly started pipeline runs"),
                EditorStyles.toolbarButton,
                GUILayout.Width(78f));
            if (captureEnabled != registry.IsCaptureEnabled)
            {
                registry.IsCaptureEnabled = captureEnabled;
                PipelineDebuggerUserState.instance.CaptureEnabled = captureEnabled;
                PipelineDebuggerUserState.instance.SaveNow();
            }

            GUILayout.Label(
                $"Runs {stats.Total}  |  Active {stats.Active}  |  Failed {stats.Failed}  |  Pinned {stats.Pinned}",
                EditorStyles.miniLabel,
                GUILayout.MinWidth(220f));
            GUILayout.FlexibleSpace();

            bool follow = GUILayout.Toggle(
                _followLatest,
                IconText("Animation.Play", "Follow", "Select the newest matching run"),
                EditorStyles.toolbarButton,
                GUILayout.Width(68f));
            if (follow != _followLatest)
            {
                _followLatest = follow;
                if (_followLatest && _visibleEntries.Count > 0) SelectRun(_visibleEntries[0].OwnerId);
            }

            if (GUILayout.Button(
                    IconOnly("SettingsIcon", "Debugger options"),
                    EditorStyles.toolbarButton,
                    GUILayout.Width(28f)))
            {
                ShowOptionsMenu();
            }

            using (new EditorGUI.DisabledScope(stats.History == 0))
            {
                if (GUILayout.Button(
                        IconOnly("TreeEditor.Trash", "Clear unpinned completed runs"),
                        EditorStyles.toolbarButton,
                        GUILayout.Width(28f)))
                {
                    registry.ClearHistory();
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawRunPane()
        {
            float maxWidth = Mathf.Max(MinRunPaneWidth, position.width - MinDetailPaneWidth - 5f);
            _runPaneWidth = Mathf.Clamp(_runPaneWidth, MinRunPaneWidth, Mathf.Min(MaxRunPaneWidth, maxWidth));
            EditorGUILayout.BeginVertical(GUILayout.Width(_runPaneWidth), GUILayout.ExpandHeight(true));

            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            string runSearch = GUILayout.TextField(_runSearch, EditorStyles.toolbarSearchField, GUILayout.ExpandWidth(true));
            if (runSearch != _runSearch) _runSearch = runSearch;
            if (!string.IsNullOrEmpty(_runSearch) && GUILayout.Button("x", EditorStyles.toolbarButton, GUILayout.Width(22f)))
            {
                _runSearch = string.Empty;
                GUI.FocusControl(null);
            }
            _runFilter = (RunFilter)EditorGUILayout.EnumPopup(
                _runFilter,
                EditorStyles.toolbarPopup,
                GUILayout.Width(74f));
            EditorGUILayout.EndHorizontal();

            _runScroll = EditorGUILayout.BeginScrollView(_runScroll, GUILayout.ExpandHeight(true));
            if (_visibleEntries.Count == 0)
            {
                DrawRunEmptyState(EditorPipelineRegistry.Instance.GetStats());
            }
            else
            {
                int previousGroup = -1;
                for (int i = 0; i < _visibleEntries.Count; i++)
                {
                    int group = GetRunGroup(_visibleEntries[i]);
                    if (_runFilter == RunFilter.All && group != previousGroup)
                    {
                        DrawRunGroupHeader(group);
                        previousGroup = group;
                    }
                    DrawRunRow(_visibleEntries[i]);
                }
            }
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawRunEmptyState(EditorPipelineRegistry.DebugStats stats)
        {
            GUILayout.Space(24f);
            string title;
            string detail;
            if (stats.Total == 0 && !EditorPipelineRegistry.Instance.IsCaptureEnabled)
            {
                title = "Capture is paused";
                detail = "Enable Capture to observe new runs.";
            }
            else if (stats.Total == 0 && !EditorApplication.isPlaying)
            {
                title = "No captured runs";
                detail = "Runs will appear here in Play Mode.";
            }
            else if (stats.Total == 0)
            {
                title = "Waiting for pipeline runs";
                detail = "Capture is active.";
            }
            else
            {
                title = "No matching runs";
                detail = "Change the search or filter.";
            }

            GUILayout.Label(title, _centeredMutedStyle);
            GUILayout.Space(3f);
            GUILayout.Label(detail, _centeredMutedStyle);
        }

        private void DrawPaneSplitter()
        {
            var splitter = GUILayoutUtility.GetRect(5f, 5f, GUILayout.Width(5f), GUILayout.ExpandHeight(true));
            EditorGUIUtility.AddCursorRect(splitter, MouseCursor.ResizeHorizontal);
            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(
                    new Rect(splitter.x + 2f, splitter.y, 1f, splitter.height),
                    new Color(0f, 0f, 0f, 0.28f));
            }

            int controlId = GUIUtility.GetControlID("PipelineDebuggerSplitter".GetHashCode(), FocusType.Passive, splitter);
            switch (Event.current.GetTypeForControl(controlId))
            {
                case EventType.MouseDown when Event.current.button == 0 && splitter.Contains(Event.current.mousePosition):
                    GUIUtility.hotControl = controlId;
                    _splitStartMouseX = Event.current.mousePosition.x;
                    _splitStartWidth = _runPaneWidth;
                    Event.current.Use();
                    break;
                case EventType.MouseDrag when GUIUtility.hotControl == controlId:
                    float maxWidth = Mathf.Min(MaxRunPaneWidth, position.width - MinDetailPaneWidth - 5f);
                    _runPaneWidth = Mathf.Clamp(
                        _splitStartWidth + Event.current.mousePosition.x - _splitStartMouseX,
                        MinRunPaneWidth,
                        Mathf.Max(MinRunPaneWidth, maxWidth));
                    Repaint();
                    Event.current.Use();
                    break;
                case EventType.MouseUp when GUIUtility.hotControl == controlId:
                    GUIUtility.hotControl = 0;
                    PersistUserState();
                    Event.current.Use();
                    break;
            }
        }

        private void BuildVisibleEntries()
        {
            _visibleEntries.Clear();
            var entries = EditorPipelineRegistry.Instance.GetEntries();
            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (MatchesRunFilter(entry) && MatchesRunSearch(entry)) _visibleEntries.Add(entry);
            }
            if (_runFilter == RunFilter.All)
            {
                _visibleEntries.Sort((left, right) =>
                {
                    int groupOrder = GetRunGroup(left).CompareTo(GetRunGroup(right));
                    return groupOrder != 0
                        ? groupOrder
                        : right.RegisteredAtUtc.CompareTo(left.RegisteredAtUtc);
                });
            }
        }

        private bool MatchesRunFilter(EditorPipelineRegistry.DebugEntry entry)
        {
            return _runFilter switch
            {
                RunFilter.Active => entry.IsActive,
                RunFilter.History => !entry.IsActive,
                RunFilter.Failed => entry.LastState == EAbilityPipelineState.Failed,
                RunFilter.Pinned => entry.IsPinned,
                _ => true
            };
        }

        private bool MatchesRunSearch(EditorPipelineRegistry.DebugEntry entry)
        {
            if (string.IsNullOrWhiteSpace(_runSearch)) return true;
            return Contains(entry.OwnerName, _runSearch)
                || Contains(entry.PipelineType, _runSearch)
                || Contains(entry.ConfigType, _runSearch)
                || Contains(entry.LastPhaseId.ToString(), _runSearch)
                || entry.OwnerId.ToString().IndexOf(_runSearch, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static int GetRunGroup(EditorPipelineRegistry.DebugEntry entry)
        {
            if (entry.IsActive) return 0;
            if (entry.IsPinned) return 1;
            return 2;
        }

        private void DrawRunGroupHeader(int group)
        {
            string title = group == 0 ? "ACTIVE" : group == 1 ? "PINNED" : "RECENT";
            var rect = GUILayoutUtility.GetRect(0f, 20f, GUILayout.ExpandWidth(true));
            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(rect, new Color(0f, 0f, 0f, 0.14f));
            }
            GUI.Label(new Rect(rect.x + 8f, rect.y + 2f, rect.width - 16f, 18f), title, EditorStyles.miniBoldLabel);
        }

        private int FindNewestVisibleRunId()
        {
            int runId = _visibleEntries[0].OwnerId;
            DateTime newest = _visibleEntries[0].RegisteredAtUtc;
            for (int i = 1; i < _visibleEntries.Count; i++)
            {
                if (_visibleEntries[i].RegisteredAtUtc <= newest) continue;
                newest = _visibleEntries[i].RegisteredAtUtc;
                runId = _visibleEntries[i].OwnerId;
            }
            return runId;
        }

        private void ShowRunContextMenu(EditorPipelineRegistry.DebugEntry entry)
        {
            var menu = new GenericMenu();
            menu.AddItem(
                new GUIContent(entry.IsPinned ? "Unpin Run" : "Pin Run"),
                false,
                () => EditorPipelineRegistry.Instance.SetPinned(entry.OwnerId, !entry.IsPinned));
            menu.AddItem(new GUIContent("Save Snapshot..."), false, () => SaveSession(entry));
            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Copy Run Summary"), false, () => CopyRunSummary(entry));
            if (entry.LastState == EAbilityPipelineState.Failed)
            {
                menu.AddItem(new GUIContent("Go To Failure"), false, () => GoToFailure(entry));
            }
            menu.ShowAsContext();
        }

        private void ResolveSelection()
        {
            bool selectedVisible = false;
            if (_selectedRunId.HasValue)
            {
                for (int i = 0; i < _visibleEntries.Count; i++)
                {
                    if (_visibleEntries[i].OwnerId != _selectedRunId.Value) continue;
                    selectedVisible = true;
                    break;
                }
            }

            if (_followLatest && _visibleEntries.Count > 0)
            {
                int latestId = FindNewestVisibleRunId();
                if (!_selectedRunId.HasValue
                    || !selectedVisible
                    || EditorPipelineRegistry.Instance.SelectedRunId == latestId)
                {
                    SelectRun(latestId);
                }
            }
            else if (!selectedVisible)
            {
                SelectRun(_visibleEntries.Count > 0 ? _visibleEntries[0].OwnerId : (int?)null);
            }
        }

        private void DrawRunRow(EditorPipelineRegistry.DebugEntry entry)
        {
            var row = GUILayoutUtility.GetRect(0f, 52f, GUILayout.ExpandWidth(true));
            bool selected = _selectedRunId == entry.OwnerId;
            if (Event.current.type == EventType.Repaint)
            {
                var background = selected
                    ? new Color(0.18f, 0.36f, 0.55f, 0.58f)
                    : new Color(1f, 1f, 1f, entry.IsActive ? 0.045f : 0.018f);
                EditorGUI.DrawRect(row, background);
                EditorGUI.DrawRect(new Rect(row.x, row.y, 3f, row.height), StateColor(entry));
            }

            string ownerName = string.IsNullOrWhiteSpace(entry.OwnerName) ? "Run #" + entry.OwnerId : entry.OwnerName;
            var pinRect = new Rect(row.xMax - 25f, row.y + 4f, 20f, 20f);
            var titleRect = new Rect(row.x + 10f, row.y + 5f, row.width - 112f, 19f);
            var stateRect = new Rect(row.xMax - 98f, row.y + 5f, 70f, 18f);
            var phaseRect = new Rect(row.x + 10f, row.y + 27f, row.width - 88f, 18f);
            var timeRect = new Rect(row.xMax - 74f, row.y + 27f, 68f, 18f);

            GUI.Label(titleRect, new GUIContent(ownerName, entry.PipelineType), _runTitleStyle);
            GUI.Label(stateRect, entry.IsPaused ? "Paused" : entry.LastState.ToString(), _mutedStyle);
            GUI.Label(phaseRect, string.IsNullOrEmpty(entry.LastPhaseId.Value) ? "No phase" : entry.LastPhaseId.ToString(), _mutedStyle);
            GUI.Label(timeRect, FormatDuration(entry), _mutedStyle);
            bool showPin = entry.IsPinned || selected || row.Contains(Event.current.mousePosition);
            if (showPin && GUI.Button(
                    pinRect,
                    IconOnly("Favorite", entry.IsPinned ? "Unpin run" : "Pin run"),
                    GUIStyle.none))
            {
                EditorPipelineRegistry.Instance.SetPinned(entry.OwnerId, !entry.IsPinned);
            }

            if (Event.current.type == EventType.ContextClick && row.Contains(Event.current.mousePosition))
            {
                ShowRunContextMenu(entry);
                Event.current.Use();
            }
            else if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && row.Contains(Event.current.mousePosition))
            {
                _followLatest = false;
                SelectRun(entry.OwnerId);
                Event.current.Use();
            }
        }

        private void DrawDetailPane()
        {
            EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            if (!TryGetSelectedEntry(out var entry) || entry == null)
            {
                GUILayout.FlexibleSpace();
                GUILayout.Label("Select a pipeline run to inspect", _centeredMutedStyle);
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndVertical();
                return;
            }

            float detailWidth = Mathf.Max(0f, position.width - _runPaneWidth - 5f);
            bool compact = detailWidth < 540f;
            DrawDetailHeader(entry, compact);
            int traceCount = EditorPipelineRegistry.Instance.GetTraceSnapshot(entry.OwnerId).Count;
            _detailTab = (DetailTab)GUILayout.Toolbar(
                (int)_detailTab,
                compact
                    ? new[] { "Overview", "Phases", "Trace", "Context" }
                    : new[]
                    {
                        "Overview",
                        "Phases " + CountPhaseNodes(entry.PhaseTree),
                        "Trace " + traceCount,
                        "Context " + entry.ContextValues.Count
                    },
                EditorStyles.toolbarButton);

            switch (_detailTab)
            {
                case DetailTab.Overview: DrawOverview(entry); break;
                case DetailTab.Phases: DrawPhases(entry); break;
                case DetailTab.Trace: DrawTrace(entry); break;
                case DetailTab.Context: DrawContext(entry); break;
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawDetailHeader(EditorPipelineRegistry.DebugEntry entry, bool compact)
        {
            if (compact) EditorGUILayout.BeginVertical(GUILayout.Height(72f));
            EditorGUILayout.BeginHorizontal(GUILayout.Height(compact ? 42f : 48f));
            GUILayout.Space(10f);
            var status = GUILayoutUtility.GetRect(8f, 8f, GUILayout.Width(8f), GUILayout.Height(28f));
            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(new Rect(status.x, status.y + 10f, 8f, 8f), StateColor(entry));
            }

            GUILayout.Space(4f);
            EditorGUILayout.BeginVertical();
            GUILayout.Space(5f);
            GUILayout.Label(
                string.IsNullOrWhiteSpace(entry.OwnerName) ? "Run #" + entry.OwnerId : entry.OwnerName,
                _sectionStyle);
            GUILayout.Label(entry.PipelineType + "  |  #" + entry.OwnerId, _mutedStyle);
            EditorGUILayout.EndVertical();
            if (!compact)
            {
                GUILayout.FlexibleSpace();
                DrawRunUtilityControls(entry);
                DrawRunControls(entry);
                GUILayout.Space(8f);
            }
            EditorGUILayout.EndHorizontal();
            if (compact)
            {
                EditorGUILayout.BeginHorizontal(GUILayout.Height(26f));
                GUILayout.FlexibleSpace();
                DrawRunUtilityControls(entry);
                DrawRunControls(entry);
                GUILayout.Space(8f);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
            }
        }

        private void DrawRunUtilityControls(EditorPipelineRegistry.DebugEntry entry)
        {
            if (GUILayout.Button(
                    IconOnly(entry.IsPinned ? "Favorite" : "Favorite Icon", entry.IsPinned ? "Unpin run" : "Pin run"),
                    GUILayout.Width(26f), GUILayout.Height(20f)))
            {
                EditorPipelineRegistry.Instance.SetPinned(entry.OwnerId, !entry.IsPinned);
            }
            if (GUILayout.Button(IconOnly("SaveAs", "Save diagnostic snapshot"), GUILayout.Width(26f), GUILayout.Height(20f)))
            {
                SaveSession(entry);
            }
            if (GUILayout.Button(IconOnly("Clipboard", "Copy run summary"), GUILayout.Width(26f), GUILayout.Height(20f)))
            {
                CopyRunSummary(entry);
            }
            GUILayout.Space(4f);
        }

        private void DrawRunControls(EditorPipelineRegistry.DebugEntry entry)
        {
            bool canControl = entry.IsActive && entry.TryGetControl(out var control) && control != null;
            using (new EditorGUI.DisabledScope(!canControl))
            {
                if (entry.IsPaused)
                {
                    if (GUILayout.Button(IconText("PlayButton", "Resume", "Resume this run"), GUILayout.Width(76f)))
                    {
                        ExecuteControl(entry, value => value.Resume());
                    }
                }
                else if (GUILayout.Button(IconText("PauseButton", "Pause", "Pause this run"), GUILayout.Width(70f)))
                {
                    ExecuteControl(entry, value => value.Pause());
                }

                if (GUILayout.Button(new GUIContent("Cancel", "Request cancellation on the next Tick"), GUILayout.Width(58f)))
                {
                    ExecuteControl(entry, value => value.Cancel());
                }

                if (GUILayout.Button(
                        IconText("winbtn_win_close", "Interrupt", "Immediately interrupt this run"),
                        GUILayout.Width(82f))
                    && (!_confirmInterrupt || EditorUtility.DisplayDialog(
                        "Interrupt Pipeline Run",
                        "Interrupt run #" + entry.OwnerId + " immediately? This cannot be resumed.",
                        "Interrupt",
                        "Keep Running")))
                {
                    ExecuteControl(entry, value => value.Interrupt());
                }
            }
        }

        private static void ExecuteControl(EditorPipelineRegistry.DebugEntry entry, Action<IPipelineRunControl> action)
        {
            if (!entry.TryGetControl(out var control) || control == null) return;
            try { action(control); }
            catch (Exception exception) { Debug.LogException(exception); }
        }

        private void DrawOverview(EditorPipelineRegistry.DebugEntry entry)
        {
            _detailScroll = EditorGUILayout.BeginScrollView(_detailScroll);
            GUILayout.Space(10f);

            string? lastError = FindLastError(entry);
            if (!string.IsNullOrEmpty(lastError))
            {
                DrawFailureSummary(entry, lastError!);
                GUILayout.Space(6f);
            }

            DrawSectionTitle("Run");
            DrawKeyValue("Run ID", entry.OwnerId.ToString());
            DrawKeyValue("State", entry.IsPaused ? "Executing (Paused)" : entry.LastState.ToString());
            DrawKeyValue("Current phase", string.IsNullOrEmpty(entry.LastPhaseId.Value) ? "No phase" : entry.LastPhaseId.ToString());
            DrawKeyValue("Elapsed time", entry.ElapsedTime.ToString("0.000") + " s");
            DrawKeyValue("Wall duration", entry.WallDurationSeconds.ToString("0.000") + " s");

            GUILayout.Space(12f);
            DrawSectionTitle("Active phases");
            if (entry.ActivePhases.Count == 0)
            {
                GUILayout.Label("No active phase", _mutedStyle);
            }
            else
            {
                for (int i = 0; i < entry.ActivePhases.Count; i++)
                {
                    GUILayout.Label(entry.ActivePhases[i].ToString(), _monoStyle);
                }
            }

            GUILayout.Space(12f);
            _showTechnicalDetails = EditorGUILayout.Foldout(_showTechnicalDetails, "Technical details", true);
            if (_showTechnicalDetails)
            {
                DrawKeyValue("Started UTC", entry.RegisteredAtUtc.ToString("yyyy-MM-dd HH:mm:ss.fff"));
                DrawKeyValue("Ended UTC", entry.EndedAtUtc?.ToString("yyyy-MM-dd HH:mm:ss.fff") ?? "Running");
                DrawKeyValue("Pipeline", entry.PipelineType);
                DrawKeyValue("Config", entry.ConfigType);
                DrawKeyValue("Context", entry.ContextType);
                DrawLiveObjectSection(entry);
            }
            EditorGUILayout.EndScrollView();
        }

        private void DrawFailureSummary(EditorPipelineRegistry.DebugEntry entry, string message)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(IconOnly("console.erroricon", "Failure"), GUILayout.Width(22f), GUILayout.Height(22f));
            EditorGUILayout.BeginVertical();
            GUILayout.Label("Run failed", EditorStyles.boldLabel);
            GUILayout.Label(
                string.IsNullOrEmpty(entry.LastPhaseId.Value) ? "No phase reported" : entry.LastPhaseId.ToString(),
                _mutedStyle);
            EditorGUILayout.EndVertical();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Trace", GUILayout.Width(52f))) GoToFailure(entry);
            if (GUILayout.Button("Phase", GUILayout.Width(52f))) _detailTab = DetailTab.Phases;
            EditorGUILayout.EndHorizontal();
            float width = Mathf.Max(160f, position.width - _runPaneWidth - 42f);
            float height = Mathf.Clamp(
                _wrappedValueStyle!.CalcHeight(new GUIContent(message), width),
                EditorGUIUtility.singleLineHeight,
                56f);
            EditorGUILayout.SelectableLabel(message, _wrappedValueStyle, GUILayout.Height(height));
            EditorGUILayout.EndVertical();
        }

        private void DrawLiveObjectSection(EditorPipelineRegistry.DebugEntry entry)
        {
            var objects = new List<(string Label, UnityEngine.Object Value)>(4);
            AddUnityObject(objects, "Pipeline", entry.GetPipeline());
            AddUnityObject(objects, "Config", entry.GetConfig());
            AddUnityObject(objects, "Context", entry.GetContext());
            AddUnityObject(objects, "Run", entry.GetRun());
            if (objects.Count == 0) return;

            GUILayout.Space(12f);
            DrawSectionTitle("Live Unity objects");
            for (int i = 0; i < objects.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label(objects[i].Label, _mutedStyle, GUILayout.Width(138f));
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.ObjectField(objects[i].Value, typeof(UnityEngine.Object), true);
                }
                if (GUILayout.Button(IconOnly("d_ViewToolZoom", "Ping object"), GUILayout.Width(26f)))
                {
                    EditorGUIUtility.PingObject(objects[i].Value);
                }
                EditorGUILayout.EndHorizontal();
            }
        }

        private static void AddUnityObject(
            ICollection<(string Label, UnityEngine.Object Value)> objects,
            string label,
            object? value)
        {
            if (value is UnityEngine.Object unityObject && unityObject != null) objects.Add((label, unityObject));
        }

        private void DrawPhases(EditorPipelineRegistry.DebugEntry entry)
        {
            if (entry.PhaseTree.Count == 0)
            {
                GUILayout.Space(8f);
                EditorGUILayout.HelpBox("This pipeline does not expose a phase definition structure.", MessageType.Info);
                return;
            }

            BuildPhaseStates(entry);
            BuildPhaseGraphLayout(entry);
            DrawPhaseToolbar(entry);
            if (entry.HasGraphLayoutMismatch)
            {
                EditorGUILayout.HelpBox(
                    "The provided graph layout does not match this runtime structure. Auto layout is being used.",
                    MessageType.Warning);
            }

            if (_showPhaseGraph) DrawPhaseGraph(entry);
            else DrawPhaseTree(entry);
        }

        private void DrawPhaseToolbar(EditorPipelineRegistry.DebugEntry entry)
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar, GUILayout.Height(22f));
            int phaseView = GUILayout.Toolbar(
                _showPhaseGraph ? 0 : 1,
                new[]
                {
                    new GUIContent("Graph", "Node graph with live execution state"),
                    new GUIContent("Tree", "Compact hierarchical phase list")
                },
                EditorStyles.toolbarButton,
                GUILayout.Width(102f));
            bool nextGraph = phaseView == 0;
            if (nextGraph != _showPhaseGraph)
            {
                _showPhaseGraph = nextGraph;
                PipelineDebuggerUserState.instance.ShowPhaseGraph = nextGraph;
                PipelineDebuggerUserState.instance.SaveNow();
                _phaseGraphNeedsFit = true;
            }

            if (_showPhaseGraph)
            {
                GUILayout.Space(4f);
                if (GUILayout.Button(IconOnly("ViewToolZoom", "Fit graph to view"), EditorStyles.toolbarButton, GUILayout.Width(28f)))
                {
                    _phaseGraphNeedsFit = true;
                }
                using (new EditorGUI.DisabledScope(!TryFindFocusPhaseNode(out _)))
                {
                    if (GUILayout.Button(new GUIContent("\u25CE", "Focus active or failed node"), EditorStyles.toolbarButton, GUILayout.Width(28f))
                        && TryFindFocusPhaseNode(out string? focusNode))
                    {
                        _phaseGraphFocusNodeKey = focusNode;
                    }
                }
            }

            GUILayout.Space(6f);
            string layoutLabel = entry.GraphLayout != null
                ? "Layout: " + (string.IsNullOrEmpty(entry.GraphLayout.SourceName) ? "asset" : entry.GraphLayout.SourceName)
                : "Layout: Auto";
            GUILayout.Label(new GUIContent(layoutLabel, entry.Graph.StructureId), EditorStyles.miniLabel);
            GUILayout.FlexibleSpace();

            if (_selectedPhaseNodeKey != null && _phaseNodes.TryGetValue(_selectedPhaseNodeKey, out var selectedNode))
            {
                GUILayout.Label(selectedNode.PhaseId.ToString(), EditorStyles.miniLabel, GUILayout.MaxWidth(130f));
                if (GUILayout.Button(new GUIContent("Trace", "Filter Trace by this phase"), EditorStyles.toolbarButton, GUILayout.Width(44f)))
                {
                    GoToPhaseTrace(selectedNode);
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        private void BuildPhaseStates(EditorPipelineRegistry.DebugEntry entry)
        {
            _phaseStates.Clear();
            for (int i = 0; i < entry.PhaseStates.Count; i++)
            {
                _phaseStates[entry.PhaseStates[i].NodeKey] = entry.PhaseStates[i];
            }
        }

        private void DrawPhaseTree(EditorPipelineRegistry.DebugEntry entry)
        {
            _detailScroll = EditorGUILayout.BeginScrollView(_detailScroll);
            GUILayout.Space(8f);
            for (int i = 0; i < entry.PhaseTree.Count; i++)
            {
                DrawPhaseNode(entry, entry.PhaseTree[i], 0);
            }
            EditorGUILayout.EndScrollView();
        }

        private void DrawPhaseNode(
            EditorPipelineRegistry.DebugEntry entry,
            PipelinePhaseDebugNode node,
            int depth)
        {
            EPipelineDebugExecutionState state = ResolvePhaseState(entry, node);
            bool active = state == EPipelineDebugExecutionState.Active;
            var row = GUILayoutUtility.GetRect(0f, 28f, GUILayout.ExpandWidth(true));
            if (Event.current.type == EventType.Repaint)
            {
                Color background = active
                    ? new Color(0.16f, 0.45f, 0.30f, 0.42f)
                    : new Color(1f, 1f, 1f, depth % 2 == 0 ? 0.025f : 0.012f);
                EditorGUI.DrawRect(row, background);
                if (state == EPipelineDebugExecutionState.Failed)
                {
                    EditorGUI.DrawRect(new Rect(row.x, row.y, 3f, row.height), new Color(0.85f, 0.23f, 0.20f));
                }
                else if (state == EPipelineDebugExecutionState.Completed)
                {
                    EditorGUI.DrawRect(new Rect(row.x, row.y, 3f, row.height), new Color(0.24f, 0.65f, 0.39f));
                }
            }

            float indent = 10f + depth * 18f;
            bool expanded = true;
            if (node.Children.Count > 0)
            {
                if (!_phaseFoldouts.TryGetValue(node.NodeKey, out expanded)) expanded = true;
                var foldoutRect = new Rect(row.x + indent, row.y + 5f, 16f, 18f);
                bool next = EditorGUI.Foldout(foldoutRect, expanded, GUIContent.none, false);
                if (next != expanded) _phaseFoldouts[node.NodeKey] = next;
                expanded = next;
            }

            string statusText = state == EPipelineDebugExecutionState.Pending ? string.Empty : state.ToString();
            var idRect = new Rect(row.x + indent + 20f, row.y + 5f, Mathf.Max(80f, row.width * 0.42f), 18f);
            var statusRect = new Rect(row.xMax - 74f, row.y + 5f, 68f, 18f);
            var typeRect = new Rect(idRect.xMax + 8f, row.y + 5f, Mathf.Max(0f, statusRect.x - idRect.xMax - 12f), 18f);
            GUI.Label(idRect, new GUIContent(node.PhaseId.ToString(), node.Summary), active ? EditorStyles.boldLabel : EditorStyles.label);
            GUI.Label(typeRect, new GUIContent(node.Kind.ToString(), node.PhaseType), _mutedStyle);
            GUI.Label(statusRect, statusText, _mutedStyle);

            if (Event.current.type == EventType.MouseDown
                && Event.current.clickCount == 2
                && row.Contains(Event.current.mousePosition))
            {
                GoToPhaseTrace(node);
                Event.current.Use();
            }

            if (!expanded) return;
            for (int i = 0; i < node.Children.Count; i++)
            {
                DrawPhaseNode(entry, node.Children[i], depth + 1);
            }
        }

        private void DrawPhaseGraph(EditorPipelineRegistry.DebugEntry entry)
        {
            Rect canvas = GUILayoutUtility.GetRect(
                120f,
                10000f,
                180f,
                10000f,
                GUILayout.ExpandWidth(true),
                GUILayout.ExpandHeight(true));
            if (canvas.width < 1f || canvas.height < 1f) return;

            if (_phaseGraphNeedsFit
                && Event.current.type == EventType.Repaint
                && canvas.width >= 300f
                && canvas.height >= 220f)
            {
                FitPhaseGraph(canvas.size);
                _phaseGraphNeedsFit = false;
            }
            if (!string.IsNullOrEmpty(_phaseGraphFocusNodeKey))
            {
                FocusPhaseNode(_phaseGraphFocusNodeKey!, canvas.size);
                _phaseGraphFocusNodeKey = null;
            }

            HandlePhaseGraphInput(entry, canvas);
            EditorGUI.DrawRect(canvas, EditorGUIUtility.isProSkin
                ? new Color(0.105f, 0.11f, 0.12f)
                : new Color(0.72f, 0.73f, 0.74f));

            GUI.BeginGroup(canvas);
            DrawPhaseGraphGrid(canvas.size);
            DrawPhaseGraphEdges(entry, canvas.size);
            DrawPhaseGraphNodes(entry, canvas.size);
            GUI.EndGroup();
        }

        private void BuildPhaseGraphLayout(EditorPipelineRegistry.DebugEntry entry)
        {
            _phaseNodes.Clear();
            _phaseNodeRects.Clear();
            _phaseNodeOrder.Clear();

            float cursorX = 24f;
            for (int i = 0; i < entry.Graph.Roots.Count; i++)
            {
                var root = entry.Graph.Roots[i];
                CollectPhaseNodes(root);
                float width = GetPhaseSubtreeWidth(root);
                LayoutPhaseSubtree(root, cursorX, 28f, width);
                cursorX += width + PhaseNodeGap * 1.5f;
            }

            if (entry.GraphLayout == null) return;
            for (int i = 0; i < entry.GraphLayout.Nodes.Count; i++)
            {
                var position = entry.GraphLayout.Nodes[i];
                if (_phaseNodeRects.ContainsKey(position.NodeKey))
                {
                    _phaseNodeRects[position.NodeKey] = new Rect(position.X, position.Y, PhaseNodeWidth, PhaseNodeHeight);
                }
            }
        }

        private void CollectPhaseNodes(PipelinePhaseDebugNode node)
        {
            _phaseNodes[node.NodeKey] = node;
            _phaseNodeOrder.Add(node);
            for (int i = 0; i < node.Children.Count; i++) CollectPhaseNodes(node.Children[i]);
        }

        private float GetPhaseSubtreeWidth(PipelinePhaseDebugNode node)
        {
            if (node.Children.Count == 0) return PhaseNodeWidth;
            float width = 0f;
            for (int i = 0; i < node.Children.Count; i++)
            {
                if (i > 0) width += PhaseNodeGap;
                width += GetPhaseSubtreeWidth(node.Children[i]);
            }
            return Mathf.Max(PhaseNodeWidth, width);
        }

        private void LayoutPhaseSubtree(PipelinePhaseDebugNode node, float left, float top, float width)
        {
            _phaseNodeRects[node.NodeKey] = new Rect(
                left + (width - PhaseNodeWidth) * 0.5f,
                top,
                PhaseNodeWidth,
                PhaseNodeHeight);
            if (node.Children.Count == 0) return;

            float childLeft = left;
            float childTop = top + PhaseNodeHeight + PhaseLevelGap;
            for (int i = 0; i < node.Children.Count; i++)
            {
                float childWidth = GetPhaseSubtreeWidth(node.Children[i]);
                LayoutPhaseSubtree(node.Children[i], childLeft, childTop, childWidth);
                childLeft += childWidth + PhaseNodeGap;
            }
        }

        private void HandlePhaseGraphInput(EditorPipelineRegistry.DebugEntry entry, Rect canvas)
        {
            var current = Event.current;
            bool inside = canvas.Contains(current.mousePosition);
            Vector2 localMouse = current.mousePosition - canvas.position;

            if (inside && current.type == EventType.ScrollWheel)
            {
                float oldZoom = _phaseGraphZoom;
                float factor = Mathf.Pow(1.08f, -current.delta.y);
                _phaseGraphZoom = Mathf.Clamp(oldZoom * factor, 0.45f, 1.6f);
                Vector2 logical = (localMouse - _phaseGraphPan) / oldZoom;
                _phaseGraphPan = localMouse - logical * _phaseGraphZoom;
                current.Use();
                Repaint();
                return;
            }

            if (inside && current.type == EventType.MouseDown
                && (current.button == 2 || (current.button == 0 && current.alt)))
            {
                _isPhaseGraphPanning = true;
                _phaseGraphDragMouse = current.mousePosition;
                current.Use();
                return;
            }
            if (_isPhaseGraphPanning && current.type == EventType.MouseDrag)
            {
                _phaseGraphPan += current.mousePosition - _phaseGraphDragMouse;
                _phaseGraphDragMouse = current.mousePosition;
                current.Use();
                Repaint();
                return;
            }
            if (_isPhaseGraphPanning && (current.type == EventType.MouseUp || current.rawType == EventType.MouseUp))
            {
                _isPhaseGraphPanning = false;
                current.Use();
                return;
            }

            if (!inside || current.type != EventType.MouseDown || current.button != 0) return;
            for (int i = _phaseNodeOrder.Count - 1; i >= 0; i--)
            {
                var node = _phaseNodeOrder[i];
                if (!TransformPhaseRect(_phaseNodeRects[node.NodeKey]).Contains(localMouse)) continue;
                _selectedPhaseNodeKey = node.NodeKey;
                if (current.clickCount == 2) GoToPhaseTrace(node);
                current.Use();
                Repaint();
                return;
            }
            _selectedPhaseNodeKey = null;
            current.Use();
        }

        private void DrawPhaseGraphGrid(Vector2 canvasSize)
        {
            float step = Mathf.Max(12f, 24f * _phaseGraphZoom);
            float startX = Mathf.Repeat(_phaseGraphPan.x, step);
            float startY = Mathf.Repeat(_phaseGraphPan.y, step);
            Color color = EditorGUIUtility.isProSkin
                ? new Color(1f, 1f, 1f, 0.045f)
                : new Color(0f, 0f, 0f, 0.06f);
            Handles.BeginGUI();
            Handles.color = color;
            for (float x = startX; x < canvasSize.x; x += step) Handles.DrawLine(new Vector3(x, 0f), new Vector3(x, canvasSize.y));
            for (float y = startY; y < canvasSize.y; y += step) Handles.DrawLine(new Vector3(0f, y), new Vector3(canvasSize.x, y));
            Handles.EndGUI();
        }

        private void DrawPhaseGraphEdges(EditorPipelineRegistry.DebugEntry entry, Vector2 canvasSize)
        {
            Handles.BeginGUI();
            for (int i = 0; i < entry.Graph.Edges.Count; i++)
            {
                var edge = entry.Graph.Edges[i];
                if (!_phaseNodeRects.TryGetValue(edge.SourceNodeKey, out var sourceLogical)
                    || !_phaseNodeRects.TryGetValue(edge.TargetNodeKey, out var targetLogical)) continue;
                Rect source = TransformPhaseRect(sourceLogical);
                Rect target = TransformPhaseRect(targetLogical);
                bool flow = edge.Kind == EPipelineDebugEdgeKind.Flow;
                Vector2 start = flow
                    ? new Vector2(source.xMax, source.center.y)
                    : new Vector2(source.center.x, source.yMax);
                Vector2 end = flow
                    ? new Vector2(target.xMin, target.center.y)
                    : new Vector2(target.center.x, target.yMin);
                float bend = flow
                    ? Mathf.Max(28f, Mathf.Abs(end.x - start.x) * 0.42f)
                    : Mathf.Max(24f, Mathf.Abs(end.y - start.y) * 0.48f);
                Vector2 startTangent = flow ? start + Vector2.right * bend : start + Vector2.down * bend;
                Vector2 endTangent = flow ? end + Vector2.left * bend : end + Vector2.up * bend;
                Color edgeColor = ResolveEdgeColor(edge);
                float width = ResolveEdgeWidth(edge);
                Handles.DrawBezier(start, end, startTangent, endTangent, edgeColor, null, width);
                DrawEdgeArrow(end, endTangent, edgeColor);

                if (!string.IsNullOrEmpty(edge.Label) && _phaseGraphZoom >= 0.62f)
                {
                    Vector2 center = (start + end) * 0.5f;
                    var label = new GUIContent(edge.Label, edge.Label);
                    Vector2 size = _graphEdgeLabelStyle!.CalcSize(label);
                    float labelWidth = Mathf.Min(116f, size.x + 8f);
                    var labelRect = new Rect(center.x - labelWidth * 0.5f, center.y - 9f, labelWidth, 18f);
                    EditorGUI.DrawRect(labelRect, EditorGUIUtility.isProSkin
                        ? new Color(0.10f, 0.11f, 0.12f, 0.88f)
                        : new Color(0.82f, 0.83f, 0.84f, 0.92f));
                    GUI.Label(labelRect, label, _graphEdgeLabelStyle!);
                }
            }
            Handles.EndGUI();
        }

        private Color ResolveEdgeColor(PipelinePhaseDebugEdge edge)
        {
            if (_phaseStates.TryGetValue(edge.SourceNodeKey, out var source)
                && edge.Kind == EPipelineDebugEdgeKind.Condition
                && edge.ChildIndex >= 0)
            {
                if (source.SelectedChildIndex == edge.ChildIndex) return new Color(0.28f, 0.78f, 0.46f, 0.95f);
                if (edge.ChildIndex < source.ChildConditions.Count
                    && source.ChildConditions[edge.ChildIndex] == EPipelineDebugConditionResult.Rejected)
                {
                    return new Color(0.70f, 0.33f, 0.29f, 0.42f);
                }
            }
            if (_phaseStates.TryGetValue(edge.TargetNodeKey, out var target)
                && target.State == EPipelineDebugExecutionState.Active)
            {
                return new Color(0.93f, 0.66f, 0.20f, 0.95f);
            }
            return EditorGUIUtility.isProSkin
                ? new Color(0.56f, 0.60f, 0.65f, 0.62f)
                : new Color(0.28f, 0.31f, 0.35f, 0.65f);
        }

        private float ResolveEdgeWidth(PipelinePhaseDebugEdge edge)
        {
            if (_phaseStates.TryGetValue(edge.SourceNodeKey, out var source)
                && edge.Kind == EPipelineDebugEdgeKind.Condition
                && source.SelectedChildIndex == edge.ChildIndex) return 3f;
            if (_phaseStates.TryGetValue(edge.TargetNodeKey, out var target)
                && target.State == EPipelineDebugExecutionState.Active) return 3f;
            return 1.5f;
        }

        private static void DrawEdgeArrow(Vector2 end, Vector2 tangent, Color color)
        {
            Vector2 direction = (end - tangent).normalized;
            if (direction.sqrMagnitude < 0.01f) direction = Vector2.down;
            Vector2 side = new Vector2(-direction.y, direction.x);
            Handles.color = color;
            Handles.DrawAAConvexPolygon(
                end,
                end - direction * 8f + side * 4f,
                end - direction * 8f - side * 4f);
        }

        private void DrawPhaseGraphNodes(EditorPipelineRegistry.DebugEntry entry, Vector2 canvasSize)
        {
            for (int i = 0; i < _phaseNodeOrder.Count; i++)
            {
                var node = _phaseNodeOrder[i];
                Rect rect = TransformPhaseRect(_phaseNodeRects[node.NodeKey]);
                if (rect.xMax < 0f || rect.yMax < 0f || rect.xMin > canvasSize.x || rect.yMin > canvasSize.y) continue;
                DrawPhaseGraphNode(entry, node, rect);
            }
        }

        private void DrawPhaseGraphNode(
            EditorPipelineRegistry.DebugEntry entry,
            PipelinePhaseDebugNode node,
            Rect rect)
        {
            EPipelineDebugExecutionState state = ResolvePhaseState(entry, node);
            bool selected = _selectedPhaseNodeKey == node.NodeKey;
            Color background = EditorGUIUtility.isProSkin
                ? new Color(0.18f, 0.19f, 0.21f, 0.98f)
                : new Color(0.88f, 0.89f, 0.90f, 0.98f);
            EditorGUI.DrawRect(new Rect(rect.x + 2f, rect.y + 3f, rect.width, rect.height), new Color(0f, 0f, 0f, 0.28f));
            EditorGUI.DrawRect(rect, background);
            float headerHeight = Mathf.Max(16f, 22f * _phaseGraphZoom);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, headerHeight), KindColor(node.Kind));
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, Mathf.Max(3f, 4f * _phaseGraphZoom), rect.height), PhaseStateColor(state));

            Color border = selected ? new Color(0.35f, 0.68f, 1f) : PhaseStateColor(state);
            float borderWidth = selected || state == EPipelineDebugExecutionState.Active || state == EPipelineDebugExecutionState.Failed ? 2f : 1f;
            DrawRectBorder(rect, border, borderWidth);

            if (_phaseGraphZoom >= 0.58f)
            {
                float inset = Mathf.Max(6f, 8f * _phaseGraphZoom);
                DrawPhaseKindGlyph(node.Kind, new Rect(rect.x + inset, rect.y + 4f, 14f, headerHeight - 7f));
                GUI.Label(
                    new Rect(rect.x + inset + 18f, rect.y + 2f, rect.width - inset - 74f, headerHeight - 3f),
                    new GUIContent(node.Kind.ToString(), node.PhaseType),
                    _graphNodeMetaStyle);
                GUI.Label(
                    new Rect(rect.xMax - 62f, rect.y + 2f, 56f, headerHeight - 3f),
                    state == EPipelineDebugExecutionState.Pending ? string.Empty : state.ToString(),
                    _graphNodeStatusStyle);
                GUI.Label(
                    new Rect(rect.x + inset, rect.y + headerHeight + 4f, rect.width - inset * 2f, 19f),
                    new GUIContent(node.PhaseId.ToString(), node.PhaseType),
                    _graphNodeTitleStyle);
                if (_phaseGraphZoom >= 0.82f)
                {
                    GUI.Label(
                        new Rect(rect.x + inset, rect.y + headerHeight + 24f, rect.width - inset * 2f, 17f),
                        new GUIContent(string.IsNullOrEmpty(node.Summary) ? node.PhaseType : node.Summary, node.PhaseType),
                        _graphNodeMetaStyle);
                }
            }
            else
            {
                GUI.Label(rect, new GUIContent(node.PhaseId.ToString(), node.PhaseType), _graphNodeTitleStyle);
            }
        }

        private void DrawPhaseKindGlyph(EPipelineDebugNodeKind kind, Rect rect)
        {
            Handles.BeginGUI();
            Handles.color = new Color(1f, 1f, 1f, 0.88f);
            Vector2 center = rect.center;
            if (kind == EPipelineDebugNodeKind.Conditional || kind == EPipelineDebugNodeKind.Gate)
            {
                Handles.DrawAAPolyLine(1.5f,
                    new Vector3(center.x, rect.y),
                    new Vector3(rect.xMax, center.y),
                    new Vector3(center.x, rect.yMax),
                    new Vector3(rect.x, center.y),
                    new Vector3(center.x, rect.y));
            }
            else if (kind == EPipelineDebugNodeKind.Parallel)
            {
                Handles.DrawLine(new Vector3(rect.x + 2f, rect.y + 2f), new Vector3(rect.x + 2f, rect.yMax - 2f));
                Handles.DrawLine(new Vector3(center.x, rect.y + 2f), new Vector3(center.x, rect.yMax - 2f));
                Handles.DrawLine(new Vector3(rect.xMax - 2f, rect.y + 2f), new Vector3(rect.xMax - 2f, rect.yMax - 2f));
            }
            else
            {
                Handles.DrawLine(new Vector3(rect.x + 1f, rect.y + 3f), new Vector3(rect.xMax - 1f, rect.y + 3f));
                Handles.DrawLine(new Vector3(rect.x + 1f, center.y), new Vector3(rect.xMax - 1f, center.y));
                Handles.DrawLine(new Vector3(rect.x + 1f, rect.yMax - 3f), new Vector3(rect.xMax - 1f, rect.yMax - 3f));
            }
            Handles.EndGUI();
        }

        private Rect TransformPhaseRect(Rect logical)
        {
            return new Rect(
                _phaseGraphPan.x + logical.x * _phaseGraphZoom,
                _phaseGraphPan.y + logical.y * _phaseGraphZoom,
                logical.width * _phaseGraphZoom,
                logical.height * _phaseGraphZoom);
        }

        private void FitPhaseGraph(Vector2 canvasSize)
        {
            if (!TryGetPhaseGraphBounds(out Rect bounds)) return;
            float widthZoom = Mathf.Max(0.01f, (canvasSize.x - 56f) / bounds.width);
            float heightZoom = Mathf.Max(0.01f, (canvasSize.y - 56f) / bounds.height);
            _phaseGraphZoom = Mathf.Clamp(Mathf.Min(widthZoom, heightZoom), 0.45f, 1.15f);
            _phaseGraphPan = canvasSize * 0.5f - bounds.center * _phaseGraphZoom;
        }

        private void FocusPhaseNode(string nodeKey, Vector2 canvasSize)
        {
            if (!_phaseNodeRects.TryGetValue(nodeKey, out var rect)) return;
            _phaseGraphZoom = Mathf.Clamp(_phaseGraphZoom, 0.75f, 1.2f);
            _phaseGraphPan = canvasSize * 0.5f - rect.center * _phaseGraphZoom;
            _selectedPhaseNodeKey = nodeKey;
        }

        private bool TryGetPhaseGraphBounds(out Rect bounds)
        {
            bounds = default;
            bool found = false;
            foreach (var pair in _phaseNodeRects)
            {
                bounds = found ? UnionRect(bounds, pair.Value) : pair.Value;
                found = true;
            }
            return found;
        }

        private static Rect UnionRect(Rect left, Rect right)
        {
            float xMin = Mathf.Min(left.xMin, right.xMin);
            float yMin = Mathf.Min(left.yMin, right.yMin);
            float xMax = Mathf.Max(left.xMax, right.xMax);
            float yMax = Mathf.Max(left.yMax, right.yMax);
            return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }

        private bool TryFindFocusPhaseNode(out string? nodeKey)
        {
            foreach (var pair in _phaseStates)
            {
                if (pair.Value.State == EPipelineDebugExecutionState.Failed)
                {
                    nodeKey = pair.Key;
                    return true;
                }
            }
            foreach (var pair in _phaseStates)
            {
                if (pair.Value.State == EPipelineDebugExecutionState.Active)
                {
                    nodeKey = pair.Key;
                    return true;
                }
            }
            nodeKey = null;
            return false;
        }

        private EPipelineDebugExecutionState ResolvePhaseState(
            EditorPipelineRegistry.DebugEntry entry,
            PipelinePhaseDebugNode node)
        {
            if (_phaseStates.TryGetValue(node.NodeKey, out var state)) return state.State;
            for (int i = 0; i < entry.ActivePhases.Count; i++)
            {
                if (entry.ActivePhases[i] == node.PhaseId) return EPipelineDebugExecutionState.Active;
            }

            var trace = EditorPipelineRegistry.Instance.GetTraceSnapshot(entry.OwnerId);
            EPipelineDebugExecutionState fallback = EPipelineDebugExecutionState.Pending;
            for (int i = 0; i < trace.Count; i++)
            {
                if (trace[i].PhaseId != node.PhaseId) continue;
                if (trace[i].Type == EPipelineTraceEventType.PhaseError) fallback = EPipelineDebugExecutionState.Failed;
                else if (trace[i].Type == EPipelineTraceEventType.PhaseComplete
                         && fallback != EPipelineDebugExecutionState.Failed) fallback = EPipelineDebugExecutionState.Completed;
            }
            return fallback;
        }

        private static Color KindColor(EPipelineDebugNodeKind kind)
        {
            return kind switch
            {
                EPipelineDebugNodeKind.Sequence => new Color(0.25f, 0.40f, 0.58f, 0.96f),
                EPipelineDebugNodeKind.Parallel => new Color(0.20f, 0.50f, 0.50f, 0.96f),
                EPipelineDebugNodeKind.Conditional => new Color(0.58f, 0.42f, 0.16f, 0.98f),
                EPipelineDebugNodeKind.Gate => new Color(0.62f, 0.32f, 0.18f, 0.98f),
                EPipelineDebugNodeKind.Composite => new Color(0.35f, 0.38f, 0.48f, 0.96f),
                _ => new Color(0.32f, 0.34f, 0.37f, 0.96f)
            };
        }

        private static Color PhaseStateColor(EPipelineDebugExecutionState state)
        {
            return state switch
            {
                EPipelineDebugExecutionState.Active => new Color(0.94f, 0.66f, 0.18f, 1f),
                EPipelineDebugExecutionState.Completed => new Color(0.25f, 0.72f, 0.43f, 1f),
                EPipelineDebugExecutionState.Skipped => new Color(0.46f, 0.49f, 0.53f, 0.78f),
                EPipelineDebugExecutionState.Failed => new Color(0.88f, 0.25f, 0.22f, 1f),
                _ => new Color(0.45f, 0.48f, 0.52f, 0.75f)
            };
        }

        private static void DrawRectBorder(Rect rect, Color color, float width)
        {
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, width), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - width, rect.width, width), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, width, rect.height), color);
            EditorGUI.DrawRect(new Rect(rect.xMax - width, rect.y, width, rect.height), color);
        }

        private void GoToPhaseTrace(PipelinePhaseDebugNode node)
        {
            _traceSearch = node.PhaseId.ToString();
            _traceFilter = TraceFilter.Phases;
            _detailTab = DetailTab.Trace;
            _traceScroll = Vector2.zero;
        }

        private void DrawTrace(EditorPipelineRegistry.DebugEntry entry)
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            string traceSearch = GUILayout.TextField(_traceSearch, EditorStyles.toolbarSearchField, GUILayout.MinWidth(80f));
            if (traceSearch != _traceSearch) _traceSearch = traceSearch;
            if (!string.IsNullOrEmpty(_traceSearch) && GUILayout.Button("x", EditorStyles.toolbarButton, GUILayout.Width(22f)))
            {
                _traceSearch = string.Empty;
                GUI.FocusControl(null);
            }
            _traceFilter = (TraceFilter)EditorGUILayout.EnumPopup(_traceFilter, EditorStyles.toolbarPopup, GUILayout.Width(90f));
            _relativeTraceTime = GUILayout.Toggle(
                _relativeTraceTime,
                new GUIContent("Relative", "Show time since the run started"),
                EditorStyles.toolbarButton,
                GUILayout.Width(62f));

            using (new EditorGUI.DisabledScope(!_selectedTraceSequence.HasValue))
            {
                if (GUILayout.Button(IconOnly("Clipboard", "Copy selected trace event"), EditorStyles.toolbarButton, GUILayout.Width(28f)))
                {
                    CopySelectedTrace(entry);
                }
            }
            if (GUILayout.Button(IconOnly("TreeEditor.Trash", "Clear trace for this run"), EditorStyles.toolbarButton, GUILayout.Width(28f)))
            {
                EditorPipelineRegistry.Instance.ClearTrace(entry.OwnerId);
                _selectedTraceSequence = null;
            }
            EditorGUILayout.EndHorizontal();

            var trace = EditorPipelineRegistry.Instance.GetTraceSnapshot(entry.OwnerId);
            DrawTraceHeader();
            _traceScroll = EditorGUILayout.BeginScrollView(_traceScroll);
            int visibleCount = 0;
            for (int i = 0; i < trace.Count; i++)
            {
                var item = trace[i];
                if (!MatchesTraceFilter(item) || !MatchesTraceSearch(item)) continue;
                DrawTraceRow(entry, item, visibleCount++);
            }
            if (visibleCount == 0)
            {
                GUILayout.Space(18f);
                GUILayout.Label("No matching trace events", _centeredMutedStyle);
            }
            EditorGUILayout.EndScrollView();
            DrawSelectedTraceDetail(trace);
        }

        private void DrawTraceHeader()
        {
            var row = GUILayoutUtility.GetRect(0f, 20f, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(row, new Color(0f, 0f, 0f, 0.18f));
            GUI.Label(new Rect(row.x + 6f, row.y + 2f, 42f, 18f), "Seq", EditorStyles.miniBoldLabel);
            GUI.Label(new Rect(row.x + 48f, row.y + 2f, 82f, 18f), _relativeTraceTime ? "Time" : "UTC", EditorStyles.miniBoldLabel);
            GUI.Label(new Rect(row.x + 130f, row.y + 2f, 92f, 18f), "Event", EditorStyles.miniBoldLabel);
            GUI.Label(new Rect(row.x + 222f, row.y + 2f, 130f, 18f), "Phase", EditorStyles.miniBoldLabel);
            GUI.Label(new Rect(row.x + 352f, row.y + 2f, Mathf.Max(0f, row.width - 358f), 18f), "Message", EditorStyles.miniBoldLabel);
        }

        private void DrawTraceRow(EditorPipelineRegistry.DebugEntry entry, PipelineTraceEvent item, int visibleIndex)
        {
            var row = GUILayoutUtility.GetRect(0f, 22f, GUILayout.ExpandWidth(true));
            bool selected = _selectedTraceSequence == item.Seq;
            if (Event.current.type == EventType.Repaint)
            {
                var color = selected
                    ? new Color(0.18f, 0.36f, 0.55f, 0.58f)
                    : new Color(1f, 1f, 1f, visibleIndex % 2 == 0 ? 0.025f : 0f);
                EditorGUI.DrawRect(row, color);
                if (item.Type == EPipelineTraceEventType.PhaseError)
                {
                    EditorGUI.DrawRect(new Rect(row.x, row.y, 3f, row.height), new Color(0.85f, 0.23f, 0.20f));
                }
            }

            string time = _relativeTraceTime
                ? "+" + Math.Max(0d, (item.UtcTime - entry.RegisteredAtUtc).TotalSeconds).ToString("0.000")
                : item.UtcTime.ToString("HH:mm:ss.fff");
            GUI.Label(new Rect(row.x + 6f, row.y + 2f, 42f, 18f), item.Seq.ToString(), _monoStyle);
            GUI.Label(new Rect(row.x + 48f, row.y + 2f, 82f, 18f), time, _monoStyle);
            GUI.Label(new Rect(row.x + 130f, row.y + 2f, 92f, 18f), item.Type.ToString(), EditorStyles.miniLabel);
            GUI.Label(new Rect(row.x + 222f, row.y + 2f, 130f, 18f), item.PhaseId.ToString(), EditorStyles.miniLabel);
            GUI.Label(new Rect(row.x + 352f, row.y + 2f, Mathf.Max(0f, row.width - 358f), 18f), item.Message, EditorStyles.miniLabel);

            if (Event.current.type == EventType.MouseDown && row.Contains(Event.current.mousePosition))
            {
                _selectedTraceSequence = item.Seq;
                if (Event.current.clickCount == 2 && !string.IsNullOrEmpty(item.PhaseId.Value)) _detailTab = DetailTab.Phases;
                Event.current.Use();
            }
        }

        private void DrawSelectedTraceDetail(IReadOnlyList<PipelineTraceEvent> trace)
        {
            if (!_selectedTraceSequence.HasValue) return;
            for (int i = 0; i < trace.Count; i++)
            {
                var item = trace[i];
                if (item.Seq != _selectedTraceSequence.Value) continue;
                EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.MinHeight(58f));
                EditorGUILayout.LabelField(
                    "#" + item.Seq + "  " + item.Type + "  |  " + item.State + "  |  " + item.PhaseId,
                    EditorStyles.miniBoldLabel);
                var wrappedStyle = _wrappedValueStyle!;
                float height = Mathf.Clamp(
                    wrappedStyle.CalcHeight(new GUIContent(item.Message), Mathf.Max(120f, position.width - _runPaneWidth - 30f)),
                    EditorGUIUtility.singleLineHeight,
                    54f);
                EditorGUILayout.SelectableLabel(item.Message, wrappedStyle, GUILayout.Height(height));
                EditorGUILayout.EndVertical();
                return;
            }
            _selectedTraceSequence = null;
        }

        private bool MatchesTraceFilter(PipelineTraceEvent item)
        {
            return _traceFilter switch
            {
                TraceFilter.Lifecycle => item.Type == EPipelineTraceEventType.RunStart || item.Type == EPipelineTraceEventType.RunEnd,
                TraceFilter.Phases => item.Type == EPipelineTraceEventType.PhaseStart || item.Type == EPipelineTraceEventType.PhaseComplete || item.Type == EPipelineTraceEventType.PhaseError,
                TraceFilter.Errors => item.Type == EPipelineTraceEventType.PhaseError || item.State == EAbilityPipelineState.Failed,
                TraceFilter.Control => item.Type == EPipelineTraceEventType.Pause || item.Type == EPipelineTraceEventType.Resume || item.Type == EPipelineTraceEventType.Interrupt,
                _ => true
            };
        }

        private bool MatchesTraceSearch(PipelineTraceEvent item)
        {
            if (string.IsNullOrWhiteSpace(_traceSearch)) return true;
            return Contains(item.PhaseId.ToString(), _traceSearch)
                || Contains(item.Message, _traceSearch)
                || Contains(item.State.ToString(), _traceSearch)
                || Contains(item.Type.ToString(), _traceSearch);
        }

        private void CopySelectedTrace(EditorPipelineRegistry.DebugEntry entry)
        {
            if (!_selectedTraceSequence.HasValue) return;
            var trace = EditorPipelineRegistry.Instance.GetTraceSnapshot(entry.OwnerId);
            for (int i = 0; i < trace.Count; i++)
            {
                if (trace[i].Seq != _selectedTraceSequence.Value) continue;
                EditorGUIUtility.systemCopyBuffer = trace[i].ToString();
                return;
            }
        }

        private void DrawContext(EditorPipelineRegistry.DebugEntry entry)
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            string contextSearch = GUILayout.TextField(_contextSearch, EditorStyles.toolbarSearchField, GUILayout.ExpandWidth(true));
            if (contextSearch != _contextSearch) _contextSearch = contextSearch;
            if (!string.IsNullOrEmpty(_contextSearch) && GUILayout.Button("x", EditorStyles.toolbarButton, GUILayout.Width(22f)))
            {
                _contextSearch = string.Empty;
                GUI.FocusControl(null);
            }
            _showOnlyChangedContext = GUILayout.Toggle(
                _showOnlyChangedContext,
                new GUIContent("Changed", "Show fields whose value changed since run start"),
                EditorStyles.toolbarButton,
                GUILayout.Width(64f));
            using (new EditorGUI.DisabledScope(entry.ContextValues.Count == 0))
            {
                if (GUILayout.Button(IconOnly("Clipboard", "Copy visible context values"), EditorStyles.toolbarButton, GUILayout.Width(28f)))
                {
                    CopyVisibleContext(entry);
                }
            }
            EditorGUILayout.EndHorizontal();

            _contextScroll = EditorGUILayout.BeginScrollView(_contextScroll);
            GUILayout.Space(8f);
            DrawContextHeader(entry.ContextType);
            int visibleCount = 0;
            BuildContextNames(entry);
            for (int i = 0; i < _contextNames.Count; i++)
            {
                string name = _contextNames[i];
                string initial = FindContextValue(entry.InitialContextValues, name, "<not captured>");
                string current = FindContextValue(entry.ContextValues, name, "<removed>");
                bool changed = initial != current;
                if (_showOnlyChangedContext && !changed) continue;
                if (!MatchesContextSearch(name, initial, current)) continue;
                DrawContextRow(name, initial, current, changed, visibleCount);
                visibleCount++;
            }
            if (visibleCount == 0)
            {
                GUILayout.Space(12f);
                GUILayout.Label(
                    entry.ContextValues.Count == 0 ? "No context snapshot available" : "No matching context values",
                    _centeredMutedStyle);
            }
            EditorGUILayout.EndScrollView();
        }

        private bool MatchesContextSearch(string name, string initial, string current)
        {
            return string.IsNullOrWhiteSpace(_contextSearch)
                || Contains(name, _contextSearch)
                || Contains(initial, _contextSearch)
                || Contains(current, _contextSearch);
        }

        private void CopyVisibleContext(EditorPipelineRegistry.DebugEntry entry)
        {
            var builder = new StringBuilder();
            BuildContextNames(entry);
            for (int i = 0; i < _contextNames.Count; i++)
            {
                string name = _contextNames[i];
                string initial = FindContextValue(entry.InitialContextValues, name, "<not captured>");
                string current = FindContextValue(entry.ContextValues, name, "<removed>");
                bool changed = initial != current;
                if (_showOnlyChangedContext && !changed) continue;
                if (!MatchesContextSearch(name, initial, current)) continue;
                if (builder.Length > 0) builder.AppendLine();
                builder.Append(name).Append(" = ");
                if (changed) builder.Append(initial).Append(" -> ");
                builder.Append(current);
            }
            EditorGUIUtility.systemCopyBuffer = builder.ToString();
        }

        private void BuildContextNames(EditorPipelineRegistry.DebugEntry entry)
        {
            _contextNames.Clear();
            AddContextNames(entry.InitialContextValues);
            AddContextNames(entry.ContextValues);
            _contextNames.Sort(StringComparer.OrdinalIgnoreCase);
        }

        private void AddContextNames(IReadOnlyList<EditorPipelineRegistry.DebugValue> values)
        {
            for (int i = 0; i < values.Count; i++)
            {
                if (!_contextNames.Contains(values[i].Name)) _contextNames.Add(values[i].Name);
            }
        }

        private static string FindContextValue(
            IReadOnlyList<EditorPipelineRegistry.DebugValue> values,
            string name,
            string fallback)
        {
            for (int i = 0; i < values.Count; i++)
            {
                if (values[i].Name == name) return values[i].Value;
            }
            return fallback;
        }

        private void DrawContextHeader(string contextType)
        {
            GUILayout.Label(contextType, _sectionStyle);
            var row = GUILayoutUtility.GetRect(0f, 20f, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(row, new Color(0f, 0f, 0f, 0.18f));
            float nameWidth = Mathf.Clamp(row.width * 0.26f, 110f, 190f);
            float valueWidth = Mathf.Max(80f, (row.width - nameWidth - 16f) * 0.5f);
            GUI.Label(new Rect(row.x + 6f, row.y + 2f, nameWidth, 18f), "Field", EditorStyles.miniBoldLabel);
            GUI.Label(new Rect(row.x + nameWidth + 8f, row.y + 2f, valueWidth, 18f), "Start", EditorStyles.miniBoldLabel);
            GUI.Label(new Rect(row.x + nameWidth + valueWidth + 10f, row.y + 2f, valueWidth, 18f), "Current", EditorStyles.miniBoldLabel);
        }

        private void DrawContextRow(string name, string initial, string current, bool changed, int visibleIndex)
        {
            var row = GUILayoutUtility.GetRect(0f, 24f, GUILayout.ExpandWidth(true));
            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(row, new Color(1f, 1f, 1f, visibleIndex % 2 == 0 ? 0.025f : 0f));
                if (changed)
                {
                    EditorGUI.DrawRect(new Rect(row.x, row.y, 3f, row.height), new Color(0.90f, 0.63f, 0.16f));
                }
            }
            float nameWidth = Mathf.Clamp(row.width * 0.26f, 110f, 190f);
            float valueWidth = Mathf.Max(80f, (row.width - nameWidth - 16f) * 0.5f);
            GUI.Label(
                new Rect(row.x + 6f, row.y + 3f, nameWidth, 18f),
                new GUIContent(name, name),
                changed ? EditorStyles.miniBoldLabel : _mutedStyle);
            GUI.Label(
                new Rect(row.x + nameWidth + 8f, row.y + 3f, valueWidth, 18f),
                new GUIContent(initial, initial),
                _monoStyle);
            GUI.Label(
                new Rect(row.x + nameWidth + valueWidth + 10f, row.y + 3f, valueWidth, 18f),
                new GUIContent(current, current),
                _monoStyle);
        }

        private void SelectRun(int? runId)
        {
            if (_selectedRunId == runId) return;
            _selectedRunId = runId;
            _selectedTraceSequence = null;
            _selectedPhaseNodeKey = null;
            _phaseGraphNeedsFit = true;
            _detailScroll = Vector2.zero;
            _traceScroll = Vector2.zero;
            _contextScroll = Vector2.zero;
            EditorPipelineRegistry.Instance.SelectedRunId = runId;
        }

        private bool TryGetSelectedEntry(out EditorPipelineRegistry.DebugEntry? entry)
        {
            if (!_selectedRunId.HasValue)
            {
                entry = null;
                return false;
            }
            return EditorPipelineRegistry.Instance.TryGetEntry(_selectedRunId.Value, out entry);
        }

        private void SaveSession(EditorPipelineRegistry.DebugEntry entry)
        {
            string defaultName = SanitizeAssetName(
                (string.IsNullOrWhiteSpace(entry.OwnerName) ? "PipelineRun" : entry.OwnerName)
                + "-"
                + entry.OwnerId);
            string path = EditorUtility.SaveFilePanelInProject(
                "Save Pipeline Debug Session",
                defaultName,
                "asset",
                "Save a copied diagnostic snapshot. Live runtime objects are not serialized.");
            if (string.IsNullOrEmpty(path)) return;

            var asset = CreateInstance<PipelineDebugSessionAsset>();
            asset.name = defaultName;
            asset.Capture(entry, EditorPipelineRegistry.Instance.GetTraceSnapshot(entry.OwnerId));
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
        }

        private void CopyRunSummary(EditorPipelineRegistry.DebugEntry entry)
        {
            var builder = new StringBuilder();
            builder.Append(entry.OwnerName).Append("  #").Append(entry.OwnerId).AppendLine();
            builder.Append("State: ").Append(entry.IsPaused ? "Executing (Paused)" : entry.LastState.ToString()).AppendLine();
            builder.Append("Phase: ").Append(entry.LastPhaseId).AppendLine();
            builder.Append("Duration: ").Append(entry.WallDurationSeconds.ToString("0.000")).AppendLine(" s");
            builder.Append("Pipeline: ").Append(entry.PipelineType);
            string? failure = FindLastError(entry);
            if (!string.IsNullOrEmpty(failure)) builder.AppendLine().Append("Failure: ").Append(failure);
            EditorGUIUtility.systemCopyBuffer = builder.ToString();
        }

        private void GoToFailure(EditorPipelineRegistry.DebugEntry entry)
        {
            var trace = EditorPipelineRegistry.Instance.GetTraceSnapshot(entry.OwnerId);
            for (int i = trace.Count - 1; i >= 0; i--)
            {
                if (trace[i].Type != EPipelineTraceEventType.PhaseError
                    && trace[i].State != EAbilityPipelineState.Failed) continue;
                _selectedTraceSequence = trace[i].Seq;
                break;
            }
            _traceFilter = TraceFilter.Errors;
            _traceSearch = string.Empty;
            _detailTab = DetailTab.Trace;
            _traceScroll = Vector2.zero;
        }

        private void ShowOptionsMenu()
        {
            var state = PipelineDebuggerUserState.instance;
            var menu = new GenericMenu();
            AddCapacityOptions(menu, "History capacity/", new[] { 32, 128, 512, 2048 }, state.HistoryCapacity, value =>
            {
                state.HistoryCapacity = value;
                EditorPipelineRegistry.Instance.ConfigureStorage(state.HistoryCapacity, state.TraceCapacity);
                state.SaveNow();
            });
            AddCapacityOptions(menu, "Trace capacity (new runs)/", new[] { 512, 2048, 8192, 32768 }, state.TraceCapacity, value =>
            {
                state.TraceCapacity = value;
                EditorPipelineRegistry.Instance.ConfigureStorage(state.HistoryCapacity, state.TraceCapacity);
                state.SaveNow();
            });
            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Relative trace time"), _relativeTraceTime, () =>
            {
                _relativeTraceTime = !_relativeTraceTime;
                Repaint();
            });
            menu.AddItem(new GUIContent("Confirm interrupt"), _confirmInterrupt, () =>
            {
                _confirmInterrupt = !_confirmInterrupt;
                Repaint();
            });
            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Refresh/20 fps"), Mathf.Approximately(_refreshIntervalSeconds, 0.05f), () => SetRefreshInterval(0.05f));
            menu.AddItem(new GUIContent("Refresh/10 fps"), Mathf.Approximately(_refreshIntervalSeconds, 0.1f), () => SetRefreshInterval(0.1f));
            menu.AddItem(new GUIContent("Refresh/4 fps"), Mathf.Approximately(_refreshIntervalSeconds, 0.25f), () => SetRefreshInterval(0.25f));
            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Reset window state"), false, ResetWindowState);
            menu.ShowAsContext();
        }

        private static void AddCapacityOptions(
            GenericMenu menu,
            string prefix,
            IReadOnlyList<int> values,
            int selected,
            Action<int> setValue)
        {
            for (int i = 0; i < values.Count; i++)
            {
                int value = values[i];
                menu.AddItem(new GUIContent(prefix + value), value == selected, () => setValue(value));
            }
        }

        private void SetRefreshInterval(float value)
        {
            _refreshIntervalSeconds = value;
            _nextRefreshAt = 0d;
            PersistUserState();
        }

        private void ResetWindowState()
        {
            _runSearch = string.Empty;
            _traceSearch = string.Empty;
            _contextSearch = string.Empty;
            _runFilter = RunFilter.All;
            _detailTab = DetailTab.Overview;
            _traceFilter = TraceFilter.All;
            _followLatest = true;
            _relativeTraceTime = true;
            _confirmInterrupt = true;
            _showOnlyChangedContext = false;
            _showPhaseGraph = true;
            _phaseGraphNeedsFit = true;
            _runPaneWidth = 300f;
            _refreshIntervalSeconds = 0.1f;
            var state = PipelineDebuggerUserState.instance;
            state.HistoryCapacity = 128;
            state.TraceCapacity = 2048;
            EditorPipelineRegistry.Instance.ConfigureStorage(state.HistoryCapacity, state.TraceCapacity);
            PersistUserState();
            Repaint();
        }

        private void RestoreUserState()
        {
            var state = PipelineDebuggerUserState.instance;
            _followLatest = state.FollowLatest;
            _relativeTraceTime = state.RelativeTraceTime;
            _confirmInterrupt = state.ConfirmInterrupt;
            _showOnlyChangedContext = state.ShowOnlyChangedContext;
            _showPhaseGraph = state.ShowPhaseGraph;
            _runFilter = Enum.IsDefined(typeof(RunFilter), state.RunFilter) ? (RunFilter)state.RunFilter : RunFilter.All;
            _detailTab = Enum.IsDefined(typeof(DetailTab), state.DetailTab) ? (DetailTab)state.DetailTab : DetailTab.Overview;
            _traceFilter = Enum.IsDefined(typeof(TraceFilter), state.TraceFilter) ? (TraceFilter)state.TraceFilter : TraceFilter.All;
            _runSearch = state.RunSearch;
            _traceSearch = state.TraceSearch;
            _contextSearch = state.ContextSearch;
            _runPaneWidth = state.RunPaneWidth;
            _refreshIntervalSeconds = state.RefreshIntervalSeconds;

            var registry = EditorPipelineRegistry.Instance;
            registry.IsCaptureEnabled = state.CaptureEnabled;
            registry.ConfigureStorage(state.HistoryCapacity, state.TraceCapacity);
        }

        private void PersistUserState()
        {
            var state = PipelineDebuggerUserState.instance;
            state.CaptureEnabled = EditorPipelineRegistry.Instance.IsCaptureEnabled;
            state.FollowLatest = _followLatest;
            state.RelativeTraceTime = _relativeTraceTime;
            state.ConfirmInterrupt = _confirmInterrupt;
            state.ShowOnlyChangedContext = _showOnlyChangedContext;
            state.ShowPhaseGraph = _showPhaseGraph;
            state.RunFilter = (int)_runFilter;
            state.DetailTab = (int)_detailTab;
            state.TraceFilter = (int)_traceFilter;
            state.RunSearch = _runSearch;
            state.TraceSearch = _traceSearch;
            state.ContextSearch = _contextSearch;
            state.RunPaneWidth = _runPaneWidth;
            state.RefreshIntervalSeconds = _refreshIntervalSeconds;
            state.SaveNow();
        }

        private string? FindLastError(EditorPipelineRegistry.DebugEntry entry)
        {
            var trace = EditorPipelineRegistry.Instance.GetTraceSnapshot(entry.OwnerId);
            for (int i = trace.Count - 1; i >= 0; i--)
            {
                if (trace[i].State == EAbilityPipelineState.Failed && !string.IsNullOrEmpty(trace[i].Message))
                {
                    return trace[i].Message;
                }
            }
            return null;
        }

        private void DrawSectionTitle(string title)
        {
            GUILayout.Label(title, _sectionStyle);
            var line = GUILayoutUtility.GetRect(1f, 1f, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(line, new Color(1f, 1f, 1f, 0.08f));
            GUILayout.Space(4f);
        }

        private void DrawKeyValue(string key, string value, bool wrap = false)
        {
            string text = value ?? string.Empty;
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(key, _mutedStyle, GUILayout.Width(138f));
            var style = wrap ? _wrappedValueStyle! : _monoStyle!;
            float availableWidth = Mathf.Max(140f, position.width - _runPaneWidth - 180f);
            float height = wrap
                ? Mathf.Clamp(style.CalcHeight(new GUIContent(text), availableWidth), EditorGUIUtility.singleLineHeight, 54f)
                : EditorGUIUtility.singleLineHeight;
            EditorGUILayout.SelectableLabel(text, style, GUILayout.Height(height));
            EditorGUILayout.EndHorizontal();
        }

        private void EnsureStyles()
        {
            if (_runTitleStyle != null) return;
            _runTitleStyle = new GUIStyle(EditorStyles.boldLabel) { clipping = TextClipping.Clip };
            _mutedStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                clipping = TextClipping.Clip,
                normal =
                {
                    textColor = EditorGUIUtility.isProSkin
                        ? new Color(0.68f, 0.70f, 0.73f)
                        : new Color(0.34f, 0.36f, 0.39f)
                }
            };
            _centeredMutedStyle = new GUIStyle(_mutedStyle)
            {
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true
            };
            _sectionStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 12,
                clipping = TextClipping.Clip
            };
            _monoStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                font = EditorStyles.miniLabel.font,
                clipping = TextClipping.Clip
            };
            _wrappedValueStyle = new GUIStyle(_monoStyle)
            {
                wordWrap = true,
                clipping = TextClipping.Clip
            };
            _graphNodeTitleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                clipping = TextClipping.Clip,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = EditorGUIUtility.isProSkin ? Color.white : new Color(0.10f, 0.11f, 0.12f) }
            };
            _graphNodeMetaStyle = new GUIStyle(_mutedStyle)
            {
                clipping = TextClipping.Clip,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = EditorGUIUtility.isProSkin ? new Color(0.82f, 0.84f, 0.87f) : new Color(0.20f, 0.22f, 0.24f) }
            };
            _graphNodeStatusStyle = new GUIStyle(_graphNodeMetaStyle)
            {
                alignment = TextAnchor.MiddleRight
            };
            _graphEdgeLabelStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                clipping = TextClipping.Clip
            };
        }

        private static GUIContent IconOnly(string iconName, string tooltip)
        {
            var source = EditorGUIUtility.IconContent(iconName);
            return new GUIContent(source.image, tooltip);
        }

        private static GUIContent IconText(string iconName, string text, string tooltip)
        {
            var source = EditorGUIUtility.IconContent(iconName);
            return new GUIContent(text, source.image, tooltip);
        }

        private static Color StateColor(EditorPipelineRegistry.DebugEntry entry)
        {
            if (entry.IsPaused) return new Color(0.90f, 0.63f, 0.16f);
            return entry.LastState switch
            {
                EAbilityPipelineState.Completed => new Color(0.22f, 0.68f, 0.38f),
                EAbilityPipelineState.Failed => new Color(0.85f, 0.23f, 0.20f),
                EAbilityPipelineState.Executing => new Color(0.20f, 0.53f, 0.86f),
                _ => new Color(0.50f, 0.52f, 0.56f)
            };
        }

        private static string FormatDuration(EditorPipelineRegistry.DebugEntry entry)
        {
            double seconds = entry.WallDurationSeconds;
            if (seconds < 60d) return seconds.ToString("0.0") + "s";
            return TimeSpan.FromSeconds(seconds).ToString(@"mm\:ss");
        }

        private static int CountPhaseNodes(IReadOnlyList<PipelinePhaseDebugNode> nodes)
        {
            int count = nodes.Count;
            for (int i = 0; i < nodes.Count; i++) count += CountPhaseNodes(nodes[i].Children);
            return count;
        }

        private static string SanitizeAssetName(string value)
        {
            var invalid = System.IO.Path.GetInvalidFileNameChars();
            var builder = new StringBuilder(value.Length);
            for (int i = 0; i < value.Length; i++)
            {
                builder.Append(Array.IndexOf(invalid, value[i]) >= 0 ? '_' : value[i]);
            }
            return builder.ToString();
        }

        private static bool Contains(string? value, string search)
        {
            return value != null && value.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}

#endif
