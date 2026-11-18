using System;

namespace AbilityKit.Core.Logging
{
    /// <summary>
    /// 向进程当前日志接收器转发消息，并隔离接收器抛出的异常。
    /// </summary>
    /// <remarks>
    /// 接收器配置应在启动阶段完成；并发切换和接收器自身的线程安全由调用方负责。
    /// 日志失败不会中断业务，不会再次写日志或尝试递归报告失败。
    /// </remarks>
    public static class Log
    {
        private static ILogSink _sink = NullLogSink.Instance;

        /// <summary>
        /// 获取当前进程使用的日志接收器。
        /// </summary>
        public static ILogSink Sink => _sink;

        /// <summary>
        /// 设置进程日志接收器；传入空引用时恢复为不保留消息的空接收器。
        /// </summary>
        public static void SetSink(ILogSink sink)
        {
            _sink = sink ?? NullLogSink.Instance;
        }

        /// <summary>
        /// 转发普通消息，隔离接收器异常。
        /// </summary>
        public static void Info(string message)
        {
            try
            {
                _sink.Info(message);
            }
            catch
            {
                /*
                 * 日志输出是尽力而为的旁路，失败不得改变业务执行结果。
                 */
            }
        }

        /// <summary>
        /// 将跟踪消息转发到普通消息入口，保持现有日志接口的兼容行为。
        /// </summary>
        public static void Trace(string message)
        {
            Info(message);
        }

        /// <summary>
        /// 转发警告消息，隔离接收器异常。
        /// </summary>
        public static void Warning(string message)
        {
            try
            {
                _sink.Warning(message);
            }
            catch
            {
                /*
                 * 日志失败不能向调用方传播，也不能递归调用日志接收器。
                 */
            }
        }

        /// <summary>
        /// 转发错误消息，隔离接收器异常。
        /// </summary>
        public static void Error(string message)
        {
            try
            {
                _sink.Error(message);
            }
            catch
            {
                /*
                 * 错误消息输出失败仍遵守日志旁路的异常隔离契约。
                 */
            }
        }

        /// <summary>
        /// 转发异常及可选说明，隔离接收器自身的失败。
        /// </summary>
        public static void Exception(Exception exception, string? message = null)
        {
            try
            {
                _sink.Exception(exception, message);
            }
            catch
            {
                /*
                 * 原始异常由业务所有者处理，此处不让诊断失败覆盖业务结果。
                 */
            }
        }
    }
}
