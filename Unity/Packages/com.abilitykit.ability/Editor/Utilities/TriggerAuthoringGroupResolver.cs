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
            return TryExpand(module, node, kind, false, out expanded, out failure);
        }

        internal static bool TryExpandForEditing(
            TriggerAuthoringModuleData module,
            TriggerNodeData node,
            TriggerNodeKind kind,
            out TriggerNodeData expanded,
            out TriggerGroupResolutionFailure failure)
        {
            return TryExpand(module, node, kind, true, out expanded, out failure);
        }

        private static bool TryExpand(
            TriggerAuthoringModuleData module,
            TriggerNodeData node,
            TriggerNodeKind kind,
            bool preserveDisabledContent,
            out TriggerNodeData expanded,
            out TriggerGroupResolutionFailure failure)
        {
            expanded = null;
            failure = null;
            if (node == null) return true;

            if (!TryBuildCatalog(module, TriggerNodeKind.Condition, out var conditionCatalog, out failure)) return false;
            if (!TryBuildCatalog(module, TriggerNodeKind.Action, out var actionCatalog, out failure)) return false;
            return TryExpandNode(
                node,
                kind,
                conditionCatalog,
                actionCatalog,
                new HashSet<string>(StringComparer.Ordinal),
                new HashSet<string>(StringComparer.Ordinal),
                preserveDisabledContent,
                out expanded,
                out failure);
        }

        public static bool TryExtract(
            TriggerAuthoringModuleData module,
            TriggerNodeData node,
            TriggerNodeKind kind,
            string groupId,
            string displayName,
            out TriggerNodeGroupData extractedGroup,
            out string error)
        {
            extractedGroup = null;
            error = null;
            if (module == null)
            {
                error = "触发器模块不存在。";
                return false;
            }
            if (node == null)
            {
                error = "没有可提取的节点。";
                return false;
            }
            if (!string.IsNullOrWhiteSpace(node.GroupReference))
            {
                error = "当前节点已经是可复用分组引用。";
                return false;
            }

            groupId = (groupId ?? string.Empty).Trim();
            if (groupId.Length == 0)
            {
                error = "分组 ID 不能为空。";
                return false;
            }

            var groups = GetOrCreateGroups(module, kind);
            for (var i = 0; i < groups.Count; i++)
            {
                var existing = groups[i];
                if (existing != null && string.Equals(existing.Id, groupId, StringComparison.Ordinal))
                {
                    error = $"分组 ID 已存在：{groupId}。";
                    return false;
                }
            }

            var referenceEnabled = node.Enabled;
            var root = CloneNode(node);
            root.Enabled = true;
            extractedGroup = new TriggerNodeGroupData
            {
                Id = groupId,
                DisplayName = string.IsNullOrWhiteSpace(displayName) ? groupId : displayName.Trim(),
                Description = "从内嵌节点提取",
                Root = root
            };
            groups.Add(extractedGroup);

            node.Enabled = referenceEnabled;
            node.Kind = kind;
            node.GroupReference = groupId;
            node.Type = string.Empty;
            node.Note = string.Empty;
            node.Arguments = new List<TriggerArgumentData>();
            node.Condition = null;
            node.Children = new List<TriggerNodeData>();
            node.ElseChildren = new List<TriggerNodeData>();
            return true;
        }

        public static bool TryLocalize(
            TriggerAuthoringModuleData module,
            TriggerNodeData node,
            TriggerNodeKind kind,
            out TriggerGroupResolutionFailure failure)
        {
            if (!TryExpand(module, node, kind, true, out var expanded, out failure)) return false;
            if (node == null || expanded == null) return true;

            var referenceEnabled = node.Enabled;
            CopyNode(node, expanded);
            node.Enabled = referenceEnabled && expanded.Enabled;
            return true;
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
                Condition = CloneNode(node.Condition),
                Children = new List<TriggerNodeData>(),
                ElseChildren = new List<TriggerNodeData>()
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
            if (node.ElseChildren != null)
            {
                for (var i = 0; i < node.ElseChildren.Count; i++)
                    clone.ElseChildren.Add(CloneNode(node.ElseChildren[i]));
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
                    $"{(kind == TriggerNodeKind.Condition ? "条件" : "行为")}分组 ID 重复：{group.Id}。");
                    return false;
                }
                catalog.Add(group.Id, group);
            }
            return true;
        }

        private static bool TryExpandNode(
            TriggerNodeData node,
            TriggerNodeKind kind,
            IReadOnlyDictionary<string, TriggerNodeGroupData> conditionCatalog,
            IReadOnlyDictionary<string, TriggerNodeGroupData> actionCatalog,
            ISet<string> resolvingConditions,
            ISet<string> resolvingActions,
            bool preserveDisabledContent,
            out TriggerNodeData expanded,
            out TriggerGroupResolutionFailure failure)
        {
            expanded = null;
            failure = null;
            if (node == null) return true;
            if (!node.Enabled && !preserveDisabledContent)
            {
                expanded = new TriggerNodeData
                {
                    Enabled = false,
                    Kind = kind,
                    Arguments = new List<TriggerArgumentData>(),
                    Children = new List<TriggerNodeData>(),
                    ElseChildren = new List<TriggerNodeData>()
                };
                return true;
            }

            if (!string.IsNullOrWhiteSpace(node.GroupReference))
            {
                var groupId = node.GroupReference;
                var catalog = kind == TriggerNodeKind.Condition ? conditionCatalog : actionCatalog;
                var resolving = kind == TriggerNodeKind.Condition ? resolvingConditions : resolvingActions;
                if (!catalog.TryGetValue(groupId, out var group) || group == null)
                {
                    failure = new TriggerGroupResolutionFailure(
                        "TRG1505",
                        groupId,
                    $"未找到{(kind == TriggerNodeKind.Condition ? "条件" : "行为")}分组引用：{groupId}。");
                    return false;
                }
                if (group.Root == null)
                {
                    failure = new TriggerGroupResolutionFailure(
                        "TRG1503",
                        groupId,
                    $"{(kind == TriggerNodeKind.Condition ? "条件" : "行为")}分组“{groupId}”没有根节点。");
                    return false;
                }
                if (!resolving.Add(groupId))
                {
                    failure = new TriggerGroupResolutionFailure(
                        "TRG1506",
                        groupId,
                    $"检测到{(kind == TriggerNodeKind.Condition ? "条件" : "行为")}分组循环引用：{groupId}。");
                    return false;
                }

                try
                {
                    var success = TryExpandNode(
                        group.Root,
                        kind,
                        conditionCatalog,
                        actionCatalog,
                        resolvingConditions,
                        resolvingActions,
                        preserveDisabledContent,
                        out expanded,
                        out failure);
                    if (success && expanded != null && preserveDisabledContent)
                        expanded.Enabled = node.Enabled && expanded.Enabled;
                    return success;
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
                Condition = null,
                Children = new List<TriggerNodeData>(),
                ElseChildren = new List<TriggerNodeData>()
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

            if (node.Condition != null &&
                !TryExpandNode(
                    node.Condition,
                    TriggerNodeKind.Condition,
                    conditionCatalog,
                    actionCatalog,
                    resolvingConditions,
                    resolvingActions,
                    preserveDisabledContent,
                    out expanded.Condition,
                    out failure))
            {
                expanded = null;
                return false;
            }

            if (node.Children != null)
            {
                for (var i = 0; i < node.Children.Count; i++)
                {
                    if (!TryExpandNode(
                            node.Children[i],
                            kind,
                            conditionCatalog,
                            actionCatalog,
                            resolvingConditions,
                            resolvingActions,
                            preserveDisabledContent,
                            out var child,
                            out failure))
                    {
                        expanded = null;
                        return false;
                    }
                    expanded.Children.Add(child);
                }
            }
            if (node.ElseChildren != null)
            {
                for (var i = 0; i < node.ElseChildren.Count; i++)
                {
                    if (!TryExpandNode(
                            node.ElseChildren[i],
                            kind,
                            conditionCatalog,
                            actionCatalog,
                            resolvingConditions,
                            resolvingActions,
                            preserveDisabledContent,
                            out var child,
                            out failure))
                    {
                        expanded = null;
                        return false;
                    }
                    expanded.ElseChildren.Add(child);
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

        private static List<TriggerNodeGroupData> GetOrCreateGroups(
            TriggerAuthoringModuleData module,
            TriggerNodeKind kind)
        {
            if (kind == TriggerNodeKind.Condition)
                return module.ConditionGroups ?? (module.ConditionGroups = new List<TriggerNodeGroupData>());
            return module.ActionGroups ?? (module.ActionGroups = new List<TriggerNodeGroupData>());
        }

        private static void CopyNode(TriggerNodeData target, TriggerNodeData source)
        {
            var copy = CloneNode(source);
            target.Enabled = copy.Enabled;
            target.Kind = copy.Kind;
            target.GroupReference = copy.GroupReference;
            target.Type = copy.Type;
            target.Note = copy.Note;
            target.Arguments = copy.Arguments;
            target.Condition = copy.Condition;
            target.Children = copy.Children;
            target.ElseChildren = copy.ElseChildren;
        }

        internal static TriggerValueRefData CloneValue(TriggerValueRefData value)
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

    internal static class TriggerAuthoringConditionalChain
    {
        public static bool IsConditional(TriggerNodeData node)
        {
            return node != null &&
                   node.Kind == TriggerNodeKind.Action &&
                   string.Equals(node.Type, "conditional", StringComparison.OrdinalIgnoreCase);
        }

        public static bool TryGetElseIf(TriggerNodeData node, out TriggerNodeData elseIf)
        {
            elseIf = null;
            var actions = node?.ElseChildren;
            if (actions == null || actions.Count != 1 || !IsConditional(actions[0])) return false;
            elseIf = actions[0];
            return true;
        }

        public static void CollectElseIfBranches(TriggerNodeData root, ICollection<TriggerNodeData> output)
        {
            if (root == null || output == null) return;
            var visited = new HashSet<TriggerNodeData>();
            var current = root;
            while (current != null && visited.Add(current))
            {
                if (!TryGetElseIf(current, out var next)) break;
                output.Add(next);
                current = next;
            }
        }

        public static List<TriggerNodeData> GetFallbackActions(TriggerNodeData root)
        {
            if (root == null) return null;
            var visited = new HashSet<TriggerNodeData>();
            var current = root;
            while (current != null && visited.Add(current))
            {
                if (!TryGetElseIf(current, out var next)) break;
                current = next;
            }
            return current.ElseChildren ?? (current.ElseChildren = new List<TriggerNodeData>());
        }

        public static bool AppendElseIf(TriggerNodeData root, TriggerNodeData branch)
        {
            if (root == null || !IsConditional(branch)) return false;
            var visited = new HashSet<TriggerNodeData>();
            var current = root;
            while (current != null)
            {
                if (!visited.Add(current)) return false;
                if (!TryGetElseIf(current, out var next)) break;
                current = next;
            }

            branch.ElseChildren = current.ElseChildren ?? new List<TriggerNodeData>();
            current.ElseChildren = new List<TriggerNodeData> { branch };
            return true;
        }

        public static bool RemoveElseIf(TriggerNodeData root, TriggerNodeData branch)
        {
            if (root == null || branch == null) return false;
            var visited = new HashSet<TriggerNodeData>();
            var current = root;
            while (current != null && visited.Add(current))
            {
                if (!TryGetElseIf(current, out var next)) break;
                if (ReferenceEquals(next, branch))
                {
                    current.ElseChildren = next.ElseChildren ?? new List<TriggerNodeData>();
                    return true;
                }
                current = next;
            }
            return false;
        }
    }

    internal static class TriggerAuthoringTriggerReuse
    {
        public const string ExecuteTriggerType = "execute_trigger";
        public const string TriggerIdArgument = "trigger_id";

        public static bool IsReference(TriggerNodeData node)
        {
            return node != null &&
                   node.Kind == TriggerNodeKind.Action &&
                   string.Equals(node.Type, ExecuteTriggerType, StringComparison.Ordinal);
        }

        public static bool TryGetReferencedTriggerId(TriggerNodeData node, out int triggerId)
        {
            triggerId = 0;
            if (!IsReference(node) || node.Arguments == null) return false;
            for (var i = 0; i < node.Arguments.Count; i++)
            {
                var argument = node.Arguments[i];
                if (argument == null ||
                    !string.Equals(argument.Name, TriggerIdArgument, StringComparison.Ordinal) ||
                    argument.Value == null ||
                    argument.Value.Source != TriggerValueSource.Constant) continue;
                triggerId = (int)argument.Value.IntegerValue;
                return triggerId > 0;
            }
            return false;
        }

        public static TriggerDefinitionData FindTrigger(TriggerAuthoringModuleData module, int triggerId)
        {
            var triggers = module?.Triggers;
            if (triggers == null || triggerId <= 0) return null;
            for (var i = 0; i < triggers.Count; i++)
            {
                var trigger = triggers[i];
                if (trigger != null && trigger.Id == triggerId) return trigger;
            }
            return null;
        }

        public static bool TryExtract(
            TriggerAuthoringModuleData module,
            TriggerDefinitionData sourceTrigger,
            TriggerNodeData sourceNode,
            int newTriggerId,
            string displayName,
            out TriggerDefinitionData extractedTrigger,
            out string error)
        {
            extractedTrigger = null;
            error = null;
            if (module == null || sourceTrigger == null)
            {
                error = "只能从触发器中的行为节点提取独立触发效果。";
                return false;
            }
            if (sourceNode == null || sourceNode.Kind != TriggerNodeKind.Action)
            {
                error = "只有行为节点可以提取为独立触发效果。";
                return false;
            }
            if (IsReference(sourceNode))
            {
                error = "当前节点已经引用了独立触发效果。";
                return false;
            }
            if (newTriggerId <= 0 || FindTrigger(module, newTriggerId) != null)
            {
                error = $"触发器 ID 无效或已存在：{newTriggerId}。";
                return false;
            }

            var referenceEnabled = sourceNode.Enabled;
            var extractedActions = TriggerAuthoringGroupResolver.CloneNode(sourceNode);
            extractedActions.Enabled = true;
            extractedTrigger = new TriggerDefinitionData
            {
                Id = newTriggerId,
                Name = string.IsNullOrWhiteSpace(displayName)
                    ? "可复用触发效果 " + newTriggerId
                    : displayName.Trim(),
                GroupPath = string.IsNullOrWhiteSpace(sourceTrigger.GroupPath)
                    ? "复用效果"
                    : sourceTrigger.GroupPath + "/复用效果",
                Enabled = true,
                EntryMode = TriggerEntryMode.Callable,
                Event = string.Empty,
                Phase = "immediate",
                Scope = sourceTrigger.Scope,
                Actions = extractedActions,
                Note = "从触发器 " + sourceTrigger.Id + " 的内嵌行为提取"
            };

            var triggers = module.Triggers ?? (module.Triggers = new List<TriggerDefinitionData>());
            var sourceIndex = triggers.IndexOf(sourceTrigger);
            if (sourceIndex >= 0) triggers.Insert(sourceIndex + 1, extractedTrigger);
            else triggers.Add(extractedTrigger);
            ApplyReference(sourceNode, newTriggerId, referenceEnabled);
            return true;
        }

        public static bool TryLocalize(
            TriggerAuthoringModuleData module,
            TriggerNodeData referenceNode,
            out TriggerDefinitionData referencedTrigger,
            out string error)
        {
            return TryLocalize(module, referenceNode, null, out referencedTrigger, out error);
        }

        public static bool TryLocalize(
            TriggerAuthoringModuleData module,
            TriggerNodeData referenceNode,
            TriggerTemplateDescriptorCatalog templates,
            out TriggerDefinitionData referencedTrigger,
            out string error)
        {
            referencedTrigger = null;
            error = null;
            if (!TryGetReferencedTriggerId(referenceNode, out var triggerId))
            {
                error = "当前节点没有有效的 TriggerId 引用。";
                return false;
            }

            referencedTrigger = FindTrigger(module, triggerId);
            if (referencedTrigger == null)
            {
                error = $"未找到触发器：{triggerId}。";
                return false;
            }
            var destinationTrigger = FindContainingTrigger(module, referenceNode);
            if (!TryCreateLocalCopy(
                    module,
                    referencedTrigger,
                    destinationTrigger,
                    true,
                    templates,
                    out var localized,
                    out var importedVariables,
                    out error))
                return false;

            var referenceEnabled = referenceNode.Enabled;
            CopyNode(referenceNode, localized);
            referenceNode.Enabled = referenceEnabled && localized.Enabled;
            if (importedVariables.Count > 0)
            {
                destinationTrigger.Blackboard = destinationTrigger.Blackboard ?? new List<TriggerBlackboardVariableData>();
                destinationTrigger.Blackboard.AddRange(importedVariables);
            }
            return true;
        }

        public static bool TryCreateLocalCopy(
            TriggerAuthoringModuleData module,
            TriggerDefinitionData trigger,
            TriggerTemplateDescriptorCatalog templates,
            out TriggerNodeData localized,
            out string error)
        {
            return TryCreateLocalCopy(
                module,
                trigger,
                null,
                false,
                templates,
                out localized,
                out _,
                out error);
        }

        private static bool TryCreateLocalCopy(
            TriggerAuthoringModuleData module,
            TriggerDefinitionData trigger,
            TriggerDefinitionData destinationTrigger,
            bool importLocalVariables,
            TriggerTemplateDescriptorCatalog templates,
            out TriggerNodeData localized,
            out List<TriggerBlackboardVariableData> importedVariables,
            out string error)
        {
            localized = null;
            importedVariables = new List<TriggerBlackboardVariableData>();
            error = null;
            if (trigger == null)
            {
                error = "目标触发器不存在。";
                return false;
            }

            var conditionSource = trigger.Condition;
            var actionSource = trigger.Actions;
            var sourceVariables = trigger.Blackboard;
            Dictionary<string, string> localKeyMap = null;
            if (trigger.Template != null)
            {
                if (templates == null ||
                    !templates.TryGet(trigger.Template.TemplateId, out var templateAsset) ||
                    templateAsset?.Template == null)
                {
                    error = $"无法解析目标触发器使用的模板：{trigger.Template.TemplateId ?? string.Empty}。";
                    return false;
                }

                var template = templateAsset.Template;
                sourceVariables = CollectTemplateInternalVariables(template);
                if (importLocalVariables && !TryBuildLocalVariableImportPlan(
                        trigger,
                        destinationTrigger,
                        sourceVariables,
                        out localKeyMap,
                        out importedVariables,
                        out error))
                    return false;
                var materialized = TriggerAuthoringTemplateDefinition.CreateMaterialized(
                    trigger,
                    template,
                    localKeyMap,
                    out error);
                if (materialized == null)
                    return false;
                conditionSource = materialized.Condition;
                actionSource = materialized.Actions;
            }
            else
            {
                if (importLocalVariables && !TryBuildLocalVariableImportPlan(
                        trigger,
                        destinationTrigger,
                        sourceVariables,
                        out localKeyMap,
                        out importedVariables,
                        out error))
                    return false;
                conditionSource = TriggerAuthoringGroupResolver.CloneNode(conditionSource);
                actionSource = TriggerAuthoringGroupResolver.CloneNode(actionSource);
                RewriteTriggerLocalReferences(conditionSource, localKeyMap);
                RewriteTriggerLocalReferences(actionSource, localKeyMap);
            }

            if (!TriggerAuthoringGroupResolver.TryExpandForEditing(
                    module,
                    actionSource,
                    TriggerNodeKind.Action,
                    out var actions,
                    out var actionFailure))
            {
                error = actionFailure?.Message ?? "无法解析目标触发器的行为逻辑。";
                return false;
            }
            if (actions == null)
            {
                error = $"触发器 {trigger.Id} 没有行为逻辑。";
                return false;
            }

            localized = actions;
            if (conditionSource == null) return true;
            if (!TriggerAuthoringGroupResolver.TryExpandForEditing(
                    module,
                    conditionSource,
                    TriggerNodeKind.Condition,
                    out var condition,
                    out var conditionFailure))
            {
                localized = null;
                error = conditionFailure?.Message ?? "无法解析目标触发器的条件逻辑。";
                return false;
            }

            localized = new TriggerNodeData
            {
                Enabled = true,
                Kind = TriggerNodeKind.Action,
                Type = "conditional",
                Note = "由触发器 " + trigger.Id + " 的入口条件转换",
                Condition = condition,
                Children = new List<TriggerNodeData> { actions },
                ElseChildren = new List<TriggerNodeData>()
            };
            return true;
        }

        private static List<TriggerBlackboardVariableData> CollectTemplateInternalVariables(
            TriggerAuthoringTemplateData template)
        {
            var result = new List<TriggerBlackboardVariableData>();
            var definition = TriggerAuthoringTemplateDefinition.Get(template);
            var variables = definition?.Blackboard;
            if (variables == null) return result;
            var inputKeys = new HashSet<string>(StringComparer.Ordinal);
            var parameters = template.Parameters;
            if (parameters != null)
                for (var i = 0; i < parameters.Count; i++)
                    if (!string.IsNullOrWhiteSpace(parameters[i]?.LocalVariableKey))
                        inputKeys.Add(parameters[i].LocalVariableKey);
            for (var i = 0; i < variables.Count; i++)
            {
                var variable = variables[i];
                if (variable != null && !inputKeys.Contains(variable.Key ?? string.Empty))
                    result.Add(variable);
            }
            return result;
        }

        private static bool TryBuildLocalVariableImportPlan(
            TriggerDefinitionData sourceTrigger,
            TriggerDefinitionData destinationTrigger,
            IReadOnlyList<TriggerBlackboardVariableData> sourceVariables,
            out Dictionary<string, string> keyMap,
            out List<TriggerBlackboardVariableData> importedVariables,
            out string error)
        {
            keyMap = new Dictionary<string, string>(StringComparer.Ordinal);
            importedVariables = new List<TriggerBlackboardVariableData>();
            error = null;
            if (sourceVariables == null || sourceVariables.Count == 0) return true;
            if (destinationTrigger == null)
            {
                error = "目标逻辑包含触发器 LocalVar，但无法确定本地副本所属的触发器。";
                return false;
            }

            var usedKeys = new HashSet<string>(StringComparer.Ordinal);
            var destinationVariables = destinationTrigger.Blackboard;
            if (destinationVariables != null)
                for (var i = 0; i < destinationVariables.Count; i++)
                    if (!string.IsNullOrWhiteSpace(destinationVariables[i]?.Key))
                        usedKeys.Add(destinationVariables[i].Key);

            for (var i = 0; i < sourceVariables.Count; i++)
            {
                var variable = sourceVariables[i];
                if (variable == null || string.IsNullOrWhiteSpace(variable.Key)) continue;
                if (keyMap.ContainsKey(variable.Key))
                {
                    error = $"目标触发器包含重复的 LocalVar：{variable.Key}。";
                    return false;
                }

                var importedKey = CreateImportedLocalKey(variable.Key, sourceTrigger.Id, usedKeys);
                keyMap.Add(variable.Key, importedKey);
                usedKeys.Add(importedKey);
                importedVariables.Add(new TriggerBlackboardVariableData
                {
                    Key = importedKey,
                    Type = variable.Type,
                    ReadOnly = variable.ReadOnly,
                    Description = string.IsNullOrWhiteSpace(variable.Description)
                        ? $"由触发器 {sourceTrigger.Id} 的本地副本导入"
                        : variable.Description,
                    DefaultValue = TriggerAuthoringGroupResolver.CloneValue(variable.DefaultValue)
                });
            }
            return true;
        }

        private static string CreateImportedLocalKey(string sourceKey, int sourceTriggerId, ISet<string> usedKeys)
        {
            if (!usedKeys.Contains(sourceKey)) return sourceKey;
            var prefix = "trigger_" + sourceTriggerId + "_" + sourceKey;
            var candidate = prefix;
            var suffix = 2;
            while (usedKeys.Contains(candidate)) candidate = prefix + "_" + suffix++;
            return candidate;
        }

        private static TriggerDefinitionData FindContainingTrigger(
            TriggerAuthoringModuleData module,
            TriggerNodeData target)
        {
            var triggers = module?.Triggers;
            if (triggers == null || target == null) return null;
            for (var i = 0; i < triggers.Count; i++)
            {
                var trigger = triggers[i];
                if (trigger != null &&
                    (ContainsNode(trigger.Condition, target) || ContainsNode(trigger.Actions, target)))
                    return trigger;
            }
            return null;
        }

        private static bool ContainsNode(TriggerNodeData node, TriggerNodeData target)
        {
            if (node == null) return false;
            if (ReferenceEquals(node, target) || ContainsNode(node.Condition, target)) return true;
            if (ContainsNode(node.Children, target) || ContainsNode(node.ElseChildren, target)) return true;
            return false;
        }

        private static bool ContainsNode(IReadOnlyList<TriggerNodeData> nodes, TriggerNodeData target)
        {
            if (nodes == null) return false;
            for (var i = 0; i < nodes.Count; i++)
                if (ContainsNode(nodes[i], target)) return true;
            return false;
        }

        private static void RewriteTriggerLocalReferences(
            TriggerNodeData node,
            IReadOnlyDictionary<string, string> keyMap)
        {
            if (node == null || keyMap == null || keyMap.Count == 0) return;
            var arguments = node.Arguments;
            if (arguments != null)
                for (var i = 0; i < arguments.Count; i++)
                    RewriteTriggerLocalValue(arguments[i]?.Value, keyMap);
            RewriteTriggerLocalReferences(node.Condition, keyMap);
            RewriteTriggerLocalReferences(node.Children, keyMap);
            RewriteTriggerLocalReferences(node.ElseChildren, keyMap);
        }

        private static void RewriteTriggerLocalReferences(
            IReadOnlyList<TriggerNodeData> nodes,
            IReadOnlyDictionary<string, string> keyMap)
        {
            if (nodes == null) return;
            for (var i = 0; i < nodes.Count; i++) RewriteTriggerLocalReferences(nodes[i], keyMap);
        }

        private static void RewriteTriggerLocalValue(
            TriggerValueRefData value,
            IReadOnlyDictionary<string, string> keyMap)
        {
            if (value == null) return;
            if (value.Source == TriggerValueSource.LocalBlackboard &&
                TriggerAuthoringLocalBlackboardPath.TryParse(value.Path, out var scope, out var key) &&
                scope != TriggerAuthoringLocalBlackboardScope.Module &&
                keyMap.TryGetValue(key, out var importedKey))
            {
                value.Path = TriggerAuthoringLocalBlackboardPath.Format(
                    TriggerAuthoringLocalBlackboardScope.Trigger,
                    importedKey);
            }
            var fields = value.Fields;
            if (fields != null)
                for (var i = 0; i < fields.Count; i++)
                    RewriteTriggerLocalValue(fields[i]?.Value, keyMap);
        }

        private static void ApplyReference(TriggerNodeData node, int triggerId, bool enabled)
        {
            node.Enabled = enabled;
            node.Kind = TriggerNodeKind.Action;
            node.GroupReference = string.Empty;
            node.Type = ExecuteTriggerType;
            node.Note = string.Empty;
            node.Arguments = new List<TriggerArgumentData>
            {
                new TriggerArgumentData
                {
                    Name = TriggerIdArgument,
                    Value = new TriggerValueRefData
                    {
                        Source = TriggerValueSource.Constant,
                        Type = TriggerValueType.Integer,
                        IntegerValue = triggerId
                    }
                }
            };
            node.Condition = null;
            node.Children = new List<TriggerNodeData>();
            node.ElseChildren = new List<TriggerNodeData>();
        }

        private static void CopyNode(TriggerNodeData target, TriggerNodeData source)
        {
            var copy = TriggerAuthoringGroupResolver.CloneNode(source);
            target.Enabled = copy.Enabled;
            target.Kind = copy.Kind;
            target.GroupReference = copy.GroupReference;
            target.Type = copy.Type;
            target.Note = copy.Note;
            target.Arguments = copy.Arguments;
            target.Condition = copy.Condition;
            target.Children = copy.Children;
            target.ElseChildren = copy.ElseChildren;
        }
    }
}
#endif
