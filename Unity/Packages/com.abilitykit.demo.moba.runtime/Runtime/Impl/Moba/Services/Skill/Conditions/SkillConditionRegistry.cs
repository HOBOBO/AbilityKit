using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AbilityKit.Ability.Share.Common.Log;
using AbilityKit.Ability.World.Services;

namespace AbilityKit.Ability.Share.Impl.Moba.Services
{
    /// <summary>
    /// 技能条件注册表
    /// 提供技能条件的发现、注册和创建
    /// </summary>
    public sealed class SkillConditionRegistry : IService
    {
        private readonly Dictionary<string, ConditionDescriptor> _byId = new Dictionary<string, ConditionDescriptor>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<Type, ConditionDescriptor> _byType = new Dictionary<Type, ConditionDescriptor>();

        private bool _initialized;

        /// <summary>
        /// 注册条件
        /// </summary>
        public void Register(ISkillCondition condition, int priority = 0)
        {
            if (condition == null) return;

            var descriptor = new ConditionDescriptor(condition, priority);
            _byId[condition.Id] = descriptor;
            _byType[condition.GetType()] = descriptor;
        }

        /// <summary>
        /// 根据ID获取条件
        /// </summary>
        public bool TryGet(string conditionId, out ISkillCondition condition)
        {
            if (_byId.TryGetValue(conditionId, out var descriptor))
            {
                condition = descriptor.Factory?.Invoke() ?? descriptor.Instance;
                return condition != null;
            }
            condition = null;
            return false;
        }

        /// <summary>
        /// 获取所有已注册的条件ID
        /// </summary>
        public IEnumerable<string> GetAllConditionIds() => _byId.Keys;

        /// <summary>
        /// 检查条件是否已注册
        /// </summary>
        public bool IsRegistered(string conditionId) => _byId.ContainsKey(conditionId);

        /// <summary>
        /// 创建条件描述符
        /// </summary>
        public ConditionDescriptor GetDescriptor(string conditionId)
        {
            return _byId.TryGetValue(conditionId, out var d) ? d : null;
        }

        /// <summary>
        /// 自动发现并注册所有带 SkillConditionAttribute 的条件
        /// </summary>
        public void DiscoverAndRegister()
        {
            if (_initialized) return;

            var assembly = typeof(SkillConditionRegistry).Assembly;
            var types = assembly.GetTypes();

            foreach (var type in types)
            {
                if (type.IsAbstract || type.IsInterface) continue;
                if (!typeof(ISkillCondition).IsAssignableFrom(type)) continue;

                var attr = type.GetCustomAttributes(typeof(SkillConditionAttribute), false)
                    .FirstOrDefault() as SkillConditionAttribute;
                if (attr == null) continue;

                try
                {
                    ISkillCondition instance = null;
                    // 尝试使用无参构造函数创建实例
                    var ctor = type.GetConstructor(Type.EmptyTypes);
                    if (ctor != null)
                    {
                        instance = (ISkillCondition)ctor.Invoke(null);
                    }

                    var priority = attr.Priority;
                    if (instance != null)
                    {
                        var descriptor = new ConditionDescriptor(instance, priority);
                        _byId[attr.Id] = descriptor;
                        _byType[type] = descriptor;
                        Log.Info($"[SkillConditionRegistry] Discovered: {attr.Id} ({type.Name})");
                    }
                }
                catch (Exception ex)
                {
                    Log.Exception(ex, $"[SkillConditionRegistry] Failed to instantiate {type.Name}");
                }
            }

            _initialized = true;
        }

        public void Dispose()
        {
            _byId.Clear();
            _byType.Clear();
        }

        /// <summary>
        /// 条件描述符
        /// </summary>
        public sealed class ConditionDescriptor
        {
            public string Id { get; }
            public string DisplayName { get; }
            public int Priority { get; }
            public ISkillCondition Instance { get; }
            public Func<ISkillCondition> Factory { get; }

            public ConditionDescriptor(ISkillCondition instance, int priority)
            {
                Instance = instance;
                Id = instance?.Id;
                DisplayName = instance?.DisplayName;
                Priority = priority;
            }

            public ConditionDescriptor(string id, string displayName, Func<ISkillCondition> factory, int priority)
            {
                Id = id;
                DisplayName = displayName;
                Factory = factory;
                Priority = priority;
            }
        }
    }
}