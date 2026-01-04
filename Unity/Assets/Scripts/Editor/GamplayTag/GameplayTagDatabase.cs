using System;
using System.Collections.Generic;
using UnityEngine.Serialization;
using UnityEngine;

namespace AbilityKit.Editor.GamplayTag
{
    public sealed class GameplayTagDatabase : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            [SerializeField] public string Name;
            [SerializeField] public string Comment;

            public Entry(string name, string comment)
            {
                Name = name;
                Comment = comment;
            }
        }

        // Legacy field used by older versions.
        [FormerlySerializedAs("tags")]
        [SerializeField] private List<string> legacyTags = new List<string>();

        [SerializeField] private List<Entry> entries = new List<Entry>();

        public IReadOnlyList<Entry> Entries
        {
            get
            {
                MigrateIfNeeded();
                return entries;
            }
        }

        public IReadOnlyList<string> Tags
        {
            get
            {
                MigrateIfNeeded();

                if (entries.Count == 0) return Array.Empty<string>();
                var arr = new string[entries.Count];
                for (int i = 0; i < entries.Count; i++) arr[i] = entries[i]?.Name ?? string.Empty;
                return arr;
            }
        }

        public Entry GetOrCreate(string name)
        {
            MigrateIfNeeded();
            if (!TryGetEntry(name, out var e))
            {
                e = new Entry(name, string.Empty);
                entries.Add(e);
                SortAndDedup();
            }
            return e;
        }

        public bool TryGetEntry(string name, out Entry entry)
        {
            MigrateIfNeeded();
            entry = null;
            if (string.IsNullOrEmpty(name)) return false;
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                if (e == null) continue;
                if (string.Equals(e.Name, name, StringComparison.Ordinal))
                {
                    entry = e;
                    return true;
                }
            }
            return false;
        }

        public List<Entry> GetMutableEntries()
        {
            MigrateIfNeeded();
            return entries;
        }

        public void SortAndDedup()
        {
            MigrateIfNeeded();

            entries.RemoveAll(e => e == null || string.IsNullOrWhiteSpace(e.Name));
            entries.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));

            for (int i = entries.Count - 1; i > 0; i--)
            {
                if (string.Equals(entries[i].Name, entries[i - 1].Name, StringComparison.Ordinal))
                {
                    // keep the first; merge comment if needed
                    if (string.IsNullOrWhiteSpace(entries[i - 1].Comment) && !string.IsNullOrWhiteSpace(entries[i].Comment))
                    {
                        entries[i - 1].Comment = entries[i].Comment;
                    }

                    entries.RemoveAt(i);
                }
            }
        }

        public void RemoveByPrefix(string full)
        {
            MigrateIfNeeded();
            if (string.IsNullOrEmpty(full)) return;

            var prefix = full + ".";
            entries.RemoveAll(e => e != null && (string.Equals(e.Name, full, StringComparison.Ordinal) || e.Name.StartsWith(prefix, StringComparison.Ordinal)));
        }

        public void RenamePrefix(string fromFull, string toFull)
        {
            MigrateIfNeeded();
            if (string.IsNullOrEmpty(fromFull) || string.IsNullOrEmpty(toFull)) return;

            var fromPrefix = fromFull + ".";
            var toPrefix = toFull + ".";

            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                if (e == null || string.IsNullOrEmpty(e.Name)) continue;

                if (string.Equals(e.Name, fromFull, StringComparison.Ordinal))
                {
                    e.Name = toFull;
                    continue;
                }

                if (e.Name.StartsWith(fromPrefix, StringComparison.Ordinal))
                {
                    e.Name = toPrefix + e.Name.Substring(fromPrefix.Length);
                }
            }
        }

        private void OnEnable()
        {
            MigrateIfNeeded();
        }

        private void OnValidate()
        {
            MigrateIfNeeded();
        }

        private void MigrateIfNeeded()
        {
            if (legacyTags == null) legacyTags = new List<string>();
            if (entries == null) entries = new List<Entry>();

            if (entries.Count > 0) return;
            if (legacyTags.Count == 0) return;

            for (int i = 0; i < legacyTags.Count; i++)
            {
                var t = legacyTags[i];
                if (string.IsNullOrWhiteSpace(t)) continue;
                entries.Add(new Entry(t.Trim(), string.Empty));
            }

            legacyTags.Clear();
            SortAndDedup();
        }
    }
}
