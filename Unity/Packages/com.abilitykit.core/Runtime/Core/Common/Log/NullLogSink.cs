using System;

namespace AbilityKit.Core.Common.Log
{
    public sealed class NullLogSink : ILogSink
    {
        public static readonly NullLogSink Instance = new NullLogSink();

        private NullLogSink() { }

        public void Info(string message) { }
        public void Warning(string message) { }
        public void Error(string message) { }
        public void Exception(Exception exception, string message = null) { }
    }
}
