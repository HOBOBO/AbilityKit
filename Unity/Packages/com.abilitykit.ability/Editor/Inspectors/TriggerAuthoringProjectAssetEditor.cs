#if UNITY_EDITOR
using System;
using System.IO;
using AbilityKit.Ability.Editor.Utilities;
using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities.Editor;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Ability.Editor.Inspectors
{
    /// <summary>
    /// Project 资产的工作区入口：维护目录引用和模块清单。
    /// 模块清单是构建门禁（TriggerAuthoringProjectValidator / Player Build 阻断）的唯一输入，
    /// 必须通过 TriggerAuthoringProjectMembership 双向登记，不能只改单侧引用。
    /// </summary>
    [CustomEditor(typeof(TriggerAuthoringProjectAsset))]
    internal sealed class TriggerAuthoringProjectAssetEditor : OdinEditor
    {
        private TriggerAuthoringProjectAsset _asset;

        protected override void OnEnable()
        {
            base.OnEnable();
            _asset = target as TriggerAuthoringProjectAsset;
        }

        public override void OnInspectorGUI()
        {
            if (_asset == null) return;

            serializedObject.Update();
            DrawCatalogs();
            GUILayout.Space(4f);
            DrawModules();
            DrawRuntimeExport();
            DrawValidation();
        }

        private void DrawCatalogs()
        {
            SirenixEditorGUI.BeginBox("Catalogs");
            using (var scope = new EditorGUI.ChangeCheckScope())
            {
                EditorGUILayout.PropertyField(
                    serializedObject.FindProperty("_eventCatalog"), new GUIContent("Event Catalog"));
                EditorGUILayout.PropertyField(
                    serializedObject.FindProperty("_globalBlackboardCatalog"), new GUIContent("Global Blackboard Catalog"));
                EditorGUILayout.PropertyField(
                    serializedObject.FindProperty("_templateCatalog"), new GUIContent("Template Catalog"));
                if (scope.changed) serializedObject.ApplyModifiedProperties();
            }
            SirenixEditorGUI.EndBox();
        }

        private void DrawModules()
        {
            var modules = _asset.Modules;
            SirenixEditorGUI.BeginBox($"Modules ({modules.Count})");

            for (var i = 0; i < modules.Count; i++)
            {
                var index = i;
                var module = modules[i];
                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                if (module == null)
                {
                    GUILayout.Label("<missing module reference>", EditorStyles.miniBoldLabel);
                }
                else
                {
                    var summary = module.Module != null && !string.IsNullOrWhiteSpace(module.Module.ModuleId)
                        ? module.Module.ModuleId
                        : module.name;
                    GUILayout.Label(summary, EditorStyles.miniBoldLabel);
                    GUILayout.FlexibleSpace();
                    GUILayout.Label(
                        module.Module != null ? module.Module.Kind.ToString() : string.Empty,
                        EditorStyles.miniLabel,
                        GUILayout.Width(76f));
                    GUILayout.Label(
                        (module.Module != null && module.Module.Triggers != null ? module.Module.Triggers.Count : 0) + " triggers",
                        EditorStyles.miniLabel,
                        GUILayout.Width(70f));
                    if (SirenixEditorGUI.ToolbarButton(new GUIContent("Open", "Select this module asset")))
                    {
                        Selection.activeObject = module;
                        EditorGUIUtility.PingObject(module);
                    }
                }
                if (GUILayout.Button("x", EditorStyles.miniButton, GUILayout.Width(22f)))
                    RemoveModuleAt(index);
                EditorGUILayout.EndHorizontal();
            }

            GUILayout.Space(2f);
            EditorGUILayout.BeginHorizontal();
            var added = (TriggerAuthoringModuleAsset)EditorGUILayout.ObjectField(
                "Add Existing", null, typeof(TriggerAuthoringModuleAsset), false);
            if (added != null)
                AddExistingModule(added);
            if (GUILayout.Button(new GUIContent("Create", "Create a new module asset and register it in this project"), EditorStyles.miniButton, GUILayout.Width(52f)))
                CreateModule();
            EditorGUILayout.EndHorizontal();

            SirenixEditorGUI.EndBox();
        }

        private void DrawRuntimeExport()
        {
            SirenixEditorGUI.BeginBox("Runtime Export");
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Output Root", GUILayout.Width(78f));
            var root = EditorGUILayout.TextField(_asset.RuntimeOutputRoot ?? string.Empty);
            if (GUILayout.Button("Browse", EditorStyles.miniButton, GUILayout.Width(52f)))
            {
                var resolved = TriggerAuthoringProjectExport.ResolveOutputRoot(root);
                var picked = EditorUtility.OpenFolderPanel(
                    "Choose Runtime Output Root",
                    string.IsNullOrEmpty(resolved) ? Application.dataPath : resolved,
                    string.Empty);
                if (!string.IsNullOrWhiteSpace(picked)) root = MakeRelativeIfInsideProject(picked);
            }
            EditorGUILayout.EndHorizontal();
            if (!string.Equals(root, _asset.RuntimeOutputRoot ?? string.Empty, StringComparison.Ordinal))
            {
                Undo.RecordObject(_asset, "Set Runtime Output Root");
                _asset.SetRuntimeOutputRoot(root);
                EditorUtility.SetDirty(_asset);
            }

            if (GUILayout.Button("Export All Runtime Plans", EditorStyles.miniButton))
            {
                var result = TriggerAuthoringProjectExport.ExportAll(_asset);
                var message = "[TriggerAuthoring] Project '" + _asset.name + "' runtime export: " + result.BuildMessage();
                if (result.Success) Debug.Log(message, _asset);
                else Debug.LogError(message, _asset);
                EditorUtility.DisplayDialog("Project Runtime Export", result.BuildMessage(), "OK");
            }
            EditorGUILayout.HelpBox(
                "Runs the full project gate before writing. Emits {moduleId}.runtime.json; the runtime loads the directory with merge-override.",
                MessageType.None);
            SirenixEditorGUI.EndBox();
        }

        private static string MakeRelativeIfInsideProject(string fullPath)
        {
            var projectRoot = (Path.GetDirectoryName(Application.dataPath) ?? Application.dataPath).Replace('\\', '/');
            projectRoot = projectRoot.TrimEnd('/');
            var normalized = fullPath.Replace('\\', '/').TrimEnd('/');
            if (normalized.StartsWith(projectRoot, StringComparison.OrdinalIgnoreCase))
                return normalized.Substring(projectRoot.Length + 1);
            return normalized;
        }

        private void DrawValidation()
        {
            SirenixEditorGUI.BeginBox("Validation");
            EditorGUILayout.BeginHorizontal();
            if (SirenixEditorGUI.ToolbarButton(new GUIContent("Validate Project", "Run the full project build gate validation")))
            {
                var result = TriggerAuthoringProjectValidator.Validate(_asset);
                var message = "[TriggerAuthoring] Project '" + _asset.name + "' validation: " + result.BuildMessage();
                if (result.Success) Debug.Log(message, _asset);
                else Debug.LogError(message, _asset);
                EditorUtility.DisplayDialog("Trigger Authoring Project Validation", result.BuildMessage(), "OK");
            }
            EditorGUILayout.EndHorizontal();
            SirenixEditorGUI.EndBox();
        }

        private void RemoveModuleAt(int index)
        {
            var module = index >= 0 && index < _asset.Modules.Count ? _asset.Modules[index] : null;
            Undo.RecordObject(_asset, "Remove Trigger Authoring Module");
            if (module != null)
            {
                Undo.RecordObject(module, "Remove Trigger Authoring Module");
                TriggerAuthoringProjectMembership.Detach(module);
                EditorUtility.SetDirty(module);
            }
            else
            {
                _asset.RemoveModuleAt(index);
            }
            EditorUtility.SetDirty(_asset);
        }

        private void AddExistingModule(TriggerAuthoringModuleAsset module)
        {
            if (module == null) return;
            Undo.RecordObject(_asset, "Add Trigger Authoring Module");
            Undo.RecordObject(module, "Add Trigger Authoring Module");
            TriggerAuthoringProjectMembership.Assign(module, _asset);
            EditorUtility.SetDirty(_asset);
            EditorUtility.SetDirty(module);
        }

        private void CreateModule()
        {
            var projectPath = AssetDatabase.GetAssetPath(_asset);
            var projectDirectory = string.IsNullOrEmpty(projectPath)
                ? "Assets"
                : Path.GetDirectoryName(projectPath)?.Replace('\\', '/') ?? "Assets";
            var path = EditorUtility.SaveFilePanelInProject(
                "Create Trigger Authoring Module",
                "TriggerAuthoringModule",
                "asset",
                "Choose where to create the module asset.",
                projectDirectory);
            if (string.IsNullOrWhiteSpace(path)) return;

            var module = TriggerAuthoringProjectSetup.CreateStarterModule(
                Path.GetDirectoryName(path)?.Replace('\\', '/') ?? "Assets",
                Path.GetFileNameWithoutExtension(path),
                _asset);
            if (module == null) return;

            Selection.activeObject = module;
            EditorGUIUtility.PingObject(module);
        }
    }
}
#endif
