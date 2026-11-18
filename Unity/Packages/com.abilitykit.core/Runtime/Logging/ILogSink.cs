using System;

namespace AbilityKit.Core.Logging
{
    /// <summary>
    /// 接收同步日志调用，由宿主提供具体输出方式。
    /// </summary>
    /// <remarks>
    /// 接口本身不保证线程安全。接收器可能被多个业务线程调用，具体实现须声明并发要求。
    /// 通过 Log 转发时接收器异常会被隔离；直接调用接收器时由调用方处理异常。
    /// </remarks>
    public interface ILogSink
    {
        /// <summary>
        /// 接收普通日志消息。
        /// </summary>
        /// <param name="message">调用方提供的消息文本。</param>
        void Info(string message);

        /// <summary>
        /// 接收警告消息。
        /// </summary>
        /// <param name="message">调用方提供的警告文本。</param>
        void Warning(string message);

        /// <summary>
        /// 接收错误消息。
        /// </summary>
        /// <param name="message">调用方提供的错误文本。</param>
        void Error(string message);

        /// <summary>
        /// 接收业务异常和可选的上下文说明。
        /// </summary>
        /// <param name="exception">需要记录的业务异常。</param>
        /// <param name="message">可选说明；空引用表示未提供额外说明。</param>
        void Exception(Exception exception, string? message = null);
    }
}
