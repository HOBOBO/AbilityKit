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
            var normalized = (groupPath ?? string.Empty).Trim();
            return Apply(triggers, indices, trigger =>
            {
                if (string.Equals(trigger.GroupPath ?? string.Empty, normalized, StringComparison.Ordinal))
                    return false;
                trigger.GroupPath = normalized;
                return true;
            });
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
