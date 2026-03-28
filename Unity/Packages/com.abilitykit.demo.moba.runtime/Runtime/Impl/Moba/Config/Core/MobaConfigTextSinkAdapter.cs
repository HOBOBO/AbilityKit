using System;

namespace AbilityKit.Ability.Impl.BattleDemo.Moba.Config.Core
{
    public sealed class MobaConfigTextSinkAdapter : IMobaConfigSource
    {
        private readonly IMobaConfigTextSink _sink;

        public MobaConfigTextSinkAdapter(IMobaConfigTextSink sink)
        {
            _sink = sink ?? throw new ArgumentNullException(nameof(sink));
        }

        public bool TryGetText(string key, out string text)
        {
            return _sink.TryGetText(key, out text);
        }
    }
}
