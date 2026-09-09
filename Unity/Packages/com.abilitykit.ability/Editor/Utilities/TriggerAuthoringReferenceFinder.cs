#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using AbilityKit.Ability.Config.Authoring;

namespace AbilityKit.Ability.Editor.Utilities
{
    internal sealed class TriggerAuthoringReference
    {
        public TriggerAuthoringModuleAsset Module;
        public string ModuleId;
        public int TriggerIndex = -1;
        public string TriggerName;
        public string Location;

        public string BuildLabel()
        {
            var module = string.IsNullOrWhiteSpace(ModuleId) ? "<无 ID>" : ModuleId;
            if (TriggerIndex < 0) return module + "  [" + Location + "]";
            var trigger = string.IsNullOrWhiteSpace(TriggerName) ? "#" + TriggerIndex : TriggerName;
            return module + "  ->  " + trigger + "  [" + Location + "]";
        }
    }

    /// <summary>
    /// 项目内引用搜索：回答"哪些触发器在用这个事件/组/模板/全局黑板键"，
    /// 供改名或删除前的破坏面评估。
    /// </summary>
    internal static class TriggerAuthoringReferenceFinder
    {
        public static List<TriggerAuthoringReference> FindEventReferences(
            TriggerAuthoringProjectAsset project,
            string eventId)
        {
            var result = new List<TriggerAuthoringReference>();
            if (project == null || string.IsNullOrWhiteSpace(eventId)) return result;
            var templates = TriggerTemplateDescriptorCatalog.FromAsset(project.TemplateCatalog);
            ForEachTrigger(project, (module, index, trigger) =>
            {
                var effective = TriggerAuthoringTemplateDefinition.ResolveEffectiveView(trigger, templates);
                if (effective?.Event != null &&
                    (string.Equals(effective.Event, eventId, StringComparison.Ordinal) ||
                     effective.Event.StartsWith(eventId + ".", StringComparison.Ordinal) ||
                     eventId.StartsWith(effective.Event + ".", StringComparison.Ordinal)))
                {
                    Add(result, module, index, trigger, "事件");
                }
            });
            return result;
        }

        public static List<TriggerAuthoringReference> FindGroupReferences(
            TriggerAuthoringProjectAsset project,
            string groupId)
        {
            var result = new List<TriggerAuthoringReference>();
            if (project == null || string.IsNullOrWhiteSpace(groupId)) return result;
            ForEachTrigger(project, (module, index, trigger) =>
            {
                if (NodeReferencesGroup(trigger.Condition, groupId) || NodeReferencesGroup(trigger.Actions, groupId))
                    Add(result, module, index, trigger, "分组");
            });
            ForEachGroupRoot(project, (module, group) =>
            {
                if (group.Root != null && NodeReferencesGroup(group.Root, groupId))
                {
                    result.Add(new TriggerAuthoringReference
                    {
                        Module = module,
                        ModuleId = module.Module?.ModuleId,
                        Location = "分组：" + group.Id
                    });
                }
            });
            return result;
        }

        public static List<TriggerAuthoringReference> FindTemplateReferences(
            TriggerAuthoringProjectAsset project,
            string templateId)
        {
            var result = new List<TriggerAuthoringReference>();
            if (project == null || string.IsNullOrWhiteSpace(templateId)) return result;
            ForEachTrigger(project, (module, index, trigger) =>
            {
                if (trigger.Template != null &&
                    string.Equals(trigger.Template.TemplateId, templateId, StringComparison.Ordinal))
                    Add(result, module, index, trigger, "模板");
            });
            return result;
        }

        public static List<TriggerAuthoringReference> FindGlobalKeyReferences(
            TriggerAuthoringProjectAsset project,
            string key)
        {
            var result = new List<TriggerAuthoringReference>();
            if (project == null || string.IsNullOrWhiteSpace(key)) return result;
            ForEachTrigger(project, (module, index, trigger) =>
            {
                if (NodeReferencesGlobalKey(trigger.Condition, key) || NodeReferencesGlobalKey(trigger.Actions, key) ||
                    ReferencesGlobalKey(trigger.Template?.Bindings, key))
                {
                    Add(result, module, index, trigger, "全局黑板：" + key);
                }
            });
            return result;
        }

        private static bool NodeReferencesGroup(TriggerNodeData node, string groupId)
        {
            if (node == null) return false;
            if (string.Equals(node.GroupReference, groupId, StringComparison.Ordinal)) return true;
            if (NodeReferencesGroup(node.Condition, groupId)) return true;
            var children = node.Children;
            if (children != null)
            {
                for (var i = 0; i < children.Count; i++)
                    if (NodeReferencesGroup(children[i], groupId)) return true;
            }
            var elseChildren = node.ElseChildren;
            if (elseChildren != null)
                for (var i = 0; i < elseChildren.Count; i++)
                    if (NodeReferencesGroup(elseChildren[i], groupId)) return true;
            return false;
        }

        private static bool NodeReferencesGlobalKey(TriggerNodeData node, string key)
        {
            if (node == null) return false;
            if (ReferencesGlobalKey(node.Arguments, key)) return true;
            if (NodeReferencesGlobalKey(node.Condition, key)) return true;
            var children = node.Children;
            if (children != null)
            {
                for (var i = 0; i < children.Count; i++)
                    if (NodeReferencesGlobalKey(children[i], key)) return true;
            }
            var elseChildren = node.ElseChildren;
            if (elseChildren != null)
                for (var i = 0; i < elseChildren.Count; i++)
                    if (NodeReferencesGlobalKey(elseChildren[i], key)) return true;
            return false;
        }

        private static bool ReferencesGlobalKey(List<TriggerArgumentData> arguments, string key)
        {
            if (arguments == null) return false;
            for (var i = 0; i < arguments.Count; i++)
            {
                var argument = arguments[i];
                if (argument?.Value == null) continue;
                if (argument.Value.Source == TriggerValueSource.GlobalBlackboard &&
                    string.Equals(argument.Value.Path, key, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        private static void ForEachTrigger(
            TriggerAuthoringProjectAsset project,
            Action<TriggerAuthoringModuleAsset, int, TriggerDefinitionData> visit)
        {
            var modules = project.Modules;
            if (modules == null) return;
            for (var m = 0; m < modules.Count; m++)
            {
                var module = modules[m];
                var data = module != null ? module.Module : null;
                var triggers = data?.Triggers;
                if (triggers == null) continue;
                for (var i = 0; i < triggers.Count; i++)
                {
                    var trigger = triggers[i];
                    if (trigger != null) visit(module, i, trigger);
                }
            }
        }

        private static void ForEachGroupRoot(
            TriggerAuthoringProjectAsset project,
            Action<TriggerAuthoringModuleAsset, TriggerNodeGroupData> visit)
        {
            var modules = project.Modules;
            if (modules == null) return;
            for (var m = 0; m < modules.Count; m++)
            {
                var module = modules[m];
                var data = module != null ? module.Module : null;
                VisitGroups(data?.ConditionGroups, module, visit);
                VisitGroups(data?.ActionGroups, module, visit);
            }
        }

        private static void VisitGroups(
            List<TriggerNodeGroupData> groups,
            TriggerAuthoringModuleAsset module,
            Action<TriggerAuthoringModuleAsset, TriggerNodeGroupData> visit)
        {
            if (groups == null) return;
            for (var i = 0; i < groups.Count; i++)
            {
                if (groups[i] != null) visit(module, groups[i]);
            }
        }

        private static void Add(
            List<TriggerAuthoringReference> result,
            TriggerAuthoringModuleAsset module,
            int index,
            TriggerDefinitionData trigger,
            string location)
        {
            result.Add(new TriggerAuthoringReference
            {
                Module = module,
                ModuleId = module.Module?.ModuleId,
                TriggerIndex = index,
                TriggerName = trigger.Name,
                Location = location
            });
        }
    }

    internal sealed class TriggerAuthoringTriggerIdRefactorPlan
    {
        private readonly List<TriggerNodeData> _referenceNodes = new List<TriggerNodeData>();
        private readonly List<TriggerAuthoringModuleAsset> _affectedModules =
            new List<TriggerAuthoringModuleAsset>();

        public TriggerAuthoringModuleAsset TargetModule;
        public TriggerDefinitionData Target;
        public int OldId;
        public int NewId;
        public string Error;
        public readonly List<TriggerAuthoringReference> References = new List<TriggerAuthoringReference>();

        public bool IsValid => string.IsNullOrEmpty(Error) && TargetModule != null && Target != null;
        public IReadOnlyList<TriggerAuthoringModuleAsset> AffectedModules => _affectedModules;

        internal void AddAffectedModule(TriggerAuthoringModuleAsset module)
        {
            if (module != null && !_affectedModules.Contains(module)) _affectedModules.Add(module);
        }

        internal void AddReference(TriggerNodeData node, TriggerAuthoringReference reference)
        {
            if (node == null || reference == null) return;
            _referenceNodes.Add(node);
            References.Add(reference);
            AddAffectedModule(reference.Module);
        }

        public int Apply()
        {
            if (!IsValid) return 0;
            Target.Id = NewId;
            for (var i = 0; i < _referenceNodes.Count; i++)
                SetReferenceId(_referenceNodes[i], NewId);
            return _referenceNodes.Count;
        }

        private static void SetReferenceId(TriggerNodeData node, int triggerId)
        {
            var arguments = node?.Arguments;
            if (arguments == null) return;
            for (var i = 0; i < arguments.Count; i++)
            {
                var argument = arguments[i];
                if (argument == null ||
                    !string.Equals(argument.Name, TriggerAuthoringTriggerReuse.TriggerIdArgument, StringComparison.Ordinal) ||
                    argument.Value == null) continue;
                argument.Value.Source = TriggerValueSource.Constant;
                argument.Value.Type = TriggerValueType.Integer;
                argument.Value.IntegerValue = triggerId;
                return;
            }
        }
    }

    /// <summary>
    /// TriggerId 是项目级运行时标识。该服务负责项目级分配、冲突检查和受管引用的原子重写计划。
    /// </summary>
    internal static class TriggerAuthoringTriggerIdRefactor
    {
        public static int NextAvailableId(TriggerAuthoringModuleAsset context)
        {
            var used = new HashSet<int>();
            var modules = CollectModules(context);
            var maximum = 0;
            for (var m = 0; m < modules.Count; m++)
            {
                var triggers = modules[m]?.Module?.Triggers;
                if (triggers == null) continue;
                for (var i = 0; i < triggers.Count; i++)
                {
                    var id = triggers[i] != null ? triggers[i].Id : 0;
                    if (id <= 0) continue;
                    used.Add(id);
                    if (id > maximum) maximum = id;
                }
            }

            if (maximum < int.MaxValue) return maximum + 1;
            for (var candidate = 1; candidate < int.MaxValue; candidate++)
                if (!used.Contains(candidate)) return candidate;
            return 0;
        }

        public static TriggerAuthoringTriggerIdRefactorPlan BuildPlan(
            TriggerAuthoringModuleAsset context,
            TriggerDefinitionData target,
            int newId)
        {
            var plan = new TriggerAuthoringTriggerIdRefactorPlan
            {
                TargetModule = context,
                Target = target,
                OldId = target != null ? target.Id : 0,
                NewId = newId
            };
            if (context?.Module == null || target == null)
            {
                plan.Error = "没有可修改的触发器。";
                return plan;
            }
            if (!ContainsTrigger(context.Module.Triggers, target))
            {
                plan.Error = "目标触发器不属于当前模块。";
                return plan;
            }
            if (newId <= 0)
            {
                plan.Error = "TriggerId 必须大于零。";
                return plan;
            }
            if (newId == target.Id)
            {
                plan.Error = "新的 TriggerId 与当前值相同。";
                return plan;
            }

            var modules = CollectModules(context);
            for (var m = 0; m < modules.Count; m++)
            {
                var module = modules[m];
                var triggers = module?.Module?.Triggers;
                if (triggers == null) continue;
                for (var i = 0; i < triggers.Count; i++)
                {
                    var trigger = triggers[i];
                    if (trigger == null || ReferenceEquals(trigger, target)) continue;
                    var moduleId = string.IsNullOrWhiteSpace(module.Module?.ModuleId)
                        ? module.name
                        : module.Module.ModuleId;
                    if (target.Id > 0 && trigger.Id == target.Id)
                    {
                        plan.Error =
                            $"当前 TriggerId {target.Id} 在项目中并不唯一，无法判断已有引用的真实目标。" +
                            $"冲突项位于模块“{moduleId}”中的“{DisplayTrigger(trigger)}”。";
                        return plan;
                    }
                    if (trigger.Id == newId)
                    {
                        plan.Error = $"TriggerId {newId} 已被模块“{moduleId}”中的“{DisplayTrigger(trigger)}”使用。";
                        return plan;
                    }
                }
            }

            plan.AddAffectedModule(context);
            if (target.Id <= 0) return plan;
            for (var m = 0; m < modules.Count; m++)
                CollectModuleReferences(modules[m], target.Id, plan);
            return plan;
        }

        public static List<TriggerAuthoringReference> FindReferences(
            TriggerAuthoringModuleAsset context,
            int triggerId)
        {
            var references = new List<TriggerAuthoringReference>();
            if (context == null || triggerId <= 0) return references;
            var plan = new TriggerAuthoringTriggerIdRefactorPlan();
            var modules = CollectModules(context);
            for (var i = 0; i < modules.Count; i++)
                CollectModuleReferences(modules[i], triggerId, plan);
            references.AddRange(plan.References);
            return references;
        }

        private static List<TriggerAuthoringModuleAsset> CollectModules(TriggerAuthoringModuleAsset context)
        {
            var result = new List<TriggerAuthoringModuleAsset>();
            var seen = new HashSet<TriggerAuthoringModuleAsset>();
            var projectModules = context?.Project?.Modules;
            if (projectModules != null)
                for (var i = 0; i < projectModules.Count; i++)
                {
                    var module = projectModules[i];
                    if (module != null && seen.Add(module)) result.Add(module);
                }
            if (context != null && seen.Add(context)) result.Add(context);
            return result;
        }

        private static void CollectModuleReferences(
            TriggerAuthoringModuleAsset module,
            int triggerId,
            TriggerAuthoringTriggerIdRefactorPlan plan)
        {
            var data = module?.Module;
            if (data == null) return;
            var triggers = data.Triggers;
            if (triggers != null)
                for (var i = 0; i < triggers.Count; i++)
                {
                    var trigger = triggers[i];
                    if (trigger == null) continue;
                    var name = DisplayTrigger(trigger);
                    CollectNodeReferences(module, i, name, trigger.Condition, "条件", triggerId, plan);
                    CollectNodeReferences(module, i, name, trigger.Actions, "行为", triggerId, plan);
                }

            CollectGroupReferences(module, data.ConditionGroups, "条件分组", triggerId, plan);
            CollectGroupReferences(module, data.ActionGroups, "行为分组", triggerId, plan);
        }

        private static void CollectGroupReferences(
            TriggerAuthoringModuleAsset module,
            IReadOnlyList<TriggerNodeGroupData> groups,
            string label,
            int triggerId,
            TriggerAuthoringTriggerIdRefactorPlan plan)
        {
            if (groups == null) return;
            for (var i = 0; i < groups.Count; i++)
            {
                var group = groups[i];
                if (group == null) continue;
                CollectNodeReferences(
                    module,
                    -1,
                    null,
                    group.Root,
                    label + "：" + (string.IsNullOrWhiteSpace(group.Id) ? "<无 ID>" : group.Id),
                    triggerId,
                    plan);
            }
        }

        private static void CollectNodeReferences(
            TriggerAuthoringModuleAsset module,
            int triggerIndex,
            string triggerName,
            TriggerNodeData node,
            string path,
            int triggerId,
            TriggerAuthoringTriggerIdRefactorPlan plan)
        {
            if (node == null) return;
            if (TriggerAuthoringTriggerReuse.TryGetReferencedTriggerId(node, out var referencedId) &&
                referencedId == triggerId)
            {
                plan.AddReference(node, new TriggerAuthoringReference
                {
                    Module = module,
                    ModuleId = module?.Module?.ModuleId,
                    TriggerIndex = triggerIndex,
                    TriggerName = triggerName,
                    Location = path
                });
            }

            CollectNodeReferences(module, triggerIndex, triggerName, node.Condition, path + "/判断条件", triggerId, plan);
            var children = node.Children;
            if (children != null)
                for (var i = 0; i < children.Count; i++)
                    CollectNodeReferences(module, triggerIndex, triggerName, children[i], path + "/子节点 " + (i + 1), triggerId, plan);
            var elseChildren = node.ElseChildren;
            if (elseChildren != null)
                for (var i = 0; i < elseChildren.Count; i++)
                    CollectNodeReferences(module, triggerIndex, triggerName, elseChildren[i], path + "/否则 " + (i + 1), triggerId, plan);
        }

        private static bool ContainsTrigger(IReadOnlyList<TriggerDefinitionData> triggers, TriggerDefinitionData target)
        {
            if (triggers == null) return false;
            for (var i = 0; i < triggers.Count; i++)
                if (ReferenceEquals(triggers[i], target)) return true;
            return false;
        }

        private static string DisplayTrigger(TriggerDefinitionData trigger)
        {
            if (trigger == null) return "<空触发器>";
            return string.IsNullOrWhiteSpace(trigger.Name) ? "触发器 " + trigger.Id : trigger.Name;
        }
    }
}
#endif
