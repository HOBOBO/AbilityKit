using System;

namespace AbilityKit.Core.Pooling
{
    /// <summary>
    /// 按提供者引用身份执行注销的值类型句柄，不提供引用计数或独立的注册代次隔离。
    /// </summary>
    /// <remarks>
    /// 句柄及其副本注销同一提供者；提供者被重新注册后，旧句柄仍可移除新登记。调用方需协调注册生命周期。
    /// </remarks>
    public readonly struct PoolConfigRegistration : IDisposable
    {
        private readonly IPoolConfigProvider _provider;

        /// <summary>
        /// 创建配置提供者注册句柄。
        /// </summary>
        /// <param name="provider">已注册的配置提供者。</param>
        /// <param name="info">注册时生成的诊断信息。</param>
        public PoolConfigRegistration(IPoolConfigProvider provider, PoolConfigProviderInfo info)
        {
            _provider = provider;
            Info = info;
        }

        /// <summary>
        /// 获取注册时生成的配置提供者诊断信息。
        /// </summary>
        public PoolConfigProviderInfo Info { get; }

        /// <summary>
        /// 获取该句柄是否绑定了非空提供者；不代表提供者当前仍在配置中心注册。
        /// </summary>
        public bool IsValid => _provider != null;

        /// <summary>
        /// 注销该句柄对应的配置提供者。
        /// </summary>
        /// <returns>如果配置提供者仍在配置中心并被成功移除，则返回 <c>true</c>。</returns>
        public bool Unregister()
        {
            return _provider != null && PoolConfigCenter.UnregisterProvider(_provider);
        }

        /// <summary>
        /// 注销该句柄对应的配置提供者。
        /// </summary>
        public void Dispose()
        {
            Unregister();
        }
    }
}
