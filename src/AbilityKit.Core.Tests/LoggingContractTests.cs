using AbilityKit.Core.Logging;
using Xunit;

namespace AbilityKit.Core.Tests;

/*
 * 日志门面持有进程静态状态；该集合禁止并行执行，测试结束后恢复原接收器。
 * 验证消息路由和失败隔离，避免整理实现时改变调用方可观察的行为。
 */
[CollectionDefinition("Core 日志静态状态", DisableParallelization = true)]
public sealed class LoggingContractCollection
{
}

[Collection("Core 日志静态状态")]
public sealed class LoggingContractTests
{
    [Fact]
    public void Messages_preserve_payload_and_trace_uses_info_channel()
    {
        var original = Log.Sink;
        var sink = new RecordingSink();
        var exception = new InvalidOperationException("业务异常");
        try
        {
            Log.SetSink(sink);
            Log.Info("普通消息");
            Log.Trace("跟踪消息");
            Log.Warning("警告消息");
            Log.Error("错误消息");
            Log.Exception(exception, "上下文");

            Assert.Same(sink, Log.Sink);
            Assert.Equal(new[]
            {
                ("普通", "普通消息"),
                ("普通", "跟踪消息"),
                ("警告", "警告消息"),
                ("错误", "错误消息"),
                ("异常", "上下文"),
            }, sink.Messages);
            Assert.Same(exception, sink.ExceptionValue);
        }
        finally
        {
            Log.SetSink(original);
        }
    }

    [Fact]
    public void Sink_failures_do_not_escape_or_trigger_recursive_logging()
    {
        var original = Log.Sink;
        var sink = new ThrowingSink();
        try
        {
            Log.SetSink(sink);
            Log.Info("普通消息");
            Log.Trace("跟踪消息");
            Log.Warning("警告消息");
            Log.Error("错误消息");
            Log.Exception(new InvalidOperationException("业务异常"));

            Assert.Equal(5, sink.CallCount);
            Assert.Same(sink, Log.Sink);
        }
        finally
        {
            Log.SetSink(original);
        }
    }

    [Fact]
    public void Null_configuration_restores_the_shared_disabled_sink()
    {
        var original = Log.Sink;
        try
        {
            Log.SetSink(new RecordingSink());
            Log.SetSink(null!);
            Assert.Same(NullLogSink.Instance, Log.Sink);
            Log.Info("丢弃消息");
            Log.Exception(new InvalidOperationException("丢弃异常"));
        }
        finally
        {
            Log.SetSink(original);
        }
    }

    private sealed class RecordingSink : ILogSink
    {
        public List<(string Channel, string Message)> Messages { get; } = new();
        public Exception? ExceptionValue { get; private set; }

        public void Info(string message) => Messages.Add(("普通", message));
        public void Warning(string message) => Messages.Add(("警告", message));
        public void Error(string message) => Messages.Add(("错误", message));
        public void Exception(Exception exception, string? message = null)
        {
            ExceptionValue = exception;
            Messages.Add(("异常", message ?? string.Empty));
        }
    }

    private sealed class ThrowingSink : ILogSink
    {
        public int CallCount { get; private set; }

        public void Info(string message) => Fail();
        public void Warning(string message) => Fail();
        public void Error(string message) => Fail();
        public void Exception(Exception exception, string? message = null) => Fail();

        private void Fail()
        {
            CallCount++;
            throw new InvalidOperationException("接收器故障");
        }
    }
}
