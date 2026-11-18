using System;

namespace AbilityKit.Core.Logging
{
    /// <summary>
    /// 丢弃所有日志的空接收器，作为未配置输出时的默认实现。
    /// </summary>
    /// <remarks>
    /// 不保留消息或异常引用，不产生输出；没有可变实例状态，可以并发复用。
    /// </remarks>
    public sealed class NullLogSink : ILogSink
    {
        /// <summary>
        /// 获取供进程复用的空接收器。
        /// </summary>
        public static readonly NullLogSink Instance = new NullLogSink();

        private NullLogSink() { }

        /// <summary>
        /// 丢弃普通日志，不保留消息引用。
        /// </summary>
        public void Info(string message) { }

        /// <summary>
        /// 丢弃警告日志，不保留消息引用。
        /// </summary>
        public void Warning(string message) { }

        /// <summary>
        /// 丢弃错误日志，不保留消息引用。
        /// </summary>
        public void Error(string message) { }

        /// <summary>
        /// 丢弃异常及其说明，不执行异常处理或保留引用。
        /// </summary>
        public void Exception(Exception exception, string? message = null) { }
    }
}
