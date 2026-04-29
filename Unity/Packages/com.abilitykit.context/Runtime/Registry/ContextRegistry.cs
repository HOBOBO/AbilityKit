using System;
using System.Collections.Generic;
using System.Linq;

namespace AbilityKit.Context
{
    /// <summary>
    /// 上下文注册中心
    /// 对齐 ECS 的 World，管理所有实体和属性
    /// </summary>
    public sealed class ContextRegistry
    {
        private readonly Dictionary<long, EntityData> _entities = new();
        private readonly List<ContextEventHandler> _globalHandlers = new();
        private readonly Dictionary<long, List<ContextEventHandler>> _idHandlers = new();

        private readonly object _lock = new();
        private long _nextEntityId = 1;

        /// <summary>
        /// 实体数据结构
        /// </summary>
        private sealed class EntityData
        {
            public long Id { get; }
            public long CreatedAtMs { get; }
            public Dictionary<int, IProperty> Properties { get; } = new();

            public EntityData(long id)
            {
                Id = id;
                CreatedAtMs = TimeUtil.CurrentTimeMs;
            }
        }

        // ============ 事件 ============

        /// <summary>
        /// 订阅全局事件
        /// </summary>
        public void Subscribe(ContextEventHandler handler)
        {
            lock (_lock)
            {
                _globalHandlers.Add(handler);
            }
        }

        /// <summary>
        /// 取消订阅全局事件
        /// </summary>
        public void Unsubscribe(ContextEventHandler handler)
        {
            lock (_lock)
            {
                _globalHandlers.Remove(handler);
            }
        }

        /// <summary>
        /// 订阅指定实体的事件
        /// </summary>
        public void Subscribe(long entityId, ContextEventHandler handler)
        {
            lock (_lock)
            {
                if (!_idHandlers.TryGetValue(entityId, out var list))
                {
                    list = new List<ContextEventHandler>();
                    _idHandlers[entityId] = list;
                }
                list.Add(handler);
            }
        }

        /// <summary>
        /// 取消订阅指定实体的事件
        /// </summary>
        public void Unsubscribe(long entityId, ContextEventHandler handler)
        {
            lock (_lock)
            {
                if (_idHandlers.TryGetValue(entityId, out var list))
                {
                    list.Remove(handler);
                }
            }
        }

        // ============ 实体操作 ============

        /// <summary>
        /// 创建实体
        /// </summary>
        public EntityBuilder Create()
        {
            lock (_lock)
            {
                var id = _nextEntityId++;
                var entity = new EntityData(id);
                _entities[id] = entity;

                RaiseEvent(ContextEvent.Created(id));
                return new EntityBuilder(this, id);
            }
        }

        /// <summary>
        /// 销毁实体
        /// </summary>
        public bool Destroy(long entityId)
        {
            lock (_lock)
            {
                if (!_entities.TryGetValue(entityId, out var entity))
                    return false;

                RaiseEvent(ContextEvent.Destroying(entityId));

                _entities.Remove(entityId);

                RaiseEvent(ContextEvent.Destroyed(entityId));
                return true;
            }
        }

        /// <summary>
        /// 检查实体是否存在
        /// </summary>
        public bool Exists(long entityId)
        {
            lock (_lock)
            {
                return _entities.ContainsKey(entityId);
            }
        }

        /// <summary>
        /// 生成新的实体 ID
        /// </summary>
        public long GenerateId()
        {
            lock (_lock)
            {
                return _nextEntityId++;
            }
        }

        // ============ 属性操作 ============

        /// <summary>
        /// 添加属性
        /// </summary>
        public void Add<T>(long entityId, T property) where T : class, IProperty
        {
            lock (_lock)
            {
                if (!_entities.TryGetValue(entityId, out var entity))
                    return;

                var type = PropertyTypeRegistry.Instance.Get<T>();
                if (type == null)
                {
                    type = PropertyTypeRegistry.Instance.Register<T>();
                }

                entity.Properties[type.Id] = property;

                RaiseEvent(ContextEvent.Updated(entityId, type.Id, $"__{type.Id}", null, property));
            }
        }

        /// <summary>
        /// 获取属性
        /// </summary>
        public T? Get<T>(long entityId) where T : class, IProperty
        {
            lock (_lock)
            {
                if (!_entities.TryGetValue(entityId, out var entity))
                    return null;

                var type = PropertyTypeRegistry.Instance.Get<T>();
                if (type == null)
                    return null;

                return entity.Properties.TryGetValue(type.Id, out var prop) ? (T)prop : null;
            }
        }

        /// <summary>
        /// 获取属性（带默认值）
        /// </summary>
        public T Get<T>(long entityId, T defaultValue) where T : class, IProperty
        {
            return Get<T>(entityId) ?? defaultValue;
        }

        /// <summary>
        /// 检查实体是否拥有指定属性
        /// </summary>
        public bool Has<T>(long entityId) where T : class, IProperty
        {
            lock (_lock)
            {
                return Get<T>(entityId) != null;
            }
        }

        /// <summary>
        /// 移除属性
        /// </summary>
        public bool Remove<T>(long entityId) where T : class, IProperty
        {
            lock (_lock)
            {
                if (!_entities.TryGetValue(entityId, out var entity))
                    return false;

                var type = PropertyTypeRegistry.Instance.Get<T>();
                if (type == null)
                    return false;

                if (entity.Properties.Remove(type.Id, out var oldProp))
                {
                    RaiseEvent(ContextEvent.Updated(entityId, type.Id, $"__{type.Id}", oldProp, null));
                    return true;
                }
                return false;
            }
        }

        /// <summary>
        /// 设置属性（覆盖）
        /// </summary>
        public void Set<T>(long entityId, T property) where T : class, IProperty
        {
            lock (_lock)
            {
                if (!_entities.TryGetValue(entityId, out var entity))
                    return;

                var type = PropertyTypeRegistry.Instance.Get<T>();
                if (type == null)
                {
                    type = PropertyTypeRegistry.Instance.Register<T>();
                }

                entity.Properties.TryGetValue(type.Id, out var oldProp);
                entity.Properties[type.Id] = property;

                RaiseEvent(ContextEvent.Updated(entityId, type.Id, $"__{type.Id}", oldProp, property));
            }
        }

        // ============ 查询操作 ============

        /// <summary>
        /// 查询拥有指定属性的所有实体 ID
        /// </summary>
        public IEnumerable<long> GetEntitiesWith<T>() where T : class, IProperty
        {
            lock (_lock)
            {
                var type = PropertyTypeRegistry.Instance.Get<T>();
                if (type == null)
                    return Enumerable.Empty<long>();

                return _entities
                    .Where(kv => kv.Value.Properties.ContainsKey(type.Id))
                    .Select(kv => kv.Key)
                    .ToList();
            }
        }

        /// <summary>
        /// 查询拥有指定属性类型 ID 的所有实体 ID（内部使用）
        /// </summary>
        internal IEnumerable<long> GetEntitiesWith(int propertyTypeId)
        {
            lock (_lock)
            {
                return _entities
                    .Where(kv => kv.Value.Properties.ContainsKey(propertyTypeId))
                    .Select(kv => kv.Key)
                    .ToList();
            }
        }

        // ============ 批量操作 ============

        /// <summary>
        /// 销毁所有实体
        /// </summary>
        public void Clear()
        {
            lock (_lock)
            {
                var ids = _entities.Keys.ToList();
                foreach (var id in ids)
                {
                    Destroy(id);
                }
            }
        }

        /// <summary>
        /// 获取实体数量
        /// </summary>
        public int Count
        {
            get
            {
                lock (_lock)
                {
                    return _entities.Count;
                }
            }
        }

        // ============ 内部方法 ============

        private void RaiseEvent(ContextEvent evt)
        {
            List<Exception>? exceptions = null;

            foreach (var handler in _globalHandlers)
            {
                try
                {
                    handler(evt);
                }
                catch (Exception ex)
                {
                    exceptions ??= new List<Exception>();
                    exceptions.Add(ex);
                }
            }

            if (_idHandlers.TryGetValue(evt.EntityId, out var idList))
            {
                foreach (var handler in idList)
                {
                    try
                    {
                        handler(evt);
                    }
                    catch (Exception ex)
                    {
                        exceptions ??= new List<Exception>();
                        exceptions.Add(ex);
                    }
                }
            }

            if (exceptions != null && exceptions.Count > 0)
            {
                throw new AggregateException("One or more event handlers threw exceptions", exceptions);
            }
        }

        /// <summary>
        /// 获取实体的所有属性类型
        /// </summary>
        public IEnumerable<int> GetPropertyTypes(long entityId)
        {
            lock (_lock)
            {
                if (!_entities.TryGetValue(entityId, out var entity))
                    return Enumerable.Empty<int>();

                return entity.Properties.Keys.ToList();
            }
        }
    }

    /// <summary>
    /// 实体构建器
    /// 用于给实体添加属性
    /// </summary>
    public sealed class EntityBuilder
    {
        private readonly ContextRegistry _registry;
        private readonly long _entityId;

        internal EntityBuilder(ContextRegistry registry, long entityId)
        {
            _registry = registry;
            _entityId = entityId;
        }

        /// <summary>
        /// 添加属性
        /// </summary>
        public EntityBuilder With<T>(T property) where T : class, IProperty
        {
            _registry.Add(_entityId, property);
            return this;
        }

        /// <summary>
        /// 添加多个属性
        /// </summary>
        public EntityBuilder With<T1, T2>(T1 prop1, T2 prop2)
            where T1 : class, IProperty
            where T2 : class, IProperty
        {
            _registry.Add(_entityId, prop1);
            _registry.Add(_entityId, prop2);
            return this;
        }

        /// <summary>
        /// 添加多个属性（3个）
        /// </summary>
        public EntityBuilder With<T1, T2, T3>(T1 prop1, T2 prop2, T3 prop3)
            where T1 : class, IProperty
            where T2 : class, IProperty
            where T3 : class, IProperty
        {
            _registry.Add(_entityId, prop1);
            _registry.Add(_entityId, prop2);
            _registry.Add(_entityId, prop3);
            return this;
        }

        /// <summary>
        /// 构建并返回实体 ID
        /// </summary>
        public long Build() => _entityId;
    }
}
