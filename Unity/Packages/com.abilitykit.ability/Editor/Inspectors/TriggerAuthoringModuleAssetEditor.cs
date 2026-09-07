#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using AbilityKit.Ability.Config.Authoring;
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
            menu.AddItem(new GUIContent("Copy"), false, () => context.Copy?.Invoke());
            AddOptional(menu, "Paste As Child", context.CanPasteChild, context.PasteChild);
            menu.AddSeparator(string.Empty);
            AddOptional(menu, context.Node != null && context.Node.Enabled ? "Disable Node" : "Enable Node", true, context.ToggleEnabled);
            AddOptional(menu, "Change Type", true, context.ChangeType);
            AddOptional(menu, "Replace With Group", true, context.SelectGroup);
            menu.AddSeparator(string.Empty);
            if (context.Kind == TriggerNodeKind.Action)
            {
                AddOptional(menu, "Debug Log/Add Child", context.CanAddChild, context.AddDebugLogChild);
                AddOptional(menu, "Debug Log/Insert Before", context.InsertDebugLogBefore != null, context.InsertDebugLogBefore);
                AddOptional(menu, "Debug Log/Insert After", context.InsertDebugLogAfter != null, context.InsertDebugLogAfter);
            }
            else
            {
                menu.AddDisabledItem(new GUIContent("Debug Log/Only available for actions"));
            }
            menu.AddSeparator(string.Empty);
            AddOptional(menu, "Remove", context.Remove != null, context.Remove);
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
        private const float SplitThreshold = 680f;
        private const float TriggerListWidth = 220f;

        internal event Action RepaintRequested;

        private readonly EditorCommandRegistry _commands = new EditorCommandRegistry();
        private readonly List<IDisposable> _commandRegistrations = new List<IDisposable>();
        private TriggerAuthoringModuleAsset _asset;
        private TriggerTypeDescriptorCatalog _types;
        private TriggerEventDescriptorCatalog _events;
        private TriggerGlobalBlackboardDescriptorCatalog _globalBlackboard;
        private TriggerTemplateDescriptorCatalog _templates;
        private List<TriggerAuthoringDiagnostic> _diagnostics = new List<TriggerAuthoringDiagnostic>();
        private EditorDiagnosticCollection _platformDiagnostics = new EditorDiagnosticCollection();
        private Vector2 _triggerScroll;
        private readonly EditorSearchState _triggerSearch = new EditorSearchState();
        private TriggerAuthoringTriggerGroupMode _triggerGroupMode = TriggerAuthoringTriggerGroupMode.Event;
        private TriggerAuthoringTriggerQuickFilter _triggerQuickFilter = TriggerAuthoringTriggerQuickFilter.All;
        private readonly HashSet<string> _expandedTriggerGroups = new HashSet<string>(StringComparer.Ordinal);
        private bool _triggerGroupsInitialized;
        private Vector2 _detailScroll;
        private Vector2 _diagnosticScroll;
        private int _selectedTriggerIndex = -1;
        private string _focusedDiagnosticPath;
        private bool _showModuleBlackboard;
        private bool _showGroups;
        private bool _showConditionGroups = true;
        private bool _showActionGroups = true;
        private bool _showAdvanced;
        private bool _showTriggerBlackboard;
        private bool _showTemplatePreview;
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

        public TriggerAuthoringModuleDrawer(TriggerAuthoringModuleAsset asset)
        {
            RegisterCommands();
            SetAsset(asset);
        }

        public TriggerAuthoringModuleAsset Asset => _asset;

        /// <summary>切换目标资产（同一资产为空操作）：重建目录、恢复选中并刷新诊断。</summary>
        public void SetAsset(TriggerAuthoringModuleAsset asset)
        {
            if (_asset == asset) return;
            _asset = asset;
            RebuildCatalogs();
            EnsureSelection();
            RefreshDiagnostics();
            _dismissedSyncBannerState = null;
            _expandedTriggerGroups.Clear();
            _triggerGroupsInitialized = false;
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
            if (_asset == null) return;

            PrepareUndoForInput();
            EditorGUI.BeginChangeCheck();
            DrawToolbar();
            DrawExternalChangeBanner();
            DrawModuleHeader();
            GUILayout.Space(4f);

            if (EditorGUIUtility.currentViewWidth >= SplitThreshold)
            {
                EditorGUILayout.BeginHorizontal();
                DrawTriggerList();
                DrawSelectedTrigger();
                EditorGUILayout.EndHorizontal();
            }
            else
            {
                DrawTriggerList(false);
                DrawSelectedTrigger();
            }

            DrawDiagnostics();
            if (!EditorGUI.EndChangeCheck()) return;

            EditorUtility.SetDirty(_asset);
            RebuildCatalogs();
            RefreshDiagnostics();
            _nextSyncInspectionAt = 0d;
        }

        private void DrawToolbar()
        {
            RefreshSyncInspectionIfNeeded();
            SirenixEditorGUI.BeginHorizontalToolbar();
            GUILayout.Label("Source", GUILayout.Width(44f));
            var state = _syncInspection != null ? _syncInspection.State.ToString() : "Unknown";
            var oldColor = GUI.color;
            GUI.color = GetSyncColor(_syncInspection != null ? _syncInspection.State : TriggerAuthoringSyncState.Untracked);
            GUILayout.Label(state, EditorStyles.boldLabel, GUILayout.Width(92f));
            GUI.color = oldColor;
            GUILayout.FlexibleSpace();

            DrawCommandButton(TriggerAuthoringCommandIds.Import);
            DrawCommandButton(TriggerAuthoringCommandIds.ExportSource);
            DrawCommandButton(TriggerAuthoringCommandIds.ExportRuntime);
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
                message = "The Source file was modified outside this inspector (external editor or AI). Import to apply the changes, or dismiss and keep editing.";
            else if (state == TriggerAuthoringSyncState.SourceMissing)
                message = "The bound Source file does not exist (moved, renamed or deleted).";
            if (message == null) return;

            EditorGUILayout.HelpBox(message, MessageType.Warning);
            EditorGUILayout.BeginHorizontal();
            if (state == TriggerAuthoringSyncState.JsonChanged &&
                GUILayout.Button("Import", EditorStyles.miniButtonLeft, GUILayout.Width(64f)))
                ImportSource();
            if (GUILayout.Button("Dismiss", EditorStyles.miniButtonRight, GUILayout.Width(64f)))
            {
                _dismissedSyncBannerState = state;
                RequestRepaint();
            }
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(4f);
        }

        private void DrawModuleHeader()
        {
            SirenixEditorGUI.BeginBox("Module");
            EditorGUILayout.BeginHorizontal();
            var project = (TriggerAuthoringProjectAsset)EditorGUILayout.ObjectField(
                "Project", _asset.Project, typeof(TriggerAuthoringProjectAsset), false);
            if (_asset.Project == null &&
                GUILayout.Button(new GUIContent("Create", "Create a project with catalogs and assign this module"), EditorStyles.miniButton, GUILayout.Width(52f)))
            {
                CreateAndAssignProject();
                project = _asset.Project;
            }
            EditorGUILayout.EndHorizontal();
            if (project != _asset.Project)
            {
                var previous = _asset.Project;
                Edit("Assign Trigger Authoring Project", () => AssignProject(previous, project));
                RebuildCatalogs();
            }

            _asset.Metadata = _asset.Metadata ?? new TriggerAuthoringSourceMetadata();
            _asset.Module = _asset.Module ?? new TriggerAuthoringModuleData();
            var module = _asset.Module;
            module.ModuleId = EditorGUILayout.TextField("Module Id", module.ModuleId);
            module.DisplayName = EditorGUILayout.TextField("Display Name", module.DisplayName);
            module.Kind = (TriggerModuleKind)EditorGUILayout.EnumPopup("Kind", module.Kind);
            _asset.Metadata.Author = EditorGUILayout.TextField("Author", _asset.Metadata.Author);
            _asset.Metadata.Description = EditorGUILayout.TextField("Description", _asset.Metadata.Description);

            _showModuleBlackboard = EditorGUILayout.Foldout(
                _showModuleBlackboard,
                $"Module Local Vars ({Count(module.Blackboard)})",
                true);
            if (_showModuleBlackboard)
                DrawBlackboard(module.Blackboard, "Module Local Vars", TriggerAuthoringLocalBlackboardScope.Module, null);

            _showGroups = EditorGUILayout.Foldout(
                _showGroups,
                $"Reusable Groups ({Count(module.ConditionGroups) + Count(module.ActionGroups)})",
                true);
            if (_showGroups) DrawGroups(module);
            SirenixEditorGUI.EndBox();
        }

        private void DrawTriggerList(bool fixedWidth = true)
        {
            var options = fixedWidth ? new[] { GUILayout.Width(TriggerListWidth) } : Array.Empty<GUILayoutOption>();
            EditorGUILayout.BeginVertical(EditorStyles.helpBox, options);
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label($"Triggers ({Count(_asset.Module.Triggers)})", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(new GUIContent("+", "Add trigger"), EditorStyles.miniButton, GUILayout.Width(26f)))
                AddTrigger();
            EditorGUILayout.EndHorizontal();

            EditorImGuiControls.DrawSearch(_triggerSearch, new GUIContent("Search"));
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Group", EditorStyles.miniLabel, GUILayout.Width(42f));
            var nextGroupMode = (TriggerAuthoringTriggerGroupMode)EditorGUILayout.EnumPopup(
                _triggerGroupMode,
                EditorStyles.toolbarPopup);
            if (nextGroupMode != _triggerGroupMode)
            {
                _triggerGroupMode = nextGroupMode;
                _expandedTriggerGroups.Clear();
                _triggerGroupsInitialized = false;
            }
            if (GUILayout.Button(new GUIContent("All", "Expand all trigger groups"), EditorStyles.toolbarButton, GUILayout.Width(34f)))
                ExpandVisibleTriggerGroups();
            if (GUILayout.Button(new GUIContent("None", "Collapse all trigger groups"), EditorStyles.toolbarButton, GUILayout.Width(44f)))
            {
                _expandedTriggerGroups.Clear();
                _triggerGroupsInitialized = true;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Filter", EditorStyles.miniLabel, GUILayout.Width(42f));
            var nextQuickFilter = (TriggerAuthoringTriggerQuickFilter)EditorGUILayout.EnumPopup(
                _triggerQuickFilter,
                EditorStyles.toolbarPopup);
            if (nextQuickFilter != _triggerQuickFilter)
            {
                _triggerQuickFilter = nextQuickFilter;
                _expandedTriggerGroups.Clear();
                _triggerGroupsInitialized = false;
            }
            if (_triggerQuickFilter != TriggerAuthoringTriggerQuickFilter.All &&
                GUILayout.Button(new GUIContent("Clear", "Clear quick filter"), EditorStyles.toolbarButton, GUILayout.Width(44f)))
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
                _triggerQuickFilter);
            var visibleIndices = TriggerAuthoringTriggerBatchOperations.CollectVisibleTriggerIndices(groups);
            DrawTriggerBatchToolbar(triggers, visibleIndices);
            DrawSelectedTriggerVisibilityHint(triggers, visibleIndices);

            _triggerScroll = EditorGUILayout.BeginScrollView(_triggerScroll, GUILayout.MinHeight(90f), GUILayout.MaxHeight(360f));
            EnsureInitialTriggerGroupExpansion(groups);
            if (groups.Count == 0)
                EditorGUILayout.HelpBox("No triggers match the current search.", MessageType.Info);
            for (var i = 0; i < groups.Count; i++)
                DrawTriggerGroup(groups[i], triggers);
            EditorGUILayout.EndScrollView();

            using (new EditorGUI.DisabledScope(_selectedTriggerIndex < 0 || _selectedTriggerIndex >= triggers.Count))
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button(new GUIContent("↑", "Move trigger up"), EditorStyles.miniButtonLeft, GUILayout.Width(24f)))
                    MoveSelectedTrigger(triggers, -1);
                if (GUILayout.Button(new GUIContent("↓", "Move trigger down"), EditorStyles.miniButtonMid, GUILayout.Width(24f)))
                    MoveSelectedTrigger(triggers, 1);
                if (GUILayout.Button("Duplicate", EditorStyles.miniButtonMid)) DuplicateSelectedTrigger();
                if (GUILayout.Button("Delete", EditorStyles.miniButtonRight)) DeleteSelectedTrigger();
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndVertical();
        }

        private void DrawTriggerBatchToolbar(
            List<TriggerDefinitionData> triggers,
            IReadOnlyList<int> visibleIndices)
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("Visible " + Count(visibleIndices), EditorStyles.miniLabel);
            GUILayout.FlexibleSpace();
            using (new EditorGUI.DisabledScope(Count(visibleIndices) == 0))
            {
                if (GUILayout.Button(new GUIContent("Batch", "Batch edit currently visible triggers"), EditorStyles.toolbarDropDown))
                    ShowTriggerBatchMenu(triggers, visibleIndices);
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
            GUILayout.Label("Selected trigger is hidden by current search or filter.", EditorStyles.miniLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(new GUIContent("Show", "Clear trigger search and quick filter"), EditorStyles.miniButton, GUILayout.Width(48f)))
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
            IReadOnlyList<int> visibleIndices)
        {
            var indices = visibleIndices != null ? new List<int>(visibleIndices) : new List<int>();
            var menu = new GenericMenu();
            if (indices.Count == 0)
            {
                menu.AddDisabledItem(new GUIContent("No Visible Triggers"));
                menu.ShowAsContext();
                return;
            }

            var count = indices.Count;
            menu.AddItem(new GUIContent("Select First Visible"), false, () =>
            {
                SelectTrigger(indices[0]);
                RequestRepaint();
            });
            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Enable Visible"), false, () =>
                ConfirmAndApplyVisibleTriggerBatch(
                    "Enable Visible Triggers",
                    "Enable " + count + " visible triggers?",
                    "Enable",
                    "Enable Visible Triggers",
                    () => TriggerAuthoringTriggerBatchOperations.SetEnabled(triggers, indices, true),
                    "Enabled visible triggers"));
            menu.AddItem(new GUIContent("Disable Visible"), false, () =>
                ConfirmAndApplyVisibleTriggerBatch(
                    "Disable Visible Triggers",
                    "Disable " + count + " visible triggers?",
                    "Disable",
                    "Disable Visible Triggers",
                    () => TriggerAuthoringTriggerBatchOperations.SetEnabled(triggers, indices, false),
                    "Disabled visible triggers"));
            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Group Path/Set Visible..."), false, () =>
                TriggerAuthoringTextPrompt.Open(
                    "Set Visible Group Path",
                    "Group Path",
                    GuessVisibleGroupPath(triggers, indices),
                    value => ApplyVisibleTriggerBatch(
                        "Set Visible Trigger Group Path",
                        () => TriggerAuthoringTriggerBatchOperations.SetGroupPath(triggers, indices, value),
                        "Updated visible group paths")));
            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Tags/Add To Visible..."), false, () =>
                TriggerAuthoringTextPrompt.Open(
                    "Add Tags To Visible Triggers",
                    "Tags, comma separated",
                    string.Empty,
                    value => ApplyVisibleTriggerBatch(
                        "Add Visible Trigger Tags",
                        () => TriggerAuthoringTriggerBatchOperations.AddTags(triggers, indices, value),
                        "Added visible trigger tags")));
            menu.AddItem(new GUIContent("Tags/Remove From Visible..."), false, () =>
                TriggerAuthoringTextPrompt.Open(
                    "Remove Tags From Visible Triggers",
                    "Tags, comma separated",
                    string.Empty,
                    value => ApplyVisibleTriggerBatch(
                        "Remove Visible Trigger Tags",
                        () => TriggerAuthoringTriggerBatchOperations.RemoveTags(triggers, indices, value),
                        "Removed visible trigger tags")));
            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Copy Visible Trigger Ids"), false, () =>
            {
                EditorGUIUtility.systemCopyBuffer =
                    TriggerAuthoringTriggerBatchOperations.BuildTriggerIdList(triggers, indices);
                ShowNotification("Copied visible trigger ids");
            });
            menu.ShowAsContext();
        }

        private void ConfirmAndApplyVisibleTriggerBatch(
            string title,
            string message,
            string ok,
            string undoName,
            Func<int> operation,
            string notification)
        {
            if (!EditorUtility.DisplayDialog(title, message, ok, "Cancel")) return;
            ApplyVisibleTriggerBatch(undoName, operation, notification);
        }

        private void ApplyVisibleTriggerBatch(
            string undoName,
            Func<int> operation,
            string notification)
        {
            var changed = 0;
            Edit(undoName, () => changed = operation != null ? operation() : 0);
            ShowNotification(notification + ": " + changed);
        }

        private string GuessVisibleGroupPath(
            IReadOnlyList<TriggerDefinitionData> triggers,
            IReadOnlyList<int> indices)
        {
            if (triggers == null || indices == null) return string.Empty;
            if (_selectedTriggerIndex >= 0 &&
                _selectedTriggerIndex < triggers.Count &&
                ContainsIndex(indices, _selectedTriggerIndex))
                return triggers[_selectedTriggerIndex] != null ? triggers[_selectedTriggerIndex].GroupPath : string.Empty;

            for (var i = 0; i < indices.Count; i++)
            {
                var index = indices[i];
                if (index < 0 || index >= triggers.Count || triggers[index] == null) continue;
                if (!string.IsNullOrWhiteSpace(triggers[index].GroupPath)) return triggers[index].GroupPath;
            }
            return string.Empty;
        }

        private static bool ContainsIndex(IReadOnlyList<int> indices, int index)
        {
            return TriggerAuthoringTriggerBatchOperations.ContainsVisibleTriggerIndex(indices, index);
        }

        private void DrawTriggerGroup(
            TriggerAuthoringTriggerIndex.Group group,
            List<TriggerDefinitionData> triggers)
        {
            if (group == null) return;
            if (_triggerGroupMode == TriggerAuthoringTriggerGroupMode.Flat)
            {
                for (var i = 0; i < group.Entries.Count; i++)
                    DrawTriggerRow(group.Entries[i], triggers);
                return;
            }

            var expanded = _triggerSearch.IsEmpty
                ? _expandedTriggerGroups.Contains(group.Key)
                : true;
            var nextExpanded = EditorGUILayout.Foldout(
                expanded,
                group.Label + " (" + group.Entries.Count + ")",
                true);
            if (nextExpanded) _expandedTriggerGroups.Add(group.Key);
            else _expandedTriggerGroups.Remove(group.Key);
            if (!nextExpanded) return;

            EditorGUI.indentLevel++;
            for (var i = 0; i < group.Entries.Count; i++)
                DrawTriggerRow(group.Entries[i], triggers);
            EditorGUI.indentLevel--;
        }

        private void DrawTriggerRow(
            TriggerAuthoringTriggerIndex.Entry entry,
            List<TriggerDefinitionData> triggers)
        {
            var label = BuildTriggerRowLabel(entry.Trigger, entry.Index, entry.Diagnostics);
            var oldBackground = GUI.backgroundColor;
            if (entry.Index == _selectedTriggerIndex) GUI.backgroundColor = new Color(0.42f, 0.66f, 0.92f);
            if (GUILayout.Button(label, EditorStyles.miniButton, GUILayout.Height(25f)))
                SelectTrigger(entry.Index);
            else if (Event.current.type == EventType.ContextClick &&
                     GUILayoutUtility.GetLastRect().Contains(Event.current.mousePosition))
            {
                SelectTrigger(entry.Index);
                ShowTriggerContextMenu(triggers, entry.Index);
                Event.current.Use();
            }
            GUI.backgroundColor = oldBackground;
        }

        private string BuildTriggerRowLabel(
            TriggerDefinitionData trigger,
            int index,
            TriggerAuthoringTriggerIndex.DiagnosticSummary diagnostics)
        {
            if (trigger == null) return (index + 1) + ". <null>";
            var suffix = diagnostics.Errors > 0 ? "  (E" + diagnostics.Errors + (diagnostics.Warnings > 0 ? " W" + diagnostics.Warnings : string.Empty) + ")"
                : diagnostics.Warnings > 0 ? "  (W" + diagnostics.Warnings + ")"
                : string.Empty;
            var eventName = string.IsNullOrWhiteSpace(trigger.Event) ? string.Empty : "  " + trigger.Event;
            return $"{(trigger.Enabled ? "+" : "-")} {trigger.Id}  {DisplayTriggerName(trigger)}{eventName}{suffix}";
        }

        private void SelectTrigger(int index)
        {
            _selectedTriggerIndex = index;
            _focusedDiagnosticPath = null;
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
                _triggerQuickFilter);
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
            menu.AddItem(new GUIContent("Move Up"), false, () => MoveSelectedTrigger(triggers, -1));
            menu.AddItem(new GUIContent("Move Down"), false, () => MoveSelectedTrigger(triggers, 1));
            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Locate/Copy Trigger Path"), false, () =>
                EditorGUIUtility.systemCopyBuffer = "module.triggers[" + index + "]");
            menu.AddItem(new GUIContent("Locate/Ping Module Asset"), false, () =>
            {
                Selection.activeObject = _asset;
                EditorGUIUtility.PingObject(_asset);
            });
            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Duplicate"), false, DuplicateSelectedTrigger);
            menu.AddItem(new GUIContent("Delete"), false, DeleteSelectedTrigger);
            if (trigger != null)
            {
                menu.AddSeparator(string.Empty);
                menu.AddItem(
                    new GUIContent(trigger.Enabled ? "Disable" : "Enable"),
                    false,
                    () => Edit("Toggle Trigger Enabled", () => trigger.Enabled = !trigger.Enabled));
            }
            menu.ShowAsContext();
        }

        private void MoveSelectedTrigger(List<TriggerDefinitionData> triggers, int delta)
        {
            var index = _selectedTriggerIndex;
            var target = index + delta;
            if (triggers == null || index < 0 || index >= triggers.Count ||
                target < 0 || target >= triggers.Count) return;
            Edit("Reorder Triggers", () =>
            {
                var temporary = triggers[index];
                triggers[index] = triggers[target];
                triggers[target] = temporary;
                _selectedTriggerIndex = target;
            });
        }

        private void DrawSelectedTrigger()
        {
            var triggers = _asset.Module.Triggers;
            if (triggers == null || _selectedTriggerIndex < 0 || _selectedTriggerIndex >= triggers.Count)
            {
                EditorGUILayout.HelpBox("Select or add a trigger.", MessageType.Info);
                return;
            }

            var trigger = triggers[_selectedTriggerIndex];
            if (trigger == null)
            {
                if (GUILayout.Button("Create Trigger"))
                    Edit("Create Trigger", () => triggers[_selectedTriggerIndex] = CreateTrigger());
                return;
            }

            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.MinWidth(360f));
            _detailScroll = EditorGUILayout.BeginScrollView(_detailScroll, GUILayout.MinHeight(320f));
            DrawTriggerHeader(trigger);
            DrawTriggerNodes(trigger);
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawTriggerHeader(TriggerDefinitionData trigger)
        {
            EditorGUILayout.BeginHorizontal();
            trigger.Enabled = EditorGUILayout.ToggleLeft("Enabled", trigger.Enabled, GUILayout.Width(72f));
            trigger.Id = EditorGUILayout.IntField("Id", trigger.Id, GUILayout.Width(150f));
            trigger.Name = EditorGUILayout.TextField(trigger.Name);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            trigger.Event = EditorGUILayout.TextField("Event", trigger.Event);
            if (GUILayout.Button(new GUIContent("Select", "Choose from Event Catalog"), GUILayout.Width(58f)))
                ShowEventMenu(trigger);
            using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(trigger.Event) || _asset.Project == null))
            {
                if (GUILayout.Button(new GUIContent("Refs", "Find triggers using this event across the project"), EditorStyles.miniButton, GUILayout.Width(40f)))
                    ShowReferences(
                        TriggerAuthoringReferenceFinder.FindEventReferences(_asset.Project, trigger.Event),
                        "Event: " + trigger.Event);
            }
            EditorGUILayout.EndHorizontal();

            if (_events != null && !string.IsNullOrWhiteSpace(trigger.Event) &&
                _events.TryResolve(trigger.Event, out var eventDefinition))
            {
                EditorGUILayout.LabelField(
                    $"{eventDefinition.Category} | {eventDefinition.PayloadType} | " +
                    $"{Count(eventDefinition.PayloadFields)} payload fields",
                    EditorStyles.miniLabel);
            }

            trigger.GroupPath = EditorGUILayout.TextField(
                new GUIContent("Group Path", "Business grouping path used by the trigger management list and Source JSON."),
                trigger.GroupPath);
            trigger.Tags = TriggerAuthoringTriggerBatchOperations.ParseTags(EditorGUILayout.TextField(
                new GUIContent("Tags", "Comma separated search tags stored in Source JSON."),
                FormatTags(trigger.Tags)));

            DrawTemplateBinding(trigger);

            _showAdvanced = EditorGUILayout.Foldout(_showAdvanced, "Execution", true);
            if (_showAdvanced)
            {
                trigger.Phase = DrawConstrainedOption("Phase", trigger.Phase, PhaseOptions);
                trigger.Scope = DrawConstrainedOption("Scope", trigger.Scope, ScopeOptions);
                trigger.Priority = EditorGUILayout.IntField("Priority", trigger.Priority);
                trigger.InterruptPriority = EditorGUILayout.IntField("Interrupt Priority", trigger.InterruptPriority);
                trigger.AllowExternal = EditorGUILayout.Toggle("Allow External", trigger.AllowExternal);
                trigger.Note = EditorGUILayout.TextField("Note", trigger.Note);

                trigger.Cue = trigger.Cue ?? new TriggerCueData();
                trigger.Cue.CueId = EditorGUILayout.TextField("Cue Id", trigger.Cue.CueId);

                trigger.Schedule = trigger.Schedule ?? new TriggerScheduleData();
                trigger.Schedule.Mode = DrawConstrainedOption("Schedule Mode", trigger.Schedule.Mode, ScheduleModeOptions);
                trigger.Schedule.DelayMilliseconds = EditorGUILayout.IntField("Delay (ms)", trigger.Schedule.DelayMilliseconds);
                trigger.Schedule.IntervalMilliseconds = EditorGUILayout.IntField("Interval (ms)", trigger.Schedule.IntervalMilliseconds);
                trigger.Schedule.RepeatCount = EditorGUILayout.IntField("Repeat Count", trigger.Schedule.RepeatCount);

                trigger.ExecutionControl = trigger.ExecutionControl ?? new TriggerExecutionControlData();
                trigger.ExecutionControl.InterruptPolicy = DrawConstrainedOption(
                    "Interrupt Policy", trigger.ExecutionControl.InterruptPolicy, InterruptPolicyOptions);
                trigger.ExecutionControl.StopPropagationOnSuccess =
                    EditorGUILayout.Toggle("Stop On Success", trigger.ExecutionControl.StopPropagationOnSuccess);
                trigger.ExecutionControl.StopPropagationOnFailure =
                    EditorGUILayout.Toggle("Stop On Failure", trigger.ExecutionControl.StopPropagationOnFailure);
            }

            _showTriggerBlackboard = EditorGUILayout.Foldout(
                _showTriggerBlackboard,
                $"Trigger Local Vars ({Count(trigger.Blackboard)})",
                true);
            if (_showTriggerBlackboard)
                DrawBlackboard(
                    trigger.Blackboard,
                    "Trigger Local Vars",
                    TriggerAuthoringLocalBlackboardScope.Trigger,
                    _asset.Module != null ? _asset.Module.Blackboard : null);
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
                "Template",
                current,
                typeof(TriggerAuthoringTemplateAsset),
                false);
            if (selected != current && ConfirmTemplateAssignment(trigger, selected))
                Edit("Assign Trigger Template", () => AssignTemplate(trigger, selected));
            using (new EditorGUI.DisabledScope(current == null))
            {
                if (GUILayout.Button(new GUIContent("Open", "Select the template asset"), EditorStyles.miniButton, GUILayout.Width(42f)))
                {
                    Selection.activeObject = current;
                    EditorGUIUtility.PingObject(current);
                }
            }
            using (new EditorGUI.DisabledScope(reference == null || string.IsNullOrWhiteSpace(reference.TemplateId) || _asset.Project == null))
            {
                if (GUILayout.Button(new GUIContent("Refs", "Find triggers bound to this template"), EditorStyles.miniButton, GUILayout.Width(40f)))
                    ShowReferences(
                        TriggerAuthoringReferenceFinder.FindTemplateReferences(_asset.Project, reference.TemplateId),
                        "Template: " + reference.TemplateId);
            }
            EditorGUILayout.EndHorizontal();

            if (reference != null)
            {
                EditorGUILayout.LabelField(
                    $"{reference.TemplateId ?? "<missing>"}  v{reference.Version ?? "<missing>"}",
                    EditorStyles.miniLabel);
                if (current == null)
                {
                    EditorGUILayout.HelpBox("Template reference is missing or ambiguous in the project catalog.", MessageType.Error);
                    DrawRawTemplateBindings(reference.Bindings, trigger);
                }
                else
                {
                    DrawTypedTemplateBindings(reference, current.Template, trigger);
                    _showTemplatePreview = EditorGUILayout.Foldout(_showTemplatePreview, "Expanded Template Preview", true);
                    if (_showTemplatePreview) DrawTemplatePreview(current.Template, trigger);
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
                           "Remove Template",
                           "Remove the template binding from this trigger?\n\n" +
                           "The trigger keeps the template-provided event and empty Condition/Actions.",
                           "Remove Template",
                           "Cancel");
            }

            var losesTrees = trigger.Condition != null || trigger.Actions != null;
            var losesEvent = !string.IsNullOrWhiteSpace(trigger.Event) &&
                             (selected.Template == null ||
                              !string.Equals(trigger.Event, selected.Template.Event, StringComparison.Ordinal));
            if (trigger.Template == null && !losesTrees && !losesEvent) return true;

            var message = "Assigning this template will:";
            if (losesTrees) message += "\n  - clear the local Condition and Actions trees";
            if (losesEvent) message += "\n  - overwrite the trigger event";
            if (trigger.Template != null) message += "\n  - replace the current template bindings";
            message += "\n\nContinue?";
            return EditorUtility.DisplayDialog("Assign Template", message, "Assign Template", "Cancel");
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
            trigger.Event = asset.Template.Event;
            trigger.Condition = null;
            trigger.Actions = null;
        }

        private void DrawTypedTemplateBindings(
            TriggerTemplateReferenceData reference,
            TriggerAuthoringTemplateData template,
            TriggerDefinitionData trigger)
        {
            var bindings = reference.Bindings;
            if (bindings == null)
            {
                EditorGUILayout.HelpBox("Template bindings collection is null.", MessageType.Error);
                return;
            }
            var parameters = template?.Parameters ?? new List<TriggerAuthoringTemplateParameterData>();
            for (var i = 0; i < parameters.Count; i++)
            {
                var parameter = parameters[i];
                if (parameter == null || string.IsNullOrWhiteSpace(parameter.Name)) continue;
                var binding = FindArgument(bindings, parameter.Name);
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label(parameter.Name, EditorStyles.miniBoldLabel);
                GUILayout.FlexibleSpace();
                if (binding == null)
                {
                    var label = parameter.HasDefault ? "Override" : "Add";
                    if (GUILayout.Button(label, EditorStyles.miniButton, GUILayout.Width(58f)))
                    {
                        var captured = parameter;
                        Edit("Add Template Binding", () => bindings.Add(new TriggerArgumentData
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
                    Edit("Remove Template Binding", () => bindings.Remove(captured));
                }
                EditorGUILayout.EndHorizontal();
                if (binding != null)
                {
                    if (binding.Value == null)
                        EditorGUILayout.HelpBox("Binding value is null.", MessageType.Error);
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
                    EditorGUILayout.LabelField("Using template default", EditorStyles.miniLabel);
                }
                else if (parameter.Required)
                {
                    EditorGUILayout.HelpBox("Required binding is missing.", MessageType.Error);
                }
                EditorGUILayout.EndVertical();
            }

            for (var i = 0; i < bindings.Count; i++)
            {
                var binding = bindings[i];
                if (binding == null || HasTemplateParameter(parameters, binding.Name)) continue;
                EditorGUILayout.HelpBox($"Unknown binding '{binding.Name}' is preserved.", MessageType.Error);
            }
        }

        private void DrawRawTemplateBindings(List<TriggerArgumentData> bindings, TriggerDefinitionData trigger)
        {
            if (bindings == null) return;
            for (var i = 0; i < bindings.Count; i++)
            {
                var binding = bindings[i];
                if (binding == null) continue;
                EditorGUILayout.LabelField(binding.Name ?? "<unnamed>", EditorStyles.miniBoldLabel);
                if (binding.Value == null)
                    EditorGUILayout.HelpBox("Binding value is null.", MessageType.Error);
                else
                    DrawValueRef(binding.Value, null, trigger);
            }
        }

        private void DrawTemplatePreview(TriggerAuthoringTemplateData template, TriggerDefinitionData trigger)
        {
            if (template == null) return;
            using (new EditorGUI.DisabledScope(true))
            {
                if (template.Condition != null)
                    DrawNode(TriggerAuthoringGroupResolver.CloneNode(template.Condition), TriggerNodeKind.Condition, trigger, 0, true, null, null, null);
                if (template.Actions != null)
                    DrawNode(TriggerAuthoringGroupResolver.CloneNode(template.Actions), TriggerNodeKind.Action, trigger, 0, true, null, null, null);
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

        private void DrawTriggerNodes(TriggerDefinitionData trigger)
        {
            GUILayout.Space(4f);
            var basePath = "module.triggers[" + _selectedTriggerIndex + "]";
            trigger.Condition = DrawRootNode(trigger.Condition, TriggerNodeKind.Condition, trigger, "Condition", basePath + ".condition");
            GUILayout.Space(4f);
            trigger.Actions = DrawRootNode(trigger.Actions, TriggerNodeKind.Action, trigger, "Actions", basePath + ".actions");
        }

        private TriggerNodeData DrawRootNode(
            TriggerNodeData node,
            TriggerNodeKind kind,
            TriggerDefinitionData trigger,
            string title,
            string nodePath)
        {
            SirenixEditorGUI.BeginBox(title);
            if (node == null)
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Add " + title))
                    ShowNodeCreationMenu(kind, created => SetRootNode(trigger, kind, created), GUILayoutUtility.GetLastRect());
                using (new EditorGUI.DisabledScope(!TriggerAuthoringNodeClipboard.HasNode()))
                {
                    if (GUILayout.Button("Paste " + title, EditorStyles.miniButton))
                        PasteNodeAsRoot(trigger, kind, title);
                }
                EditorGUILayout.EndHorizontal();
                SirenixEditorGUI.EndBox();
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
            SirenixEditorGUI.EndBox();
            return node;
        }

        private void PasteNodeAsRoot(TriggerDefinitionData trigger, TriggerNodeKind kind, string title)
        {
            if (!TriggerAuthoringNodeClipboard.TryPaste(kind, out var pasted))
            {
                EditorUtility.DisplayDialog("Paste Node", "Clipboard does not contain a matching " + title + " node.", "OK");
                return;
            }
            Edit("Paste Trigger Node", () => SetRootNode(trigger, kind, pasted));
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
            EditorGUILayout.BeginVertical(depth == 0 ? EditorStyles.helpBox : SirenixGUIStyles.BoxContainer);
            if (!string.IsNullOrWhiteSpace(node.GroupReference))
            {
                var groupNode = DrawGroupReferenceNode(node, kind, depth, nodePath, replaceNode, removeNode, insertBefore, insertAfter);
                GUI.backgroundColor = oldBackground;
                return groupNode;
            }

            _types.TryGet(kind, node.Type, out var descriptor);
            var children = node.Children ?? (node.Children = new List<TriggerNodeData>());
            var maxChildren = descriptor != null ? descriptor.MaxChildren : 0;
            var canPasteChild = maxChildren != 0 &&
                                (maxChildren < 0 || children.Count < maxChildren) &&
                                TriggerAuthoringNodeClipboard.HasNode();
            var canAddChild = maxChildren != 0 && (maxChildren < 0 || children.Count < maxChildren);

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label((node.Enabled ? string.Empty : "[Disabled] ") + (node.Type ?? "<type>"), EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            var hasMoveButtons = onMoveUp != null || onMoveDown != null;
            if (onMoveUp != null && GUILayout.Button(new GUIContent("↑", "Move node up"), EditorStyles.miniButtonLeft, GUILayout.Width(22f)))
                onMoveUp();
            if (onMoveDown != null && GUILayout.Button(new GUIContent("↓", "Move node down"), onMoveUp != null ? EditorStyles.miniButtonMid : EditorStyles.miniButtonLeft, GUILayout.Width(22f)))
                onMoveDown();
            if (GUILayout.Button(new GUIContent("Copy", "Copy this node subtree to clipboard"), hasMoveButtons ? EditorStyles.miniButtonMid : EditorStyles.miniButtonLeft, GUILayout.Width(38f)))
                TriggerAuthoringNodeClipboard.Copy(node, kind);
            using (new EditorGUI.DisabledScope(!canPasteChild))
            {
                if (GUILayout.Button(new GUIContent("Paste", "Paste clipboard node as a child"), EditorStyles.miniButtonMid, GUILayout.Width(40f)))
                    PasteNodeAsChild(children, kind);
            }
            if (GUILayout.Button(new GUIContent("Type", "Change node type"), EditorStyles.miniButtonMid, GUILayout.Width(42f)))
                ShowNodeTypeMenu(kind, descriptor => ApplyDescriptor(node, descriptor), GUILayoutUtility.GetLastRect());
            if (GUILayout.Button(new GUIContent("Group", "Replace with a reusable group reference"), EditorStyles.miniButtonMid, GUILayout.Width(48f)))
                ShowGroupMenu(kind, groupId => ApplyGroupReference(node, kind, groupId));
            var remove = GUILayout.Button(new GUIContent("x", "Remove node"), EditorStyles.miniButtonRight, GUILayout.Width(25f));
            EditorGUILayout.EndHorizontal();
            var headerRect = GUILayoutUtility.GetLastRect();
            if (ShouldOpenContextMenu(headerRect))
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
                    headerRect);
                Event.current.Use();
            }
            if (remove)
            {
                EditorGUILayout.EndVertical();
                GUI.backgroundColor = oldBackground;
                return null;
            }

            if (!node.Enabled)
                EditorGUILayout.HelpBox("Disabled nodes stay in source JSON but are ignored by validation and Runtime Plan export.", MessageType.Info);
            DrawNodeArguments(node, descriptor, trigger);

            if (maxChildren != 0)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label($"Children ({children.Count})", EditorStyles.miniBoldLabel);
                GUILayout.FlexibleSpace();
                using (new EditorGUI.DisabledScope(maxChildren > 0 && children.Count >= maxChildren))
                {
                    if (GUILayout.Button("+ Child", EditorStyles.miniButton, GUILayout.Width(64f)))
                        ShowNodeCreationMenu(kind, children.Add, GUILayoutUtility.GetLastRect());
                }
                EditorGUILayout.EndHorizontal();

                for (var i = 0; i < children.Count; i++)
                {
                    var index = i;
                    var childPath = string.IsNullOrEmpty(nodePath) ? null : nodePath + ".children[" + i + "]";
                    Action moveUp = index > 0
                        ? () => Edit("Reorder Trigger Nodes", () => SwapNodes(children, index, index - 1))
                        : null;
                    Action moveDown = index < children.Count - 1
                        ? () => Edit("Reorder Trigger Nodes", () => SwapNodes(children, index, index + 1))
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
                        Edit("Remove Trigger Node", () => children.RemoveAt(i));
                        i--;
                    }
                    else
                    {
                        children[i] = child;
                    }
                }
            }
            EditorGUILayout.EndVertical();
            GUI.backgroundColor = oldBackground;
            return node;
        }

        private void PasteNodeAsChild(List<TriggerNodeData> children, TriggerNodeKind kind)
        {
            if (!TriggerAuthoringNodeClipboard.TryPaste(kind, out var pasted))
            {
                EditorUtility.DisplayDialog("Paste Node", "Clipboard does not contain a matching node.", "OK");
                return;
            }
            Edit("Paste Trigger Node", () => children.Add(pasted));
        }

        private static bool ShouldOpenContextMenu(Rect rect)
        {
            var current = Event.current;
            return current != null &&
                   current.type == EventType.ContextClick &&
                   rect.Contains(current.mousePosition);
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
                ToggleEnabled = () => Edit(node.Enabled ? "Disable Trigger Node" : "Enable Trigger Node", () => node.Enabled = !node.Enabled),
                AddDebugLogChild = () => Edit("Add Debug Log Node", () => children.Add(CreateDebugLogNode("debug"))),
                InsertDebugLogBefore = insertBefore == null
                    ? null
                    : (Action)(() => Edit("Insert Debug Log Node", () => insertBefore(CreateDebugLogNode("before")))),
                InsertDebugLogAfter = insertAfter == null
                    ? null
                    : (Action)(() => Edit("Insert Debug Log Node", () => insertAfter(CreateDebugLogNode("after")))),
                Remove = removeNode == null ? null : (Action)(() => Edit("Remove Trigger Node", removeNode))
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
                EditorGUILayout.HelpBox("Unknown node descriptor. Existing arguments are preserved.", MessageType.Error);
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
                        EditorGUILayout.BeginHorizontal();
                        GUILayout.Label(parameter.Name, EditorStyles.miniLabel);
                        GUILayout.FlexibleSpace();
                        if (GUILayout.Button("Add", EditorStyles.miniButton, GUILayout.Width(42f)))
                            Edit("Add Trigger Argument", () => arguments.Add(CreateArgument(parameter)));
                        EditorGUILayout.EndHorizontal();
                    }
                    continue;
                }

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label(parameter.Name, EditorStyles.miniBoldLabel);
                GUILayout.FlexibleSpace();
                if (!parameter.Required && GUILayout.Button("x", EditorStyles.miniButton, GUILayout.Width(22f)))
                {
                    Edit("Remove Trigger Argument", () => arguments.Remove(argument));
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndVertical();
                    continue;
                }
                EditorGUILayout.EndHorizontal();
                argument.Value = argument.Value ?? CreateValue(parameter.Type);
                DrawValueRef(argument.Value, parameter, trigger);
                EditorGUILayout.EndVertical();
            }

            for (var i = 0; i < arguments.Count; i++)
            {
                var argument = arguments[i];
                if (argument == null || HasParameter(descriptor, argument.Name)) continue;
                EditorGUILayout.HelpBox($"Unknown argument '{argument.Name}' is preserved.", MessageType.Warning);
                DrawValueRef(argument.Value ?? (argument.Value = new TriggerValueRefData()), null, trigger);
            }
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
            if (GUILayout.Button("Add Raw Argument", EditorStyles.miniButton))
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
                    Trigger = trigger,
                    Events = _events,
                    GlobalBlackboard = _globalBlackboard
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
                    value.IntegerValue = EditorGUILayout.LongField("Value", value.IntegerValue);
                    break;
                case TriggerValueType.Entity:
                case TriggerValueType.ObjectId:
                    value.IntegerValue = EditorGUILayout.LongField("Value", value.IntegerValue);
                    break;
                case TriggerValueType.Number:
                    value.NumberValue = EditorGUILayout.DoubleField("Value", value.NumberValue);
                    break;
                case TriggerValueType.Boolean:
                    value.BooleanValue = EditorGUILayout.Toggle("Value", value.BooleanValue);
                    break;
                case TriggerValueType.String:
                    value.StringValue = EditorGUILayout.TextField("Value", value.StringValue);
                    break;
                case TriggerValueType.IntegerList:
                    var current = value.IntegerListValue != null ? string.Join(",", value.IntegerListValue) : string.Empty;
                    var next = EditorGUILayout.TextField("Values", current);
                    if (!string.Equals(current, next, StringComparison.Ordinal))
                        value.IntegerListValue = ParseIntegerList(next);
                    break;
                case TriggerValueType.Vector3:
                    value.Vector3Value = value.Vector3Value ?? new TriggerVector3Data();
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Label("Value", GUILayout.Width(EditorGUIUtility.labelWidth - 4f));
                    value.Vector3Value.X = EditorGUILayout.DoubleField(value.Vector3Value.X);
                    value.Vector3Value.Y = EditorGUILayout.DoubleField(value.Vector3Value.Y);
                    value.Vector3Value.Z = EditorGUILayout.DoubleField(value.Vector3Value.Z);
                    EditorGUILayout.EndHorizontal();
                    break;
                case TriggerValueType.Object:
                    DrawConstantObject(value);
                    break;
                default:
                    EditorGUILayout.HelpBox("Choose a value type.", MessageType.Info);
                    break;
            }
        }

        private static void DrawConstantObject(TriggerValueRefData value)
        {
            value.Fields = value.Fields ?? new List<TriggerArgumentData>();
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Fields", EditorStyles.miniBoldLabel);
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
                var nextType = (TriggerValueType)EditorGUILayout.EnumPopup("Type", field.Value.Type);
                if (nextType != field.Value.Type) field.Value = CreateValue(nextType);
                DrawConstant(field.Value, field.Value.Type, null);
                EditorGUILayout.EndVertical();
                if (remove)
                {
                    value.Fields.RemoveAt(index);
                    i--;
                }
            }

            if (GUILayout.Button("Add Field", EditorStyles.miniButton))
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
                names.Add(value.IntegerValue + "  [unavailable]");
                selected = names.Count - 1;
            }

            var next = EditorGUILayout.Popup("Value", selected, names.ToArray());
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
            DrawPathPopup(value, fields, "Payload Field");
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
            DrawPathPopup(value, options, "Local Key");
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
            DrawPathPopup(value, options, "Global Key");
        }

        private static void DrawPathPopup(TriggerValueRefData value, List<PathOption> options, string label)
        {
            var names = new List<string> { "<None>" };
            var selected = 0;
            for (var i = 0; i < options.Count; i++)
            {
                names.Add(options[i].Path + "  [" + options[i].Type + "]");
                if (string.Equals(options[i].Path, value.Path, StringComparison.Ordinal)) selected = i + 1;
            }
            if (selected == 0 && !string.IsNullOrWhiteSpace(value.Path))
            {
                names.Add(value.Path + "  [unavailable]");
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
            var scopeLabel = scope == TriggerAuthoringLocalBlackboardScope.Module ? "module" : "trigger";
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(scopeLabel + " scope", EditorStyles.miniBoldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(new GUIContent("+", "Add local variable"), EditorStyles.miniButton, GUILayout.Width(26f)))
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
                var nextType = (TriggerValueType)EditorGUILayout.EnumPopup(variable.Type, GUILayout.Width(100f));
                if (nextType != variable.Type)
                {
                    variable.Type = nextType;
                    variable.DefaultValue = CreateValue(nextType);
                }
                variable.ReadOnly = GUILayout.Toggle(variable.ReadOnly, "Read Only", GUILayout.Width(76f));
                var remove = GUILayout.Button("x", EditorStyles.miniButton, GUILayout.Width(22f));
                EditorGUILayout.EndHorizontal();
                variable.Description = EditorGUILayout.TextField("Description", variable.Description);
                variable.DefaultValue = variable.DefaultValue ?? CreateValue(variable.Type);
                EditorGUILayout.LabelField("Default Value", EditorStyles.miniBoldLabel);
                DrawConstant(variable.DefaultValue, variable.Type, null);
                if (string.IsNullOrWhiteSpace(variable.Key))
                {
                    EditorGUILayout.HelpBox("Local var key is required.", MessageType.Error);
                }
                else
                {
                    if (duplicates.Contains(variable.Key))
                        EditorGUILayout.HelpBox("Duplicate local var key in this scope.", MessageType.Error);
                    if (ContainsLocalVarKey(inheritedVariables, variable.Key))
                        EditorGUILayout.HelpBox("This trigger local var shadows a module local var with the same key.", MessageType.Info);
                    EditorGUILayout.SelectableLabel(
                        TriggerAuthoringLocalBlackboardPath.Format(scope, variable.Key),
                        EditorStyles.miniLabel,
                        GUILayout.Height(EditorGUIUtility.singleLineHeight));
                }
                EditorGUILayout.EndVertical();
                if (remove)
                {
                    Edit("Remove " + undoName + " Key", () => variables.RemoveAt(index));
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
            AddLocalVarType(menu, variables, undoName, TriggerValueType.Vector3);
            AddLocalVarType(menu, variables, undoName, TriggerValueType.IntegerList);
            menu.DropDown(activator);
        }

        private void AddLocalVarType(
            GenericMenu menu,
            List<TriggerBlackboardVariableData> variables,
            string undoName,
            TriggerValueType type)
        {
            menu.AddItem(new GUIContent(type.ToString()), false, () =>
                Edit("Add " + undoName + " Key", () => variables.Add(new TriggerBlackboardVariableData
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
                type == TriggerValueType.Vector3 ? "position" :
                type == TriggerValueType.IntegerList ? "ids" :
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
                "Condition Groups",
                _showConditionGroups);
            _showActionGroups = DrawGroupList(
                module.ActionGroups,
                TriggerNodeKind.Action,
                "Action Groups",
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
            if (GUILayout.Button(new GUIContent("+", "Add reusable group"), EditorStyles.miniButton, GUILayout.Width(26f)))
            {
                Edit("Add Trigger Group", () => groups.Add(new TriggerNodeGroupData
                {
                    Id = CreateUniqueGroupId(groups, kind),
                    DisplayName = "New " + (kind == TriggerNodeKind.Condition ? "Condition Group" : "Action Group")
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
                        string.IsNullOrWhiteSpace(group.DisplayName) ? group.Id ?? "<group>" : group.DisplayName,
                        true);
                    if (nextOpen != isOpen)
                    {
                        if (nextOpen) _expandedGroupEditors.Add(editorKey);
                        else _expandedGroupEditors.Remove(editorKey);
                    }
                    GUILayout.FlexibleSpace();
                    using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(group.Id) || _asset.Project == null))
                    {
                        if (GUILayout.Button(new GUIContent("Refs", "Find usages of this group"), EditorStyles.miniButton, GUILayout.Width(38f)))
                            ShowReferences(
                                TriggerAuthoringReferenceFinder.FindGroupReferences(_asset.Project, group.Id),
                                "Group: " + group.Id);
                    }
                    var remove = GUILayout.Button("x", EditorStyles.miniButton, GUILayout.Width(22f));
                    EditorGUILayout.EndHorizontal();

                    if (nextOpen)
                    {
                        group.Id = EditorGUILayout.TextField("Id", group.Id);
                        group.DisplayName = EditorGUILayout.TextField("Display Name", group.DisplayName);
                        group.Description = EditorGUILayout.TextField("Description", group.Description);
                        SirenixEditorGUI.BeginBox("Root");
                        if (group.Root == null)
                        {
                            if (GUILayout.Button("Add Root"))
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
                        Edit("Remove Trigger Group", () => groups.RemoveAt(index));
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
            GUILayout.Label((node.Enabled ? string.Empty : "[Disabled] ") + "Group: " + node.GroupReference, EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(new GUIContent("Select", "Select another reusable group"), EditorStyles.miniButtonLeft, GUILayout.Width(48f)))
                ShowGroupMenu(kind, groupId => ApplyGroupReference(node, kind, groupId));
            if (GUILayout.Button(new GUIContent("Local", "Copy the expanded group tree into this node"), EditorStyles.miniButtonMid, GUILayout.Width(42f)))
                LocalizeGroupReference(node, kind);
            var remove = GUILayout.Button(new GUIContent("x", "Remove node"), EditorStyles.miniButtonRight, GUILayout.Width(25f));
            EditorGUILayout.EndHorizontal();
            var headerRect = GUILayoutUtility.GetLastRect();
            if (ShouldOpenContextMenu(headerRect))
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
                    headerRect);
                Event.current.Use();
            }
            if (remove)
            {
                EditorGUILayout.EndVertical();
                return null;
            }

            if (!node.Enabled)
                EditorGUILayout.HelpBox("Disabled group references stay in source JSON but are ignored by validation and Runtime Plan export.", MessageType.Info);

            var showPreview = _expandedGroupPreviews.Contains(previewKey);
            var nextPreview = EditorGUILayout.Foldout(showPreview, "Expanded Preview", true);
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
                            failure != null ? failure.Message : "Unable to resolve group reference.",
                            MessageType.Error);
                    }
                }
            }
            EditorGUILayout.EndVertical();
            return node;
        }

        private static void DrawPreviewNode(TriggerNodeData node, int depth)
        {
            if (node == null) return;
            EditorGUILayout.BeginVertical(depth == 0 ? EditorStyles.helpBox : SirenixGUIStyles.BoxContainer);
            EditorGUILayout.LabelField(node.Type ?? "<type>", EditorStyles.miniBoldLabel);
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
            var children = node.Children;
            if (children != null)
            {
                for (var i = 0; i < children.Count; i++) DrawPreviewNode(children[i], depth + 1);
            }
            EditorGUILayout.EndVertical();
        }

        private static string SummarizeValue(TriggerValueRefData value)
        {
            if (value == null) return "<null>";
            if (value.Source != TriggerValueSource.Constant)
            {
                if (value.Source == TriggerValueSource.Expression) return "Expression: " + value.Expression;
                return GetSourceName(value.Source) + ": " + value.Path;
            }
            switch (value.Type)
            {
                case TriggerValueType.Integer:
                case TriggerValueType.Entity:
                case TriggerValueType.ObjectId: return value.IntegerValue.ToString();
                case TriggerValueType.Number: return value.NumberValue.ToString("G");
                case TriggerValueType.Boolean: return value.BooleanValue.ToString();
                case TriggerValueType.String: return value.StringValue ?? string.Empty;
                case TriggerValueType.IntegerList: return value.IntegerListValue != null
                    ? string.Join(",", value.IntegerListValue)
                    : string.Empty;
                case TriggerValueType.Vector3: return value.Vector3Value == null
                    ? "(0, 0, 0)"
                    : $"({value.Vector3Value.X:G}, {value.Vector3Value.Y:G}, {value.Vector3Value.Z:G})";
                default: return value.Type.ToString();
            }
        }

        private void DrawDiagnostics()
        {
            _showDiagnostics = EditorGUILayout.Foldout(
                _showDiagnostics,
                $"Diagnostics ({_platformDiagnostics.Items.Count})",
                true);
            if (!_showDiagnostics) return;

            if (!string.IsNullOrEmpty(_focusedDiagnosticPath))
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.HelpBox("Focused: " + _focusedDiagnosticPath, MessageType.Info);
                if (GUILayout.Button("Clear", EditorStyles.miniButton, GUILayout.Width(44f)))
                {
                    _focusedDiagnosticPath = null;
                    RequestRepaint();
                }
                EditorGUILayout.EndHorizontal();
            }
            if (_platformDiagnostics.Items.Count == 0)
            {
                EditorGUILayout.HelpBox("No diagnostics.", MessageType.Info);
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
                menu.AddDisabledItem(new GUIContent("No Event Catalog"));
            }
            else
            {
                var definitions = _events.Definitions;
                for (var i = 0; i < definitions.Count; i++)
                {
                    var definition = definitions[i];
                    if (definition == null) continue;
                    var family = definition.MatchMode == TriggerEventMatchMode.Prefix ? "Event Families" : definition.Category;
                    var label = family + "/" + (string.IsNullOrWhiteSpace(definition.DisplayName) ? definition.Id : definition.DisplayName);
                    var captured = definition;
                    menu.AddItem(new GUIContent(label), string.Equals(trigger.Event, definition.Id, StringComparison.Ordinal), () =>
                        Edit("Select Trigger Event", () => trigger.Event = captured.Id));
                }
            }
            menu.ShowAsContext();
        }

        private void ShowNodeTypeMenu(TriggerNodeKind kind, Action<TriggerTypeDescriptor> selected, Rect activator)
        {
            void OnType(TriggerTypeDescriptor descriptor) =>
                Edit("Select Trigger Node Type", () => selected(descriptor));
            new TriggerNodeTypeBrowser(_nodeBrowserState, kind, OnType).Show(activator);
        }

        private void ShowNodeCreationMenu(TriggerNodeKind kind, Action<TriggerNodeData> selected, Rect activator)
        {
            void OnType(TriggerTypeDescriptor descriptor) =>
                Edit("Add Trigger Node", () => selected(CreateNode(descriptor)));
            void OnGroup(string groupId) =>
                Edit("Add Trigger Group Reference", () => selected(CreateGroupReference(kind, groupId)));
            new TriggerNodeTypeBrowser(_nodeBrowserState, kind, OnType, GetGroups(kind), OnGroup).Show(activator);
        }

        private void ShowGroupMenu(TriggerNodeKind kind, Action<string> selected)
        {
            var menu = new GenericMenu();
            var groups = GetGroups(kind);
            if (groups == null || groups.Count == 0)
            {
                menu.AddDisabledItem(new GUIContent("No Groups"));
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
                        Edit("Select Trigger Group", () => selected(groupId)));
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
            Edit("Add Trigger", () =>
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
            Edit("Duplicate Trigger", () =>
            {
                var json = JsonUtility.ToJson(new TriggerCloneContainer { Trigger = source });
                var copy = JsonUtility.FromJson<TriggerCloneContainer>(json).Trigger;
                copy.Id = NextTriggerId();
                copy.Name = string.IsNullOrWhiteSpace(copy.Name) ? "Copy" : copy.Name + " Copy";
                triggers.Insert(_selectedTriggerIndex + 1, copy);
                _selectedTriggerIndex++;
            });
        }

        private void DeleteSelectedTrigger()
        {
            var triggers = _asset.Module.Triggers;
            if (triggers == null || _selectedTriggerIndex < 0 || _selectedTriggerIndex >= triggers.Count) return;
            if (!EditorUtility.DisplayDialog("Delete Trigger", "Delete the selected trigger?", "Delete", "Cancel")) return;
            Edit("Delete Trigger", () =>
            {
                triggers.RemoveAt(_selectedTriggerIndex);
                _selectedTriggerIndex = Mathf.Clamp(_selectedTriggerIndex, 0, triggers.Count - 1);
            });
        }

        private TriggerDefinitionData CreateTrigger()
        {
            return new TriggerDefinitionData
            {
                Id = NextTriggerId(),
                Name = "New Trigger",
                GroupPath = "Drafts",
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
            return node;
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
            node.Children = new List<TriggerNodeData>();
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
            node.Children = new List<TriggerNodeData>();
        }

        private void LocalizeGroupReference(TriggerNodeData node, TriggerNodeKind kind)
        {
            if (!TriggerAuthoringGroupResolver.TryExpand(
                    _asset.Module,
                    node,
                    kind,
                    out var expanded,
                    out var failure))
            {
                EditorUtility.DisplayDialog(
                    "Group Expansion Failed",
                    failure != null ? failure.Message : "Unable to resolve group reference.",
                    "OK");
                return;
            }

            Edit("Copy Trigger Group Locally", () => CopyNode(node, expanded));
        }

        private static void CopyNode(TriggerNodeData target, TriggerNodeData source)
        {
            var copy = TriggerAuthoringGroupResolver.CloneNode(source);
            target.Kind = copy.Kind;
            target.GroupReference = copy.GroupReference;
            target.Type = copy.Type;
            target.Note = copy.Note;
            target.Arguments = copy.Arguments;
            target.Children = copy.Children;
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
                    "Export Trigger Source JSON", Application.dataPath, defaultName,
                    TriggerSourceCodecs.ModuleDefault.FileExtension);
                if (string.IsNullOrWhiteSpace(path)) return;
            }

            var result = TriggerAuthoringSourceSync.Export(_asset, path);
            if (!result.Success && result.CanForce && EditorUtility.DisplayDialog(
                    "Trigger Source Conflict", result.Message + "\n\nOverwrite Source JSON?", "Force Export", "Cancel"))
                result = TriggerAuthoringSourceSync.Export(_asset, path, true);
            ShowSyncResult("Export", result);
        }

        private void ImportSource()
        {
            var path = ResolveSourcePath();
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                path = EditorUtility.OpenFilePanel(
                    "Import Trigger Source JSON", Application.dataPath,
                    TriggerSourceCodecs.ModuleDefault.FileExtension);
                if (string.IsNullOrWhiteSpace(path)) return;
            }

            var preview = TriggerAuthoringSourceSync.PreviewImport(_asset, path);
            if (!TriggerAuthoringSourceImportPreviewDialog.Confirm(preview)) return;

            var result = TriggerAuthoringSourceSync.Import(_asset, path, preview.RequiresForce);
            if (!result.Success && result.CanForce && EditorUtility.DisplayDialog(
                    "Trigger Asset Conflict", result.Message + "\n\nOverwrite Asset content?", "Force Import", "Cancel"))
                result = TriggerAuthoringSourceSync.Import(_asset, path, true);
            ShowSyncResult("Import", result);
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
            var path = EditorUtility.SaveFilePanel("Export Runtime Plan JSON", Application.dataPath, defaultName, "json");
            if (string.IsNullOrWhiteSpace(path)) return;

            var result = TriggerAuthoringRuntimeExporter.Export(_asset, path);
            if (result.Success)
            {
                AssetDatabase.Refresh();
                ShowNotification("Runtime export succeeded");
                return;
            }

            _diagnostics = result.Diagnostics;
            EditorUtility.DisplayDialog("Runtime Plan Export Failed", result.BuildMessage(), "OK");
            RequestRepaint();
        }

        private void ShowSyncResult(string operation, TriggerAuthoringSyncResult result)
        {
            _nextSyncInspectionAt = 0d;
            if (result.Success)
            {
                AssetDatabase.SaveAssets();
                ShowNotification(operation + " succeeded");
                return;
            }
            EditorUtility.DisplayDialog("Trigger Source " + operation + " Failed", result.Message, "OK");
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
            _types = TriggerTypeDescriptorCatalog.CreateProjectDefaults();
            var project = _asset != null ? _asset.Project : null;
            _events = TriggerEventDescriptorCatalog.FromAsset(project != null ? project.EventCatalog : null);
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
            _selectedTriggerIndex = index;

            var sectionStart = end + 1;
            if (sectionStart >= path.Length || path[sectionStart] != '.') return;
            var rest = path.Substring(sectionStart + 1);
            var section = rest.Split('.', '[')[0];
            switch (section)
            {
                case "blackboard":
                    _showTriggerBlackboard = true;
                    break;
                case "condition":
                case "actions":
                case "template":
                case "name":
                case "enabled":
                case "event":
                    break;
                default:
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
                Undo.RecordObject(_asset, "Edit Trigger Authoring Module");
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
            var next = 1;
            var triggers = _asset.Module != null ? _asset.Module.Triggers : null;
            if (triggers == null) return next;
            for (var i = 0; i < triggers.Count; i++)
            {
                if (triggers[i] != null) next = Math.Max(next, triggers[i].Id + 1);
            }
            return next;
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
            switch (source)
            {
                case TriggerValueSource.LocalBlackboard: return "Local Blackboard";
                case TriggerValueSource.GlobalBlackboard: return "Global Blackboard";
                case TriggerValueSource.TemplateParameter: return "Template Parameter";
                default: return source.ToString();
            }
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
            if (!string.IsNullOrWhiteSpace(trigger.Event)) return trigger.Event;
            return "Untitled";
        }

        // 约定字符串字段的合法值与 TriggerAuthoringRuntimeExporter 的解析保持一致；
        // 仅 transient/none 当前可导出（TRG2011/TRG2012），下拉只暴露受支持值。
        private static readonly string[] PhaseOptions = { "immediate", "early", "late" };
        private static readonly string[] ScopeOptions = { "owner", "global" };
        private static readonly string[] ScheduleModeOptions = { "transient" };
        private static readonly string[] InterruptPolicyOptions = { "none" };

        private static string DrawConstrainedOption(string label, string value, string[] options)
        {
            var blank = string.IsNullOrWhiteSpace(value);
            var names = new List<string>(options.Length + 1);
            var selected = -1;
            for (var i = 0; i < options.Length; i++)
            {
                names.Add(options[i]);
                if (!blank && string.Equals(options[i], value, StringComparison.OrdinalIgnoreCase)) selected = i;
            }
            if (blank) selected = 0;
            if (selected < 0)
            {
                names.Add(value + "  [unavailable]");
                selected = names.Count - 1;
            }

            var next = EditorGUILayout.Popup(label, selected, names.ToArray());
            if (next == selected) return value;
            return next < options.Length ? options[next] : value;
        }

        private void AssignProject(TriggerAuthoringProjectAsset previous, TriggerAuthoringProjectAsset next)
        {
            if (previous != null) Undo.RecordObject(previous, "Assign Trigger Authoring Project");
            if (next != null) Undo.RecordObject(next, "Assign Trigger Authoring Project");
            TriggerAuthoringProjectMembership.Assign(_asset, next);
            if (previous != null) EditorUtility.SetDirty(previous);
            if (next != null) EditorUtility.SetDirty(next);
        }

        private void CreateAndAssignProject()
        {
            var path = EditorUtility.SaveFilePanelInProject(
                "Create Trigger Authoring Project",
                "TriggerAuthoringProject",
                "asset",
                "Choose where to create the project and its catalogs.");
            if (string.IsNullOrWhiteSpace(path)) return;

            var directory = Path.GetDirectoryName(path)?.Replace('\\', '/') ?? "Assets";
            var baseName = Path.GetFileNameWithoutExtension(path);
            var project = TriggerAuthoringProjectSetup.CreateProjectWithCatalogs(directory, baseName);
            if (project == null) return;

            var previous = _asset.Project;
            Edit("Assign Trigger Authoring Project", () => AssignProject(previous, project));
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
