using System;

namespace AbilityKit.Game.Flow
{
    public sealed partial class BattleSessionFeature
    {
        void ISessionPlanHost.StartSession() => StartSession();

        void ISessionPlanHost.StopSession() => StopSession();

        void ISessionPlanHost.ApplyAutoPlanActions() => ApplyAutoPlanActions();

        bool ISessionPlanHost.InvokeSubFeaturesPlanBuilt() => InvokeSubFeaturesPlanBuilt();

        void ISessionPlanHost.NotifySessionStarted(BattleStartPlan plan) => _eventsCtrl.NotifySessionStarted(this, plan);

        void ISessionPlanHost.NotifySessionFailed(System.Exception exception) => _eventsCtrl.NotifySessionFailed(this, exception);

    }

    internal sealed class TickLoopHost : ITickLoopHost
    {
        private readonly Func<float> _getFixedDeltaSeconds;
        private readonly Action<float> _tickRemoteDrivenLocalSim;
        private readonly Action<float> _tickConfirmedAuthorityWorldSim;
        private readonly Action<float> _tickRemoteInterpolation;

        public TickLoopHost(
            Func<float> getFixedDeltaSeconds,
            Action<float> tickRemoteDrivenLocalSim,
            Action<float> tickConfirmedAuthorityWorldSim,
            Action<float> tickRemoteInterpolation)
        {
            _getFixedDeltaSeconds = getFixedDeltaSeconds;
            _tickRemoteDrivenLocalSim = tickRemoteDrivenLocalSim;
            _tickConfirmedAuthorityWorldSim = tickConfirmedAuthorityWorldSim;
            _tickRemoteInterpolation = tickRemoteInterpolation;
        }

        public float GetFixedDeltaSeconds() => _getFixedDeltaSeconds();

        public void TickRemoteDrivenLocalSim(float deltaTime) => _tickRemoteDrivenLocalSim(deltaTime);

        public void TickConfirmedAuthorityWorldSim(float deltaTime) => _tickConfirmedAuthorityWorldSim(deltaTime);

        public void TickRemoteInterpolation(float deltaTime) => _tickRemoteInterpolation(deltaTime);
    }

}
