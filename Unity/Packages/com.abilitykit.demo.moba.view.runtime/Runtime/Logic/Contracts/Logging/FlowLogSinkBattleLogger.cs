using System;
using AbilityKit.Core.Logging;

namespace AbilityKit.Game.Battle.Shared.Logging
{
    public sealed class FlowLogSinkBattleLogger : IBattleLogger
    {
        private readonly ILogSink _sink;

        public FlowLogSinkBattleLogger(ILogSink sink)
        {
            _sink = sink;
        }

        public void Info(string message)
        {
            _sink?.Info(message);
        }

        public void Warning(string message)
        {
            _sink?.Warning(message);
        }

        public void Error(string message)
        {
            _sink?.Error(message);
        }

        public void Exception(Exception exception, string message = null)
        {
            _sink?.Exception(exception, message);
        }
    }
}
