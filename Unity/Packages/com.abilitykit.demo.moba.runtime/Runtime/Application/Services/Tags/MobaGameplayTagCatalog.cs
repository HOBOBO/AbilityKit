using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using AbilityKit.Ability.Config;
using AbilityKit.GameplayTags;
using Luban;
using Newtonsoft.Json.Linq;

namespace AbilityKit.Demo.Moba.Services
{
    /// <summary>
    /// 将 Luban 标签名称注册到通用 GameplayTags 目录。
    /// </summary>
    public static class MobaGameplayTagCatalog
    {
        private static readonly object Sync = new object();
        private static string _catalogVersion;
        private static string _bundleVersion;
        private static Dictionary<string, int> _definitionIds = new Dictionary<string, int>(StringComparer.Ordinal);
        private static readonly string[] BundleTables =
        {
            "buffs", "continuous_processes", "continuous_tag_templates", "skills",
            "tag_behavior_gates", "tag_definitions", "tag_result_flags",
            "tag_result_mappings", "tag_rule_groups", "tag_rule_nodes"
        };

        public static string CatalogVersion
        {
            get { lock (Sync) return _catalogVersion; }
        }

        public static string BundleVersion
        {
            get { lock (Sync) return _bundleVersion; }
        }

        public static bool TryGetDefinitionId(string tagName, out int definitionId)
        {
            lock (Sync) return _definitionIds.TryGetValue(tagName ?? string.Empty, out definitionId);
        }

        public static void RegisterDefinitions(ITextAssetLoader assets)
        {
            var bundleVersion = VerifyPublishedBundle(assets);
            if (assets == null || !assets.TryLoadBytes("luban/moba_bytes/tag_definitions.bytes", out var bytes) ||
                bytes == null || bytes.Length == 0)
                throw new InvalidOperationException("Missing MOBA tag definitions.");
            var table = new moba_luban.TagDefinitions(ByteBuf.Wrap(bytes));
            var byId = table.DataMap;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var row in table.DataList)
            {
                if (string.IsNullOrWhiteSpace(row.Name) || !seen.Add(row.Name))
                    throw new InvalidOperationException($"Duplicate or empty MOBA tag name: {row.Id}");
                var lastDot = row.Name.LastIndexOf('.');
                if (lastDot < 0)
                {
                    if (row.ParentId != 0)
                        throw new InvalidOperationException($"Root MOBA tag has a parent: {row.Id}");
                }
                else if (!byId.TryGetValue(row.ParentId, out var parent) ||
                         parent.Namespace != row.Namespace ||
                         !string.Equals(parent.Name, row.Name.Substring(0, lastDot), StringComparison.Ordinal))
                {
                    throw new InvalidOperationException($"MOBA tag parent disagrees with its name: {row.Id}");
                }
            }
            var ordered = table.DataList.OrderBy(row => row.Id).ToArray();
            var identity = new StringBuilder();
            foreach (var row in ordered)
                identity.Append(row.Id).Append('|').Append(row.Name).Append('|').Append((int)row.Namespace)
                    .Append('|').Append(row.ParentId).Append('|').Append((int)row.Kind)
                    .Append('|').Append((int)row.Status).Append('\n');
            string version;
            using (var hash = SHA256.Create())
                version = BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(identity.ToString()))).Replace("-", "");
            lock (Sync)
            {
                if (_catalogVersion != null && _catalogVersion != version)
                    throw new InvalidOperationException("A different MOBA tag catalog is already registered in this process.");
                if (_bundleVersion != null && _bundleVersion != bundleVersion)
                    throw new InvalidOperationException("A different MOBA tag bundle is already registered in this process.");
                GameplayTagManager.Instance.RegisterTags(ordered.Select(row => row.Name));
                _definitionIds = ordered.ToDictionary(row => row.Name, row => row.Id, StringComparer.Ordinal);
                _catalogVersion = version;
                _bundleVersion = bundleVersion;
            }
        }

        public static string VerifyPublishedBundle(ITextAssetLoader assets)
        {
            if (assets == null || !assets.TryLoadText("luban/moba/tag_bundle_manifest.json", out var text) ||
                string.IsNullOrWhiteSpace(text))
                throw new InvalidOperationException("Missing MOBA tag bundle manifest.");
            var manifest = JObject.Parse(text);
            var tables = manifest["tables"] as JObject;
            if (manifest.Value<int?>("schemaVersion") != 1 || tables == null ||
                tables.Properties().Count() != BundleTables.Length)
                throw new InvalidOperationException("Invalid MOBA tag bundle manifest.");
            var identity = new StringBuilder();
            using (var hash = SHA256.Create())
            {
                foreach (var name in BundleTables)
                {
                    if (!tables.TryGetValue(name, out var token) || token.Type != JTokenType.String ||
                        !assets.TryLoadBytes("luban/moba_bytes/" + name + ".bytes", out var bytes) ||
                        bytes == null || bytes.Length == 0)
                        throw new InvalidOperationException($"Missing MOBA tag bundle table: {name}");
                    var actual = BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
                    if (!string.Equals(token.Value<string>(), actual, StringComparison.Ordinal))
                        throw new InvalidOperationException($"MOBA tag bundle table differs: {name}");
                    identity.Append(name).Append('=').Append(actual).Append('\n');
                }
                var version = BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(identity.ToString())))
                    .Replace("-", "").ToLowerInvariant();
                if (!string.Equals(manifest.Value<string>("bundleVersion"), version, StringComparison.Ordinal))
                    throw new InvalidOperationException("MOBA tag bundle version differs from its tables.");
                return version;
            }
        }

        public static bool TryGet(string tagName, out GameplayTag tag)
        {
            tag = default;
            return !string.IsNullOrWhiteSpace(tagName)
                && GameplayTagManager.Instance.TryGetTag(tagName, out tag)
                && tag.IsValid;
        }

        public static bool TryResolve(string tagName, out GameplayTag tag)
        {
            return TryGet(tagName, out tag);
        }

        public static GameplayTagContainer ToContainer(IReadOnlyList<string> names)
        {
            var c = new GameplayTagContainer();
            Append(c, names);
            return c.Count > 0 ? c : null;
        }

        public static GameplayTagContainer ToConfigContainer(IReadOnlyList<string> names)
        {
            if (names != null && CatalogVersion != null)
                for (var i = 0; i < names.Count; i++)
                    if (!TryGetDefinitionId(names[i], out _))
                        throw new InvalidOperationException($"Unknown MOBA config tag: {names[i]}");
            return ToContainer(names);
        }

        public static void Append(GameplayTagContainer container, IReadOnlyList<string> names)
        {
            if (container == null || names == null) return;

            for (int i = 0; i < names.Count; i++)
            {
                if (!TryResolve(names[i], out var tag))
                    throw new InvalidOperationException($"Unknown MOBA gameplay tag: {names[i]}");
                container.Add(tag);
            }
        }

        public static bool HasAny(GameplayTagContainer container, GameplayTagContainer query)
        {
            return container != null && query != null && query.Count > 0 && container.HasAny(query, exact: false);
        }

    }
}
