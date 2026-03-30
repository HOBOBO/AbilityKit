using System;

namespace AbilityKit.Ability.Impl.BattleDemo.Moba.Config.Core
{
    /// <summary>
    /// 将 IMobaConfigTextSink 适配为 IMobaConfigSource
    /// </summary>
    public sealed class MobaConfigSourceAdapter : IMobaConfigSource
    {
        private readonly IMobaConfigTextSink _sink;

        public MobaConfigSourceAdapter(IMobaConfigTextSink sink)
        {
            _sink = sink ?? throw new ArgumentNullException(nameof(sink));
        }

        public bool TryGetText(string key, out string text)
        {
            return _sink.TryGetText(key, out text);
        }
    }
}