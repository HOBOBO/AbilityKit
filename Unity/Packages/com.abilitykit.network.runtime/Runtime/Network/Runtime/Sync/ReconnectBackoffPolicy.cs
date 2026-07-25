namespace AbilityKit.Network.Runtime.Sync
{
    /// <summary>
    /// 断线重连的退避策略（纯函数，无会话依赖）。
    /// 指数退避：1s、2s、4s、8s……封顶 15s。
    ///
    /// 框架级共享策略：MOBA（BattleSessionFeature.Reconnect）与
    /// Shooter（FastReconnect 路径）等 demo 统一使用，
    /// 避免每个示例各自实现一套重连节奏。
    /// </summary>
    public static class ReconnectBackoffPolicy
    {
        public const int MaxAttempts = 10;
        public const float BaseDelaySeconds = 1f;
        public const float MaxDelaySeconds = 15f;

        public static float ResolveDelay(int attempts)
        {
            if (attempts < 0) attempts = 0;
            var delay = BaseDelaySeconds * (1 << attempts);
            return delay > MaxDelaySeconds ? MaxDelaySeconds : delay;
        }
    }
}
