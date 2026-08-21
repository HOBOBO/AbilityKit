#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using AbilityKit.Ability.Config.Authoring;
using AbilityKit.Ability.Editor.Inspectors;
using AbilityKit.Ability.Editor.Utilities;
using Sirenix.Utilities.Editor;
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

        private TriggerAuthoringModuleAsset _selectedModule;
        private TriggerAuthoringModuleDrawer _moduleDrawer;
        private Vector2 _treeScroll;
        private Vector2 _detailScroll;
        private Vector2 _rightScroll;
        private string _search = string.Empty;
        private TriggerAuthoringSyncInspection _syncInspection;
        private double _nextSyncInspectionAt;
        private TriggerAuthoringProjectValidationResult _validation;
        private TriggerAuthoringProjectAsset _validationProject;
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
            RefreshProjects();
            Selection.selectionChanged += _selectionChangedHandler;
        }

        private void OnDisable()
        {
            Selection.selectionChanged -= _selectionChangedHandler;
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
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("Trigger Authoring", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(new GUIContent("Create Project", "Run the project setup wizard (catalogs + starter module)"), EditorStyles.toolbarButton))
                EditorApplication.ExecuteMenuItem("Assets/AbilityKit/Trigger Authoring/Create MOBA Project Setup");
            if (GUILayout.Button(new GUIContent("Validate All", "Validate every Trigger Authoring project"), EditorStyles.toolbarButton))
            {
                var failures = TriggerAuthoringProjectValidationMenu.ValidateAllProjects(true);
                EditorUtility.DisplayDialog(
                    "Trigger Authoring Project Validation",
                    failures.Count == 0
                        ? "All Trigger Authoring projects are valid."
                        : string.Join(Environment.NewLine, failures.ToArray()),
                    "OK");
            }
            if (GUILayout.Button("Refresh", EditorStyles.toolbarButton))
            {
                RefreshProjects();
                Repaint();
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawTree()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Width(LeftPaneWidth));
            EditorGUILayout.BeginHorizontal();
            _search = EditorGUILayout.TextField(_search ?? string.Empty, EditorStyles.toolbarSearchField);
            if (GUILayout.Button("x", EditorStyles.toolbarButton, GUILayout.Width(20f)) &&
                !string.IsNullOrEmpty(_search))
            {
                _search = string.Empty;
                GUI.FocusControl(null);
            }
            EditorGUILayout.EndHorizontal();

            _treeScroll = EditorGUILayout.BeginScrollView(_treeScroll);
            var filter = (_search ?? string.Empty).Trim();
            for (var i = 0; i < _projects.Count; i++)
            {
                var project = _projects[i];
                if (project == null) continue;
                GUILayout.Label(project.name, EditorStyles.boldLabel);
                var modules = project.Modules;
                for (var m = 0; m < modules.Count; m++)
                {
                    if (modules[m] != null) DrawModuleRow(modules[m], filter);
                }
            }

            if (_unassignedModules.Count > 0)
            {
                GUILayout.Space(4f);
                GUILayout.Label($"Unassigned Modules ({_unassignedModules.Count})", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox(
                    "These modules are not registered in any project; the build gate skips them.",
                    MessageType.Warning);
                for (var i = 0; i < _unassignedModules.Count; i++)
                    DrawModuleRow(_unassignedModules[i], filter);
            }
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawModuleRow(TriggerAuthoringModuleAsset module, string filter)
        {
            var moduleId = module.Module != null ? module.Module.ModuleId : null;
            var summary = string.IsNullOrWhiteSpace(moduleId) ? module.name : moduleId;
            var triggerCount = module.Module != null && module.Module.Triggers != null
                ? module.Module.Triggers.Count
                : 0;
            var label = summary + "  (" + triggerCount + ")";
            if (filter.Length > 0 &&
                label.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0 &&
                module.name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0)
                return;

            var oldBackground = GUI.backgroundColor;
            if (module == _selectedModule) GUI.backgroundColor = new Color(0.42f, 0.66f, 0.92f);
            if (GUILayout.Button(label, EditorStyles.miniButton, GUILayout.Height(24f)))
                SelectModule(module, true);
            GUI.backgroundColor = oldBackground;
        }

        private void DrawCenter()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            if (_selectedModule == null)
            {
                EditorGUILayout.HelpBox(
                    "Select a module on the left to start editing.\n" +
                    "No project yet? Use Create Project in the toolbar (catalogs + starter module).",
                    MessageType.Info);
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndVertical();
                return;
            }

            _detailScroll = EditorGUILayout.BeginScrollView(_detailScroll);
            if (_moduleDrawer != null) _moduleDrawer.Draw();
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
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
            SirenixEditorGUI.BeginBox("Source Sync");
            if (_selectedModule == null)
            {
                EditorGUILayout.LabelField("No module selected.", EditorStyles.miniLabel);
                SirenixEditorGUI.EndBox();
                return;
            }

            if (_syncInspection == null || _nextSyncInspectionAt <= EditorApplication.timeSinceStartup)
            {
                _syncInspection = TriggerAuthoringSourceSync.Inspect(_selectedModule);
                _nextSyncInspectionAt = EditorApplication.timeSinceStartup + 0.5d;
            }

            var inspection = _syncInspection;
            var oldColor = GUI.color;
            GUI.color = GetSyncColor(inspection.State);
            GUILayout.Label(inspection.State.ToString(), EditorStyles.boldLabel);
            GUI.color = oldColor;
            if (inspection.State == TriggerAuthoringSyncState.JsonChanged)
                EditorGUILayout.HelpBox("External changes detected. Use Import to apply.", MessageType.Warning);
            EditorGUILayout.LabelField(
                "Path",
                string.IsNullOrEmpty(inspection.SourcePath) ? "<unbound>" : inspection.SourcePath,
                EditorStyles.miniLabel);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Import", EditorStyles.miniButtonLeft)) ImportSource();
            if (GUILayout.Button("Export", EditorStyles.miniButtonRight)) ExportSource();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(inspection.SourcePath)))
            {
                if (GUILayout.Button("Copy Path", EditorStyles.miniButtonLeft))
                    EditorGUIUtility.systemCopyBuffer = inspection.SourcePath ?? string.Empty;
                if (GUILayout.Button("Reveal", EditorStyles.miniButtonRight))
                    EditorUtility.RevealInFinder(inspection.SourcePath);
            }
            EditorGUILayout.EndHorizontal();
            SirenixEditorGUI.EndBox();
        }

        private void DrawValidationCard()
        {
            SirenixEditorGUI.BeginBox("Project Validation");
            var project = _selectedModule != null ? _selectedModule.Project : null;
            if (project == null)
            {
                EditorGUILayout.HelpBox("The selected module is not registered in a project.", MessageType.Warning);
                SirenixEditorGUI.EndBox();
                return;
            }

            EditorGUILayout.LabelField(project.name, EditorStyles.miniBoldLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Validate", EditorStyles.miniButtonLeft))
            {
                _validation = TriggerAuthoringProjectValidator.Validate(project);
                _validationProject = project;
            }
            if (GUILayout.Button("Export Runtime", EditorStyles.miniButtonRight))
            {
                var result = TriggerAuthoringProjectExport.ExportAll(project);
                var message = "[TriggerAuthoring] Project runtime export " +
                              (result.Success ? "succeeded. " : "failed. ") + result.BuildMessage();
                if (result.Success) Debug.Log(message, project);
                else Debug.LogError(message, project);
                EditorUtility.DisplayDialog("Project Runtime Export", result.BuildMessage(), "OK");
            }
            EditorGUILayout.EndHorizontal();

            if (_validation != null && _validationProject == project)
            {
                var errors = 0;
                var warnings = 0;
                for (var i = 0; i < _validation.Diagnostics.Count; i++)
                {
                    if (_validation.Diagnostics[i].Severity == TriggerAuthoringDiagnosticSeverity.Error) errors++;
                    else if (_validation.Diagnostics[i].Severity == TriggerAuthoringDiagnosticSeverity.Warning) warnings++;
                }

                EditorGUILayout.LabelField(
                    $"{_validation.ModuleCount} modules, {_validation.TemplateCount} templates  (E{errors} W{warnings})",
                    EditorStyles.miniLabel);
                for (var i = 0; i < _validation.Diagnostics.Count; i++)
                {
                    var diagnostic = _validation.Diagnostics[i];
                    EditorGUILayout.HelpBox(
                        diagnostic.Code + " " + diagnostic.Path + ": " + diagnostic.Message,
                        diagnostic.Severity == TriggerAuthoringDiagnosticSeverity.Error
                            ? MessageType.Error
                            : MessageType.Warning);
                }
            }
            else
            {
                EditorGUILayout.LabelField("Not validated yet.", EditorStyles.miniLabel);
            }
            SirenixEditorGUI.EndBox();
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
            _nextSyncInspectionAt = 0d;
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
            _nextSyncInspectionAt = 0d;
            Repaint();
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
            _syncInspection = null;
            _nextSyncInspectionAt = 0d;
            if (syncSelection) Selection.activeObject = module;
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
    }
}
#endif
