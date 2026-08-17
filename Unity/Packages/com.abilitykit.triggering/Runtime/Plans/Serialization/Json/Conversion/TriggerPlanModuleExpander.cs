using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace AbilityKit.Triggering.Runtime.Plan.Json
{
    using ActionCallPlanDto = TriggerPlanJsonDatabase.ActionCallPlanDto;
    using BoolExprNodeDto = TriggerPlanJsonDatabase.BoolExprNodeDto;
    using ExecutionControlPlanDto = TriggerPlanJsonDatabase.ExecutionControlPlanDto;
    using ExecutionNodeDto = TriggerPlanJsonDatabase.ExecutionNodeDto;
    using NumericValueRefDto = TriggerPlanJsonDatabase.NumericValueRefDto;
    using PredicatePlanDto = TriggerPlanJsonDatabase.PredicatePlanDto;
    using TemplateParameterDto = TriggerPlanJsonDatabase.TemplateParameterDto;
    using TriggerPlanDatabaseDto = TriggerPlanJsonDatabase.TriggerPlanDatabaseDto;
    using TriggerPlanDto = TriggerPlanJsonDatabase.TriggerPlanDto;
    using TriggerPlanModuleInstanceDto = TriggerPlanJsonDatabase.TriggerPlanModuleInstanceDto;
    using TriggerPlanModuleTemplateDto = TriggerPlanJsonDatabase.TriggerPlanModuleTemplateDto;
    using TriggerTemplateBindingDto = TriggerPlanJsonDatabase.TriggerTemplateBindingDto;

    /// <summary>
    /// 负责展开触发器计划模块/模板实例，避免 TriggerPlanJsonDatabase 同时承担 DTO 组装和模块实例化逻辑。
    /// </summary>
    internal static class TriggerPlanModuleExpander
    {
        public static TriggerPlanDatabaseDto Expand(TriggerPlanDatabaseDto dto)
        {
            if (dto == null)
            {
                return null;
            }

            var instances = CollectModuleInstances(dto);
            if (instances.Count == 0)
            {
                return dto;
            }

            var templates = BuildModuleTemplateCatalog(dto);
            var expanded = CloneDto(dto);
            expanded.Triggers = expanded.Triggers != null
                ? new List<TriggerPlanDto>(expanded.Triggers)
                : new List<TriggerPlanDto>();
            expanded.Behaviors = expanded.Behaviors != null
                ? new Dictionary<string, ExecutionNodeDto>(expanded.Behaviors, StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, ExecutionNodeDto>(StringComparer.OrdinalIgnoreCase);
            expanded.Nodes = expanded.Nodes != null
                ? new Dictionary<string, ExecutionNodeDto>(expanded.Nodes, StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, ExecutionNodeDto>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < instances.Count; i++)
            {
                ExpandModuleInstance(instances[i], templates, expanded);
            }

            return expanded;
        }

        private static List<TriggerPlanModuleInstanceDto> CollectModuleInstances(TriggerPlanDatabaseDto dto)
        {
            var instances = new List<TriggerPlanModuleInstanceDto>();
            if (dto?.ModuleInstances != null)
            {
                instances.AddRange(dto.ModuleInstances);
            }

            if (dto?.TemplateInstances != null)
            {
                instances.AddRange(dto.TemplateInstances);
            }

            return instances;
        }

        private static Dictionary<string, TriggerPlanModuleTemplateDto> BuildModuleTemplateCatalog(TriggerPlanDatabaseDto dto)
        {
            var catalog = new Dictionary<string, TriggerPlanModuleTemplateDto>(StringComparer.OrdinalIgnoreCase);
            AddModuleTemplates(catalog, dto?.Templates);
            AddModuleTemplates(catalog, dto?.Modules);
            return catalog;
        }

        private static void AddModuleTemplates(
            Dictionary<string, TriggerPlanModuleTemplateDto> catalog,
            Dictionary<string, TriggerPlanModuleTemplateDto> templates)
        {
            if (catalog == null || templates == null)
            {
                return;
            }

            foreach (var kv in templates)
            {
                var template = kv.Value;
                if (template == null)
                {
                    continue;
                }

                catalog[kv.Key] = template;
                AddModuleTemplateAlias(catalog, template.Id, template);
                AddModuleTemplateAlias(catalog, template.TemplateId, template);
                AddModuleTemplateAlias(catalog, template.ModuleId, template);
            }
        }

        private static void AddModuleTemplateAlias(
            Dictionary<string, TriggerPlanModuleTemplateDto> catalog,
            string id,
            TriggerPlanModuleTemplateDto template)
        {
            if (!string.IsNullOrEmpty(id))
            {
                catalog[id] = template;
            }
        }

        private static void ExpandModuleInstance(
            TriggerPlanModuleInstanceDto instance,
            Dictionary<string, TriggerPlanModuleTemplateDto> templates,
            TriggerPlanDatabaseDto target)
        {
            if (instance == null)
            {
                return;
            }

            var templateId = !string.IsNullOrEmpty(instance.TemplateId) ? instance.TemplateId : instance.ModuleId;
            if (string.IsNullOrEmpty(templateId))
            {
                throw new InvalidOperationException("Module instance requires TemplateId or ModuleId.");
            }

            if (templates == null || !templates.TryGetValue(templateId, out var template) || template == null)
            {
                throw new InvalidOperationException($"Module template not found: {templateId}");
            }

            var behaviorIdMap = BuildModuleScopedIdMap(template.Behaviors, instance);
            var nodeIdMap = BuildModuleScopedIdMap(template.Nodes, instance);
            MergeTemplateBehaviors(target, template, behaviorIdMap, nodeIdMap);
            MergeTemplateNodes(target, template, behaviorIdMap, nodeIdMap);
            if (template.Triggers == null || template.Triggers.Count == 0)
            {
                return;
            }

            for (int i = 0; i < template.Triggers.Count; i++)
            {
                var trigger = CloneTrigger(template.Triggers[i]);
                if (trigger == null)
                {
                    continue;
                }

                RewriteTriggerExecutionRefs(trigger, behaviorIdMap, nodeIdMap);
                ApplyModuleInstanceToTrigger(trigger, template, instance);
                target.Triggers.Add(trigger);
            }
        }

        private static Dictionary<string, string> BuildModuleScopedIdMap(
            Dictionary<string, ExecutionNodeDto> nodes,
            TriggerPlanModuleInstanceDto instance)
        {
            var idMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (nodes == null || nodes.Count == 0)
            {
                return idMap;
            }

            foreach (var kv in nodes)
            {
                idMap[kv.Key] = FormatModuleScopedId(kv.Key, instance);
            }

            return idMap;
        }

        private static void MergeTemplateBehaviors(
            TriggerPlanDatabaseDto target,
            TriggerPlanModuleTemplateDto template,
            Dictionary<string, string> behaviorIdMap,
            Dictionary<string, string> nodeIdMap)
        {
            if (template.Behaviors == null || template.Behaviors.Count == 0)
            {
                return;
            }

            foreach (var kv in template.Behaviors)
            {
                var node = CloneExecutionNode(kv.Value);
                RewriteExecutionNodeRefs(node, behaviorIdMap, nodeIdMap);
                target.Behaviors[behaviorIdMap[kv.Key]] = node;
            }
        }

        private static void MergeTemplateNodes(
            TriggerPlanDatabaseDto target,
            TriggerPlanModuleTemplateDto template,
            Dictionary<string, string> behaviorIdMap,
            Dictionary<string, string> nodeIdMap)
        {
            if (template.Nodes == null || template.Nodes.Count == 0)
            {
                return;
            }

            foreach (var kv in template.Nodes)
            {
                var node = CloneExecutionNode(kv.Value);
                RewriteExecutionNodeRefs(node, behaviorIdMap, nodeIdMap);
                target.Nodes[nodeIdMap[kv.Key]] = node;
            }
        }

        private static void ApplyModuleInstanceToTrigger(
            TriggerPlanDto trigger,
            TriggerPlanModuleTemplateDto template,
            TriggerPlanModuleInstanceDto instance)
        {
            var offset = instance.TriggerIdOffset != 0 ? instance.TriggerIdOffset : instance.TriggerIdBase;
            if (offset != 0)
            {
                trigger.TriggerId += offset;
            }

            if (!string.IsNullOrEmpty(instance.EventNamePrefix))
            {
                trigger.EventName = instance.EventNamePrefix + trigger.EventName;
                trigger.EventId = 0;
            }

            if (!string.IsNullOrEmpty(instance.EventNameSuffix))
            {
                trigger.EventName += instance.EventNameSuffix;
                trigger.EventId = 0;
            }

            trigger.Template = BuildModuleTriggerTemplateBinding(trigger.Template, template, instance);
        }

        private static TriggerTemplateBindingDto BuildModuleTriggerTemplateBinding(
            TriggerTemplateBindingDto triggerBinding,
            TriggerPlanModuleTemplateDto template,
            TriggerPlanModuleInstanceDto instance)
        {
            var bindings = new Dictionary<string, NumericValueRefDto>(StringComparer.OrdinalIgnoreCase);
            AddTemplateParameterDefaults(bindings, template?.Parameters);
            AddNumericBindings(bindings, template?.Defaults);
            AddNumericBindings(bindings, triggerBinding?.Bindings);
            AddNumericBindings(bindings, instance?.Bindings);
            ValidateRequiredTemplateParameters(bindings, template?.Parameters);

            return new TriggerTemplateBindingDto
            {
                TemplateId = !string.IsNullOrEmpty(instance?.TemplateId)
                    ? instance.TemplateId
                    : (!string.IsNullOrEmpty(instance?.ModuleId) ? instance.ModuleId : triggerBinding?.TemplateId),
                Bindings = bindings
            };
        }

        private static void AddTemplateParameterDefaults(
            Dictionary<string, NumericValueRefDto> bindings,
            List<TemplateParameterDto> parameters)
        {
            if (bindings == null || parameters == null)
            {
                return;
            }

            for (int i = 0; i < parameters.Count; i++)
            {
                var parameter = parameters[i];
                if (parameter == null || string.IsNullOrEmpty(parameter.Name))
                {
                    continue;
                }

                if (parameter.Default != null)
                {
                    bindings[parameter.Name] = CloneNumericValueRef(parameter.Default);
                }
            }
        }

        private static void ValidateRequiredTemplateParameters(
            Dictionary<string, NumericValueRefDto> bindings,
            List<TemplateParameterDto> parameters)
        {
            if (bindings == null || parameters == null)
            {
                return;
            }

            for (int i = 0; i < parameters.Count; i++)
            {
                var parameter = parameters[i];
                if (parameter == null || string.IsNullOrEmpty(parameter.Name) || !parameter.Required)
                {
                    continue;
                }

                if (!bindings.TryGetValue(parameter.Name, out var binding) || binding == null)
                {
                    throw new InvalidOperationException($"Required module template parameter has no binding: {parameter.Name}");
                }
            }
        }

        private static void RewriteTriggerExecutionRefs(
            TriggerPlanDto trigger,
            Dictionary<string, string> behaviorIdMap,
            Dictionary<string, string> nodeIdMap)
        {
            RewriteExecutionNodeRefs(trigger?.ExecutionRoot, behaviorIdMap, nodeIdMap);
        }

        private static void RewriteExecutionNodeRefs(
            ExecutionNodeDto node,
            Dictionary<string, string> behaviorIdMap,
            Dictionary<string, string> nodeIdMap)
        {
            if (node == null)
            {
                return;
            }

            RewriteRef(ref node.BehaviorRef, behaviorIdMap);
            RewriteRef(ref node.BehaviorId, behaviorIdMap);
            RewriteRef(ref node.NodeRef, nodeIdMap);
            RewriteRef(ref node.NodeId, nodeIdMap);
            if (!RewriteRef(ref node.Ref, behaviorIdMap))
            {
                RewriteRef(ref node.Ref, nodeIdMap);
            }

            RewriteExecutionNodeRefs(node.Children, behaviorIdMap, nodeIdMap);
            RewriteExecutionNodeRefs(node.ElseChildren, behaviorIdMap, nodeIdMap);
        }

        private static void RewriteExecutionNodeRefs(
            List<ExecutionNodeDto> nodes,
            Dictionary<string, string> behaviorIdMap,
            Dictionary<string, string> nodeIdMap)
        {
            if (nodes == null)
            {
                return;
            }

            for (int i = 0; i < nodes.Count; i++)
            {
                RewriteExecutionNodeRefs(nodes[i], behaviorIdMap, nodeIdMap);
            }
        }

        private static bool RewriteRef(ref string id, Dictionary<string, string> idMap)
        {
            if (string.IsNullOrEmpty(id) || idMap == null || !idMap.TryGetValue(id, out var scopedId))
            {
                return false;
            }

            id = scopedId;
            return true;
        }

        private static void AddNumericBindings(
            Dictionary<string, NumericValueRefDto> target,
            Dictionary<string, NumericValueRefDto> source)
        {
            if (target == null || source == null)
            {
                return;
            }

            foreach (var kv in source)
            {
                target[kv.Key] = CloneNumericValueRef(kv.Value);
            }
        }

        private static string FormatModuleScopedId(string id, TriggerPlanModuleInstanceDto instance)
        {
            if (string.IsNullOrEmpty(id))
            {
                return id;
            }

            var prefix = !string.IsNullOrEmpty(instance?.InstanceId) ? instance.InstanceId : instance?.Id;
            return string.IsNullOrEmpty(prefix) ? id : prefix + ":" + id;
        }

        private static TriggerPlanDatabaseDto CloneDto(TriggerPlanDatabaseDto dto)
        {
            return dto == null ? null : JsonConvert.DeserializeObject<TriggerPlanDatabaseDto>(JsonConvert.SerializeObject(dto));
        }

        private static TriggerPlanDto CloneTrigger(TriggerPlanDto dto)
        {
            return dto == null ? null : JsonConvert.DeserializeObject<TriggerPlanDto>(JsonConvert.SerializeObject(dto));
        }

        private static ExecutionNodeDto CloneExecutionNode(ExecutionNodeDto dto)
        {
            return dto == null ? null : JsonConvert.DeserializeObject<ExecutionNodeDto>(JsonConvert.SerializeObject(dto));
        }

        private static NumericValueRefDto CloneNumericValueRef(NumericValueRefDto dto)
        {
            return dto == null ? null : JsonConvert.DeserializeObject<NumericValueRefDto>(JsonConvert.SerializeObject(dto));
        }
    }
}
