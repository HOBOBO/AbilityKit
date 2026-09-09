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
            SirenixEditorGUI.BeginBox("目录");
            using (var scope = new EditorGUI.ChangeCheckScope())
            {
                EditorGUILayout.PropertyField(
                    serializedObject.FindProperty("_eventCatalog"), new GUIContent("事件目录"));
                EditorGUILayout.PropertyField(
                    serializedObject.FindProperty("_globalBlackboardCatalog"), new GUIContent("全局黑板目录"));
                EditorGUILayout.PropertyField(
                    serializedObject.FindProperty("_templateCatalog"), new GUIContent("模板目录"));
                if (scope.changed) serializedObject.ApplyModifiedProperties();
            }
            SirenixEditorGUI.EndBox();
        }

        private void DrawModules()
        {
            var modules = _asset.Modules;
            SirenixEditorGUI.BeginBox($"模块（{modules.Count}）");

            for (var i = 0; i < modules.Count; i++)
            {
                var index = i;
                var module = modules[i];
                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                if (module == null)
                {
                    GUILayout.Label("<模块引用缺失>", EditorStyles.miniBoldLabel);
                }
                else
                {
                    var summary = module.Module != null && !string.IsNullOrWhiteSpace(module.Module.ModuleId)
                        ? module.Module.ModuleId
                        : module.name;
                    GUILayout.Label(summary, EditorStyles.miniBoldLabel);
                    GUILayout.FlexibleSpace();
                    GUILayout.Label(
                        module.Module != null ? TriggerAuthoringEditorLabels.ModuleKind(module.Module.Kind) : string.Empty,
                        EditorStyles.miniLabel,
                        GUILayout.Width(76f));
                    GUILayout.Label(
                        (module.Module != null && module.Module.Triggers != null ? module.Module.Triggers.Count : 0) + " 个触发器",
                        EditorStyles.miniLabel,
                        GUILayout.Width(70f));
                    if (SirenixEditorGUI.ToolbarButton(new GUIContent("打开", "选择并定位此模块资产")))
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
                "添加现有模块", null, typeof(TriggerAuthoringModuleAsset), false);
            if (added != null)
                AddExistingModule(added);
            if (GUILayout.Button(new GUIContent("创建", "创建新模块资产并注册到当前项目"), EditorStyles.miniButton, GUILayout.Width(52f)))
                CreateModule();
            EditorGUILayout.EndHorizontal();

            SirenixEditorGUI.EndBox();
        }

        private void DrawRuntimeExport()
        {
            SirenixEditorGUI.BeginBox("运行时导出");
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(TriggerAuthoringEditorIntegration.T("output-root"), GUILayout.Width(78f));
            var root = EditorGUILayout.TextField(_asset.RuntimeOutputRoot ?? string.Empty);
            if (GUILayout.Button(TriggerAuthoringEditorIntegration.T("browse"), EditorStyles.miniButton, GUILayout.Width(52f)))
            {
                var resolved = TriggerAuthoringProjectExport.ResolveOutputRoot(root);
                var picked = EditorUtility.OpenFolderPanel(
                    "选择运行时输出根目录",
                    string.IsNullOrEmpty(resolved) ? Application.dataPath : resolved,
                    string.Empty);
                if (!string.IsNullOrWhiteSpace(picked)) root = MakeRelativeIfInsideProject(picked);
            }
            EditorGUILayout.EndHorizontal();
            if (!string.Equals(root, _asset.RuntimeOutputRoot ?? string.Empty, StringComparison.Ordinal))
            {
                Undo.RecordObject(_asset, "设置运行时输出目录");
                _asset.SetRuntimeOutputRoot(root);
                EditorUtility.SetDirty(_asset);
            }

            if (GUILayout.Button(TriggerAuthoringEditorIntegration.T("export-all-runtime-plans"), EditorStyles.miniButton))
            {
                var result = TriggerAuthoringProjectExport.ExportAll(_asset);
                var message = "[TriggerAuthoring] Project '" + _asset.name + "' runtime export: " + result.BuildMessage();
                if (result.Success) Debug.Log(message, _asset);
                else Debug.LogError(message, _asset);
                EditorUtility.DisplayDialog("项目运行时导出", result.BuildMessage(), "确定");
            }
            EditorGUILayout.HelpBox(
                "写入前会执行完整的项目构建门禁。每个模块输出为 {moduleId}.runtime.json，运行时会以合并覆盖方式加载此目录。",
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
            SirenixEditorGUI.BeginBox("校验");
            EditorGUILayout.BeginHorizontal();
            if (SirenixEditorGUI.ToolbarButton(new GUIContent("校验项目", "执行完整的项目构建门禁校验")))
            {
                var result = TriggerAuthoringProjectValidator.Validate(_asset);
                var message = "[TriggerAuthoring] Project '" + _asset.name + "' validation: " + result.BuildMessage();
                if (result.Success) Debug.Log(message, _asset);
                else Debug.LogError(message, _asset);
                EditorUtility.DisplayDialog("触发器项目校验", result.BuildMessage(), "确定");
            }
            EditorGUILayout.EndHorizontal();
            SirenixEditorGUI.EndBox();
        }

        private void RemoveModuleAt(int index)
        {
            var module = index >= 0 && index < _asset.Modules.Count ? _asset.Modules[index] : null;
            Undo.RecordObject(_asset, "移除触发器模块");
            if (module != null)
            {
                Undo.RecordObject(module, "移除触发器模块");
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
            Undo.RecordObject(_asset, "添加触发器模块");
            Undo.RecordObject(module, "添加触发器模块");
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
                "创建触发器模块",
                "TriggerAuthoringModule",
                "asset",
                "请选择模块资产的创建位置。",
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
