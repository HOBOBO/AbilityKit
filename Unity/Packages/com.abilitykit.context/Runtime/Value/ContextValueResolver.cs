using System;

namespace AbilityKit.Context
{
    /// <summary>
    /// 上下文的统一读取入口。
    /// 只按精确引用读取，不跨来源回退或用默认值表示命中。
    /// </summary>
    public sealed class ContextValueResolver
    {
        private readonly ContextRegistry _registry;
        private readonly IContextSnapshotReader _snapshots;
        private readonly ContextRealtimeProviderRegistry _realtimeProviders;

        /// <summary>
        /// 装配实时注册表、可选的快照读取器和外部属性提供者。
        /// </summary>
        public ContextValueResolver(ContextRegistry registry, IContextSnapshotReader snapshots = null, ContextRealtimeProviderRegistry realtimeProviders = null)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _snapshots = snapshots;
            _realtimeProviders = realtimeProviders;
        }

        /// <summary>
        /// 按带代际的引用读取实时属性，不读取快照或默认值。
        /// </summary>
        public ContextRealtimeValue<TProperty> ReadRealtimeProperty<TProperty>(in ContextEntityReference reference)
            where TProperty : class, IProperty
        {
            var status = _registry.Query(reference);
            if (status != ContextEntityQueryStatus.Found)
                return new ContextRealtimeValue<TProperty>(status, reference, null);
            var type = PropertyTypeRegistry.Instance.Get<TProperty>();
            if (type == null)
                return new ContextRealtimeValue<TProperty>(ContextEntityQueryStatus.PropertyMissing, reference, null);

            TProperty property = null;
            if (_realtimeProviders == null || !_realtimeProviders.TryGetProperty(reference.EntityId, type.Id, out property))
            {
                status = _registry.ReadProperty(reference, type.Id, out var raw);
                if (status != ContextEntityQueryStatus.Found)
                    return new ContextRealtimeValue<TProperty>(status, reference, null);
                property = raw as TProperty;
            }
            status = _registry.Query(reference);
            if (status == ContextEntityQueryStatus.Found && property == null)
                status = ContextEntityQueryStatus.PropertyMissing;
            return new ContextRealtimeValue<TProperty>(status, reference,
                status == ContextEntityQueryStatus.Found ? property : null);
        }

        /// <summary>
        /// 从同一个已解析的实时属性读取字段，并在读取后重新确认引用。
        /// </summary>
        public ContextRealtimeValue<TValue> ReadRealtime<TValue, TProperty>(in ContextEntityReference reference, string key)
            where TProperty : class, IProperty
        {
            var property = ReadRealtimeProperty<TProperty>(reference);
            if (!property.Found)
                return new ContextRealtimeValue<TValue>(property.Status, reference, default);
            var value = default(TValue);
            var found = !string.IsNullOrEmpty(key) && property.Value is IContextValueProvider provider && provider.TryGetValue(key, out value);
            var status = _registry.Query(reference);
            return new ContextRealtimeValue<TValue>(status == ContextEntityQueryStatus.Found
                ? (found ? ContextEntityQueryStatus.Found : ContextEntityQueryStatus.FieldMissing) : status,
                reference, found && status == ContextEntityQueryStatus.Found ? value : default);
        }

        /// <summary>
        /// 按精确引用读取指定快照，不回退到最新记录或实时值。
        /// </summary>
        public ContextSnapshotValue<TValue> ReadSnapshot<TSnapshot, TValue>(in ContextSnapshotReference reference, string key)
            where TSnapshot : IImmutableContextSnapshot
        {
            var reader = _snapshots;
            return reader != null
                ? reader.ReadSnapshot<TSnapshot, TValue>(reference, key)
                : new ContextSnapshotValue<TValue>(ContextSnapshotQueryStatus.Unavailable, default, default);
        }
    }
}
