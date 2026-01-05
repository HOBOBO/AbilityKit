using System;

namespace AbilityKit.Ability.Share.Common.Log
{
    public static class Log
    {
        private static ILogSink _sink = NullLogSink.Instance;

        public static ILogSink Sink => _sink;

        public static void SetSink(ILogSink sink)
        {
            _sink = sink ?? NullLogSink.Instance;
        }

        public static void Info(string message)
        {
            try { _sink.Info(message); } catch { }
        }

        public static void Warning(string message)
        {
            try { _sink.Warning(message); } catch { }
        }

        public static void Error(string message)
        {
            try { _sink.Error(message); } catch { }
        }

        public static void Exception(Exception exception, string message = null)
        {
            try { _sink.Exception(exception, message); } catch { }
        }
    }
}
