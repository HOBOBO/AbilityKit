namespace AbilityKit.Core.Pooling
{
    /// <summary>
    /// 包级对象池配置提供者，每个包可以向中心注册一个或多个提供者。
    /// </summary>
    public interface IPoolConfigProvider
    {
        /// <summary>
        /// 尝试提供请求对应的配置；配置中心仅接受返回 true 且已指定的配置，异常直接传播。
        /// </summary>
        bool TryGetConfig(PoolConfigRequest request, out PoolItemConfig config);
    }
}
