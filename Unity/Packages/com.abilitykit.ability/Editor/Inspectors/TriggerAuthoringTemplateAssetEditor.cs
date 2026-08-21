#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using AbilityKit.Ability.Editor.Utilities;
using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities.Editor;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Ability.Editor.Inspectors
{
    [CustomEditor(typeof(TriggerAuthoringTemplateAsset))]
    internal sealed class TriggerAuthoringTemplateAssetEditor : OdinEditor
    {
        private TriggerAuthoringTemplateAsset _asset;
        private TriggerAuthoringSyncInspection _inspection;
        private double _nextInspectionAt;

        protected override void OnEnable()
        {
            base.OnEnable();
            _asset = target as TriggerAuthoringTemplateAsset;
        }

        public override void OnInspectorGUI()
        {
            if (_asset == null) return;
            DrawToolbar();
            DrawValidation();
            base.OnInspectorGUI();
        }

        private void DrawToolbar()
        {
            RefreshInspection();
            SirenixEditorGUI.BeginHorizontalToolbar();
            GUILayout.Label("Source", GUILayout.Width(44f));
            GUILayout.Label(_inspection != null ? _inspection.State.ToString() : "Unknown", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (SirenixEditorGUI.ToolbarButton(new GUIContent("Import", "Import Template Source JSON"))) Import();
            if (SirenixEditorGUI.ToolbarButton(new GUIContent("Export", "Export Template Source JSON"))) Export();
            if (SirenixEditorGUI.ToolbarButton(new GUIContent("Validate", "Validate template schema and trees"))) Repaint();
            SirenixEditorGUI.EndHorizontalToolbar();
        }

        private void DrawValidation()
        {
            var diagnostics = TriggerAuthoringTemplateValidator.Validate(
                _asset.Template,
                TriggerAuthoringValidationContext.Create(_asset));
            for (var i = 0; i < diagnostics.Count; i++)
            {
                var diagnostic = diagnostics[i];
                if (diagnostic.Severity == TriggerAuthoringDiagnosticSeverity.Info) continue;
                EditorGUILayout.HelpBox(
                    diagnostic.Code + " " + diagnostic.Path + ": " + diagnostic.Message,
                    diagnostic.Severity == TriggerAuthoringDiagnosticSeverity.Error
                        ? MessageType.Error
                        : MessageType.Warning);
            }
        }

        private void Export()
        {
            var path = ResolveSourcePath();
            if (string.IsNullOrWhiteSpace(path))
            {
                var name = _asset.Template != null && !string.IsNullOrWhiteSpace(_asset.Template.TemplateId)
                    ? _asset.Template.TemplateId
                    : _asset.name;
                path = EditorUtility.SaveFilePanel(
                    "Export Trigger Template Source JSON", Application.dataPath, name,
                    TriggerSourceCodecs.TemplateDefault.FileExtension);
                if (string.IsNullOrWhiteSpace(path)) return;
            }

            var result = TriggerAuthoringTemplateSourceSync.Export(_asset, path);
            if (!result.Success && result.CanForce && EditorUtility.DisplayDialog(
                    "Trigger Template Source Conflict",
                    result.Message + "\n\nForce export and overwrite Source JSON?",
                    "Force Export",
                    "Cancel"))
                result = TriggerAuthoringTemplateSourceSync.Export(_asset, path, true);
            ShowResult("Export", result);
        }

        private void Import()
        {
            var path = ResolveSourcePath();
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                path = EditorUtility.OpenFilePanel(
                    "Import Trigger Template Source JSON", Application.dataPath,
                    TriggerSourceCodecs.TemplateDefault.FileExtension);
                if (string.IsNullOrWhiteSpace(path)) return;
            }

            var result = TriggerAuthoringTemplateSourceSync.Import(_asset, path);
            if (!result.Success && result.CanForce && EditorUtility.DisplayDialog(
                    "Trigger Template Asset Conflict",
                    result.Message + "\n\nForce import and overwrite Asset content?",
                    "Force Import",
                    "Cancel"))
                result = TriggerAuthoringTemplateSourceSync.Import(_asset, path, true);
            ShowResult("Import", result);
        }

        private void ShowResult(string operation, TriggerAuthoringSyncResult result)
        {
            if (result.Success)
            {
                AssetDatabase.SaveAssets();
                _nextInspectionAt = 0d;
                ShowNotification("Template " + operation.ToLowerInvariant() + " succeeded");
                return;
            }
            EditorUtility.DisplayDialog("Trigger Template " + operation + " Failed", result.Message, "OK");
        }

        private void RefreshInspection()
        {
            if (_inspection != null && EditorApplication.timeSinceStartup < _nextInspectionAt) return;
            _inspection = TriggerAuthoringTemplateSourceSync.Inspect(_asset);
            _nextInspectionAt = EditorApplication.timeSinceStartup + 0.5d;
        }

        private string ResolveSourcePath()
        {
            if (string.IsNullOrWhiteSpace(_asset.SourceJsonPath)) return string.Empty;
            if (Path.IsPathRooted(_asset.SourceJsonPath)) return Path.GetFullPath(_asset.SourceJsonPath);
            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            return Path.GetFullPath(Path.Combine(projectRoot, _asset.SourceJsonPath));
        }

        private static void ShowNotification(string message)
        {
            var window = EditorWindow.focusedWindow;
            if (window != null) window.ShowNotification(new GUIContent(message));
        }
    }
}
#endif
