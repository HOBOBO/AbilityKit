#nullable enable

using System;
using System.Threading.Tasks;
using AbilityKit.Demo.Shooter.View.Hosting;

namespace AbilityKit.Demo.Shooter.View.PlayMode
{
    internal sealed class ShooterInitialFullStateSyncCoordinator
    {
        private readonly Action<bool> _setWaiting;
        private readonly Action<ShooterSnapshotApplyResult> _setLastApplyResult;
        private readonly Action _notifyStateChanged;

        public ShooterInitialFullStateSyncCoordinator(
            Action<bool> setWaiting,
            Action<ShooterSnapshotApplyResult> setLastApplyResult,
            Action notifyStateChanged)
        {
            _setWaiting = setWaiting ?? throw new ArgumentNullException(nameof(setWaiting));
            _setLastApplyResult = setLastApplyResult ?? throw new ArgumentNullException(nameof(setLastApplyResult));
            _notifyStateChanged = notifyStateChanged ?? throw new ArgumentNullException(nameof(notifyStateChanged));
        }

        public async Task RequestIfNeededAsync(
            ShooterRemoteStateSyncConnectionResult connectionResult,
            ShooterClientNetworkLauncher launcher,
            TimeSpan timeout,
            int tickRate)
        {
            if (!connectionResult.RequiresInitialFullStateSync)
            {
                return;
            }

            var launch = connectionResult.Launch;
            var gatewayConnection = launch.GatewayConnection;
            var session = launch.Session;
            var snapshotApplied = false;
            var lastApplyResult = default(ShooterSnapshotApplyResult);

            void OnSnapshotPushDispatched(uint opCode, ArraySegment<byte> payload, ShooterSnapshotApplyResult result)
            {
                lastApplyResult = result;
                _setLastApplyResult(result);
                if (IsFullSnapshotPush(opCode) && IsApplied(result, session))
                {
                    snapshotApplied = true;
                }
            }

            _setWaiting(true);
            _setLastApplyResult(default);
            _notifyStateChanged();
            gatewayConnection.SnapshotPushDispatched += OnSnapshotPushDispatched;

            try
            {
                var request = launch.Battle.RequestFullSnapshotBaselineAsync(timeout);
                var deadline = DateTime.UtcNow + timeout;
                var fixedDeltaTime = 1f / Math.Max(1, tickRate);

                while (!request.IsCompleted)
                {
                    ThrowIfTimedOut(deadline, "Timed out requesting initial Shooter full-state sync.");
                    launcher.Tick(fixedDeltaTime);
                    await Task.Delay(10).ConfigureAwait(false);
                }

                var result = await request.ConfigureAwait(false);
                if (!result.Success || !result.Accepted)
                {
                    throw new InvalidOperationException($"Initial Shooter full-state sync request was rejected. Success={result.Success}, Accepted={result.Accepted}, Message={result.Message}");
                }

                while (!snapshotApplied)
                {
                    ThrowIfTimedOut(deadline, $"Timed out waiting for initial Shooter full-state sync to apply. LastResult={lastApplyResult}.");
                    launcher.Tick(fixedDeltaTime);
                    await Task.Delay(10).ConfigureAwait(false);
                }
            }
            finally
            {
                gatewayConnection.SnapshotPushDispatched -= OnSnapshotPushDispatched;
                _setWaiting(false);
                _notifyStateChanged();
            }
        }

        internal static bool IsFullSnapshotPush(uint opCode)
        {
            return opCode == AbilityKit.Protocol.Room.RoomGatewayOpCodes.SnapshotPushed;
        }

        private static bool IsApplied(ShooterSnapshotApplyResult result, ShooterClientSession session)
        {
            return result == ShooterSnapshotApplyResult.AppliedActorSnapshot
                || result == ShooterSnapshotApplyResult.AppliedPackedSnapshot
                || (!session.NeedsFullSnapshotResync && !session.Presentation.NeedsPureStateFullBaselineResync && session.Presentation.LastPureStateAppliedFrame > 0);
        }

        private static void ThrowIfTimedOut(DateTime deadline, string message)
        {
            if (DateTime.UtcNow >= deadline)
            {
                throw new TimeoutException(message);
            }
        }
    }
}
