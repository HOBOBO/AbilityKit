using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Game.Editor
{
    internal readonly struct BattleDebugConfigSourceLocation
    {
        public BattleDebugConfigSourceLocation(TextAsset asset, string assetPath, int lineNumber)
        {
            Asset = asset;
            AssetPath = assetPath ?? string.Empty;
            LineNumber = lineNumber;
        }

        public TextAsset Asset { get; }
        public string AssetPath { get; }
        public int LineNumber { get; }
    }

    internal static class BattleDebugConfigSourceIndex
    {
        private const string MobaResourceRoot =
            "Packages/com.abilitykit.demo.moba.view.runtime/Resources/moba/";
        private const string AbilityResourceRoot =
            "Packages/com.abilitykit.demo.moba.view.runtime/Resources/ability/";

        public static bool TryLocate(
            in BattleDebugConfigReference reference,
            out BattleDebugConfigSourceLocation location,
            out string error)
        {
            location = default;
            if (!reference.IsValid)
            {
                error = "Configuration reference is invalid.";
                return false;
            }

            if (!TryGetDescriptor(reference.Kind, out var descriptor))
            {
                error = $"No configuration source is registered for {reference.Kind}.";
                return false;
            }

            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(descriptor.AssetPath);
            if (asset == null)
            {
                error = $"Configuration source is missing: {descriptor.AssetPath}";
                return false;
            }

            if (!TryFindInJson(asset.text, in reference, out var lineNumber, out error))
            {
                return false;
            }

            location = new BattleDebugConfigSourceLocation(asset, descriptor.AssetPath, lineNumber);
            return true;
        }

        internal static bool TryFindInJson(
            string json,
            in BattleDebugConfigReference reference,
            out int lineNumber,
            out string error)
        {
            lineNumber = 0;
            if (!reference.IsValid)
            {
                error = "Configuration reference is invalid.";
                return false;
            }

            if (!TryGetDescriptor(reference.Kind, out var descriptor))
            {
                error = $"No configuration source is registered for {reference.Kind}.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(json))
            {
                error = $"Configuration source for {reference.Kind} is empty.";
                return false;
            }

            JToken root;
            try
            {
                root = JToken.Parse(
                    json,
                    new JsonLoadSettings
                    {
                        LineInfoHandling = LineInfoHandling.Load,
                        CommentHandling = CommentHandling.Ignore,
                    });
            }
            catch (JsonException ex)
            {
                error = $"Failed to parse {reference.Kind} configuration: {ex.Message}";
                return false;
            }

            var entries = string.IsNullOrEmpty(descriptor.RootProperty)
                ? root as JArray
                : root[descriptor.RootProperty] as JArray;
            if (entries == null)
            {
                error = string.IsNullOrEmpty(descriptor.RootProperty)
                    ? $"The {reference.Kind} configuration root must be an array."
                    : $"The {reference.Kind} configuration is missing array '{descriptor.RootProperty}'.";
                return false;
            }

            JObject matchingEntry = null;
            for (var i = 0; i < entries.Count; i++)
            {
                if (!(entries[i] is JObject candidate)) continue;
                var idToken = candidate[descriptor.IdProperty];
                if (idToken != null && idToken.Type == JTokenType.Integer &&
                    idToken.Value<int>() == reference.Id)
                {
                    matchingEntry = candidate;
                    break;
                }
            }

            if (matchingEntry == null)
            {
                error = $"{reference.Kind} configuration #{reference.Id} was not found.";
                return false;
            }

            if (!string.IsNullOrEmpty(reference.PhaseId))
            {
                if (reference.Kind != BattleDebugConfigKind.SkillFlow)
                {
                    error = $"Phase lookup is only supported for {BattleDebugConfigKind.SkillFlow}.";
                    return false;
                }

                var phaseToken = FindPropertyValueRecursive(matchingEntry, "PhaseId", reference.PhaseId);
                if (phaseToken == null)
                {
                    error = $"SkillFlow configuration #{reference.Id} has no phase '{reference.PhaseId}'.";
                    return false;
                }

                lineNumber = GetLineNumber(phaseToken);
                error = string.Empty;
                return true;
            }

            lineNumber = GetLineNumber(matchingEntry[descriptor.IdProperty] ?? matchingEntry);
            error = string.Empty;
            return true;
        }

        private static JToken FindPropertyValueRecursive(JToken token, string propertyName, string value)
        {
            if (token is JObject obj)
            {
                var candidate = obj[propertyName];
                if (candidate != null && candidate.Type == JTokenType.String &&
                    string.Equals(candidate.Value<string>(), value, StringComparison.Ordinal))
                {
                    return candidate;
                }
            }

            foreach (var child in token.Children())
            {
                var match = FindPropertyValueRecursive(child, propertyName, value);
                if (match != null) return match;
            }

            return null;
        }

        private static int GetLineNumber(JToken token)
        {
            if (token is IJsonLineInfo lineInfo && lineInfo.HasLineInfo())
            {
                return Math.Max(1, lineInfo.LineNumber);
            }

            return 1;
        }

        private static bool TryGetDescriptor(
            BattleDebugConfigKind kind,
            out ConfigSourceDescriptor descriptor)
        {
            switch (kind)
            {
                case BattleDebugConfigKind.Skill:
                    descriptor = Moba("skills.json");
                    return true;
                case BattleDebugConfigKind.SkillFlow:
                    descriptor = Moba("skill_flows.json");
                    return true;
                case BattleDebugConfigKind.TriggerPlan:
                    descriptor = new ConfigSourceDescriptor(
                        AbilityResourceRoot + "ability_trigger_plans.json",
                        "Triggers",
                        "TriggerId");
                    return true;
                case BattleDebugConfigKind.Effect:
                    descriptor = Moba("effects.json");
                    return true;
                case BattleDebugConfigKind.Buff:
                    descriptor = Moba("buffs.json");
                    return true;
                case BattleDebugConfigKind.Projectile:
                    descriptor = Moba("projectiles.json");
                    return true;
                case BattleDebugConfigKind.Area:
                    descriptor = Moba("aoes.json");
                    return true;
                case BattleDebugConfigKind.Summon:
                    descriptor = Moba("summons.json");
                    return true;
                case BattleDebugConfigKind.ContinuousProcess:
                    descriptor = Moba("continuous_processes.json");
                    return true;
                case BattleDebugConfigKind.PresentationTemplate:
                    descriptor = Moba("presentation_templates.json");
                    return true;
                default:
                    descriptor = default;
                    return false;
            }
        }

        private static ConfigSourceDescriptor Moba(string fileName)
        {
            return new ConfigSourceDescriptor(MobaResourceRoot + fileName, string.Empty, "Id");
        }

        private readonly struct ConfigSourceDescriptor
        {
            public ConfigSourceDescriptor(string assetPath, string rootProperty, string idProperty)
            {
                AssetPath = assetPath;
                RootProperty = rootProperty;
                IdProperty = idProperty;
            }

            public string AssetPath { get; }
            public string RootProperty { get; }
            public string IdProperty { get; }
        }
    }
}
