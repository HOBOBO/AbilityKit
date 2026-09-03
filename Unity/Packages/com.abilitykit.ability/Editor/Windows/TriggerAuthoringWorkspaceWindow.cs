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
        private const float LeftPaneWidth = 250f;
        private const float RightPaneWidth = 262f;
        private const float RightPaneThreshold = 960f;

        private readonly List<TriggerAuthoringProjectAsset> _projects = new List<TriggerAuthoringProjectAsset>();
        private readonly List<TriggerAuthoringModuleAsset> _unassignedModules = new List<TriggerAuthoringModuleAsset>();
        private readonly EditorCommandRegistry _commands = new EditorCommandRegistry();
        private readonly List<IDisposable> _commandRegistrations = new List<IDisposable>();
        private readonly TriggerAuthoringProjectTreePanel _projectTreePanel = new TriggerAuthoringProjectTreePanel();
        private readonly TriggerAuthoringModuleContentPanel _moduleContentPanel = new TriggerAuthoringModuleContentPanel();
        private readonly TriggerAuthoringSourceSyncPanel _sourceSyncPanel = new TriggerAuthoringSourceSyncPanel();
        private readonly TriggerAuthoringProjectValidationPanel _validationPanel = new TriggerAuthoringProjectValidationPanel();

        private TriggerAuthoringModuleAsset _selectedModule;
        private TriggerAuthoringModuleDrawer _moduleDrawer;
        private Vector2 _rightScroll;
        private TriggerAuthoringProjectValidationResult _validation;
        private TriggerAuthoringProjectAsset _validationProject;
        private EditorDiagnosticCollection _platformDiagnostics = new EditorDiagnosticCollection();
        private readonly Action _selectionChangedHandler;

        public TriggerAuthoringWorkspaceWindow()
        {
            _selectionChangedHandler = OnSelectionChanged;
        }

        [MenuItem("Window/AbilityKit/Trigger Authoring Workspace")]
        private static void Open()
        {
            var window = GetWindow<TriggerAuthoringWorkspaceWindow>();
            window.titleContent = new GUIContent("Trigger Authoring");
            window.minSize = new Vector2(720f, 480f);
        }

        private void OnEnable()
        {
            RegisterCommands();
            RefreshProjects();
            Selection.selectionChanged += _selectionChangedHandler;
        }

        private void OnDisable()
        {
            Selection.selectionChanged -= _selectionChangedHandler;
            DisposeModuleDrawer();
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
            if (module == null || module == _selectedModule) return;
            SelectModule(module, false);
            Repaint();
        }

        private void OnGUI()
        {
            DrawToolbar();
            EditorGUILayout.BeginHorizontal();
            DrawTree();
            DrawCenter();
            if (position.width >= RightPaneThreshold) DrawRightPane();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawToolbar()
        {
            EditorImGuiControls.DrawCommandToolbar(
                _commands,
                TriggerAuthoringEditorIntegration.Localization,
                new EditorCommandContext(this, _selectedModule),
                command => command.Id == TriggerAuthoringCommandIds.CreateProject ||
                           command.Id == TriggerAuthoringCommandIds.ValidateAll ||
                           command.Id == TriggerAuthoringCommandIds.Refresh);
        }

        private void RegisterCommands()
        {
            if (_commandRegistrations.Count > 0) return;
            var commands = TriggerAuthoringCommandFactory.CreateWorkspace(
                () => EditorApplication.ExecuteMenuItem(
                    "Assets/AbilityKit/Trigger Authoring/Create MOBA Project Setup"),
                ValidateAllProjects,
                () =>
                {
                    RefreshProjects();
                    Repaint();
                },
                ValidateSelectedProject,
                ExportSelectedProject,
                () => _selectedModule != null && _selectedModule.Project != null);
            for (var i = 0; i < commands.Count; i++)
                _commandRegistrations.Add(_commands.Register(commands[i]));
        }

        private static void ValidateAllProjects()
        {
            var failures = TriggerAuthoringProjectValidationMenu.ValidateAllProjects(true);
            EditorUtility.DisplayDialog(
                "Trigger Authoring Project Validation",
                failures.Count == 0
                    ? "All Trigger Authoring projects are valid."
                    : string.Join(Environment.NewLine, failures.ToArray()),
                "OK");
        }

        private void DrawTree()
        {
            _projectTreePanel.Draw(
                _projects,
                _unassignedModules,
                _selectedModule,
                module => SelectModule(module, true),
                LeftPaneWidth);
        }

        private void DrawCenter()
        {
            _moduleContentPanel.Draw(_selectedModule, _moduleDrawer);
        }

        private void DrawRightPane()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Width(RightPaneWidth));
            _rightScroll = EditorGUILayout.BeginScrollView(_rightScroll);
            DrawSyncCard();
            GUILayout.Space(6f);
            DrawValidationCard();
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
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
                    "Import Trigger Source JSON",
                    Application.dataPath,
                    TriggerSourceCodecs.ModuleDefault.FileExtension);
                if (string.IsNullOrWhiteSpace(path)) return;
            }

            var result = TriggerAuthoringSourceSync.Import(asset, path);
            if (!result.Success && result.CanForce && EditorUtility.DisplayDialog(
                    "Trigger Asset Conflict",
                    result.Message + "\n\nForce import and overwrite Asset content?",
                    "Force Import",
                    "Cancel"))
                result = TriggerAuthoringSourceSync.Import(asset, path, true);

            if (result.Success)
            {
                AssetDatabase.SaveAssets();
                ShowNotification(new GUIContent("Import succeeded"));
            }
            else
            {
                EditorUtility.DisplayDialog("Trigger Source Import Failed", result.Message, "OK");
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
                    "Export Trigger Source JSON",
                    Application.dataPath,
                    defaultName,
                    TriggerSourceCodecs.ModuleDefault.FileExtension);
                if (string.IsNullOrWhiteSpace(path)) return;
            }

            var result = TriggerAuthoringSourceSync.Export(asset, path);
            if (!result.Success && result.CanForce && EditorUtility.DisplayDialog(
                    "Trigger Source Conflict",
                    result.Message + "\n\nForce export and overwrite Source JSON?",
                    "Force Export",
                    "Cancel"))
                result = TriggerAuthoringSourceSync.Export(asset, path, true);

            if (result.Success)
            {
                AssetDatabase.SaveAssets();
                ShowNotification(new GUIContent("Export succeeded"));
            }
            else
            {
                EditorUtility.DisplayDialog("Trigger Source Export Failed", result.Message, "OK");
            }
            _sourceSyncPanel.Invalidate();
            Repaint();
        }

        private void ValidateSelectedProject()
        {
            var project = _selectedModule != null ? _selectedModule.Project : null;
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
            var project = _selectedModule != null ? _selectedModule.Project : null;
            if (project == null) return;
            var result = TriggerAuthoringProjectExport.ExportAll(project);
            var message = "[TriggerAuthoring] Project runtime export " +
                          (result.Success ? "succeeded. " : "failed. ") + result.BuildMessage();
            if (result.Success) Debug.Log(message, project);
            else Debug.LogError(message, project);
            EditorUtility.DisplayDialog("Project Runtime Export", result.BuildMessage(), "OK");
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
            if (module == _selectedModule)
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
