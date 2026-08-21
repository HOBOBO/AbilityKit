using AbilityKit.Demo.Moba.Diagnostics;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Game.Editor
{
    [CustomEditor(typeof(BattleDiagnosticMetricProfileAsset))]
    internal sealed class BattleDiagnosticMetricProfileAssetEditor : OdinEditor
    {
        private bool _showEffectiveThresholds = true;

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            var asset = (BattleDiagnosticMetricProfileAsset)target;
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Validation Preview", EditorStyles.boldLabel);
            DrawIssues(asset.ValidateConfiguration());
            if (asset.IsActive && BattleDiagnosticMetricProfileAssetSync.ActiveAsset != null &&
                !ReferenceEquals(asset, BattleDiagnosticMetricProfileAssetSync.ActiveAsset))
                EditorGUILayout.HelpBox(
                    "Another active profile asset sorts first and is currently applied to BattleDebug.",
                    MessageType.Warning);

            var preview = asset.BuildPreview();
            if (preview == null) return;
            EditorGUILayout.LabelField("Effective Profile", preview.Name);
            EditorGUILayout.LabelField(
                "Matched Layers",
                preview.MatchedLayers.Count == 0
                    ? "Default only"
                    : string.Join(" -> ", preview.MatchedLayers));
            _showEffectiveThresholds = EditorGUILayout.Foldout(
                _showEffectiveThresholds,
                "Effective Thresholds",
                true);
            if (!_showEffectiveThresholds) return;

            EditorGUI.indentLevel++;
            for (var i = 0; i < preview.Descriptors.Count; i++)
            {
                var descriptor = preview.Descriptors[i];
                if (!descriptor.HasAssessment) continue;
                EditorGUILayout.LabelField(
                    descriptor.DisplayName,
                    FormatThresholds(in descriptor));
            }
            EditorGUI.indentLevel--;
        }

        private static void DrawIssues(
            System.Collections.Generic.IReadOnlyList<BattleDiagnosticMetricProfileValidationIssue> issues)
        {
            if (issues.Count == 0)
            {
                EditorGUILayout.HelpBox("Configuration is valid.", MessageType.Info);
                return;
            }
            for (var i = 0; i < issues.Count; i++)
            {
                var issue = issues[i];
                EditorGUILayout.HelpBox(
                    issue.Message,
                    issue.Severity == BattleDiagnosticMetricProfileValidationSeverity.Error
                        ? MessageType.Error
                        : MessageType.Warning);
            }
        }

        private static string FormatThresholds(in BattleDiagnosticMetricDescriptor descriptor)
        {
            var unit = string.IsNullOrEmpty(descriptor.Unit) ? string.Empty : " " + descriptor.Unit;
            var value = "W " + descriptor.WarningThreshold.ToString("0.###") + unit +
                        "  |  C " + descriptor.CriticalThreshold.ToString("0.###") + unit;
            if (descriptor.HasSuggestedRange)
                value += "  |  Range " + descriptor.SuggestedMinimum.ToString("0.###") +
                         "-" + descriptor.SuggestedMaximum.ToString("0.###") + unit;
            return value;
        }
    }
}
