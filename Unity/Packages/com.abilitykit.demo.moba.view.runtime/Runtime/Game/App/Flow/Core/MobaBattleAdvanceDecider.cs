#nullable enable
namespace AbilityKit.Game.Flow
{
    /// <summary>
    /// 战斗阶段「推进决策表」：给定当前 battle 状态与收到的信号 / runtime 标志，
    /// 决定应触发哪个 <see cref="MobaBattleEvent"/>（返回 null 表示不触发）。
    /// 纯逻辑、无副作用——不依赖 AbilityKit.HFSM、scope 或 Unity。
    /// 副作用（写 runtime 状态、日志、StateMachine.Trigger）保留在 GameFlowDomain。
    /// </summary>
    internal sealed class MobaBattleAdvanceDecider
    {
        /// <summary>收到 SessionStarted 信号：Prepare→PrepareDone；Connect→Connected；其余不推进。</summary>
        public MobaBattleEvent? OnSessionStarted(MobaBattleState current)
        {
            return current switch
            {
                MobaBattleState.Prepare => MobaBattleEvent.PrepareDone,
                MobaBattleState.Connect => MobaBattleEvent.Connected,
                _ => null
            };
        }

        /// <summary>世界资源就绪：仅在 CreateOrJoinWorld 推进为 JoinedWorld。</summary>
        public MobaBattleEvent? OnWorldReady(MobaBattleState current)
        {
            return current == MobaBattleState.CreateOrJoinWorld
                ? MobaBattleEvent.JoinedWorld
                : (MobaBattleEvent?)null;
        }

        /// <summary>
        /// 收到 FirstFrameReceived 信号：Prepare→PrepareDone；Connect→Connected；
        /// CreateOrJoinWorld→JoinedWorld；其余不推进。
        /// 阶段 7a：LoadAssets 不再因首帧推进——真实资源加载完成由 <see cref="OnAssetsLoadCompleted"/> 驱动。
        /// </summary>
        public MobaBattleEvent? OnFirstFrameReceived(MobaBattleState current)
        {
            return current switch
            {
                MobaBattleState.Prepare => MobaBattleEvent.PrepareDone,
                MobaBattleState.Connect => MobaBattleEvent.Connected,
                MobaBattleState.CreateOrJoinWorld => MobaBattleEvent.JoinedWorld,
                _ => null
            };
        }

        /// <summary>
        /// 真实资源加载完成信号（manifest barrier）：仅在 LoadAssets 状态推进为 AssetsLoadCompleted；
        /// 其余状态不推进。首帧不再代表资源加载完成。
        /// </summary>
        public MobaBattleEvent? OnAssetsLoadCompleted(MobaBattleState current)
        {
            return current == MobaBattleState.LoadAssets
                ? MobaBattleEvent.AssetsLoadCompleted
                : (MobaBattleEvent?)null;
        }

        /// <summary>收到 SessionFailed 信号：非 End 态一律 Ended；已在 End 不重复推进。</summary>
        public MobaBattleEvent? OnSessionFailed(MobaBattleState current)
        {
            return current != MobaBattleState.End ? MobaBattleEvent.Ended : (MobaBattleEvent?)null;
        }

        /// <summary>
        /// 进入某状态时按 runtime 标志补判（覆盖原 TryAdvanceOnConnectEnter /
        /// TryAdvanceOnCreateOrJoinWorldEnter / TryAdvanceOnLoadAssetsEnter）：
        /// Connect 看 SessionStarted‖FirstFrameReceived→Connected；
        /// CreateOrJoinWorld 看 WorldReady→JoinedWorld；
        /// 其余状态不补判。
        /// 阶段 7a：LoadAssets 不再因 firstFrameReceived 自动推进——资源加载完成由
        /// <see cref="OnAssetsLoadCompleted"/> 驱动（真实 manifest barrier）。
        /// </summary>
        public MobaBattleEvent? OnStateEntered(
            MobaBattleState current,
            bool sessionStarted,
            bool worldReady,
            bool firstFrameReceived,
            bool assetsLoadCompleted = false)
        {
            return current switch
            {
                MobaBattleState.Connect when sessionStarted || firstFrameReceived => MobaBattleEvent.Connected,
                MobaBattleState.CreateOrJoinWorld when worldReady => MobaBattleEvent.JoinedWorld,
                MobaBattleState.LoadAssets when assetsLoadCompleted => MobaBattleEvent.AssetsLoadCompleted,
                _ => null
            };
        }
    }
}
