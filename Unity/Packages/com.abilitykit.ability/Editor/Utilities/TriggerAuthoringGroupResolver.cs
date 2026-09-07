#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using AbilityKit.Ability.Config.Authoring;

namespace AbilityKit.Ability.Editor.Utilities
{
    internal sealed class TriggerGroupResolutionFailure
    {
        public TriggerGroupResolutionFailure(string code, string groupId, string message)
        {
            Code = code;
            GroupId = groupId ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public string Code { get; }
        public string GroupId { get; }
        public string Message { get; }
    }

    internal static class TriggerAuthoringGroupResolver
    {
        public static bool TryExpand(
            TriggerAuthoringModuleData module,
            TriggerNodeData node,
            TriggerNodeKind kind,
            out TriggerNodeData expanded,
            out TriggerGroupResolutionFailure failure)
        {
            expanded = null;
            failure = null;
            if (node == null) return true;

            if (!TryBuildCatalog(module, kind, out var catalog, out failure)) return false;
            return TryExpandNode(
                node,
                kind,
                catalog,
                new HashSet<string>(StringComparer.Ordinal),
                out expanded,
                out failure);
        }

        public static TriggerNodeData CloneNode(TriggerNodeData node)
        {
            if (node == null) return null;
            var clone = new TriggerNodeData
            {
                Enabled = node.Enabled,
                Kind = node.Kind,
                GroupReference = node.GroupReference,
                Type = node.Type,
                Note = node.Note,
                Arguments = new List<TriggerArgumentData>(),
                Children = new List<TriggerNodeData>()
            };

            if (node.Arguments != null)
            {
                for (var i = 0; i < node.Arguments.Count; i++)
                {
                    var argument = node.Arguments[i];
                    clone.Arguments.Add(argument == null
                        ? null
                        : new TriggerArgumentData
                        {
                            Name = argument.Name,
                            Value = CloneValue(argument.Value)
                        });
                }
            }

            if (node.Children != null)
            {
                for (var i = 0; i < node.Children.Count; i++)
                    clone.Children.Add(CloneNode(node.Children[i]));
            }
            return clone;
        }

        private static bool TryBuildCatalog(
            TriggerAuthoringModuleData module,
            TriggerNodeKind kind,
            out Dictionary<string, TriggerNodeGroupData> catalog,
            out TriggerGroupResolutionFailure failure)
        {
            catalog = new Dictionary<string, TriggerNodeGroupData>(StringComparer.Ordinal);
            failure = null;
            var groups = GetGroups(module, kind);
            if (groups == null) return true;

            for (var i = 0; i < groups.Count; i++)
            {
                var group = groups[i];
                if (group == null || string.IsNullOrWhiteSpace(group.Id)) continue;
                if (catalog.ContainsKey(group.Id))
                {
                    failure = new TriggerGroupResolutionFailure(
                        "TRG1502",
                        group.Id,
                        $"Duplicate {kind} group id: {group.Id}.");
                    return false;
                }
                catalog.Add(group.Id, group);
            }
            return true;
        }

        private static bool TryExpandNode(
            TriggerNodeData node,
            TriggerNodeKind kind,
            IReadOnlyDictionary<string, TriggerNodeGroupData> catalog,
            ISet<string> resolving,
            out TriggerNodeData expanded,
            out TriggerGroupResolutionFailure failure)
        {
            expanded = null;
            failure = null;
            if (node == null) return true;
            if (!node.Enabled)
            {
                expanded = new TriggerNodeData
                {
                    Enabled = false,
                    Kind = kind,
                    Arguments = new List<TriggerArgumentData>(),
                    Children = new List<TriggerNodeData>()
                };
                return true;
            }

            if (!string.IsNullOrWhiteSpace(node.GroupReference))
            {
                var groupId = node.GroupReference;
                if (!catalog.TryGetValue(groupId, out var group) || group == null)
                {
                    failure = new TriggerGroupResolutionFailure(
                        "TRG1505",
                        groupId,
                        $"{kind} group reference not found: {groupId}.");
                    return false;
                }
                if (group.Root == null)
                {
                    failure = new TriggerGroupResolutionFailure(
                        "TRG1503",
                        groupId,
                        $"{kind} group '{groupId}' has no root node.");
                    return false;
                }
                if (!resolving.Add(groupId))
                {
                    failure = new TriggerGroupResolutionFailure(
                        "TRG1506",
                        groupId,
                        $"Cyclic {kind} group reference detected: {groupId}.");
                    return false;
                }

                try
                {
                    return TryExpandNode(group.Root, kind, catalog, resolving, out expanded, out failure);
                }
                finally
                {
                    resolving.Remove(groupId);
                }
            }

            expanded = new TriggerNodeData
            {
                Enabled = node.Enabled,
                Kind = node.Kind,
                Type = node.Type,
                Note = node.Note,
                Arguments = new List<TriggerArgumentData>(),
                Children = new List<TriggerNodeData>()
            };

            if (node.Arguments != null)
            {
                for (var i = 0; i < node.Arguments.Count; i++)
                {
                    var argument = node.Arguments[i];
                    expanded.Arguments.Add(argument == null
                        ? null
                        : new TriggerArgumentData
                        {
                            Name = argument.Name,
                            Value = CloneValue(argument.Value)
                        });
                }
            }

            if (node.Children != null)
            {
                for (var i = 0; i < node.Children.Count; i++)
                {
                    if (!TryExpandNode(
                            node.Children[i],
                            kind,
                            catalog,
                            resolving,
                            out var child,
                            out failure))
                    {
                        expanded = null;
                        return false;
                    }
                    expanded.Children.Add(child);
                }
            }
            return true;
        }

        private static IReadOnlyList<TriggerNodeGroupData> GetGroups(
            TriggerAuthoringModuleData module,
            TriggerNodeKind kind)
        {
            if (module == null) return null;
            return kind == TriggerNodeKind.Condition ? module.ConditionGroups : module.ActionGroups;
        }

        private static TriggerValueRefData CloneValue(TriggerValueRefData value)
        {
            if (value == null) return null;
            return new TriggerValueRefData
            {
                Source = value.Source,
                Type = value.Type,
                IntegerValue = value.IntegerValue,
                NumberValue = value.NumberValue,
                BooleanValue = value.BooleanValue,
                StringValue = value.StringValue,
                IntegerListValue = value.IntegerListValue != null
                    ? new List<long>(value.IntegerListValue)
                    : new List<long>(),
                Vector3Value = value.Vector3Value == null
                    ? null
                    : new TriggerVector3Data
                    {
                        X = value.Vector3Value.X,
                        Y = value.Vector3Value.Y,
                        Z = value.Vector3Value.Z
                    },
                Fields = CloneArguments(value.Fields),
                Path = value.Path,
                Expression = value.Expression
            };
        }

        private static List<TriggerArgumentData> CloneArguments(IReadOnlyList<TriggerArgumentData> arguments)
        {
            var clone = new List<TriggerArgumentData>();
            if (arguments == null) return clone;
            for (var i = 0; i < arguments.Count; i++)
            {
                var argument = arguments[i];
                clone.Add(argument == null
                    ? null
                    : new TriggerArgumentData
                    {
                        Name = argument.Name,
                        Value = CloneValue(argument.Value)
                    });
            }
            return clone;
        }
    }
}
#endif
