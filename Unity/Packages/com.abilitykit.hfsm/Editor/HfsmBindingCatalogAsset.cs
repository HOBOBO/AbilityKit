using System;
using System.Collections.Generic;
using AbilityKit.HFSM;
using UnityEngine;

namespace UnityHFSM.Editor
{
    /// <summary>
    /// Version-controlled editor metadata for Next runtime bindings.
    /// This asset contains no executable instances and is safe to inspect in batch export.
    /// </summary>
    [CreateAssetMenu(
        fileName = "HfsmBindingCatalog",
        menuName = "AbilityKit/HFSM/Binding Catalog",
        order = 2)]
    public sealed class HfsmBindingCatalogAsset : ScriptableObject
    {
        [SerializeField] private int formatVersion = 1;
        [SerializeField] private List<HfsmBindingCatalogEntry> entries =
            new List<HfsmBindingCatalogEntry>();

        public int FormatVersion => formatVersion;

        public IReadOnlyList<HfsmBindingCatalogEntry> Entries => entries;

        public void AddEntry(HfsmBindingCatalogEntry entry)
        {
            if (entry == null) throw new ArgumentNullException(nameof(entry));
            entries.Add(entry);
        }

        /// <summary>Builds a metadata-only runtime catalog and returns all asset diagnostics.</summary>
        public HfsmBindingCatalog BuildCatalog()
        {
            var catalog = new HfsmBindingCatalog();
            if (formatVersion != 1)
            {
                catalog.AddIssue(new HfsmBindingCatalogIssue(
                    "HFSMBIND002",
                    HfsmBindingKind.State,
                    string.Empty,
                    $"Unsupported binding catalog format version {formatVersion}; expected 1."));
                return catalog;
            }

            if (entries == null)
                return catalog;

            for (var index = 0; index < entries.Count; index++)
            {
                var entry = entries[index];
                if (entry == null)
                {
                    catalog.AddIssue(new HfsmBindingCatalogIssue(
                        "HFSMBIND003", HfsmBindingKind.State, string.Empty,
                        $"Binding catalog entry at index {index} is null."));
                    continue;
                }

                if (string.IsNullOrWhiteSpace(entry.Key))
                {
                    catalog.AddIssue(new HfsmBindingCatalogIssue(
                        "HFSMBIND004", entry.Kind, entry.Key,
                        $"Binding catalog entry at index {index} has an empty stable key."));
                    continue;
                }

                try
                {
                    catalog.Register(new HfsmBindingDescriptor(
                        entry.Kind,
                        entry.Key.Trim(),
                        entry.DisplayName,
                        entry.Category,
                        entry.Description));
                }
                catch (InvalidOperationException exception)
                {
                    catalog.AddIssue(new HfsmBindingCatalogIssue(
                        "HFSMBIND001", entry.Kind, entry.Key, exception.Message));
                }
            }

            return catalog;
        }
    }

    [Serializable]
    public sealed class HfsmBindingCatalogEntry
    {
        [SerializeField] private HfsmBindingKind kind;
        [SerializeField] private string key = string.Empty;
        [SerializeField] private string displayName = string.Empty;
        [SerializeField] private string category = string.Empty;
        [SerializeField] private string description = string.Empty;

        public HfsmBindingKind Kind { get => kind; set => kind = value; }
        public string Key { get => key; set => key = value ?? string.Empty; }
        public string DisplayName { get => displayName; set => displayName = value ?? string.Empty; }
        public string Category { get => category; set => category = value ?? string.Empty; }
        public string Description { get => description; set => description = value ?? string.Empty; }
    }
}
