namespace AbilityKit.Game.Flow
{
    public sealed partial class BattleSessionFeature
    {
        float ITickLoopHost.GetFixedDeltaSeconds() => GetFixedDeltaSeconds();

        void ITickLoopHost.TickRemoteDrivenLocalSim(float deltaTime) => TickRemoteDrivenLocalSim(deltaTime);

        void ITickLoopHost.TickConfirmedAuthorityWorldSim(float deltaTime) => TickConfirmedAuthorityWorldSim(deltaTime);

        void ITickLoopHost.TickRemoteInterpolation(float deltaTime) => TickRemoteInterpolation(deltaTime);

        void ISessionPlanHost.StartSession() => StartSession();

        void ISessionPlanHost.StopSession() => StopSession();

        void ISessionPlanHost.ApplyAutoPlanActions() => ApplyAutoPlanActions();

        bool ISessionPlanHost.InvokeSubFeaturesPlanBuilt() => InvokeSubFeaturesPlanBuilt();

        void ISessionPlanHost.NotifySessionStarted(BattleStartPlan plan) => _eventsCtrl.NotifySessionStarted(this, plan);

        void ISessionPlanHost.NotifySessionFailed(System.Exception exception) => _eventsCtrl.NotifySessionFailed(this, exception);

        bool ISessionReplayHost.RenderPresentation => RenderPresentation;

        void ISessionReplayHost.StartSession() => StartSession();

        void ISessionReplayHost.StopSession() => StopSession();

        void ISessionReplayHost.ApplyAutoPlanActions() => ApplyAutoPlanActions();

        void ISessionReplayHost.SuspendReplayPresentation() => SuspendReplayPresentation();

        void ISessionReplayHost.RestoreReplayPresentation() => RestoreReplayPresentationIfEnabled();

        float ISessionReplayHost.GetFixedDeltaSeconds() => GetFixedDeltaSeconds();
    }
}
