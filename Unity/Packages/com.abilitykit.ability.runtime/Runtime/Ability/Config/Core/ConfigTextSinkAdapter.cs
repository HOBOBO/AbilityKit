using System;

namespace AbilityKit.Ability.Config
{
    /// <summary>
    /// 将 IConfigTextSink 适配为 IConfigSource
    /// </summary>
    public sealed class ConfigTextSinkAdapter : IConfigSource
    {
        private readonly IConfigTextSink _sink;

        public ConfigTextSinkAdapter(IConfigTextSink sink)
        {
            _sink = sink ?? throw new ArgumentNullException(nameof(sink));
        }

        public bool TryGetText(string path, out string text)
        {
            return _sink.TryGetText(path, out text);
        }

        public bool TryGetBytes(string path, out byte[] bytes)
        {
            bytes = null;
            return false;
        }
    }
}