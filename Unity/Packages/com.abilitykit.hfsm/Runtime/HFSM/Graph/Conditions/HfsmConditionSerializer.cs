using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityHFSM.Graph.Conditions
{
    /// <summary>
    /// 条件序列化器 - 负责条件的 JSON 序列化和反序列化
    /// </summary>
    public static class HfsmConditionSerializer
    {
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
                Conditions = conditions.ConvertAll(c => new ConditionData
                {
                    TypeName = c.TypeName,
                    Config = ConditionConfigWrapper.CreateFrom(c.ToConfig())
                })
            };

            return JsonUtility.ToJson(wrapper);
        }

        /// <summary>
        /// 从 JSON 字符串反序列化条件列表
        /// </summary>
        /// <param name="json">JSON 字符串</param>
        /// <returns>条件列表</returns>
        public static List<HfsmTransitionCondition> Deserialize(string json)
        {
            var result = new List<HfsmTransitionCondition>();

            if (string.IsNullOrEmpty(json) || json == "{}")
                return result;

            try
            {
                var wrapper = JsonUtility.FromJson<ConditionListWrapper>(json);
                if (wrapper == null || wrapper.Conditions == null)
                    return result;

                foreach (var conditionData in wrapper.Conditions)
                {
                    if (string.IsNullOrEmpty(conditionData.TypeName))
                        continue;

                    var condition = HfsmConditionRegistry.Create(conditionData.TypeName);
                    if (condition != null && conditionData.Config != null)
                    {
                        var configDict = conditionData.Config.ToDictionary();
                        condition.SetFromConfig(configDict);
                        result.Add(condition);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"HfsmConditionSerializer: Failed to deserialize conditions: {e.Message}");
            }

            return result;
        }

        /// <summary>
        /// 序列化包装类（用于 JSON）
        /// </summary>
        [Serializable]
        private class ConditionListWrapper
        {
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

            if (config.TryGetValue("ParameterName", out var name))
                wrapper.ParameterName = name as string;
            if (config.TryGetValue("Operator", out var op))
                wrapper.Operator = Convert.ToInt32(op);
            if (config.TryGetValue("ParameterType", out var type))
                wrapper.ParameterType = Convert.ToInt32(type);
            if (config.TryGetValue("BoolValue", out var bval))
                wrapper.BoolValue = Convert.ToBoolean(bval);
            if (config.TryGetValue("FloatValue", out var fval))
                wrapper.FloatValue = Convert.ToSingle(fval);
            if (config.TryGetValue("IntValue", out var ival))
                wrapper.IntValue = Convert.ToInt32(ival);
            if (config.TryGetValue("Duration", out var dur))
                wrapper.Duration = Convert.ToSingle(dur);
            if (config.TryGetValue("BehaviorId", out var bid))
                wrapper.BehaviorId = bid as string;

            return wrapper;
        }
    }
}
