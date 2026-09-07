using System;
using System.Collections.Generic;

namespace AbilityKit.BehaviorTree.Execution
{
    /// <summary>按类型字典实现的默认服务解析�?/summary>
    public sealed class DefaultServiceResolver : ServiceResolver
    {
        private readonly Dictionary<Type, object> _services = new();

        public DefaultServiceResolver Add<T>(T service) where T : class
        {
            _services[typeof(T)] = service!;
            return this;
        }

        public T Resolve<T>() where T : class
        {
            if (!TryResolve<T>(out var service))
                throw new InvalidOperationException($"BT service '{typeof(T).Name}' is not registered.");
            return service;
        }

        public bool TryResolve<T>(out T service) where T : class
        {
            if (_services.TryGetValue(typeof(T), out var obj) && obj is T typed)
            {
                service = typed;
                return true;
            }

            service = null!;
            return false;
        }
    }
}
