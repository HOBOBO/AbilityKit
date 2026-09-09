#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using AbilityKit.Ability.Config.Authoring;
using AbilityKit.Ability.Editor.Inspectors;
using AbilityKit.Ability.Editor.Utilities;
using AbilityKit.Editor.Platform.Commands;
using AbilityKit.Editor.Platform.Diagnostics;
using AbilityKit.Editor.Platform.UI;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Ability.Editor.Windows
{
    /// <summary>
    /// 触发器编辑工作台（Y3 式三栏）：
    /// 左栏 = 项目/模块树（含未挂载模块警示）；中栏 = 所选模块的完整编辑器（复用 Module Inspector）；
    /// 右栏 = Source 同步状态卡 + 项目构建校验卡。
    /// </summary>
    internal sealed class TriggerAuthoringWorkspaceWindow : EditorWindow
    {
        private const float DefaultNavigationWidth = 270f;
        private const float DefaultInspectorWidth = 320f;

        private enum FocusedPage
        {
            Modules,
            Editor,
            Review
        }

        private enum ResourcePage
        {
            Modules,
            Templates
        }

        private enum InspectorPage
        {
            Overview,
            Source,
            Validation
        }

        private readonly List<TriggerAuthoringProjectAsset> _projects = new List<TriggerAuthoringProjectAsset>();
        private readonly List<TriggerAuthoringModuleAsset> _unassignedModules = new List<TriggerAuthoringModuleAsset>();
        private readonly List<TriggerAuthoringTemplateAsset> _unassignedTemplates = new List<TriggerAuthoringTemplateAsset>();
        private readonly EditorCommandRegistry _commands = new EditorCommandRegistry();
        private readonly List<IDisposable> _commandRegistrations = new List<IDisposable>();
        private readonly TriggerAuthoringProjectTreePanel _projectTreePanel = new TriggerAuthoringProjectTreePanel();
        private readonly TriggerAuthoringTemplateTreePanel _templateTreePanel = new TriggerAuthoringTemplateTreePanel();
        private readonly TriggerAuthoringModuleContentPanel _moduleContentPanel = new TriggerAuthoringModuleContentPanel();
        private readonly TriggerAuthoringTemplateContentPanel _templateContentPanel = new TriggerAuthoringTemplateContentPanel();
        private readonly TriggerAuthoringSourceSyncPanel _sourceSyncPanel = new TriggerAuthoringSourceSyncPanel();
        private readonly TriggerAuthoringProjectValidationPanel _validationPanel = new TriggerAuthoringProjectValidationPanel();

        private TriggerAuthoringModuleAsset _selectedModule;
        private TriggerAuthoringTemplateAsset _selectedTemplate;
        private TriggerAuthoringModuleDrawer _moduleDrawer;
        private Vector2 _rightScroll;
        private TriggerAuthoringProjectValidationResult _validation;
        private TriggerAuthoringProjectAsset _validationProject;
        private EditorDiagnosticCollection _platformDiagnostics = new EditorDiagnosticCollection();
        private readonly Action _selectionChangedHandler;
        [SerializeField] private float _navigationWidth = DefaultNavigationWidth;
        [SerializeField] private float _inspectorWidth = DefaultInspectorWidth;
        [SerializeField] private FocusedPage _focusedPage = FocusedPage.Editor;
        [SerializeField] private InspectorPage _inspectorPage = InspectorPage.Overview;
        [SerializeField] private ResourcePage _resourcePage = ResourcePage.Modules;
        [SerializeField] private bool _focusEditor;
        private bool _draggingNavigationSplitter;
        private bool _draggingInspectorSplitter;

        public TriggerAuthoringWorkspaceWindow()
        {
            _selectionChangedHandler = OnSelectionChanged;
        }

        [MenuItem("Window/AbilityKit/触发器编辑工作台 %#t")]
        private static void Open()
        {
            var window = GetWindow<TriggerAuthoringWorkspaceWindow>();
            window.titleContent = new GUIContent("触发器编辑器");
            window.minSize = new Vector2(720f, 480f);
        }

        private void OnEnable()
        {
            RegisterCommands();
            RefreshProjects();
            if (_selectedModule != null) SelectModule(_selectedModule, false);
            if (_selectedTemplate != null) SelectTemplate(_selectedTemplate, false);
            Selection.selectionChanged += _selectionChangedHandler;
        }

        private void OnDisable()
        {
            Selection.selectionChanged -= _selectionChangedHandler;
            DisposeModuleDrawer();
            _templateContentPanel.Dispose();
            for (var i = 0; i < _commandRegistrations.Count; i++)
                _commandRegistrations[i].Dispose();
            _commandRegistrations.Clear();
        }

        private void OnFocus()
        {
            RefreshProjects();
            Repaint();
        }

        private void OnSelectionChanged()
        {
            var module = Selection.activeObject as TriggerAuthoringModuleAsset;
            if (module != null)
            {
                _resourcePage = ResourcePage.Modules;
                if (module != _selectedModule) SelectModule(module, false);
                Repaint();
                return;
            }

            var template = Selection.activeObject as TriggerAuthoringTemplateAsset;
            if (template == null) return;
            _resourcePage = ResourcePage.Templates;
            if (template != _selectedTemplate) SelectTemplate(template, false);
            Repaint();
        }

        private void OnGUI()
        {
            HandleKeyboardShortcuts();
            DrawToolbar();
            DrawContextHeader();

            if (_focusEditor)
            {
                DrawCenter(position.width, true);
                return;
            }

            var mode = TriggerAuthoringWorkspaceLayout.Resolve(position.width);
            if (mode == TriggerAuthoringWorkspaceLayoutMode.Focused)
            {
                DrawFocusedLayout();
                return;
            }

            _navigationWidth = TriggerAuthoringWorkspaceLayout.ClampNavigationWidth(_navigationWidth, position.width);
            _inspectorWidth = TriggerAuthoringWorkspaceLayout.ClampInspectorWidth(_inspectorWidth, position.width);
            var showInspector = mode == TriggerAuthoringWorkspaceLayoutMode.Full;
            var centerWidth = position.width - _navigationWidth - TriggerAuthoringWorkspaceLayout.SplitterWidth;
            if (showInspector)
                centerWidth -= _inspectorWidth + TriggerAuthoringWorkspaceLayout.SplitterWidth;

            EditorGUILayout.BeginHorizontal();
            DrawTree(_navigationWidth);
            DrawPaneSplitter(ref _navigationWidth, ref _draggingNavigationSplitter, false);
            DrawCenter(Mathf.Max(360f, centerWidth), true);
            if (showInspector)
            {
                DrawPaneSplitter(ref _inspectorWidth, ref _draggingInspectorSplitter, true);
                DrawRightPane(_inspectorWidth);
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawToolbar()
        {
            EditorImGuiControls.DrawCommandToolbar(
                _commands,
                TriggerAuthoringEditorIntegration.Localization,
                new EditorCommandContext(this, CurrentSelection),
                command => command.Id == TriggerAuthoringCommandIds.CreateProject ||
                           command.Id == TriggerAuthoringCommandIds.ValidateAll ||
                           command.Id == TriggerAuthoringCommandIds.Refresh);
        }

        private void DrawContextHeader()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            if (_resourcePage == ResourcePage.Templates)
            {
                DrawTemplateContextHeader();
                EditorGUILayout.EndHorizontal();
                return;
            }

            if (_selectedModule == null)
            {
                GUILayout.Label("未选择模块", EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                GUILayout.Label(_projects.Count + " 个项目", EditorStyles.miniLabel);
                GUILayout.Label(_unassignedModules.Count + " 个未分配模块", EditorStyles.miniLabel);
                EditorGUILayout.EndHorizontal();
                return;
            }

            var module = _selectedModule.Module;
            var project = _selectedModule.Project;
            var title = module != null && !string.IsNullOrWhiteSpace(module.DisplayName)
                ? module.DisplayName
                : module != null && !string.IsNullOrWhiteSpace(module.ModuleId)
                    ? module.ModuleId
                    : _selectedModule.name;
            GUILayout.Label(new GUIContent(title, AssetDatabase.GetAssetPath(_selectedModule)), EditorStyles.boldLabel);
            if (module != null && !string.IsNullOrWhiteSpace(module.ModuleId) &&
                !string.Equals(title, module.ModuleId, StringComparison.Ordinal))
                GUILayout.Label(module.ModuleId, EditorStyles.miniLabel);
            GUILayout.FlexibleSpace();
            GUILayout.Label(project != null ? project.name : "未分配", EditorStyles.miniLabel);
            GUILayout.Space(8f);
            GUILayout.Label((module != null && module.Triggers != null ? module.Triggers.Count : 0) + " 个触发器", EditorStyles.miniLabel);
            if (_moduleDrawer != null && (_moduleDrawer.DiagnosticErrorCount > 0 || _moduleDrawer.DiagnosticWarningCount > 0))
            {
                GUILayout.Space(8f);
                var previousColor = GUI.color;
                GUI.color = _moduleDrawer.DiagnosticErrorCount > 0
                    ? new Color(1f, 0.48f, 0.44f)
                    : new Color(1f, 0.75f, 0.3f);
                GUILayout.Label(
                    "E" + _moduleDrawer.DiagnosticErrorCount + "  W" + _moduleDrawer.DiagnosticWarningCount,
                    EditorStyles.miniBoldLabel);
                GUI.color = previousColor;
            }
            if (position.width >= TriggerAuthoringWorkspaceLayout.StandardWidthThreshold)
            {
                GUILayout.Space(8f);
                using (new EditorGUI.DisabledScope(project == null))
                {
                    if (GUILayout.Button(new GUIContent("校验项目", "校验所选项目中的全部模块、模板和目录。"), EditorStyles.toolbarButton))
                        ValidateSelectedProject();
                    if (GUILayout.Button(new GUIContent("导出项目", "校验并导出所选项目中的全部 Runtime Plan。"), EditorStyles.toolbarButton))
                        ExportSelectedProject();
                }
            }
            GUILayout.Space(6f);
            var oldBackground = GUI.backgroundColor;
            if (_focusEditor) GUI.backgroundColor = new Color(0.42f, 0.66f, 0.92f);
            var focusIcon = EditorGUIUtility.IconContent("d_ViewToolZoom");
            var focusContent = new GUIContent(
                _focusEditor ? "退出专注" : "专注",
                focusIcon != null ? focusIcon.image : null,
                _focusEditor
                    ? "退出专注编辑模式（Esc）"
                    : "进入专注编辑模式，隐藏导航和检查面板（Ctrl+Shift+F）");
            if (GUILayout.Toggle(_focusEditor, focusContent, EditorStyles.toolbarButton, GUILayout.Width(_focusEditor ? 86f : 64f)) != _focusEditor)
            {
                _focusEditor = !_focusEditor;
                GUI.FocusControl(null);
                Repaint();
            }
            GUI.backgroundColor = oldBackground;
            EditorGUILayout.EndHorizontal();
        }

        private void DrawTemplateContextHeader()
        {
            if (_selectedTemplate == null)
            {
                GUILayout.Label("未选择模板", EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                GUILayout.Label(_projects.Count + " 个项目", EditorStyles.miniLabel);
                GUILayout.Label(_unassignedTemplates.Count + " 个未分配模板", EditorStyles.miniLabel);
                return;
            }

            var template = _selectedTemplate.Template;
            var project = _selectedTemplate.Project;
            var title = template != null && !string.IsNullOrWhiteSpace(template.DisplayName)
                ? template.DisplayName
                : template != null && !string.IsNullOrWhiteSpace(template.TemplateId)
                    ? template.TemplateId
                    : _selectedTemplate.name;
            GUILayout.Label(new GUIContent(title, AssetDatabase.GetAssetPath(_selectedTemplate)), EditorStyles.boldLabel);
            if (template != null && !string.IsNullOrWhiteSpace(template.TemplateId) &&
                !string.Equals(title, template.TemplateId, StringComparison.Ordinal))
                GUILayout.Label(template.TemplateId, EditorStyles.miniLabel);
            GUILayout.FlexibleSpace();
            GUILayout.Label(project != null ? project.name : "未分配", EditorStyles.miniLabel);
            GUILayout.Space(8f);
            var parameterCount = template?.Parameters != null ? template.Parameters.Count : 0;
            GUILayout.Label(parameterCount + " 个调用输入", EditorStyles.miniLabel);
            DrawSharedHeaderCommands(project);
        }

        private void DrawSharedHeaderCommands(TriggerAuthoringProjectAsset project)
        {
            if (position.width >= TriggerAuthoringWorkspaceLayout.StandardWidthThreshold)
            {
                GUILayout.Space(8f);
                using (new EditorGUI.DisabledScope(project == null))
                {
                    if (GUILayout.Button(new GUIContent("校验项目", "校验所选项目中的全部模块、模板和目录。"), EditorStyles.toolbarButton))
                        ValidateSelectedProject();
                    if (GUILayout.Button(new GUIContent("导出项目", "校验并导出所选项目中的全部 Runtime Plan。"), EditorStyles.toolbarButton))
                        ExportSelectedProject();
                }
            }
            GUILayout.Space(6f);
            var oldBackground = GUI.backgroundColor;
            if (_focusEditor) GUI.backgroundColor = new Color(0.42f, 0.66f, 0.92f);
            var focusIcon = EditorGUIUtility.IconContent("d_ViewToolZoom");
            var focusContent = new GUIContent(
                _focusEditor ? "退出专注" : "专注",
                focusIcon != null ? focusIcon.image : null,
                _focusEditor
                    ? "退出专注编辑模式（Esc）"
                    : "进入专注编辑模式，隐藏导航和检查面板（Ctrl+Shift+F）");
            if (GUILayout.Toggle(_focusEditor, focusContent, EditorStyles.toolbarButton, GUILayout.Width(_focusEditor ? 86f : 64f)) != _focusEditor)
            {
                _focusEditor = !_focusEditor;
                GUI.FocusControl(null);
                Repaint();
            }
            GUI.backgroundColor = oldBackground;
        }

        private void HandleKeyboardShortcuts()
        {
            var current = Event.current;
            if (current == null || current.type != EventType.KeyDown) return;
            if (_focusEditor && current.keyCode == KeyCode.Escape)
            {
                _focusEditor = false;
                current.Use();
                Repaint();
                return;
            }

            if ((current.control || current.command) && current.shift && current.keyCode == KeyCode.F)
            {
                _focusEditor = !_focusEditor;
                GUI.FocusControl(null);
                current.Use();
                Repaint();
            }
        }

        private void DrawFocusedLayout()
        {
            var labels = new[] { "资源", "编辑器", "检查" };
            _focusedPage = (FocusedPage)GUILayout.Toolbar((int)_focusedPage, labels, EditorStyles.toolbarButton);
            switch (_focusedPage)
            {
                case FocusedPage.Modules:
                    DrawTree(position.width);
                    break;
                case FocusedPage.Review:
                    DrawRightPane(position.width);
                    break;
                default:
                    DrawCenter(position.width, true);
                    break;
            }
        }

        private void RegisterCommands()
        {
            if (_commandRegistrations.Count > 0) return;
            var commands = TriggerAuthoringCommandFactory.CreateWorkspace(
                () => EditorApplication.ExecuteMenuItem(
                    "Assets/AbilityKit/触发器编辑/创建 MOBA 项目配置"),
                ValidateAllProjects,
                () =>
                {
                    RefreshProjects();
                    Repaint();
                },
                ValidateSelectedProject,
                ExportSelectedProject,
                () => CurrentProject != null);
            for (var i = 0; i < commands.Count; i++)
                _commandRegistrations.Add(_commands.Register(commands[i]));
        }

        private static void ValidateAllProjects()
        {
            var failures = TriggerAuthoringProjectValidationMenu.ValidateAllProjects(true);
            EditorUtility.DisplayDialog(
                "触发器项目校验",
                failures.Count == 0
                    ? "全部触发器项目均已通过校验。"
                    : string.Join(Environment.NewLine, failures.ToArray()),
                "确定");
        }

        private void DrawTree(float width)
        {
            if (_resourcePage == ResourcePage.Templates)
            {
                _templateTreePanel.Draw(
                    _projects,
                    _unassignedTemplates,
                    _selectedTemplate,
                    template =>
                    {
                        SelectTemplate(template, true);
                        _focusedPage = FocusedPage.Editor;
                    },
                    CreateTemplateFromToolbar,
                    CreateTemplate,
                    () =>
                    {
                        _resourcePage = ResourcePage.Modules;
                        Repaint();
                    },
                    width);
                return;
            }

            _projectTreePanel.Draw(
                _projects,
                _unassignedModules,
                _selectedModule,
                module =>
                {
                    SelectModule(module, true);
                    _focusedPage = FocusedPage.Editor;
                },
                () =>
                {
                    _resourcePage = ResourcePage.Templates;
                    Repaint();
                },
                width);
        }

        private void DrawCenter(float width, bool showEmbeddedDiagnostics)
        {
            if (_resourcePage == ResourcePage.Templates)
            {
                _templateContentPanel.Draw(_selectedTemplate);
                return;
            }
            _moduleContentPanel.Draw(_selectedModule, _moduleDrawer, width, showEmbeddedDiagnostics);
        }

        private void DrawRightPane(float width)
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(width), GUILayout.ExpandHeight(true));
            if (_resourcePage == ResourcePage.Templates)
            {
                _rightScroll = EditorGUILayout.BeginScrollView(_rightScroll);
                DrawTemplateOverview();
                EditorGUILayout.EndScrollView();
                EditorGUILayout.EndVertical();
                return;
            }
            _inspectorPage = (InspectorPage)GUILayout.Toolbar(
                (int)_inspectorPage,
                new[] { "概览", "源同步", "校验" },
                EditorStyles.toolbarButton);
            _rightScroll = EditorGUILayout.BeginScrollView(_rightScroll);
            switch (_inspectorPage)
            {
                case InspectorPage.Source:
                    DrawSyncCard();
                    break;
                case InspectorPage.Validation:
                    DrawValidationCard();
                    break;
                default:
                    DrawOverview();
                    break;
            }
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawTemplateOverview()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label("模板概览", EditorStyles.boldLabel);
            if (_selectedTemplate == null)
            {
                EditorGUILayout.HelpBox("请选择一个模板。", MessageType.Info);
                EditorGUILayout.EndVertical();
                return;
            }

            var template = _selectedTemplate.Template;
            var definition = TriggerAuthoringTemplateDefinition.Get(template);
            DrawOverviewMetric("所属项目", _selectedTemplate.Project != null ? _selectedTemplate.Project.name : "未分配");
            DrawOverviewMetric("模板 ID", template != null ? template.TemplateId : string.Empty);
            DrawOverviewMetric("版本", template != null ? template.TemplateVersion : string.Empty);
            DrawOverviewMetric("入口模式", definition != null && definition.EntryMode == TriggerEntryMode.Callable ? "仅供调用" : "事件触发");
            DrawOverviewMetric("触发事件", definition != null ? definition.Event : string.Empty);
            DrawOverviewMetric("LocalVar", definition?.Blackboard != null ? definition.Blackboard.Count.ToString() : "0");
            var parameters = template?.Parameters;
            DrawOverviewMetric("调用输入", parameters != null ? parameters.Count.ToString() : "0");
            DrawOverviewMetric("必填输入", CountRequiredTemplateInputs(parameters).ToString());
            var referenceCount = template != null && _selectedTemplate.Project != null
                ? TriggerAuthoringReferenceFinder.FindTemplateReferences(_selectedTemplate.Project, template.TemplateId).Count
                : 0;
            DrawOverviewMetric("引用次数", referenceCount.ToString());
            GUILayout.Space(4f);
            if (GUILayout.Button("在 Project 中定位", EditorStyles.miniButton))
                EditorGUIUtility.PingObject(_selectedTemplate);
            EditorGUILayout.EndVertical();
        }

        private void DrawOverview()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label("模块状态", EditorStyles.boldLabel);
            if (_selectedModule == null)
            {
                EditorGUILayout.HelpBox("请选择一个模块以检查其构建就绪状态。", MessageType.Info);
                EditorGUILayout.EndVertical();
                return;
            }

            var module = _selectedModule.Module;
            var project = _selectedModule.Project;
            DrawOverviewMetric("所属项目", project != null ? project.name : "未分配");
            DrawOverviewMetric("模块 ID", module != null ? module.ModuleId : string.Empty);
            DrawOverviewMetric("模块类型", module != null ? TriggerAuthoringEditorLabels.ModuleKind(module.Kind) : "未知");
            DrawOverviewMetric("触发器", module != null && module.Triggers != null ? module.Triggers.Count.ToString() : "0");
            DrawOverviewMetric("源文件", _selectedModule.SourceJsonPath ?? string.Empty);
            GUILayout.Space(4f);

            if (project == null)
                EditorGUILayout.HelpBox("此模块尚未分配到项目，在完成分配前不会参与项目校验和运行时导出。", MessageType.Warning);
            else if (_validation == null || _validationProject != project)
                EditorGUILayout.HelpBox("当前工作台会话中尚未执行项目校验。", MessageType.Info);
            else
                EditorGUILayout.HelpBox(
                    _platformDiagnostics.ErrorCount == 0
                        ? "项目校验已通过，包含 " + _platformDiagnostics.WarningCount + " 个警告。"
                        : "项目校验发现 " + _platformDiagnostics.ErrorCount + " 个错误和 " + _platformDiagnostics.WarningCount + " 个警告。",
                    _platformDiagnostics.ErrorCount == 0 ? MessageType.Info : MessageType.Error);

            using (new EditorGUI.DisabledScope(project == null))
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("校验", EditorStyles.miniButtonLeft)) ValidateSelectedProject();
                if (GUILayout.Button("导出运行时", EditorStyles.miniButtonRight)) ExportSelectedProject();
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndVertical();
        }

        private static void DrawOverviewMetric(string label, string value)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(label, EditorStyles.miniLabel, GUILayout.Width(72f));
            GUILayout.Label(string.IsNullOrWhiteSpace(value) ? "-" : value, EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawPaneSplitter(ref float paneWidth, ref bool dragging, bool rightPane)
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
                dragging = true;
                Event.current.Use();
            }
            else if (dragging && Event.current.type == EventType.MouseDrag)
            {
                paneWidth += rightPane ? -Event.current.delta.x : Event.current.delta.x;
                paneWidth = rightPane
                    ? TriggerAuthoringWorkspaceLayout.ClampInspectorWidth(paneWidth, position.width)
                    : TriggerAuthoringWorkspaceLayout.ClampNavigationWidth(paneWidth, position.width);
                Repaint();
                Event.current.Use();
            }
            else if (dragging && Event.current.rawType == EventType.MouseUp)
            {
                dragging = false;
                Event.current.Use();
            }
        }

        private void DrawSyncCard()
        {
            _sourceSyncPanel.Draw(_selectedModule, ImportSource, ExportSource);
        }

        private void DrawValidationCard()
        {
            _validationPanel.Draw(
                _selectedModule,
                _validation,
                _validationProject,
                _platformDiagnostics,
                _commands,
                this);
        }

        private void ImportSource()
        {
            var asset = _selectedModule;
            if (asset == null) return;
            var path = ResolveSourcePath(asset);
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                path = EditorUtility.OpenFilePanel(
                    "导入触发器 Source JSON",
                    Application.dataPath,
                    TriggerSourceCodecs.ModuleDefault.FileExtension);
                if (string.IsNullOrWhiteSpace(path)) return;
            }

            var preview = TriggerAuthoringSourceSync.PreviewImport(asset, path);
            if (!TriggerAuthoringSourceImportPreviewDialog.Confirm(preview)) return;

            var result = TriggerAuthoringSourceSync.Import(asset, path, preview.RequiresForce);
            if (!result.Success && result.CanForce && EditorUtility.DisplayDialog(
                    "触发器资产冲突",
                    result.Message + "\n\n是否强制导入并覆盖资产内容？",
                    "强制导入",
                    "取消"))
                result = TriggerAuthoringSourceSync.Import(asset, path, true);

            if (result.Success)
            {
                AssetDatabase.SaveAssets();
                ShowNotification(new GUIContent("导入成功"));
            }
            else
            {
                EditorUtility.DisplayDialog("触发器源文件导入失败", result.Message, "确定");
            }
            _sourceSyncPanel.Invalidate();
            Repaint();
        }

        private void ExportSource()
        {
            var asset = _selectedModule;
            if (asset == null) return;
            var path = ResolveSourcePath(asset);
            if (string.IsNullOrWhiteSpace(path))
            {
                var defaultName = asset.Module != null && !string.IsNullOrWhiteSpace(asset.Module.ModuleId)
                    ? asset.Module.ModuleId
                    : asset.name;
                path = EditorUtility.SaveFilePanel(
                    "导出触发器 Source JSON",
                    Application.dataPath,
                    defaultName,
                    TriggerSourceCodecs.ModuleDefault.FileExtension);
                if (string.IsNullOrWhiteSpace(path)) return;
            }

            var result = TriggerAuthoringSourceSync.Export(asset, path);
            if (!result.Success && result.CanForce && EditorUtility.DisplayDialog(
                    "触发器源文件冲突",
                    result.Message + "\n\n是否强制导出并覆盖 Source JSON？",
                    "强制导出",
                    "取消"))
                result = TriggerAuthoringSourceSync.Export(asset, path, true);

            if (result.Success)
            {
                AssetDatabase.SaveAssets();
                ShowNotification(new GUIContent("导出成功"));
            }
            else
            {
                EditorUtility.DisplayDialog("触发器源文件导出失败", result.Message, "确定");
            }
            _sourceSyncPanel.Invalidate();
            Repaint();
        }

        private void ValidateSelectedProject()
        {
            var project = CurrentProject;
            if (project == null) return;
            _validation = TriggerAuthoringProjectValidator.Validate(project);
            _validationProject = project;
            _platformDiagnostics = TriggerAuthoringDiagnosticAdapter.Adapt(
                _validation.Diagnostics,
                project,
                LocateProjectDiagnostic);
            Repaint();
        }

        private void ExportSelectedProject()
        {
            var project = CurrentProject;
            if (project == null) return;
            var result = TriggerAuthoringProjectExport.ExportAll(project);
            var message = "[TriggerAuthoring] Project runtime export " +
                          (result.Success ? "succeeded. " : "failed. ") + result.BuildMessage();
            if (result.Success) Debug.Log(message, project);
            else Debug.LogError(message, project);
            EditorUtility.DisplayDialog("项目运行时导出", result.BuildMessage(), "确定");
        }

        private void LocateProjectDiagnostic(string path)
        {
            if (_validationProject == null) return;

            UnityEngine.Object target = _validationProject;
            if (TryReadPathIndex(path, "project.modules[", out var moduleIndex) &&
                moduleIndex >= 0 &&
                moduleIndex < _validationProject.Modules.Count &&
                _validationProject.Modules[moduleIndex] != null)
            {
                target = _validationProject.Modules[moduleIndex];
            }
            else if (TryReadPathIndex(path, "project.templates[", out var templateIndex) &&
                     _validationProject.TemplateCatalog != null &&
                     templateIndex >= 0 &&
                     templateIndex < _validationProject.TemplateCatalog.Templates.Count &&
                     _validationProject.TemplateCatalog.Templates[templateIndex] != null)
            {
                target = _validationProject.TemplateCatalog.Templates[templateIndex];
            }
            else if (!string.IsNullOrEmpty(path) &&
                     path.StartsWith("project.eventCatalog", StringComparison.Ordinal) &&
                     _validationProject.EventCatalog != null)
            {
                target = _validationProject.EventCatalog;
            }
            else if (!string.IsNullOrEmpty(path) &&
                     path.StartsWith("project.globalBlackboardCatalog", StringComparison.Ordinal) &&
                     _validationProject.GlobalBlackboardCatalog != null)
            {
                target = _validationProject.GlobalBlackboardCatalog;
            }
            else if (!string.IsNullOrEmpty(path) &&
                     path.StartsWith("project.templateCatalog", StringComparison.Ordinal) &&
                     _validationProject.TemplateCatalog != null)
            {
                target = _validationProject.TemplateCatalog;
            }

            Selection.activeObject = target;
            EditorGUIUtility.PingObject(target);
        }

        private static bool TryReadPathIndex(
            string path,
            string prefix,
            out int index)
        {
            index = -1;
            if (string.IsNullOrEmpty(path)) return false;
            var start = path.IndexOf(prefix, StringComparison.Ordinal);
            if (start < 0) return false;
            start += prefix.Length;
            var end = path.IndexOf(']', start);
            return end > start &&
                   int.TryParse(path.Substring(start, end - start), out index);
        }

        private void SelectModule(TriggerAuthoringModuleAsset module, bool syncSelection)
        {
            _resourcePage = ResourcePage.Modules;
            if (module == _selectedModule && _moduleDrawer != null)
            {
                if (syncSelection) Selection.activeObject = module;
                return;
            }

            _selectedModule = module;
            if (_moduleDrawer == null)
            {
                _moduleDrawer = new TriggerAuthoringModuleDrawer(module);
                _moduleDrawer.RepaintRequested += Repaint;
            }
            else
            {
                _moduleDrawer.SetAsset(module);
            }

            _validation = null;
            _validationProject = null;
            _platformDiagnostics.Clear();
            _sourceSyncPanel.Invalidate();
            if (syncSelection) Selection.activeObject = module;
        }

        private void SelectTemplate(TriggerAuthoringTemplateAsset template, bool syncSelection)
        {
            _resourcePage = ResourcePage.Templates;
            _selectedTemplate = template;
            _validation = null;
            _validationProject = null;
            _platformDiagnostics.Clear();
            if (syncSelection) Selection.activeObject = template;
        }

        private void CreateTemplate(TriggerAuthoringProjectAsset project)
        {
            var template = TriggerAuthoringProjectSetup.CreateTemplateForProject(project);
            if (template == null) return;
            RefreshProjects();
            SelectTemplate(template, true);
            _focusedPage = FocusedPage.Editor;
            Repaint();
        }

        private void CreateTemplateFromToolbar()
        {
            var availableProjects = new List<TriggerAuthoringProjectAsset>();
            for (var i = 0; i < _projects.Count; i++)
            {
                var project = _projects[i];
                if (project != null && project.TemplateCatalog != null)
                    availableProjects.Add(project);
            }

            if (availableProjects.Count == 0)
            {
                if (_projects.Count > 0)
                {
                    EditorUtility.DisplayDialog(
                        "创建触发器模板",
                        "当前项目均未配置模板目录，请先在项目资产中配置模板目录。",
                        "确定");
                    return;
                }

                if (!EditorUtility.DisplayDialog(
                        "创建触发器模板",
                        "当前没有触发器项目。创建模板前需要先创建项目，是否现在创建？",
                        "创建项目",
                        "取消"))
                    return;

                EditorApplication.ExecuteMenuItem("Assets/AbilityKit/触发器编辑/创建 MOBA 项目配置");
                RefreshProjects();
                Repaint();
                return;
            }

            if (availableProjects.Count == 1)
            {
                CreateTemplate(availableProjects[0]);
                return;
            }

            var menu = new GenericMenu();
            for (var i = 0; i < availableProjects.Count; i++)
            {
                var project = availableProjects[i];
                menu.AddItem(
                    new GUIContent(project.name),
                    false,
                    () => CreateTemplate(project));
            }
            menu.ShowAsContext();
        }

        private void DisposeModuleDrawer()
        {
            if (_moduleDrawer == null) return;
            _moduleDrawer.RepaintRequested -= Repaint;
            _moduleDrawer.Dispose();
            _moduleDrawer = null;
        }

        private void RefreshProjects()
        {
            _projects.Clear();
            var guids = AssetDatabase.FindAssets("t:TriggerAuthoringProjectAsset");
            Array.Sort(guids, StringComparer.Ordinal);
            for (var i = 0; i < guids.Length; i++)
            {
                var project = AssetDatabase.LoadAssetAtPath<TriggerAuthoringProjectAsset>(
                    AssetDatabase.GUIDToAssetPath(guids[i]));
                if (project != null) _projects.Add(project);
            }

            _unassignedModules.Clear();
            var assigned = new HashSet<TriggerAuthoringModuleAsset>();
            for (var i = 0; i < _projects.Count; i++)
            {
                var modules = _projects[i].Modules;
                if (modules == null) continue;
                for (var m = 0; m < modules.Count; m++)
                    if (modules[m] != null) assigned.Add(modules[m]);
            }

            var moduleGuids = AssetDatabase.FindAssets("t:TriggerAuthoringModuleAsset");
            Array.Sort(moduleGuids, StringComparer.Ordinal);
            for (var i = 0; i < moduleGuids.Length; i++)
            {
                var module = AssetDatabase.LoadAssetAtPath<TriggerAuthoringModuleAsset>(
                    AssetDatabase.GUIDToAssetPath(moduleGuids[i]));
                if (module != null && !assigned.Contains(module)) _unassignedModules.Add(module);
            }

            _unassignedTemplates.Clear();
            var assignedTemplates = new HashSet<TriggerAuthoringTemplateAsset>();
            for (var i = 0; i < _projects.Count; i++)
            {
                var templates = _projects[i].TemplateCatalog != null
                    ? _projects[i].TemplateCatalog.Templates
                    : null;
                if (templates == null) continue;
                for (var t = 0; t < templates.Count; t++)
                    if (templates[t] != null) assignedTemplates.Add(templates[t]);
            }

            var templateGuids = AssetDatabase.FindAssets("t:TriggerAuthoringTemplateAsset");
            Array.Sort(templateGuids, StringComparer.Ordinal);
            for (var i = 0; i < templateGuids.Length; i++)
            {
                var template = AssetDatabase.LoadAssetAtPath<TriggerAuthoringTemplateAsset>(
                    AssetDatabase.GUIDToAssetPath(templateGuids[i]));
                if (template != null && !assignedTemplates.Contains(template)) _unassignedTemplates.Add(template);
            }

            // Domain reload may preserve the selected asset while disposing the non-serialized drawer.
            if (_selectedModule != null && _moduleDrawer == null)
                SelectModule(_selectedModule, false);
        }

        private TriggerAuthoringProjectAsset CurrentProject =>
            _resourcePage == ResourcePage.Templates
                ? _selectedTemplate != null ? _selectedTemplate.Project : null
                : _selectedModule != null ? _selectedModule.Project : null;

        private object CurrentSelection =>
            _resourcePage == ResourcePage.Templates
                ? (object)_selectedTemplate
                : _selectedModule;

        private static int CountRequiredTemplateInputs(
            IReadOnlyList<TriggerAuthoringTemplateParameterData> parameters)
        {
            if (parameters == null) return 0;
            var count = 0;
            for (var i = 0; i < parameters.Count; i++)
                if (parameters[i] != null && parameters[i].Required && !parameters[i].HasDefault) count++;
            return count;
        }

        private static string ResolveSourcePath(TriggerAuthoringModuleAsset asset)
        {
            if (string.IsNullOrWhiteSpace(asset.SourceJsonPath)) return string.Empty;
            if (Path.IsPathRooted(asset.SourceJsonPath)) return Path.GetFullPath(asset.SourceJsonPath);
            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            return Path.GetFullPath(Path.Combine(projectRoot, asset.SourceJsonPath));
        }
    }
}
#endif
