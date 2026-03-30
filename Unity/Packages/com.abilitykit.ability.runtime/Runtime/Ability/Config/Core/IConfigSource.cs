using System;

namespace AbilityKit.Ability.Config
{
    /// <summary>
    /// 配置数据源接口，从各种来源获取原始配置数据
    /// </summary>
    public interface IConfigSource
    {
        /// <summary>
        /// 尝试获取指定路径的文本数据
        /// </summary>
        bool TryGetText(string path, out string text);

        /// <summary>
        /// 尝试获取指定路径的二进制数据
        /// </summary>
        bool TryGetBytes(string path, out byte[] bytes);
    }

    /// <summary>
    /// 配置文本源接口（轻量版，仅支持文本）
    /// </summary>
    public interface IConfigTextSource
    {
        /// <summary>
        /// 尝试获取指定路径的文本数据
        /// </summary>
        bool TryGetText(string path, out string text);
    }

    /// <summary>
    /// 空配置源，总是返回失败
    /// </summary>
    public sealed class EmptyConfigSource : IConfigSource
    {
        public static readonly EmptyConfigSource Instance = new EmptyConfigSource();

        private EmptyConfigSource() { }

        public bool TryGetText(string path, out string text)
        {
            text = null;
            return false;
        }

        public bool TryGetBytes(string path, out byte[] bytes)
        {
            bytes = null;
            return false;
        }
    }
}
