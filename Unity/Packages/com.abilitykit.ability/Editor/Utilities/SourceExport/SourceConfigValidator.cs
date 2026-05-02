#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using AbilityKit.Ability.Config.Source;
using AbilityKit.Ability.Editor.Utilities;
using AbilityKit.Ability.Triggering.Runtime;

namespace AbilityKit.Ability.Editor.Utilities
{
    /// <summary>
    /// Source JSON 配置验证器
    /// </summary>
    internal static class SourceConfigValidator
    {
        /// <summary>
        /// 验证结果
        /// </summary>
        public class ValidationResult
        {
            public bool IsValid => Errors.Count == 0;
            public List<ValidationError> Errors = new List<ValidationError>();
            public List<ValidationWarning> Warnings = new List<ValidationWarning>();
            public List<ValidationInfo> Infos = new List<ValidationInfo>();

            public void AddError(string path, string message) => Errors.Add(new ValidationError { Path = path, Message = message });
            public void AddWarning(string path, string message) => Warnings.Add(new ValidationWarning { Path = path, Message = message });
            public void AddInfo(string path, string message) => Infos.Add(new ValidationInfo { Path = path, Message = message });
        }

        public class ValidationError { public string Path; public string Message; }
        public class ValidationWarning { public string Path; public string Message; }
        public class ValidationInfo { public string Path; public string Message; }

        /// <summary>
        /// 验证 Source 配置
        /// </summary>
        public static ValidationResult Validate(TriggerSourceConfig source)
        {
            var result = new ValidationResult();

            if (source == null)
            {
                result.AddError("root", "Source config is null");
                return result;
            }

            // 1. 验证 Schema 版本
            ValidateSchema(source, result);

            // 2. 验证触发器
            if (source.Triggers != null)
            {
                ValidateTriggers(source.Triggers, result);
            }

            // 3. 验证类型定义
            ValidateTypeDefinitions(source, result);

            return result;
        }

        /// <summary>
        /// 验证 Schema
        /// </summary>
        private static void ValidateSchema(TriggerSourceConfig source, ValidationResult result)
        {
            if (source.Schema != "abilitykit-trigger-source-v1")
            {
                result.AddWarning("$schema", $"Unknown or missing schema version. Expected: abilitykit-trigger-source-v1");
            }
        }

        /// <summary>
        /// 验证触发器列表
        /// </summary>
        private static void ValidateTriggers(List<SourceTriggerConfig> triggers, ValidationResult result)
        {
            var usedIds = new HashSet<int>();
            var usedNames = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < triggers.Count; i++)
            {
                var trigger = triggers[i];
                var path = $"triggers[{i}]";

                // 验证 ID
                if (trigger.Id <= 0)
                {
                    result.AddError($"{path}.id", $"Trigger ID must be positive integer, got: {trigger.Id}");
                }
                else
                {
                    if (usedIds.Contains(trigger.Id))
                    {
                        result.AddError($"{path}.id", $"Duplicate TriggerId: {trigger.Id}");
                    }
                    usedIds.Add(trigger.Id);
                }

                // 验证名称
                if (string.IsNullOrWhiteSpace(trigger.Name))
                {
                    result.AddWarning($"{path}.name", "Trigger name is empty");
                }
                else
                {
                    var nameKey = trigger.Name.ToLowerInvariant();
                    if (!usedNames.ContainsKey(nameKey))
                    {
                        usedNames[nameKey] = new List<int>();
                    }
                    usedNames[nameKey].Add(trigger.Id);
                }

                // 验证事件
                if (string.IsNullOrWhiteSpace(trigger.Event))
                {
                    result.AddInfo($"{path}.event", "Trigger has no event binding (will be triggered by ID only)");
                }
                else
                {
                    ValidateEventName($"{path}.event", trigger.Event, result);
                }

                // 验证条件
                if (trigger.Conditions != null && trigger.Conditions.Count > 0)
                {
                    ValidateConditions($"{path}.conditions", trigger.Conditions, result);
                }

                // 验证动作
                if (trigger.Actions == null || trigger.Actions.Count == 0)
                {
                    result.AddError($"{path}.actions", "Trigger has no actions");
                }
                else
                {
                    ValidateActions($"{path}.actions", trigger.Actions, result);
                }
            }

            // 检查重复名称
            foreach (var kvp in usedNames)
            {
                if (kvp.Value.Count > 1)
                {
                    result.AddWarning($"triggers", $"Duplicate trigger name: '{kvp.Key}' (IDs: {string.Join(", ", kvp.Value)})");
                }
            }
        }

        /// <summary>
        /// 验证事件名称
        /// </summary>
        private static void ValidateEventName(string path, string eventName, ValidationResult result)
        {
            // 检查常见命名模式
            var validPatterns = new[]
            {
                "attack.", "skill.", "buff.", "item.", "movement.", "health.", "death.", "spawn."
            };

            var hasValidPrefix = validPatterns.Any(p => eventName.StartsWith(p, StringComparison.OrdinalIgnoreCase));

            if (!hasValidPrefix && !eventName.Contains("."))
            {
                result.AddWarning(path, $"Event name '{eventName}' doesn't follow 'category.name' pattern");
            }
        }

        /// <summary>
        /// 验证条件列表
        /// </summary>
        private static void ValidateConditions(string path, List<SourceConditionConfig> conditions, ValidationResult result)
        {
            for (int i = 0; i < conditions.Count; i++)
            {
                ValidateCondition($"{path}[{i}]", conditions[i], result);
            }
        }

        /// <summary>
        /// 验证单个条件
        /// </summary>
        private static void ValidateCondition(string path, SourceConditionConfig condition, ValidationResult result)
        {
            if (condition == null)
            {
                result.AddError(path, "Condition is null");
                return;
            }

            if (string.IsNullOrWhiteSpace(condition.Type))
            {
                result.AddError($"{path}.type", "Condition type is empty");
                return;
            }

            // 验证条件类型是否存在
            ValidateConditionType($"{path}.type", condition.Type, result);

            // 复合条件验证
            if (condition.Type == "all" || condition.Type == "any")
            {
                if (condition.Items == null || condition.Items.Count == 0)
                {
                    result.AddError($"{path}.items", $"{condition.Type} condition must have at least one item");
                }
                else
                {
                    for (int i = 0; i < condition.Items.Count; i++)
                    {
                        ValidateCondition($"{path}.items[{i}]", condition.Items[i], result);
                    }
                }
            }
            else if (condition.Type == "not")
            {
                if (condition.Item == null)
                {
                    result.AddError($"{path}.item", "not condition must have an item");
                }
                else
                {
                    ValidateCondition($"{path}.item", condition.Item, result);
                }
            }
            else
            {
                // 原子条件验证参数
                ValidateConditionArgs(path, condition, result);
            }
        }

        /// <summary>
        /// 验证条件参数
        /// </summary>
        private static void ValidateConditionArgs(string path, SourceConditionConfig condition, ValidationResult result)
        {
            switch (condition.Type)
            {
                case "arg_eq":
                case "arg_gt":
                case "num_var_gt":
                    if (condition.Args == null || !condition.Args.ContainsKey("value"))
                    {
                        result.AddError($"{path}.args", $"{condition.Type} condition requires 'value' argument");
                    }
                    break;
            }
        }

        /// <summary>
        /// 验证条件类型
        /// </summary>
        private static void ValidateConditionType(string path, string type, ValidationResult result)
        {
            var knownTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "all", "any", "not",
                "arg_eq", "arg_gt", "arg_lt", "arg_leq", "arg_geq",
                "num_var_gt", "num_var_lt", "num_var_eq",
                "has_buff", "has_tag", "is_alive", "is_grounded"
            };

            if (!knownTypes.Contains(type))
            {
                // 检查是否在注册表中
                if (!TriggerConditionTypeRegistry.Instance.Contains(type))
                {
                    result.AddWarning(path, $"Unknown condition type: '{type}'");
                }
            }
        }

        /// <summary>
        /// 验证动作列表
        /// </summary>
        private static void ValidateActions(string path, List<SourceActionConfig> actions, ValidationResult result)
        {
            for (int i = 0; i < actions.Count; i++)
            {
                ValidateAction($"{path}[{i}]", actions[i], result);
            }
        }

        /// <summary>
        /// 验证单个动作
        /// </summary>
        private static void ValidateAction(string path, SourceActionConfig action, ValidationResult result)
        {
            if (action == null)
            {
                result.AddError(path, "Action is null");
                return;
            }

            if (string.IsNullOrWhiteSpace(action.Type))
            {
                result.AddError($"{path}.type", "Action type is empty");
                return;
            }

            // 验证动作类型是否存在
            ValidateActionType($"{path}.type", action.Type, result);

            // 复合动作验证
            if (action.Type == "seq")
            {
                if (action.Items == null || action.Items.Count == 0)
                {
                    result.AddError($"{path}.items", "seq action must have at least one item");
                }
                else
                {
                    for (int i = 0; i < action.Items.Count; i++)
                    {
                        ValidateAction($"{path}.items[{i}]", action.Items[i], result);
                    }
                }
            }
            else
            {
                // 原子动作验证参数
                ValidateActionArgs(path, action, result);
            }
        }

        /// <summary>
        /// 验证动作参数
        /// </summary>
        private static void ValidateActionArgs(string path, SourceActionConfig action, ValidationResult result)
        {
            switch (action.Type)
            {
                case "debug_log":
                    if (action.Args == null || (!action.Args.ContainsKey("message") && !action.Args.ContainsKey("msg_id")))
                    {
                        result.AddWarning($"{path}.args", "debug_log action should have 'message' argument");
                    }
                    break;

                case "shoot_projectile":
                    ValidateRequiredArg(path, action, "projectile_id", result);
                    break;

                case "give_damage":
                    ValidateRequiredArg(path, action, "amount", result);
                    break;

                case "add_buff":
                    ValidateRequiredArg(path, action, "buff_id", result);
                    break;
            }
        }

        /// <summary>
        /// 验证必需参数
        /// </summary>
        private static void ValidateRequiredArg(string path, SourceActionConfig action, string argName, ValidationResult result)
        {
            if (action.Args == null || !action.Args.ContainsKey(argName))
            {
                result.AddWarning($"{path}.args", $"'{action.Type}' action should have '{argName}' argument");
            }
        }

        /// <summary>
        /// 验证动作类型
        /// </summary>
        private static void ValidateActionType(string path, string type, ValidationResult result)
        {
            var knownTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "seq", "set_var", "set_num_var",
                "debug_log", "log_attacker",
                "effect_execute", "add_buff", "remove_buff",
                "shoot_projectile", "give_damage", "take_damage",
                "spawn_summon", "play_presentation",
                "attr_effect_duration"
            };

            if (!knownTypes.Contains(type))
            {
                // 检查是否在注册表中
                if (!TriggerActionTypeRegistry.Instance.Contains(type))
                {
                    result.AddWarning(path, $"Unknown action type: '{type}'");
                }
            }
        }

        /// <summary>
        /// 验证类型定义
        /// </summary>
        private static void ValidateTypeDefinitions(TriggerSourceConfig source, ValidationResult result)
        {
            // 验证动作类型定义
            if (source.Actions != null)
            {
                foreach (var kvp in source.Actions)
                {
                    var path = $"actions['{kvp.Key}']";
                    ValidateActionTypeDefinition(path, kvp.Value, result);
                }
            }

            // 验证条件类型定义
            if (source.Conditions != null)
            {
                foreach (var kvp in source.Conditions)
                {
                    var path = $"conditions['{kvp.Key}']";
                    ValidateConditionTypeDefinition(path, kvp.Value, result);
                }
            }
        }

        private static void ValidateActionTypeDefinition(string path, ActionTypeDefinition def, ValidationResult result)
        {
            if (def == null) return;

            if (string.IsNullOrWhiteSpace(def.DisplayName))
            {
                result.AddInfo($"{path}.displayName", "Action type definition should have a display name");
            }

            if (def.Params != null)
            {
                var paramNames = new HashSet<string>();
                foreach (var param in def.Params)
                {
                    if (string.IsNullOrWhiteSpace(param.Name))
                    {
                        result.AddError($"{path}.params", "Parameter definition must have a name");
                    }
                    else if (!paramNames.Add(param.Name))
                    {
                        result.AddWarning($"{path}.params", $"Duplicate parameter name: '{param.Name}'");
                    }
                }
            }
        }

        private static void ValidateConditionTypeDefinition(string path, ConditionTypeDefinition def, ValidationResult result)
        {
            if (def == null) return;

            if (string.IsNullOrWhiteSpace(def.DisplayName))
            {
                result.AddInfo($"{path}.displayName", "Condition type definition should have a display name");
            }

            if (def.Params != null)
            {
                var paramNames = new HashSet<string>();
                foreach (var param in def.Params)
                {
                    if (string.IsNullOrWhiteSpace(param.Name))
                    {
                        result.AddError($"{path}.params", "Parameter definition must have a name");
                    }
                    else if (!paramNames.Add(param.Name))
                    {
                        result.AddWarning($"{path}.params", $"Duplicate parameter name: '{param.Name}'");
                    }
                }
            }
        }

        /// <summary>
        /// 生成验证报告
        /// </summary>
        public static string GenerateReport(ValidationResult result)
        {
            var lines = new List<string>();

            lines.Add("=== Source JSON Validation Report ===");
            lines.Add($"Status: {(result.IsValid ? "VALID" : "INVALID")}");
            lines.Add("");

            if (result.Errors.Count > 0)
            {
                lines.Add($"Errors ({result.Errors.Count}):");
                foreach (var err in result.Errors)
                {
                    lines.Add($"  [{err.Path}] {err.Message}");
                }
                lines.Add("");
            }

            if (result.Warnings.Count > 0)
            {
                lines.Add($"Warnings ({result.Warnings.Count}):");
                foreach (var warn in result.Warnings)
                {
                    lines.Add($"  [{warn.Path}] {warn.Message}");
                }
                lines.Add("");
            }

            if (result.Infos.Count > 0)
            {
                lines.Add($"Info ({result.Infos.Count}):");
                foreach (var info in result.Infos)
                {
                    lines.Add($"  [{info.Path}] {info.Message}");
                }
                lines.Add("");
            }

            return string.Join("\n", lines);
        }
    }
}
#endif
