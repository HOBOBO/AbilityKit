#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using AbilityKit.Ability.Config.Authoring;
using AbilityKit.Ability.Editor.Packages;
using AbilityKit.Ability.Editor.Panels;
using AbilityKit.Ability.Editor.Utilities;
using AbilityKit.Ability.Editor.Windows;
using AbilityKit.Editor.Platform.Commands;
using AbilityKit.Editor.Platform.Diagnostics;
using AbilityKit.Editor.Platform.UI;
using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities.Editor;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace AbilityKit.Ability.Editor.Inspectors
{
    internal interface ITriggerAuthoringNodeContextMenuContributor
    {
        void Populate(TriggerAuthoringNodeContextMenuContext context);
    }

    internal sealed class TriggerAuthoringNodeContextMenuContext
    {
        public GenericMenu Menu;
        public TriggerNodeKind Kind;
        public TriggerNodeData Node;
        public TriggerTypeDescriptor Descriptor;
        public bool CanPasteChild;
        public bool CanAddChild;
        public Action Copy;
        public Action PasteChild;
        public Action ChangeType;
        public Action SelectGroup;
        public Action ExtractGroup;
        public Action LocalizeGroup;
        public Action ExtractTrigger;
        public Action LocalizeTrigger;
        public Action NavigateTrigger;
        public Action ToggleEnabled;
        public Action AddDebugLogChild;
        public Action InsertDebugLogBefore;
        public Action InsertDebugLogAfter;
        public Action Remove;
    }

    internal sealed class TriggerAuthoringDefaultNodeContextMenuContributor :
        ITriggerAuthoringNodeContextMenuContributor
    {
        public void Populate(TriggerAuthoringNodeContextMenuContext context)
        {
            var menu = context.Menu;
            menu.AddItem(new GUIContent("复制节点"), false, () => context.Copy?.Invoke());
            AddOptional(menu, "粘贴为子节点", context.CanPasteChild, context.PasteChild);
            menu.AddSeparator(string.Empty);
            AddOptional(menu, context.Node != null && context.Node.Enabled ? "停用节点" : "启用节点", true, context.ToggleEnabled);
            AddOptional(menu, "更改类型", true, context.ChangeType);
            if (TriggerAuthoringTriggerReuse.IsReference(context.Node))
            {
                AddOptional(menu, "复用/定位触发效果", true, context.NavigateTrigger);
                AddOptional(menu, "复用/转为本地副本", true, context.LocalizeTrigger);
            }
            else if (context.Node != null && !string.IsNullOrWhiteSpace(context.Node.GroupReference))
            {
                AddOptional(menu, "复用/选择其他分组", true, context.SelectGroup);
                AddOptional(menu, "复用/转为本地副本", true, context.LocalizeGroup);
            }
            else
            {
                if (context.Kind == TriggerNodeKind.Action)
                    AddOptional(menu, "复用/提取为独立触发效果...", true, context.ExtractTrigger);
                else
                    AddOptional(menu, "复用/提取为可复用分组...", true, context.ExtractGroup);
                AddOptional(menu, "复用/替换为已有分组", true, context.SelectGroup);
            }
            menu.AddSeparator(string.Empty);
            if (context.Kind == TriggerNodeKind.Action)
            {
                AddOptional(menu, "调试日志/添加子节点", context.CanAddChild, context.AddDebugLogChild);
                AddOptional(menu, "调试日志/在前方插入", context.InsertDebugLogBefore != null, context.InsertDebugLogBefore);
                AddOptional(menu, "调试日志/在后方插入", context.InsertDebugLogAfter != null, context.InsertDebugLogAfter);
            }
            else
            {
                menu.AddDisabledItem(new GUIContent("调试日志/仅行为节点可用"));
            }
            menu.AddSeparator(string.Empty);
            AddOptional(menu, "删除节点", context.Remove != null, context.Remove);
        }

        private static void AddOptional(GenericMenu menu, string label, bool enabled, Action action)
        {
            if (enabled && action != null) menu.AddItem(new GUIContent(label), false, () => action());
            else menu.AddDisabledItem(new GUIContent(label));
        }
    }

    /// <summary>
    /// 模块资产的共享绘制器：Module Inspector 与 TriggerAuthoringWorkspaceWindow 共用同一份编辑 UI。
    /// 纯 IMGUI 类，不持有 Editor 生命周期；宿主通过 RepaintRequested 订阅重绘。
    /// </summary>
    internal sealed class TriggerAuthoringModuleDrawer : IDisposable
    {
        private enum TriggerSemanticArea
        {
            Event,
            Condition,
            Action
        }

        private enum TriggerEditorTab
        {
            Overview,
            RuleTree,
            Settings
        }

        private enum TriggerWorkspaceMode
        {
            RuleEditor,
            Table,
            TemplateMatrix
        }

        private enum NodeOutlineSource
        {
            Local,
            Template,
            GroupPreview,
            TriggerPreview
        }

        private enum NodeOutlineBranch
        {
            None,
            Predicate,
            Then,
            Else
        }

        private sealed class NodeOutlineItem
        {
            public TriggerNodeData Node;
            public TriggerNodeKind Kind;
            public TriggerNodeKind WorkspaceKind;
            public string Path;
            public string ParentPath;
            public string Breadcrumb;
            public int Depth;
            public bool IsReadOnly;
            public bool IsRoot;
            public bool HasChildren;
            public NodeOutlineSource Source;
            public List<TriggerNodeData> Owner;
            public int OwnerIndex = -1;
            public NodeOutlineBranch Branch;
            public bool IsBranchRoot;
            public TriggerNodeData ConditionalOwner;
        }

        private const float SplitThreshold = 680f;
        private const float DefaultTriggerListWidth = 300f;
        private const string TriggerListWidthPreference = "AbilityKit.TriggerAuthoring.TriggerListWidth";
        private const string TriggerGroupModePreferencePrefix = "AbilityKit.TriggerAuthoring.TriggerGroupMode.";
        private const string WorkspaceModePreference = "AbilityKit.TriggerAuthoring.WorkspaceMode";
        private const float DefaultNodeOutlineWidth = 280f;
        private const string NodeOutlineWidthPreference = "AbilityKit.TriggerAuthoring.NodeOutlineWidth";

        internal event Action RepaintRequested;

        private readonly EditorCommandRegistry _commands = new EditorCommandRegistry();
        private readonly List<IDisposable> _commandRegistrations = new List<IDisposable>();
        private TriggerAuthoringModuleAsset _asset;
        private TriggerTypeDescriptorCatalog _types;
        private TriggerEventDescriptorCatalog _events;
        private TriggerGlobalBlackboardDescriptorCatalog _globalBlackboard;
        private TriggerAuthoringValueSourceCatalog _valueSources;
        private TriggerTemplateDescriptorCatalog _templates;
        private List<TriggerAuthoringDiagnostic> _diagnostics = new List<TriggerAuthoringDiagnostic>();
        private EditorDiagnosticCollection _platformDiagnostics = new EditorDiagnosticCollection();
        private Vector2 _triggerScroll;
        private readonly EditorSearchState _triggerSearch = new EditorSearchState();
        private TriggerAuthoringTriggerGroupMode _triggerGroupMode = TriggerAuthoringTriggerGroupMode.GroupPath;
        private string _selectedTriggerGroupKey;
        private TriggerAuthoringTriggerQuickFilter _triggerQuickFilter = TriggerAuthoringTriggerQuickFilter.All;
        private readonly HashSet<string> _expandedTriggerGroups = new HashSet<string>(StringComparer.Ordinal);
        private bool _triggerGroupsInitialized;
        private bool _scrollToSelectedTrigger;
        private Vector2 _detailScroll;
        private Vector2 _nodeOutlineScroll;
        private Vector2 _nodeDetailScroll;
        private Vector2 _diagnosticScroll;
        private int _selectedTriggerIndex = -1;
        private string _focusedDiagnosticPath;
        private TriggerEditorTab _selectedEditorTab = TriggerEditorTab.Overview;
        private TriggerNodeKind _selectedRuleNodeKind = TriggerNodeKind.Condition;
        private string _selectedRuleNodePath;
        private TriggerNodeKind _ruleFocusKind = TriggerNodeKind.Condition;
        private string _ruleFocusPath;
        private bool _ruleTreeBranchesInitialized;
        private string _nodeSearch = string.Empty;
        private readonly HashSet<string> _expandedNodePaths = new HashSet<string>(StringComparer.Ordinal);
        private TriggerAuthoringTemplateData _activeTemplatePreview;
        private bool _showModuleBlackboard;
        private bool _showGroups;
        private bool _showModuleSettings;
        private bool _showConditionGroups = true;
        private bool _showActionGroups = true;
        private bool _showEditorOrganization = true;
        private bool _showAdvanced;
        private bool _showTriggerBlackboard;
        private bool _showCallableParameters;
        private bool _showDiagnostics = true;
        private readonly HashSet<string> _expandedGroupEditors = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _expandedGroupPreviews = new HashSet<string>(StringComparer.Ordinal);
        private readonly List<ITriggerAuthoringNodeContextMenuContributor> _nodeContextMenuContributors =
            new List<ITriggerAuthoringNodeContextMenuContributor>
            {
                new TriggerAuthoringDefaultNodeContextMenuContributor()
            };
        private double _nextSyncInspectionAt;
        private TriggerAuthoringSyncInspection _syncInspection;
        private TriggerAuthoringSyncState? _dismissedSyncBannerState;
        private readonly AdvancedDropdownState _nodeBrowserState = new AdvancedDropdownState();
        private readonly TriggerAuthoringTriggerTablePanel _triggerTablePanel;
        private readonly TriggerAuthoringTemplateMatrixPanel _templateMatrixPanel;
        private GUIStyle _triggerRowStyle;
        private GUIStyle _semanticSectionTitleStyle;
        private GUIStyle _semanticSectionSubtitleStyle;
        private GUIStyle _nodeTechnicalStyle;
        private GUIStyle _ruleSummaryStyle;
        private float _triggerListWidth;
        private bool _draggingTriggerListSplitter;
        private float _nodeOutlineWidth;
        private bool _draggingNodeOutlineSplitter;
        private TriggerWorkspaceMode _workspaceMode;

        public TriggerAuthoringModuleDrawer(TriggerAuthoringModuleAsset asset)
        {
            _triggerListWidth = EditorPrefs.GetFloat(TriggerListWidthPreference, DefaultTriggerListWidth);
            _nodeOutlineWidth = EditorPrefs.GetFloat(NodeOutlineWidthPreference, DefaultNodeOutlineWidth);
            _workspaceMode = (TriggerWorkspaceMode)Mathf.Clamp(
                EditorPrefs.GetInt(WorkspaceModePreference, (int)TriggerWorkspaceMode.RuleEditor),
                (int)TriggerWorkspaceMode.RuleEditor,
                (int)TriggerWorkspaceMode.TemplateMatrix);
            _triggerTablePanel = new TriggerAuthoringTriggerTablePanel();
            _triggerTablePanel.SelectionChanged += OnTableSelectionChanged;
            _triggerTablePanel.OpenRequested += OpenTriggerFromTable;
            _triggerTablePanel.ContextMenuRequested += ShowTriggerContextMenuFromTable;
            _templateMatrixPanel = new TriggerAuthoringTemplateMatrixPanel();
            _templateMatrixPanel.SelectionChanged += OnTableSelectionChanged;
            _templateMatrixPanel.OpenRequested += OpenTriggerFromTable;
            _templateMatrixPanel.PasteRequested += PreviewTemplateMatrixPaste;
            _templateMatrixPanel.NotificationRequested += ShowNotification;
            RegisterCommands();
            SetAsset(asset);
        }

        public TriggerAuthoringModuleAsset Asset => _asset;
        internal int DiagnosticErrorCount => _platformDiagnostics != null ? _platformDiagnostics.ErrorCount : 0;
        internal int DiagnosticWarningCount => _platformDiagnostics != null ? _platformDiagnostics.WarningCount : 0;

        /// <summary>切换目标资产（同一资产为空操作）：重建目录、恢复选中并刷新诊断。</summary>
        public void SetAsset(TriggerAuthoringModuleAsset asset)
        {
            if (_asset == asset) return;
            _asset = asset;
            _triggerGroupMode = LoadTriggerGroupMode(asset);
            _selectedTriggerGroupKey = null;
            RebuildCatalogs();
            EnsureSelection();
            RefreshDiagnostics();
            _dismissedSyncBannerState = null;
            _expandedTriggerGroups.Clear();
            _triggerGroupsInitialized = false;
            _templateMatrixPanel.Reset();
            ResetNodeNavigation();
        }

        public void Dispose()
        {
            for (var i = 0; i < _commandRegistrations.Count; i++)
                _commandRegistrations[i].Dispose();
            _commandRegistrations.Clear();
            RepaintRequested = null;
        }

        public void Draw()
        {
            Draw(EditorGUIUtility.currentViewWidth, false, true);
        }

        internal void Draw(float availableWidth, bool embedded, bool showDiagnostics)
        {
            if (_asset == null) return;

            PrepareUndoForInput();
            EditorGUI.BeginChangeCheck();
            DrawToolbar();
            DrawExternalChangeBanner();
            DrawModuleHeader(embedded);
            GUILayout.Space(4f);
            DrawWorkspaceModeToolbar();

            if (_workspaceMode == TriggerWorkspaceMode.Table)
            {
                DrawTriggerTableWorkspace();
            }
            else if (_workspaceMode == TriggerWorkspaceMode.TemplateMatrix)
            {
                DrawTemplateMatrixWorkspace();
            }
            else if (availableWidth >= SplitThreshold)
            {
                _triggerListWidth = TriggerAuthoringWorkspaceLayout.ClampTriggerListWidth(
                    _triggerListWidth,
                    availableWidth);
                var detailWidth = Mathf.Max(
                    360f,
                    availableWidth - _triggerListWidth - TriggerAuthoringWorkspaceLayout.SplitterWidth - 30f);
                EditorGUILayout.BeginHorizontal();
                DrawTriggerList(_triggerListWidth);
                DrawTriggerListSplitter(availableWidth);
                DrawSelectedTrigger(detailWidth);
                EditorGUILayout.EndHorizontal();
            }
            else
            {
                DrawTriggerList();
                DrawSelectedTrigger();
            }

            if (showDiagnostics) DrawDiagnostics();
            if (!EditorGUI.EndChangeCheck()) return;

            EditorUtility.SetDirty(_asset);
            RebuildCatalogs();
            RefreshDiagnostics();
            _templateMatrixPanel.Invalidate();
            _nextSyncInspectionAt = 0d;
        }

        private void DrawToolbar()
        {
            RefreshSyncInspectionIfNeeded();
            SirenixEditorGUI.BeginHorizontalToolbar();
            GUILayout.Label(TriggerAuthoringEditorIntegration.T("source"), GUILayout.Width(44f));
            var state = _syncInspection != null ? GetSyncStateLabel(_syncInspection.State) : "未知";
            var oldColor = GUI.color;
            GUI.color = GetSyncColor(_syncInspection != null ? _syncInspection.State : TriggerAuthoringSyncState.Untracked);
            GUILayout.Label(state, EditorStyles.boldLabel, GUILayout.Width(92f));
            GUI.color = oldColor;
            GUILayout.FlexibleSpace();

            DrawCommandButton(TriggerAuthoringCommandIds.Import);
            DrawCommandButton(TriggerAuthoringCommandIds.ExportSource);
            DrawCommandButton(TriggerAuthoringCommandIds.ExportRuntime);
            using (new EditorGUI.DisabledScope(_asset == null))
            {
                if (SirenixEditorGUI.ToolbarButton(new GUIContent("计划预览", "查看当前配置编译后的 Runtime Plan JSON")))
                    TriggerAuthoringRuntimePlanPreviewWindow.Open(_asset);
            }
            DrawCommandButton(TriggerAuthoringCommandIds.Validate);
            SirenixEditorGUI.EndHorizontalToolbar();
        }

        private void RegisterCommands()
        {
            var commands = TriggerAuthoringCommandFactory.CreateModule(
                ImportSource,
                ExportSource,
                ExportRuntime,
                RefreshDiagnostics,
                () => _asset != null);
            for (var i = 0; i < commands.Count; i++)
                _commandRegistrations.Add(_commands.Register(commands[i]));
        }

        private void DrawCommandButton(string commandId)
        {
            if (!_commands.TryGet(commandId, out var command)) return;
            var context = new EditorCommandContext(this, _asset);
            var previousEnabled = GUI.enabled;
            GUI.enabled = command.CanExecute(context);
            var localization = TriggerAuthoringEditorIntegration.Localization;
            var content = new GUIContent(
                localization.Get(command.LabelKey),
                localization.Get(command.TooltipKey));
            var pressed = SirenixEditorGUI.ToolbarButton(content);
            GUI.enabled = previousEnabled;
            if (pressed) command.TryExecute(context);
        }

        private void DrawExternalChangeBanner()
        {
            if (_syncInspection == null) return;
            var state = _syncInspection.State;
            if (_dismissedSyncBannerState == state) return;

            string message = null;
            if (state == TriggerAuthoringSyncState.JsonChanged)
                message = "Source 文件已在当前检查器之外被修改（例如外部编辑器或 AI）。请导入以应用改动，或忽略提示并继续编辑。";
            else if (state == TriggerAuthoringSyncState.SourceMissing)
                message = "绑定的 Source 文件不存在，可能已被移动、重命名或删除。";
            if (message == null) return;

            EditorGUILayout.HelpBox(message, MessageType.Warning);
            EditorGUILayout.BeginHorizontal();
            if (state == TriggerAuthoringSyncState.JsonChanged &&
                GUILayout.Button(TriggerAuthoringEditorIntegration.T("import"), EditorStyles.miniButtonLeft, GUILayout.Width(64f)))
                ImportSource();
            if (GUILayout.Button(TriggerAuthoringEditorIntegration.T("dismiss"), EditorStyles.miniButtonRight, GUILayout.Width(64f)))
            {
                _dismissedSyncBannerState = state;
                RequestRepaint();
            }
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(4f);
        }

        private void DrawModuleHeader(bool compact)
        {
            _asset.Metadata = _asset.Metadata ?? new TriggerAuthoringSourceMetadata();
            _asset.Module = _asset.Module ?? new TriggerAuthoringModuleData();
            var module = _asset.Module;

            if (compact)
            {
                SirenixEditorGUI.BeginHorizontalToolbar();
                GUILayout.Label(
                    string.IsNullOrWhiteSpace(module.DisplayName) ? module.ModuleId : module.DisplayName,
                    EditorStyles.boldLabel);
                GUILayout.Space(8f);
                GUILayout.Label(TriggerAuthoringEditorLabels.ModuleKind(module.Kind), EditorStyles.miniLabel);
                GUILayout.FlexibleSpace();
                GUILayout.Label(Count(module.Triggers) + " 个触发器", EditorStyles.miniLabel);
                _showModuleSettings = GUILayout.Toggle(
                    _showModuleSettings,
                    new GUIContent("设置", "显示内容包标识、所属项目、局部变量和可复用分组。"),
                    EditorStyles.toolbarButton,
                    GUILayout.Width(64f));
                SirenixEditorGUI.EndHorizontalToolbar();
                if (!_showModuleSettings) return;
            }

            SirenixEditorGUI.BeginBox(compact ? "内容包设置" : "内容包");
            EditorGUILayout.BeginHorizontal();
            var project = (TriggerAuthoringProjectAsset)EditorGUILayout.ObjectField(
                "所属项目", _asset.Project, typeof(TriggerAuthoringProjectAsset), false);
            if (_asset.Project == null &&
                GUILayout.Button(new GUIContent("创建", "创建包含所需目录的项目，并将当前模块分配到该项目"), EditorStyles.miniButton, GUILayout.Width(52f)))
            {
                CreateAndAssignProject();
                project = _asset.Project;
            }
            EditorGUILayout.EndHorizontal();
            if (project != _asset.Project)
            {
                var previous = _asset.Project;
                Edit("分配触发器项目", () => AssignProject(previous, project));
                RebuildCatalogs();
            }

            module.ModuleId = EditorGUILayout.TextField(TriggerAuthoringEditorIntegration.T("module-id"), module.ModuleId);
            module.DisplayName = EditorGUILayout.TextField(TriggerAuthoringEditorIntegration.T("display-name"), module.DisplayName);
            module.Kind = DrawModuleKindPopup(TriggerAuthoringEditorIntegration.T("kind"), module.Kind);
            DrawPackageMetadata();
            _asset.Metadata.Author = EditorGUILayout.TextField(TriggerAuthoringEditorIntegration.T("author"), _asset.Metadata.Author);
            _asset.Metadata.Description = EditorGUILayout.TextField(TriggerAuthoringEditorIntegration.T("description"), _asset.Metadata.Description);

            _showModuleBlackboard = EditorGUILayout.Foldout(
                _showModuleBlackboard,
                $"模块局部变量（{Count(module.Blackboard)}）",
                true);
            if (_showModuleBlackboard)
                DrawBlackboard(module.Blackboard, "模块局部变量", TriggerAuthoringLocalBlackboardScope.Module, null);

            _showGroups = EditorGUILayout.Foldout(
                _showGroups,
                $"可复用分组（{Count(module.ConditionGroups) + Count(module.ActionGroups)}）",
                true);
            if (_showGroups) DrawGroups(module);
            SirenixEditorGUI.EndBox();
        }

        private void DrawPackageMetadata()
        {
            var metadata = _asset.PackageMetadata;
            var domainId = EditorGUILayout.TextField(
                new GUIContent("业务域 ID", "用于项目树归类，不改变运行时逻辑"),
                metadata.DomainId);
            var contentKey = EditorGUILayout.TextField(
                new GUIContent("内容标识", "同一业务域内保持唯一，例如 hero.zhaoyun"),
                metadata.ContentKey);
            var owner = EditorGUILayout.TextField("负责人", metadata.Owner);
            var tags = EditorGUILayout.TextField(
                new GUIContent("内容包标签", "使用英文逗号分隔"),
                string.Join(", ", metadata.Tags));
            if (string.Equals(domainId, metadata.DomainId, StringComparison.Ordinal) &&
                string.Equals(contentKey, metadata.ContentKey, StringComparison.Ordinal) &&
                string.Equals(owner, metadata.Owner, StringComparison.Ordinal) &&
                string.Equals(tags, string.Join(", ", metadata.Tags), StringComparison.Ordinal)) return;

            Edit("编辑内容包信息", () =>
            {
                metadata.SetIdentity(
                    TriggerAuthoringPackageService.NormalizeIdentifier(domainId),
                    TriggerAuthoringPackageService.NormalizeIdentifier(contentKey));
                metadata.SetOwner(owner);
                metadata.SetTags((tags ?? string.Empty).Split(
                    new[] { ',' },
                    StringSplitOptions.RemoveEmptyEntries));
            });
        }

        private void DrawWorkspaceModeToolbar()
        {
            var nextMode = (TriggerWorkspaceMode)GUILayout.Toolbar(
                (int)_workspaceMode,
                new[] { "规则编辑", "表格批量", "参数矩阵" },
                EditorStyles.toolbarButton,
                GUILayout.Height(24f));
            if (nextMode == _workspaceMode) return;
            _workspaceMode = nextMode;
            EditorPrefs.SetInt(WorkspaceModePreference, (int)_workspaceMode);
            GUI.FocusControl(null);
            RequestRepaint();
        }

        private void DrawTriggerTableWorkspace()
        {
            var triggers = _asset.Module.Triggers ?? (_asset.Module.Triggers = new List<TriggerDefinitionData>());
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("触发器表格（" + triggers.Count + "）", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(new GUIContent("+", "添加触发器"), EditorStyles.miniButton, GUILayout.Width(26f)))
                AddTrigger();
            EditorGUILayout.EndHorizontal();

            EditorImGuiControls.DrawSearch(_triggerSearch, new GUIContent("搜索"));
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("筛选", EditorStyles.miniLabel, GUILayout.Width(42f));
            var nextFilter = (TriggerAuthoringTriggerQuickFilter)EditorGUILayout.Popup(
                (int)_triggerQuickFilter,
                TriggerQuickFilterNames,
                EditorStyles.toolbarPopup,
                GUILayout.Width(126f));
            if (nextFilter != _triggerQuickFilter) _triggerQuickFilter = nextFilter;
            GUILayout.Space(8f);

            var groups = TriggerAuthoringTriggerIndex.Build(
                triggers,
                _diagnostics,
                _events,
                TriggerAuthoringTriggerGroupMode.Flat,
                _triggerSearch.Text,
                _triggerQuickFilter,
                _templates);
            var entries = groups.Count > 0
                ? (IReadOnlyList<TriggerAuthoringTriggerIndex.Entry>)groups[0].Entries
                : Array.Empty<TriggerAuthoringTriggerIndex.Entry>();
            _triggerTablePanel.SetEntries(entries);
            if (_triggerTablePanel.SelectedCount == 0 &&
                _selectedTriggerIndex >= 0 &&
                _selectedTriggerIndex < triggers.Count)
                _triggerTablePanel.EnsureSelection(triggers[_selectedTriggerIndex]);

            GUILayout.Label(
                "已选 " + _triggerTablePanel.SelectedCount + " / 显示 " + entries.Count,
                EditorStyles.miniLabel);
            GUILayout.FlexibleSpace();
            using (new EditorGUI.DisabledScope(entries.Count == 0))
            {
                if (GUILayout.Button(new GUIContent("全选", "选择当前筛选结果"), EditorStyles.toolbarButton, GUILayout.Width(44f)))
                    _triggerTablePanel.SelectAll();
            }
            using (new EditorGUI.DisabledScope(_triggerTablePanel.SelectedCount == 0))
            {
                if (GUILayout.Button(new GUIContent("清除", "清除表格多选"), EditorStyles.toolbarButton, GUILayout.Width(44f)))
                    _triggerTablePanel.ClearSelection();
                if (GUILayout.Button(new GUIContent("批量", "批量编辑明确选中的触发器"), EditorStyles.toolbarDropDown, GUILayout.Width(54f)))
                    ShowTriggerBatchMenu(triggers, _triggerTablePanel.GetSelectedIndices(), "选中项");
            }
            EditorGUILayout.EndHorizontal();

            var tableRect = GUILayoutUtility.GetRect(
                0f,
                10000f,
                360f,
                10000f,
                GUILayout.ExpandWidth(true),
                GUILayout.ExpandHeight(true));
            _triggerTablePanel.Draw(tableRect);
            EditorGUILayout.EndVertical();
        }

        private void DrawTemplateMatrixWorkspace()
        {
            var triggers = _asset.Module.Triggers ?? (_asset.Module.Triggers = new List<TriggerDefinitionData>());
            var groups = TriggerAuthoringTriggerIndex.Build(
                triggers,
                _diagnostics,
                _events,
                TriggerAuthoringTriggerGroupMode.Flat,
                string.Empty,
                TriggerAuthoringTriggerQuickFilter.All,
                _templates);
            var entries = groups.Count > 0
                ? (IReadOnlyList<TriggerAuthoringTriggerIndex.Entry>)groups[0].Entries
                : Array.Empty<TriggerAuthoringTriggerIndex.Entry>();
            var selected = _selectedTriggerIndex >= 0 && _selectedTriggerIndex < triggers.Count
                ? triggers[_selectedTriggerIndex]
                : null;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("函数库参数矩阵", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            GUILayout.Label("一行一个调用实例 · 一列一个输入参数", EditorStyles.centeredGreyMiniLabel);
            EditorGUILayout.EndHorizontal();
            _templateMatrixPanel.Draw(triggers, entries, _templates, selected);
            EditorGUILayout.EndVertical();
        }

        private void PreviewTemplateMatrixPaste(TriggerAuthoringTemplateData template, string tsv)
        {
            var triggers = _asset?.Module?.Triggers;
            var plan = TriggerAuthoringTemplateBindingPastePlan.Create(tsv, triggers, template);
            var preview = plan.BuildPreview();
            if (!plan.CanApply)
            {
                EditorUtility.DisplayDialog("参数矩阵粘贴预览", preview, "确定");
                return;
            }

            var confirm = plan.Errors.Count == 0 ? "应用修改" : "应用有效项";
            if (!EditorUtility.DisplayDialog("参数矩阵粘贴预览", preview, confirm, "取消")) return;
            Undo.RecordObject(_asset, "粘贴函数库参数矩阵");
            plan.Apply();
            EditorUtility.SetDirty(_asset);
            RebuildCatalogs();
            RefreshDiagnostics();
            _templateMatrixPanel.Invalidate();
            _nextSyncInspectionAt = 0d;
            ShowNotification("已更新 " + plan.ChangedTriggerCount + " 个函数调用实例");
        }

        private void OnTableSelectionChanged(IReadOnlyList<int> indices)
        {
            if (indices == null || indices.Count == 0) return;
            if (TriggerAuthoringTriggerBatchOperations.ContainsVisibleTriggerIndex(indices, _selectedTriggerIndex))
                return;
            SelectTrigger(indices[0]);
            RequestRepaint();
        }

        private void OpenTriggerFromTable(int index)
        {
            SelectTrigger(index);
            _workspaceMode = TriggerWorkspaceMode.RuleEditor;
            EditorPrefs.SetInt(WorkspaceModePreference, (int)_workspaceMode);
            GUI.FocusControl(null);
            RequestRepaint();
        }

        private void ShowTriggerContextMenuFromTable(int index)
        {
            var triggers = _asset?.Module?.Triggers;
            if (triggers == null || index < 0 || index >= triggers.Count) return;
            SelectTrigger(index);
            ShowTriggerContextMenu(triggers, index);
        }

        private void DrawTriggerList(float width = 0f)
        {
            var options = width > 0f ? new[] { GUILayout.Width(width) } : Array.Empty<GUILayoutOption>();
            EditorGUILayout.BeginVertical(EditorStyles.helpBox, options);
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label($"触发器（{Count(_asset.Module.Triggers)}）", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(new GUIContent("+", "添加触发器"), EditorStyles.miniButton, GUILayout.Width(26f)))
                AddTrigger();
            EditorGUILayout.EndHorizontal();

            EditorImGuiControls.DrawSearch(_triggerSearch, new GUIContent("搜索"));
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("视图", EditorStyles.miniLabel, GUILayout.Width(42f));
            var nextGroupMode = (TriggerAuthoringTriggerGroupMode)EditorGUILayout.Popup(
                (int)_triggerGroupMode,
                TriggerGroupModeNames,
                EditorStyles.toolbarPopup);
            if (nextGroupMode != _triggerGroupMode)
                SetTriggerGroupMode(nextGroupMode);
            if (GUILayout.Button(new GUIContent("全部", "展开全部触发器分组"), EditorStyles.toolbarButton, GUILayout.Width(42f)))
                ExpandVisibleTriggerGroups();
            if (GUILayout.Button(new GUIContent("收起", "收起全部触发器分组"), EditorStyles.toolbarButton, GUILayout.Width(42f)))
            {
                _expandedTriggerGroups.Clear();
                _triggerGroupsInitialized = true;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(TriggerAuthoringEditorIntegration.T("filter"), EditorStyles.miniLabel, GUILayout.Width(42f));
            var nextQuickFilter = (TriggerAuthoringTriggerQuickFilter)EditorGUILayout.Popup(
                (int)_triggerQuickFilter,
                TriggerQuickFilterNames,
                EditorStyles.toolbarPopup);
            if (nextQuickFilter != _triggerQuickFilter)
            {
                _triggerQuickFilter = nextQuickFilter;
                _expandedTriggerGroups.Clear();
                _triggerGroupsInitialized = false;
            }
            if (_triggerQuickFilter != TriggerAuthoringTriggerQuickFilter.All &&
                GUILayout.Button(new GUIContent(TriggerAuthoringEditorIntegration.T("clear"), "清除快捷筛选"), EditorStyles.toolbarButton, GUILayout.Width(44f)))
            {
                _triggerQuickFilter = TriggerAuthoringTriggerQuickFilter.All;
                _expandedTriggerGroups.Clear();
                _triggerGroupsInitialized = false;
                GUI.FocusControl(null);
            }
            EditorGUILayout.EndHorizontal();

            var triggers = _asset.Module.Triggers ?? (_asset.Module.Triggers = new List<TriggerDefinitionData>());
            var groups = TriggerAuthoringTriggerIndex.Build(
                triggers,
                _diagnostics,
                _events,
                _triggerGroupMode,
                _triggerSearch.Text,
                _triggerQuickFilter,
                _templates);
            var visibleIndices = TriggerAuthoringTriggerBatchOperations.CollectVisibleTriggerIndices(groups);
            DrawTriggerBatchToolbar(triggers, visibleIndices);
            DrawSelectedTriggerVisibilityHint(triggers, visibleIndices);

            _triggerScroll = EditorGUILayout.BeginScrollView(_triggerScroll, GUILayout.MinHeight(90f), GUILayout.MaxHeight(360f));
            EnsureInitialTriggerGroupExpansion(groups);
            if (groups.Count == 0)
                EditorGUILayout.HelpBox(TriggerAuthoringEditorIntegration.T("no-triggers-match"), MessageType.Info);
            for (var i = 0; i < groups.Count; i++)
                DrawTriggerGroup(groups[i], triggers);
            EditorGUILayout.EndScrollView();

            using (new EditorGUI.DisabledScope(_selectedTriggerIndex < 0 || _selectedTriggerIndex >= triggers.Count))
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button(new GUIContent("↑", "上移触发器"), EditorStyles.miniButtonLeft, GUILayout.Width(24f)))
                    MoveSelectedTrigger(triggers, -1);
                if (GUILayout.Button(new GUIContent("↓", "下移触发器"), EditorStyles.miniButtonMid, GUILayout.Width(24f)))
                    MoveSelectedTrigger(triggers, 1);
                if (GUILayout.Button(TriggerAuthoringEditorIntegration.T("duplicate"), EditorStyles.miniButtonMid)) DuplicateSelectedTrigger();
                if (GUILayout.Button(TriggerAuthoringEditorIntegration.T("delete"), EditorStyles.miniButtonRight)) DeleteSelectedTrigger();
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndVertical();
        }

        private void DrawTriggerListSplitter(float availableWidth)
        {
            var rect = GUILayoutUtility.GetRect(
                TriggerAuthoringWorkspaceLayout.SplitterWidth,
                TriggerAuthoringWorkspaceLayout.SplitterWidth,
                GUILayout.Width(TriggerAuthoringWorkspaceLayout.SplitterWidth),
                GUILayout.ExpandHeight(true));
            EditorGUIUtility.AddCursorRect(rect, MouseCursor.ResizeHorizontal);
            if (Event.current.type == EventType.Repaint)
                EditorGUI.DrawRect(rect, new Color(0f, 0f, 0f, 0.22f));

            if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && rect.Contains(Event.current.mousePosition))
            {
                _draggingTriggerListSplitter = true;
                Event.current.Use();
            }
            else if (_draggingTriggerListSplitter && Event.current.type == EventType.MouseDrag)
            {
                _triggerListWidth = TriggerAuthoringWorkspaceLayout.ClampTriggerListWidth(
                    _triggerListWidth + Event.current.delta.x,
                    availableWidth);
                EditorPrefs.SetFloat(TriggerListWidthPreference, _triggerListWidth);
                RequestRepaint();
                Event.current.Use();
            }
            else if (_draggingTriggerListSplitter && Event.current.rawType == EventType.MouseUp)
            {
                _draggingTriggerListSplitter = false;
                Event.current.Use();
            }
        }

        private void DrawTriggerBatchToolbar(
            List<TriggerDefinitionData> triggers,
            IReadOnlyList<int> visibleIndices)
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label(TriggerAuthoringEditorIntegration.F("visible", Count(visibleIndices)), EditorStyles.miniLabel);
            GUILayout.FlexibleSpace();
            using (new EditorGUI.DisabledScope(Count(visibleIndices) == 0))
            {
                if (GUILayout.Button(new GUIContent("批量", "批量编辑当前显示的触发器"), EditorStyles.toolbarDropDown))
                    ShowTriggerBatchMenu(triggers, visibleIndices, "可见项");
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawSelectedTriggerVisibilityHint(
            IReadOnlyList<TriggerDefinitionData> triggers,
            IReadOnlyList<int> visibleIndices)
        {
            if (_selectedTriggerIndex < 0 ||
                triggers == null ||
                _selectedTriggerIndex >= triggers.Count ||
                TriggerAuthoringTriggerBatchOperations.ContainsVisibleTriggerIndex(visibleIndices, _selectedTriggerIndex))
                return;

            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            GUILayout.Label(TriggerAuthoringEditorIntegration.T("hidden-by-filter"), EditorStyles.miniLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(new GUIContent("显示", "清除触发器搜索和快捷筛选"), EditorStyles.miniButton, GUILayout.Width(48f)))
                ShowSelectedTriggerInList();
            EditorGUILayout.EndHorizontal();
        }

        private void ShowSelectedTriggerInList()
        {
            _triggerSearch.Clear();
            _triggerQuickFilter = TriggerAuthoringTriggerQuickFilter.All;
            _expandedTriggerGroups.Clear();
            _triggerGroupsInitialized = false;
            GUI.FocusControl(null);
            RequestRepaint();
        }

        private void ShowTriggerBatchMenu(
            List<TriggerDefinitionData> triggers,
            IReadOnlyList<int> visibleIndices,
            string scopeLabel)
        {
            var indices = visibleIndices != null ? new List<int>(visibleIndices) : new List<int>();
            TriggerAuthoringTriggerBatchMenu.Show(
                _asset,
                triggers,
                indices,
                scopeLabel,
                index =>
                {
                    SelectTrigger(index);
                    RequestRepaint();
                },
                OnTriggerBatchChanged,
                ShowNotification);
        }

        private void OnTriggerBatchChanged()
        {
            RefreshDiagnostics();
            _nextSyncInspectionAt = 0d;
            _expandedTriggerGroups.Clear();
            _triggerGroupsInitialized = false;
            RequestRepaint();
        }

        private void DrawTriggerGroup(
            TriggerAuthoringTriggerIndex.Group group,
            List<TriggerDefinitionData> triggers)
        {
            if (group == null) return;
            if (_triggerGroupMode == TriggerAuthoringTriggerGroupMode.Flat)
            {
                for (var i = 0; i < group.Entries.Count; i++)
                    DrawTriggerRow(group.Entries[i], triggers, group.Key);
                return;
            }

            var expanded = _triggerSearch.IsEmpty
                ? _expandedTriggerGroups.Contains(group.Key)
                : true;
            var nextExpanded = EditorGUILayout.Foldout(
                expanded,
                group.Label + "（" + group.Entries.Count + "）",
                true);
            if (nextExpanded) _expandedTriggerGroups.Add(group.Key);
            else _expandedTriggerGroups.Remove(group.Key);
            if (!nextExpanded) return;

            EditorGUI.indentLevel++;
            for (var i = 0; i < group.Entries.Count; i++)
                DrawTriggerRow(group.Entries[i], triggers, group.Key);
            EditorGUI.indentLevel--;
        }

        private void DrawTriggerRow(
            TriggerAuthoringTriggerIndex.Entry entry,
            List<TriggerDefinitionData> triggers,
            string groupKey)
        {
            var label = BuildTriggerRowLabel(entry.EffectiveTrigger, entry.Index, entry.Diagnostics);
            var oldBackground = GUI.backgroundColor;
            if (entry.Index == _selectedTriggerIndex) GUI.backgroundColor = new Color(0.42f, 0.66f, 0.92f);
            if (GUILayout.Button(label, TriggerRowStyle, GUILayout.Height(38f)))
                SelectTrigger(entry.Index, groupKey);
            else if (Event.current.type == EventType.ContextClick &&
                     GUILayoutUtility.GetLastRect().Contains(Event.current.mousePosition))
            {
                SelectTrigger(entry.Index, groupKey);
                ShowTriggerContextMenu(triggers, entry.Index);
                Event.current.Use();
            }
            if (_scrollToSelectedTrigger &&
                entry.Index == _selectedTriggerIndex &&
                Event.current.type == EventType.Repaint)
            {
                GUI.ScrollTo(GUILayoutUtility.GetLastRect());
                _scrollToSelectedTrigger = false;
            }
            GUI.backgroundColor = oldBackground;
        }

        private GUIStyle TriggerRowStyle
        {
            get
            {
                if (_triggerRowStyle != null) return _triggerRowStyle;
                _triggerRowStyle = new GUIStyle(EditorStyles.miniButton)
                {
                    alignment = TextAnchor.MiddleLeft,
                    padding = new RectOffset(7, 5, 3, 3),
                    clipping = TextClipping.Clip,
                    wordWrap = false,
                    fixedHeight = 0f,
                    stretchHeight = true
                };
                return _triggerRowStyle;
            }
        }

        private static GUIContent BuildTriggerRowLabel(
            TriggerDefinitionData trigger,
            int index,
            TriggerAuthoringTriggerIndex.DiagnosticSummary diagnostics)
        {
            if (trigger == null) return new GUIContent((index + 1) + ". <空>");
            var status = diagnostics.Errors > 0
                ? "E" + diagnostics.Errors + (diagnostics.Warnings > 0 ? "  W" + diagnostics.Warnings : string.Empty)
                : diagnostics.Warnings > 0 ? "W" + diagnostics.Warnings : "就绪";
            var eventName = trigger.EntryMode == TriggerEntryMode.Callable
                ? "仅供 TriggerId 调用"
                : string.IsNullOrWhiteSpace(trigger.Event) ? "未设置事件" : trigger.Event;
            var state = trigger.Enabled ? string.Empty : "[已停用] ";
            var text = state + trigger.Id + "  " + DisplayTriggerName(trigger) + "\n" + eventName + "  |  " + status;
            var tooltip = "触发器 " + trigger.Id + "\n" +
                          "事件：" + eventName + "\n" +
                          "分组：" + (string.IsNullOrWhiteSpace(trigger.GroupPath) ? "未分配" : trigger.GroupPath) + "\n" +
                          "状态：" + status;
            return new GUIContent(text, tooltip);
        }

        private void SelectTrigger(int index, string groupKey = null)
        {
            if (_selectedTriggerIndex != index) ResetNodeNavigation();
            _selectedTriggerIndex = index;
            _selectedTriggerGroupKey = groupKey;
            _focusedDiagnosticPath = null;
        }

        private void SetTriggerGroupMode(TriggerAuthoringTriggerGroupMode mode)
        {
            if (_triggerGroupMode == mode) return;
            _triggerGroupMode = mode;
            _selectedTriggerGroupKey = null;
            SaveTriggerGroupMode(_asset, mode);
            _expandedTriggerGroups.Clear();
            _triggerGroupsInitialized = false;
            RequestRepaint();
        }

        private void ExpandVisibleTriggerGroups()
        {
            var triggers = _asset != null && _asset.Module != null
                ? _asset.Module.Triggers
                : null;
            var groups = TriggerAuthoringTriggerIndex.Build(
                triggers,
                _diagnostics,
                _events,
                _triggerGroupMode,
                _triggerSearch.Text,
                _triggerQuickFilter,
                _templates);
            for (var i = 0; i < groups.Count; i++)
                _expandedTriggerGroups.Add(groups[i].Key);
            _triggerGroupsInitialized = true;
        }

        private void EnsureInitialTriggerGroupExpansion(IReadOnlyList<TriggerAuthoringTriggerIndex.Group> groups)
        {
            if (_triggerGroupsInitialized || groups == null) return;
            for (var i = 0; i < groups.Count; i++)
                if (groups[i] != null)
                    _expandedTriggerGroups.Add(groups[i].Key);
            _triggerGroupsInitialized = true;
        }

        private void ShowTriggerContextMenu(List<TriggerDefinitionData> triggers, int index)
        {
            var menu = new GenericMenu();
            var trigger = triggers[index];
            menu.AddItem(new GUIContent("上移"), false, () => MoveSelectedTrigger(triggers, -1));
            menu.AddItem(new GUIContent("下移"), false, () => MoveSelectedTrigger(triggers, 1));
            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("定位/复制触发器路径"), false, () =>
                EditorGUIUtility.systemCopyBuffer = "module.triggers[" + index + "]");
            menu.AddItem(new GUIContent("定位/复制 TriggerId"), false, () =>
                EditorGUIUtility.systemCopyBuffer = trigger != null ? trigger.Id.ToString() : string.Empty);
            menu.AddItem(new GUIContent("定位/定位模块资产"), false, () =>
            {
                Selection.activeObject = _asset;
                EditorGUIUtility.PingObject(_asset);
            });
            if (trigger != null)
            {
                menu.AddSeparator(string.Empty);
                menu.AddItem(new GUIContent("重构/修改 TriggerId..."), false, () => BeginRefactorTriggerId(trigger));
                menu.AddItem(new GUIContent("重构/查看 TriggerId 引用"), false, () =>
                    ShowReferences(
                        TriggerAuthoringTriggerIdRefactor.FindReferences(_asset, trigger.Id),
                        "TriggerId: " + trigger.Id));
                AddBusinessGroupMenu(menu, trigger);
            }
            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent(TriggerAuthoringEditorIntegration.T("duplicate")), false, DuplicateSelectedTrigger);
            menu.AddItem(new GUIContent(TriggerAuthoringEditorIntegration.T("delete")), false, DeleteSelectedTrigger);
            if (trigger != null)
            {
                menu.AddSeparator(string.Empty);
                menu.AddItem(
                    new GUIContent(trigger.Enabled ? "停用" : "启用"),
                    false,
                    () => Edit("切换触发器启用状态", () => trigger.Enabled = !trigger.Enabled));
            }
            menu.ShowAsContext();
        }

        private void AddBusinessGroupMenu(GenericMenu menu, TriggerDefinitionData trigger)
        {
            var current = TriggerAuthoringTriggerBatchOperations.NormalizeGroupPath(trigger?.GroupPath);
            menu.AddItem(
                new GUIContent("业务分组/未分组"),
                current.Length == 0,
                () => MoveTriggerToGroup(trigger, string.Empty));
            menu.AddItem(
                new GUIContent("业务分组/新建或输入分组..."),
                false,
                () => TriggerAuthoringTextPrompt.Open(
                    "移动触发器",
                    "业务分组路径（可使用 / 创建层级）",
                    current,
                    value => MoveTriggerToGroup(trigger, value)));

            var paths = CollectBusinessGroupPaths();
            if (paths.Count == 0) return;
            menu.AddSeparator("业务分组/");
            for (var i = 0; i < paths.Count; i++)
            {
                var path = paths[i];
                var captured = path;
                menu.AddItem(
                    new GUIContent("业务分组/已有分组/" + path),
                    string.Equals(current, path, StringComparison.Ordinal),
                    () => MoveTriggerToGroup(trigger, captured));
            }
        }

        private List<string> CollectBusinessGroupPaths()
        {
            var result = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var triggers = _asset?.Module?.Triggers;
            if (triggers == null) return result;
            for (var i = 0; i < triggers.Count; i++)
            {
                var path = TriggerAuthoringTriggerBatchOperations.NormalizeGroupPath(triggers[i]?.GroupPath);
                if (path.Length > 0 && seen.Add(path)) result.Add(path);
            }
            result.Sort(StringComparer.OrdinalIgnoreCase);
            return result;
        }

        private void MoveTriggerToGroup(TriggerDefinitionData trigger, string groupPath)
        {
            if (trigger == null) return;
            var normalized = TriggerAuthoringTriggerBatchOperations.NormalizeGroupPath(groupPath);
            Edit("移动触发器业务分组", () => trigger.GroupPath = normalized);
            SetTriggerGroupMode(TriggerAuthoringTriggerGroupMode.GroupPath);
            _selectedTriggerGroupKey = null;
            _expandedTriggerGroups.Clear();
            _triggerGroupsInitialized = false;
            _scrollToSelectedTrigger = true;
            ExpandVisibleTriggerGroups();
            RequestRepaint();
        }

        private void BeginRefactorTriggerId(TriggerDefinitionData trigger)
        {
            if (trigger == null) return;
            TriggerAuthoringTextPrompt.Open(
                "重构 TriggerId",
                "新的 TriggerId（将同步修改项目内的受管引用）",
                trigger.Id.ToString(),
                value => RefactorTriggerId(trigger, value));
        }

        private void RefactorTriggerId(TriggerDefinitionData trigger, string value)
        {
            if (!int.TryParse((value ?? string.Empty).Trim(), out var newId))
            {
                EditorUtility.DisplayDialog("无法修改 TriggerId", "请输入有效的整数 TriggerId。", "确定");
                return;
            }

            var plan = TriggerAuthoringTriggerIdRefactor.BuildPlan(_asset, trigger, newId);
            if (!plan.IsValid)
            {
                EditorUtility.DisplayDialog(
                    "无法修改 TriggerId",
                    string.IsNullOrWhiteSpace(plan.Error) ? "无法生成 TriggerId 重构计划。" : plan.Error,
                    "确定");
                return;
            }

            var preview = "TriggerId：" + plan.OldId + "  ->  " + plan.NewId +
                          "\n受管引用：" + plan.References.Count +
                          "\n影响模块：" + plan.AffectedModules.Count;
            var previewCount = Math.Min(8, plan.References.Count);
            for (var i = 0; i < previewCount; i++)
                preview += "\n- " + plan.References[i].BuildLabel();
            if (plan.References.Count > previewCount)
                preview += "\n- 其余 " + (plan.References.Count - previewCount) + " 处引用";
            preview += "\n\n已绑定的 Source JSON 将标记为待同步。";
            if (!EditorUtility.DisplayDialog("确认修改 TriggerId", preview, "修改", "取消")) return;

            var affected = plan.AffectedModules;
            var undoTargets = new UnityEngine.Object[affected.Count];
            for (var i = 0; i < affected.Count; i++) undoTargets[i] = affected[i];
            Undo.RecordObjects(undoTargets, "重构 TriggerId");
            var referenceCount = plan.Apply();
            for (var i = 0; i < affected.Count; i++)
                if (affected[i] != null)
                    EditorUtility.SetDirty(affected[i]);

            RefreshDiagnostics();
            _nextSyncInspectionAt = 0d;
            _expandedTriggerGroups.Clear();
            _triggerGroupsInitialized = false;
            ExpandVisibleTriggerGroups();
            ShowNotification("TriggerId 已修改，并更新 " + referenceCount + " 处引用");
            RequestRepaint();
        }

        private void MoveSelectedTrigger(List<TriggerDefinitionData> triggers, int delta)
        {
            var index = _selectedTriggerIndex;
            var target = index + delta;
            if (triggers == null || index < 0 || index >= triggers.Count ||
                target < 0 || target >= triggers.Count) return;
            Edit("调整触发器顺序", () =>
            {
                var temporary = triggers[index];
                triggers[index] = triggers[target];
                triggers[target] = temporary;
                _selectedTriggerIndex = target;
            });
        }

        private void DrawSelectedTrigger(float width = 0f)
        {
            var triggers = _asset.Module.Triggers;
            if (triggers == null || _selectedTriggerIndex < 0 || _selectedTriggerIndex >= triggers.Count)
            {
                EditorGUILayout.HelpBox(TriggerAuthoringEditorIntegration.T("select-or-add-trigger"), MessageType.Info);
                return;
            }

            var trigger = triggers[_selectedTriggerIndex];
            if (trigger == null)
            {
                if (GUILayout.Button(TriggerAuthoringEditorIntegration.T("create-trigger")))
                    Edit(TriggerAuthoringEditorIntegration.T("create-trigger"), () => triggers[_selectedTriggerIndex] = CreateTrigger());
                return;
            }

            var options = width > 0f
                ? new[] { GUILayout.Width(width) }
                : new[] { GUILayout.MinWidth(360f), GUILayout.ExpandWidth(true) };
            EditorGUILayout.BeginVertical(EditorStyles.helpBox, options);
            DrawTriggerEditorTabs(trigger, width);
            EditorGUILayout.EndVertical();
        }

        private void DrawTriggerEditorTabs(TriggerDefinitionData trigger, float width)
        {
            var nextTab = (TriggerEditorTab)GUILayout.Toolbar(
                (int)_selectedEditorTab,
                new[] { "规则总览", "规则树", "配置" },
                EditorStyles.toolbarButton,
                GUILayout.Height(25f));
            if (nextTab != _selectedEditorTab)
            {
                _selectedEditorTab = nextTab;
                _focusedDiagnosticPath = null;
                GUI.FocusControl(null);
            }

            switch (_selectedEditorTab)
            {
                case TriggerEditorTab.RuleTree:
                    DrawRuleTreeWorkspace(trigger, width);
                    break;
                case TriggerEditorTab.Settings:
                    _detailScroll = EditorGUILayout.BeginScrollView(_detailScroll, GUILayout.MinHeight(420f));
                    DrawTriggerSettings(trigger);
                    EditorGUILayout.EndScrollView();
                    break;
                default:
                    _detailScroll = EditorGUILayout.BeginScrollView(_detailScroll, GUILayout.MinHeight(420f));
                    DrawTriggerHeader(trigger, width);
                    EditorGUILayout.EndScrollView();
                    break;
            }
        }

        private void DrawTriggerHeader(TriggerDefinitionData trigger, float availableWidth)
        {
            var effectiveTrigger = ResolveEffectiveTrigger(trigger);
            var usesTemplate = trigger.Template != null && !ReferenceEquals(effectiveTrigger, trigger);
            DrawSemanticSectionHeader(
                effectiveTrigger.EntryMode == TriggerEntryMode.Callable ? "调用入口" : "触发事件",
                effectiveTrigger.EntryMode == TriggerEntryMode.Callable
                    ? "由其他触发效果通过 TriggerId 直接调用"
                    : "当指定事件到达时，开始判断条件",
                TriggerSemanticArea.Event);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            trigger.Enabled = EditorGUILayout.ToggleLeft("启用", trigger.Enabled, GUILayout.Width(72f));
            GUILayout.FlexibleSpace();
            GUILayout.Label("稳定标识", EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("TriggerId", EditorStyles.miniLabel, GUILayout.Width(54f));
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.IntField(trigger.Id, GUILayout.Width(88f));
            if (GUILayout.Button(new GUIContent("修改 ID...", "项目级重构 TriggerId，并同步更新所有受管引用"), EditorStyles.miniButton, GUILayout.Width(72f)))
                BeginRefactorTriggerId(trigger);
            if (GUILayout.Button(new GUIContent("引用", "查看项目内对此 TriggerId 的引用"), EditorStyles.miniButton, GUILayout.Width(42f)))
                ShowReferences(
                    TriggerAuthoringTriggerIdRefactor.FindReferences(_asset, trigger.Id),
                    "TriggerId: " + trigger.Id);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            trigger.Name = EditorGUILayout.TextField("显示名称", trigger.Name);

            using (new EditorGUI.DisabledScope(usesTemplate))
            {
                var entryMode = GUILayout.Toolbar(
                    effectiveTrigger.EntryMode == TriggerEntryMode.Callable ? 1 : 0,
                    new[] { "事件触发", "仅供调用" },
                    EditorStyles.miniButton);
                effectiveTrigger.EntryMode = entryMode == 1 ? TriggerEntryMode.Callable : TriggerEntryMode.Event;

                if (effectiveTrigger.EntryMode == TriggerEntryMode.Event)
                {
                    EditorGUILayout.BeginHorizontal();
                    effectiveTrigger.Event = EditorGUILayout.TextField("事件", effectiveTrigger.Event);
                    if (GUILayout.Button(new GUIContent("选择", "从事件目录中选择"), GUILayout.Width(48f)))
                        ShowEventMenu(effectiveTrigger);
                    using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(effectiveTrigger.Event) || _asset.Project == null))
                    {
                        if (GUILayout.Button(new GUIContent("引用", "查找项目中使用此事件的触发器"), EditorStyles.miniButton, GUILayout.Width(42f)))
                            ShowReferences(
                                TriggerAuthoringReferenceFinder.FindEventReferences(_asset.Project, effectiveTrigger.Event),
                                "事件: " + effectiveTrigger.Event);
                    }
                    EditorGUILayout.EndHorizontal();
                }
            }
            if (usesTemplate)
                GUILayout.Label("入口与执行逻辑由模板提供", EditorStyles.centeredGreyMiniLabel);

            if (effectiveTrigger.EntryMode == TriggerEntryMode.Event &&
                _events != null && !string.IsNullOrWhiteSpace(effectiveTrigger.Event) &&
                _events.TryResolve(effectiveTrigger.Event, out var eventDefinition))
            {
                EditorGUILayout.LabelField(
                    $"分类: {eventDefinition.Category}    载荷: {eventDefinition.PayloadType}    " +
                    $"参数: {Count(eventDefinition.PayloadFields)} 个",
                    EditorStyles.miniLabel);
            }

            EditorGUILayout.EndVertical();
            DrawRuleOverview(trigger, availableWidth);
        }

        private void DrawRuleOverview(TriggerDefinitionData selectedTrigger, float availableWidth)
        {
            var effectiveSelectedTrigger = ResolveEffectiveTrigger(selectedTrigger);
            GUILayout.Space(8f);
            var overview = TriggerAuthoringRuleOverviewBuilder.BuildForGroup(
                _asset.Module,
                _selectedTriggerIndex,
                _triggerGroupMode,
                _selectedTriggerGroupKey,
                _diagnostics,
                _events,
                _types,
                _templates);
            _selectedTriggerGroupKey = overview.GroupKey;
            var rules = overview.Rules;

            EditorGUILayout.BeginHorizontal();
            var groupLabel = string.IsNullOrWhiteSpace(overview.GroupLabel)
                ? "当前视图"
                : overview.GroupLabel;
            GUILayout.Label("规则总览 · " + groupLabel + "（" + rules.Count + "）", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            GUILayout.Label("视图", EditorStyles.miniLabel, GUILayout.Width(28f));
            var nextGroupMode = (TriggerAuthoringTriggerGroupMode)EditorGUILayout.Popup(
                (int)_triggerGroupMode,
                TriggerGroupModeNames,
                EditorStyles.toolbarPopup,
                GUILayout.Width(104f));
            if (_triggerGroupMode == TriggerAuthoringTriggerGroupMode.Event &&
                effectiveSelectedTrigger.EntryMode == TriggerEntryMode.Event)
            {
                using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(effectiveSelectedTrigger.Event)))
                {
                    if (GUILayout.Button(new GUIContent("+ 规则", "添加一条监听当前事件的新规则"), EditorStyles.miniButton, GUILayout.Width(62f)))
                        AddRuleForEvent(effectiveSelectedTrigger);
                }
            }
            EditorGUILayout.EndHorizontal();
            if (nextGroupMode != _triggerGroupMode)
            {
                SetTriggerGroupMode(nextGroupMode);
                GUI.FocusControl(null);
                GUIUtility.ExitGUI();
            }

            if (rules.Count == 0)
            {
                EditorGUILayout.HelpBox("当前分组没有可显示的规则。", MessageType.Info);
                return;
            }
            if (_triggerGroupMode == TriggerAuthoringTriggerGroupMode.Event &&
                effectiveSelectedTrigger.EntryMode == TriggerEntryMode.Event &&
                !string.IsNullOrWhiteSpace(effectiveSelectedTrigger.Event) &&
                rules.Count > 1)
                EditorGUILayout.HelpBox("这些规则会独立判断；多个条件同时成立时，对应行为都会执行。", MessageType.Info);

            var overviewWidth = availableWidth > 0f ? availableWidth : EditorGUIUtility.currentViewWidth;
            var split = TriggerAuthoringWorkspaceLayout.ShouldSplitRuleOverview(overviewWidth);
            for (var i = 0; i < rules.Count; i++) DrawRuleOverviewRow(rules[i], split, overviewWidth);
        }

        private void DrawRuleOverviewRow(TriggerAuthoringRuleOverviewItem item, bool split, float availableWidth)
        {
            var trigger = item.Trigger;
            var selected = item.Index == _selectedTriggerIndex;
            var oldBackground = GUI.backgroundColor;
            if (selected) GUI.backgroundColor = new Color(0.50f, 0.72f, 0.94f);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUI.backgroundColor = oldBackground;

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(trigger.Enabled ? "启用" : "停用", trigger.Enabled ? EditorStyles.miniBoldLabel : EditorStyles.centeredGreyMiniLabel, GUILayout.Width(30f));
            if (GUILayout.Button(
                    new GUIContent(trigger.Id + "  " + DisplayTriggerName(trigger), "选择此规则"),
                    EditorStyles.label,
                    GUILayout.ExpandWidth(true)))
                OpenRule(item.Index, TriggerEditorTab.Overview);
            if (!string.IsNullOrWhiteSpace(item.SourceLabel) && !string.Equals(item.SourceLabel, "本地", StringComparison.Ordinal))
                GUILayout.Label(item.SourceLabel, EditorStyles.centeredGreyMiniLabel, GUILayout.Width(34f));
            if (item.HasResolutionError)
                GUILayout.Label("错误", EditorStyles.miniBoldLabel, GUILayout.Width(30f));
            GUILayout.Label("优先级 " + trigger.Priority, EditorStyles.miniLabel, GUILayout.Width(64f));
            if (GUILayout.Button(new GUIContent("×", "删除此规则"), EditorStyles.miniButton, GUILayout.Width(24f)) &&
                DeleteTrigger(item.Index))
            {
                // The list changed during OnGUI. Stop this event before the mouse-up can hit
                // the row that moved into the deleted row's position.
                GUIUtility.ExitGUI();
            }
            EditorGUILayout.EndHorizontal();

            var summaryWidth = Mathf.Max(120f, split ? (availableWidth - 52f) * 0.5f : availableWidth - 22f);
            if (split)
            {
                var summaryHeight = Mathf.Max(
                    GetRuleSummaryHeight(item, TriggerNodeKind.Condition, summaryWidth),
                    GetRuleSummaryHeight(item, TriggerNodeKind.Action, summaryWidth));
                EditorGUILayout.BeginHorizontal();
                DrawRuleSummaryButton(item, TriggerNodeKind.Condition, summaryHeight);
                GUILayout.Label("→", EditorStyles.centeredGreyMiniLabel, GUILayout.Width(18f));
                DrawRuleSummaryButton(item, TriggerNodeKind.Action, summaryHeight);
                EditorGUILayout.EndHorizontal();
            }
            else
            {
                DrawRuleSummaryButton(
                    item,
                    TriggerNodeKind.Condition,
                    GetRuleSummaryHeight(item, TriggerNodeKind.Condition, summaryWidth));
                DrawRuleSummaryButton(
                    item,
                    TriggerNodeKind.Action,
                    GetRuleSummaryHeight(item, TriggerNodeKind.Action, summaryWidth));
            }
            EditorGUILayout.EndVertical();
        }

        private float GetRuleSummaryHeight(
            TriggerAuthoringRuleOverviewItem item,
            TriggerNodeKind kind,
            float width)
        {
            var isCondition = kind == TriggerNodeKind.Condition;
            var summary = isCondition ? item.ConditionSummary : item.ActionSummary;
            var content = new GUIContent((isCondition ? "条件\n" : "行为\n") + summary);
            return Mathf.Max(48f, RuleSummaryStyle.CalcHeight(content, width));
        }

        private void DrawRuleSummaryButton(
            TriggerAuthoringRuleOverviewItem item,
            TriggerNodeKind kind,
            float height)
        {
            var isCondition = kind == TriggerNodeKind.Condition;
            var summary = isCondition ? item.ConditionSummary : item.ActionSummary;
            var tooltip = isCondition ? item.ConditionTooltip : item.ActionTooltip;
            var oldBackground = GUI.backgroundColor;
            GUI.backgroundColor = Color.Lerp(
                Color.white,
                GetSemanticColor(isCondition ? TriggerSemanticArea.Condition : TriggerSemanticArea.Action),
                EditorGUIUtility.isProSkin ? 0.45f : 0.30f);
            if (GUILayout.Button(
                    new GUIContent((isCondition ? "条件\n" : "行为\n") + summary, tooltip + "\n\n点击进入详细编辑。"),
                    RuleSummaryStyle,
                    GUILayout.MinWidth(120f),
                    GUILayout.ExpandWidth(true),
                    GUILayout.Height(height)))
                OpenRule(item.Index, TriggerEditorTab.RuleTree, kind);
            GUI.backgroundColor = oldBackground;
        }

        private GUIStyle RuleSummaryStyle
        {
            get
            {
                if (_ruleSummaryStyle != null) return _ruleSummaryStyle;
                _ruleSummaryStyle = new GUIStyle(EditorStyles.miniButton)
                {
                    alignment = TextAnchor.MiddleLeft,
                    padding = new RectOffset(8, 8, 4, 4),
                    wordWrap = true,
                    clipping = TextClipping.Clip,
                    fixedHeight = 0f
                };
                return _ruleSummaryStyle;
            }
        }

        private void OpenRule(int index, TriggerEditorTab tab, TriggerNodeKind? nodeKind = null)
        {
            SelectTrigger(index, _selectedTriggerGroupKey);
            _selectedEditorTab = tab;
            if (nodeKind.HasValue)
            {
                var isCondition = nodeKind.Value == TriggerNodeKind.Condition;
                SetSelectedNodePath(
                    nodeKind.Value,
                    "module.triggers[" + index + "]" + (isCondition ? ".condition" : ".actions"));
                SetNodeFocusPath(nodeKind.Value, null);
            }
            _focusedDiagnosticPath = null;
            GUI.FocusControl(null);
            RequestRepaint();
        }

        private void AddRuleForEvent(TriggerDefinitionData source)
        {
            var triggers = _asset.Module.Triggers ?? (_asset.Module.Triggers = new List<TriggerDefinitionData>());
            var insertIndex = triggers.Count;
            for (var i = triggers.Count - 1; i >= 0; i--)
            {
                var effective = ResolveEffectiveTrigger(triggers[i]);
                if (effective != null && string.Equals(effective.Event, source.Event, StringComparison.Ordinal))
                {
                    insertIndex = i + 1;
                    break;
                }
            }

            Edit("添加同事件规则", () =>
            {
                var created = CreateTrigger();
                created.Name = "新建事件规则";
                created.Event = source.Event;
                created.GroupPath = source.GroupPath;
                triggers.Insert(insertIndex, created);
                _selectedTriggerIndex = insertIndex;
                ResetNodeNavigation();
            });
            _selectedEditorTab = TriggerEditorTab.Overview;
            RequestRepaint();
        }

        private void DrawTriggerSettings(TriggerDefinitionData trigger)
        {
            var effectiveTrigger = ResolveEffectiveTrigger(trigger);
            var usesTemplate = trigger.Template != null && !ReferenceEquals(effectiveTrigger, trigger);
            GUILayout.Space(10f);
            GUILayout.Label("其他配置", EditorStyles.boldLabel);
            DrawTemplateBinding(trigger);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            _showEditorOrganization = EditorGUILayout.Foldout(
                _showEditorOrganization,
                "编辑器整理信息",
                true);
            if (_showEditorOrganization)
            {
                EditorGUILayout.HelpBox(
                    "以下信息仅用于编辑器中的搜索、筛选和分组，不参与 EventBus、GameplayTag 或运行时逻辑。",
                    MessageType.Info);
                trigger.GroupPath = EditorGUILayout.TextField(
                    new GUIContent("业务分组", "用于触发器列表和 Source JSON 的业务分组路径，不参与运行时逻辑"),
                    trigger.GroupPath);
                trigger.Tags = TriggerAuthoringTriggerBatchOperations.ParseTags(EditorGUILayout.TextField(
                    new GUIContent("检索关键词", "仅用于编辑器搜索、筛选和分组；多个关键词使用英文逗号分隔"),
                    FormatTags(trigger.Tags)));
            }
            EditorGUILayout.EndVertical();

            if (effectiveTrigger.EntryMode == TriggerEntryMode.Callable ||
                Count(trigger.CallableParameters) > 0)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                _showCallableParameters = EditorGUILayout.Foldout(
                    _showCallableParameters,
                    $"调用接口（{Count(trigger.CallableParameters)}）",
                    true);
                if (_showCallableParameters)
                    DrawCallableParameters(trigger);
                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            _showAdvanced = EditorGUILayout.Foldout(_showAdvanced, "高级执行配置", true);
            if (_showAdvanced)
            {
                using (new EditorGUI.DisabledScope(usesTemplate))
                {
                    effectiveTrigger.Phase = DrawConstrainedOption("执行阶段", effectiveTrigger.Phase, PhaseOptions, PhaseOptionNames);
                    effectiveTrigger.Scope = DrawConstrainedOption("作用域", effectiveTrigger.Scope, ScopeOptions, ScopeOptionNames);
                    effectiveTrigger.Priority = EditorGUILayout.IntField("优先级", effectiveTrigger.Priority);
                    effectiveTrigger.InterruptPriority = EditorGUILayout.IntField("中断优先级", effectiveTrigger.InterruptPriority);
                    effectiveTrigger.AllowExternal = EditorGUILayout.Toggle("允许外部触发", effectiveTrigger.AllowExternal);
                    effectiveTrigger.Note = EditorGUILayout.TextField("备注", effectiveTrigger.Note);

                    effectiveTrigger.Cue = effectiveTrigger.Cue ?? new TriggerCueData();
                    effectiveTrigger.Cue.CueId = EditorGUILayout.TextField("表现提示 ID", effectiveTrigger.Cue.CueId);

                    effectiveTrigger.Schedule = effectiveTrigger.Schedule ?? new TriggerScheduleData();
                    effectiveTrigger.Schedule.Mode = DrawConstrainedOption("调度模式", effectiveTrigger.Schedule.Mode, ScheduleModeOptions, ScheduleModeOptionNames);
                    effectiveTrigger.Schedule.DelayMilliseconds = EditorGUILayout.IntField("延迟（毫秒）", effectiveTrigger.Schedule.DelayMilliseconds);
                    effectiveTrigger.Schedule.IntervalMilliseconds = EditorGUILayout.IntField("间隔（毫秒）", effectiveTrigger.Schedule.IntervalMilliseconds);
                    effectiveTrigger.Schedule.RepeatCount = EditorGUILayout.IntField("重复次数", effectiveTrigger.Schedule.RepeatCount);

                    effectiveTrigger.ExecutionControl = effectiveTrigger.ExecutionControl ?? new TriggerExecutionControlData();
                    effectiveTrigger.ExecutionControl.Mode = DrawConstrainedOption(
                        "执行模式", effectiveTrigger.ExecutionControl.Mode, ExecutionModeOptions, ExecutionModeOptionNames);
                    if (string.Equals(effectiveTrigger.ExecutionControl.Mode, "repeat", StringComparison.OrdinalIgnoreCase))
                        effectiveTrigger.ExecutionControl.MaxExecutions = EditorGUILayout.IntField(
                            "最大执行次数", effectiveTrigger.ExecutionControl.MaxExecutions);
                    if (string.Equals(effectiveTrigger.ExecutionControl.Mode, "cooldown", StringComparison.OrdinalIgnoreCase))
                        effectiveTrigger.ExecutionControl.CooldownMilliseconds = EditorGUILayout.DoubleField(
                            "冷却（毫秒）", effectiveTrigger.ExecutionControl.CooldownMilliseconds);
                    effectiveTrigger.ExecutionControl.InterruptPolicy = DrawConstrainedOption(
                        "中断策略", effectiveTrigger.ExecutionControl.InterruptPolicy, InterruptPolicyOptions, InterruptPolicyOptionNames);
                    effectiveTrigger.ExecutionControl.StopPropagationOnSuccess =
                        EditorGUILayout.Toggle("成功后停止传播", effectiveTrigger.ExecutionControl.StopPropagationOnSuccess);
                    effectiveTrigger.ExecutionControl.StopPropagationOnFailure =
                        EditorGUILayout.Toggle("失败后停止传播", effectiveTrigger.ExecutionControl.StopPropagationOnFailure);
                }
                if (usesTemplate)
                    GUILayout.Label("以上配置由模板统一维护", EditorStyles.centeredGreyMiniLabel);
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            _showTriggerBlackboard = EditorGUILayout.Foldout(
                _showTriggerBlackboard,
                (usesTemplate ? "模板局部变量" : "触发器局部变量") + $"（{Count(effectiveTrigger.Blackboard)}）",
                true);
            if (_showTriggerBlackboard)
            {
                using (new EditorGUI.DisabledScope(usesTemplate))
                    DrawBlackboard(
                        effectiveTrigger.Blackboard,
                        usesTemplate ? "模板局部变量" : "触发器局部变量",
                        TriggerAuthoringLocalBlackboardScope.Trigger,
                        _asset.Module != null ? _asset.Module.Blackboard : null);
            }
            EditorGUILayout.EndVertical();
        }

        private void DrawCallableParameters(TriggerDefinitionData trigger)
        {
            var parameters = trigger.CallableParameters ??
                             (trigger.CallableParameters = new List<TriggerCallableParameterData>());
            EditorGUILayout.HelpBox(
                "输入和输出通过触发器局部变量传递。输入在被调用逻辑中只读，输出由被调用逻辑写入。",
                MessageType.Info);
            for (var i = 0; i < parameters.Count; i++)
            {
                var parameter = parameters[i] ?? (parameters[i] = new TriggerCallableParameterData());
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label("参数 " + (i + 1), EditorStyles.miniBoldLabel);
                GUILayout.FlexibleSpace();
                var remove = GUILayout.Button(new GUIContent("×", "删除调用参数"), EditorStyles.miniButton, GUILayout.Width(22f));
                EditorGUILayout.EndHorizontal();

                parameter.Name = EditorGUILayout.TextField("名称", parameter.Name);
                var previousType = parameter.Type;
                parameter.Type = DrawCallableValueTypePopup("类型", parameter.Type);
                parameter.Direction = (TriggerCallableParameterDirection)EditorGUILayout.EnumPopup(
                    "方向",
                    parameter.Direction);
                parameter.Required = EditorGUILayout.Toggle("必填", parameter.Required);
                var previousKey = parameter.LocalVariableKey;
                parameter.LocalVariableKey = EditorGUILayout.TextField("局部变量 Key", parameter.LocalVariableKey);
                parameter.Description = EditorGUILayout.TextField("说明", parameter.Description);

                if (parameter.Direction == TriggerCallableParameterDirection.Input)
                {
                    parameter.HasDefault = EditorGUILayout.Toggle("使用默认值", parameter.HasDefault);
                    if (parameter.HasDefault)
                    {
                        if (parameter.DefaultValue == null || parameter.DefaultValue.Type != parameter.Type)
                            parameter.DefaultValue = CreateValue(parameter.Type);
                        DrawValueRef(
                            parameter.DefaultValue,
                            new TriggerParameterDescriptor(
                                parameter.Name,
                                parameter.Type,
                                false,
                                TriggerValueSourceMask.Constant),
                            trigger);
                    }
                }
                else
                {
                    parameter.HasDefault = false;
                }

                if (previousType != parameter.Type ||
                    !string.Equals(previousKey, parameter.LocalVariableKey, StringComparison.Ordinal))
                    EnsureCallableLocalVariable(trigger, parameter);

                EditorGUILayout.EndVertical();
                if (remove)
                {
                    parameters.RemoveAt(i);
                    i--;
                }
            }

            if (GUILayout.Button("+ 添加调用参数", EditorStyles.miniButton))
            {
                var name = NextCallableParameterName(parameters);
                var parameter = new TriggerCallableParameterData
                {
                    Name = name,
                    LocalVariableKey = name,
                    Type = TriggerValueType.Number,
                    Direction = TriggerCallableParameterDirection.Input,
                    Required = true,
                    DefaultValue = CreateValue(TriggerValueType.Number)
                };
                parameters.Add(parameter);
                EnsureCallableLocalVariable(trigger, parameter);
            }
        }

        private static void EnsureCallableLocalVariable(
            TriggerDefinitionData trigger,
            TriggerCallableParameterData parameter)
        {
            if (trigger == null || parameter == null || string.IsNullOrWhiteSpace(parameter.LocalVariableKey)) return;
            var variables = trigger.Blackboard ?? (trigger.Blackboard = new List<TriggerBlackboardVariableData>());
            for (var i = 0; i < variables.Count; i++)
            {
                var variable = variables[i];
                if (variable == null || !string.Equals(variable.Key, parameter.LocalVariableKey, StringComparison.Ordinal)) continue;
                variable.Type = parameter.Type;
                variable.ReadOnly = false;
                if (variable.DefaultValue == null || variable.DefaultValue.Type != parameter.Type)
                    variable.DefaultValue = CreateValue(parameter.Type);
                return;
            }
            variables.Add(new TriggerBlackboardVariableData
            {
                Key = parameter.LocalVariableKey,
                Type = parameter.Type,
                ReadOnly = false,
                Description = "Callable " + parameter.Direction,
                DefaultValue = CreateValue(parameter.Type)
            });
        }

        private static string NextCallableParameterName(IReadOnlyList<TriggerCallableParameterData> parameters)
        {
            for (var suffix = 1; ; suffix++)
            {
                var candidate = "parameter" + suffix;
                var used = false;
                for (var i = 0; i < parameters.Count; i++)
                    if (parameters[i] != null && string.Equals(parameters[i].Name, candidate, StringComparison.Ordinal))
                    {
                        used = true;
                        break;
                    }
                if (!used) return candidate;
            }
        }

        private void DrawTemplateBinding(TriggerDefinitionData trigger)
        {
            TriggerAuthoringTemplateAsset current = null;
            var reference = trigger.Template;
            if (reference != null && _templates != null)
                _templates.TryGet(reference.TemplateId, out current);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            var selected = (TriggerAuthoringTemplateAsset)EditorGUILayout.ObjectField(
                "触发器模板",
                current,
                typeof(TriggerAuthoringTemplateAsset),
                false);
            if (selected != current && ConfirmTemplateAssignment(trigger, selected))
                Edit("分配触发器模板", () => AssignTemplate(trigger, selected));
            using (new EditorGUI.DisabledScope(current == null))
            {
                if (GUILayout.Button(new GUIContent("打开", "定位模板资源"), EditorStyles.miniButton, GUILayout.Width(42f)))
                {
                    Selection.activeObject = current;
                    EditorGUIUtility.PingObject(current);
                }
            }
            using (new EditorGUI.DisabledScope(reference == null || string.IsNullOrWhiteSpace(reference.TemplateId) || _asset.Project == null))
            {
                if (GUILayout.Button(new GUIContent("引用", "查找绑定此模板的触发器"), EditorStyles.miniButton, GUILayout.Width(42f)))
                    ShowReferences(
                        TriggerAuthoringReferenceFinder.FindTemplateReferences(_asset.Project, reference.TemplateId),
                        "模板：" + reference.TemplateId);
            }
            EditorGUILayout.EndHorizontal();

            if (reference != null)
            {
                EditorGUILayout.LabelField(
                    $"{reference.TemplateId ?? "<缺失>"}  v{reference.Version ?? "<缺失>"}",
                    EditorStyles.miniLabel);
                if (current == null)
                {
                    EditorGUILayout.HelpBox(TriggerAuthoringEditorIntegration.T("template-ref-missing"), MessageType.Error);
                    DrawRawTemplateBindings(reference.Bindings, trigger);
                }
                else
                {
                    DrawTypedTemplateBindings(reference, current.Template, trigger);
                    EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                    GUILayout.Label("模板真实逻辑会在规则树中以只读方式展开。", EditorStyles.miniLabel);
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("查看规则树", EditorStyles.miniButton, GUILayout.Width(78f)))
                        _selectedEditorTab = TriggerEditorTab.RuleTree;
                    EditorGUILayout.EndHorizontal();
                    if (GUILayout.Button(new GUIContent("转为本地触发器", "复制模板当前完整配置并应用实例输入，之后不再跟随模板变化")))
                        MaterializeTemplateInstance(trigger, current);
                }
            }
            EditorGUILayout.EndVertical();
        }

        private static bool ConfirmTemplateAssignment(
            TriggerDefinitionData trigger,
            TriggerAuthoringTemplateAsset selected)
        {
            if (selected == null)
            {
                return trigger.Template == null ||
                       EditorUtility.DisplayDialog(
                    "移除模板",
                    "确定移除此触发器的模板绑定吗？\n\n" +
                           "实例将保留自身 ID 和整理信息，并恢复为空的本地触发器配置。",
                           "移除模板",
                           "取消");
            }

            var losesTrees = trigger.Condition != null || trigger.Actions != null;
            var losesLocalVariables = trigger.Blackboard != null && trigger.Blackboard.Count > 0;
            if (trigger.Template == null && !losesTrees && !losesLocalVariables) return true;

            var message = "分配此模板将执行以下操作：";
            if (losesTrees) message += "\n  - 清除本地条件树和行为树";
            if (losesLocalVariables) message += "\n  - 清除实例局部变量，改用模板 LocalVar";
            message += "\n  - 使用模板提供的入口与执行配置";
            if (trigger.Template != null) message += "\n  - 替换当前模板绑定";
            message += "\n\n是否继续？";
            return EditorUtility.DisplayDialog("分配模板", message, "分配模板", "取消");
        }

        private void AssignTemplate(TriggerDefinitionData trigger, TriggerAuthoringTemplateAsset asset)
        {
            if (asset?.Template == null)
            {
                trigger.Template = null;
                return;
            }

            var reference = new TriggerTemplateReferenceData
            {
                TemplateId = asset.Template.TemplateId,
                Version = asset.Template.TemplateVersion
            };
            var parameters = asset.Template.Parameters ?? new List<TriggerAuthoringTemplateParameterData>();
            for (var i = 0; i < parameters.Count; i++)
            {
                var parameter = parameters[i];
                if (parameter == null || string.IsNullOrWhiteSpace(parameter.Name) ||
                    !parameter.Required || parameter.HasDefault)
                    continue;
                reference.Bindings.Add(new TriggerArgumentData
                {
                    Name = parameter.Name,
                    Value = CreateValue(parameter.Type)
                });
            }
            trigger.Template = reference;
            trigger.EntryMode = TriggerEntryMode.Callable;
            trigger.Event = string.Empty;
            trigger.Phase = "immediate";
            trigger.Priority = 0;
            trigger.InterruptPriority = 0;
            trigger.Scope = "owner";
            trigger.AllowExternal = false;
            trigger.Schedule = new TriggerScheduleData();
            trigger.Cue = new TriggerCueData();
            trigger.ExecutionControl = new TriggerExecutionControlData();
            trigger.Condition = null;
            trigger.Actions = null;
            trigger.Blackboard = new List<TriggerBlackboardVariableData>();
            trigger.Note = null;
        }

        private void MaterializeTemplateInstance(
            TriggerDefinitionData trigger,
            TriggerAuthoringTemplateAsset templateAsset)
        {
            if (trigger?.Template == null || templateAsset?.Template == null) return;
            if (!EditorUtility.DisplayDialog(
                    "转为本地触发器",
                    "将复制模板当前的完整入口、条件、行为、执行配置和内部 LocalVar，并应用此实例的输入绑定。\n\n转换后不再跟随模板更新，是否继续？",
                    "转为本地触发器",
                    "取消"))
                return;

            var materialized = TriggerAuthoringTemplateDefinition.CreateMaterialized(
                trigger,
                templateAsset.Template,
                out var error);
            if (materialized == null)
            {
                EditorUtility.DisplayDialog("无法转为本地触发器", error ?? "模板输入无法实化。", "确定");
                return;
            }

            Undo.RecordObject(_asset, "模板转为本地触发器");
            TriggerAuthoringTemplateDefinition.CopyInto(trigger, materialized);
            EditorUtility.SetDirty(_asset);
            RefreshDiagnostics();
            _nextSyncInspectionAt = 0d;
            _triggerGroupsInitialized = false;
            ExpandVisibleTriggerGroups();
            ShowNotification("已转为本地触发器");
            RequestRepaint();
        }

        private void DrawTypedTemplateBindings(
            TriggerTemplateReferenceData reference,
            TriggerAuthoringTemplateData template,
            TriggerDefinitionData trigger)
        {
            var bindings = reference.Bindings;
            if (bindings == null)
            {
                EditorGUILayout.HelpBox(TriggerAuthoringEditorIntegration.T("template-bindings-null"), MessageType.Error);
                return;
            }
            var parameters = template?.Parameters ?? new List<TriggerAuthoringTemplateParameterData>();
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("模板调用输入（" + parameters.Count + "）", EditorStyles.miniBoldLabel);
            GUILayout.FlexibleSpace();
            var requiredCount = CountRequiredTemplateInputs(parameters);
            if (requiredCount > 0)
                GUILayout.Label(requiredCount + " 个必填", EditorStyles.centeredGreyMiniLabel);
            EditorGUILayout.EndHorizontal();
            if (parameters.Count == 0)
                GUILayout.Label("此模板无需额外输入。", EditorStyles.miniLabel);
            for (var i = 0; i < parameters.Count; i++)
            {
                var parameter = parameters[i];
                if (parameter == null || string.IsNullOrWhiteSpace(parameter.Name)) continue;
                var binding = FindArgument(bindings, parameter.Name);
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();
                var inputTitle = parameter.Name + "  ·  " + TriggerAuthoringEditorLabels.ValueType(parameter.Type);
                GUILayout.Label(new GUIContent(inputTitle, parameter.Description ?? string.Empty), EditorStyles.miniBoldLabel);
                GUILayout.FlexibleSpace();
                GUILayout.Label(
                    parameter.Required && !parameter.HasDefault ? "必填" : "可选",
                    EditorStyles.centeredGreyMiniLabel,
                    GUILayout.Width(30f));
                if (binding == null)
                {
                    var label = parameter.HasDefault ? "覆盖" : TriggerAuthoringEditorIntegration.T("add");
                    if (GUILayout.Button(label, EditorStyles.miniButton, GUILayout.Width(58f)))
                    {
                        var captured = parameter;
                        Edit("添加模板绑定", () => bindings.Add(new TriggerArgumentData
                        {
                            Name = captured.Name,
                            Value = CreateValue(captured.Type)
                        }));
                    }
                }
                else if ((!parameter.Required || parameter.HasDefault) &&
                         GUILayout.Button("x", EditorStyles.miniButton, GUILayout.Width(22f)))
                {
                    var captured = binding;
                    Edit("移除模板绑定", () => bindings.Remove(captured));
                }
                EditorGUILayout.EndHorizontal();
                if (binding != null)
                {
                    if (binding.Value == null)
                        EditorGUILayout.HelpBox(TriggerAuthoringEditorIntegration.T("binding-value-null"), MessageType.Error);
                    else
                        DrawValueRef(
                            binding.Value,
                            new TriggerParameterDescriptor(
                                parameter.Name,
                                parameter.Type,
                                true,
                                (TriggerValueSourceMask)(int)parameter.AllowedSources),
                            trigger);
                }
                else if (parameter.HasDefault)
                {
                    EditorGUILayout.LabelField(
                        TriggerAuthoringEditorIntegration.T("using-template-default") + "：" + SummarizeValue(parameter.DefaultValue),
                        EditorStyles.miniLabel);
                }
                else if (parameter.Required)
                {
                    EditorGUILayout.HelpBox(TriggerAuthoringEditorIntegration.T("required-binding-missing"), MessageType.Error);
                }
                EditorGUILayout.EndVertical();
            }

            for (var i = 0; i < bindings.Count; i++)
            {
                var binding = bindings[i];
                if (binding == null || HasTemplateParameter(parameters, binding.Name)) continue;
                EditorGUILayout.HelpBox($"无法识别绑定“{binding.Name}”，其数据会继续保留。", MessageType.Error);
            }
        }

        private void DrawRawTemplateBindings(List<TriggerArgumentData> bindings, TriggerDefinitionData trigger)
        {
            if (bindings == null) return;
            for (var i = 0; i < bindings.Count; i++)
            {
                var binding = bindings[i];
                if (binding == null) continue;
                EditorGUILayout.LabelField(binding.Name ?? "<未命名>", EditorStyles.miniBoldLabel);
                if (binding.Value == null)
                    EditorGUILayout.HelpBox(TriggerAuthoringEditorIntegration.T("binding-value-null"), MessageType.Error);
                else
                    DrawValueRef(binding.Value, null, trigger);
            }
        }

        private static bool HasTemplateParameter(
            IReadOnlyList<TriggerAuthoringTemplateParameterData> parameters,
            string name)
        {
            if (parameters == null) return false;
            for (var i = 0; i < parameters.Count; i++)
                if (parameters[i] != null && string.Equals(parameters[i].Name, name, StringComparison.Ordinal)) return true;
            return false;
        }

        private static int CountRequiredTemplateInputs(
            IReadOnlyList<TriggerAuthoringTemplateParameterData> parameters)
        {
            if (parameters == null) return 0;
            var count = 0;
            for (var i = 0; i < parameters.Count; i++)
                if (parameters[i] != null && parameters[i].Required && !parameters[i].HasDefault) count++;
            return count;
        }

        private GUIStyle SemanticSectionTitleStyle
        {
            get
            {
                if (_semanticSectionTitleStyle != null) return _semanticSectionTitleStyle;
                _semanticSectionTitleStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    fontSize = 13,
                    alignment = TextAnchor.UpperLeft
                };
                return _semanticSectionTitleStyle;
            }
        }

        private GUIStyle SemanticSectionSubtitleStyle
        {
            get
            {
                if (_semanticSectionSubtitleStyle != null) return _semanticSectionSubtitleStyle;
                _semanticSectionSubtitleStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    alignment = TextAnchor.UpperLeft
                };
                return _semanticSectionSubtitleStyle;
            }
        }

        private GUIStyle NodeTechnicalStyle
        {
            get
            {
                if (_nodeTechnicalStyle != null) return _nodeTechnicalStyle;
                _nodeTechnicalStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    fontSize = 9,
                    clipping = TextClipping.Clip
                };
                return _nodeTechnicalStyle;
            }
        }

        private void DrawSemanticSectionHeader(string title, string subtitle, TriggerSemanticArea area)
        {
            var color = GetSemanticColor(area);
            var background = EditorGUIUtility.isProSkin
                ? Color.Lerp(new Color(0.17f, 0.17f, 0.17f), color, 0.22f)
                : Color.Lerp(new Color(0.88f, 0.88f, 0.88f), color, 0.16f);
            var rect = GUILayoutUtility.GetRect(0f, 44f, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(rect, background);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 5f, rect.height), color);
            GUI.Label(new Rect(rect.x + 12f, rect.y + 5f, rect.width - 18f, 20f), title, SemanticSectionTitleStyle);
            GUI.Label(new Rect(rect.x + 12f, rect.y + 24f, rect.width - 18f, 16f), subtitle, SemanticSectionSubtitleStyle);
        }

        private static void DrawNodeAccent(TriggerNodeKind kind)
        {
            var rect = GUILayoutUtility.GetRect(4f, 34f, GUILayout.Width(4f), GUILayout.Height(34f));
            EditorGUI.DrawRect(
                rect,
                GetSemanticColor(kind == TriggerNodeKind.Condition
                    ? TriggerSemanticArea.Condition
                    : TriggerSemanticArea.Action));
        }

        private static Color GetSemanticColor(TriggerSemanticArea area)
        {
            switch (area)
            {
                case TriggerSemanticArea.Condition: return new Color(0.16f, 0.66f, 0.54f);
                case TriggerSemanticArea.Action: return new Color(0.93f, 0.52f, 0.16f);
                default: return new Color(0.20f, 0.53f, 0.88f);
            }
        }

        private void DrawRuleTreeWorkspace(TriggerDefinitionData trigger, float availableWidth)
        {
            DrawSemanticSectionHeader(
                "完整规则树",
                "入口条件与执行流程在同一棵树中查看和编辑",
                TriggerSemanticArea.Event);

            var triggerPath = "module.triggers[" + _selectedTriggerIndex + "]";
            var conditionPath = triggerPath + ".condition";
            var actionPath = triggerPath + ".actions";
            var conditionRoot = trigger.Condition;
            var actionRoot = trigger.Actions;
            var source = NodeOutlineSource.Local;
            var readOnly = false;
            string conditionError = null;
            string actionError = null;
            var template = ResolveBoundTemplate(trigger);
            if (trigger.Template != null)
            {
                source = NodeOutlineSource.Template;
                readOnly = true;
                if (template == null)
                {
                    conditionRoot = null;
                    actionRoot = null;
                    conditionError = actionError = "模板引用无法解析，请在“配置”页签修复模板绑定。";
                }
                else
                {
                    var definition = TriggerAuthoringTemplateDefinition.Get(template);
                    conditionRoot = definition.Condition;
                    actionRoot = definition.Actions;
                    DrawTemplateLogicBanner(template, trigger.Template);
                }
            }

            if (readOnly)
            {
                if (conditionRoot != null && !TriggerAuthoringGroupResolver.TryExpand(
                        _asset.Module,
                        conditionRoot,
                        TriggerNodeKind.Condition,
                        out conditionRoot,
                        out var conditionFailure))
                    conditionError = conditionFailure != null ? conditionFailure.Message : "模板条件逻辑解析失败。";
                if (actionRoot != null && !TriggerAuthoringGroupResolver.TryExpand(
                        _asset.Module,
                        actionRoot,
                        TriggerNodeKind.Action,
                        out actionRoot,
                        out var actionFailure))
                    actionError = actionFailure != null ? actionFailure.Message : "模板执行逻辑解析失败。";
            }

            var items = new List<NodeOutlineItem>();
            if (conditionRoot != null && conditionError == null)
                BuildNodeOutline(
                    items,
                    conditionRoot,
                    TriggerNodeKind.Condition,
                    conditionPath,
                    null,
                    "入口条件",
                    1,
                    source,
                    readOnly,
                    null,
                    -1,
                    true,
                    TriggerNodeKind.Condition);
            if (actionRoot != null && actionError == null)
                BuildNodeOutline(
                    items,
                    actionRoot,
                    TriggerNodeKind.Action,
                    actionPath,
                    null,
                    "执行流程",
                    1,
                    source,
                    readOnly,
                    null,
                    -1,
                    true,
                    TriggerNodeKind.Action);
            MarkOutlineChildren(items);
            EnsureRuleTreeSelection(items);

            if (!string.IsNullOrEmpty(conditionError))
                EditorGUILayout.HelpBox("入口条件：" + conditionError, MessageType.Error);
            if (!string.IsNullOrEmpty(actionError) && !string.Equals(actionError, conditionError, StringComparison.Ordinal))
                EditorGUILayout.HelpBox("执行流程：" + actionError, MessageType.Error);

            var workspaceWidth = availableWidth > 0f
                ? availableWidth
                : Mathf.Max(360f, EditorGUIUtility.currentViewWidth - 32f);
            if (TriggerAuthoringWorkspaceLayout.ShouldSplitNodeWorkspace(workspaceWidth))
            {
                _nodeOutlineWidth = TriggerAuthoringWorkspaceLayout.ClampNodeOutlineWidth(
                    _nodeOutlineWidth,
                    workspaceWidth);
                EditorGUILayout.BeginHorizontal(GUILayout.MinHeight(520f));
                DrawRuleTreeOutlinePanel(trigger, items, conditionRoot, actionRoot, readOnly, _nodeOutlineWidth, 520f);
                DrawNodeOutlineSplitter(workspaceWidth);
                DrawSelectedNodeDetails(trigger, items, readOnly, 520f);
                EditorGUILayout.EndHorizontal();
            }
            else
            {
                DrawRuleTreeOutlinePanel(trigger, items, conditionRoot, actionRoot, readOnly, 0f, 320f);
                GUILayout.Space(5f);
                DrawSelectedNodeDetails(trigger, items, readOnly, 420f);
            }
        }

        private TriggerAuthoringTemplateData ResolveBoundTemplate(TriggerDefinitionData trigger)
        {
            if (trigger?.Template == null || _templates == null) return null;
            return _templates.TryGet(trigger.Template.TemplateId, out var asset) && asset != null
                ? asset.Template
                : null;
        }

        private TriggerDefinitionData ResolveEffectiveTrigger(TriggerDefinitionData trigger)
        {
            return TriggerAuthoringTemplateDefinition.ResolveEffectiveView(trigger, _templates);
        }

        private static void DrawTemplateLogicBanner(
            TriggerAuthoringTemplateData template,
            TriggerTemplateReferenceData reference)
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            GUILayout.Label("模板真实逻辑", EditorStyles.miniBoldLabel, GUILayout.Width(78f));
            GUILayout.Label(
                (string.IsNullOrWhiteSpace(template.DisplayName) ? template.TemplateId : template.DisplayName) +
                "  v" + (reference.Version ?? template.TemplateVersion),
                EditorStyles.miniLabel);
            GUILayout.FlexibleSpace();
            GUILayout.Label("只读预览", EditorStyles.miniBoldLabel, GUILayout.Width(56f));
            EditorGUILayout.EndHorizontal();
        }

        private void BuildNodeOutline(
            List<NodeOutlineItem> items,
            TriggerNodeData node,
            TriggerNodeKind kind,
            string path,
            string parentPath,
            string parentBreadcrumb,
            int depth,
            NodeOutlineSource source,
            bool readOnly,
            List<TriggerNodeData> owner,
            int ownerIndex,
            bool isRoot,
            TriggerNodeKind? workspaceKind = null,
            NodeOutlineBranch branch = NodeOutlineBranch.None,
            bool isBranchRoot = false,
            TriggerNodeData conditionalOwner = null)
        {
            if (node == null) return;
            _types.TryGet(kind, node.Type, out var descriptor);
            string displayName;
            if (!string.IsNullOrWhiteSpace(node.GroupReference))
            {
                displayName = "引用分组：" + node.GroupReference;
            }
            else if (TriggerAuthoringTriggerReuse.TryGetReferencedTriggerId(node, out var referencedId))
            {
                var referenced = TriggerAuthoringTriggerReuse.FindTrigger(_asset.Module, referencedId);
                displayName = "触发效果 #" + referencedId +
                              (referenced != null ? " · " + DisplayTriggerName(referenced) : " · 引用缺失");
            }
            else
            {
                displayName = TriggerAuthoringEditorLabels.Node(node.Type, descriptor != null ? descriptor.DisplayName : null);
            }
            if (isBranchRoot) displayName = GetBranchLabel(branch) + " / " + displayName;
            var breadcrumb = string.IsNullOrEmpty(parentBreadcrumb)
                ? displayName
                : parentBreadcrumb + " / " + displayName;
            items.Add(new NodeOutlineItem
            {
                Node = node,
                Kind = kind,
                WorkspaceKind = workspaceKind ?? kind,
                Path = path,
                ParentPath = parentPath,
                Breadcrumb = breadcrumb,
                Depth = depth,
                IsReadOnly = readOnly,
                IsRoot = isRoot,
                Source = source,
                Owner = owner,
                OwnerIndex = ownerIndex,
                Branch = branch,
                IsBranchRoot = isBranchRoot,
                ConditionalOwner = conditionalOwner
            });

            if (!string.IsNullOrWhiteSpace(node.GroupReference))
            {
                if (!readOnly && TriggerAuthoringGroupResolver.TryExpand(
                        _asset.Module,
                        node,
                        kind,
                        out var expanded,
                        out _))
                {
                    BuildNodeOutline(
                        items,
                        expanded,
                        kind,
                        path + ".resolved",
                        path,
                        breadcrumb,
                        depth + 1,
                        NodeOutlineSource.GroupPreview,
                        true,
                        null,
                        -1,
                        false,
                        workspaceKind ?? kind,
                        branch,
                        false,
                        conditionalOwner);
                }
                return;
            }

            if (TriggerAuthoringTriggerReuse.IsReference(node))
            {
                if (!readOnly &&
                    TriggerAuthoringTriggerReuse.TryGetReferencedTriggerId(node, out var triggerId))
                {
                    var target = TriggerAuthoringTriggerReuse.FindTrigger(_asset.Module, triggerId);
                    if (target != null &&
                        TriggerAuthoringTriggerReuse.TryCreateLocalCopy(
                            _asset.Module,
                            target,
                            _templates,
                            out var targetLogic,
                            out _) &&
                        targetLogic != null)
                    {
                        BuildNodeOutline(
                            items,
                            targetLogic,
                            TriggerNodeKind.Action,
                            path + ".trigger[" + triggerId + "]",
                            path,
                            breadcrumb,
                            depth + 1,
                            NodeOutlineSource.TriggerPreview,
                            true,
                            null,
                            -1,
                            false,
                            workspaceKind ?? kind,
                            branch,
                            false,
                            conditionalOwner);
                    }
                }
                return;
            }

            if (kind == TriggerNodeKind.Action &&
                string.Equals(node.Type, "conditional", StringComparison.OrdinalIgnoreCase))
            {
                if (node.Condition != null)
                {
                    BuildNodeOutline(
                        items,
                        node.Condition,
                        TriggerNodeKind.Condition,
                        path + ".condition",
                        path,
                        breadcrumb,
                        depth + 1,
                        source,
                        readOnly,
                        null,
                        -1,
                        false,
                        workspaceKind ?? kind,
                        NodeOutlineBranch.Predicate,
                        true,
                        readOnly ? null : node);
                }

                BuildActionBranchOutline(
                    items,
                    node.Children,
                    path + ".children",
                    path,
                    breadcrumb,
                    depth + 1,
                    source,
                    readOnly,
                    workspaceKind ?? kind,
                    NodeOutlineBranch.Then,
                    readOnly ? null : node);
                BuildActionBranchOutline(
                    items,
                    node.ElseChildren,
                    path + ".elseChildren",
                    path,
                    breadcrumb,
                    depth + 1,
                    source,
                    readOnly,
                    workspaceKind ?? kind,
                    NodeOutlineBranch.Else,
                    readOnly ? null : node);
                return;
            }

            var children = node.Children;
            if (children == null) return;
            for (var i = 0; i < children.Count; i++)
            {
                BuildNodeOutline(
                    items,
                    children[i],
                    kind,
                    path + ".children[" + i + "]",
                    path,
                    breadcrumb,
                    depth + 1,
                    source,
                    readOnly,
                    readOnly ? null : children,
                    readOnly ? -1 : i,
                    false,
                    workspaceKind ?? kind,
                    branch,
                    false,
                    conditionalOwner);
            }
        }

        private void BuildActionBranchOutline(
            List<NodeOutlineItem> items,
            List<TriggerNodeData> nodes,
            string pathPrefix,
            string parentPath,
            string parentBreadcrumb,
            int depth,
            NodeOutlineSource source,
            bool readOnly,
            TriggerNodeKind workspaceKind,
            NodeOutlineBranch branch,
            TriggerNodeData conditionalOwner)
        {
            if (nodes == null) return;
            for (var i = 0; i < nodes.Count; i++)
                BuildNodeOutline(
                    items,
                    nodes[i],
                    TriggerNodeKind.Action,
                    pathPrefix + "[" + i + "]",
                    parentPath,
                    parentBreadcrumb,
                    depth,
                    source,
                    readOnly,
                    readOnly ? null : nodes,
                    readOnly ? -1 : i,
                    false,
                    workspaceKind,
                    branch,
                    true,
                    conditionalOwner);
        }

        private static string GetBranchLabel(NodeOutlineBranch branch)
        {
            switch (branch)
            {
                case NodeOutlineBranch.Predicate: return "判断条件";
                case NodeOutlineBranch.Then: return "条件成立";
                case NodeOutlineBranch.Else: return "条件不成立";
                default: return string.Empty;
            }
        }

        private static void MarkOutlineChildren(List<NodeOutlineItem> items)
        {
            var parents = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < items.Count; i++)
                if (!string.IsNullOrEmpty(items[i].ParentPath)) parents.Add(items[i].ParentPath);
            for (var i = 0; i < items.Count; i++) items[i].HasChildren = parents.Contains(items[i].Path);
        }

        private void EnsureRuleTreeSelection(List<NodeOutlineItem> items)
        {
            if (items.Count == 0)
            {
                _selectedRuleNodePath = null;
                return;
            }

            var selectedPath = _selectedRuleNodePath;
            var selected = FindOutlineItem(items, selectedPath);
            if (selected == null && !string.IsNullOrEmpty(_focusedDiagnosticPath))
                selected = FindClosestOutlineItem(items, _focusedDiagnosticPath);
            if (selected == null && !string.IsNullOrEmpty(selectedPath))
            {
                var hasSelectedArea = items.Exists(item => item.WorkspaceKind == _selectedRuleNodeKind);
                if (!hasSelectedArea)
                {
                    _selectedRuleNodePath = null;
                    return;
                }
            }
            if (selected == null) selected = items[0];
            if (!string.Equals(selectedPath, selected.Path, StringComparison.Ordinal))
            {
                SetSelectedNodePath(selected.WorkspaceKind, selected.Path);
                ExpandNodeAncestors(selected, items);
                _expandedNodePaths.Add(selected.Path);
            }
        }

        private void DrawRuleTreeOutlinePanel(
            TriggerDefinitionData trigger,
            List<NodeOutlineItem> items,
            TriggerNodeData conditionRoot,
            TriggerNodeData actionRoot,
            bool readOnly,
            float width,
            float height)
        {
            var options = width > 0f
                ? new[] { GUILayout.Width(width), GUILayout.Height(height) }
                : new[] { GUILayout.ExpandWidth(true), GUILayout.Height(height) };
            EditorGUILayout.BeginVertical(EditorStyles.helpBox, options);
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("节点大纲", EditorStyles.miniBoldLabel, GUILayout.Width(56f));
            _nodeSearch = GUILayout.TextField(_nodeSearch ?? string.Empty, EditorStyles.toolbarSearchField);
            if (GUILayout.Button(new GUIContent("×", "清除节点搜索"), EditorStyles.toolbarButton, GUILayout.Width(22f)))
            {
                _nodeSearch = string.Empty;
                GUI.FocusControl(null);
            }
            if (GUILayout.Button(new GUIContent("+", "展开全部节点"), EditorStyles.toolbarButton, GUILayout.Width(22f)))
            {
                _expandedNodePaths.Add(GetRuleAreaPath(TriggerNodeKind.Condition));
                _expandedNodePaths.Add(GetRuleAreaPath(TriggerNodeKind.Action));
                for (var i = 0; i < items.Count; i++)
                    if (items[i].HasChildren) _expandedNodePaths.Add(items[i].Path);
                _ruleTreeBranchesInitialized = true;
            }
            if (GUILayout.Button(new GUIContent("−", "折叠全部节点"), EditorStyles.toolbarButton, GUILayout.Width(22f)))
            {
                _expandedNodePaths.Clear();
                _ruleFocusPath = null;
                _ruleTreeBranchesInitialized = true;
            }
            EditorGUILayout.EndHorizontal();

            var focusPath = _ruleFocusPath;
            if (!string.IsNullOrEmpty(focusPath))
            {
                var focusItem = FindOutlineItem(items, focusPath);
                EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
                if (GUILayout.Button(new GUIContent("‹", "返回上一级"), EditorStyles.toolbarButton, GUILayout.Width(24f)))
                    SetNodeFocusPath(_ruleFocusKind, focusItem != null ? focusItem.ParentPath : null);
                GUILayout.Label(
                    focusItem != null ? focusItem.Breadcrumb : "全部节点",
                    EditorStyles.miniLabel,
                    GUILayout.ExpandWidth(true));
                if (GUILayout.Button("全部", EditorStyles.toolbarButton, GUILayout.Width(38f)))
                    _ruleFocusPath = null;
                EditorGUILayout.EndHorizontal();
            }

            _nodeOutlineScroll = EditorGUILayout.BeginScrollView(_nodeOutlineScroll);
            var filter = (_nodeSearch ?? string.Empty).Trim();
            if (!_ruleTreeBranchesInitialized)
            {
                _expandedNodePaths.Add(GetRuleAreaPath(TriggerNodeKind.Condition));
                _expandedNodePaths.Add(GetRuleAreaPath(TriggerNodeKind.Action));
                _ruleTreeBranchesInitialized = true;
            }

            DrawRuleTreeRootRow(trigger);
            DrawRuleTreeArea(
                trigger,
                items,
                TriggerNodeKind.Condition,
                conditionRoot,
                readOnly,
                focusPath,
                filter);
            DrawRuleTreeArea(
                trigger,
                items,
                TriggerNodeKind.Action,
                actionRoot,
                readOnly,
                focusPath,
                filter);
            if (!string.IsNullOrEmpty(filter) && !items.Exists(item => MatchesNodeOutline(item, filter)))
                EditorGUILayout.HelpBox("没有匹配的节点。", MessageType.Info);
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawRuleTreeRootRow(TriggerDefinitionData trigger)
        {
            var rect = GUILayoutUtility.GetRect(0f, 27f, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(rect, new Color(0f, 0f, 0f, EditorGUIUtility.isProSkin ? 0.18f : 0.07f));
            GUI.Label(
                new Rect(rect.x + 7f, rect.y + 3f, rect.width - 14f, 21f),
                "触发器 #" + trigger.Id + " · " + DisplayTriggerName(trigger),
                EditorStyles.boldLabel);
        }

        private void DrawRuleTreeArea(
            TriggerDefinitionData trigger,
            List<NodeOutlineItem> items,
            TriggerNodeKind kind,
            TriggerNodeData root,
            bool readOnly,
            string focusPath,
            string filter)
        {
            var areaPath = GetRuleAreaPath(kind);
            var areaItems = items.FindAll(item => item.WorkspaceKind == kind);
            var searchMode = !string.IsNullOrEmpty(filter);
            var expanded = _expandedNodePaths.Contains(areaPath);
            var isCondition = kind == TriggerNodeKind.Condition;
            var label = isCondition ? "入口条件" : "执行流程";
            var color = GetSemanticColor(isCondition ? TriggerSemanticArea.Condition : TriggerSemanticArea.Action);

            var row = GUILayoutUtility.GetRect(0f, 28f, GUILayout.ExpandWidth(true));
            if (_selectedRuleNodeKind == kind && string.IsNullOrEmpty(_selectedRuleNodePath))
                EditorGUI.DrawRect(row, Color.Lerp(color, GUI.skin.settings.selectionColor, 0.45f));
            EditorGUI.DrawRect(new Rect(row.x + 5f, row.y + 3f, 3f, row.height - 6f), color);
            var foldRect = new Rect(row.x + 12f, row.y + 5f, 16f, 18f);
            if (!searchMode)
            {
                var next = EditorGUI.Foldout(foldRect, expanded, GUIContent.none, true);
                if (next != expanded)
                {
                    expanded = next;
                    if (expanded) _expandedNodePaths.Add(areaPath);
                    else _expandedNodePaths.Remove(areaPath);
                }
            }

            var buttonWidth = root == null && !readOnly ? 49f : 0f;
            var titleRect = new Rect(foldRect.xMax, row.y + 2f, row.width - 37f - buttonWidth, 22f);
            var state = root == null ? (readOnly ? "（模板未配置）" : "（未配置）") : "（" + areaItems.Count + " 个节点）";
            if (GUI.Button(titleRect, new GUIContent(label + " " + state), EditorStyles.boldLabel))
            {
                if (root != null && areaItems.Count > 0)
                    SetSelectedNodePath(kind, areaItems[0].Path);
                else
                    SetSelectedNodePath(kind, null);
                if (!searchMode) _expandedNodePaths.Add(areaPath);
            }

            if (root == null && !readOnly)
            {
                var addRect = new Rect(row.xMax - 47f, row.y + 4f, 43f, 20f);
                if (GUI.Button(addRect, new GUIContent("+ 新增", isCondition ? "添加入口条件" : "添加行为根节点"), EditorStyles.miniButton))
                    ShowNodeCreationMenu(kind, created => SetRootNode(trigger, kind, created), addRect);
            }

            if (!expanded && !searchMode) return;
            for (var i = 0; i < areaItems.Count; i++)
            {
                var item = areaItems[i];
                if (!IsOutlineItemVisible(item, items, focusPath, filter)) continue;
                DrawNodeOutlineRow(item, items, searchMode);
            }
        }

        private string GetRuleAreaPath(TriggerNodeKind kind)
        {
            return "rule-tree[" + _selectedTriggerIndex + "]." +
                   (kind == TriggerNodeKind.Condition ? "conditions" : "actions");
        }

        private bool IsOutlineItemVisible(
            NodeOutlineItem item,
            List<NodeOutlineItem> items,
            string focusPath,
            string filter)
        {
            if (!string.IsNullOrEmpty(filter)) return MatchesNodeOutline(item, filter);
            if (!string.IsNullOrEmpty(focusPath) && !IsPathAtOrBelow(item.Path, focusPath)) return false;

            var parentPath = item.ParentPath;
            while (!string.IsNullOrEmpty(parentPath) &&
                   !string.Equals(parentPath, focusPath, StringComparison.Ordinal))
            {
                if (!_expandedNodePaths.Contains(parentPath)) return false;
                var parent = FindOutlineItem(items, parentPath);
                parentPath = parent != null ? parent.ParentPath : null;
            }
            return true;
        }

        private bool MatchesNodeOutline(NodeOutlineItem item, string filter)
        {
            if (ContainsIgnoreCase(item.Breadcrumb, filter) ||
                ContainsIgnoreCase(item.Node.Type, filter) ||
                ContainsIgnoreCase(item.Node.GroupReference, filter)) return true;
            var arguments = item.Node.Arguments;
            if (arguments == null) return false;
            for (var i = 0; i < arguments.Count; i++)
            {
                var argument = arguments[i];
                if (argument != null &&
                    (ContainsIgnoreCase(argument.Name, filter) ||
                     ContainsIgnoreCase(SummarizeValue(argument.Value), filter))) return true;
            }
            return false;
        }

        private void DrawNodeOutlineRow(
            NodeOutlineItem item,
            List<NodeOutlineItem> items,
            bool searchMode)
        {
            var kind = item.WorkspaceKind;
            var row = GUILayoutUtility.GetRect(0f, searchMode ? 36f : 25f, GUILayout.ExpandWidth(true));
            var openContextMenu = ShouldOpenContextMenu(row);
            var selected = string.Equals(GetSelectedNodePath(kind), item.Path, StringComparison.Ordinal);
            var semanticColor = GetSemanticColor(
                item.Kind == TriggerNodeKind.Condition ? TriggerSemanticArea.Condition : TriggerSemanticArea.Action);
            if (selected)
                EditorGUI.DrawRect(row, Color.Lerp(semanticColor, GUI.skin.settings.selectionColor, 0.45f));
            else if (Event.current.type == EventType.Repaint && row.Contains(Event.current.mousePosition))
                EditorGUI.DrawRect(row, new Color(1f, 1f, 1f, EditorGUIUtility.isProSkin ? 0.055f : 0.1f));
            if (item.IsBranchRoot)
                EditorGUI.DrawRect(new Rect(row.x, row.y + 2f, 3f, row.height - 4f), semanticColor);

            var focusItem = FindOutlineItem(items, GetNodeFocusPath(kind));
            var focusDepth = focusItem != null ? focusItem.Depth : 0;
            var depth = searchMode ? 0 : Mathf.Max(0, item.Depth - focusDepth);
            var indent = Mathf.Min(depth, 6) * 14f;
            var foldRect = new Rect(row.x + indent, row.y + 3f, 16f, 18f);
            if (item.HasChildren && !searchMode)
            {
                var expanded = _expandedNodePaths.Contains(item.Path);
                var next = EditorGUI.Foldout(foldRect, expanded, GUIContent.none, true);
                if (next != expanded)
                {
                    if (next) _expandedNodePaths.Add(item.Path);
                    else
                    {
                        _expandedNodePaths.Remove(item.Path);
                        if (IsPathAtOrBelow(GetSelectedNodePath(kind), item.Path))
                            SetSelectedNodePath(kind, item.Path);
                    }
                }
            }

            var badgeWidth = item.Source == NodeOutlineSource.Local ? 0f : 48f;
            var labelRect = new Rect(
                foldRect.xMax,
                row.y + 1f,
                Mathf.Max(20f, row.width - indent - 20f - badgeWidth),
                searchMode ? 20f : row.height - 2f);
            var title = GetNodeOutlineTitle(item);
            if (GUI.Button(labelRect, new GUIContent(title, item.Breadcrumb), EditorStyles.label))
            {
                SetSelectedNodePath(kind, item.Path);
                ExpandNodeAncestors(item, items);
                if (Event.current.clickCount >= 2 && item.HasChildren)
                {
                    _expandedNodePaths.Add(item.Path);
                    SetNodeFocusPath(kind, item.Path);
                }
            }

            if (searchMode)
                GUI.Label(
                    new Rect(labelRect.x, row.y + 19f, labelRect.width, 15f),
                    item.Breadcrumb,
                    NodeTechnicalStyle);
            if (badgeWidth > 0f)
            {
                var badgeRect = new Rect(row.xMax - badgeWidth, row.y + 4f, badgeWidth - 3f, 17f);
                GUI.Label(
                    badgeRect,
                    item.Source == NodeOutlineSource.Template
                        ? "模板"
                        : item.Source == NodeOutlineSource.TriggerPreview ? "触发" : "预览",
                    EditorStyles.centeredGreyMiniLabel);
            }

            if (openContextMenu)
            {
                SetSelectedNodePath(kind, item.Path);
                ShowOutlineNodeContextMenu(item, items);
                Event.current.Use();
            }
        }

        private string GetNodeOutlineTitle(NodeOutlineItem item)
        {
            var branchPrefix = item.IsBranchRoot
                ? item.Branch == NodeOutlineBranch.Else && TriggerAuthoringConditionalChain.IsConditional(item.Node)
                    ? "否则如果 · "
                    : GetBranchLabel(item.Branch) + " · "
                : string.Empty;
            if (!string.IsNullOrWhiteSpace(item.Node.GroupReference))
                return (item.Node.Enabled ? string.Empty : "[停用] ") + branchPrefix + "引用分组：" + item.Node.GroupReference;
            if (TriggerAuthoringTriggerReuse.TryGetReferencedTriggerId(item.Node, out var triggerId))
            {
                var target = TriggerAuthoringTriggerReuse.FindTrigger(_asset.Module, triggerId);
                return (item.Node.Enabled ? string.Empty : "[停用] ") + branchPrefix + "触发效果 #" + triggerId +
                       (target != null ? " · " + DisplayTriggerName(target) : " · 引用缺失");
            }
            var descriptor = ResolveNodeDescriptor(item.Kind, item.Node);
            var name = TriggerAuthoringEditorLabels.Node(
                item.Node.Type,
                descriptor != null ? descriptor.DisplayName : null);
            var childCount = item.Node.Children != null ? item.Node.Children.Count : 0;
            if (string.Equals(item.Node.Type, "conditional", StringComparison.OrdinalIgnoreCase))
                childCount += (item.Node.Condition != null ? 1 : 0) +
                              (item.Node.ElseChildren != null ? item.Node.ElseChildren.Count : 0);
            return (item.Node.Enabled ? string.Empty : "[停用] ") + branchPrefix + name +
                   (childCount > 0 ? "  (" + childCount + ")" : string.Empty);
        }

        private void DrawSelectedNodeDetails(
            TriggerDefinitionData trigger,
            List<NodeOutlineItem> items,
            bool readOnly,
            float height)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.ExpandWidth(true), GUILayout.Height(height));
            var item = FindOutlineItem(items, _selectedRuleNodePath);
            if (item == null)
            {
                DrawEmptyRuleAreaDetails(trigger, _selectedRuleNodeKind, readOnly);
                EditorGUILayout.EndVertical();
                return;
            }

            DrawSelectedNodeHeader(item);
            _nodeDetailScroll = EditorGUILayout.BeginScrollView(_nodeDetailScroll);
            if (item.IsReadOnly)
            {
                EditorGUILayout.HelpBox(
                    item.Source == NodeOutlineSource.Template
                        ? "这是模板解析后的真实逻辑。参数值由当前触发器的模板绑定提供，需在模板资产中修改结构。"
                        : item.Source == NodeOutlineSource.TriggerPreview
                            ? "这是被引用触发效果的真实逻辑。需定位目标触发器修改，或在引用节点上转为本地副本。"
                            : "这是可复用分组解析后的真实逻辑。需编辑分组定义，或转为本地副本后再修改。",
                    MessageType.Info);
            }

            var previousTemplatePreview = _activeTemplatePreview;
            _activeTemplatePreview = item.Source == NodeOutlineSource.Template
                ? ResolveBoundTemplate(trigger)
                : null;
            try
            {
                using (new EditorGUI.DisabledScope(item.IsReadOnly))
                {
                    if (!string.IsNullOrWhiteSpace(item.Node.GroupReference))
                        DrawGroupReferenceDetails(item);
                    else
                        DrawEditableNodeDetails(item, trigger);
                }
            }
            finally
            {
                _activeTemplatePreview = previousTemplatePreview;
            }
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawEmptyRuleAreaDetails(
            TriggerDefinitionData trigger,
            TriggerNodeKind kind,
            bool readOnly)
        {
            var isCondition = kind == TriggerNodeKind.Condition;
            var title = isCondition ? "入口条件" : "执行流程";
            var color = GetSemanticColor(isCondition ? TriggerSemanticArea.Condition : TriggerSemanticArea.Action);
            var rect = GUILayoutUtility.GetRect(0f, 31f, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(rect, Color.Lerp(color, Color.black, EditorGUIUtility.isProSkin ? 0.32f : 0.08f));
            GUI.Label(new Rect(rect.x + 11f, rect.y + 5f, rect.width - 18f, 20f), title, EditorStyles.boldLabel);
            GUILayout.Space(8f);

            if (readOnly)
            {
                EditorGUILayout.HelpBox("当前模板没有配置" + title + "。", MessageType.Info);
                return;
            }

            EditorGUILayout.HelpBox(
                isCondition
                    ? "未配置入口条件时，事件到达后会直接进入执行流程。"
                    : "当前触发器还没有可执行的行为。",
                MessageType.Info);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(isCondition ? "+ 添加入口条件" : "+ 添加行为根节点", GUILayout.Height(28f)))
                ShowNodeCreationMenu(kind, created => SetRootNode(trigger, kind, created), GUILayoutUtility.GetLastRect());
            using (new EditorGUI.DisabledScope(!TriggerAuthoringNodeClipboard.HasNode()))
            {
                if (GUILayout.Button("粘贴为根节点", GUILayout.Height(28f)))
                    PasteNodeAsRoot(trigger, kind, title);
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawSelectedNodeHeader(NodeOutlineItem item)
        {
            var color = GetSemanticColor(
                item.Kind == TriggerNodeKind.Condition ? TriggerSemanticArea.Condition : TriggerSemanticArea.Action);
            var rect = GUILayoutUtility.GetRect(0f, 48f, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(
                rect,
                EditorGUIUtility.isProSkin
                    ? Color.Lerp(new Color(0.16f, 0.16f, 0.16f), color, 0.2f)
                    : Color.Lerp(new Color(0.92f, 0.92f, 0.92f), color, 0.14f));
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 4f, rect.height), color);
            GUI.Label(new Rect(rect.x + 11f, rect.y + 5f, rect.width - 18f, 20f), GetNodeOutlineTitle(item), EditorStyles.boldLabel);
            GUI.Label(
                new Rect(rect.x + 11f, rect.y + 26f, rect.width - 18f, 16f),
                "类型：" + (string.IsNullOrWhiteSpace(item.Node.Type) ? "分组引用" : item.Node.Type),
                NodeTechnicalStyle);
        }

        private void DrawEditableNodeDetails(NodeOutlineItem item, TriggerDefinitionData trigger)
        {
            var descriptor = ResolveNodeDescriptor(item.Kind, item.Node);
            var isTriggerReference = TriggerAuthoringTriggerReuse.IsReference(item.Node);
            var children = item.Node.Children ?? (item.Node.Children = new List<TriggerNodeData>());
            var maxChildren = descriptor != null ? descriptor.MaxChildren : 0;
            var canAddChild = maxChildren != 0 && (maxChildren < 0 || children.Count < maxChildren);
            var canPasteChild = canAddChild && TriggerAuthoringNodeClipboard.HasNode();

            if (!item.IsReadOnly)
            {
                EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
                using (new EditorGUI.DisabledScope(!CanMoveOutlineItem(item, -1)))
                    if (GUILayout.Button(new GUIContent("▲", "上移节点"), EditorStyles.toolbarButton, GUILayout.Width(26f)))
                        MoveOutlineItem(item, -1);
                using (new EditorGUI.DisabledScope(!CanMoveOutlineItem(item, 1)))
                    if (GUILayout.Button(new GUIContent("▼", "下移节点"), EditorStyles.toolbarButton, GUILayout.Width(26f)))
                        MoveOutlineItem(item, 1);
                if (GUILayout.Button("复制", EditorStyles.toolbarButton, GUILayout.Width(42f)))
                    TriggerAuthoringNodeClipboard.Copy(item.Node, item.Kind);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button(new GUIContent("⋮", "更多节点操作"), EditorStyles.toolbarButton, GUILayout.Width(25f)))
                    ShowOutlineNodeContextMenu(item, null);
                if (GUILayout.Button(new GUIContent("×", "删除节点"), EditorStyles.toolbarButton, GUILayout.Width(25f)))
                    Edit("删除触发器节点", () => RemoveOutlineItem(item));
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
                using (new EditorGUI.DisabledScope(!canPasteChild))
                    if (GUILayout.Button("粘贴子节点", EditorStyles.toolbarButton, GUILayout.Width(72f)))
                        PasteNodeAsChild(children, item.Kind);
                using (new EditorGUI.DisabledScope(IsElseIfOutlineItem(item)))
                    if (GUILayout.Button(
                            new GUIContent("更改类型", IsElseIfOutlineItem(item) ? "否则如果分支必须保持为条件分支" : "更改节点类型"),
                            EditorStyles.toolbarButton,
                            GUILayout.Width(64f)))
                        ShowNodeTypeMenu(item.Kind, selected => Edit("更改触发器节点类型", () => ApplyDescriptor(item.Node, selected)), GUILayoutUtility.GetLastRect());
                if (isTriggerReference)
                {
                    if (GUILayout.Button("转本地副本", EditorStyles.toolbarButton, GUILayout.Width(76f)))
                        LocalizeTriggerReference(item.Node);
                    if (GUILayout.Button("定位目标", EditorStyles.toolbarButton, GUILayout.Width(60f)))
                        SelectReferencedTrigger(item.Node);
                }
                else if (item.Kind == TriggerNodeKind.Action)
                {
                    if (GUILayout.Button("提取触发效果", EditorStyles.toolbarButton, GUILayout.Width(84f)))
                        BeginExtractTrigger(item.Node, descriptor);
                }
                else if (GUILayout.Button("提取为分组", EditorStyles.toolbarButton, GUILayout.Width(72f)))
                {
                    BeginExtractNode(item.Node, item.Kind, descriptor);
                }
                if (GUILayout.Button("引用分组", EditorStyles.toolbarButton, GUILayout.Width(64f)))
                    ShowGroupMenu(item.Kind, groupId => Edit("引用触发器分组", () => ApplyGroupReference(item.Node, item.Kind, groupId)));
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
            }

            if (isTriggerReference)
            {
                DrawTriggerReferenceDetails(item.Node, trigger);
                return;
            }

            if (item.Kind == TriggerNodeKind.Action && descriptor != null && !descriptor.RuntimeSupported)
            {
                EditorGUILayout.HelpBox(
                    "此行为仅用于编辑器配置，当前项目尚未注册对应的 Runtime PlanAction，导出运行时计划时会报错。",
                    MessageType.Warning);
            }

            item.Node.Enabled = EditorGUILayout.Toggle("启用节点", item.Node.Enabled);
            item.Node.Note = EditorGUILayout.TextField("备注", item.Node.Note);
            DrawNodeArguments(item.Node, descriptor, trigger);

            if (item.Kind == TriggerNodeKind.Action &&
                string.Equals(item.Node.Type, "conditional", StringComparison.OrdinalIgnoreCase))
            {
                DrawConditionalActionDetails(item.Node);
                return;
            }

            if (maxChildren != 0)
            {
                DrawParameterSeparator();
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label(
                    item.Kind == TriggerNodeKind.Condition
                        ? "直属子条件（" + children.Count + "）"
                        : "直属执行步骤（" + children.Count + "）",
                    EditorStyles.miniBoldLabel);
                GUILayout.FlexibleSpace();
                using (new EditorGUI.DisabledScope(!canAddChild))
                {
                    if (GUILayout.Button(item.Kind == TriggerNodeKind.Condition ? "+ 添加条件" : "+ 添加行为", EditorStyles.miniButton, GUILayout.Width(82f)))
                        ShowNodeCreationMenu(item.Kind, children.Add, GUILayoutUtility.GetLastRect());
                }
                EditorGUILayout.EndHorizontal();
                if (children.Count > 0)
                    GUILayout.Label("子节点的顺序和层级请在左侧大纲中查看与调整。", EditorStyles.miniLabel);
            }
        }

        private void DrawConditionalActionDetails(TriggerNodeData node)
        {
            node.Children = node.Children ?? new List<TriggerNodeData>();
            node.ElseChildren = node.ElseChildren ?? new List<TriggerNodeData>();
            EditorGUILayout.HelpBox(
                "执行到此节点时计算判断条件。这里与触发器入口共用同一套条件节点和求值语义。",
                MessageType.Info);

            DrawConditionalPredicateControls(node, "判断条件");
            DrawActionBranchControls("如果成立时执行", node.Children);
            DrawElseIfBranchControls(node);
            DrawActionBranchControls(
                "否则执行",
                TriggerAuthoringConditionalChain.GetFallbackActions(node));
            GUILayout.Label("分支的完整层级、顺序和参数请在左侧大纲中查看与调整。", EditorStyles.miniLabel);
        }

        private void DrawConditionalPredicateControls(TriggerNodeData node, string label)
        {
            DrawParameterSeparator();
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(label, EditorStyles.miniBoldLabel);
            GUILayout.FlexibleSpace();
            if (node.Condition == null)
            {
                if (GUILayout.Button("+ 添加条件", EditorStyles.miniButton, GUILayout.Width(86f)))
                    ShowNodeCreationMenu(
                        TriggerNodeKind.Condition,
                        created => node.Condition = created,
                        GUILayoutUtility.GetLastRect());
            }
            else
            {
                if (GUILayout.Button("更换", EditorStyles.miniButtonLeft, GUILayout.Width(48f)))
                    ShowNodeCreationMenu(
                        TriggerNodeKind.Condition,
                        created => node.Condition = created,
                        GUILayoutUtility.GetLastRect());
                if (GUILayout.Button("移除", EditorStyles.miniButtonRight, GUILayout.Width(48f)))
                    Edit("移除行为内判断条件", () => node.Condition = null);
            }
            EditorGUILayout.EndHorizontal();
            GUILayout.Label(
                node.Condition == null ? "尚未配置判断条件" : DescribeEmbeddedNode(node.Condition, TriggerNodeKind.Condition),
                NodeTechnicalStyle);
        }

        private void DrawElseIfBranchControls(TriggerNodeData root)
        {
            var branches = new List<TriggerNodeData>();
            TriggerAuthoringConditionalChain.CollectElseIfBranches(root, branches);
            for (var i = 0; i < branches.Count; i++)
            {
                var branch = branches[i];
                branch.Children = branch.Children ?? new List<TriggerNodeData>();
                DrawParameterSeparator();
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label("否则如果 " + (i + 1), EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                var remove = GUILayout.Button("删除分支", EditorStyles.miniButton, GUILayout.Width(66f));
                EditorGUILayout.EndHorizontal();
                if (remove)
                {
                    Edit("删除否则如果分支", () => TriggerAuthoringConditionalChain.RemoveElseIf(root, branch));
                    return;
                }
                DrawConditionalPredicateControls(branch, "分支条件");
                DrawActionBranchControls("该分支成立时执行", branch.Children);
            }

            DrawParameterSeparator();
            if (GUILayout.Button("+ 添加否则如果", EditorStyles.miniButton, GUILayout.Width(112f)))
                Edit(
                    "添加否则如果分支",
                    () => TriggerAuthoringConditionalChain.AppendElseIf(root, CreateConditionalActionNode()));
        }

        private void DrawActionBranchControls(string label, List<TriggerNodeData> actions)
        {
            DrawParameterSeparator();
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(label + "（" + actions.Count + "）", EditorStyles.miniBoldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("+ 添加行为", EditorStyles.miniButton, GUILayout.Width(86f)))
                ShowNodeCreationMenu(TriggerNodeKind.Action, actions.Add, GUILayoutUtility.GetLastRect());
            EditorGUILayout.EndHorizontal();
        }

        private TriggerNodeData CreateConditionalActionNode()
        {
            _types.TryGet(TriggerNodeKind.Action, "conditional", out var descriptor);
            var node = CreateNode(descriptor);
            node.Kind = TriggerNodeKind.Action;
            node.Type = "conditional";
            node.Condition = node.Condition ?? CreateDefaultEmbeddedCondition();
            return node;
        }

        private string DescribeEmbeddedNode(TriggerNodeData node, TriggerNodeKind kind)
        {
            if (node == null) return "未配置";
            if (!string.IsNullOrWhiteSpace(node.GroupReference)) return "引用分组：" + node.GroupReference;
            var descriptor = ResolveNodeDescriptor(kind, node);
            return TriggerAuthoringEditorLabels.Node(node.Type, descriptor != null ? descriptor.DisplayName : null);
        }

        private void DrawGroupReferenceDetails(NodeOutlineItem item)
        {
            EditorGUILayout.LabelField("分组 ID", item.Node.GroupReference ?? string.Empty);
            item.Node.Enabled = EditorGUILayout.Toggle("启用节点", item.Node.Enabled);
            item.Node.Note = EditorGUILayout.TextField("备注", item.Node.Note);
            if (item.IsReadOnly) return;
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("选择其他分组", EditorStyles.miniButtonLeft))
                ShowGroupMenu(item.Kind, groupId => Edit("更换触发器分组", () => ApplyGroupReference(item.Node, item.Kind, groupId)));
            if (GUILayout.Button("转为本地副本", EditorStyles.miniButtonMid))
                LocalizeGroupReference(item.Node, item.Kind);
            if (GUILayout.Button("复制", EditorStyles.miniButtonRight))
                TriggerAuthoringNodeClipboard.Copy(item.Node, item.Kind);
            EditorGUILayout.EndHorizontal();
        }

        private void ShowOutlineNodeContextMenu(NodeOutlineItem item, List<NodeOutlineItem> items)
        {
            if (item.IsReadOnly)
            {
                var previewMenu = new GenericMenu();
                previewMenu.AddDisabledItem(new GUIContent(
                    item.Source == NodeOutlineSource.Template
                        ? "来源：模板真实逻辑"
                        : item.Source == NodeOutlineSource.TriggerPreview
                            ? "来源：触发效果真实逻辑"
                            : "来源：分组真实逻辑"));
                previewMenu.AddItem(new GUIContent("复制预览节点"), false, () => TriggerAuthoringNodeClipboard.Copy(item.Node, item.Kind));
                previewMenu.ShowAsContext();
                return;
            }

            if (IsElseIfOutlineItem(item))
            {
                var branchMenu = new GenericMenu();
                branchMenu.AddItem(
                    new GUIContent("复制否则如果分支"),
                    false,
                    () => TriggerAuthoringNodeClipboard.Copy(item.Node, item.Kind));
                branchMenu.AddSeparator(string.Empty);
                branchMenu.AddItem(
                    new GUIContent("删除否则如果分支"),
                    false,
                    () => Edit("删除否则如果分支", () => RemoveOutlineItem(item)));
                branchMenu.ShowAsContext();
                return;
            }

            var descriptor = ResolveNodeDescriptor(item.Kind, item.Node);
            var children = item.Node.Children ?? (item.Node.Children = new List<TriggerNodeData>());
            var maxChildren = descriptor != null ? descriptor.MaxChildren : 0;
            var canAddChild = maxChildren != 0 && (maxChildren < 0 || children.Count < maxChildren);
            Action<TriggerNodeData> replace = created => ReplaceOutlineItem(item, created);
            Action remove = () => RemoveOutlineItem(item);
            Action<TriggerNodeData> insertBefore = item.Owner == null
                ? null
                : (Action<TriggerNodeData>)(created => item.Owner.Insert(Mathf.Clamp(item.OwnerIndex, 0, item.Owner.Count), created));
            Action<TriggerNodeData> insertAfter = item.Owner == null
                ? null
                : (Action<TriggerNodeData>)(created => item.Owner.Insert(Mathf.Clamp(item.OwnerIndex + 1, 0, item.Owner.Count), created));
            ShowNodeContextMenu(
                item.Node,
                item.Kind,
                descriptor,
                children,
                canAddChild && TriggerAuthoringNodeClipboard.HasNode(),
                canAddChild,
                replace,
                remove,
                insertBefore,
                insertAfter,
                GetContextMenuActivator());
        }

        private void ReplaceOutlineItem(NodeOutlineItem item, TriggerNodeData replacement)
        {
            if (item.Owner != null && item.OwnerIndex >= 0 && item.OwnerIndex < item.Owner.Count)
                item.Owner[item.OwnerIndex] = replacement;
            else if (item.ConditionalOwner != null && item.Branch == NodeOutlineBranch.Predicate)
                item.ConditionalOwner.Condition = replacement;
            else if (item.IsRoot)
                SetRootNode(_asset.Module.Triggers[_selectedTriggerIndex], item.Kind, replacement);
            SetSelectedNodePath(item.WorkspaceKind, item.ParentPath);
        }

        private void RemoveOutlineItem(NodeOutlineItem item)
        {
            if (IsElseIfOutlineItem(item))
                TriggerAuthoringConditionalChain.RemoveElseIf(item.ConditionalOwner, item.Node);
            else if (item.Owner != null && item.OwnerIndex >= 0 && item.OwnerIndex < item.Owner.Count)
                item.Owner.RemoveAt(item.OwnerIndex);
            else if (item.ConditionalOwner != null && item.Branch == NodeOutlineBranch.Predicate)
                item.ConditionalOwner.Condition = null;
            else if (item.IsRoot)
                SetRootNode(_asset.Module.Triggers[_selectedTriggerIndex], item.Kind, null);
            SetSelectedNodePath(item.WorkspaceKind, item.ParentPath);
        }

        private static bool IsElseIfOutlineItem(NodeOutlineItem item)
        {
            if (item == null || !item.IsBranchRoot || item.Branch != NodeOutlineBranch.Else ||
                item.ConditionalOwner == null || !TriggerAuthoringConditionalChain.IsConditional(item.Node))
                return false;
            return TriggerAuthoringConditionalChain.TryGetElseIf(item.ConditionalOwner, out var branch) &&
                   ReferenceEquals(branch, item.Node);
        }

        private bool CanMoveOutlineItem(NodeOutlineItem item, int delta)
        {
            return item.Owner != null &&
                   item.OwnerIndex >= 0 &&
                   item.OwnerIndex + delta >= 0 &&
                   item.OwnerIndex + delta < item.Owner.Count;
        }

        private void MoveOutlineItem(NodeOutlineItem item, int delta)
        {
            if (!CanMoveOutlineItem(item, delta)) return;
            var target = item.OwnerIndex + delta;
            Edit("调整触发器节点顺序", () => SwapNodes(item.Owner, item.OwnerIndex, target));
            var suffix = "[" + item.OwnerIndex + "]";
            var targetSuffix = "[" + target + "]";
            if (item.Path.EndsWith(suffix, StringComparison.Ordinal))
                SetSelectedNodePath(item.WorkspaceKind, item.Path.Substring(0, item.Path.Length - suffix.Length) + targetSuffix);
        }

        private void DrawNodeOutlineSplitter(float availableWidth)
        {
            var rect = GUILayoutUtility.GetRect(
                TriggerAuthoringWorkspaceLayout.SplitterWidth,
                TriggerAuthoringWorkspaceLayout.SplitterWidth,
                GUILayout.Width(TriggerAuthoringWorkspaceLayout.SplitterWidth),
                GUILayout.ExpandHeight(true));
            EditorGUIUtility.AddCursorRect(rect, MouseCursor.ResizeHorizontal);
            if (Event.current.type == EventType.Repaint)
                EditorGUI.DrawRect(rect, new Color(0f, 0f, 0f, 0.22f));
            if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && rect.Contains(Event.current.mousePosition))
            {
                _draggingNodeOutlineSplitter = true;
                Event.current.Use();
            }
            else if (_draggingNodeOutlineSplitter && Event.current.type == EventType.MouseDrag)
            {
                _nodeOutlineWidth = TriggerAuthoringWorkspaceLayout.ClampNodeOutlineWidth(
                    _nodeOutlineWidth + Event.current.delta.x,
                    availableWidth);
                EditorPrefs.SetFloat(NodeOutlineWidthPreference, _nodeOutlineWidth);
                RequestRepaint();
                Event.current.Use();
            }
            else if (_draggingNodeOutlineSplitter && Event.current.rawType == EventType.MouseUp)
            {
                _draggingNodeOutlineSplitter = false;
                Event.current.Use();
            }
        }

        private static NodeOutlineItem FindOutlineItem(List<NodeOutlineItem> items, string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            for (var i = 0; i < items.Count; i++)
                if (string.Equals(items[i].Path, path, StringComparison.Ordinal)) return items[i];
            return null;
        }

        private static NodeOutlineItem FindClosestOutlineItem(List<NodeOutlineItem> items, string path)
        {
            NodeOutlineItem best = null;
            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (!IsPathAtOrBelow(path, item.Path)) continue;
                if (best == null || item.Path.Length > best.Path.Length) best = item;
            }
            return best;
        }

        private void ExpandNodeAncestors(NodeOutlineItem item, List<NodeOutlineItem> items)
        {
            var parentPath = item.ParentPath;
            while (!string.IsNullOrEmpty(parentPath))
            {
                _expandedNodePaths.Add(parentPath);
                var parent = FindOutlineItem(items, parentPath);
                parentPath = parent != null ? parent.ParentPath : null;
            }
        }

        private static bool IsPathAtOrBelow(string path, string parentPath)
        {
            return !string.IsNullOrEmpty(path) &&
                   !string.IsNullOrEmpty(parentPath) &&
                   (string.Equals(path, parentPath, StringComparison.Ordinal) ||
                    path.StartsWith(parentPath + ".", StringComparison.Ordinal));
        }

        private static bool ContainsIgnoreCase(string value, string filter)
        {
            return !string.IsNullOrEmpty(value) &&
                   value.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private string GetSelectedNodePath(TriggerNodeKind kind)
        {
            return kind == _selectedRuleNodeKind ? _selectedRuleNodePath : null;
        }

        private void SetSelectedNodePath(TriggerNodeKind kind, string path)
        {
            _selectedRuleNodeKind = kind;
            _selectedRuleNodePath = path;
        }

        private string GetNodeFocusPath(TriggerNodeKind kind)
        {
            return kind == _ruleFocusKind ? _ruleFocusPath : null;
        }

        private void SetNodeFocusPath(TriggerNodeKind kind, string path)
        {
            _ruleFocusKind = kind;
            _ruleFocusPath = path;
        }

        private void ResetNodeNavigation()
        {
            _selectedRuleNodeKind = TriggerNodeKind.Condition;
            _selectedRuleNodePath = null;
            _ruleFocusKind = TriggerNodeKind.Condition;
            _ruleFocusPath = null;
            _ruleTreeBranchesInitialized = false;
            _nodeSearch = string.Empty;
            _expandedNodePaths.Clear();
            _nodeOutlineScroll = Vector2.zero;
            _nodeDetailScroll = Vector2.zero;
        }

        private void DrawTriggerNodes(TriggerDefinitionData trigger)
        {
            GUILayout.Space(8f);
            var basePath = "module.triggers[" + _selectedTriggerIndex + "]";
            trigger.Condition = DrawRootNode(trigger.Condition, TriggerNodeKind.Condition, trigger, "触发条件", basePath + ".condition");
            GUILayout.Space(10f);
            trigger.Actions = DrawRootNode(trigger.Actions, TriggerNodeKind.Action, trigger, "执行行为", basePath + ".actions");
        }

        private TriggerNodeData DrawRootNode(
            TriggerNodeData node,
            TriggerNodeKind kind,
            TriggerDefinitionData trigger,
            string title,
            string nodePath)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            DrawSemanticSectionHeader(
                title,
                kind == TriggerNodeKind.Condition ? "满足以下条件时继续" : "条件通过后按顺序执行",
                kind == TriggerNodeKind.Condition ? TriggerSemanticArea.Condition : TriggerSemanticArea.Action);
            if (node == null)
            {
                GUILayout.Space(4f);
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button(kind == TriggerNodeKind.Condition ? "添加条件" : "添加行为"))
                    ShowNodeCreationMenu(kind, created => SetRootNode(trigger, kind, created), GUILayoutUtility.GetLastRect());
                using (new EditorGUI.DisabledScope(!TriggerAuthoringNodeClipboard.HasNode()))
                {
                    if (GUILayout.Button(kind == TriggerNodeKind.Condition ? "粘贴条件" : "粘贴行为", EditorStyles.miniButton))
                        PasteNodeAsRoot(trigger, kind, title);
                }
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                return null;
            }

            node = DrawNode(
                node,
                kind,
                trigger,
                0,
                true,
                nodePath,
                null,
                null,
                created => SetRootNode(trigger, kind, created),
                () => SetRootNode(trigger, kind, null));
            EditorGUILayout.EndVertical();
            return node;
        }

        private void PasteNodeAsRoot(TriggerDefinitionData trigger, TriggerNodeKind kind, string title)
        {
            if (!TriggerAuthoringNodeClipboard.TryPaste(kind, out var pasted))
            {
                EditorUtility.DisplayDialog("粘贴节点", "剪贴板中没有可用于“" + title + "”的匹配节点。", "确定");
                return;
            }
            Edit("粘贴触发器节点", () => SetRootNode(trigger, kind, pasted));
        }

        private TriggerNodeData DrawNode(
            TriggerNodeData node,
            TriggerNodeKind kind,
            TriggerDefinitionData trigger,
            int depth,
            bool root,
            string nodePath,
            Action onMoveUp,
            Action onMoveDown,
            Action<TriggerNodeData> replaceNode = null,
            Action removeNode = null,
            Action<TriggerNodeData> insertBefore = null,
            Action<TriggerNodeData> insertAfter = null)
        {
            if (node == null) return null;
            var oldBackground = GUI.backgroundColor;
            if (IsFocusedPath(nodePath)) GUI.backgroundColor = new Color(1f, 0.85f, 0.45f);
            EditorGUILayout.BeginVertical(depth == 0 ? GUIStyle.none : SirenixGUIStyles.BoxContainer);
            if (!string.IsNullOrWhiteSpace(node.GroupReference))
            {
                var groupNode = DrawGroupReferenceNode(node, kind, depth, nodePath, replaceNode, removeNode, insertBefore, insertAfter);
                GUI.backgroundColor = oldBackground;
                return groupNode;
            }

            var descriptor = ResolveNodeDescriptor(kind, node);
            var children = node.Children ?? (node.Children = new List<TriggerNodeData>());
            var maxChildren = descriptor != null ? descriptor.MaxChildren : 0;
            var canPasteChild = maxChildren != 0 &&
                                (maxChildren < 0 || children.Count < maxChildren) &&
                                TriggerAuthoringNodeClipboard.HasNode();
            var canAddChild = maxChildren != 0 && (maxChildren < 0 || children.Count < maxChildren);

            EditorGUILayout.BeginHorizontal(GUILayout.MinHeight(34f));
            DrawNodeAccent(kind);
            EditorGUILayout.BeginVertical(GUILayout.MinWidth(92f));
            var displayName = TriggerAuthoringEditorLabels.Node(
                node.Type,
                descriptor != null ? descriptor.DisplayName : null);
            GUILayout.Label((node.Enabled ? string.Empty : "[已停用] ") + displayName, EditorStyles.boldLabel);
            GUILayout.Label("类型: " + (node.Type ?? "未选择"), NodeTechnicalStyle);
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            var hasMoveButtons = onMoveUp != null || onMoveDown != null;
            if (onMoveUp != null && GUILayout.Button(new GUIContent("▲", "上移节点"), EditorStyles.miniButtonLeft, GUILayout.Width(22f)))
                onMoveUp();
            if (onMoveDown != null && GUILayout.Button(new GUIContent("▼", "下移节点"), onMoveUp != null ? EditorStyles.miniButtonMid : EditorStyles.miniButtonLeft, GUILayout.Width(22f)))
                onMoveDown();
            if (GUILayout.Button(new GUIContent("复制", "复制当前节点及其所有子节点"), hasMoveButtons ? EditorStyles.miniButtonMid : EditorStyles.miniButtonLeft, GUILayout.Width(40f)))
                TriggerAuthoringNodeClipboard.Copy(node, kind);
            using (new EditorGUI.DisabledScope(!canPasteChild))
            {
                if (GUILayout.Button(new GUIContent("粘贴", "将剪贴板节点粘贴为子节点"), EditorStyles.miniButtonMid, GUILayout.Width(40f)))
                    PasteNodeAsChild(children, kind);
            }
            if (GUILayout.Button(new GUIContent("类型", "更改节点类型"), EditorStyles.miniButtonMid, GUILayout.Width(40f)))
                ShowNodeTypeMenu(kind, descriptor => ApplyDescriptor(node, descriptor), GUILayoutUtility.GetLastRect());
            if (GUILayout.Button(new GUIContent("分组", "替换为可复用分组引用"), EditorStyles.miniButtonMid, GUILayout.Width(40f)))
                ShowGroupMenu(kind, groupId => ApplyGroupReference(node, kind, groupId));
            var showMore = GUILayout.Button(new GUIContent("⋮", "更多节点操作"), EditorStyles.miniButtonMid, GUILayout.Width(25f));
            var moreButtonRect = GUILayoutUtility.GetLastRect();
            var remove = GUILayout.Button(new GUIContent("×", "删除节点"), EditorStyles.miniButtonRight, GUILayout.Width(25f));
            EditorGUILayout.EndHorizontal();
            if (showMore)
            {
                ShowNodeContextMenu(
                    node,
                    kind,
                    descriptor,
                    children,
                    canPasteChild,
                    canAddChild,
                    replaceNode,
                    removeNode,
                    insertBefore,
                    insertAfter,
                    moreButtonRect);
            }
            if (remove)
            {
                EditorGUILayout.EndVertical();
                GUI.backgroundColor = oldBackground;
                return null;
            }

            if (!node.Enabled)
                EditorGUILayout.HelpBox("此节点已停用：数据仍会保留，但校验与运行时导出会忽略它。", MessageType.Info);
            DrawNodeArguments(node, descriptor, trigger);

            if (kind == TriggerNodeKind.Action &&
                string.Equals(node.Type, "conditional", StringComparison.OrdinalIgnoreCase))
            {
                DrawInlineConditionalAction(node, trigger, depth, nodePath);
            }
            else if (maxChildren != 0)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label(
                    kind == TriggerNodeKind.Condition
                        ? $"子条件（{children.Count}）"
                        : $"执行步骤（{children.Count}）",
                    EditorStyles.miniBoldLabel);
                GUILayout.FlexibleSpace();
                using (new EditorGUI.DisabledScope(maxChildren > 0 && children.Count >= maxChildren))
                {
                    if (GUILayout.Button(
                            kind == TriggerNodeKind.Condition ? "+ 添加条件" : "+ 添加行为",
                            EditorStyles.miniButton,
                            GUILayout.Width(78f)))
                        ShowNodeCreationMenu(kind, children.Add, GUILayoutUtility.GetLastRect());
                }
                EditorGUILayout.EndHorizontal();

                for (var i = 0; i < children.Count; i++)
                {
                    var index = i;
                    var childPath = string.IsNullOrEmpty(nodePath) ? null : nodePath + ".children[" + i + "]";
                    Action moveUp = index > 0
                        ? () => Edit("调整触发器节点顺序", () => SwapNodes(children, index, index - 1))
                        : null;
                    Action moveDown = index < children.Count - 1
                        ? () => Edit("调整触发器节点顺序", () => SwapNodes(children, index, index + 1))
                        : null;
                    var child = DrawNode(
                        children[i],
                        kind,
                        trigger,
                        depth + 1,
                        false,
                        childPath,
                        moveUp,
                        moveDown,
                        created => children[index] = created,
                        () => children.RemoveAt(index),
                        created => children.Insert(index, created),
                        created => children.Insert(index + 1, created));
                    if (child == null)
                    {
                        Edit("删除触发器节点", () => children.RemoveAt(i));
                        i--;
                    }
                    else
                    {
                        children[i] = child;
                    }
                }
            }
            EditorGUILayout.EndVertical();
            var nodeRect = GUILayoutUtility.GetLastRect();
            if (ShouldOpenContextMenu(nodeRect))
            {
                var activator = GetContextMenuActivator();
                ShowNodeContextMenu(
                    node,
                    kind,
                    descriptor,
                    children,
                    canPasteChild,
                    canAddChild,
                    replaceNode,
                    removeNode,
                    insertBefore,
                    insertAfter,
                    activator);
                Event.current.Use();
            }
            GUI.backgroundColor = oldBackground;
            return node;
        }

        private void DrawInlineConditionalAction(
            TriggerNodeData node,
            TriggerDefinitionData trigger,
            int depth,
            string nodePath)
        {
            node.Children = node.Children ?? new List<TriggerNodeData>();
            node.ElseChildren = node.ElseChildren ?? new List<TriggerNodeData>();
            EditorGUILayout.HelpBox(
                "执行到这里时计算判断条件，并只执行成立或不成立分支。",
                MessageType.Info);

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("判断条件", EditorStyles.miniBoldLabel);
            GUILayout.FlexibleSpace();
            if (node.Condition == null && GUILayout.Button("+ 添加条件", EditorStyles.miniButton, GUILayout.Width(82f)))
                ShowNodeCreationMenu(
                    TriggerNodeKind.Condition,
                    created => node.Condition = created,
                    GUILayoutUtility.GetLastRect());
            EditorGUILayout.EndHorizontal();
            if (node.Condition != null)
            {
                var conditionPath = string.IsNullOrEmpty(nodePath) ? null : nodePath + ".condition";
                node.Condition = DrawNode(
                    node.Condition,
                    TriggerNodeKind.Condition,
                    trigger,
                    depth + 1,
                    false,
                    conditionPath,
                    null,
                    null,
                    created => node.Condition = created,
                    () => node.Condition = null);
            }

            DrawInlineActionBranch("如果成立时执行", node.Children, trigger, depth, nodePath, ".children");
            DrawInlineElseIfBranches(node, trigger, depth, nodePath);

            var elseIfBranches = new List<TriggerNodeData>();
            TriggerAuthoringConditionalChain.CollectElseIfBranches(node, elseIfBranches);
            var fallbackOwnerPath = BuildElseIfOwnerPath(nodePath, elseIfBranches.Count);
            DrawInlineActionBranch(
                "否则执行",
                TriggerAuthoringConditionalChain.GetFallbackActions(node),
                trigger,
                depth,
                fallbackOwnerPath,
                ".elseChildren");
        }

        private void DrawInlineElseIfBranches(
            TriggerNodeData root,
            TriggerDefinitionData trigger,
            int depth,
            string nodePath)
        {
            var branches = new List<TriggerNodeData>();
            TriggerAuthoringConditionalChain.CollectElseIfBranches(root, branches);
            for (var i = 0; i < branches.Count; i++)
            {
                var branch = branches[i];
                branch.Children = branch.Children ?? new List<TriggerNodeData>();
                var branchPath = BuildElseIfOwnerPath(nodePath, i + 1);
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label("否则如果 " + (i + 1), EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                var remove = GUILayout.Button("删除分支", EditorStyles.miniButton, GUILayout.Width(66f));
                EditorGUILayout.EndHorizontal();
                if (remove)
                {
                    Edit("删除否则如果分支", () => TriggerAuthoringConditionalChain.RemoveElseIf(root, branch));
                    EditorGUILayout.EndVertical();
                    break;
                }

                if (branch.Condition == null && GUILayout.Button("+ 添加分支条件", EditorStyles.miniButton, GUILayout.Width(104f)))
                    ShowNodeCreationMenu(
                        TriggerNodeKind.Condition,
                        created => branch.Condition = created,
                        GUILayoutUtility.GetLastRect());
                if (branch.Condition != null)
                    branch.Condition = DrawNode(
                        branch.Condition,
                        TriggerNodeKind.Condition,
                        trigger,
                        depth + 1,
                        false,
                        string.IsNullOrEmpty(branchPath) ? null : branchPath + ".condition",
                        null,
                        null,
                        created => branch.Condition = created,
                        () => branch.Condition = null);
                DrawInlineActionBranch(
                    "该分支成立时执行",
                    branch.Children,
                    trigger,
                    depth,
                    branchPath,
                    ".children");
                EditorGUILayout.EndVertical();
            }

            if (GUILayout.Button("+ 添加否则如果", EditorStyles.miniButton, GUILayout.Width(112f)))
                Edit(
                    "添加否则如果分支",
                    () => TriggerAuthoringConditionalChain.AppendElseIf(root, CreateConditionalActionNode()));
        }

        private static string BuildElseIfOwnerPath(string rootPath, int branchDepth)
        {
            var path = rootPath;
            for (var i = 0; i < branchDepth; i++)
                path = string.IsNullOrEmpty(path) ? null : path + ".elseChildren[0]";
            return path;
        }

        private void DrawInlineActionBranch(
            string label,
            List<TriggerNodeData> actions,
            TriggerDefinitionData trigger,
            int depth,
            string nodePath,
            string pathSegment)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(label + "（" + actions.Count + "）", EditorStyles.miniBoldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("+ 添加行为", EditorStyles.miniButton, GUILayout.Width(82f)))
                ShowNodeCreationMenu(TriggerNodeKind.Action, actions.Add, GUILayoutUtility.GetLastRect());
            EditorGUILayout.EndHorizontal();

            for (var i = 0; i < actions.Count; i++)
            {
                var index = i;
                var childPath = string.IsNullOrEmpty(nodePath)
                    ? null
                    : nodePath + pathSegment + "[" + i + "]";
                Action moveUp = index > 0
                    ? () => Edit("调整分支行为顺序", () => SwapNodes(actions, index, index - 1))
                    : null;
                Action moveDown = index < actions.Count - 1
                    ? () => Edit("调整分支行为顺序", () => SwapNodes(actions, index, index + 1))
                    : null;
                var child = DrawNode(
                    actions[i],
                    TriggerNodeKind.Action,
                    trigger,
                    depth + 1,
                    false,
                    childPath,
                    moveUp,
                    moveDown,
                    created => actions[index] = created,
                    () => actions.RemoveAt(index),
                    created => actions.Insert(index, created),
                    created => actions.Insert(index + 1, created));
                if (child == null)
                {
                    Edit("删除分支行为", () => actions.RemoveAt(i));
                    i--;
                }
                else if (i < actions.Count)
                {
                    actions[i] = child;
                }
            }
        }

        private void PasteNodeAsChild(List<TriggerNodeData> children, TriggerNodeKind kind)
        {
            if (!TriggerAuthoringNodeClipboard.TryPaste(kind, out var pasted))
            {
                EditorUtility.DisplayDialog("粘贴节点", "剪贴板中没有匹配的节点。", "确定");
                return;
            }
            Edit("粘贴触发器节点", () => children.Add(pasted));
        }

        private static bool ShouldOpenContextMenu(Rect rect)
        {
            var current = Event.current;
            return current != null &&
                   current.type == EventType.ContextClick &&
                   rect.Contains(current.mousePosition);
        }

        private static Rect GetContextMenuActivator()
        {
            var mousePosition = Event.current != null ? Event.current.mousePosition : Vector2.zero;
            return new Rect(mousePosition.x, mousePosition.y, 1f, 1f);
        }

        private void ShowNodeContextMenu(
            TriggerNodeData node,
            TriggerNodeKind kind,
            TriggerTypeDescriptor descriptor,
            List<TriggerNodeData> children,
            bool canPasteChild,
            bool canAddChild,
            Action<TriggerNodeData> replaceNode,
            Action removeNode,
            Action<TriggerNodeData> insertBefore,
            Action<TriggerNodeData> insertAfter,
            Rect activator)
        {
            if (node == null) return;
            var menu = new GenericMenu();
            var context = new TriggerAuthoringNodeContextMenuContext
            {
                Menu = menu,
                Kind = kind,
                Node = node,
                Descriptor = descriptor,
                CanPasteChild = canPasteChild,
                CanAddChild = canAddChild,
                Copy = () => TriggerAuthoringNodeClipboard.Copy(node, kind),
                PasteChild = () => PasteNodeAsChild(children, kind),
                ChangeType = () => ShowNodeTypeMenu(kind, selected => ApplyDescriptor(node, selected), activator),
                SelectGroup = () => ShowGroupMenu(kind, groupId => ApplyGroupReference(node, kind, groupId)),
                ExtractGroup = string.IsNullOrWhiteSpace(node.GroupReference)
                    ? (Action)(() => BeginExtractNode(node, kind, descriptor))
                    : null,
                LocalizeGroup = string.IsNullOrWhiteSpace(node.GroupReference)
                    ? null
                    : (Action)(() => LocalizeGroupReference(node, kind)),
                ExtractTrigger = kind == TriggerNodeKind.Action &&
                                 string.IsNullOrWhiteSpace(node.GroupReference) &&
                                 !TriggerAuthoringTriggerReuse.IsReference(node)
                    ? (Action)(() => BeginExtractTrigger(node, descriptor))
                    : null,
                LocalizeTrigger = TriggerAuthoringTriggerReuse.IsReference(node)
                    ? (Action)(() => LocalizeTriggerReference(node))
                    : null,
                NavigateTrigger = TriggerAuthoringTriggerReuse.IsReference(node)
                    ? (Action)(() => SelectReferencedTrigger(node))
                    : null,
                ToggleEnabled = () => Edit(node.Enabled ? "停用触发器节点" : "启用触发器节点", () => node.Enabled = !node.Enabled),
                AddDebugLogChild = () => Edit("添加调试日志节点", () => children.Add(CreateDebugLogNode("debug"))),
                InsertDebugLogBefore = insertBefore == null
                    ? null
                    : (Action)(() => Edit("插入调试日志节点", () => insertBefore(CreateDebugLogNode("before")))),
                InsertDebugLogAfter = insertAfter == null
                    ? null
                    : (Action)(() => Edit("插入调试日志节点", () => insertAfter(CreateDebugLogNode("after")))),
                Remove = removeNode == null ? null : (Action)(() => Edit("删除触发器节点", removeNode))
            };

            for (var i = 0; i < _nodeContextMenuContributors.Count; i++)
                _nodeContextMenuContributors[i]?.Populate(context);
            menu.ShowAsContext();
        }

        private TriggerNodeData CreateDebugLogNode(string message)
        {
            var node = CreateNode(_types.TryGet(TriggerNodeKind.Action, "debug_log", out var debugLog) ? debugLog : null);
            node.Kind = TriggerNodeKind.Action;
            node.Type = "debug_log";
            var argument = FindArgument(node.Arguments, "message");
            if (argument == null)
            {
                argument = new TriggerArgumentData { Name = "message" };
                node.Arguments.Add(argument);
            }
            argument.Value = new TriggerValueRefData
            {
                Source = TriggerValueSource.Constant,
                Type = TriggerValueType.String,
                StringValue = message ?? string.Empty
            };
            return node;
        }

        private static void SwapNodes(List<TriggerNodeData> children, int first, int second)
        {
            var temporary = children[first];
            children[first] = children[second];
            children[second] = temporary;
        }

        private void DrawNodeArguments(
            TriggerNodeData node,
            TriggerTypeDescriptor descriptor,
            TriggerDefinitionData trigger)
        {
            var arguments = node.Arguments ?? (node.Arguments = new List<TriggerArgumentData>());
            if (descriptor == null)
            {
                EditorGUILayout.HelpBox("找不到此节点类型的编辑器描述，以下内容将按原始参数显示。", MessageType.Error);
                DrawRawArguments(arguments, trigger);
                return;
            }

            for (var i = 0; i < descriptor.Parameters.Count; i++)
            {
                var parameter = descriptor.Parameters[i];
                var argument = FindArgument(arguments, parameter.Name);
                if (argument == null)
                {
                    if (!parameter.Required)
                    {
                        DrawParameterSeparator();
                        EditorGUILayout.BeginHorizontal();
                        GUILayout.Label(
                            TriggerAuthoringEditorLabels.Parameter(parameter.Name) + "（可选）",
                            EditorStyles.miniLabel);
                        GUILayout.Label(parameter.Name, NodeTechnicalStyle);
                        GUILayout.FlexibleSpace();
                        if (GUILayout.Button("添加", EditorStyles.miniButton, GUILayout.Width(42f)))
                            Edit("添加触发器参数", () => arguments.Add(CreateArgument(parameter)));
                        EditorGUILayout.EndHorizontal();
                    }
                    continue;
                }

                DrawParameterSeparator();
                EditorGUILayout.BeginVertical();
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label(TriggerAuthoringEditorLabels.Parameter(parameter.Name), EditorStyles.miniBoldLabel);
                GUILayout.Label(parameter.Name, NodeTechnicalStyle);
                GUILayout.FlexibleSpace();
                if (!parameter.Required && GUILayout.Button(new GUIContent("×", "移除此可选参数"), EditorStyles.miniButton, GUILayout.Width(22f)))
                {
                    Edit("移除触发器参数", () => arguments.Remove(argument));
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndVertical();
                    continue;
                }
                EditorGUILayout.EndHorizontal();
                argument.Value = argument.Value ?? CreateValue(parameter.Type);
                DrawValueRef(argument.Value, parameter, trigger);
                DrawEffectiveTemplateBinding(argument.Value, trigger);
                EditorGUILayout.EndVertical();
            }

            for (var i = 0; i < arguments.Count; i++)
            {
                var argument = arguments[i];
                if (argument == null || HasParameter(descriptor, argument.Name)) continue;
                EditorGUILayout.HelpBox($"无法识别参数“{argument.Name}”，其原始数据会继续保留。", MessageType.Warning);
                DrawValueRef(argument.Value ?? (argument.Value = new TriggerValueRefData()), null, trigger);
                DrawEffectiveTemplateBinding(argument.Value, trigger);
            }
        }

        private void DrawEffectiveTemplateBinding(TriggerValueRefData value, TriggerDefinitionData trigger)
        {
            if (_activeTemplatePreview == null ||
                value == null ||
                value.Source != TriggerValueSource.LocalBlackboard ||
                string.IsNullOrWhiteSpace(value.Path)) return;

            if (!TriggerAuthoringLocalBlackboardPath.TryParse(value.Path, out var scope, out var localKey) ||
                scope == TriggerAuthoringLocalBlackboardScope.Module) return;
            TriggerAuthoringTemplateParameterData input = null;
            var templateInputs = _activeTemplatePreview.Parameters;
            if (templateInputs != null)
                for (var i = 0; i < templateInputs.Count; i++)
                    if (templateInputs[i] != null &&
                        string.Equals(templateInputs[i].LocalVariableKey, localKey, StringComparison.Ordinal))
                    {
                        input = templateInputs[i];
                        break;
                    }
            if (input == null) return;

            TriggerValueRefData effectiveValue = null;
            var bindings = trigger?.Template?.Bindings;
            if (bindings != null)
            {
                for (var i = 0; i < bindings.Count; i++)
                {
                    var binding = bindings[i];
                    if (binding != null && string.Equals(binding.Name, input.Name, StringComparison.Ordinal))
                    {
                        effectiveValue = binding.Value;
                        break;
                    }
                }
            }

            var sourceLabel = "当前实例绑定";
            if (effectiveValue == null)
            {
                var parameters = _activeTemplatePreview.Parameters;
                if (parameters != null)
                {
                    for (var i = 0; i < parameters.Count; i++)
                    {
                        var parameter = parameters[i];
                        if (parameter == null ||
                            !parameter.HasDefault ||
                            !string.Equals(parameter.Name, input.Name, StringComparison.Ordinal)) continue;
                        effectiveValue = parameter.DefaultValue;
                        sourceLabel = "模板默认值";
                        break;
                    }
                }
            }

            EditorGUILayout.LabelField(
                sourceLabel,
                effectiveValue != null ? SummarizeValue(effectiveValue) : "未绑定",
                EditorStyles.miniLabel);
        }

        private static void DrawParameterSeparator()
        {
            GUILayout.Space(3f);
            var rect = GUILayoutUtility.GetRect(0f, 1f, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(
                rect,
                EditorGUIUtility.isProSkin
                    ? new Color(1f, 1f, 1f, 0.09f)
                    : new Color(0f, 0f, 0f, 0.12f));
            GUILayout.Space(3f);
        }

        private void DrawRawArguments(List<TriggerArgumentData> arguments, TriggerDefinitionData trigger)
        {
            for (var i = 0; i < arguments.Count; i++)
            {
                var argument = arguments[i] ?? (arguments[i] = new TriggerArgumentData());
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();
                argument.Name = EditorGUILayout.TextField(argument.Name);
                var remove = GUILayout.Button("x", EditorStyles.miniButton, GUILayout.Width(22f));
                EditorGUILayout.EndHorizontal();
                DrawValueRef(argument.Value ?? (argument.Value = new TriggerValueRefData()), null, trigger);
                EditorGUILayout.EndVertical();
                if (remove)
                {
                    arguments.RemoveAt(i);
                    i--;
                }
            }
            if (GUILayout.Button(TriggerAuthoringEditorIntegration.T("add-raw-argument"), EditorStyles.miniButton))
                arguments.Add(new TriggerArgumentData());
        }

        private void DrawValueRef(
            TriggerValueRefData value,
            TriggerParameterDescriptor parameter,
            TriggerDefinitionData trigger)
        {
            TriggerAuthoringValueRefEditor.Draw(
                value,
                parameter,
                new TriggerAuthoringValueRefEditorContext
                {
                    Module = _asset != null ? _asset.Module : null,
                    Trigger = _activeTemplatePreview != null
                        ? TriggerAuthoringTemplateDefinition.Get(_activeTemplatePreview)
                        : trigger,
                    Events = _events,
                    GlobalBlackboard = _globalBlackboard,
                    ValueSources = _valueSources
                });
        }

        private static void DrawConstant(
            TriggerValueRefData value,
            TriggerValueType type,
            TriggerParameterDescriptor parameter)
        {
            switch (type)
            {
                case TriggerValueType.Integer:
                    if (parameter != null && parameter.Options.Count > 0)
                    {
                        DrawIntegerChoice(value, parameter.Options);
                        break;
                    }
                    value.IntegerValue = EditorGUILayout.LongField(TriggerAuthoringEditorIntegration.T("value"), value.IntegerValue);
                    break;
                case TriggerValueType.Entity:
                case TriggerValueType.ObjectId:
                    value.IntegerValue = EditorGUILayout.LongField(TriggerAuthoringEditorIntegration.T("value"), value.IntegerValue);
                    break;
                case TriggerValueType.Number:
                    value.NumberValue = EditorGUILayout.DoubleField(TriggerAuthoringEditorIntegration.T("value"), value.NumberValue);
                    break;
                case TriggerValueType.Boolean:
                    value.BooleanValue = EditorGUILayout.Toggle(TriggerAuthoringEditorIntegration.T("value"), value.BooleanValue);
                    break;
                case TriggerValueType.String:
                    value.StringValue = EditorGUILayout.TextField(TriggerAuthoringEditorIntegration.T("value"), value.StringValue);
                    break;
                case TriggerValueType.IntegerList:
                    var current = value.IntegerListValue != null ? string.Join(",", value.IntegerListValue) : string.Empty;
                    var next = EditorGUILayout.TextField(TriggerAuthoringEditorIntegration.T("values"), current);
                    if (!string.Equals(current, next, StringComparison.Ordinal))
                        value.IntegerListValue = ParseIntegerList(next);
                    break;
                case TriggerValueType.Vector3:
                    value.Vector3Value = value.Vector3Value ?? new TriggerVector3Data();
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Label(TriggerAuthoringEditorIntegration.T("value"), GUILayout.Width(EditorGUIUtility.labelWidth - 4f));
                    value.Vector3Value.X = EditorGUILayout.DoubleField(value.Vector3Value.X);
                    value.Vector3Value.Y = EditorGUILayout.DoubleField(value.Vector3Value.Y);
                    value.Vector3Value.Z = EditorGUILayout.DoubleField(value.Vector3Value.Z);
                    EditorGUILayout.EndHorizontal();
                    break;
                case TriggerValueType.Object:
                    DrawConstantObject(value);
                    break;
                default:
                    EditorGUILayout.HelpBox(TriggerAuthoringEditorIntegration.T("choose-value-type"), MessageType.Info);
                    break;
            }
        }

        private static void DrawConstantObject(TriggerValueRefData value)
        {
            value.Fields = value.Fields ?? new List<TriggerArgumentData>();
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(TriggerAuthoringEditorIntegration.T("fields"), EditorStyles.miniBoldLabel);
            for (var i = 0; i < value.Fields.Count; i++)
            {
                var index = i;
                var field = value.Fields[i] ?? (value.Fields[i] = new TriggerArgumentData());
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();
                field.Name = EditorGUILayout.TextField(field.Name);
                var remove = GUILayout.Button("x", EditorStyles.miniButton, GUILayout.Width(22f));
                EditorGUILayout.EndHorizontal();
                field.Value = field.Value ?? CreateValue(TriggerValueType.Number);
                var nextType = DrawValueTypePopup(TriggerAuthoringEditorIntegration.T("type"), field.Value.Type);
                if (nextType != field.Value.Type) field.Value = CreateValue(nextType);
                DrawConstant(field.Value, field.Value.Type, null);
                EditorGUILayout.EndVertical();
                if (remove)
                {
                    value.Fields.RemoveAt(index);
                    i--;
                }
            }

            if (GUILayout.Button(TriggerAuthoringEditorIntegration.T("add-field"), EditorStyles.miniButton))
                value.Fields.Add(new TriggerArgumentData
                {
                    Name = CreateUniqueObjectFieldName(value.Fields),
                    Value = CreateValue(TriggerValueType.Number)
                });
            EditorGUILayout.EndVertical();
        }

        private static string CreateUniqueObjectFieldName(IReadOnlyList<TriggerArgumentData> fields)
        {
            var suffix = 1;
            var name = "field";
            while (ContainsObjectFieldName(fields, name))
            {
                suffix++;
                name = "field" + suffix;
            }
            return name;
        }

        private static bool ContainsObjectFieldName(IReadOnlyList<TriggerArgumentData> fields, string name)
        {
            if (fields == null) return false;
            for (var i = 0; i < fields.Count; i++)
            {
                var field = fields[i];
                if (field != null && string.Equals(field.Name, name, StringComparison.Ordinal)) return true;
            }
            return false;
        }

        private static void DrawIntegerChoice(
            TriggerValueRefData value,
            IReadOnlyList<TriggerParameterOption> options)
        {
            var names = new List<string>(options.Count + 1);
            var selected = -1;
            for (var i = 0; i < options.Count; i++)
            {
                var option = options[i];
                names.Add(option.DisplayName + "  [" + option.Value + "]");
                if (option.Value == value.IntegerValue) selected = i;
            }
            if (selected < 0)
            {
                names.Add(value.IntegerValue + "  [当前不可用]");
                selected = names.Count - 1;
            }

            var next = EditorGUILayout.Popup(TriggerAuthoringEditorIntegration.T("value"), selected, names.ToArray());
            if (next != selected && next < options.Count)
                value.IntegerValue = options[next].Value;
        }

        private void DrawPayloadPath(TriggerValueRefData value, TriggerDefinitionData trigger, TriggerValueType expectedType)
        {
            var fields = new List<PathOption>();
            if (trigger != null && _events != null &&
                _events.TryResolve(trigger.Event, out var definition) && definition.PayloadFields != null)
            {
                for (var i = 0; i < definition.PayloadFields.Count; i++)
                {
                    var field = definition.PayloadFields[i];
                    if (field == null || !TypeMatches(expectedType, field.Type)) continue;
                    fields.Add(new PathOption(field.Path, field.Type));
                }
            }
            DrawPathPopup(value, fields, "事件参数字段");
        }

        private void DrawLocalBlackboardPath(
            TriggerValueRefData value,
            TriggerDefinitionData trigger,
            TriggerValueType expectedType,
            bool write)
        {
            var options = new List<PathOption>();
            AddLocalBlackboardOptions(options, _asset.Module.Blackboard, expectedType, write);
            if (trigger != null)
                AddLocalBlackboardOptions(options, trigger.Blackboard, expectedType, write);
            DrawPathPopup(value, options, "局部变量 Key");
        }

        private void DrawGlobalBlackboardPath(
            TriggerValueRefData value,
            TriggerValueType expectedType,
            bool write)
        {
            var options = new List<PathOption>();
            if (_globalBlackboard != null)
            {
                var keys = _globalBlackboard.Definitions;
                for (var i = 0; i < keys.Count; i++)
                {
                    var key = keys[i];
                    if (key == null || !TypeMatches(expectedType, key.Type)) continue;
                    if (write && !key.CanWrite || !write && !key.CanRead) continue;
                    options.Add(new PathOption(key.Key, key.Type));
                }
            }
            DrawPathPopup(value, options, "全局变量 Key");
        }

        private static void DrawPathPopup(TriggerValueRefData value, List<PathOption> options, string label)
        {
            var names = new List<string> { "<无>" };
            var selected = 0;
            for (var i = 0; i < options.Count; i++)
            {
                names.Add(options[i].Path + "  [" + TriggerAuthoringEditorLabels.ValueType(options[i].Type) + "]");
                if (string.Equals(options[i].Path, value.Path, StringComparison.Ordinal)) selected = i + 1;
            }
            if (selected == 0 && !string.IsNullOrWhiteSpace(value.Path))
            {
                names.Add(value.Path + "  [当前不可用]");
                selected = names.Count - 1;
            }

            var next = EditorGUILayout.Popup(label, selected, names.ToArray());
            if (next == 0)
            {
                value.Path = string.Empty;
                return;
            }
            if (next <= options.Count)
            {
                var option = options[next - 1];
                value.Path = option.Path;
                value.Type = option.Type;
            }
        }

        private void DrawBlackboard(
            List<TriggerBlackboardVariableData> variables,
            string undoName,
            TriggerAuthoringLocalBlackboardScope scope,
            IReadOnlyList<TriggerBlackboardVariableData> inheritedVariables)
        {
            if (variables == null) return;
            var duplicates = FindDuplicateLocalVarKeys(variables);
            var scopeLabel = scope == TriggerAuthoringLocalBlackboardScope.Module ? "模块" : "触发器";
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(scopeLabel + "作用域", EditorStyles.miniBoldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(new GUIContent("+", "添加局部变量"), EditorStyles.miniButton, GUILayout.Width(26f)))
                ShowLocalVarCreationMenu(variables, undoName, GUILayoutUtility.GetLastRect());
            EditorGUILayout.EndHorizontal();

            for (var i = 0; i < variables.Count; i++)
            {
                var index = i;
                var variable = variables[i] ?? (variables[i] = new TriggerBlackboardVariableData());
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label(scopeLabel, EditorStyles.miniLabel, GUILayout.Width(44f));
                variable.Key = EditorGUILayout.TextField(variable.Key);
                var nextType = DrawValueTypePopup(variable.Type, GUILayout.Width(100f));
                if (nextType != variable.Type)
                {
                    variable.Type = nextType;
                    variable.DefaultValue = CreateValue(nextType);
                }
                variable.ReadOnly = GUILayout.Toggle(variable.ReadOnly, "只读", GUILayout.Width(56f));
                var remove = GUILayout.Button("x", EditorStyles.miniButton, GUILayout.Width(22f));
                EditorGUILayout.EndHorizontal();
                variable.Description = EditorGUILayout.TextField(TriggerAuthoringEditorIntegration.T("description"), variable.Description);
                variable.DefaultValue = variable.DefaultValue ?? CreateValue(variable.Type);
                EditorGUILayout.LabelField(TriggerAuthoringEditorIntegration.T("default-value"), EditorStyles.miniBoldLabel);
                DrawConstant(variable.DefaultValue, variable.Type, null);
                if (string.IsNullOrWhiteSpace(variable.Key))
                {
                    EditorGUILayout.HelpBox(TriggerAuthoringEditorIntegration.T("local-var-key-required"), MessageType.Error);
                }
                else
                {
                    if (duplicates.Contains(variable.Key))
                        EditorGUILayout.HelpBox(TriggerAuthoringEditorIntegration.T("duplicate-local-var"), MessageType.Error);
                    if (ContainsLocalVarKey(inheritedVariables, variable.Key))
                        EditorGUILayout.HelpBox(TriggerAuthoringEditorIntegration.T("shadows-module-var"), MessageType.Info);
                    EditorGUILayout.SelectableLabel(
                        TriggerAuthoringLocalBlackboardPath.Format(scope, variable.Key),
                        EditorStyles.miniLabel,
                        GUILayout.Height(EditorGUIUtility.singleLineHeight));
                }
                EditorGUILayout.EndVertical();
                if (remove)
                {
                    Edit("删除" + undoName + "变量", () => variables.RemoveAt(index));
                    i--;
                }
            }
        }

        private void ShowLocalVarCreationMenu(
            List<TriggerBlackboardVariableData> variables,
            string undoName,
            Rect activator)
        {
            var menu = new GenericMenu();
            AddLocalVarType(menu, variables, undoName, TriggerValueType.Number);
            AddLocalVarType(menu, variables, undoName, TriggerValueType.Integer);
            AddLocalVarType(menu, variables, undoName, TriggerValueType.Boolean);
            AddLocalVarType(menu, variables, undoName, TriggerValueType.String);
            AddLocalVarType(menu, variables, undoName, TriggerValueType.Entity);
            AddLocalVarType(menu, variables, undoName, TriggerValueType.ObjectId);
            menu.DropDown(activator);
        }

        private void AddLocalVarType(
            GenericMenu menu,
            List<TriggerBlackboardVariableData> variables,
            string undoName,
            TriggerValueType type)
        {
            menu.AddItem(new GUIContent(TriggerAuthoringEditorLabels.ValueType(type)), false, () =>
                Edit("添加" + undoName + "变量", () => variables.Add(new TriggerBlackboardVariableData
                {
                    Key = CreateUniqueLocalVarKey(variables, type),
                    Type = type,
                    DefaultValue = CreateValue(type)
                })));
        }

        private static HashSet<string> FindDuplicateLocalVarKeys(IReadOnlyList<TriggerBlackboardVariableData> variables)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var duplicates = new HashSet<string>(StringComparer.Ordinal);
            if (variables == null) return duplicates;
            for (var i = 0; i < variables.Count; i++)
            {
                var key = variables[i]?.Key;
                if (string.IsNullOrWhiteSpace(key)) continue;
                if (!seen.Add(key)) duplicates.Add(key);
            }
            return duplicates;
        }

        private static bool ContainsLocalVarKey(
            IReadOnlyList<TriggerBlackboardVariableData> variables,
            string key)
        {
            if (variables == null || string.IsNullOrWhiteSpace(key)) return false;
            for (var i = 0; i < variables.Count; i++)
            {
                var variable = variables[i];
                if (variable != null && string.Equals(variable.Key, key, StringComparison.Ordinal)) return true;
            }
            return false;
        }

        private static string CreateUniqueLocalVarKey(
            IReadOnlyList<TriggerBlackboardVariableData> variables,
            TriggerValueType type)
        {
            var prefix = type == TriggerValueType.Integer ? "intValue" :
                type == TriggerValueType.Boolean ? "flag" :
                type == TriggerValueType.String ? "text" :
                type == TriggerValueType.Entity ? "entity" :
                type == TriggerValueType.ObjectId ? "objectId" :
                "number";
            var suffix = 1;
            var key = prefix;
            while (ContainsLocalVarKey(variables, key))
            {
                suffix++;
                key = prefix + suffix;
            }
            return key;
        }

        private void DrawGroups(TriggerAuthoringModuleData module)
        {
            module.ConditionGroups = module.ConditionGroups ?? new List<TriggerNodeGroupData>();
            module.ActionGroups = module.ActionGroups ?? new List<TriggerNodeGroupData>();
            _showConditionGroups = DrawGroupList(
                module.ConditionGroups,
                TriggerNodeKind.Condition,
                "条件分组",
                _showConditionGroups);
            _showActionGroups = DrawGroupList(
                module.ActionGroups,
                TriggerNodeKind.Action,
                "行为分组",
                _showActionGroups);
        }

        private bool DrawGroupList(
            List<TriggerNodeGroupData> groups,
            TriggerNodeKind kind,
            string title,
            bool expanded)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            expanded = EditorGUILayout.Foldout(expanded, $"{title} ({groups.Count})", true);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(new GUIContent("+", "添加可复用分组"), EditorStyles.miniButton, GUILayout.Width(26f)))
            {
                Edit("添加触发器分组", () => groups.Add(new TriggerNodeGroupData
                {
                    Id = CreateUniqueGroupId(groups, kind),
                    DisplayName = kind == TriggerNodeKind.Condition ? "新建条件分组" : "新建行为分组"
                }));
            }
            EditorGUILayout.EndHorizontal();

            if (expanded)
            {
                for (var i = 0; i < groups.Count; i++)
                {
                    var index = i;
                    var group = groups[i] ?? (groups[i] = new TriggerNodeGroupData());
                    var editorKey = ((int)kind) + ":" + index;
                    EditorGUILayout.BeginVertical(SirenixGUIStyles.BoxContainer);
                    EditorGUILayout.BeginHorizontal();
                    var isOpen = _expandedGroupEditors.Contains(editorKey);
                    var nextOpen = EditorGUILayout.Foldout(
                        isOpen,
                        string.IsNullOrWhiteSpace(group.DisplayName) ? group.Id ?? "<分组>" : group.DisplayName,
                        true);
                    if (nextOpen != isOpen)
                    {
                        if (nextOpen) _expandedGroupEditors.Add(editorKey);
                        else _expandedGroupEditors.Remove(editorKey);
                    }
                    GUILayout.FlexibleSpace();
                    using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(group.Id) || _asset.Project == null))
                    {
                        if (GUILayout.Button(new GUIContent("引用", "查找此分组的使用位置"), EditorStyles.miniButton, GUILayout.Width(42f)))
                            ShowReferences(
                                TriggerAuthoringReferenceFinder.FindGroupReferences(_asset.Project, group.Id),
                                "分组：" + group.Id);
                    }
                    var remove = GUILayout.Button("x", EditorStyles.miniButton, GUILayout.Width(22f));
                    EditorGUILayout.EndHorizontal();

                    if (nextOpen)
                    {
                        group.Id = EditorGUILayout.TextField(TriggerAuthoringEditorIntegration.T("id"), group.Id);
                        group.DisplayName = EditorGUILayout.TextField(TriggerAuthoringEditorIntegration.T("display-name"), group.DisplayName);
                        group.Description = EditorGUILayout.TextField(TriggerAuthoringEditorIntegration.T("description"), group.Description);
                        SirenixEditorGUI.BeginBox("根节点");
                        if (group.Root == null)
                        {
                            if (GUILayout.Button(TriggerAuthoringEditorIntegration.T("add-root")))
                                ShowNodeCreationMenu(kind, created => group.Root = created, GUILayoutUtility.GetLastRect());
                        }
                        else
                        {
                            var groupPath = "module." +
                                            (kind == TriggerNodeKind.Condition ? "conditionGroups" : "actionGroups") +
                                            "[" + i + "].root";
                            group.Root = DrawNode(
                                group.Root,
                                kind,
                                null,
                                0,
                                true,
                                groupPath,
                                null,
                                null,
                                created => group.Root = created,
                                () => group.Root = null);
                        }
                        SirenixEditorGUI.EndBox();
                    }
                    EditorGUILayout.EndVertical();

                    if (remove)
                    {
                        Edit("删除触发器分组", () => groups.RemoveAt(index));
                        i--;
                    }
                }
            }
            EditorGUILayout.EndVertical();
            return expanded;
        }

        private TriggerNodeData DrawGroupReferenceNode(
            TriggerNodeData node,
            TriggerNodeKind kind,
            int depth,
            string nodePath,
            Action<TriggerNodeData> replaceNode,
            Action removeNode,
            Action<TriggerNodeData> insertBefore,
            Action<TriggerNodeData> insertAfter)
        {
            var previewKey = ((int)kind) + ":" + depth + ":" + (node.GroupReference ?? string.Empty);
            EditorGUILayout.BeginHorizontal();
            DrawNodeAccent(kind);
            GUILayout.Label((node.Enabled ? string.Empty : "[已停用] ") + "可复用分组: " + node.GroupReference, EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(new GUIContent("选择", "选择其他可复用分组"), EditorStyles.miniButtonLeft, GUILayout.Width(42f)))
                ShowGroupMenu(kind, groupId => ApplyGroupReference(node, kind, groupId));
            if (GUILayout.Button(new GUIContent("转本地", "将分组的完整真实逻辑复制到当前节点"), EditorStyles.miniButtonMid, GUILayout.Width(52f)))
                LocalizeGroupReference(node, kind);
            var showMore = GUILayout.Button(new GUIContent("⋮", "更多分组操作"), EditorStyles.miniButtonMid, GUILayout.Width(25f));
            var moreButtonRect = GUILayoutUtility.GetLastRect();
            var remove = GUILayout.Button(new GUIContent("×", "删除节点"), EditorStyles.miniButtonRight, GUILayout.Width(25f));
            EditorGUILayout.EndHorizontal();
            if (showMore)
            {
                ShowNodeContextMenu(
                    node,
                    kind,
                    null,
                    null,
                    false,
                    false,
                    replaceNode,
                    removeNode,
                    insertBefore,
                    insertAfter,
                    moreButtonRect);
            }
            if (remove)
            {
                EditorGUILayout.EndVertical();
                return null;
            }

            if (!node.Enabled)
                EditorGUILayout.HelpBox("此分组引用已停用：数据仍会保留，但校验与运行时导出会忽略它。", MessageType.Info);

            var showPreview = _expandedGroupPreviews.Contains(previewKey);
            var nextPreview = EditorGUILayout.Foldout(showPreview, "展开预览", true);
            if (nextPreview != showPreview)
            {
                if (nextPreview) _expandedGroupPreviews.Add(previewKey);
                else _expandedGroupPreviews.Remove(previewKey);
            }
            if (nextPreview)
            {
                using (new EditorGUI.DisabledScope(true))
                {
                    if (TriggerAuthoringGroupResolver.TryExpand(
                            _asset.Module,
                            node,
                            kind,
                            out var expanded,
                            out var failure))
                    {
                        DrawPreviewNode(expanded, 0);
                    }
                    else
                    {
                        EditorGUILayout.HelpBox(
                            failure != null ? failure.Message : "无法解析此分组引用。",
                            MessageType.Error);
                    }
                }
            }
            EditorGUILayout.EndVertical();
            var nodeRect = GUILayoutUtility.GetLastRect();
            if (ShouldOpenContextMenu(nodeRect))
            {
                var activator = GetContextMenuActivator();
                ShowNodeContextMenu(
                    node,
                    kind,
                    null,
                    null,
                    false,
                    false,
                    replaceNode,
                    removeNode,
                    insertBefore,
                    insertAfter,
                    activator);
                Event.current.Use();
            }
            return node;
        }

        private static void DrawPreviewNode(TriggerNodeData node, int depth)
        {
            if (node == null) return;
            EditorGUILayout.BeginVertical(depth == 0 ? EditorStyles.helpBox : SirenixGUIStyles.BoxContainer);
            EditorGUILayout.LabelField(node.Type ?? "<未选择类型>", EditorStyles.miniBoldLabel);
            var arguments = node.Arguments;
            if (arguments != null)
            {
                for (var i = 0; i < arguments.Count; i++)
                {
                    var argument = arguments[i];
                    if (argument == null) continue;
                    EditorGUILayout.LabelField(argument.Name, SummarizeValue(argument.Value));
                }
            }
            var conditional = string.Equals(node.Type, "conditional", StringComparison.OrdinalIgnoreCase);
            if (conditional)
            {
                EditorGUILayout.LabelField("判断条件", EditorStyles.centeredGreyMiniLabel);
                DrawPreviewNode(node.Condition, depth + 1);
            }
            var children = node.Children;
            if (children != null)
            {
                if (conditional && children.Count > 0)
                    EditorGUILayout.LabelField("条件成立", EditorStyles.centeredGreyMiniLabel);
                for (var i = 0; i < children.Count; i++) DrawPreviewNode(children[i], depth + 1);
            }
            if (conditional)
            {
                var elseChildren = node.ElseChildren;
                if (elseChildren != null && elseChildren.Count > 0)
                {
                    EditorGUILayout.LabelField("条件不成立", EditorStyles.centeredGreyMiniLabel);
                    for (var i = 0; i < elseChildren.Count; i++) DrawPreviewNode(elseChildren[i], depth + 1);
                }
            }
            EditorGUILayout.EndVertical();
        }

        private static string SummarizeValue(TriggerValueRefData value)
        {
            if (value == null) return "<空>";
            if (value.Source != TriggerValueSource.Constant)
            {
                if (value.Source == TriggerValueSource.Expression) return "表达式：" + value.Expression;
                return GetSourceName(value.Source) + ": " + value.Path;
            }
            switch (value.Type)
            {
                case TriggerValueType.Integer:
                case TriggerValueType.Entity:
                case TriggerValueType.ObjectId: return value.IntegerValue.ToString();
                case TriggerValueType.Number: return value.NumberValue.ToString("G");
                case TriggerValueType.Boolean: return value.BooleanValue ? "是" : "否";
                case TriggerValueType.String: return value.StringValue ?? string.Empty;
                case TriggerValueType.IntegerList: return value.IntegerListValue != null
                    ? string.Join(",", value.IntegerListValue)
                    : string.Empty;
                case TriggerValueType.Vector3: return value.Vector3Value == null
                    ? "(0, 0, 0)"
                    : $"({value.Vector3Value.X:G}, {value.Vector3Value.Y:G}, {value.Vector3Value.Z:G})";
                default: return TriggerAuthoringEditorLabels.ValueType(value.Type);
            }
        }

        private void DrawDiagnostics()
        {
            _showDiagnostics = EditorGUILayout.Foldout(
                _showDiagnostics,
                $"诊断（{_platformDiagnostics.Items.Count}）",
                true);
            if (!_showDiagnostics) return;

            if (!string.IsNullOrEmpty(_focusedDiagnosticPath))
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.HelpBox(TriggerAuthoringEditorIntegration.F("focused-format", _focusedDiagnosticPath), MessageType.Info);
                if (GUILayout.Button(TriggerAuthoringEditorIntegration.T("clear"), EditorStyles.miniButton, GUILayout.Width(44f)))
                {
                    _focusedDiagnosticPath = null;
                    RequestRepaint();
                }
                EditorGUILayout.EndHorizontal();
            }
            if (_platformDiagnostics.Items.Count == 0)
            {
                EditorGUILayout.HelpBox(TriggerAuthoringEditorIntegration.T("no-diagnostics"), MessageType.Info);
                return;
            }

            _diagnosticScroll = EditorGUILayout.BeginScrollView(_diagnosticScroll, GUILayout.MaxHeight(190f));
            for (var i = 0; i < _platformDiagnostics.Items.Count; i++)
            {
                var diagnostic = _platformDiagnostics.Items[i];
                var icon = diagnostic.Severity == EditorDiagnosticSeverity.Error
                    ? EditorGUIUtility.IconContent("console.erroricon.sml")
                    : diagnostic.Severity == EditorDiagnosticSeverity.Warning
                        ? EditorGUIUtility.IconContent("console.warnicon.sml")
                        : EditorGUIUtility.IconContent("console.infoicon.sml");
                var content = new GUIContent(
                    $"{diagnostic.Code}  {diagnostic.Path}\n{diagnostic.Message}",
                    icon != null ? icon.image : null);
                if (GUILayout.Button(content, EditorStyles.helpBox, GUILayout.MinHeight(38f)))
                    diagnostic.Locate?.Invoke();
            }
            EditorGUILayout.EndScrollView();
        }

        private void ShowEventMenu(TriggerDefinitionData trigger)
        {
            var menu = new GenericMenu();
            if (_events == null || _events.Definitions.Count == 0)
            {
                menu.AddDisabledItem(new GUIContent("没有事件目录"));
            }
            else
            {
                var definitions = _events.Definitions;
                for (var i = 0; i < definitions.Count; i++)
                {
                    var definition = definitions[i];
                    if (definition == null) continue;
                    var family = definition.MatchMode == TriggerEventMatchMode.Prefix ? "事件族" : definition.Category;
                    var label = family + "/" + (string.IsNullOrWhiteSpace(definition.DisplayName) ? definition.Id : definition.DisplayName);
                    var captured = definition;
                    menu.AddItem(new GUIContent(label), string.Equals(trigger.Event, definition.Id, StringComparison.Ordinal), () =>
                        Edit("选择触发器事件", () => trigger.Event = captured.Id));
                }
            }
            menu.ShowAsContext();
        }

        private void ShowNodeTypeMenu(TriggerNodeKind kind, Action<TriggerTypeDescriptor> selected, Rect activator)
        {
            void OnType(TriggerTypeDescriptor descriptor) =>
                Edit("选择触发器节点类型", () => selected(descriptor));
            new TriggerNodeTypeBrowser(
                _nodeBrowserState,
                kind,
                OnType,
                catalog: _types).Show(activator);
        }

        private void ShowNodeCreationMenu(TriggerNodeKind kind, Action<TriggerNodeData> selected, Rect activator)
        {
            void OnType(TriggerTypeDescriptor descriptor) =>
                Edit("添加触发器节点", () => selected(CreateNode(descriptor)));
            void OnGroup(string groupId) =>
                Edit("添加触发器分组引用", () => selected(CreateGroupReference(kind, groupId)));
            new TriggerNodeTypeBrowser(
                _nodeBrowserState,
                kind,
                OnType,
                GetGroups(kind),
                OnGroup,
                _types).Show(activator);
        }

        private void ShowGroupMenu(TriggerNodeKind kind, Action<string> selected)
        {
            var menu = new GenericMenu();
            var groups = GetGroups(kind);
            if (groups == null || groups.Count == 0)
            {
                menu.AddDisabledItem(new GUIContent("没有可用分组"));
            }
            else
            {
                for (var i = 0; i < groups.Count; i++)
                {
                    var group = groups[i];
                    if (group == null || string.IsNullOrWhiteSpace(group.Id)) continue;
                    var groupId = group.Id;
                    var label = string.IsNullOrWhiteSpace(group.DisplayName) ? group.Id : group.DisplayName;
                    menu.AddItem(new GUIContent(label), false, () =>
                        Edit("选择触发器分组", () => selected(groupId)));
                }
            }
            menu.ShowAsContext();
        }

        private List<TriggerNodeGroupData> GetGroups(TriggerNodeKind kind)
        {
            if (_asset == null || _asset.Module == null) return null;
            return kind == TriggerNodeKind.Condition
                ? _asset.Module.ConditionGroups
                : _asset.Module.ActionGroups;
        }

        private void AddTrigger()
        {
            Edit("添加触发器", () =>
            {
                var triggers = _asset.Module.Triggers ?? (_asset.Module.Triggers = new List<TriggerDefinitionData>());
                triggers.Add(CreateTrigger());
                _selectedTriggerIndex = triggers.Count - 1;
            });
        }

        private void DuplicateSelectedTrigger()
        {
            var triggers = _asset.Module.Triggers;
            if (triggers == null || _selectedTriggerIndex < 0 || _selectedTriggerIndex >= triggers.Count) return;
            var source = triggers[_selectedTriggerIndex];
            Edit("复制触发器", () =>
            {
                var json = JsonUtility.ToJson(new TriggerCloneContainer { Trigger = source });
                var copy = JsonUtility.FromJson<TriggerCloneContainer>(json).Trigger;
                copy.Id = NextTriggerId();
                copy.Name = string.IsNullOrWhiteSpace(copy.Name) ? "副本" : copy.Name + " 副本";
                triggers.Insert(_selectedTriggerIndex + 1, copy);
                _selectedTriggerIndex++;
            });
        }

        private void DeleteSelectedTrigger()
        {
            DeleteTrigger(_selectedTriggerIndex);
        }

        private bool DeleteTrigger(int index)
        {
            var triggers = _asset.Module.Triggers;
            if (triggers == null || index < 0 || index >= triggers.Count) return false;
            var trigger = triggers[index];
            var name = trigger != null ? DisplayTriggerName(trigger) : "空规则";
            var references = trigger != null
                ? TriggerAuthoringTriggerIdRefactor.FindReferences(_asset, trigger.Id)
                : new List<TriggerAuthoringReference>();
            if (references.Count > 0)
            {
                if (EditorUtility.DisplayDialog(
                        "无法删除被引用的触发器",
                        "规则“" + name + "”仍有 " + references.Count +
                        " 处 TriggerId 引用。请先更换引用或转为本地副本。",
                        "查看引用",
                        "取消"))
                    ShowReferences(references, "TriggerId: " + trigger.Id);
                return false;
            }
            if (!EditorUtility.DisplayDialog(
                    "删除触发器",
                    "确定删除规则“" + name + "”吗？",
                    TriggerAuthoringEditorIntegration.T("delete"),
                    "取消")) return false;
            Edit("删除触发器", () =>
            {
                triggers.RemoveAt(index);
                if (_selectedTriggerIndex > index) _selectedTriggerIndex--;
                else if (_selectedTriggerIndex == index)
                    _selectedTriggerIndex = Mathf.Clamp(index, 0, triggers.Count - 1);
                ResetNodeNavigation();
            });
            RequestRepaint();
            return true;
        }

        private TriggerDefinitionData CreateTrigger()
        {
            return new TriggerDefinitionData
            {
                Id = NextTriggerId(),
                Name = "新建触发器",
                GroupPath = GetInheritedGroupPath(),
                Enabled = true,
                Actions = CreateNode(_types.TryGet(TriggerNodeKind.Action, "seq", out var seq) ? seq : null)
            };
        }

        private static string FormatTags(IReadOnlyList<string> tags)
        {
            return tags != null ? string.Join(", ", tags) : string.Empty;
        }

        private static TriggerNodeData CreateNode(TriggerTypeDescriptor descriptor)
        {
            var node = new TriggerNodeData
            {
                Kind = descriptor != null ? descriptor.Kind : TriggerNodeKind.Action,
                Type = descriptor != null ? descriptor.Type : string.Empty
            };
            if (descriptor == null) return node;
            AddDefaultArguments(node.Arguments, descriptor);
            if (descriptor.Kind == TriggerNodeKind.Action &&
                string.Equals(descriptor.Type, "conditional", StringComparison.OrdinalIgnoreCase))
                node.Condition = CreateDefaultEmbeddedCondition();
            return node;
        }

        private static TriggerNodeData CreateDefaultEmbeddedCondition()
        {
            return new TriggerNodeData
            {
                Kind = TriggerNodeKind.Condition,
                Type = "always_true"
            };
        }

        private static TriggerNodeData CreateGroupReference(TriggerNodeKind kind, string groupId)
        {
            return new TriggerNodeData
            {
                Kind = kind,
                GroupReference = groupId ?? string.Empty
            };
        }

        private static TriggerArgumentData CreateArgument(TriggerParameterDescriptor parameter)
        {
            return new TriggerArgumentData
            {
                Name = parameter.Name,
                Value = TriggerAuthoringValueRefEditor.CreateDefaultValue(parameter)
            };
        }

        private static TriggerValueRefData CreateValue(TriggerValueType type)
        {
            return TriggerAuthoringValueRefEditor.CreateDefaultValue(type);
        }

        private static void ApplyDescriptor(TriggerNodeData node, TriggerTypeDescriptor descriptor)
        {
            node.Kind = descriptor.Kind;
            node.GroupReference = string.Empty;
            node.Type = descriptor.Type;
            node.Arguments = new List<TriggerArgumentData>();
            node.Condition = descriptor.Kind == TriggerNodeKind.Action &&
                             string.Equals(descriptor.Type, "conditional", StringComparison.OrdinalIgnoreCase)
                ? CreateDefaultEmbeddedCondition()
                : null;
            node.Children = new List<TriggerNodeData>();
            node.ElseChildren = new List<TriggerNodeData>();
            AddDefaultArguments(node.Arguments, descriptor);
        }

        private static void ApplyGroupReference(
            TriggerNodeData node,
            TriggerNodeKind kind,
            string groupId)
        {
            node.Kind = kind;
            node.GroupReference = groupId ?? string.Empty;
            node.Type = string.Empty;
            node.Note = string.Empty;
            node.Arguments = new List<TriggerArgumentData>();
            node.Condition = null;
            node.Children = new List<TriggerNodeData>();
            node.ElseChildren = new List<TriggerNodeData>();
        }

        private void BeginExtractTrigger(TriggerNodeData node, TriggerTypeDescriptor descriptor)
        {
            var triggers = _asset?.Module?.Triggers;
            if (node == null || triggers == null ||
                _selectedTriggerIndex < 0 || _selectedTriggerIndex >= triggers.Count) return;
            var sourceTrigger = triggers[_selectedTriggerIndex];
            var nodeName = TriggerAuthoringEditorLabels.Node(
                node.Type,
                descriptor != null ? descriptor.DisplayName : null);
            var defaultName = string.IsNullOrWhiteSpace(nodeName)
                ? "可复用触发效果"
                : nodeName + "效果";
            TriggerAuthoringTextPrompt.Open(
                "提取为独立触发效果",
                "新触发效果名称",
                defaultName,
                displayName => ExtractNodeToTrigger(sourceTrigger, node, displayName));
        }

        private void ExtractNodeToTrigger(
            TriggerDefinitionData sourceTrigger,
            TriggerNodeData node,
            string displayName)
        {
            var newTriggerId = NextTriggerId();
            Undo.RecordObject(_asset, "提取独立触发效果");
            if (!TriggerAuthoringTriggerReuse.TryExtract(
                    _asset.Module,
                    sourceTrigger,
                    node,
                    newTriggerId,
                    displayName,
                    out var extractedTrigger,
                    out var error))
            {
                EditorUtility.DisplayDialog("无法提取触发效果", error ?? "提取独立触发效果失败。", "确定");
                return;
            }

            _selectedTriggerIndex = _asset.Module.Triggers.IndexOf(extractedTrigger);
            _selectedEditorTab = TriggerEditorTab.RuleTree;
            _triggerSearch.Clear();
            _triggerQuickFilter = TriggerAuthoringTriggerQuickFilter.All;
            _scrollToSelectedTrigger = true;
            ResetNodeNavigation();
            _selectedRuleNodeKind = TriggerNodeKind.Action;
            EditorUtility.SetDirty(_asset);
            RefreshDiagnostics();
            _nextSyncInspectionAt = 0d;
            _triggerGroupsInitialized = false;
            ExpandVisibleTriggerGroups();
            ShowNotification("已创建触发效果：" + extractedTrigger.Id);
            RequestRepaint();
        }

        private void DrawTriggerReferenceDetails(
            TriggerNodeData node,
            TriggerDefinitionData caller)
        {
            TriggerAuthoringTriggerReuse.TryGetReferencedTriggerId(node, out var triggerId);
            node.Enabled = EditorGUILayout.Toggle("启用节点", node.Enabled);
            EditorGUILayout.BeginHorizontal();
            var nextId = EditorGUILayout.IntField("触发器 ID", triggerId);
            if (nextId != triggerId && nextId > 0)
                SetTriggerReferenceId(node, nextId);
            if (GUILayout.Button("选择", GUILayout.Width(48f)))
                ShowTriggerReferenceMenu(node);
            EditorGUILayout.EndHorizontal();

            var target = TriggerAuthoringTriggerReuse.FindTrigger(_asset.Module, nextId > 0 ? nextId : triggerId);
            if (target == null)
            {
                EditorGUILayout.HelpBox("找不到引用的触发效果。", MessageType.Error);
                return;
            }

            var effectiveTarget = ResolveEffectiveTrigger(target);
            EditorGUILayout.LabelField("目标名称", DisplayTriggerName(effectiveTarget));
            EditorGUILayout.LabelField(
                "入口模式",
                effectiveTarget.EntryMode == TriggerEntryMode.Callable ? "仅供调用" : "事件触发并允许调用");
            DrawReferencedTemplateInputs(target);
            DrawCallableBindings(node, caller, effectiveTarget);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("定位目标", EditorStyles.miniButtonLeft))
                SelectTrigger(_asset.Module.Triggers.IndexOf(target));
            if (GUILayout.Button("转为本地副本", EditorStyles.miniButtonRight))
                LocalizeTriggerReference(node);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawCallableBindings(
            TriggerNodeData node,
            TriggerDefinitionData caller,
            TriggerDefinitionData target)
        {
            var parameters = target?.CallableParameters;
            if (parameters == null || parameters.Count == 0) return;
            var arguments = node.Arguments ?? (node.Arguments = new List<TriggerArgumentData>());
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("调用参数", EditorStyles.miniBoldLabel);
            for (var i = 0; i < parameters.Count; i++)
            {
                var callableParameter = parameters[i];
                if (callableParameter == null || string.IsNullOrWhiteSpace(callableParameter.Name)) continue;
                var descriptor = CreateCallableBindingDescriptor(callableParameter);
                var argument = FindArgument(arguments, callableParameter.Name);
                DrawParameterSeparator();
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label(
                    callableParameter.Name +
                    (callableParameter.Direction == TriggerCallableParameterDirection.Output ? "（输出）" : "（输入）"),
                    EditorStyles.miniBoldLabel);
                GUILayout.FlexibleSpace();
                if (argument == null)
                {
                    if (callableParameter.Direction == TriggerCallableParameterDirection.Input && callableParameter.HasDefault)
                        GUILayout.Label("默认值：" + SummarizeValue(callableParameter.DefaultValue), EditorStyles.miniLabel);
                    if (GUILayout.Button("添加绑定", EditorStyles.miniButton, GUILayout.Width(64f)))
                        arguments.Add(CreateArgument(descriptor));
                    EditorGUILayout.EndHorizontal();
                    continue;
                }
                if (!callableParameter.Required &&
                    GUILayout.Button(new GUIContent("×", "移除可选绑定"), EditorStyles.miniButton, GUILayout.Width(22f)))
                {
                    arguments.Remove(argument);
                    EditorGUILayout.EndHorizontal();
                    continue;
                }
                EditorGUILayout.EndHorizontal();
                argument.Value = argument.Value ?? CreateValue(callableParameter.Type);
                DrawValueRef(argument.Value, descriptor, caller);
                if (!string.IsNullOrWhiteSpace(callableParameter.Description))
                    GUILayout.Label(callableParameter.Description, EditorStyles.wordWrappedMiniLabel);
            }
            EditorGUILayout.EndVertical();
        }

        private static TriggerParameterDescriptor CreateCallableBindingDescriptor(
            TriggerCallableParameterData parameter)
        {
            var output = parameter.Direction == TriggerCallableParameterDirection.Output;
            return new TriggerParameterDescriptor(
                parameter.Name,
                parameter.Type,
                parameter.Required && !(parameter.HasDefault && !output),
                output
                    ? TriggerValueSourceMask.LocalBlackboard | TriggerValueSourceMask.GlobalBlackboard
                    : TriggerValueSourceMask.All,
                output ? TriggerParameterAccess.Output : TriggerParameterAccess.Read);
        }

        private void DrawReferencedTemplateInputs(TriggerDefinitionData target)
        {
            var reference = target?.Template;
            if (reference == null) return;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            if (_templates == null ||
                !_templates.TryGet(reference.TemplateId, out var templateAsset) ||
                templateAsset?.Template == null)
            {
                EditorGUILayout.HelpBox("目标触发效果引用的模板无法解析。", MessageType.Error);
                EditorGUILayout.EndVertical();
                return;
            }

            var template = templateAsset.Template;
            var parameters = template.Parameters ?? new List<TriggerAuthoringTemplateParameterData>();
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("模板调用输入", EditorStyles.miniBoldLabel);
            GUILayout.FlexibleSpace();
            GUILayout.Label(reference.TemplateId, EditorStyles.centeredGreyMiniLabel);
            EditorGUILayout.EndHorizontal();
            if (parameters.Count == 0)
            {
                GUILayout.Label("无需额外输入", EditorStyles.miniLabel);
                EditorGUILayout.EndVertical();
                return;
            }

            var bindings = reference.Bindings ?? new List<TriggerArgumentData>();
            for (var i = 0; i < parameters.Count; i++)
            {
                var parameter = parameters[i];
                if (parameter == null || string.IsNullOrWhiteSpace(parameter.Name)) continue;
                var binding = FindArgument(bindings, parameter.Name);
                var value = binding?.Value ?? (parameter.HasDefault ? parameter.DefaultValue : null);
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label(
                    parameter.Name + "  ·  " + TriggerAuthoringEditorLabels.ValueType(parameter.Type),
                    EditorStyles.miniLabel,
                    GUILayout.Width(150f));
                if (value == null)
                {
                    var oldColor = GUI.color;
                    GUI.color = new Color(1f, 0.48f, 0.44f);
                    GUILayout.Label(parameter.Required ? "缺少必填绑定" : "未绑定", EditorStyles.miniBoldLabel);
                    GUI.color = oldColor;
                }
                else
                {
                    GUILayout.Label(
                        (binding != null ? "读取 " : "默认值 ") + SummarizeValue(value),
                        EditorStyles.wordWrappedMiniLabel);
                }
                EditorGUILayout.EndHorizontal();
            }
            GUILayout.Label("局部黑板绑定会从当前触发执行上下文读取。", EditorStyles.centeredGreyMiniLabel);
            EditorGUILayout.EndVertical();
        }

        private void ShowTriggerReferenceMenu(TriggerNodeData node)
        {
            var menu = new GenericMenu();
            var triggers = _asset?.Module?.Triggers;
            if (triggers == null || triggers.Count == 0)
            {
                menu.AddDisabledItem(new GUIContent("没有可用触发器"));
            }
            else
            {
                TriggerAuthoringTriggerReuse.TryGetReferencedTriggerId(node, out var currentId);
                for (var i = 0; i < triggers.Count; i++)
                {
                    var trigger = triggers[i];
                    if (trigger == null || trigger.Id <= 0) continue;
                    var effectiveTrigger = ResolveEffectiveTrigger(trigger);
                    var capturedId = trigger.Id;
                    var prefix = effectiveTrigger.EntryMode == TriggerEntryMode.Callable ? "仅供调用/" : "事件触发/";
                    menu.AddItem(
                        new GUIContent(prefix + trigger.Id + "  " + DisplayTriggerName(effectiveTrigger)),
                        trigger.Id == currentId,
                        () => Edit("更换触发效果引用", () => SetTriggerReferenceId(node, capturedId)));
                }
            }
            menu.ShowAsContext();
        }

        private void SetTriggerReferenceId(TriggerNodeData node, int triggerId)
        {
            var argument = FindArgument(node.Arguments, TriggerAuthoringTriggerReuse.TriggerIdArgument);
            if (argument == null)
            {
                argument = new TriggerArgumentData { Name = TriggerAuthoringTriggerReuse.TriggerIdArgument };
                node.Arguments.Add(argument);
            }
            argument.Value = new TriggerValueRefData
            {
                Source = TriggerValueSource.Constant,
                Type = TriggerValueType.Integer,
                IntegerValue = triggerId
            };
            var target = TriggerAuthoringTriggerReuse.FindTrigger(_asset?.Module, triggerId);
            var parameters = ResolveEffectiveTrigger(target)?.CallableParameters;
            if (parameters == null) return;
            for (var i = 0; i < parameters.Count; i++)
            {
                var parameter = parameters[i];
                if (parameter == null || string.IsNullOrWhiteSpace(parameter.Name) ||
                    !parameter.Required ||
                    parameter.Direction == TriggerCallableParameterDirection.Input && parameter.HasDefault ||
                    FindArgument(node.Arguments, parameter.Name) != null)
                    continue;
                node.Arguments.Add(CreateArgument(CreateCallableBindingDescriptor(parameter)));
            }
        }

        private TriggerTypeDescriptor ResolveNodeDescriptor(TriggerNodeKind kind, TriggerNodeData node)
        {
            if (TriggerAuthoringTriggerReuse.TryGetReferencedTriggerId(node, out var targetId))
            {
                var target = TriggerAuthoringTriggerReuse.FindTrigger(_asset?.Module, targetId);
                if (target != null)
                    return TriggerAuthoringTriggerReuse.BuildCallDescriptor(ResolveEffectiveTrigger(target));
            }
            _types.TryGet(kind, node?.Type, out var descriptor);
            return descriptor;
        }

        private void SelectReferencedTrigger(TriggerNodeData node)
        {
            if (!TriggerAuthoringTriggerReuse.TryGetReferencedTriggerId(node, out var triggerId)) return;
            var target = TriggerAuthoringTriggerReuse.FindTrigger(_asset.Module, triggerId);
            if (target == null) return;
            SelectTrigger(_asset.Module.Triggers.IndexOf(target));
            _selectedEditorTab = TriggerEditorTab.RuleTree;
            _selectedRuleNodeKind = TriggerNodeKind.Action;
            RequestRepaint();
        }

        private void LocalizeTriggerReference(TriggerNodeData node)
        {
            Undo.RecordObject(_asset, "触发效果转为本地副本");
            if (!TriggerAuthoringTriggerReuse.TryLocalize(
                    _asset.Module,
                    node,
                    _templates,
                    out var target,
                    out var error))
            {
                EditorUtility.DisplayDialog("无法转为本地副本", error ?? "无法解析触发效果引用。", "确定");
                return;
            }

            EditorUtility.SetDirty(_asset);
            RefreshDiagnostics();
            _nextSyncInspectionAt = 0d;
            ShowNotification("已复制触发效果 " + target.Id + " 的真实逻辑");
            RequestRepaint();
        }

        private void BeginExtractNode(
            TriggerNodeData node,
            TriggerNodeKind kind,
            TriggerTypeDescriptor descriptor)
        {
            if (node == null || !string.IsNullOrWhiteSpace(node.GroupReference)) return;
            var defaultId = CreateUniqueGroupId(GetGroups(kind), kind);
            var nodeName = TriggerAuthoringEditorLabels.Node(
                node.Type,
                descriptor != null ? descriptor.DisplayName : null);
            var displayName = string.IsNullOrWhiteSpace(nodeName)
                ? (kind == TriggerNodeKind.Condition ? "提取的条件分组" : "提取的行为分组")
                : nodeName + "分组";
            TriggerAuthoringTextPrompt.Open(
                "提取为可复用分组",
                "分组 ID（其他节点将通过此 ID 引用）",
                defaultId,
                groupId => ExtractNodeToGroup(node, kind, groupId, displayName));
        }

        private void ExtractNodeToGroup(
            TriggerNodeData node,
            TriggerNodeKind kind,
            string groupId,
            string displayName)
        {
            Undo.RecordObject(_asset, "提取可复用触发器分组");
            if (!TriggerAuthoringGroupResolver.TryExtract(
                    _asset.Module,
                    node,
                    kind,
                    groupId,
                    displayName,
                    out var extractedGroup,
                    out var error))
            {
                EditorUtility.DisplayDialog("无法提取分组", error ?? "提取可复用分组失败。", "确定");
                return;
            }

            _showGroups = true;
            if (kind == TriggerNodeKind.Condition) _showConditionGroups = true;
            else _showActionGroups = true;
            var groups = GetGroups(kind);
            var groupIndex = groups != null ? groups.IndexOf(extractedGroup) : -1;
            if (groupIndex >= 0) _expandedGroupEditors.Add(((int)kind) + ":" + groupIndex);
            EditorUtility.SetDirty(_asset);
            RefreshDiagnostics();
            _nextSyncInspectionAt = 0d;
            ShowNotification("已提取可复用分组：" + extractedGroup.Id);
            RequestRepaint();
        }

        private void LocalizeGroupReference(TriggerNodeData node, TriggerNodeKind kind)
        {
            Undo.RecordObject(_asset, "转为本地副本");
            if (!TriggerAuthoringGroupResolver.TryLocalize(
                    _asset.Module,
                    node,
                    kind,
                    out var failure))
            {
                EditorUtility.DisplayDialog(
                    "无法转为本地副本",
                    failure != null ? failure.Message : "无法解析分组引用。",
                    "确定");
                return;
            }

            EditorUtility.SetDirty(_asset);
            RefreshDiagnostics();
            _nextSyncInspectionAt = 0d;
            ShowNotification("已转为本地副本");
            RequestRepaint();
        }

        private static string CreateUniqueGroupId(
            IReadOnlyList<TriggerNodeGroupData> groups,
            TriggerNodeKind kind)
        {
            var prefix = kind == TriggerNodeKind.Condition ? "condition_group" : "action_group";
            var suffix = 1;
            while (ContainsGroupId(groups, prefix + "_" + suffix)) suffix++;
            return prefix + "_" + suffix;
        }

        private static bool ContainsGroupId(IReadOnlyList<TriggerNodeGroupData> groups, string id)
        {
            if (groups == null) return false;
            for (var i = 0; i < groups.Count; i++)
            {
                var group = groups[i];
                if (group != null && string.Equals(group.Id, id, StringComparison.Ordinal)) return true;
            }
            return false;
        }

        private static void AddDefaultArguments(
            ICollection<TriggerArgumentData> arguments,
            TriggerTypeDescriptor descriptor)
        {
            var createdGroups = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < descriptor.Parameters.Count; i++)
            {
                var parameter = descriptor.Parameters[i];
                if (parameter.Required ||
                    !string.IsNullOrEmpty(parameter.RequiredGroup) && createdGroups.Add(parameter.RequiredGroup))
                {
                    arguments.Add(CreateArgument(parameter));
                }
            }
        }

        private static void SetRootNode(
            TriggerDefinitionData trigger,
            TriggerNodeKind kind,
            TriggerNodeData node)
        {
            if (kind == TriggerNodeKind.Condition) trigger.Condition = node;
            else trigger.Actions = node;
        }

        private void ExportSource()
        {
            var path = ResolveSourcePath();
            if (string.IsNullOrWhiteSpace(path))
            {
                var defaultName = !string.IsNullOrWhiteSpace(_asset.Module.ModuleId) ? _asset.Module.ModuleId : _asset.name;
                path = EditorUtility.SaveFilePanel(
                    "导出触发器 Source JSON", Application.dataPath, defaultName,
                    TriggerSourceCodecs.ModuleDefault.FileExtension);
                if (string.IsNullOrWhiteSpace(path)) return;
            }

            var result = TriggerAuthoringSourceSync.Export(_asset, path);
            if (!result.Success && result.CanForce && EditorUtility.DisplayDialog(
                    "触发器源文件冲突", result.Message + "\n\n是否覆盖 Source JSON？", "强制导出", "取消"))
                result = TriggerAuthoringSourceSync.Export(_asset, path, true);
            ShowSyncResult("导出", result);
        }

        private void ImportSource()
        {
            var path = ResolveSourcePath();
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                path = EditorUtility.OpenFilePanel(
                    "导入触发器 Source JSON", Application.dataPath,
                    TriggerSourceCodecs.ModuleDefault.FileExtension);
                if (string.IsNullOrWhiteSpace(path)) return;
            }

            var preview = TriggerAuthoringSourceSync.PreviewImport(_asset, path);
            if (!TriggerAuthoringSourceImportPreviewDialog.Confirm(preview)) return;

            var result = TriggerAuthoringSourceSync.Import(_asset, path, preview.RequiresForce);
            if (!result.Success && result.CanForce && EditorUtility.DisplayDialog(
                    "触发器资产冲突", result.Message + "\n\n是否覆盖资产内容？", "强制导入", "取消"))
                result = TriggerAuthoringSourceSync.Import(_asset, path, true);
            ShowSyncResult(TriggerAuthoringEditorIntegration.T("import"), result);
            if (result.Success)
            {
                EnsureSelection();
                RebuildCatalogs();
                RefreshDiagnostics();
            }
        }

        private void ExportRuntime()
        {
            var defaultName = _asset.Module != null && !string.IsNullOrWhiteSpace(_asset.Module.ModuleId)
                ? _asset.Module.ModuleId + ".runtime"
                : _asset.name + ".runtime";
            var path = EditorUtility.SaveFilePanel("导出 Runtime Plan JSON", Application.dataPath, defaultName, "json");
            if (string.IsNullOrWhiteSpace(path)) return;

            var result = TriggerAuthoringRuntimeExporter.Export(_asset, path);
            if (result.Success)
            {
                AssetDatabase.Refresh();
                ShowNotification("运行时导出成功");
                return;
            }

            _diagnostics = result.Diagnostics;
            EditorUtility.DisplayDialog("Runtime Plan 导出失败", result.BuildMessage(), "确定");
            RequestRepaint();
        }

        private void ShowSyncResult(string operation, TriggerAuthoringSyncResult result)
        {
            _nextSyncInspectionAt = 0d;
            if (result.Success)
            {
                AssetDatabase.SaveAssets();
                ShowNotification(operation + "成功");
                return;
            }
            EditorUtility.DisplayDialog("触发器源文件" + operation + "失败", result.Message, "确定");
        }

        private void ShowNotification(string message)
        {
            var window = EditorWindow.focusedWindow;
            if (window != null) window.ShowNotification(new GUIContent(message));
        }

        private void RefreshSyncInspectionIfNeeded()
        {
            if (_syncInspection != null && EditorApplication.timeSinceStartup < _nextSyncInspectionAt) return;
            _syncInspection = TriggerAuthoringSourceSync.Inspect(_asset);
            _nextSyncInspectionAt = EditorApplication.timeSinceStartup + 0.5d;
        }

        private void RebuildCatalogs()
        {
            var project = _asset != null ? _asset.Project : null;
            _types = TriggerTypeDescriptorCatalog.CreateForProject(project);
            _events = TriggerEventDescriptorCatalog.FromProject(project);
            _valueSources = TriggerAuthoringValueSourceCatalog.CreateForProject(project);
            _globalBlackboard = TriggerGlobalBlackboardDescriptorCatalog.FromAsset(
                project != null ? project.GlobalBlackboardCatalog : null);
            _templates = TriggerTemplateDescriptorCatalog.FromAsset(
                project != null ? project.TemplateCatalog : null);
        }

        private void RefreshDiagnostics()
        {
            if (_asset == null) return;
            _diagnostics = TriggerAuthoringValidator.Validate(
                _asset.Module,
                TriggerAuthoringValidationContext.Create(_asset));
            _platformDiagnostics = TriggerAuthoringDiagnosticAdapter.Adapt(
                _diagnostics,
                _asset,
                FocusDiagnostic);
            RequestRepaint();
        }

        private void FocusDiagnostic(string path)
        {
            _focusedDiagnosticPath = path;
            const string prefix = "module.triggers[";
            var start = path != null ? path.IndexOf(prefix, StringComparison.Ordinal) : -1;
            if (start < 0) return;
            start += prefix.Length;
            var end = path.IndexOf(']', start);
            if (end <= start) return;
            if (!int.TryParse(path.Substring(start, end - start), out var index)) return;
            if (_selectedTriggerIndex != index) ResetNodeNavigation();
            _selectedTriggerIndex = index;

            var sectionStart = end + 1;
            if (sectionStart >= path.Length || path[sectionStart] != '.') return;
            var rest = path.Substring(sectionStart + 1);
            var section = rest.Split('.', '[')[0];
            switch (section)
            {
                case "blackboard":
                    _selectedEditorTab = TriggerEditorTab.Settings;
                    _showTriggerBlackboard = true;
                    break;
                case "callableParameters":
                    _selectedEditorTab = TriggerEditorTab.Settings;
                    _showCallableParameters = true;
                    break;
                case "condition":
                    _selectedEditorTab = TriggerEditorTab.RuleTree;
                    SetSelectedNodePath(TriggerNodeKind.Condition, path);
                    break;
                case "actions":
                    _selectedEditorTab = TriggerEditorTab.RuleTree;
                    SetSelectedNodePath(TriggerNodeKind.Action, path);
                    break;
                case "template":
                    _selectedEditorTab = TriggerEditorTab.Settings;
                    break;
                case "name":
                case "enabled":
                case "event":
                    _selectedEditorTab = TriggerEditorTab.Overview;
                    break;
                default:
                    _selectedEditorTab = TriggerEditorTab.Settings;
                    _showAdvanced = true;
                    break;
            }
        }

        private bool IsFocusedPath(string nodePath)
        {
            if (string.IsNullOrEmpty(_focusedDiagnosticPath) || string.IsNullOrEmpty(nodePath)) return false;
            return string.Equals(_focusedDiagnosticPath, nodePath, StringComparison.Ordinal) ||
                   _focusedDiagnosticPath.StartsWith(nodePath + ".", StringComparison.Ordinal);
        }

        private void EnsureSelection()
        {
            var count = _asset != null && _asset.Module != null ? Count(_asset.Module.Triggers) : 0;
            _selectedTriggerIndex = count > 0 ? Mathf.Clamp(_selectedTriggerIndex, 0, count - 1) : -1;
        }

        private void PrepareUndoForInput()
        {
            var current = Event.current;
            if (current == null) return;
            if (current.type == EventType.MouseDown || current.type == EventType.KeyDown)
                Undo.RecordObject(_asset, "编辑触发器模块");
        }

        private void Edit(string undoName, Action action)
        {
            Undo.RecordObject(_asset, undoName);
            action();
            EditorUtility.SetDirty(_asset);
            RefreshDiagnostics();
            _nextSyncInspectionAt = 0d;
        }

        private void RequestRepaint()
        {
            RepaintRequested?.Invoke();
        }

        private static void ShowReferences(List<TriggerAuthoringReference> references, string title)
        {
            TriggerAuthoringReferenceWindow.Show(references, title);
        }

        private string ResolveSourcePath()
        {
            if (string.IsNullOrWhiteSpace(_asset.SourceJsonPath)) return string.Empty;
            if (Path.IsPathRooted(_asset.SourceJsonPath)) return Path.GetFullPath(_asset.SourceJsonPath);
            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            return Path.GetFullPath(Path.Combine(projectRoot, _asset.SourceJsonPath));
        }

        private int NextTriggerId()
        {
            return TriggerAuthoringTriggerIdRefactor.NextAvailableId(_asset);
        }

        private string GetInheritedGroupPath()
        {
            var triggers = _asset?.Module?.Triggers;
            if (triggers == null ||
                _selectedTriggerIndex < 0 ||
                _selectedTriggerIndex >= triggers.Count ||
                triggers[_selectedTriggerIndex] == null)
                return string.Empty;
            return TriggerAuthoringTriggerBatchOperations.NormalizeGroupPath(
                triggers[_selectedTriggerIndex].GroupPath);
        }

        private static TriggerAuthoringTriggerGroupMode LoadTriggerGroupMode(TriggerAuthoringModuleAsset asset)
        {
            var key = BuildTriggerGroupModePreferenceKey(asset);
            if (string.IsNullOrEmpty(key)) return TriggerAuthoringTriggerGroupMode.GroupPath;
            var value = EditorPrefs.GetInt(key, (int)TriggerAuthoringTriggerGroupMode.GroupPath);
            return Enum.IsDefined(typeof(TriggerAuthoringTriggerGroupMode), value)
                ? (TriggerAuthoringTriggerGroupMode)value
                : TriggerAuthoringTriggerGroupMode.GroupPath;
        }

        private static void SaveTriggerGroupMode(
            TriggerAuthoringModuleAsset asset,
            TriggerAuthoringTriggerGroupMode mode)
        {
            var key = BuildTriggerGroupModePreferenceKey(asset);
            if (!string.IsNullOrEmpty(key)) EditorPrefs.SetInt(key, (int)mode);
        }

        private static string BuildTriggerGroupModePreferenceKey(TriggerAuthoringModuleAsset asset)
        {
            if (asset == null) return string.Empty;
            var path = AssetDatabase.GetAssetPath(asset);
            var identity = string.IsNullOrWhiteSpace(path)
                ? asset.GetInstanceID().ToString()
                : AssetDatabase.AssetPathToGUID(path);
            return TriggerGroupModePreferencePrefix + identity;
        }

        private static TriggerArgumentData FindArgument(IReadOnlyList<TriggerArgumentData> arguments, string name)
        {
            if (arguments == null) return null;
            for (var i = 0; i < arguments.Count; i++)
            {
                var argument = arguments[i];
                if (argument != null && string.Equals(argument.Name, name, StringComparison.Ordinal)) return argument;
            }
            return null;
        }

        private static bool HasParameter(TriggerTypeDescriptor descriptor, string name)
        {
            for (var i = 0; i < descriptor.Parameters.Count; i++)
            {
                if (string.Equals(descriptor.Parameters[i].Name, name, StringComparison.Ordinal)) return true;
            }
            return false;
        }

        private static void AddLocalBlackboardOptions(
            ICollection<PathOption> output,
            IReadOnlyList<TriggerBlackboardVariableData> variables,
            TriggerValueType expectedType,
            bool write)
        {
            if (variables == null) return;
            for (var i = 0; i < variables.Count; i++)
            {
                var variable = variables[i];
                if (variable == null || string.IsNullOrWhiteSpace(variable.Key)) continue;
                if (write && variable.ReadOnly || !TypeMatches(expectedType, variable.Type)) continue;
                output.Add(new PathOption(variable.Key, variable.Type));
            }
        }

        private static List<TriggerValueSource> GetAllowedSources(TriggerValueSourceMask mask)
        {
            var result = new List<TriggerValueSource>();
            foreach (TriggerValueSource source in Enum.GetValues(typeof(TriggerValueSource)))
            {
                var sourceMask = (TriggerValueSourceMask)(1 << (int)source);
                if ((mask & sourceMask) != 0) result.Add(source);
            }
            if (result.Count == 0) result.Add(TriggerValueSource.Constant);
            return result;
        }

        private static string GetSourceName(TriggerValueSource source)
        {
            return TriggerAuthoringEditorLabels.Source(source);
        }

        private static bool TypeMatches(TriggerValueType expected, TriggerValueType actual)
        {
            return expected == TriggerValueType.None || expected == actual ||
                   expected == TriggerValueType.Number && actual == TriggerValueType.Integer;
        }

        private static List<long> ParseIntegerList(string value)
        {
            var result = new List<long>();
            if (string.IsNullOrWhiteSpace(value)) return result;
            var parts = value.Split(',');
            for (var i = 0; i < parts.Length; i++)
            {
                if (long.TryParse(parts[i].Trim(), out var parsed)) result.Add(parsed);
            }
            return result;
        }

        private static int Count<T>(IReadOnlyCollection<T> values)
        {
            return values != null ? values.Count : 0;
        }

        private static string DisplayTriggerName(TriggerDefinitionData trigger)
        {
            if (!string.IsNullOrWhiteSpace(trigger.Name)) return trigger.Name;
            if (trigger.EntryMode == TriggerEntryMode.Callable) return "可复用触发效果 " + trigger.Id;
            if (!string.IsNullOrWhiteSpace(trigger.Event)) return trigger.Event;
            return "未命名";
        }

        // 约定字符串字段的合法值与 TriggerAuthoringRuntimeExporter 的解析保持一致；
        // 仅 transient/none 当前可导出（TRG2011/TRG2012），下拉只暴露受支持值。
        private static readonly string[] PhaseOptions = { "immediate", "early", "late" };
        private static readonly string[] PhaseOptionNames = { "立即", "前置阶段", "后置阶段" };
        private static readonly string[] ScopeOptions = { "owner", "global" };
        private static readonly string[] ScopeOptionNames = { "触发器所有者", "全局" };
        private static readonly string[] ScheduleModeOptions = { "transient" };
        private static readonly string[] ScheduleModeOptionNames = { "临时调度" };
        private static readonly string[] InterruptPolicyOptions = { "none" };
        private static readonly string[] InterruptPolicyOptionNames = { "不中断" };
        private static readonly string[] ExecutionModeOptions = { "", "always", "once", "repeat", "cooldown" };
        private static readonly string[] ExecutionModeOptionNames = { "默认", "始终", "仅一次", "限定次数", "冷却" };
        private static readonly string[] TriggerGroupModeNames =
        {
            "平铺", "按事件", "按状态", "按作用域", "按阶段", "按业务分组", "按关键词"
        };
        private static readonly string[] TriggerQuickFilterNames =
        {
            "全部", "仅错误", "仅警告", "已停用", "未设置事件", "未分组", "无关键词"
        };
        private static readonly TriggerModuleKind[] ModuleKindOptions =
        {
            TriggerModuleKind.Ability,
            TriggerModuleKind.Buff,
            TriggerModuleKind.Passive,
            TriggerModuleKind.Projectile,
            TriggerModuleKind.Summon,
            TriggerModuleKind.Custom
        };
        private static readonly TriggerValueType[] ValueTypeOptions =
        {
            TriggerValueType.None,
            TriggerValueType.Integer,
            TriggerValueType.Number,
            TriggerValueType.Boolean,
            TriggerValueType.String,
            TriggerValueType.Entity,
            TriggerValueType.ObjectId,
            TriggerValueType.IntegerList,
            TriggerValueType.Vector3,
            TriggerValueType.Object
        };
        private static readonly TriggerValueType[] CallableValueTypeOptions =
        {
            TriggerValueType.Integer,
            TriggerValueType.Number,
            TriggerValueType.Boolean,
            TriggerValueType.String,
            TriggerValueType.Entity,
            TriggerValueType.ObjectId
        };

        private static TriggerModuleKind DrawModuleKindPopup(string label, TriggerModuleKind value)
        {
            var names = new string[ModuleKindOptions.Length];
            var selected = 0;
            for (var i = 0; i < ModuleKindOptions.Length; i++)
            {
                names[i] = TriggerAuthoringEditorLabels.ModuleKind(ModuleKindOptions[i]);
                if (ModuleKindOptions[i] == value) selected = i;
            }
            return ModuleKindOptions[EditorGUILayout.Popup(label, selected, names)];
        }

        private static TriggerValueType DrawValueTypePopup(TriggerValueType value, params GUILayoutOption[] options)
        {
            var names = new string[ValueTypeOptions.Length];
            var selected = 0;
            for (var i = 0; i < ValueTypeOptions.Length; i++)
            {
                names[i] = TriggerAuthoringEditorLabels.ValueType(ValueTypeOptions[i]);
                if (ValueTypeOptions[i] == value) selected = i;
            }
            return ValueTypeOptions[EditorGUILayout.Popup(selected, names, options)];
        }

        private static TriggerValueType DrawValueTypePopup(string label, TriggerValueType value)
        {
            var names = new string[ValueTypeOptions.Length];
            var selected = 0;
            for (var i = 0; i < ValueTypeOptions.Length; i++)
            {
                names[i] = TriggerAuthoringEditorLabels.ValueType(ValueTypeOptions[i]);
                if (ValueTypeOptions[i] == value) selected = i;
            }
            return ValueTypeOptions[EditorGUILayout.Popup(label, selected, names)];
        }

        private static TriggerValueType DrawCallableValueTypePopup(string label, TriggerValueType value)
        {
            var names = new string[CallableValueTypeOptions.Length];
            var selected = 0;
            for (var i = 0; i < CallableValueTypeOptions.Length; i++)
            {
                names[i] = TriggerAuthoringEditorLabels.ValueType(CallableValueTypeOptions[i]);
                if (CallableValueTypeOptions[i] == value) selected = i;
            }
            return CallableValueTypeOptions[EditorGUILayout.Popup(label, selected, names)];
        }

        private static string GetSyncStateLabel(TriggerAuthoringSyncState state)
        {
            switch (state)
            {
                case TriggerAuthoringSyncState.Untracked: return "未跟踪";
                case TriggerAuthoringSyncState.InSync: return "已同步";
                case TriggerAuthoringSyncState.AssetChanged: return "资产已修改";
                case TriggerAuthoringSyncState.JsonChanged: return "源文件已修改";
                case TriggerAuthoringSyncState.Conflict: return "存在冲突";
                case TriggerAuthoringSyncState.SourceMissing: return "源文件缺失";
                case TriggerAuthoringSyncState.InvalidSource: return "源文件无效";
                default: return "未知";
            }
        }

        private static string DrawConstrainedOption(
            string label,
            string value,
            string[] options,
            string[] displayNames = null)
        {
            var blank = string.IsNullOrWhiteSpace(value);
            var names = new List<string>(options.Length + 1);
            var selected = -1;
            for (var i = 0; i < options.Length; i++)
            {
                var displayName = displayNames != null && i < displayNames.Length
                    ? displayNames[i]
                    : options[i];
                names.Add(displayName + "  [" + options[i] + "]");
                if (!blank && string.Equals(options[i], value, StringComparison.OrdinalIgnoreCase)) selected = i;
            }
            if (blank) selected = 0;
            if (selected < 0)
            {
                names.Add(value + "  [当前不可用]");
                selected = names.Count - 1;
            }

            var next = EditorGUILayout.Popup(label, selected, names.ToArray());
            if (next == selected) return value;
            return next < options.Length ? options[next] : value;
        }

        private void AssignProject(TriggerAuthoringProjectAsset previous, TriggerAuthoringProjectAsset next)
        {
            if (previous != null) Undo.RecordObject(previous, "分配触发器项目");
            if (next != null) Undo.RecordObject(next, "分配触发器项目");
            TriggerAuthoringProjectMembership.Assign(_asset, next);
            if (previous != null) EditorUtility.SetDirty(previous);
            if (next != null) EditorUtility.SetDirty(next);
        }

        private void CreateAndAssignProject()
        {
            var path = EditorUtility.SaveFilePanelInProject(
                "创建触发器项目",
                "TriggerAuthoringProject",
                "asset",
                "请选择项目及其目录资产的创建位置。");
            if (string.IsNullOrWhiteSpace(path)) return;

            var directory = Path.GetDirectoryName(path)?.Replace('\\', '/') ?? "Assets";
            var baseName = Path.GetFileNameWithoutExtension(path);
            var project = TriggerAuthoringProjectSetup.CreateProjectWithCatalogs(directory, baseName);
            if (project == null) return;

            var previous = _asset.Project;
            Edit("分配触发器项目", () => AssignProject(previous, project));
            RebuildCatalogs();
        }

        private static Color GetSyncColor(TriggerAuthoringSyncState state)
        {
            switch (state)
            {
                case TriggerAuthoringSyncState.InSync: return new Color(0.55f, 0.9f, 0.62f);
                case TriggerAuthoringSyncState.AssetChanged:
                case TriggerAuthoringSyncState.JsonChanged: return new Color(1f, 0.82f, 0.38f);
                case TriggerAuthoringSyncState.Conflict:
                case TriggerAuthoringSyncState.InvalidSource: return new Color(1f, 0.48f, 0.44f);
                default: return Color.white;
            }
        }

        [Serializable]
        private sealed class TriggerCloneContainer
        {
            public TriggerDefinitionData Trigger;
        }

        private readonly struct PathOption
        {
            public PathOption(string path, TriggerValueType type)
            {
                Path = path;
                Type = type;
            }

            public string Path { get; }
            public TriggerValueType Type { get; }
        }
    }

    internal sealed class TriggerAuthoringRuntimePlanPreviewWindow : EditorWindow
    {
        [SerializeField]
        private TriggerAuthoringModuleAsset _asset;

        private Vector2 _scroll;
        private string _json = string.Empty;
        private string _message = string.Empty;
        private List<TriggerAuthoringDiagnostic> _diagnostics = new List<TriggerAuthoringDiagnostic>();
        private int _exportedTriggerCount;
        private int _skippedTriggerCount;
        private bool _success;

        internal static void Open(TriggerAuthoringModuleAsset asset)
        {
            var window = GetWindow<TriggerAuthoringRuntimePlanPreviewWindow>(false, "Runtime Plan 预览", true);
            window.minSize = new Vector2(620f, 420f);
            window._asset = asset;
            window.Compile();
            window.Show();
        }

        private void OnEnable()
        {
            if (_asset != null) Compile();
        }

        private void OnGUI()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label(_asset != null ? DisplayModuleName(_asset) : "未选择模块", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            using (new EditorGUI.DisabledScope(!_success || string.IsNullOrEmpty(_json)))
            {
                if (GUILayout.Button("复制 JSON", EditorStyles.toolbarButton, GUILayout.Width(72f)))
                    EditorGUIUtility.systemCopyBuffer = _json;
            }
            using (new EditorGUI.DisabledScope(_asset == null))
            {
                if (GUILayout.Button("重新编译", EditorStyles.toolbarButton, GUILayout.Width(72f))) Compile();
            }
            EditorGUILayout.EndHorizontal();

            if (_asset == null)
            {
                EditorGUILayout.HelpBox("未选择触发器模块。", MessageType.Info);
                return;
            }

            if (_success)
            {
                EditorGUILayout.HelpBox(
                    "编译成功：" + _exportedTriggerCount + " 个触发器，跳过 " + _skippedTriggerCount + " 个已停用触发器。",
                    MessageType.Info);
                _scroll = EditorGUILayout.BeginScrollView(_scroll);
                var style = new GUIStyle(EditorStyles.textArea) { wordWrap = false };
                EditorGUILayout.TextArea(_json, style, GUILayout.ExpandHeight(true));
                EditorGUILayout.EndScrollView();
                return;
            }

            EditorGUILayout.HelpBox(string.IsNullOrWhiteSpace(_message) ? "Runtime Plan 编译失败。" : _message, MessageType.Error);
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            for (var i = 0; i < _diagnostics.Count; i++)
            {
                var diagnostic = _diagnostics[i];
                if (diagnostic == null) continue;
                var messageType = diagnostic.Severity == TriggerAuthoringDiagnosticSeverity.Error
                    ? MessageType.Error
                    : diagnostic.Severity == TriggerAuthoringDiagnosticSeverity.Warning
                        ? MessageType.Warning
                        : MessageType.Info;
                EditorGUILayout.HelpBox(
                    diagnostic.Code + "  " + diagnostic.Path + "\n" + diagnostic.Message,
                    messageType);
            }
            EditorGUILayout.EndScrollView();
        }

        private void Compile()
        {
            _json = string.Empty;
            _message = string.Empty;
            _diagnostics.Clear();
            _success = false;
            _exportedTriggerCount = 0;
            _skippedTriggerCount = 0;
            if (_asset == null) return;

            var result = TriggerAuthoringRuntimeExporter.Build(_asset);
            _success = result.Success;
            _exportedTriggerCount = result.ExportedTriggerCount;
            _skippedTriggerCount = result.SkippedDisabledCount;
            _diagnostics = result.Diagnostics ?? new List<TriggerAuthoringDiagnostic>();
            _message = result.BuildMessage();
            if (_success) _json = TriggerAuthoringRuntimeExporter.Serialize(result.Database);
            Repaint();
        }

        private static string DisplayModuleName(TriggerAuthoringModuleAsset asset)
        {
            var moduleId = asset?.Module?.ModuleId;
            return string.IsNullOrWhiteSpace(moduleId) ? asset?.name ?? "未命名模块" : moduleId;
        }
    }

    /// <summary>Inspector 宿主：把绘制委托给共享的 TriggerAuthoringModuleDrawer（工作台窗口持有同一个类）。</summary>
    [CustomEditor(typeof(TriggerAuthoringModuleAsset))]
    internal sealed class TriggerAuthoringModuleAssetEditor : OdinEditor
    {
        private TriggerAuthoringModuleDrawer _drawer;

        protected override void OnEnable()
        {
            base.OnEnable();
            if (_drawer == null)
            {
                _drawer = new TriggerAuthoringModuleDrawer(target as TriggerAuthoringModuleAsset);
                _drawer.RepaintRequested += Repaint;
            }
            else
            {
                _drawer.SetAsset(target as TriggerAuthoringModuleAsset);
            }
        }

        protected override void OnDisable()
        {
            if (_drawer != null)
            {
                _drawer.RepaintRequested -= Repaint;
                _drawer.Dispose();
                _drawer = null;
            }

            base.OnDisable();
        }

        public override void OnInspectorGUI()
        {
            _drawer?.Draw();
        }
    }
}
#endif
