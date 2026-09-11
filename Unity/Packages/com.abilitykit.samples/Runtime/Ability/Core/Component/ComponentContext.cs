#nullable enable

using System;
using System.Collections.Generic;

namespace AbilityKit.Samples.Logic.Ability.Core.Component
{
    /// <summary>
    /// 组件上下文，提供组件数据的运行时存储。
    /// </summary>
    public sealed class ComponentContext
    {
        private readonly Dictionary<int, Dictionary<Type, object>> _entityComponents;
        private readonly Dictionary<Type, List<int>> _componentIndex;

        public ComponentContext()
        {
            _entityComponents = new Dictionary<int, Dictionary<Type, object>>();
            _componentIndex = new Dictionary<Type, List<int>>();
        }

        /// <summary>
        /// 添加组件到实体。
        /// </summary>
        public void Add<T>(int entityId, T component) where T : class
        {
            var type = typeof(T);

            if (!_entityComponents.TryGetValue(entityId, out var components))
            {
                components = new Dictionary<Type, object>();
                _entityComponents[entityId] = components;
            }

            components[type] = component;

            if (!_componentIndex.TryGetValue(type, out var entities))
            {
                entities = new List<int>();
                _componentIndex[type] = entities;
            }

            if (!entities.Contains(entityId))
            {
                entities.Add(entityId);
            }
        }

        /// <summary>
        /// 获取实体的组件。
        /// </summary>
        public T? Get<T>(int entityId) where T : class
        {
            if (_entityComponents.TryGetValue(entityId, out var components))
            {
                if (components.TryGetValue(typeof(T), out var component))
                {
                    return component as T;
                }
            }

            return null;
        }

        /// <summary>
        /// 移除实体的组件。
        /// </summary>
        public bool Remove<T>(int entityId) where T : class
        {
            var type = typeof(T);

            if (_entityComponents.TryGetValue(entityId, out var components))
            {
                if (components.Remove(type))
                {
                    if (_componentIndex.TryGetValue(type, out var entities))
                    {
                        entities.Remove(entityId);
                    }

                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 检查实体是否拥有组件。
        /// </summary>
        public bool Has<T>(int entityId) where T : class
        {
            if (_entityComponents.TryGetValue(entityId, out var components))
            {
                return components.ContainsKey(typeof(T));
            }

            return false;
        }

        /// <summary>
        /// 查询拥有指定组件类型的所有实体。
        /// </summary>
        public IReadOnlyList<int> Query<T>() where T : class
        {
            if (_componentIndex.TryGetValue(typeof(T), out var entities))
            {
                return entities.AsReadOnly();
            }

            return Array.Empty<int>();
        }
    }
}
