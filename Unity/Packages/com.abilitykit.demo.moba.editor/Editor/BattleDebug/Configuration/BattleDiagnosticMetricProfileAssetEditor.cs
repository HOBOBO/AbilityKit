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
            EditorGUILayout.LabelField("验证预览", EditorStyles.boldLabel);
            DrawIssues(asset.ValidateConfiguration());
            if (asset.IsActive && BattleDiagnosticMetricProfileAssetSync.ActiveAsset != null &&
                !ReferenceEquals(asset, BattleDiagnosticMetricProfileAssetSync.ActiveAsset))
                EditorGUILayout.HelpBox(
                    "另一个已启用的配置资源排序更靠前，当前战斗调试器正在使用它。",
                    MessageType.Warning);

            var preview = asset.BuildPreview();
            if (preview == null) return;
            EditorGUILayout.LabelField("生效配置", preview.Name);
            EditorGUILayout.LabelField(
                "匹配层",
                preview.MatchedLayers.Count == 0
                    ? "仅默认配置"
                    : string.Join(" -> ", preview.MatchedLayers));
            _showEffectiveThresholds = EditorGUILayout.Foldout(
                _showEffectiveThresholds,
                "生效阈值",
                true);
            if (!_showEffectiveThresholds) return;

            EditorGUI.indentLevel++;
            for (var i = 0; i < preview.Descriptors.Count; i++)
            {
                var descriptor = preview.Descriptors[i];
                if (!descriptor.HasAssessment) continue;
                EditorGUILayout.LabelField(
                    BattleDebugDisplayText.MetricName(descriptor.Metric, descriptor.DisplayName),
                    FormatThresholds(in descriptor));
            }
            EditorGUI.indentLevel--;
        }

        private static void DrawIssues(
            System.Collections.Generic.IReadOnlyList<BattleDiagnosticMetricProfileValidationIssue> issues)
        {
            if (issues.Count == 0)
            {
                EditorGUILayout.HelpBox("配置有效。", MessageType.Info);
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
            var unit = string.IsNullOrEmpty(descriptor.Unit) ? string.Empty : " " + BattleDebugDisplayText.MetricUnit(descriptor.Unit);
            var value = "警告 " + descriptor.WarningThreshold.ToString("0.###") + unit +
                        "  |  严重 " + descriptor.CriticalThreshold.ToString("0.###") + unit;
            if (descriptor.HasSuggestedRange)
                value += "  |  范围 " + descriptor.SuggestedMinimum.ToString("0.###") +
                         "-" + descriptor.SuggestedMaximum.ToString("0.###") + unit;
            return value;
        }
    }
}
