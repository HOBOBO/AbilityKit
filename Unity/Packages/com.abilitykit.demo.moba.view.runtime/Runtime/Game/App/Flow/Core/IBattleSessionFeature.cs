using System;

namespace AbilityKit.Game.Flow
{
    public interface IBattleSessionFeature : IGamePhaseFeature
    {
        event Action SessionStarted;
        // 会话所需世界资源已建立。Lockstep 在远端驱动世界安装完成后触发；
        // SnapshotAuthority 由首个权威状态帧隐式满足。
        event Action WorldReady;
        event Action FirstFrameReceived;
        event Action<Exception> SessionFailed;
        // 阶段 7a：真实资源加载完成信号（manifest barrier）。append-only。
        event Action AssetsLoadCompleted;
    }
}
