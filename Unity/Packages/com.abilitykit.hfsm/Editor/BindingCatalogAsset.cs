using System;
using System.Collections.Generic;
using AbilityKit.HFSM.Definition;
using AbilityKit.HFSM.Runtime;
using UnityEngine;

namespace AbilityKit.HFSM.Editor
{
    public sealed class BindingCatalogAsset : ScriptableObject
    {
        [SerializeField, InspectorName("格式版本")] private int formatVersion = 1;
        [SerializeField, InspectorName("绑定条目")] private List<BindingCatalogEntry> entries =
            new List<BindingCatalogEntry>();

        public int FormatVersion => formatVersion;

        public IReadOnlyList<BindingCatalogEntry> Entries => entries;

        public void AddEntry(BindingCatalogEntry entry)
        {
            if (entry == null) throw new ArgumentNullException(nameof(entry));
            entries.Add(entry);
        }

        /// <summary>Builds a metadata-only runtime catalog and returns all asset diagnostics.</summary>
        public BindingCatalog BuildCatalog()
        {
            var catalog = new BindingCatalog();
            if (formatVersion != 1)
            {
                catalog.AddIssue(new BindingCatalogIssue(
                    "HFSMBIND002",
                    BindingKind.State,
                    string.Empty,
                    $"不支持绑定目录格式版本 {formatVersion}；预期版本为 1。"));
                return catalog;
            }

            if (entries == null)
                return catalog;

            for (var index = 0; index < entries.Count; index++)
            {
                var entry = entries[index];
                if (entry == null)
                {
                    catalog.AddIssue(new BindingCatalogIssue(
                        "HFSMBIND003", BindingKind.State, string.Empty,
                        $"索引 {index} 处的绑定目录条目为空。"));
                    continue;
                }

                if (string.IsNullOrWhiteSpace(entry.Key))
                {
                    catalog.AddIssue(new BindingCatalogIssue(
                        "HFSMBIND004", entry.Kind, entry.Key,
                        $"索引 {index} 处的绑定目录条目缺少稳定键。"));
                    continue;
                }

                try
                {
                    catalog.Register(new BindingDescriptor(
                        entry.Kind,
                        entry.Key.Trim(),
                        entry.DisplayName,
                        entry.Category,
                        entry.Description));
                }
                catch (InvalidOperationException exception)
                {
                    catalog.AddIssue(new BindingCatalogIssue(
                        "HFSMBIND001", entry.Kind, entry.Key,
                        $"绑定键“{entry.Key}”重复。"));
                }
            }

            return catalog;
        }
    }
}
