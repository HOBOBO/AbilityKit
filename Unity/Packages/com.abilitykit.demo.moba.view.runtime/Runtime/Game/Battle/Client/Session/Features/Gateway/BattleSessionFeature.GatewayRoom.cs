using System.Threading.Tasks;

namespace AbilityKit.Game.Flow
{
    public sealed partial class BattleSessionFeature
    {
        private bool HasGatewayRoomConnection => _runtime.GatewayRoom.IsBuilt;

        private void TickGatewayRoomConnection(float deltaTime) => _runtime.GatewayRoom.Tick(deltaTime);

        private Task GatewayRoomPreparationTask => _runtime.GatewayRoom.PreparationTask;

        private bool ShouldPrepareGatewayRoom() => GatewayRoomPreparationHelper.ShouldPrepareGatewayRoom(_plan);

        private Task StartGatewayRoomPreparation() =>
            _runtime.GatewayRoom.StartNewPreparationAsync(
                _plan,
                _unityDispatcher,
                _networkIoDispatcher,
                () =>
                {
                    _state.GatewayRoomTimeSync.Reset();
                    _runtime.Diagnostics.ClearTimeSync();
                },
                plan => _plan = plan,
                PublishGatewayClockSample,
                exception => _eventsCtrl.NotifySessionFailed(this, exception));

        private Task CompleteGatewayRoomPreparationAsync()
        {
            return _runtime.GatewayRoom.CompletePreparationAsync();
        }

        private async Task StopGatewayRoomPreparationAsync()
        {
            try
            {
                await _runtime.GatewayRoom.StopPreparationAsync().ConfigureAwait(false);
            }
            finally
            {
                _state.GatewayRoomTimeSync.Reset();
                _runtime.Diagnostics.ClearTimeSync();
            }
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
            var current = BuildCurrentTimeSyncStats(
                options.OpCode,
                options.IntervalMs,
                options.Alpha,
                options.TimeoutMs);
            var byWorld = BuildTimeSyncStatsByWorld(
                current,
                options.OpCode,
                options.IntervalMs,
                options.Alpha,
                options.TimeoutMs);
            _runtime.Diagnostics.PublishTimeSync(current, byWorld);
        }
    }
}
