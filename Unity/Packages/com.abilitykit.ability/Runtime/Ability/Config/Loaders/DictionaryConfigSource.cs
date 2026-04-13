using System.Collections.Generic;

namespace AbilityKit.Ability.Config
{
    /// <summary>
    /// 字典配置数据源，用于测试或程序化配置
    /// </summary>
    public sealed class DictionaryConfigSource : IConfigSource
    {
        private readonly Dictionary<string, string> _texts;
        private readonly Dictionary<string, byte[]> _bytes;

        public DictionaryConfigSource()
        {
            _texts = new Dictionary<string, string>(System.StringComparer.Ordinal);
            _bytes = new Dictionary<string, byte[]>(System.StringComparer.Ordinal);
        }

        public DictionaryConfigSource(IReadOnlyDictionary<string, string> texts, IReadOnlyDictionary<string, byte[]> bytes = null)
        {
            _texts = texts != null
                ? new Dictionary<string, string>(texts, System.StringComparer.Ordinal)
                : new Dictionary<string, string>(System.StringComparer.Ordinal);
            _bytes = bytes != null
                ? new Dictionary<string, byte[]>(bytes, System.StringComparer.Ordinal)
                : new Dictionary<string, byte[]>(System.StringComparer.Ordinal);
        }

        public bool TryGetText(string path, out string text)
        {
            return _texts.TryGetValue(path, out text);
        }

        public bool TryGetBytes(string path, out byte[] bytes)
        {
            return _bytes.TryGetValue(path, out bytes);
        }

        /// <summary>
        /// 添加文本配置
        /// </summary>
        public void AddText(string path, string text)
        {
            _texts[path] = text;
        }

        /// <summary>
        /// 添加二进制配置
        /// </summary>
        public void AddBytes(string path, byte[] bytes)
        {
            _bytes[path] = bytes;
        }
    }
}
