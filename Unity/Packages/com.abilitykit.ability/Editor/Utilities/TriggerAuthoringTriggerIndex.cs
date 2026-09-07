#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using AbilityKit.Ability.Config.Authoring;

namespace AbilityKit.Ability.Editor.Utilities
{
    internal enum TriggerAuthoringTriggerGroupMode
    {
        Flat = 0,
        Event = 1,
        Status = 2,
        Scope = 3,
        Phase = 4,
        GroupPath = 5,
        Tag = 6
    }

    internal enum TriggerAuthoringTriggerQuickFilter
    {
        All = 0,
        Errors = 1,
        Warnings = 2,
        Disabled = 3,
        NoEvent = 4,
        NoGroup = 5,
        Untagged = 6
    }

    internal static class TriggerAuthoringTriggerIndex
    {
        public static List<Group> Build(
            IReadOnlyList<TriggerDefinitionData> triggers,
            IReadOnlyList<TriggerAuthoringDiagnostic> diagnostics,
            TriggerEventDescriptorCatalog events,
            TriggerAuthoringTriggerGroupMode groupMode,
            string searchText,
            TriggerAuthoringTriggerQuickFilter quickFilter = TriggerAuthoringTriggerQuickFilter.All)
        {
            var groups = new List<Group>();
            var byKey = new Dictionary<string, Group>(StringComparer.Ordinal);
            var filter = (searchText ?? string.Empty).Trim();
            var count = triggers != null ? triggers.Count : 0;
            for (var i = 0; i < count; i++)
            {
                var trigger = triggers[i];
                var diagnosticSummary = CountDiagnostics(i, diagnostics);
                var entry = new Entry(i, trigger, diagnosticSummary);
                if (!MatchesQuickFilter(entry, quickFilter)) continue;
                if (!Matches(entry, filter, diagnostics)) continue;

                var keys = GetGroupKeys(trigger, diagnosticSummary, events, groupMode);
                for (var keyIndex = 0; keyIndex < keys.Count; keyIndex++)
                {
                    var key = keys[keyIndex];
                    if (!byKey.TryGetValue(key, out var group))
                    {
                        group = new Group(key, GetGroupLabel(key, groupMode), GetGroupSortKey(key, groupMode));
                        byKey.Add(key, group);
                        groups.Add(group);
                    }
                    group.Entries.Add(entry);
                }
            }

            groups.Sort(CompareGroups);
            for (var i = 0; i < groups.Count; i++)
                groups[i].Entries.Sort(CompareEntries);
            return groups;
        }

        public static bool Matches(
            Entry entry,
            string searchText,
            IReadOnlyList<TriggerAuthoringDiagnostic> diagnostics = null)
        {
            var filter = (searchText ?? string.Empty).Trim();
            if (filter.Length == 0) return true;
            var trigger = entry.Trigger;
            if (trigger == null) return ContainsText("<null>", filter);

            if (ContainsText(trigger.Id.ToString(), filter) ||
                ContainsText(trigger.Name, filter) ||
                ContainsText(trigger.GroupPath, filter) ||
                ContainsText(trigger.Event, filter) ||
                ContainsText(trigger.Phase, filter) ||
                ContainsText(trigger.Scope, filter) ||
                ContainsText(trigger.Note, filter) ||
                ContainsText(trigger.Template != null ? trigger.Template.TemplateId : null, filter) ||
                ContainsText(trigger.Enabled ? "enabled" : "disabled", filter) ||
                ContainsText(entry.Diagnostics.Errors > 0 ? "error" : null, filter) ||
                ContainsText(entry.Diagnostics.Warnings > 0 ? "warning" : null, filter))
                return true;

            if (TagsMatch(trigger.Tags, filter)) return true;
            if (NodeMatches(trigger.Condition, filter) || NodeMatches(trigger.Actions, filter)) return true;
            if (BlackboardMatches(trigger.Blackboard, filter)) return true;

            var prefix = GetTriggerPathPrefix(entry.Index);
            if (diagnostics != null)
            {
                for (var i = 0; i < diagnostics.Count; i++)
                {
                    var diagnostic = diagnostics[i];
                    if (diagnostic == null || !IsAtOrBelow(diagnostic.Path, prefix)) continue;
                    if (ContainsText(diagnostic.Code, filter) ||
                        ContainsText(diagnostic.Message, filter) ||
                        ContainsText(diagnostic.Path, filter))
                        return true;
                }
            }
            return false;
        }

        public static bool MatchesQuickFilter(
            Entry entry,
            TriggerAuthoringTriggerQuickFilter quickFilter)
        {
            var trigger = entry.Trigger;
            switch (quickFilter)
            {
                case TriggerAuthoringTriggerQuickFilter.Errors:
                    return entry.Diagnostics.Errors > 0;
                case TriggerAuthoringTriggerQuickFilter.Warnings:
                    return entry.Diagnostics.Warnings > 0;
                case TriggerAuthoringTriggerQuickFilter.Disabled:
                    return trigger != null && !trigger.Enabled;
                case TriggerAuthoringTriggerQuickFilter.NoEvent:
                    return trigger != null && string.IsNullOrWhiteSpace(trigger.Event);
                case TriggerAuthoringTriggerQuickFilter.NoGroup:
                    return trigger != null && string.IsNullOrWhiteSpace(trigger.GroupPath);
                case TriggerAuthoringTriggerQuickFilter.Untagged:
                    return trigger != null && (trigger.Tags == null || trigger.Tags.Count == 0);
                default:
                    return true;
            }
        }

        private static bool NodeMatches(TriggerNodeData node, string filter)
        {
            if (node == null) return false;
            if (ContainsText(node.Type, filter) ||
                ContainsText(node.GroupReference, filter) ||
                ContainsText(node.Kind.ToString(), filter) ||
                ContainsText(node.Note, filter) ||
                ContainsText(node.Enabled ? "enabled" : "disabled", filter))
                return true;

            var arguments = node.Arguments;
            if (arguments != null)
            {
                for (var i = 0; i < arguments.Count; i++)
                {
                    var argument = arguments[i];
                    if (argument == null) continue;
                    if (ContainsText(argument.Name, filter) || ValueMatches(argument.Value, filter)) return true;
                }
            }

            var children = node.Children;
            if (children == null) return false;
            for (var i = 0; i < children.Count; i++)
                if (NodeMatches(children[i], filter))
                    return true;
            return false;
        }

        private static bool ValueMatches(TriggerValueRefData value, string filter)
        {
            if (value == null) return false;
            if (ContainsText(value.Source.ToString(), filter) ||
                ContainsText(value.Type.ToString(), filter) ||
                ContainsText(value.Path, filter) ||
                ContainsText(value.Expression, filter) ||
                ContainsText(value.StringValue, filter))
                return true;

            var fields = value.Fields;
            if (fields == null) return false;
            for (var i = 0; i < fields.Count; i++)
            {
                var field = fields[i];
                if (field == null) continue;
                if (ContainsText(field.Name, filter) || ValueMatches(field.Value, filter)) return true;
            }
            return false;
        }

        private static bool BlackboardMatches(IReadOnlyList<TriggerBlackboardVariableData> blackboard, string filter)
        {
            if (blackboard == null) return false;
            for (var i = 0; i < blackboard.Count; i++)
            {
                var variable = blackboard[i];
                if (variable == null) continue;
                if (ContainsText(variable.Key, filter) ||
                    ContainsText(variable.Type.ToString(), filter) ||
                    ContainsText(variable.Description, filter))
                    return true;
            }
            return false;
        }

        private static DiagnosticSummary CountDiagnostics(
            int triggerIndex,
            IReadOnlyList<TriggerAuthoringDiagnostic> diagnostics)
        {
            var errors = 0;
            var warnings = 0;
            var prefix = GetTriggerPathPrefix(triggerIndex);
            if (diagnostics != null)
            {
                for (var i = 0; i < diagnostics.Count; i++)
                {
                    var diagnostic = diagnostics[i];
                    if (diagnostic == null || !IsAtOrBelow(diagnostic.Path, prefix)) continue;
                    if (diagnostic.Severity == TriggerAuthoringDiagnosticSeverity.Error) errors++;
                    else if (diagnostic.Severity == TriggerAuthoringDiagnosticSeverity.Warning) warnings++;
                }
            }
            return new DiagnosticSummary(errors, warnings);
        }

        private static List<string> GetGroupKeys(
            TriggerDefinitionData trigger,
            DiagnosticSummary diagnostics,
            TriggerEventDescriptorCatalog events,
            TriggerAuthoringTriggerGroupMode groupMode)
        {
            if (groupMode != TriggerAuthoringTriggerGroupMode.Tag)
                return new List<string> { GetGroupKey(trigger, diagnostics, events, groupMode) };

            var tags = trigger != null ? trigger.Tags : null;
            if (tags == null || tags.Count == 0) return new List<string> { "tag:<untagged>" };

            var keys = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < tags.Count; i++)
            {
                var tag = DisplayOrUnassigned(tags[i]);
                if (tag == "<unassigned>") tag = "<untagged>";
                if (seen.Add(tag)) keys.Add("tag:" + tag);
            }
            return keys.Count > 0 ? keys : new List<string> { "tag:<untagged>" };
        }

        private static string GetGroupKey(
            TriggerDefinitionData trigger,
            DiagnosticSummary diagnostics,
            TriggerEventDescriptorCatalog events,
            TriggerAuthoringTriggerGroupMode groupMode)
        {
            switch (groupMode)
            {
                case TriggerAuthoringTriggerGroupMode.Event:
                    return GetEventGroupKey(trigger, events);
                case TriggerAuthoringTriggerGroupMode.GroupPath:
                    return "groupPath:" + DisplayOrUnassigned(trigger != null ? trigger.GroupPath : null);
                case TriggerAuthoringTriggerGroupMode.Status:
                    if (trigger == null) return "status:null";
                    if (!trigger.Enabled) return "status:disabled";
                    if (diagnostics.Errors > 0) return "status:errors";
                    if (diagnostics.Warnings > 0) return "status:warnings";
                    return "status:ready";
                case TriggerAuthoringTriggerGroupMode.Scope:
                    return "scope:" + DisplayOrUnassigned(trigger != null ? trigger.Scope : null);
                case TriggerAuthoringTriggerGroupMode.Phase:
                    return "phase:" + DisplayOrUnassigned(trigger != null ? trigger.Phase : null);
                default:
                    return "flat:all";
            }
        }

        private static string GetEventGroupKey(TriggerDefinitionData trigger, TriggerEventDescriptorCatalog events)
        {
            var eventId = trigger != null ? trigger.Event : null;
            if (string.IsNullOrWhiteSpace(eventId)) return "event:<unassigned>";
            var category = string.Empty;
            if (events != null && events.TryResolve(eventId, out var definition) && definition != null)
                category = definition.Category;
            return "event:" + DisplayOrUnassigned(category) + "/" + eventId;
        }

        private static string GetGroupLabel(string key, TriggerAuthoringTriggerGroupMode groupMode)
        {
            if (groupMode == TriggerAuthoringTriggerGroupMode.Flat) return "All Triggers";
            var separator = key.IndexOf(':');
            var value = separator >= 0 ? key.Substring(separator + 1) : key;
            switch (groupMode)
            {
                case TriggerAuthoringTriggerGroupMode.Status:
                    switch (value)
                    {
                        case "errors": return "Errors";
                        case "warnings": return "Warnings";
                        case "disabled": return "Disabled";
                        case "ready": return "Ready";
                        case "null": return "Invalid Rows";
                    }
                    break;
                case TriggerAuthoringTriggerGroupMode.Scope:
                    return "Scope / " + value;
                case TriggerAuthoringTriggerGroupMode.Phase:
                    return "Phase / " + value;
                case TriggerAuthoringTriggerGroupMode.Event:
                    return "Event / " + value;
                case TriggerAuthoringTriggerGroupMode.GroupPath:
                    return "Group / " + value;
                case TriggerAuthoringTriggerGroupMode.Tag:
                    return "Tag / " + value;
            }
            return value;
        }

        private static bool TagsMatch(IReadOnlyList<string> tags, string filter)
        {
            if (tags == null) return false;
            for (var i = 0; i < tags.Count; i++)
                if (ContainsText(tags[i], filter))
                    return true;
            return false;
        }

        private static string GetGroupSortKey(string key, TriggerAuthoringTriggerGroupMode groupMode)
        {
            if (groupMode != TriggerAuthoringTriggerGroupMode.Status) return key;
            if (key == "status:errors") return "0";
            if (key == "status:warnings") return "1";
            if (key == "status:disabled") return "2";
            if (key == "status:ready") return "3";
            return "9:" + key;
        }

        private static int CompareGroups(Group left, Group right)
        {
            return string.Compare(left.SortKey, right.SortKey, StringComparison.OrdinalIgnoreCase);
        }

        private static int CompareEntries(Entry left, Entry right)
        {
            var priority = right.Trigger != null && left.Trigger != null
                ? right.Trigger.Priority.CompareTo(left.Trigger.Priority)
                : 0;
            if (priority != 0) return priority;
            return left.Index.CompareTo(right.Index);
        }

        private static string DisplayOrUnassigned(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "<unassigned>" : value.Trim();
        }

        private static bool ContainsText(string value, string filter)
        {
            return !string.IsNullOrEmpty(value) &&
                   value.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsAtOrBelow(string path, string prefix)
        {
            return !string.IsNullOrEmpty(path) &&
                   (string.Equals(path, prefix, StringComparison.Ordinal) ||
                    path.StartsWith(prefix + ".", StringComparison.Ordinal));
        }

        private static string GetTriggerPathPrefix(int index)
        {
            return "module.triggers[" + index + "]";
        }

        internal sealed class Group
        {
            public Group(string key, string label, string sortKey)
            {
                Key = key ?? string.Empty;
                Label = label ?? string.Empty;
                SortKey = sortKey ?? string.Empty;
            }

            public string Key { get; }
            public string Label { get; }
            public string SortKey { get; }
            public List<Entry> Entries { get; } = new List<Entry>();
        }

        internal readonly struct Entry
        {
            public Entry(int index, TriggerDefinitionData trigger, DiagnosticSummary diagnostics)
            {
                Index = index;
                Trigger = trigger;
                Diagnostics = diagnostics;
            }

            public int Index { get; }
            public TriggerDefinitionData Trigger { get; }
            public DiagnosticSummary Diagnostics { get; }
        }

        internal readonly struct DiagnosticSummary
        {
            public DiagnosticSummary(int errors, int warnings)
            {
                Errors = errors;
                Warnings = warnings;
            }

            public int Errors { get; }
            public int Warnings { get; }
        }
    }
}
#endif
