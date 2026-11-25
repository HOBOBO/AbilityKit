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

    /// <summary>
    /// 为实时读取提供一个可读取的属性对象；引用校验由解析器负责。
    /// </summary>
    public interface IContextRealtimeValueProvider
    {
        /// <summary>
        /// 查找当前绑定的属性，不创建上下文实体。
        /// </summary>
        bool TryGetProperty(long contextId, out IProperty property);
    }
}
