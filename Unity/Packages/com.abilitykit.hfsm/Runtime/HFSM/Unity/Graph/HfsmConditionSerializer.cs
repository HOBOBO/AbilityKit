using System;
using System.Collections.Generic;

namespace UnityHFSM.Graph.Conditions
{
    /// <summary>
    /// 条件序列化器 - 负责条件的 JSON 序列化和反序列化
    /// </summary>
    public static class HfsmConditionSerializer
    {
#if !UNITY_EDITOR && !UNITY_STANDALONE && !UNITY_WEBGL && !UNITY_ANDROID && !UNITY_IOS && !UNITY_SERVER
        private static System.Text.Json.JsonSerializerOptions JsonOptions => HfsmConditionSerializerJson.JsonOptions;
#endif
        /// <summary>
        /// 将条件列表序列化为 JSON 字符串
        /// </summary>
        /// <param name="conditions">条件列表</param>
        /// <returns>JSON 字符串</returns>
        public static string Serialize(List<HfsmTransitionCondition> conditions)
        {
            if (conditions == null || conditions.Count == 0)
                return "{}";

            var wrapper = new ConditionListWrapper
            {
                Version = 2,
                Conditions = conditions.ConvertAll(c => new ConditionData
                {
                    TypeName = c.TypeName,
                    Config = ConditionConfigWrapper.CreateFrom(c.ToConfig())
                })
            };

#if UNITY_EDITOR || UNITY_STANDALONE || UNITY_WEBGL || UNITY_ANDROID || UNITY_IOS || UNITY_SERVER
            return UnityEngine.JsonUtility.ToJson(wrapper);
#else
            return System.Text.Json.JsonSerializer.Serialize(wrapper, JsonOptions);
#endif
        }

        /// <summary>
        /// 从 JSON 字符串反序列化条件列表
        /// </summary>
        /// <param name="json">JSON 字符串</param>
        /// <returns>条件列表</returns>
        public static List<HfsmTransitionCondition> Deserialize(string json)
        {
            try
            {
                return DeserializeCore(json, false);
            }
            catch (Exception e)
            {
#if UNITY_EDITOR || UNITY_STANDALONE || UNITY_WEBGL || UNITY_ANDROID || UNITY_IOS || UNITY_SERVER
                UnityEngine.Debug.LogError($"HfsmConditionSerializer: Failed to deserialize conditions: {e.Message}");
#else
                UnityHFSM.Graph.HfsmLog.LogError($"HfsmConditionSerializer: Failed to deserialize conditions: {e.Message}");
#endif
                return new List<HfsmTransitionCondition>();
            }
        }

        public static List<HfsmTransitionCondition> DeserializeStrict(string json)
        {
            return DeserializeCore(json, true);
        }

        private static List<HfsmTransitionCondition> DeserializeCore(string json, bool strict)
        {
            var result = new List<HfsmTransitionCondition>();
            if (string.IsNullOrEmpty(json) || json == "{}")
                return result;

#if UNITY_EDITOR || UNITY_STANDALONE || UNITY_WEBGL || UNITY_ANDROID || UNITY_IOS || UNITY_SERVER
            var wrapper = UnityEngine.JsonUtility.FromJson<ConditionListWrapper>(json);
#else
            var wrapper = System.Text.Json.JsonSerializer.Deserialize<ConditionListWrapper>(json, JsonOptions);
#endif
            if (wrapper == null)
                throw new InvalidOperationException("The condition document could not be deserialized.");
            if (strict && wrapper.Version > 2)
                throw new InvalidOperationException($"Condition document version '{wrapper.Version}' is not supported.");
            if (wrapper.Conditions == null)
            {
                if (strict)
                    throw new InvalidOperationException("The condition document has no condition list.");
                return result;
            }

            foreach (var conditionData in wrapper.Conditions)
            {
                if (conditionData == null || string.IsNullOrEmpty(conditionData.TypeName))
                {
                    if (strict)
                        throw new InvalidOperationException("A serialized condition has no type name.");
                    continue;
                }

                var condition = HfsmConditionRegistry.Create(conditionData.TypeName);
                if (condition == null)
                {
                    if (strict)
                        throw new InvalidOperationException($"Condition type '{conditionData.TypeName}' is not registered.");
                    continue;
                }

                if (conditionData.Config == null)
                {
                    if (strict)
                        throw new InvalidOperationException($"Condition '{conditionData.TypeName}' has no configuration.");
                    continue;
                }

                condition.SetFromConfig(conditionData.Config.ToDictionary());
                result.Add(condition);
            }

            return result;
        }

        /// <summary>
        /// 序列化包装类（用于 JSON）
        /// </summary>
        [Serializable]
        private class ConditionListWrapper
        {
            public int Version;
            public List<ConditionData> Conditions;
        }

        /// <summary>
        /// 单个条件数据（用于 JSON）
        /// </summary>
        [Serializable]
        private class ConditionData
        {
            public string TypeName;
            public ConditionConfigWrapper Config;
        }
    }

    /// <summary>
    /// 可序列化的条件配置包装器
    /// </summary>
    [Serializable]
    public class ConditionConfigWrapper
    {
        public List<ConditionConfigEntry> Entries = new List<ConditionConfigEntry>();

        // Legacy v1 fields retained for backward-compatible reads.
        public string ParameterName;
        public int Operator;
        public int ParameterType;
        public bool BoolValue;
        public float FloatValue;
        public int IntValue;
        public float Duration;
        public string BehaviorId;

        public Dictionary<string, object> ToDictionary()
        {
            if (Entries != null && Entries.Count > 0)
            {
                var entries = new Dictionary<string, object>(StringComparer.Ordinal);
                foreach (var entry in Entries)
                {
                    if (entry != null && !string.IsNullOrEmpty(entry.Key))
                        entries[entry.Key] = entry.GetValue();
                }

                return entries;
            }

            var dict = new Dictionary<string, object>();

            if (ParameterName != null)
                dict["ParameterName"] = ParameterName;
            if (Operator != 0 || dict.Count > 0)
                dict["Operator"] = Operator;
            if (ParameterType != 0 || dict.Count > 0)
                dict["ParameterType"] = ParameterType;
            if (BoolValue || dict.Count > 0)
                dict["BoolValue"] = BoolValue;
            if (FloatValue != 0f || dict.Count > 0)
                dict["FloatValue"] = FloatValue;
            if (IntValue != 0 || dict.Count > 0)
                dict["IntValue"] = IntValue;
            if (Duration != 0f)
                dict["Duration"] = Duration;
            if (BehaviorId != null)
                dict["BehaviorId"] = BehaviorId;

            return dict;
        }

        public static ConditionConfigWrapper CreateFrom(Dictionary<string, object> config)
        {
            var wrapper = new ConditionConfigWrapper();

            if (config == null)
                return wrapper;

            var keys = new List<string>(config.Keys);
            keys.Sort(StringComparer.Ordinal);
            foreach (var key in keys)
            {
                wrapper.Entries.Add(ConditionConfigEntry.Create(key, config[key]));
            }

            return wrapper;
        }
    }

    public enum ConditionConfigValueType
    {
        Null,
        String,
        Boolean,
        Int32,
        Int64,
        Single,
        Double
    }

    [Serializable]
    public sealed class ConditionConfigEntry
    {
        public string Key;
        public ConditionConfigValueType ValueType;
        public string StringValue;
        public bool BoolValue;
        public int Int32Value;
        public long Int64Value;
        public float SingleValue;
        public double DoubleValue;

        public object GetValue()
        {
            return ValueType switch
            {
                ConditionConfigValueType.Null => null,
                ConditionConfigValueType.String => StringValue,
                ConditionConfigValueType.Boolean => BoolValue,
                ConditionConfigValueType.Int32 => Int32Value,
                ConditionConfigValueType.Int64 => Int64Value,
                ConditionConfigValueType.Single => SingleValue,
                ConditionConfigValueType.Double => DoubleValue,
                _ => throw new InvalidOperationException($"Unknown condition config value type '{ValueType}'.")
            };
        }

        public static ConditionConfigEntry Create(string key, object value)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentException("Condition config keys cannot be null or empty.", nameof(key));

            var entry = new ConditionConfigEntry { Key = key };
            if (value == null)
            {
                entry.ValueType = ConditionConfigValueType.Null;
                return entry;
            }

            if (value.GetType().IsEnum)
            {
                entry.ValueType = ConditionConfigValueType.Int32;
                entry.Int32Value = Convert.ToInt32(value);
                return entry;
            }

            switch (value)
            {
                case string stringValue:
                    entry.ValueType = ConditionConfigValueType.String;
                    entry.StringValue = stringValue;
                    break;
                case bool boolValue:
                    entry.ValueType = ConditionConfigValueType.Boolean;
                    entry.BoolValue = boolValue;
                    break;
                case byte byteValue:
                    entry.ValueType = ConditionConfigValueType.Int32;
                    entry.Int32Value = byteValue;
                    break;
                case sbyte signedByteValue:
                    entry.ValueType = ConditionConfigValueType.Int32;
                    entry.Int32Value = signedByteValue;
                    break;
                case short shortValue:
                    entry.ValueType = ConditionConfigValueType.Int32;
                    entry.Int32Value = shortValue;
                    break;
                case ushort unsignedShortValue:
                    entry.ValueType = ConditionConfigValueType.Int32;
                    entry.Int32Value = unsignedShortValue;
                    break;
                case int intValue:
                    entry.ValueType = ConditionConfigValueType.Int32;
                    entry.Int32Value = intValue;
                    break;
                case long longValue:
                    entry.ValueType = ConditionConfigValueType.Int64;
                    entry.Int64Value = longValue;
                    break;
                case float floatValue:
                    entry.ValueType = ConditionConfigValueType.Single;
                    entry.SingleValue = floatValue;
                    break;
                case double doubleValue:
                    entry.ValueType = ConditionConfigValueType.Double;
                    entry.DoubleValue = doubleValue;
                    break;
                default:
                    throw new NotSupportedException(
                        $"Condition config value '{key}' has unsupported type '{value.GetType().FullName}'. " +
                        "Use null, string, bool, integer, float, double, or enum values.");
            }

            return entry;
        }
    }

#if !UNITY_EDITOR && !UNITY_STANDALONE && !UNITY_WEBGL && !UNITY_ANDROID && !UNITY_IOS && !UNITY_SERVER
    internal static class HfsmConditionSerializerJson
    {
        internal static readonly System.Text.Json.JsonSerializerOptions JsonOptions = new System.Text.Json.JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            IncludeFields = true,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = false
        };
    }
#endif
}
