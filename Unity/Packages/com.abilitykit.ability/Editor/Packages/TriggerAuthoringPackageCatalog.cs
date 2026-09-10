#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using AbilityKit.Ability.Config.Authoring;

namespace AbilityKit.Ability.Editor.Packages
{
    internal sealed class TriggerAuthoringDomainGroup
    {
        internal TriggerAuthoringDomainGroup(string domainId, string displayName)
        {
            DomainId = domainId;
            DisplayName = displayName;
        }

        internal string DomainId { get; }
        internal string DisplayName { get; }
        internal List<TriggerAuthoringModuleAsset> Packages { get; } =
            new List<TriggerAuthoringModuleAsset>();
        internal int TriggerCount { get; set; }
    }

    /// <summary>将物理模块资产投影为工作区使用的业务域与内容包目录。</summary>
    internal static class TriggerAuthoringPackageCatalog
    {
        internal static List<TriggerAuthoringDomainGroup> Build(
            IReadOnlyList<TriggerAuthoringModuleAsset> packages,
            Predicate<TriggerAuthoringModuleAsset> include = null)
        {
            var byId = new Dictionary<string, TriggerAuthoringDomainGroup>(StringComparer.OrdinalIgnoreCase);
            if (packages != null)
            {
                for (var i = 0; i < packages.Count; i++)
                {
                    var package = packages[i];
                    if (package == null || include != null && !include(package)) continue;
                    var domainId = ResolveDomainId(package);
                    if (!byId.TryGetValue(domainId, out var group))
                    {
                        group = new TriggerAuthoringDomainGroup(domainId, ResolveDomainDisplayName(package, domainId));
                        byId.Add(domainId, group);
                    }
                    group.Packages.Add(package);
                    group.TriggerCount += package.Module?.Triggers?.Count ?? 0;
                }
            }

            var result = new List<TriggerAuthoringDomainGroup>(byId.Values);
            result.Sort(CompareDomains);
            for (var i = 0; i < result.Count; i++)
                result[i].Packages.Sort(ComparePackages);
            return result;
        }

        internal static string ResolveDomainId(TriggerAuthoringModuleAsset package)
        {
            var explicitId = package?.PackageMetadata?.DomainId;
            if (!string.IsNullOrWhiteSpace(explicitId)) return explicitId.Trim().ToLowerInvariant();
            return DomainIdForKind(package?.Module?.Kind ?? TriggerModuleKind.Custom);
        }

        internal static string DomainIdForKind(TriggerModuleKind kind)
        {
            switch (kind)
            {
                case TriggerModuleKind.Ability: return "ability";
                case TriggerModuleKind.Buff: return "buff";
                case TriggerModuleKind.Passive: return "passive";
                case TriggerModuleKind.Projectile: return "projectile";
                case TriggerModuleKind.Summon: return "summon";
                default: return "custom";
            }
        }

        internal static string DomainDisplayNameForKind(TriggerModuleKind kind)
        {
            switch (kind)
            {
                case TriggerModuleKind.Ability: return "技能";
                case TriggerModuleKind.Buff: return "增益效果";
                case TriggerModuleKind.Passive: return "被动效果";
                case TriggerModuleKind.Projectile: return "投射物";
                case TriggerModuleKind.Summon: return "召唤物";
                default: return "自定义";
            }
        }

        internal static string DisplayName(TriggerAuthoringModuleAsset package)
        {
            var displayName = package?.Module?.DisplayName;
            if (!string.IsNullOrWhiteSpace(displayName)) return displayName;
            var moduleId = package?.Module?.ModuleId;
            return !string.IsNullOrWhiteSpace(moduleId) ? moduleId : package != null ? package.name : "<内容包>";
        }

        private static string ResolveDomainDisplayName(TriggerAuthoringModuleAsset package, string domainId)
        {
            var defaultId = DomainIdForKind(package?.Module?.Kind ?? TriggerModuleKind.Custom);
            return string.Equals(domainId, defaultId, StringComparison.OrdinalIgnoreCase)
                ? DomainDisplayNameForKind(package?.Module?.Kind ?? TriggerModuleKind.Custom)
                : domainId;
        }

        private static int CompareDomains(TriggerAuthoringDomainGroup left, TriggerAuthoringDomainGroup right)
        {
            var byName = string.Compare(left.DisplayName, right.DisplayName, StringComparison.OrdinalIgnoreCase);
            return byName != 0 ? byName : string.Compare(left.DomainId, right.DomainId, StringComparison.OrdinalIgnoreCase);
        }

        private static int ComparePackages(TriggerAuthoringModuleAsset left, TriggerAuthoringModuleAsset right)
        {
            return string.Compare(DisplayName(left), DisplayName(right), StringComparison.OrdinalIgnoreCase);
        }
    }
}
#endif
