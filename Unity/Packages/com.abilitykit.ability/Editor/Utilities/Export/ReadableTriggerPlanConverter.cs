#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using AbilityKit.Ability.Editor.Utilities;
using AbilityKit.Triggering.Runtime.Plan;
using AbilityKit.Triggering.Registry;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace AbilityKit.Ability.Editor.Utilities
{
    /// <summary>
    /// 用于生成稳定字符串哈希ID的工具类
    /// </summary>
    internal static class StableStringId
    {
        private static readonly Dictionary<int, string> _reverse = new Dictionary<int, string>();

        public static int Get(string value)
        {
            if (string.IsNullOrEmpty(value)) throw new ArgumentException("Id string is null or empty", nameof(value));

            var id = Fnv1a32(value);
            if (_reverse.TryGetValue(id, out var existing))
            {
                if (!string.Equals(existing, value, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException($"StableStringId collision: '{existing}' and '{value}' => {id}");
                }

                return id;
            }

            _reverse[id] = value;
            return id;
        }

        private static int Fnv1a32(string s)
        {
            unchecked
            {
                const uint offset = 2166136261;
                const uint prime = 16777619;

                uint hash = offset;
                for (int i = 0; i < s.Length; i++)
                {
                    hash ^= s[i];
                    hash *= prime;
                }

                return (int)(hash & 0x7FFFFFFF);
            }
        }
    }

    /// <summary>
    /// 可读格式与内部格式之间的转换器
    /// </summary>
    internal static class ReadableTriggerPlanConverter
    {
        private static readonly JsonSerializerSettings JsonSettings = new()
        {
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Ignore,
            Converters = new List<JsonConverter> { new ReadableValueRefConverter() }
        };

        /// <summary>
        /// 将内部格式转换为可读格式
        /// </summary>
        public static string ToReadable(TriggerPlanDatabaseDto internalDto)
        {
            // 转换 strings 字典：从 int->string 转换为 string->string
            var readableStrings = new Dictionary<string, string>();
            foreach (var kvp in internalDto.Strings)
            {
                readableStrings[kvp.Key.ToString()] = kvp.Value;
            }

            var readable = new ReadableTriggerPlanDatabase
            {
                ActionDefs = CollectActionDefs(internalDto),
                Strings = readableStrings,
                Triggers = internalDto.Triggers.Select(ToReadableTrigger).ToList()
            };

            return JsonConvert.SerializeObject(readable, JsonSettings);
        }

        /// <summary>
        /// 将可读格式转换为内部格式
        /// </summary>
        public static TriggerPlanDatabaseDto FromReadable(string readableJson)
        {
            var readable = JsonConvert.DeserializeObject<ReadableTriggerPlanDatabase>(readableJson, JsonSettings);
            if (readable == null)
                throw new InvalidOperationException("Failed to parse readable JSON");

            // 构建动作名称到ID的映射（基于 ActionDefs）
            var actionNameToId = BuildActionNameToIdMap(readable.ActionDefs);

            // 转换 strings 字典：从 string->string 转换为 int->string
            var stringsDict = new Dictionary<int, string>();
            foreach (var kvp in readable.Strings)
            {
                if (int.TryParse(kvp.Key, out var keyId))
                {
                    stringsDict[keyId] = kvp.Value;
                }
            }

            // 创建 DTO 并手动填充字段
            var dto = new TriggerPlanDatabaseDto();
            dto.Strings.Clear();
            foreach (var kvp in stringsDict)
            {
                dto.Strings.Add(kvp.Key, kvp.Value);
            }
            dto.Triggers.Clear();
            dto.Triggers.AddRange(readable.Triggers.Select(t => FromReadableTrigger(t, stringsDict, actionNameToId)));

            return dto;
        }

        /// <summary>
        /// 收集所有动作定义
        /// </summary>
        internal static Dictionary<string, ReadableActionDef> CollectActionDefs(TriggerPlanDatabaseDto dto)
        {
            var defs = new Dictionary<string, ReadableActionDef>();

            // 收集所有使用的 ActionId（包括嵌套的）
            var usedActionIds = new HashSet<int>();
            foreach (var trigger in dto.Triggers)
            {
                if (trigger.Actions == null) continue;
                CollectUsedActionIds(trigger.Actions, usedActionIds);
            }

            // 构建动作定义
            foreach (var trigger in dto.Triggers)
            {
                if (trigger.Actions == null) continue;
                CollectActionDefsFromActions(trigger.Actions, defs);
            }

            return defs;
        }

        /// <summary>
        /// 递归收集所有使用的 ActionId
        /// </summary>
        private static void CollectUsedActionIds(List<ActionCallPlanDto> actions, HashSet<int> usedActionIds)
        {
            foreach (var action in actions)
            {
                usedActionIds.Add(action.ActionId);
                if (action.Children != null && action.Children.Count > 0)
                {
                    CollectUsedActionIds(action.Children, usedActionIds);
                }
            }
        }

        /// <summary>
        /// 递归收集动作定义
        /// </summary>
        private static void CollectActionDefsFromActions(
            List<ActionCallPlanDto> actions,
            Dictionary<string, ReadableActionDef> defs)
        {
            foreach (var action in actions)
            {
                var actionId = action.ActionId;
                var actionName = GetActionName(actionId);

                // 跳过没有具名参数的动作
                if (action.Args == null || action.Args.Count == 0)
                {
                    // 仍然处理子动作
                    if (action.Children != null && action.Children.Count > 0)
                    {
                        CollectActionDefsFromActions(action.Children, defs);
                    }
                    continue;
                }

                // 使用动作名称作为键（如果已存在则跳过）
                if (!defs.ContainsKey(actionName))
                {
                    var def = new ReadableActionDef
                    {
                        Args = action.Args.Keys.ToList(),
                        Description = actionName
                    };
                    defs[actionName] = def;
                }

                // 递归处理子动作
                if (action.Children != null && action.Children.Count > 0)
                {
                    CollectActionDefsFromActions(action.Children, defs);
                }
            }
        }

        /// <summary>
        /// 构建动作名称到ID的映射
        /// </summary>
        private static Dictionary<string, int> BuildActionNameToIdMap(Dictionary<string, ReadableActionDef> actionDefs)
        {
            var map = new Dictionary<string, int>();
            foreach (var kvp in actionDefs)
            {
                var name = kvp.Key;
                var id = StableStringId.Get("action:" + name);
                map[name] = id;
            }
            return map;
        }

        /// <summary>
        /// 将内部触发器转换为可读格式
        /// </summary>
        private static ReadableTriggerPlan ToReadableTrigger(TriggerPlanDto trigger)
        {
            var readable = new ReadableTriggerPlan
            {
                TriggerId = trigger.TriggerId,
                Event = trigger.EventName ?? "",
                AllowExternal = trigger.AllowExternal,
                Phase = trigger.Phase,
                Priority = trigger.Priority,
                Predicate = ToReadablePredicate(trigger.Predicate),
                Actions = trigger.Actions?.Select(ToReadableAction).ToList() ?? new List<ReadableActionCall>()
            };

            return readable;
        }

        /// <summary>
        /// 将内部条件转换为可读格式
        /// </summary>
        private static ReadablePredicate ToReadablePredicate(PredicatePlanDto predicate)
        {
            if (predicate == null || predicate.Kind == "none")
                return ReadablePredicate.None();

            if (predicate.Kind == "expr" && predicate.Nodes != null)
            {
                var nodes = predicate.Nodes.Select(ToReadableBoolExprNode).ToList();
                return ReadablePredicate.Expr(nodes);
            }

            return new ReadablePredicate { Kind = predicate.Kind ?? "none" };
        }

        /// <summary>
        /// 将内部布尔表达式节点转换为可读格式
        /// </summary>
        private static ReadableBoolExprNode ToReadableBoolExprNode(BoolExprNodeDto node)
        {
            return new ReadableBoolExprNode
            {
                Kind = node.Kind ?? "Compare",
                ConstValue = node.ConstValue,
                CompareOp = node.CompareOp,
                Left = ToReadableValueRef(node.Left),
                Right = ToReadableValueRef(node.Right)
            };
        }

        /// <summary>
        /// 将内部值引用转换为可读格式
        /// </summary>
        private static ReadableValueRef ToReadableValueRef(NumericValueRefDto dto)
        {
            if (dto == null) return ReadableValueRef.Const(0);

            return new ReadableValueRef
            {
                Kind = dto.Kind ?? "Const",
                ConstValue = dto.ConstValue,
                BoardId = dto.BoardId,
                KeyId = dto.KeyId,
                FieldId = dto.FieldId,
                DomainId = dto.DomainId,
                Key = dto.Key,
                ExprText = dto.ExprText
            };
        }

        /// <summary>
        /// 将内部动作转换为可读格式
        /// </summary>
        private static ReadableActionCall ToReadableAction(ActionCallPlanDto action)
        {
            var actionName = GetActionName(action.ActionId);

            // 收集参数
            var args = new Dictionary<string, object>();

            if (action.Args != null && action.Args.Count > 0)
            {
                foreach (var kvp in action.Args)
                {
                    args[kvp.Key] = ValueRefToObject(kvp.Value);
                }
            }

            var readable = new ReadableActionCall
            {
                Action = actionName,
                Args = args,
                Children = action.Children?.Select(ToReadableAction).ToList()
            };

            return readable;
        }

        private static string GetActionName(int actionId)
        {
            // 由于没有运行时注册表，使用格式化的名称
            return $"action_{actionId}";
        }

        private static object ValueRefToObject(NumericValueRefDto dto)
        {
            if (dto == null) return 0;

            switch (dto.Kind)
            {
                case "Const":
                    // 如果是整数，就返回整数
                    if (dto.ConstValue == Math.Floor(dto.ConstValue))
                        return (int)dto.ConstValue;
                    return dto.ConstValue;

                case "Board":
                    return new { Kind = "Board", BoardId = dto.BoardId, KeyId = dto.KeyId };
                case "Field":
                    return new { Kind = "Field", FieldId = dto.FieldId, KeyId = dto.KeyId };
                case "Domain":
                    return new { Kind = "Domain", DomainId = dto.DomainId, Key = dto.Key };
                case "Expr":
                    return new { Kind = "Expr", ExprText = dto.ExprText };
                default:
                    return dto.ConstValue;
            }
        }

        /// <summary>
        /// 将可读触发器转换为内部格式
        /// </summary>
        private static TriggerPlanDto FromReadableTrigger(
            ReadableTriggerPlan readable,
            Dictionary<int, string> strings,
            Dictionary<string, int> actionNameToId)
        {
            // 获取事件ID
            var eventId = !string.IsNullOrEmpty(readable.Event)
                ? StableStringId.Get("event:" + readable.Event)
                : 0;

            return new TriggerPlanDto
            {
                TriggerId = readable.TriggerId,
                EventName = readable.Event,
                EventId = eventId,
                AllowExternal = readable.AllowExternal,
                Phase = readable.Phase,
                Priority = readable.Priority,
                Predicate = FromReadablePredicate(readable.Predicate, strings),
                Actions = readable.Actions?.Select(a => FromReadableAction(a, strings, actionNameToId)).ToList(),
                LegacyPredicate = null,
                LegacyActions = null
            };
        }

        /// <summary>
        /// 将可读条件转换为内部格式
        /// </summary>
        private static PredicatePlanDto FromReadablePredicate(ReadablePredicate predicate, Dictionary<int, string> strings)
        {
            if (predicate == null || predicate.Kind == "none")
                return new PredicatePlanDto { Kind = "none" };

            if (predicate.Kind == "expr" && predicate.Nodes != null)
            {
                return new PredicatePlanDto
                {
                    Kind = "expr",
                    Nodes = predicate.Nodes.Select(n => FromReadableBoolExprNode(n)).ToList()
                };
            }

            return new PredicatePlanDto { Kind = predicate.Kind ?? "function" };
        }

        /// <summary>
        /// 将可读布尔表达式节点转换为内部格式
        /// </summary>
        private static BoolExprNodeDto FromReadableBoolExprNode(ReadableBoolExprNode node)
        {
            return new BoolExprNodeDto
            {
                Kind = node.Kind ?? "Compare",
                ConstValue = node.ConstValue,
                CompareOp = node.CompareOp,
                Left = FromReadableValueRef(node.Left),
                Right = FromReadableValueRef(node.Right)
            };
        }

        /// <summary>
        /// 将可读值引用转换为内部格式
        /// </summary>
        private static NumericValueRefDto FromReadableValueRef(ReadableValueRef readable)
        {
            if (readable == null)
                return new NumericValueRefDto { Kind = "Const", ConstValue = 0 };

            return new NumericValueRefDto
            {
                Kind = readable.Kind ?? "Const",
                ConstValue = readable.ConstValue,
                BoardId = readable.BoardId,
                KeyId = readable.KeyId,
                FieldId = readable.FieldId,
                DomainId = readable.DomainId,
                Key = readable.Key,
                ExprText = readable.ExprText
            };
        }

        /// <summary>
        /// 将可读动作转换为内部格式
        /// </summary>
        private static ActionCallPlanDto FromReadableAction(
            ReadableActionCall readable,
            Dictionary<int, string> strings,
            Dictionary<string, int> actionNameToId)
        {
            int actionId;

            // 如果是数字格式的动作ID
            if (int.TryParse(readable.Action, out var id))
            {
                actionId = id;
            }
            // 否则查找名称对应的ID
            else if (actionNameToId.TryGetValue(readable.Action, out var mappedId))
            {
                actionId = mappedId;
            }
            else
            {
                // 尝试解析为 "action_name" 格式
                var name = readable.Action.StartsWith("action_") ? readable.Action.Substring(7) : readable.Action;
                actionId = StableStringId.Get("action:" + name);
            }

            var args = new Dictionary<string, NumericValueRefDto>();

            foreach (var kvp in readable.Args)
            {
                var paramName = kvp.Key;
                var paramValue = kvp.Value;

                NumericValueRefDto valueRef;

                if (paramValue is JObject jo)
                {
                    valueRef = new NumericValueRefDto
                    {
                        Kind = jo["Kind"]?.ToString() ?? "Const",
                        BoardId = jo["BoardId"]?.Value<int>() ?? 0,
                        KeyId = jo["KeyId"]?.Value<int>() ?? 0,
                        FieldId = jo["FieldId"]?.Value<int>() ?? 0,
                        DomainId = jo["DomainId"]?.ToString(),
                        Key = jo["Key"]?.ToString(),
                        ExprText = jo["ExprText"]?.ToString()
                    };
                }
                else
                {
                    var doubleValue = Convert.ToDouble(paramValue);
                    valueRef = new NumericValueRefDto { Kind = "Const", ConstValue = doubleValue };
                }

                args[paramName] = valueRef;
            }

            // 递归转换子动作
            List<ActionCallPlanDto> children = null;
            if (readable.Children != null && readable.Children.Count > 0)
            {
                children = readable.Children
                    .Select(c => FromReadableAction(c, strings, actionNameToId))
                    .ToList();
            }

            return new ActionCallPlanDto
            {
                ActionId = actionId,
                Arity = (byte)args.Count,
                Args = args,
                Children = children
            };
        }
    }

    /// <summary>
    /// ReadableValueRef 的 JSON 转换器，处理复杂类型
    /// </summary>
    internal class ReadableValueRefConverter : JsonConverter<ReadableValueRef>
    {
        public override ReadableValueRef ReadJson(JsonReader reader, Type objectType, ReadableValueRef existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
                return ReadableValueRef.Const(0);

            if (reader.TokenType == JsonToken.Integer || reader.TokenType == JsonToken.Float)
            {
                var value = Convert.ToDouble(reader.Value);
                return ReadableValueRef.Const(value);
            }

            if (reader.TokenType == JsonToken.String)
            {
                var str = reader.Value?.ToString();
                if (double.TryParse(str, out var value))
                    return ReadableValueRef.Const(value);
                return ReadableValueRef.Expr(str);
            }

            if (reader.TokenType == JsonToken.StartObject)
            {
                var obj = JObject.Load(reader);
                return new ReadableValueRef
                {
                    Kind = obj["Kind"]?.ToString() ?? "Const",
                    ConstValue = obj["ConstValue"]?.Value<double>() ?? 0,
                    BoardId = obj["BoardId"]?.Value<int>() ?? 0,
                    KeyId = obj["KeyId"]?.Value<int>() ?? 0,
                    FieldId = obj["FieldId"]?.Value<int>() ?? 0,
                    DomainId = obj["DomainId"]?.ToString(),
                    Key = obj["Key"]?.ToString(),
                    ExprText = obj["ExprText"]?.ToString()
                };
            }

            return ReadableValueRef.Const(0);
        }

        public override void WriteJson(JsonWriter writer, ReadableValueRef value, JsonSerializer serializer)
        {
            if (value == null || value.IsDefault())
            {
                writer.WriteValue(0);
                return;
            }

            switch (value.Kind)
            {
                case "Const":
                    // 整数显示为整数
                    if (value.ConstValue == Math.Floor(value.ConstValue))
                        writer.WriteValue((int)value.ConstValue);
                    else
                        writer.WriteValue(value.ConstValue);
                    break;

                case "Board":
                    writer.WriteStartObject();
                    writer.WritePropertyName("Kind");
                    writer.WriteValue("Board");
                    writer.WritePropertyName("BoardId");
                    writer.WriteValue(value.BoardId);
                    writer.WritePropertyName("KeyId");
                    writer.WriteValue(value.KeyId);
                    writer.WriteEndObject();
                    break;

                case "Field":
                    writer.WriteStartObject();
                    writer.WritePropertyName("Kind");
                    writer.WriteValue("Field");
                    writer.WritePropertyName("FieldId");
                    writer.WriteValue(value.FieldId);
                    writer.WritePropertyName("KeyId");
                    writer.WriteValue(value.KeyId);
                    writer.WriteEndObject();
                    break;

                case "Domain":
                    writer.WriteStartObject();
                    writer.WritePropertyName("Kind");
                    writer.WriteValue("Domain");
                    writer.WritePropertyName("DomainId");
                    writer.WriteValue(value.DomainId);
                    writer.WritePropertyName("Key");
                    writer.WriteValue(value.Key);
                    writer.WriteEndObject();
                    break;

                case "Expr":
                    writer.WriteStartObject();
                    writer.WritePropertyName("Kind");
                    writer.WriteValue("Expr");
                    writer.WritePropertyName("ExprText");
                    writer.WriteValue(value.ExprText);
                    writer.WriteEndObject();
                    break;

                default:
                    writer.WriteValue(value.ConstValue);
                    break;
            }
        }
    }
}
#endif
