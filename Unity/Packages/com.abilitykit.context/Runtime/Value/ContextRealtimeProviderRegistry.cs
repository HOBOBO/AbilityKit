using System;
using System.Collections.Generic;

namespace AbilityKit.Context
{
    /// <summary>
    /// 上下文 ID 的运行时值提供器注册表。
    /// 提供器会先于已存储属性被使用，使系统可以暴露实时数据，而无需镜像写入 ContextRegistry。
    /// </summary>
    public sealed class ContextRealtimeProviderRegistry
    {
        private readonly Dictionary<int, IContextRealtimeValueProvider> _providersByPropertyType = new Dictionary<int, IContextRealtimeValueProvider>();
        private readonly object _lock = new object();

        public void Register<TProperty>(IContextRealtimeValueProvider provider)
            where TProperty : IProperty
        {
            if (provider == null) throw new ArgumentNullException(nameof(provider));
            var type = PropertyTypeRegistry.Instance.Get<TProperty>() ?? PropertyTypeRegistry.Instance.Register<TProperty>();
            Register(type.Id, provider);
        }

        public void Register(int propertyTypeId, IContextRealtimeValueProvider provider)
        {
            if (propertyTypeId <= 0) throw new ArgumentOutOfRangeException(nameof(propertyTypeId));
            if (provider == null) throw new ArgumentNullException(nameof(provider));

            lock (_lock)
                _providersByPropertyType[propertyTypeId] = provider;
        }

        public bool Unregister<TProperty>() where TProperty : IProperty
        {
            var type = PropertyTypeRegistry.Instance.Get<TProperty>();
            return type != null && Unregister(type.Id);
        }

        public bool Unregister(int propertyTypeId)
        {
            lock (_lock)
                return _providersByPropertyType.Remove(propertyTypeId);
        }

        public bool TryGetProperty<TProperty>(long contextId, int propertyTypeId, out TProperty property)
            where TProperty : class, IProperty
        {
            property = null;
            if (TryGetProvider(propertyTypeId, out var provider) && provider.TryGetProperty(contextId, out var raw) && raw is TProperty typed)
            {
                property = typed;
                return true;
            }

            return false;
        }

        public bool TryGetValue<T>(in ContextValueRequest request, out T value)
        {
            value = default;
            return TryGetProvider(request.PropertyTypeId, out var provider) && provider.TryGetValue(request.ContextId, request.Key, out value);
        }

        /// <summary>Captures one provider instance for a multi-field compatibility read; does not create context data.</summary>
        public bool TryGetValueReader<TProperty>(long contextId, out IContextValueProvider reader)
            where TProperty : IProperty
        {
            reader = null;
            var type = PropertyTypeRegistry.Instance.Get<TProperty>();
            if (type == null || !TryGetProvider(type.Id, out var provider)) return false;
            reader = new RealtimeValueReader(provider, contextId);
            return true;
        }

        private sealed class RealtimeValueReader : IContextValueProvider
        {
            private readonly IContextRealtimeValueProvider _provider;
            private readonly long _contextId;

            internal RealtimeValueReader(IContextRealtimeValueProvider provider, long contextId)
            {
                _provider = provider;
                _contextId = contextId;
            }

            public bool TryGetValue<T>(string key, out T value) => _provider.TryGetValue(_contextId, key, out value);
        }

        public void Clear()
        {
            lock (_lock)
                _providersByPropertyType.Clear();
        }

        private bool TryGetProvider(int propertyTypeId, out IContextRealtimeValueProvider provider)
        {
            lock (_lock)
                return _providersByPropertyType.TryGetValue(propertyTypeId, out provider);
        }
    }

    public interface IContextRealtimeValueProvider
    {
        bool TryGetProperty(long contextId, out IProperty property);
        bool TryGetValue<T>(long contextId, string key, out T value);
    }
}
