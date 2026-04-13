namespace AbilityKit.Ability.Config
{
    /// <summary>
    /// 二进制配置数据源接口
    /// </summary>
    public interface IConfigBytesSource
    {
        /// <summary>
        /// 尝试获取指定键的二进制数据
        /// </summary>
        bool TryGetBytes(string key, out byte[] bytes);
    }
}