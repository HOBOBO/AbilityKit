#nullable enable

namespace AbilityKit.Samples.Abstractions
{
    /// <summary>
    /// 资源加载器接口
    /// </summary>
    public interface IResourceProvider
    {
        /// <summary>
        /// 加载文本资源
        /// </summary>
        string LoadText(string path);

        /// <summary>
        /// 尝试加载文本资源；资源不存在时返回 false 而不抛异常
        /// </summary>
        bool TryLoadText(string path, out string content);

        /// <summary>
        /// 资源是否存在
        /// </summary>
        bool Exists(string path);
    }
}
