namespace AbilityKit.Context
{
    /// <summary>
    /// 按键提供上下文值的接口。
    /// 属性或快照实现该接口后，可被上下文值解析器统一读取。
    /// </summary>
    public interface IContextValueProvider
    {
        /// <summary>
        /// 读取指定类型的字段；字段缺失或类型不匹配时返回 false。
        /// </summary>
        bool TryGetValue<T>(string key, out T value);
    }
}
