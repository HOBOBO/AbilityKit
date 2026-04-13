using System;
using System.Collections.Generic;
using AbilityKit.Ability.World.DI;
using Entitas;

namespace AbilityKit.Ability.World
{
    /// <summary>
    /// 基于 IGroup 观察者的响应式系统基类。
    /// 支持监控实体的 Added、Removed 事件以及组件替换（Replace）事件。
    /// </summary>
    /// <typeparam name="TEntity">实体类型，必须实现 IEntity 接口</typeparam>
    public abstract class ReactiveWorldSystemBase<TEntity> : WorldSystemBase where TEntity : class, IEntity
    {
        private readonly HashSet<TEntity> _pending = new HashSet<TEntity>();
        private readonly Dictionary<TEntity, EntityComponentReplaced> _handlers = new Dictionary<TEntity, EntityComponentReplaced>();

        private readonly List<TEntity> _executionBuffer = new List<TEntity>(64);
        private TEntity[] _executionArray = Array.Empty<TEntity>();

        private IGroup<TEntity> _group;

        /// <summary>
        /// 获取当前观察的实体组。
        /// </summary>
        protected IGroup<TEntity> Group => _group;

        /// <summary>
        /// 初始化响应式系统基类。
        /// </summary>
        /// <param name="contexts">Entitas 上下文集合</param>
        /// <param name="services">世界服务解析器</param>
        protected ReactiveWorldSystemBase(global::Entitas.IContexts contexts, IWorldResolver services)
            : base(contexts, services)
        {
        }

        /// <summary>
        /// 创建要观察的实体组。子类必须实现此方法。
        /// </summary>
        /// <param name="contexts">Entitas 上下文集合</param>
        /// <returns>要观察的实体组</returns>
        protected abstract IGroup<TEntity> CreateGroup(global::Entitas.IContexts contexts);

        /// <summary>
        /// 判断当组件被替换时是否应该触发响应。子类实现此方法指定要监听的组件索引。
        /// </summary>
        /// <param name="componentIndex">组件索引</param>
        /// <returns>是否应该触发</returns>
        protected abstract bool ShouldReactToReplace(int componentIndex);

        /// <summary>
        /// 当实体发生变化（Added 或 Replace）时调用。子类实现此方法处理实体。
        /// </summary>
        /// <param name="entity">发生变化的实体</param>
        protected abstract void OnEntityChanged(TEntity entity);

        /// <summary>
        /// 当实体从组中移除时调用。子类实现此方法处理实体移除。
        /// </summary>
        /// <param name="entity">被移除的实体</param>
        protected abstract void OnEntityRemovedFromGroup(TEntity entity);

        /// <summary>
        /// 是否启用组移除调试日志。默认为 false。
        /// </summary>
        protected virtual bool EnableGroupRemoveDebugLog => false;

        /// <summary>
        /// 当实体从组中移除时调用的调试回调。EnableGroupRemoveDebugLog 为 true 时调用。
        /// </summary>
        /// <param name="group">实体组</param>
        /// <param name="entity">被移除的实体</param>
        /// <param name="index">组件索引</param>
        /// <param name="component">被移除的组件</param>
        protected virtual void OnGroupRemoveDebug(IGroup<TEntity> group, TEntity entity, int index, IComponent component)
        {
        }

        /// <summary>
        /// 初始化阶段：创建组并订阅实体事件。
        /// </summary>
        protected override void OnInit()
        {
            _group = CreateGroup(Contexts);
            if (_group == null) return;

            _group.OnEntityAdded += OnGroupEntityAdded;
            _group.OnEntityRemoved += OnGroupEntityRemoved;

            var existing = _group.GetEntities();
            if (existing != null)
            {
                for (int i = 0; i < existing.Length; i++)
                {
                    TrackAndEnqueue(existing[i]);
                }
            }
        }

        /// <summary>
        /// 执行阶段：处理所有待处理的实体变化。
        /// </summary>
        protected override void OnExecute()
        {
            if (_pending.Count == 0) return;

            var buffer = _executionBuffer;
            buffer.Clear();

            foreach (var entity in _pending)
            {
                buffer.Add(entity);
            }
            _pending.Clear();

            if (_executionArray.Length < buffer.Count)
            {
                _executionArray = new TEntity[buffer.Count];
            }

            buffer.CopyTo(_executionArray);
            int count = buffer.Count;

            for (int i = 0; i < count; i++)
            {
                var e = _executionArray[i];
                if (e == null) continue;
                OnEntityChanged(e);
            }
        }

        /// <summary>
        /// 清理阶段：空实现，响应式系统的资源释放在 TearDown 中进行。
        /// </summary>
        protected override void OnCleanup()
        {
        }

        /// <summary>
        /// 销毁阶段：取消所有事件订阅并清理资源。
        /// </summary>
        protected override void OnTearDown()
        {
            if (_group != null)
            {
                _group.OnEntityAdded -= OnGroupEntityAdded;
                _group.OnEntityRemoved -= OnGroupEntityRemoved;

                var entities = _group.GetEntities();
                if (entities != null)
                {
                    for (int i = 0; i < entities.Length; i++)
                    {
                        Untrack(entities[i]);
                    }
                }
            }

            _pending.Clear();
            _handlers.Clear();
        }

        private void OnGroupEntityAdded(IGroup<TEntity> group, TEntity entity, int index, IComponent component)
        {
            TrackAndEnqueue(entity);
        }

        private void OnGroupEntityRemoved(IGroup<TEntity> group, TEntity entity, int index, IComponent component)
        {
            _pending.Remove(entity);
            Untrack(entity);

            if (EnableGroupRemoveDebugLog)
            {
                OnGroupRemoveDebug(group, entity, index, component);
            }
            OnEntityRemovedFromGroup(entity);
        }

        private void TrackAndEnqueue(TEntity entity)
        {
            if (entity == null) return;
            if (_handlers.ContainsKey(entity)) return;

            _pending.Add(entity);

            if (entity is Entity ent)
            {
                EntityComponentReplaced handler = (e, componentIndex, previousComponent, newComponent) =>
                {
                    if (!ShouldReactToReplace(componentIndex)) return;
                    _pending.Add((TEntity)e);
                };
                ent.OnComponentReplaced += handler;
                _handlers[entity] = handler;
            }
        }

        private void Untrack(TEntity entity)
        {
            if (entity == null) return;
            if (!_handlers.TryGetValue(entity, out var handler)) return;

            if (entity is Entity ent)
            {
                ent.OnComponentReplaced -= handler;
            }
            _handlers.Remove(entity);
        }
    }
}