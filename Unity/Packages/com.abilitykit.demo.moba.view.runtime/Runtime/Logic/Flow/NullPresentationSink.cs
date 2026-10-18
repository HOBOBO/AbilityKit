namespace AbilityKit.Game.Flow
{
    /// <summary>
    /// <see cref="IPresentationSink"/> 的空实现（Null Object 模式）。
    /// 当构造函数未提供 presentationSink 时用作默认值，避免 null 检查散落各处。
    /// </summary>
    internal sealed class NullPresentationSink : IPresentationSink
    {
        public static readonly NullPresentationSink Instance = new NullPresentationSink();

        private NullPresentationSink() { }

        public void OnPhaseChanged(MobaRootState root, MobaBattleState battle) { }
        public void OnBattleStart() { }
        public void OnBattleEnd() { }
        public void OnError(string message) { }
    }
}
