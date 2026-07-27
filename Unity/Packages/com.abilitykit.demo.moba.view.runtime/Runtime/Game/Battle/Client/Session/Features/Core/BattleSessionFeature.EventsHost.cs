using System;
using AbilityKit.Core.Logging;
using AbilityKit.Protocol.Room;
using AbilityKit.Core.Recording.FrameRecord;
using AbilityKit.Game.Battle;
using AbilityKit.Game.Flow.Battle.Modules;

namespace AbilityKit.Game.Flow
{
    public sealed partial class BattleSessionFeature
    {
        void ISessionEventsHost.OnStartSessionRequested() => OnStartSessionRequested();

        void ISessionEventsHost.RaiseSessionStarted(BattleStartPlan plan)
        {
            Log.Info("[BattleSessionFeature] Session started");
            SessionStarted?.Invoke();
            Hooks?.SessionStarted.Invoke(plan);
        }

        void ISessionEventsHost.RaiseSessionFailed(Exception exception)
        {
            SessionFailed?.Invoke(exception);
            Hooks?.SessionFailed.Invoke(exception);
        }

        void ISessionEventsHost.RaiseFirstFrameReceived()
        {
            FirstFrameReceived?.Invoke();
            Hooks?.FirstFrameReceived.Invoke();
        }

        Exception ISessionEventsHost.PendingSubFeatureValidationFailure
        {
            get => _pendingSubFeatureValidationFailure;
            set => _pendingSubFeatureValidationFailure = value;
        }

        BattleSessionHooks ISessionEventsHost.Hooks
        {
            get => Hooks;
            set => Hooks = value;
        }

        internal BattleSessionHooks Hooks { get; private set; }

        public event Action SessionStarted;
        public event Action FirstFrameReceived;
        public event Action<Exception> SessionFailed;
        public event Action<WireReliableBattleEvent> ReliableBattleEventReceived;

        // 阶段 7a：真实资源加载完成事件（manifest barrier）。
        public event Action AssetsLoadCompleted;

        /// <summary>
        /// 阶段 7a：外部资源加载协调器在 manifest barrier 通过后调用，触发 <see cref="AssetsLoadCompleted"/>。
        /// </summary>
        internal void NotifyAssetsLoadCompleted()
        {
            Log.Info("[BattleSessionFeature] Assets load completed (manifest barrier)");
            AssetsLoadCompleted?.Invoke();
        }
    }
}
