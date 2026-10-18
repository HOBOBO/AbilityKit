using System;

namespace AbilityKit.Game.Battle.Shared.Logging
{
    public sealed class NullBattleLogger : IBattleLogger
    {
        public static readonly NullBattleLogger Instance = new NullBattleLogger();

        private NullBattleLogger()
        {
        }

        public void Info(string message) { }
        public void Warning(string message) { }
        public void Error(string message) { }
        public void Exception(Exception exception, string message = null) { }
    }
}
