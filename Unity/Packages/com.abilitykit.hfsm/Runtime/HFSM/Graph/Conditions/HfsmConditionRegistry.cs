using System;
using System.Collections.Generic;
using System.Linq;

namespace UnityHFSM.Graph.Conditions
{
    /// <summary>
    /// 条件注册器 - 管理所有可用的条件类型
    /// 开发者可以通过此类注册自定义条件类型
    /// </summary>
    public static class HfsmConditionRegistry
    {
        private static readonly Dictionary<string, Type> _conditions = new();
        private static bool _initialized = false;

        /// <summary>
        /// 初始化注册默认条件
        /// </summary>
        private static void Initialize()
        {
            if (_initialized)
                return;

            // 注册内置条件
            Register<HfsmParameterCondition>();
            Register<HfsmBehaviorCompleteCondition>();
            Register<HfsmTimeElapsedCondition>();

            _initialized = true;
        }

        /// <summary>
        /// 注册一个条件类型
        /// </summary>
        /// <typeparam name="T">条件类型，必须继承自 HfsmTransitionCondition</typeparam>
        public static void Register<T>() where T : HfsmTransitionCondition, new()
        {
            var condition = new T();
            Register(condition.TypeName, typeof(T));
        }

        /// <summary>
        /// 注册一个条件类型
        /// </summary>
        /// <param name="typeName">条件类型名称</param>
        /// <param name="conditionType">条件类型</param>
        public static void Register(string typeName, Type conditionType)
        {
            if (string.IsNullOrEmpty(typeName))
                throw new ArgumentException("Type name cannot be null or empty", nameof(typeName));

            if (!typeof(HfsmTransitionCondition).IsAssignableFrom(conditionType))
                throw new ArgumentException($"Type must inherit from {nameof(HfsmTransitionCondition)}", nameof(conditionType));

            _conditions[typeName] = conditionType;
        }

        /// <summary>
        /// 创建指定类型名称的条件实例
        /// </summary>
        /// <param name="typeName">条件类型名称</param>
        /// <returns>条件实例，如果类型不存在则返回 null</returns>
        public static HfsmTransitionCondition Create(string typeName)
        {
            Initialize();

            if (string.IsNullOrEmpty(typeName))
                return null;

            if (_conditions.TryGetValue(typeName, out var type))
            {
                return Activator.CreateInstance(type) as HfsmTransitionCondition;
            }

            return null;
        }

        /// <summary>
        /// 获取所有已注册的条件类型信息
        /// </summary>
        /// <returns>条件类型信息列表</returns>
        public static IReadOnlyList<ConditionTypeInfo> GetAllConditionTypes()
        {
            Initialize();

            return _conditions.Values
                .Select(t =>
                {
                    var condition = Activator.CreateInstance(t) as HfsmTransitionCondition;
                    return new ConditionTypeInfo
                    {
                        TypeName = condition.TypeName,
                        DisplayName = condition.DisplayName
                    };
                })
                .ToList();
        }

        /// <summary>
        /// 检查指定类型名称的条件是否存在
        /// </summary>
        /// <param name="typeName">条件类型名称</param>
        /// <returns>是否存在</returns>
        public static bool Exists(string typeName)
        {
            Initialize();
            return !string.IsNullOrEmpty(typeName) && _conditions.ContainsKey(typeName);
        }
    }

    /// <summary>
    /// 条件类型信息
    /// </summary>
    public class ConditionTypeInfo
    {
        public string TypeName;
        public string DisplayName;
    }
}
