using System.Collections.Generic;

namespace AbilityKit.Ability.Config
{
    /// <summary>
    /// 配置文本接收器接口，用于接收配置文本数据
    /// </summary>
    public interface IConfigTextSink
    {
        /// <summary>
        /// 尝试获取指定键的文本数据
        /// </summary>
        bool TryGetText(string key, out string text);
    }

    /// <summary>
    /// 基于字典的配置文本接收器
    /// </summary>
    public sealed class DictionaryConfigTextSink : IConfigTextSink
    {
        private readonly IReadOnlyDictionary<string, string> _texts;

        public DictionaryConfigTextSink(IReadOnlyDictionary<string, string> texts)
        {
            _texts = texts ?? throw new System.ArgumentNullException(nameof(texts));
        }

        public bool TryGetText(string key, out string text)
        {
            text = null;
            if (_texts == null) return false;
            return _texts.TryGetValue(key, out text);
        }
    }
}