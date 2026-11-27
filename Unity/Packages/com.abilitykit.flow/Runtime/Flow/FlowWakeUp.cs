#nullable enable annotations

namespace AbilityKit.Ability.Flow
{
    /// <summary>
    /// 单次流程运行的唤醒句柄，运行收尾时失效。
    /// </summary>
    public sealed class FlowWakeUp
    {
        private FlowRunner? _runner;

        internal FlowWakeUp(FlowRunner runner)
        {
            _runner = runner;
        }

        /// <summary>
        /// 在调用线程请求推进所属运行；运行结束后无操作，不提供跨线程同步。
        /// </summary>
        public void Wake()
        {
            _runner?.Wake();
        }

        internal void Invalidate()
        {
            _runner = null;
        }
    }
}
