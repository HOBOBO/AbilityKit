using System;

namespace AbilityKit.Core.Logging
{
    public interface ILogSink
    {
        void Info(string message);
        void Warning(string message);
        void Error(string message);
        void Exception(Exception exception, string? message = null);
    }
}
