using System.Threading;
using System.Threading.Tasks;

namespace AbilityKit.Game.Flow
{
    public sealed partial class BattleSessionFeature
    {
        private readonly object _gatewayStopGate = new object();
        private readonly SemaphoreSlim _gatewayStartGate = new SemaphoreSlim(1, 1);
        private Task _pendingGatewayStopTask = Task.CompletedTask;

        private bool HasGatewayRoomConnection => _runtime.GatewayRoom.IsBuilt;

        private void TickGatewayRoomConnection(float deltaTime) => _runtime.GatewayRoom.Tick(deltaTime);

        private Task GatewayRoomPreparationTask => _runtime.GatewayRoom.PreparationTask;

        private bool ShouldPrepareGatewayRoom() => GatewayRoomPreparationHelper.ShouldPrepareGatewayRoom(_plan);

        private async Task StartGatewayRoomPreparation()
        {
            await _gatewayStartGate.WaitAsync().ConfigureAwait(false);
            try
            {
                await StopGatewayRoomPreparationCoreAsync().ConfigureAwait(false);
                _runtime.GatewayRoom.Build(_plan, _unityDispatcher, _networkIoDispatcher);
                _runtime.GatewayRoom.StartPreparation(
                    _plan,
                    plan => _plan = plan,
                    PublishGatewayClockSample,
                    exception => _eventsCtrl.NotifySessionFailed(this, exception));
            }
            finally
            {
                _gatewayStartGate.Release();
            }
        }

        private void CompleteGatewayRoomPreparation()
        {
            _runtime.GatewayRoom.CompletePreparation();
        }

        private Task StopGatewayRoomPreparation() =>
            StopGatewayRoomPreparationAsync();

        private Task StopGatewayRoomPreparationAsync()
        {
            lock (_gatewayStopGate)
            {
                if (!_pendingGatewayStopTask.IsCompleted)
                {
                    return _pendingGatewayStopTask;
                }

                _pendingGatewayStopTask = StopGatewayRoomPreparationAfterStartAsync();
                return _pendingGatewayStopTask;
            }
        }

        private async Task StopGatewayRoomPreparationAfterStartAsync()
        {
            await _gatewayStartGate.WaitAsync().ConfigureAwait(false);
            try
            {
                await StopGatewayRoomPreparationCoreAsync().ConfigureAwait(false);
            }
            finally
            {
                _gatewayStartGate.Release();
            }
        }

        private async Task StopGatewayRoomPreparationCoreAsync()
        {
            try
            {
                await _runtime.GatewayRoom.StopAsync().ConfigureAwait(false);
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
