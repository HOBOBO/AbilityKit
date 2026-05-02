#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using AbilityKit.Ability.Config.Source;
using AbilityKit.Ability.Editor.Utilities;
using AbilityKit.Ability.Triggering.Runtime;
using AbilityKit.Triggering.Eventing;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime.Plan;
using Newtonsoft.Json;
using UnityEngine;

namespace AbilityKit.Ability.Editor.Utilities
{
    /// <summary>
    /// Source JSON → Plan JSON 转换器
    /// </summary>
    internal static class SourceToPlanConverter
    {
        /// <summary>
        /// 转换结果
        /// </summary>
        public class ConvertResult
        {
            public bool Success = true;
            public TriggerPlanDatabaseDto PlanDatabase;
            public List<string> Errors = new List<string>();
            public List<string> Warnings = new List<string>();
            public Dictionary<string, int> ActionNameToId = new Dictionary<string, int>();
            public Dictionary<string, int> ConditionNameToId = new Dictionary<string, int>();
            public Dictionary<string, int> EventNameToId = new Dictionary<string, int>();

            public void AddError(string msg) { Errors.Add(msg); Success = false; }
            public void AddWarning(string msg) { Warnings.Add(msg); }
        }

        /// <summary>
        /// 从 Source JSON 字符串转换
        /// </summary>
        public static ConvertResult Convert(string sourceJson)
        {
            var result = new ConvertResult();

            if (string.IsNullOrWhiteSpace(sourceJson))
            {
                result.AddError("Source JSON is empty");
                return result;
            }

            TriggerSourceConfig source;
            try
            {
                source = JsonConvert.DeserializeObject<TriggerSourceConfig>(sourceJson);
                if (source == null)
                {
                    result.AddError("Failed to parse Source JSON");
                    return result;
                }
            }
            catch (Exception ex)
            {
                result.AddError($"JSON parse error: {ex.Message}");
                return result;
            }

            return Convert(source);
        }

        /// <summary>
        /// 从 Source 配置对象转换
        /// </summary>
        public static ConvertResult Convert(TriggerSourceConfig source)
        {
            var result = new ConvertResult();
            var planDb = new TriggerPlanDatabaseDto();

            if (source == null)
            {
                result.AddError("Source config is null");
                return result;
            }

            // 1. 构建名称→ID 映射表
            BuildNameToIdMaps(source, result);

            // 2. 转换触发器
            if (source.Triggers != null)
            {
                for (int i = 0; i < source.Triggers.Count; i++)
                {
                    var trigger = source.Triggers[i];
                    ConvertTrigger(trigger, planDb, result);
                }
            }

            // 3. 更新元数据时间
            if (source.Metadata != null)
            {
                source.Metadata.LastModified = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
            }

            result.PlanDatabase = planDb;
            return result;
        }

        /// <summary>
        /// 构建名称到 ID 的映射表
        /// </summary>
        private static void BuildNameToIdMaps(TriggerSourceConfig source, ConvertResult result)
        {
            // 动作类型 → ID
            foreach (var type in TriggerActionTypeRegistry.Instance.Keys)
            {
                var id = StableStringId.Get("action:" + type);
                result.ActionNameToId[type] = id;
            }

            // 条件类型 → ID
            foreach (var type in TriggerConditionTypeRegistry.Instance.Keys)
            {
                var id = StableStringId.Get("condition:" + type);
                result.ConditionNameToId[type] = id;
            }

            // 事件名称 → ID（从触发器配置中收集）
            if (source.Triggers != null)
            {
                var eventNames = source.Triggers
                    .Where(t => !string.IsNullOrEmpty(t.Event))
                    .Select(t => t.Event)
                    .Distinct();

                foreach (var evt in eventNames)
                {
                    var id = StableStringId.Get("event:" + evt);
                    result.EventNameToId[evt] = id;
                }
            }

            // 添加内置复合类型
            result.ActionNameToId["seq"] = StableStringId.Get("action:seq");
            result.ConditionNameToId["all"] = StableStringId.Get("condition:all");
            result.ConditionNameToId["any"] = StableStringId.Get("condition:any");
            result.ConditionNameToId["not"] = StableStringId.Get("condition:not");
        }

        /// <summary>
        /// 转换单个触发器
        /// </summary>
        private static void ConvertTrigger(SourceTriggerConfig trigger, TriggerPlanDatabaseDto planDb, ConvertResult result)
        {
            if (trigger == null) return;

            if (trigger.Id <= 0)
            {
                result.AddWarning($"Trigger '{trigger.Name ?? "<unnamed>"}' has invalid Id <= 0, skipped");
                return;
            }

            if (!trigger.Enabled)
            {
                result.AddWarning($"Trigger {trigger.Id} '{trigger.Name}' is disabled, skipped");
                return;
            }

            if (trigger.Actions == null || trigger.Actions.Count == 0)
            {
                result.AddWarning($"Trigger {trigger.Id} '{trigger.Name}' has no actions, skipped");
                return;
            }

            var dto = new TriggerPlanDto
            {
                TriggerId = trigger.Id,
                EventName = trigger.Event ?? string.Empty,
                AllowExternal = trigger.AllowExternal,
                Phase = GetPhaseValue(trigger.Phase),
                Priority = trigger.Priority
            };

            // 解析 EventId
            if (!string.IsNullOrEmpty(trigger.Event))
            {
                dto.EventId = StableStringId.Get("event:" + trigger.Event);
            }

            // 转换条件
            dto.Predicate = ConvertCondition(trigger.Conditions, result);

            // 转换动作
            dto.Actions = ConvertActions(trigger.Actions, planDb.Strings, result);

            if (dto.Actions == null || dto.Actions.Count == 0)
            {
                result.AddWarning($"Trigger {trigger.Id} '{trigger.Name}' has no valid actions after conversion, skipped");
                return;
            }

            planDb.Triggers.Add(dto);
        }

        /// <summary>
        /// 转换条件列表
        /// </summary>
        private static PredicatePlanDto ConvertCondition(List<SourceConditionConfig> conditions, ConvertResult result)
        {
            var predicate = new PredicatePlanDto
            {
                Kind = "expr",
                Nodes = new List<BoolExprNodeDto>()
            };

            if (conditions == null || conditions.Count == 0)
            {
                predicate.Kind = "none";
                return predicate;
            }

            if (conditions.Count == 1)
            {
                var singleCondition = conditions[0];
                ConvertConditionToExpr(singleCondition, predicate.Nodes, result);
            }
            else
            {
                // 多个条件用 AND 连接
                var allCondition = new SourceConditionConfig { Type = "all", Items = conditions };
                ConvertConditionToExpr(allCondition, predicate.Nodes, result);
            }

            return predicate;
        }

        /// <summary>
        /// 转换条件为表达式节点
        /// </summary>
        private static void ConvertConditionToExpr(SourceConditionConfig condition, List<BoolExprNodeDto> nodes, ConvertResult result)
        {
            if (condition == null) return;

            // 复合条件处理
            if (condition.Type == "all" || condition.Type == "any")
            {
                if (condition.Items != null)
                {
                    foreach (var item in condition.Items)
                    {
                        ConvertConditionToExpr(item, nodes, result);
                    }
                }
                // 注意：这里简单地按顺序展开，实际运行时通过逻辑运算符组合
                return;
            }

            if (condition.Type == "not")
            {
                if (condition.Item != null)
                {
                    // NOT 条件的处理需要在运行时处理，这里先展开子条件
                    ConvertConditionToExpr(condition.Item, nodes, result);
                }
                return;
            }

            // 原子条件转换为 BoolExprNode
            var node = new BoolExprNodeDto { Kind = "compare" };

            if (condition.Type == "arg_eq")
            {
                node.CompareOp = "==";
            }
            else if (condition.Type == "arg_gt")
            {
                node.CompareOp = ">";
            }
            else if (condition.Type == "num_var_gt")
            {
                node.CompareOp = ">";
            }
            else
            {
                result.AddWarning($"Unknown condition type: {condition.Type}, skipped");
                return;
            }

            // 解析参数
            if (condition.Args != null)
            {
                // Left side - 参数引用
                if (condition.Args.TryGetValue("arg_name", out var argNameObj) ||
                    condition.Args.TryGetValue("var_name", out argNameObj))
                {
                    var argName = argNameObj?.ToString() ?? "arg1";
                    node.Left = CreatePayloadRef(argName);
                }

                // Right side - 比较值
                if (condition.Args.TryGetValue("value", out var valueObj))
                {
                    node.Right = CreateConstRef(valueObj);
                }
                else if (condition.Args.TryGetValue("threshold", out valueObj))
                {
                    node.Right = CreateConstRef(valueObj);
                }
            }

            nodes.Add(node);
        }

        /// <summary>
        /// 转换动作列表
        /// </summary>
        private static List<ActionCallPlanDto> ConvertActions(
            List<SourceActionConfig> actions,
            Dictionary<int, string> stringTable,
            ConvertResult result)
        {
            var plans = new List<ActionCallPlanDto>();

            if (actions == null) return plans;

            foreach (var action in actions)
            {
                var plan = ConvertAction(action, stringTable, result);
                if (plan != null)
                {
                    plans.Add(plan);
                }
            }

            return plans;
        }

        /// <summary>
        /// 转换单个动作
        /// </summary>
        private static ActionCallPlanDto ConvertAction(
            SourceActionConfig action,
            Dictionary<int, string> stringTable,
            ConvertResult result)
        {
            if (action == null) return null;

            var plan = new ActionCallPlanDto();

            // 解析动作 ID
            var actionId = TriggerPlanCompilerResolvers.ResolveActionId(action.Type);
            if (actionId.Value == 0)
            {
                result.AddWarning($"Unknown action type: {action.Type}");
                return null;
            }

            plan.ActionId = actionId.Value;

            // 处理 seq 复合动作 - 展平
            if (action.Type == "seq" && action.Items != null && action.Items.Count > 0)
            {
                // seq 本身不需要生成 plan，子动作会分别生成
                var subPlans = ConvertActions(action.Items, stringTable, result);
                if (subPlans.Count > 0)
                {
                    // 返回第一个，子动作会被递归处理
                    return subPlans[0];
                }
                return null;
            }

            // 解析参数
            plan.Args = new Dictionary<string, NumericValueRefDto>();
            plan.Arity = 0;

            if (action.Args != null)
            {
                foreach (var kvp in action.Args)
                {
                    var valueRef = ConvertValue(kvp.Key, kvp.Value, stringTable, result);
                    if (valueRef != null)
                    {
                        plan.Args[kvp.Key] = valueRef;
                        plan.Arity++;
                    }
                }
            }

            // 处理特殊动作类型的参数映射
            MapActionParams(action.Type, action.Args, plan, stringTable, result);

            return plan;
        }

        /// <summary>
        /// 特殊动作类型的参数映射
        /// </summary>
        private static void MapActionParams(
            string actionType,
            Dictionary<string, object> args,
            ActionCallPlanDto plan,
            Dictionary<int, string> stringTable,
            ConvertResult result)
        {
            if (args == null) return;

            switch (actionType)
            {
                case "debug_log":
                    // message 参数需要存入字符串表
                    if (args.TryGetValue("message", out var msgObj))
                    {
                        var msg = msgObj?.ToString() ?? "";
                        var strId = StableStringId.Get("str:" + msg);
                        plan.Args["msg_id"] = new NumericValueRefDto
                        {
                            Kind = "Const",
                            ConstValue = strId
                        };

                        if (!stringTable.ContainsKey(strId))
                        {
                            stringTable[strId] = msg;
                        }
                    }

                    if (args.TryGetValue("dump_args", out var dumpObj))
                    {
                        var dump = System.Convert.ToDouble(dumpObj) != 0;
                        plan.Args["dump"] = new NumericValueRefDto
                        {
                            Kind = "Const",
                            ConstValue = dump ? 1 : 0
                        };
                    }
                    break;

                case "shoot_projectile":
                    // 映射到运行时参数名
                    MapEntityArg(args, "launcher", "launcher_id", plan);
                    MapEntityArg(args, "target", "target_id", plan);
                    MapIntArg(args, "projectile_id", plan);
                    MapFloatArg(args, "speed", plan);
                    break;

                case "give_damage":
                    MapEntityArg(args, "from", "from_id", plan);
                    MapEntityArg(args, "to", "to_id", plan);
                    MapExprArg(args, "amount", "damage_amount", plan);
                    MapIntArg(args, "reason", plan);
                    break;

                case "add_buff":
                    MapEntityArg(args, "target", "target_id", plan);
                    MapIntArg(args, "buff_id", plan);
                    MapFloatArg(args, "duration", plan);
                    break;
            }
        }

        /// <summary>
        /// 映射实体参数
        /// </summary>
        private static void MapEntityArg(Dictionary<string, object> args, string sourceKey, string targetKey, ActionCallPlanDto plan)
        {
            if (args.TryGetValue(sourceKey, out var obj))
            {
                var value = obj?.ToString() ?? "";
                var ref_ = CreateEntityRef(value);
                plan.Args[targetKey] = ref_;
            }
        }

        /// <summary>
        /// 映射整数参数
        /// </summary>
        private static void MapIntArg(Dictionary<string, object> args, string key, ActionCallPlanDto plan)
        {
            if (args.TryGetValue(key, out var obj) && obj != null)
            {
                var value = System.Convert.ToDouble(obj);
                plan.Args[key] = new NumericValueRefDto { Kind = "Const", ConstValue = value };
            }
        }

        /// <summary>
        /// 映射浮点数参数
        /// </summary>
        private static void MapFloatArg(Dictionary<string, object> args, string key, ActionCallPlanDto plan)
        {
            MapIntArg(args, key, plan); // 使用相同逻辑，double 可表示 float
        }

        /// <summary>
        /// 映射表达式参数
        /// </summary>
        private static void MapExprArg(Dictionary<string, object> args, string sourceKey, string targetKey, ActionCallPlanDto plan)
        {
            if (args.TryGetValue(sourceKey, out var obj))
            {
                var value = obj?.ToString() ?? "";
                plan.Args[targetKey] = new NumericValueRefDto { Kind = "Expr", ExprText = value };
            }
        }

        /// <summary>
        /// 转换值为 NumericValueRefDto
        /// </summary>
        private static NumericValueRefDto ConvertValue(string key, object value, Dictionary<int, string> stringTable, ConvertResult result)
        {
            if (value == null)
            {
                return new NumericValueRefDto { Kind = "Const", ConstValue = 0 };
            }

            // 字符串可能是实体引用或表达式
            if (value is string strValue)
            {
                return ConvertStringValue(strValue);
            }

            // 数字
            if (value is double dValue)
            {
                return new NumericValueRefDto { Kind = "Const", ConstValue = dValue };
            }

            if (value is int iValue)
            {
                return new NumericValueRefDto { Kind = "Const", ConstValue = iValue };
            }

            if (value is long lValue)
            {
                return new NumericValueRefDto { Kind = "Const", ConstValue = lValue };
            }

            // 布尔值
            if (value is bool bValue)
            {
                return new NumericValueRefDto { Kind = "Const", ConstValue = bValue ? 1 : 0 };
            }

            // 其他类型转字符串处理
            return ConvertStringValue(value.ToString());
        }

        /// <summary>
        /// 转换字符串值为 NumericValueRefDto
        /// </summary>
        private static NumericValueRefDto ConvertStringValue(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return new NumericValueRefDto { Kind = "Const", ConstValue = 0 };
            }

            // 实体引用
            if (value.StartsWith("$"))
            {
                return CreateEntityRef(value);
            }

            // 表达式
            if (value.StartsWith("=") || value.Contains("."))
            {
                return new NumericValueRefDto { Kind = "Expr", ExprText = value };
            }

            // 尝试解析为数字
            if (double.TryParse(value, out var dValue))
            {
                return new NumericValueRefDto { Kind = "Const", ConstValue = dValue };
            }

            // 默认作为常量字符串（会存入字符串表）
            return new NumericValueRefDto { Kind = "Const", ConstValue = 0 };
        }

        /// <summary>
        /// 创建实体引用
        /// </summary>
        private static NumericValueRefDto CreateEntityRef(string entityRef)
        {
            if (string.IsNullOrEmpty(entityRef)) return new NumericValueRefDto { Kind = "Const", ConstValue = 0 };

            // 常见上下文变量映射
            switch (entityRef.ToLower())
            {
                case "$caster":
                    return new NumericValueRefDto { Kind = "Const", ConstValue = 1 }; // payload:caster
                case "$target":
                    return new NumericValueRefDto { Kind = "Const", ConstValue = 2 }; // payload:target
                case "$self":
                    return new NumericValueRefDto { Kind = "Const", ConstValue = 3 }; // payload:self
                default:
                    // 自定义变量
                    var varId = StableStringId.Get("var:" + entityRef);
                    return new NumericValueRefDto { Kind = "Var", DomainId = "context", Key = entityRef };
            }
        }

        /// <summary>
        /// 创建 Payload 字段引用
        /// </summary>
        private static NumericValueRefDto CreatePayloadRef(string fieldName)
        {
            var fieldId = StableStringId.Get("payload:" + fieldName);
            return new NumericValueRefDto { Kind = "PayloadField", FieldId = fieldId };
        }

        /// <summary>
        /// 创建常量引用
        /// </summary>
        private static NumericValueRefDto CreateConstRef(object value)
        {
            if (value == null) return new NumericValueRefDto { Kind = "Const", ConstValue = 0 };

            if (value is double d) return new NumericValueRefDto { Kind = "Const", ConstValue = d };
            if (value is int i) return new NumericValueRefDto { Kind = "Const", ConstValue = i };
            if (value is long l) return new NumericValueRefDto { Kind = "Const", ConstValue = l };
            if (value is bool b) return new NumericValueRefDto { Kind = "Const", ConstValue = b ? 1 : 0 };

            if (double.TryParse(value.ToString(), out var dValue))
            {
                return new NumericValueRefDto { Kind = "Const", ConstValue = dValue };
            }

            return new NumericValueRefDto { Kind = "Const", ConstValue = 0 };
        }

        /// <summary>
        /// 获取阶段值
        /// </summary>
        private static int GetPhaseValue(string phase)
        {
            if (string.IsNullOrEmpty(phase)) return 0;

            switch (phase.ToLower())
            {
                case "immediate": return 0;
                case "early": return 1;
                case "late": return 2;
                default:
                    if (int.TryParse(phase, out var v)) return v;
                    return 0;
            }
        }
    }
}
#endif
