using System;

namespace AbilityKit.Ability.Share.Common.Log
{
    public interface ILogSink
    {
        void Info(string message);
        void Warning(string message);
        void Error(string message);
        void Exception(Exception exception, string message = null);
    }
}
