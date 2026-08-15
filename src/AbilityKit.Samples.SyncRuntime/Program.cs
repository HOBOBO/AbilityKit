using System;
using AbilityKit.Core.Logging;

namespace AbilityKit.Samples.SyncRuntime;

internal static class Program
{
    private static void Main()
    {
        Log.SetSink(new ConsoleLogSink());

        Log.Info("=== AbilityKit SyncRuntime Starter（framesync + statesync + record）===");
        Log.Info("演示：帧驱动 → 状态快照/哈希采样 → 录制 → 回放重算 → DiffAnalyzer 验证确定性\n");

        SyncRuntimeDemo.Run();

        Log.Info("\n=== Starter 完成 ===");
    }

    private sealed class ConsoleLogSink : ILogSink
    {
        public void Info(string message) => Console.WriteLine($"[INFO ] {message}");

        public void Warning(string message) => Console.WriteLine($"[WARN ] {message}");

        public void Error(string message) => Console.WriteLine($"[ERROR] {message}");

        public void Exception(Exception exception, string message = null!)
            => Console.WriteLine($"[EXCPT] {message} {exception}");
    }
}
