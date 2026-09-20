#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using AbilityKit.Ability.Config.Authoring;

namespace AbilityKit.Ability.Editor.Utilities
{
    internal static class TriggerAuthoringTriggerBatchOperations
    {
        public static List<int> CollectVisibleTriggerIndices(
            IReadOnlyList<TriggerAuthoringTriggerIndex.Group> groups)
        {
            var indices = new List<int>();
            var seen = new HashSet<int>();
            if (groups == null) return indices;

            for (var groupIndex = 0; groupIndex < groups.Count; groupIndex++)
            {
                var group = groups[groupIndex];
                if (group == null) continue;
                for (var entryIndex = 0; entryIndex < group.Entries.Count; entryIndex++)
                {
                    var index = group.Entries[entryIndex].Index;
                    if (seen.Add(index)) indices.Add(index);
                }
            }

            indices.Sort();
            return indices;
        }

        public static List<TriggerAuthoringTriggerIndex.Group> PaginateGroups(
            IReadOnlyList<TriggerAuthoringTriggerIndex.Group> groups,
            int requestedPageIndex,
            int pageSize,
            out int totalCount,
            out int pageCount,
            out int pageIndex)
        {
            pageSize = Math.Max(1, pageSize);
            var orderedIndices = CollectOrderedUniqueIndices(groups);
            totalCount = orderedIndices.Count;
            pageCount = Math.Max(1, (totalCount + pageSize - 1) / pageSize);
            pageIndex = Math.Max(0, Math.Min(requestedPageIndex, pageCount - 1));

            var first = pageIndex * pageSize;
            var last = Math.Min(first + pageSize, totalCount);
            var included = new HashSet<int>();
            for (var i = first; i < last; i++) included.Add(orderedIndices[i]);

            var result = new List<TriggerAuthoringTriggerIndex.Group>();
            if (groups == null || included.Count == 0) return result;
            for (var groupIndex = 0; groupIndex < groups.Count; groupIndex++)
            {
                var source = groups[groupIndex];
                if (source == null) continue;
                TriggerAuthoringTriggerIndex.Group pageGroup = null;
                for (var entryIndex = 0; entryIndex < source.Entries.Count; entryIndex++)
                {
                    var entry = source.Entries[entryIndex];
                    if (!included.Contains(entry.Index)) continue;
                    if (pageGroup == null)
                        pageGroup = new TriggerAuthoringTriggerIndex.Group(
                            source.Key,
                            source.Label,
                            source.SortKey);
                    pageGroup.Entries.Add(entry);
                }
                if (pageGroup != null) result.Add(pageGroup);
            }
            return result;
        }

        public static int SetEnabled(
            IList<TriggerDefinitionData> triggers,
            IReadOnlyList<int> indices,
            bool enabled)
        {
            return Apply(triggers, indices, trigger =>
            {
                if (trigger.Enabled == enabled) return false;
                trigger.Enabled = enabled;
                return true;
            });
        }

        public static int SetGroupPath(
            IList<TriggerDefinitionData> triggers,
            IReadOnlyList<int> indices,
            string groupPath)
        {
            var normalized = NormalizeGroupPath(groupPath);
            return Apply(triggers, indices, trigger =>
            {
                if (string.Equals(trigger.GroupPath ?? string.Empty, normalized, StringComparison.Ordinal))
                    return false;
                trigger.GroupPath = normalized;
                return true;
            });
        }

        public static string NormalizeGroupPath(string groupPath)
        {
            if (string.IsNullOrWhiteSpace(groupPath)) return string.Empty;
            var segments = groupPath.Replace('\\', '/').Split('/');
            var normalized = new List<string>();
            for (var i = 0; i < segments.Length; i++)
            {
                var segment = segments[i].Trim();
                if (segment.Length > 0) normalized.Add(segment);
            }
            return string.Join("/", normalized);
        }

        public static int AddTags(
            IList<TriggerDefinitionData> triggers,
            IReadOnlyList<int> indices,
            string tagsText)
        {
            var tags = ParseTags(tagsText);
            if (tags.Count == 0) return 0;
            return Apply(triggers, indices, trigger =>
            {
                if (trigger.Tags == null) trigger.Tags = new List<string>();
                var changed = false;
                for (var i = 0; i < tags.Count; i++)
                {
                    if (ContainsTag(trigger.Tags, tags[i])) continue;
                    trigger.Tags.Add(tags[i]);
                    changed = true;
                }
                return changed;
            });
        }

        public static int RemoveTags(
            IList<TriggerDefinitionData> triggers,
            IReadOnlyList<int> indices,
            string tagsText)
        {
            var tags = ParseTags(tagsText);
            if (tags.Count == 0) return 0;
            return Apply(triggers, indices, trigger =>
            {
                var existing = trigger.Tags;
                if (existing == null || existing.Count == 0) return false;

                var changed = false;
                for (var i = existing.Count - 1; i >= 0; i--)
                {
                    if (!ContainsTag(tags, existing[i])) continue;
                    existing.RemoveAt(i);
                    changed = true;
                }
                return changed;
            });
        }

        public static string BuildTriggerIdList(
            IReadOnlyList<TriggerDefinitionData> triggers,
            IReadOnlyList<int> indices)
        {
            var values = new List<string>();
            if (triggers == null || indices == null) return string.Empty;
            for (var i = 0; i < indices.Count; i++)
            {
                var index = indices[i];
                if (index < 0 || index >= triggers.Count || triggers[index] == null) continue;
                values.Add(triggers[index].Id.ToString());
            }
            return string.Join(", ", values);
        }

        public static bool ContainsVisibleTriggerIndex(
            IReadOnlyList<int> indices,
            int index)
        {
            if (indices == null) return false;
            for (var i = 0; i < indices.Count; i++)
                if (indices[i] == index)
                    return true;
            return false;
        }

        public static List<string> ParseTags(string value)
        {
            var tags = new List<string>();
            if (string.IsNullOrWhiteSpace(value)) return tags;

            var parts = value.Split(',');
            for (var i = 0; i < parts.Length; i++)
            {
                var tag = parts[i].Trim();
                if (tag.Length == 0 || ContainsTag(tags, tag)) continue;
                tags.Add(tag);
            }
            return tags;
        }

        private static int Apply(
            IList<TriggerDefinitionData> triggers,
            IReadOnlyList<int> indices,
            Func<TriggerDefinitionData, bool> mutate)
        {
            if (triggers == null || indices == null || mutate == null) return 0;

            var changed = 0;
            var seen = new HashSet<int>();
            for (var i = 0; i < indices.Count; i++)
            {
                var index = indices[i];
                if (!seen.Add(index) || index < 0 || index >= triggers.Count) continue;
                var trigger = triggers[index];
                if (trigger == null) continue;
                if (mutate(trigger)) changed++;
            }
            return changed;
        }

        private static List<int> CollectOrderedUniqueIndices(
            IReadOnlyList<TriggerAuthoringTriggerIndex.Group> groups)
        {
            var result = new List<int>();
            var seen = new HashSet<int>();
            if (groups == null) return result;
            for (var groupIndex = 0; groupIndex < groups.Count; groupIndex++)
            {
                var group = groups[groupIndex];
                if (group == null) continue;
                for (var entryIndex = 0; entryIndex < group.Entries.Count; entryIndex++)
                {
                    var index = group.Entries[entryIndex].Index;
                    if (seen.Add(index)) result.Add(index);
                }
            }
            return result;
        }

        private static bool ContainsTag(IReadOnlyList<string> tags, string tag)
        {
            if (tags == null) return false;
            for (var i = 0; i < tags.Count; i++)
            {
                if (string.Equals(tags[i], tag, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }
    }
}
#endif
