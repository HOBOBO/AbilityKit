using System;
using System.Collections.Generic;
using AbilityKit.Demo.Moba.Diagnostics;
using Sirenix.OdinInspector;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Game.Editor
{
    public enum BattleDiagnosticMetricProfileValidationSeverity
    {
        Warning = 0,
        Error = 1
    }

    public readonly struct BattleDiagnosticMetricProfileValidationIssue
    {
        public BattleDiagnosticMetricProfileValidationIssue(
            BattleDiagnosticMetricProfileValidationSeverity severity,
            string message)
        {
            Severity = severity;
            Message = message ?? string.Empty;
        }

        public BattleDiagnosticMetricProfileValidationSeverity Severity { get; }
        public string Message { get; }
    }

    [Serializable]
    public sealed class BattleDiagnosticMetricThresholdOverrideConfig
    {
        [ValueDropdown(nameof(MetricOptions))]
        [LabelText("指标")]
        public string Metric = string.Empty;

        [HorizontalGroup("Thresholds")]
        [LabelText("警告阈值")]
        public double WarningThreshold;

        [HorizontalGroup("Thresholds")]
        [LabelText("严重阈值")]
        public double CriticalThreshold;

        [ToggleLeft, LabelText("覆盖建议范围")]
        public bool OverrideSuggestedRange;

        [HorizontalGroup("Range")]
        [ShowIf(nameof(OverrideSuggestedRange))]
        [LabelText("最小值")]
        public double SuggestedMinimum;

        [HorizontalGroup("Range")]
        [ShowIf(nameof(OverrideSuggestedRange))]
        [LabelText("最大值")]
        public double SuggestedMaximum = 1d;

        public string DisplayLabel
        {
            get
            {
                if (string.IsNullOrEmpty(Metric)) return "未指定指标";
                return BattleDiagnosticFrameMetricCatalog.TryGet(Metric, out var descriptor)
                    ? BattleDebugDisplayText.MetricName(descriptor.Metric, descriptor.DisplayName)
                    : Metric;
            }
        }

        private static IEnumerable<ValueDropdownItem<string>> MetricOptions
        {
            get
            {
                for (var i = 0; i < BattleDiagnosticFrameMetricCatalog.All.Count; i++)
                {
                    var descriptor = BattleDiagnosticFrameMetricCatalog.All[i];
                    if (!descriptor.HasAssessment) continue;
                    yield return new ValueDropdownItem<string>(
                        BattleDebugDisplayText.MetricName(descriptor.Metric, descriptor.DisplayName) + "  (" + descriptor.Metric + ")",
                        descriptor.Metric);
                }
            }
        }
    }

    [Serializable]
    public sealed class BattleDiagnosticMetricProfileLayerConfig
    {
        [ToggleLeft, LabelText("启用")]
        public bool Enabled = true;

        [Required]
        [LabelText("名称")]
        public string Name = "新层";

        [LabelText("优先级")]
        public int Priority;

        [FoldoutGroup("选择条件"), LabelText("项目")]
        public string Project = string.Empty;

        [FoldoutGroup("选择条件"), LabelText("游戏模式")]
        public string GameMode = string.Empty;

        [FoldoutGroup("选择条件"), LabelText("网络模式")]
        public string NetworkMode = string.Empty;

        [FoldoutGroup("选择条件"), LabelText("设备等级")]
        public string DeviceTier = string.Empty;

        [ListDrawerSettings(
            ShowFoldout = true,
            DefaultExpandedState = true,
            ListElementLabelName = nameof(BattleDiagnosticMetricThresholdOverrideConfig.DisplayLabel))]
        [LabelText("阈值覆盖")]
        public List<BattleDiagnosticMetricThresholdOverrideConfig> Overrides =
            new List<BattleDiagnosticMetricThresholdOverrideConfig>();

        public string DisplayLabel => string.IsNullOrWhiteSpace(Name) ? "未命名层" : Name;

    }

    [CreateAssetMenu(
        menuName = "AbilityKit/Moba/诊断/指标配置",
        fileName = "MobaMetricProfile")]
    public sealed class BattleDiagnosticMetricProfileAsset : ScriptableObject
    {
        [Title("启用状态")]
        [ToggleLeft, LabelText("启用此配置")]
        public bool IsActive = true;

        [Title("分析上下文")]
        [LabelText("项目")]
        public string Project = "AbilityKit.Demo.Moba";
        [LabelText("游戏模式")]
        public string GameMode = string.Empty;
        [LabelText("网络模式")]
        public string NetworkMode = string.Empty;
        [LabelText("设备等级")]
        public string DeviceTier = string.Empty;

        [Title("阈值层")]
        [ListDrawerSettings(
            ShowFoldout = true,
            DefaultExpandedState = true,
            ListElementLabelName = nameof(BattleDiagnosticMetricProfileLayerConfig.DisplayLabel))]
        [LabelText("配置层")]
        public List<BattleDiagnosticMetricProfileLayerConfig> Layers =
            new List<BattleDiagnosticMetricProfileLayerConfig>();

        public BattleDiagnosticMetricProfileContext Context =>
            new BattleDiagnosticMetricProfileContext(
                Project?.Trim(),
                GameMode?.Trim(),
                NetworkMode?.Trim(),
                DeviceTier?.Trim());

        public IReadOnlyList<BattleDiagnosticMetricProfileValidationIssue> ValidateConfiguration()
        {
            var issues = new List<BattleDiagnosticMetricProfileValidationIssue>();
            var layerNames = new HashSet<string>(StringComparer.Ordinal);
            var layers = Layers ?? new List<BattleDiagnosticMetricProfileLayerConfig>();
            for (var i = 0; i < layers.Count; i++)
            {
                var layer = layers[i];
                if (layer == null)
                {
                    AddError(issues, "第 " + (i + 1) + " 层为空。");
                    continue;
                }
                if (!layer.Enabled) continue;
                var layerName = layer.Name?.Trim() ?? string.Empty;
                if (string.IsNullOrEmpty(layerName))
                    AddError(issues, "第 " + (i + 1) + " 层需要名称。");
                else if (!layerNames.Add(layerName))
                    AddError(issues, "层名称重复：" + layerName + "。");

                var overrides = layer.Overrides ??
                                new List<BattleDiagnosticMetricThresholdOverrideConfig>();
                if (overrides.Count == 0)
                    AddWarning(issues, "层“" + layer.DisplayLabel + "”没有阈值覆盖项。");
                var metrics = new HashSet<string>(StringComparer.Ordinal);
                for (var j = 0; j < overrides.Count; j++)
                    ValidateOverride(issues, layer.DisplayLabel, j, overrides[j], metrics);
            }
            return issues;
        }

        public bool TryBuild(
            out BattleDiagnosticMetricProfileContext context,
            out List<BattleDiagnosticMetricProfileLayer> layers,
            out IReadOnlyList<BattleDiagnosticMetricProfileValidationIssue> issues)
        {
            context = Context;
            issues = ValidateConfiguration();
            layers = new List<BattleDiagnosticMetricProfileLayer>();
            for (var i = 0; i < issues.Count; i++)
            {
                if (issues[i].Severity == BattleDiagnosticMetricProfileValidationSeverity.Error)
                    return false;
            }

            var configuredLayers = Layers ?? new List<BattleDiagnosticMetricProfileLayerConfig>();
            for (var i = 0; i < configuredLayers.Count; i++)
            {
                var configured = configuredLayers[i];
                if (configured == null || !configured.Enabled) continue;
                var overrides = new List<BattleDiagnosticMetricThresholdOverride>();
                var configuredOverrides = configured.Overrides ??
                                          new List<BattleDiagnosticMetricThresholdOverrideConfig>();
                for (var j = 0; j < configuredOverrides.Count; j++)
                {
                    var item = configuredOverrides[j];
                    overrides.Add(new BattleDiagnosticMetricThresholdOverride(
                        item.Metric.Trim(),
                        item.WarningThreshold,
                        item.CriticalThreshold,
                        item.OverrideSuggestedRange ? item.SuggestedMinimum : double.NaN,
                        item.OverrideSuggestedRange ? item.SuggestedMaximum : double.NaN));
                }
                layers.Add(new BattleDiagnosticMetricProfileLayer(
                    configured.Name.Trim(),
                    configured.Priority,
                    overrides,
                    configured.Project?.Trim(),
                    configured.GameMode?.Trim(),
                    configured.NetworkMode?.Trim(),
                    configured.DeviceTier?.Trim()));
            }
            return true;
        }

        public BattleDiagnosticResolvedMetricProfile BuildPreview()
        {
            return TryBuild(out var context, out var layers, out _)
                ? BattleDiagnosticMetricProfileResolver.Resolve(in context, layers)
                : null;
        }

        [Button("应用到战斗调试器", ButtonSizes.Medium)]
        public void ApplyToBattleDebug()
        {
            BattleDiagnosticMetricProfileAssetSync.Refresh(force: true);
        }

        private void OnValidate()
        {
            BattleDiagnosticMetricProfileAssetSync.ScheduleRefresh();
        }

        private static void ValidateOverride(
            List<BattleDiagnosticMetricProfileValidationIssue> issues,
            string layerName,
            int index,
            BattleDiagnosticMetricThresholdOverrideConfig item,
            HashSet<string> metrics)
        {
            var prefix = "层“" + layerName + "”的第 " + (index + 1) + " 个覆盖项：";
            if (item == null)
            {
                AddError(issues, prefix + "条目为空。");
                return;
            }
            var metric = item.Metric?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(metric))
            {
                AddError(issues, prefix + "必须指定指标。");
                return;
            }
            if (!metrics.Add(metric)) AddError(issues, prefix + "指标重复：" + metric + "。");
            if (!BattleDiagnosticFrameMetricCatalog.TryGet(metric, out var descriptor) || !descriptor.HasAssessment)
                AddError(issues, prefix + "指标未知或没有评估规则：" + metric + "。");
            if (!IsFinite(item.WarningThreshold)) AddError(issues, prefix + "警告阈值必须是有限数值。");
            if (!IsFinite(item.CriticalThreshold) || item.CriticalThreshold < item.WarningThreshold)
                AddError(issues, prefix + "严重阈值必须是有限数值，且不能低于警告阈值。");
            if (item.OverrideSuggestedRange &&
                (!IsFinite(item.SuggestedMinimum) || !IsFinite(item.SuggestedMaximum) ||
                 item.SuggestedMaximum <= item.SuggestedMinimum))
                AddError(issues, prefix + "建议范围必须包含有限且递增的边界值。");
        }

        private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

        private static void AddError(
            List<BattleDiagnosticMetricProfileValidationIssue> issues,
            string message) => issues.Add(new BattleDiagnosticMetricProfileValidationIssue(
            BattleDiagnosticMetricProfileValidationSeverity.Error,
            message));

        private static void AddWarning(
            List<BattleDiagnosticMetricProfileValidationIssue> issues,
            string message) => issues.Add(new BattleDiagnosticMetricProfileValidationIssue(
            BattleDiagnosticMetricProfileValidationSeverity.Warning,
            message));
    }

    [InitializeOnLoad]
    internal static class BattleDiagnosticMetricProfileAssetSync
    {
        private static string _lastFingerprint = string.Empty;
        private static bool _hasApplied;
        private static bool _scheduled;

        static BattleDiagnosticMetricProfileAssetSync()
        {
            ScheduleRefresh();
        }

        public static BattleDiagnosticMetricProfileAsset ActiveAsset { get; private set; }

        public static void ScheduleRefresh()
        {
            if (_scheduled) return;
            _scheduled = true;
            EditorApplication.delayCall += RefreshScheduled;
        }

        public static void Refresh(bool force = false)
        {
            _scheduled = false;
            var activeAssets = FindActiveAssets();
            if (activeAssets.Count == 0)
            {
                ActiveAsset = null;
                if (!_hasApplied) return;
                var defaultContext = new BattleDiagnosticMetricProfileContext("AbilityKit.Demo.Moba");
                BattleDiagnosticMetricProfileRegistry.ReplaceAll(
                    in defaultContext,
                    Array.Empty<BattleDiagnosticMetricProfileLayer>());
                _lastFingerprint = string.Empty;
                _hasApplied = false;
                return;
            }

            activeAssets.Sort((left, right) => string.Compare(
                AssetDatabase.GetAssetPath(left),
                AssetDatabase.GetAssetPath(right),
                StringComparison.Ordinal));
            var asset = activeAssets[0];
            ActiveAsset = asset;
            var fingerprint = EditorJsonUtility.ToJson(asset);
            for (var i = 0; i < activeAssets.Count; i++)
                fingerprint += "\n" + AssetDatabase.GetAssetPath(activeAssets[i]);
            if (!force && string.Equals(fingerprint, _lastFingerprint, StringComparison.Ordinal)) return;
            if (!asset.TryBuild(out var context, out var layers, out var issues))
            {
                Debug.LogWarning("战斗调试指标配置未应用，因为其中包含 " +
                                 CountErrors(issues) + " 个验证错误。", asset);
                return;
            }

            BattleDiagnosticMetricProfileRegistry.ReplaceAll(in context, layers);
            _lastFingerprint = fingerprint;
            _hasApplied = true;
            if (activeAssets.Count > 1)
                Debug.LogWarning("发现多个已启用的战斗调试指标配置资源。当前使用“" +
                                 AssetDatabase.GetAssetPath(asset) + "”。", asset);
        }

        public static void OpenOrCreateAsset()
        {
            Refresh();
            if (ActiveAsset == null)
            {
                var path = EditorUtility.SaveFilePanelInProject(
                    "创建战斗调试指标配置",
                    "MobaMetricProfile",
                    "asset",
                    string.Empty);
                if (string.IsNullOrEmpty(path)) return;
                var asset = ScriptableObject.CreateInstance<BattleDiagnosticMetricProfileAsset>();
                AssetDatabase.CreateAsset(asset, path);
                AssetDatabase.SaveAssets();
                Refresh(force: true);
                ActiveAsset = asset;
            }
            Selection.activeObject = ActiveAsset;
            EditorGUIUtility.PingObject(ActiveAsset);
        }

        private static void RefreshScheduled()
        {
            Refresh();
        }

        private static List<BattleDiagnosticMetricProfileAsset> FindActiveAssets()
        {
            var result = new List<BattleDiagnosticMetricProfileAsset>();
            var guids = AssetDatabase.FindAssets("t:BattleDiagnosticMetricProfileAsset");
            for (var i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var asset = AssetDatabase.LoadAssetAtPath<BattleDiagnosticMetricProfileAsset>(path);
                if (asset != null && asset.IsActive) result.Add(asset);
            }
            return result;
        }

        private static int CountErrors(IReadOnlyList<BattleDiagnosticMetricProfileValidationIssue> issues)
        {
            var count = 0;
            for (var i = 0; i < issues.Count; i++)
            {
                if (issues[i].Severity == BattleDiagnosticMetricProfileValidationSeverity.Error) count++;
            }
            return count;
        }
    }

    internal sealed class BattleDiagnosticMetricProfileAssetPostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            BattleDiagnosticMetricProfileAssetSync.ScheduleRefresh();
        }
    }
}
