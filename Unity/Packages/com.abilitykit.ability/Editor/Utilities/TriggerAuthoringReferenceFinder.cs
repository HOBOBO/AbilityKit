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
            var module = string.IsNullOrWhiteSpace(ModuleId) ? "<no id>" : ModuleId;
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
            ForEachTrigger(project, (module, index, trigger) =>
            {
                if (trigger.Event != null &&
                    (string.Equals(trigger.Event, eventId, StringComparison.Ordinal) ||
                     trigger.Event.StartsWith(eventId + ".", StringComparison.Ordinal) ||
                     eventId.StartsWith(trigger.Event + ".", StringComparison.Ordinal)))
                {
                    Add(result, module, index, trigger, "event");
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
                    Add(result, module, index, trigger, "group");
            });
            ForEachGroupRoot(project, (module, group) =>
            {
                if (group.Root != null && NodeReferencesGroup(group.Root, groupId))
                {
                    result.Add(new TriggerAuthoringReference
                    {
                        Module = module,
                        ModuleId = module.Module?.ModuleId,
                        Location = "group:" + group.Id
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
                    Add(result, module, index, trigger, "template");
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
                    Add(result, module, index, trigger, "global:" + key);
                }
            });
            return result;
        }

        private static bool NodeReferencesGroup(TriggerNodeData node, string groupId)
        {
            if (node == null) return false;
            if (string.Equals(node.GroupReference, groupId, StringComparison.Ordinal)) return true;
            if (node.Children == null) return false;
            for (var i = 0; i < node.Children.Count; i++)
            {
                if (NodeReferencesGroup(node.Children[i], groupId)) return true;
            }
            return false;
        }

        private static bool NodeReferencesGlobalKey(TriggerNodeData node, string key)
        {
            if (node == null) return false;
            if (ReferencesGlobalKey(node.Arguments, key)) return true;
            if (node.Children == null) return false;
            for (var i = 0; i < node.Children.Count; i++)
            {
                if (NodeReferencesGlobalKey(node.Children[i], key)) return true;
            }
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
}
#endif
