using System.Threading.Tasks;

namespace AbilityKit.Game.Flow
{
    public sealed partial class BattleSessionFeature
    {
        Task ISessionPlanHost.StartSessionAsync() => StartSessionAsync();

        Task ISessionPlanHost.StopSessionAsync() => StopSessionAsync();

        void ISessionPlanHost.ApplyAutoPlanActions() => ApplyAutoPlanActions();

        bool ISessionPlanHost.InvokeSubFeaturesPlanBuilt() => InvokeSubFeaturesPlanBuilt();

        void ISessionPlanHost.NotifySessionStarted(BattleStartPlan plan) => _eventsCtrl.NotifySessionStarted(this, plan);

        void ISessionPlanHost.NotifySessionFailed(System.Exception exception) => _eventsCtrl.NotifySessionFailed(this, exception);

        bool ISessionTickLoopPort.HasSession => _session != null;

        float ISessionTickLoopPort.FixedDeltaSeconds =>
            GetFixedDeltaSeconds();

        void ISessionTickLoopPort.TickTransport(float elapsedSeconds) =>
            _session?.NetworkTransport?.Tick(elapsedSeconds);

        void ISessionTickLoopPort.TickSimulationFrame(
            float fixedDeltaSeconds) =>
            _session.Tick(fixedDeltaSeconds);

        void ISessionTickLoopPort.TickRemoteDrivenSimulation(
            float deltaTime) =>
            TickRemoteDrivenLocalSim(deltaTime);

        void ISessionTickLoopPort.TickConfirmedSimulation(
            float deltaTime) =>
            TickConfirmedAuthorityWorldSim(deltaTime);

        void ISessionTickLoopPort.TickPresentation(float deltaTime)
        {
            TickRemoteInterpolation(deltaTime);
            _runtime.Presentation.SyncProjectionViews();
        }
    }

}
