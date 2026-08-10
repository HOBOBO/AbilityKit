using System.Threading.Tasks;

namespace AbilityKit.Game.Flow
{
    public sealed partial class BattleSessionFeature
    {
        private bool HasGatewayRoomConnection => _runtime.GatewayRoom.IsBuilt;

        private void TickGatewayRoomConnection(float deltaTime) => _runtime.GatewayRoom.Tick(deltaTime);

        private Task GatewayRoomPreparationTask => _runtime.GatewayRoom.PreparationTask;

        private bool ShouldPrepareGatewayRoom() => GatewayRoomPreparationHelper.ShouldPrepareGatewayRoom(_plan);

        private void StartGatewayRoomPreparation()
        {
            StopGatewayRoomPreparation();
            _runtime.GatewayRoom.Build(_plan, _unityDispatcher, _networkIoDispatcher);
            _runtime.GatewayRoom.StartPreparation(
                _plan,
                plan => _plan = plan,
                PublishGatewayClockSample,
                exception => _eventsCtrl.NotifySessionFailed(this, exception));
        }

        private void CompleteGatewayRoomPreparation()
        {
            _runtime.GatewayRoom.CompletePreparation();
        }

        private void StopGatewayRoomPreparation()
        {
            _runtime.GatewayRoom.Dispose();
            _state.GatewayRoomTimeSync.Reset();
            BattleFlowDebugProvider.TimeSyncStats = null;
            BattleFlowDebugProvider.TimeSyncStatsByWorld = null;
        }

        private void PublishGatewayClockSample(
            GatewayTimeSyncEwma estimate,
            GatewayTimeSyncRuntimeOptions options)
        {
            var state = _state.GatewayRoomTimeSync;
            state.HasClockSync = estimate.HasClockSync;
            state.ClockOffsetSecondsEwma = estimate.ClockOffsetSecondsEwma;
            state.RttSecondsEwma = estimate.RttSecondsEwma;
            state.Samples = estimate.Samples;
            BattleFlowDebugProvider.TimeSyncStats = BuildCurrentTimeSyncStats(
                options.OpCode,
                options.IntervalMs,
                options.Alpha,
                options.TimeoutMs);
            UpdateTimeSyncStatsByWorld(
                options.OpCode,
                options.IntervalMs,
                options.Alpha,
                options.TimeoutMs);
        }
    }
}
