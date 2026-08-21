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
        [LabelText("Metric")]
        public string Metric = string.Empty;

        [HorizontalGroup("Thresholds")]
        [LabelText("Warning")]
        public double WarningThreshold;

        [HorizontalGroup("Thresholds")]
        [LabelText("Critical")]
        public double CriticalThreshold;

        [ToggleLeft]
        public bool OverrideSuggestedRange;

        [HorizontalGroup("Range")]
        [ShowIf(nameof(OverrideSuggestedRange))]
        [LabelText("Minimum")]
        public double SuggestedMinimum;

        [HorizontalGroup("Range")]
        [ShowIf(nameof(OverrideSuggestedRange))]
        [LabelText("Maximum")]
        public double SuggestedMaximum = 1d;

        public string DisplayLabel
        {
            get
            {
                if (string.IsNullOrEmpty(Metric)) return "Unassigned Metric";
                return BattleDiagnosticFrameMetricCatalog.TryGet(Metric, out var descriptor)
                    ? descriptor.DisplayName
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
                        descriptor.DisplayName + "  (" + descriptor.Metric + ")",
                        descriptor.Metric);
                }
            }
        }
    }

    [Serializable]
    public sealed class BattleDiagnosticMetricProfileLayerConfig
    {
        [ToggleLeft]
        public bool Enabled = true;

        [Required]
        public string Name = "New Layer";

        public int Priority;

        [FoldoutGroup("Selectors")]
        public string Project = string.Empty;

        [FoldoutGroup("Selectors")]
        public string GameMode = string.Empty;

        [FoldoutGroup("Selectors")]
        public string NetworkMode = string.Empty;

        [FoldoutGroup("Selectors")]
        public string DeviceTier = string.Empty;

        [ListDrawerSettings(
            ShowFoldout = true,
            DefaultExpandedState = true,
            ListElementLabelName = nameof(BattleDiagnosticMetricThresholdOverrideConfig.DisplayLabel))]
        public List<BattleDiagnosticMetricThresholdOverrideConfig> Overrides =
            new List<BattleDiagnosticMetricThresholdOverrideConfig>();

        public string DisplayLabel => string.IsNullOrWhiteSpace(Name) ? "Unnamed Layer" : Name;

    }

    [CreateAssetMenu(
        menuName = "AbilityKit/Moba/Diagnostics/Metric Profile",
        fileName = "MobaMetricProfile")]
    public sealed class BattleDiagnosticMetricProfileAsset : ScriptableObject
    {
        [Title("Activation")]
        [ToggleLeft]
        public bool IsActive = true;

        [Title("Analysis Context")]
        public string Project = "AbilityKit.Demo.Moba";
        public string GameMode = string.Empty;
        public string NetworkMode = string.Empty;
        public string DeviceTier = string.Empty;

        [Title("Threshold Layers")]
        [ListDrawerSettings(
            ShowFoldout = true,
            DefaultExpandedState = true,
            ListElementLabelName = nameof(BattleDiagnosticMetricProfileLayerConfig.DisplayLabel))]
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
                    AddError(issues, "Layer " + (i + 1) + " is null.");
                    continue;
                }
                if (!layer.Enabled) continue;
                var layerName = layer.Name?.Trim() ?? string.Empty;
                if (string.IsNullOrEmpty(layerName))
                    AddError(issues, "Layer " + (i + 1) + " requires a name.");
                else if (!layerNames.Add(layerName))
                    AddError(issues, "Layer name is duplicated: " + layerName + ".");

                var overrides = layer.Overrides ??
                                new List<BattleDiagnosticMetricThresholdOverrideConfig>();
                if (overrides.Count == 0)
                    AddWarning(issues, "Layer '" + layer.DisplayLabel + "' has no threshold overrides.");
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

        [Button("Apply to BattleDebug", ButtonSizes.Medium)]
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
            var prefix = "Layer '" + layerName + "', override " + (index + 1) + ": ";
            if (item == null)
            {
                AddError(issues, prefix + "entry is null.");
                return;
            }
            var metric = item.Metric?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(metric))
            {
                AddError(issues, prefix + "metric is required.");
                return;
            }
            if (!metrics.Add(metric)) AddError(issues, prefix + "metric is duplicated: " + metric + ".");
            if (!BattleDiagnosticFrameMetricCatalog.TryGet(metric, out var descriptor) || !descriptor.HasAssessment)
                AddError(issues, prefix + "metric is unknown or has no assessment rule: " + metric + ".");
            if (!IsFinite(item.WarningThreshold)) AddError(issues, prefix + "warning threshold must be finite.");
            if (!IsFinite(item.CriticalThreshold) || item.CriticalThreshold < item.WarningThreshold)
                AddError(issues, prefix + "critical threshold must be finite and not lower than warning.");
            if (item.OverrideSuggestedRange &&
                (!IsFinite(item.SuggestedMinimum) || !IsFinite(item.SuggestedMaximum) ||
                 item.SuggestedMaximum <= item.SuggestedMinimum))
                AddError(issues, prefix + "suggested range must contain finite increasing bounds.");
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
                Debug.LogWarning("BattleDebug metric profile was not applied because it contains " +
                                 CountErrors(issues) + " validation error(s).", asset);
                return;
            }

            BattleDiagnosticMetricProfileRegistry.ReplaceAll(in context, layers);
            _lastFingerprint = fingerprint;
            _hasApplied = true;
            if (activeAssets.Count > 1)
                Debug.LogWarning("Multiple active BattleDebug metric profile assets were found. Using '" +
                                 AssetDatabase.GetAssetPath(asset) + "'.", asset);
        }

        public static void OpenOrCreateAsset()
        {
            Refresh();
            if (ActiveAsset == null)
            {
                var path = EditorUtility.SaveFilePanelInProject(
                    "Create BattleDebug Metric Profile",
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
