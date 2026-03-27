using System;
using System.Collections.Generic;
using System.Linq;
using UnityHFSM.Actions;

namespace UnityHFSM.Config
{
    /// <summary>
    /// Action 注册信息
    /// </summary>
    public class ActionTypeInfo
    {
        public string TypeName;
        public string DisplayName;
        public string Description;
        public string Category;
        public Type ActionType;
        public Func<IAction> Factory;
    }

    /// <summary>
    /// Action 注册器 - 管理所有可用的 Action 类型
    /// 支持 Attribute 自动发现和手动注册
    /// </summary>
    public static class HfsmActionRegistry
    {
        private static readonly Dictionary<string, ActionTypeInfo> _actions = new();
        private static bool _initialized = false;

        /// <summary>
        /// 初始化注册默认 Action
        /// </summary>
        private static void Initialize()
        {
            if (_initialized)
                return;

            // 注册内置 Action（通过 Attribute 标记的类会被自动发现）
            RegisterBuiltInActions();

            _initialized = true;
        }

        /// <summary>
        /// 注册内置 Action
        /// </summary>
        private static void RegisterBuiltInActions()
        {
            // Primitive Actions
            Register("Wait", () => new WaitAction());
            Register("WaitUntil", () => new WaitUntilAction());
            Register("Log", () => new LogAction());
            Register("SetFloat", () => new SetFloatAction());
            Register("SetBool", () => new SetBoolAction());
            Register("SetInt", () => new SetIntAction());

            // Decorator Actions
            Register("Repeat", () => new RepeatAction());
            Register("Invert", () => new InvertAction());
            Register("TimeLimit", () => new TimeLimitAction());
            Register("UntilSuccess", () => new UntilSuccessAction());
            Register("UntilFailure", () => new UntilFailureAction());
            Register("Cooldown", () => new CooldownAction());
            Register("If", () => new IfAction());
            Register("ConditionalAbort", () => new ConditionalAbortAction());

            // Composite Actions
            Register("Sequence", () => new SequenceAction());
            Register("Selector", () => new SelectorAction());
            Register("Parallel", () => new ParallelAction());
            Register("RandomSelector", () => new RandomSelectorAction());
            Register("RandomSequence", () => new RandomSequenceAction());
        }

        /// <summary>
        /// 注册一个 Action 类型
        /// </summary>
        public static void Register(string typeName, Func<IAction> factory, string displayName = null, string description = null, string category = null)
        {
            if (string.IsNullOrEmpty(typeName))
                throw new ArgumentException("Type name cannot be null or empty", nameof(typeName));

            if (factory == null)
                throw new ArgumentNullException(nameof(factory));

            _actions[typeName] = new ActionTypeInfo
            {
                TypeName = typeName,
                DisplayName = displayName ?? typeName,
                Description = description ?? string.Empty,
                Category = category ?? "Default",
                Factory = factory
            };
        }

        /// <summary>
        /// 注册一个 Action 类型（带 Attribute 标记的类）
        /// </summary>
        public static void Register<T>(string typeName, string displayName = null, string description = null, string category = null) where T : class, IAction, new()
        {
            Register(typeName, () => new T(), displayName, description, category);
        }

        /// <summary>
        /// 创建指定类型名称的 Action 实例
        /// </summary>
        public static IAction Create(string typeName)
        {
            Initialize();

            if (string.IsNullOrEmpty(typeName))
                return null;

            if (_actions.TryGetValue(typeName, out var info))
            {
                return info.Factory?.Invoke();
            }

            return null;
        }

        /// <summary>
        /// 创建指定类型名称的 Action 实例，并设置参数
        /// </summary>
        public static IAction Create(string typeName, Dictionary<string, object> parameters)
        {
            var action = Create(typeName);
            if (action != null && parameters != null)
            {
                ApplyParameters(action, parameters);
            }
            return action;
        }

        /// <summary>
        /// 应用参数到 Action
        /// </summary>
        public static void ApplyParameters(IAction action, Dictionary<string, object> parameters)
        {
            if (parameters == null || parameters.Count == 0)
                return;

            foreach (var param in parameters)
            {
                SetFieldValue(action, param.Key, param.Value);
            }
        }

        private static void SetFieldValue(object target, string fieldName, object value)
        {
            var type = target.GetType();

            // 首先尝试设置公共字段
            var field = type.GetField(fieldName);
            if (field != null)
            {
                var convertedValue = ConvertValue(value, field.FieldType);
                field.SetValue(target, convertedValue);
                return;
            }

            // 然后尝试设置公共属性
            var property = type.GetProperty(fieldName);
            if (property != null && property.CanWrite)
            {
                var convertedValue = ConvertValue(value, property.PropertyType);
                property.SetValue(target, convertedValue);
            }
        }

        private static object ConvertValue(object value, Type targetType)
        {
            if (value == null)
                return null;

            if (targetType.IsAssignableFrom(value.GetType()))
                return value;

            try
            {
                if (targetType == typeof(bool))
                    return Convert.ToBoolean(value);
                if (targetType == typeof(int))
                    return Convert.ToInt32(value);
                if (targetType == typeof(float))
                    return Convert.ToSingle(value);
                if (targetType == typeof(string))
                    return value.ToString();
                if (targetType == typeof(double))
                    return Convert.ToDouble(value);
                if (targetType == typeof(long))
                    return Convert.ToInt64(value);
                if (targetType.IsEnum)
                    return Enum.Parse(targetType, value.ToString());
            }
            catch
            {
                // 转换失败，返回原始值
            }

            return value;
        }

        /// <summary>
        /// 获取所有已注册的 Action 类型信息
        /// </summary>
        public static IReadOnlyList<ActionTypeInfo> GetAllActionTypes()
        {
            Initialize();
            return _actions.Values.ToList();
        }

        /// <summary>
        /// 获取指定类别的所有 Action 类型
        /// </summary>
        public static IReadOnlyList<ActionTypeInfo> GetActionTypesByCategory(string category)
        {
            Initialize();
            return _actions.Values.Where(a => a.Category == category).ToList();
        }

        /// <summary>
        /// 获取所有类别
        /// </summary>
        public static IReadOnlyList<string> GetAllCategories()
        {
            Initialize();
            return _actions.Values.Select(a => a.Category).Distinct().ToList();
        }

        /// <summary>
        /// 检查指定类型名称的 Action 是否存在
        /// </summary>
        public static bool Exists(string typeName)
        {
            Initialize();
            return !string.IsNullOrEmpty(typeName) && _actions.ContainsKey(typeName);
        }

        /// <summary>
        /// 获取 Action 类型信息
        /// </summary>
        public static ActionTypeInfo GetActionTypeInfo(string typeName)
        {
            Initialize();
            return _actions.TryGetValue(typeName, out var info) ? info : null;
        }
    }
}
