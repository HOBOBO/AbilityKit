namespace AbilityKit.Ability.Host.Extensions.FrameSync
{
    /// <summary>
    /// 单次世界推进使用的配置值，避免阶段控制器依赖宿主模块或增加委托分配。
    /// </summary>
    internal readonly struct ClientPredictionTickSettings
    {
        public readonly int InputDelayFrames;
        public readonly int MinPredictionWindow;
        public readonly int MaxPredictionAheadFrames;
        public readonly float BacklogEwmaAlpha;
        public readonly bool EnableRollback;
        public readonly int MaxLocalDelayQueueDepth;

        public ClientPredictionTickSettings(int inputDelayFrames, int minPredictionWindow,
            int maxPredictionAheadFrames, float backlogEwmaAlpha, bool enableRollback, int maxLocalDelayQueueDepth)
        {
            InputDelayFrames = inputDelayFrames;
            MinPredictionWindow = minPredictionWindow;
            MaxPredictionAheadFrames = maxPredictionAheadFrames;
            BacklogEwmaAlpha = backlogEwmaAlpha;
            EnableRollback = enableRollback;
            MaxLocalDelayQueueDepth = maxLocalDelayQueueDepth;
        }
    }
}
