using System;
using System.Collections.Generic;

namespace AbilityKit.World.ECS
{
    /// <summary>
    /// 默认的事件总线实现。
    /// 支持强类型事件的发布/订阅，内部使用字典存储订阅者。
    /// </summary>
    public sealed class WorldEventBus : IWorldEventBus
    {
        private readonly object _lock = new object();
        private readonly Dictionary<Type, List<Delegate>> _handlers = new Dictionary<Type, List<Delegate>>();

        #region 生命周期事件

        public IDisposable OnEntityCreated(Action<EntityCreated> handler)
        {
            return Subscribe(handler);
        }

        public IDisposable OnEntityDestroyed(Action<EntityDestroyed> handler)
        {
            return Subscribe(handler);
        }

        #endregion

        #region 组件事件

        public IDisposable OnComponentSet(Action<ComponentSet> handler)
        {
            return Subscribe(handler);
        }

        public IDisposable OnComponentRemoved(Action<ComponentRemoved> handler)
        {
            return Subscribe(handler);
        }

        #endregion

        #region 层级事件

        public IDisposable OnParentChanged(Action<ParentChanged> handler)
        {
            return Subscribe(handler);
        }

        #endregion

        #region 通用事件

        public void Publish<TEvent>(TEvent evt) where TEvent : struct
        {
            var type = typeof(TEvent);
            List<Delegate> handlers;

            lock (_lock)
            {
                if (!_handlers.TryGetValue(type, out handlers) || handlers.Count == 0)
                    return;
                handlers = new List<Delegate>(handlers);
            }

            foreach (var handler in handlers)
            {
                ((Action<TEvent>)handler)(evt);
            }
        }

        public IDisposable Subscribe<TEvent>(Action<TEvent> handler) where TEvent : struct
        {
            var type = typeof(TEvent);

            lock (_lock)
            {
                if (!_handlers.TryGetValue(type, out var handlers))
                {
                    handlers = new List<Delegate>();
                    _handlers[type] = handlers;
                }
                handlers.Add(handler);
            }

            return new EventSubscription<TEvent>(this, handler);
        }

        public int GetSubscriberCount<TEvent>() where TEvent : struct
        {
            var type = typeof(TEvent);
            lock (_lock)
            {
                if (!_handlers.TryGetValue(type, out var handlers))
                    return 0;
                return handlers.Count;
            }
        }

        public void Clear()
        {
            lock (_lock)
            {
                _handlers.Clear();
            }
        }

        private void Unsubscribe<TEvent>(Action<TEvent> handler) where TEvent : struct
        {
            var type = typeof(TEvent);
            lock (_lock)
            {
                if (_handlers.TryGetValue(type, out var handlers))
                {
                    handlers.Remove(handler);
                }
            }
        }

        #endregion

        #region 内部类型

        private sealed class EventSubscription<TEvent> : IDisposable
            where TEvent : struct
        {
            private readonly WorldEventBus _bus;
            private readonly Action<TEvent> _handler;
            private bool _disposed;

            public EventSubscription(WorldEventBus bus, Action<TEvent> handler)
            {
                _bus = bus;
                _handler = handler;
            }

            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                _bus.Unsubscribe(_handler);
            }
        }

        #endregion
    }
}
