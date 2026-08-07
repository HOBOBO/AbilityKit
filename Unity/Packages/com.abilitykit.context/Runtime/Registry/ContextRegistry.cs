using System;
using System.Collections.Generic;
using System.Linq;

namespace AbilityKit.Context
{
    /// <summary>
    /// 上下文注册中心
    /// 对齐 ECS 的 World，管理所有实体、属性和流程上下文
    /// </summary>
    public sealed class ContextRegistry
    {
        private readonly Dictionary<long, EntityData> _entities = new Dictionary<long, EntityData>();
        private readonly Dictionary<long, FlowContext> _flows = new Dictionary<long, FlowContext>();
        private readonly Dictionary<int, HashSet<long>> _entitiesByPropertyType = new Dictionary<int, HashSet<long>>();
        private readonly List<ContextEventHandler> _globalHandlers = new List<ContextEventHandler>();
        private readonly Dictionary<long, List<ContextEventHandler>> _idHandlers = new Dictionary<long, List<ContextEventHandler>>();

        private readonly object _lock = new object();
        private long _nextEntityId = 1;
        private long _nextFlowId = 1;

        /// <summary>
        /// Receives event observer failures without changing the result of the state operation.
        /// </summary>
        public Action<ContextEvent, Exception> EventHandlerException { get; set; }

        private sealed class EntityData
        {
            public long Id { get; }
            public long FlowId { get; set; }
            public long CreatedAtMs { get; }
            public Dictionary<int, IProperty> Properties { get; } = new Dictionary<int, IProperty>();

            public EntityData(long id, long flowId)
            {
                Id = id;
                FlowId = flowId;
                CreatedAtMs = TimeUtil.CurrentTimeMs;
            }
        }

        public void Subscribe(ContextEventHandler handler)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            lock (_lock)
                _globalHandlers.Add(handler);
        }

        public void Unsubscribe(ContextEventHandler handler)
        {
            if (handler == null) return;
            lock (_lock)
                _globalHandlers.Remove(handler);
        }

        public void Subscribe(long entityId, ContextEventHandler handler)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
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

        public void Unsubscribe(long entityId, ContextEventHandler handler)
        {
            if (handler == null) return;
            lock (_lock)
            {
                if (_idHandlers.TryGetValue(entityId, out var list))
                    list.Remove(handler);
            }
        }

        public EntityBuilder Create()
        {
            return CreateInFlow(0);
        }

        public EntityBuilder CreateInFlow(long flowId)
        {
            ContextEvent evt;
            long id;
            lock (_lock)
            {
                if (flowId != 0 && !_flows.ContainsKey(flowId))
                    throw new ArgumentException($"Flow {flowId} not found", nameof(flowId));

                id = _nextEntityId++;
                var entity = new EntityData(id, flowId);
                _entities[id] = entity;
                if (flowId != 0)
                    _flows[flowId].AddEntity(id);

                evt = ContextEvent.Created(id, flowId);
            }

            RaiseEvent(evt);
            return new EntityBuilder(this, id);
        }

        public bool Destroy(long entityId)
        {
            ContextEvent destroying;
            ContextEvent destroyed;
            lock (_lock)
            {
                if (!_entities.TryGetValue(entityId, out var entity))
                    return false;

                destroying = ContextEvent.Destroying(entityId, entity.FlowId);
            }

            RaiseEvent(destroying);

            lock (_lock)
            {
                if (!_entities.TryGetValue(entityId, out var entity))
                    return false;

                foreach (var propertyTypeId in entity.Properties.Keys.ToList())
                    RemovePropertyIndex(propertyTypeId, entityId);

                if (entity.FlowId != 0 && _flows.TryGetValue(entity.FlowId, out var flow))
                    flow.RemoveEntity(entityId);

                _entities.Remove(entityId);
                _idHandlers.Remove(entityId);
                destroyed = ContextEvent.Destroyed(entityId, entity.FlowId);
            }

            RaiseEvent(destroyed);
            return true;
        }

        public bool Exists(long entityId)
        {
            lock (_lock)
                return _entities.ContainsKey(entityId);
        }

        public long GenerateId()
        {
            lock (_lock)
                return _nextEntityId++;
        }

        public FlowContextScope BeginFlow(string name = null, long ownerEntityId = 0, long parentFlowId = 0, FlowContextPhase disposePhase = FlowContextPhase.Completed)
        {
            var flowId = CreateFlow(name, ownerEntityId, parentFlowId);
            SetFlowPhase(flowId, FlowContextPhase.Running);
            return new FlowContextScope(this, flowId, disposePhase);
        }

        public long CreateFlow(string name = null, long ownerEntityId = 0, long parentFlowId = 0)
        {
            ContextEvent evt;
            long flowId;
            lock (_lock)
            {
                if (parentFlowId != 0 && !_flows.ContainsKey(parentFlowId))
                    throw new ArgumentException($"Parent flow {parentFlowId} not found", nameof(parentFlowId));

                flowId = _nextFlowId++;
                var flow = new FlowContext(flowId, parentFlowId, ownerEntityId, name ?? string.Empty);
                _flows[flowId] = flow;
                if (parentFlowId != 0)
                    _flows[parentFlowId].AddChildFlow(flowId);

                evt = ContextEvent.FlowCreated(flowId, ownerEntityId, parentFlowId, flow.Phase);
            }

            RaiseEvent(evt);
            return flowId;
        }

        public bool SetFlowPhase(long flowId, FlowContextPhase phase)
        {
            ContextEvent evt;
            lock (_lock)
            {
                if (!_flows.TryGetValue(flowId, out var flow))
                    return false;

                if (flow.Phase == phase)
                    return true;

                flow.SetPhase(phase);
                evt = ContextEvent.FlowPhaseChanged(flowId, flow.OwnerEntityId, flow.ParentFlowId, phase);
            }

            RaiseEvent(evt);
            return true;
        }

        public bool TryGetFlow(long flowId, out FlowContextInfo info)
        {
            lock (_lock)
            {
                if (_flows.TryGetValue(flowId, out var flow))
                {
                    info = flow.ToInfo();
                    return true;
                }
            }

            info = default;
            return false;
        }

        public IEnumerable<FlowContextInfo> GetFlows(bool activeOnly = false)
        {
            lock (_lock)
            {
                return _flows.Values
                    .Where(flow => !activeOnly || flow.ToInfo().IsActive)
                    .Select(flow => flow.ToInfo())
                    .ToList();
            }
        }


        public IEnumerable<long> GetEntitiesInFlow(long flowId)
        {
            lock (_lock)
            {
                if (!_flows.TryGetValue(flowId, out var flow))
                    return Enumerable.Empty<long>();
                return flow.EntityIds.ToList();
            }
        }

        public long GetEntityFlow(long entityId)
        {
            lock (_lock)
                return _entities.TryGetValue(entityId, out var entity) ? entity.FlowId : 0;
        }

        public void Add<T>(long entityId, T property) where T : class, IProperty
        {
            if (property == null) throw new ArgumentNullException(nameof(property));
            var type = GetOrRegisterPropertyType<T>();
            ContextEvent? evt = null;
            lock (_lock)
            {
                if (!_entities.TryGetValue(entityId, out var entity))
                    return;

                entity.Properties[type.Id] = property;
                AddPropertyIndex(type.Id, entityId);
                evt = ContextEvent.Updated(entityId, entity.FlowId, type.Id, $"__{type.Id}", null, property);
            }

            RaiseEvent(evt.Value);
        }

        public T Get<T>(long entityId) where T : class, IProperty
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

        public T Get<T>(long entityId, T defaultValue) where T : class, IProperty
        {
            return Get<T>(entityId) ?? defaultValue;
        }

        public bool Has<T>(long entityId) where T : class, IProperty
        {
            lock (_lock)
            {
                if (!_entities.TryGetValue(entityId, out var entity))
                    return false;

                var type = PropertyTypeRegistry.Instance.Get<T>();
                return type != null && entity.Properties.ContainsKey(type.Id);
            }
        }

        public bool Remove<T>(long entityId) where T : class, IProperty
        {
            var type = PropertyTypeRegistry.Instance.Get<T>();
            if (type == null)
                return false;

            ContextEvent? evt = null;
            lock (_lock)
            {
                if (!_entities.TryGetValue(entityId, out var entity))
                    return false;

                if (!entity.Properties.Remove(type.Id, out var oldProp))
                    return false;

                RemovePropertyIndex(type.Id, entityId);
                evt = ContextEvent.Updated(entityId, entity.FlowId, type.Id, $"__{type.Id}", oldProp, null);
            }

            RaiseEvent(evt.Value);
            return true;
        }

        public void Set<T>(long entityId, T property) where T : class, IProperty
        {
            if (property == null) throw new ArgumentNullException(nameof(property));
            var type = GetOrRegisterPropertyType<T>();
            ContextEvent? evt = null;
            lock (_lock)
            {
                if (!_entities.TryGetValue(entityId, out var entity))
                    return;

                entity.Properties.TryGetValue(type.Id, out var oldProp);
                entity.Properties[type.Id] = property;
                AddPropertyIndex(type.Id, entityId);
                evt = ContextEvent.Updated(entityId, entity.FlowId, type.Id, $"__{type.Id}", oldProp, property);
            }

            RaiseEvent(evt.Value);
        }

        public IEnumerable<long> GetEntitiesWith<T>() where T : class, IProperty
        {
            var type = PropertyTypeRegistry.Instance.Get<T>();
            return type == null ? Enumerable.Empty<long>() : GetEntitiesWith(type.Id);
        }

        internal IEnumerable<long> GetEntitiesWith(int propertyTypeId)
        {
            lock (_lock)
            {
                return _entitiesByPropertyType.TryGetValue(propertyTypeId, out var ids)
                    ? ids.ToList()
                    : Enumerable.Empty<long>();
            }
        }

        internal IEnumerable<long> GetEntitiesWithout(int propertyTypeId)
        {
            lock (_lock)
            {
                if (!_entitiesByPropertyType.TryGetValue(propertyTypeId, out var ids))
                    return _entities.Keys.ToList();

                return _entities.Keys.Where(id => !ids.Contains(id)).ToList();
            }
        }

        internal IEnumerable<long> GetAllEntityIds()
        {
            lock (_lock)
                return _entities.Keys.ToList();
        }

        internal IProperty GetProperty(long entityId, int propertyTypeId)
        {
            lock (_lock)
            {
                if (!_entities.TryGetValue(entityId, out var entity))
                    return null;

                return entity.Properties.TryGetValue(propertyTypeId, out var property) ? property : null;
            }
        }

        public QueryBuilder Query()
        {
            return new QueryBuilder(this);
        }

        public void Clear()
        {
            foreach (var id in GetAllEntityIds().ToList())
                Destroy(id);

            lock (_lock)
            {
                _flows.Clear();
                _entitiesByPropertyType.Clear();
                _nextEntityId = 1;
                _nextFlowId = 1;
            }
        }

        public int Count
        {
            get
            {
                lock (_lock)
                    return _entities.Count;
            }
        }

        public int FlowCount
        {
            get
            {
                lock (_lock)
                    return _flows.Count;
            }
        }

        public IEnumerable<int> GetPropertyTypes(long entityId)
        {
            lock (_lock)
            {
                if (!_entities.TryGetValue(entityId, out var entity))
                    return Enumerable.Empty<int>();

                return entity.Properties.Keys.ToList();
            }
        }

        private static PropertyType GetOrRegisterPropertyType<T>() where T : IProperty
        {
            var type = PropertyTypeRegistry.Instance.Get<T>();
            return type ?? PropertyTypeRegistry.Instance.Register<T>();
        }

        private void AddPropertyIndex(int propertyTypeId, long entityId)
        {
            if (!_entitiesByPropertyType.TryGetValue(propertyTypeId, out var ids))
            {
                ids = new HashSet<long>();
                _entitiesByPropertyType[propertyTypeId] = ids;
            }

            ids.Add(entityId);
        }

        private void RemovePropertyIndex(int propertyTypeId, long entityId)
        {
            if (!_entitiesByPropertyType.TryGetValue(propertyTypeId, out var ids))
                return;

            ids.Remove(entityId);
            if (ids.Count == 0)
                _entitiesByPropertyType.Remove(propertyTypeId);
        }

        private void RaiseEvent(ContextEvent evt)
        {
            List<ContextEventHandler> handlers;
            Action<ContextEvent, Exception> exceptionHandler;
            lock (_lock)
            {
                handlers = new List<ContextEventHandler>(_globalHandlers);
                if (_idHandlers.TryGetValue(evt.EntityId, out var idList))
                    handlers.AddRange(idList);
                exceptionHandler = EventHandlerException;
            }

            foreach (var handler in handlers)
            {
                try
                {
                    handler(evt);
                }
                catch (Exception ex)
                {
                    if (exceptionHandler == null)
                        continue;

                    try
                    {
                        exceptionHandler(evt, ex);
                    }
                    catch
                    {
                        // Diagnostics observers must not affect context state or lifecycle.
                    }
                }
            }
        }
    }

    public sealed class EntityBuilder
    {
        private readonly ContextRegistry _registry;
        private readonly long _entityId;

        internal EntityBuilder(ContextRegistry registry, long entityId)
        {
            _registry = registry;
            _entityId = entityId;
        }

        public long EntityId => _entityId;

        public EntityBuilder With<T>(T property) where T : class, IProperty
        {
            _registry.Add(_entityId, property);
            return this;
        }

        public EntityBuilder With<T1, T2>(T1 prop1, T2 prop2)
            where T1 : class, IProperty
            where T2 : class, IProperty
        {
            _registry.Add(_entityId, prop1);
            _registry.Add(_entityId, prop2);
            return this;
        }

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

        public long Build() => _entityId;
    }
}
