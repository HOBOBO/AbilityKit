using System;

namespace AbilityKit.Game.Battle.Shared.Logging
{
    /// <summary>
    /// 战斗 Runtime 统一日志入口。用于隔离 Unity Debug、Core Log 与后续线上日志采集实现。
    /// </summary>
    public interface IBattleLogger
    {
        void Info(string message);
        void Warning(string message);
        void Error(string message);
        void Exception(Exception exception, string message = null);
    }
}
