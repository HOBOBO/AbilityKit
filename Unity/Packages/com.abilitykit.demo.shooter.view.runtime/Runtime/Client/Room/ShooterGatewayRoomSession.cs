#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using AbilityKit.Ability.Host.Extensions.Session;
using AbilityKit.Game.View.Loading;

namespace AbilityKit.Demo.Shooter.View
{
    public interface IShooterRoomSession : IDisposable
    {
        Task<ShooterRoomSessionJoinResult> CreateAndJoinAsync(
            ShooterRoomSessionLaunchSpec spec,
            CancellationToken cancellationToken = default);

        Task<ShooterRoomSessionJoinResult> JoinAsync(
            ShooterRoomSessionLaunchSpec spec,
            string roomId,
            CancellationToken cancellationToken = default);

        Task SetReadyAsync(string roomId, bool ready, CancellationToken cancellationToken = default);
        Task BeginLoadingAsync(string roomId, long? expectedRevision, CancellationToken cancellationToken = default);
        Task PrepareAndReportAssetsLoadedAsync(
            ShooterRoomSessionLaunchSpec spec,
            ShooterRoomSessionSnapshot snapshot,
            CancellationToken cancellationToken = default);
        Task CancelLoadingAsync(string roomId, long? expectedRevision, CancellationToken cancellationToken = default);
        Task LeaveRoomAsync(string roomId, long? expectedRevision, CancellationToken cancellationToken = default);
        Task<ShooterRoomSessionSnapshot> RefreshAsync(string roomId, CancellationToken cancellationToken = default);
        Task<ShooterRoomSessionSnapshot> WaitForBattleStartAsync(string roomId, CancellationToken cancellationToken = default);
    }

    public sealed class ShooterGatewayRoomSession : IShooterRoomSession
    {
        private readonly ShooterRoomGatewayFlow _shooterFlow;
        private readonly RoomGatewaySessionFlow _flow;
        private readonly ShooterRoomSessionStore _store;
        private ShooterRoomSessionLaunchSpec _activeSpec;
        private bool _hasActiveSpec;
        private bool _disposed;

        public ShooterGatewayRoomSession(
            IShooterRoomGatewayRoomClient roomClient,
            ShooterRoomSessionStore store,
            ClientLoadingPipelineDefinition? loadingDefinition = null,
            IShooterClientLoadingStepProvider? loadingStepProvider = null)
        {
            if (roomClient == null) throw new ArgumentNullException(nameof(roomClient));
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _shooterFlow = new ShooterRoomGatewayFlow(roomClient, loadingDefinition, loadingStepProvider);
            _flow = _shooterFlow.StagedFlow;
        }

        public async Task<ShooterRoomSessionJoinResult> CreateAndJoinAsync(
            ShooterRoomSessionLaunchSpec spec,
            CancellationToken cancellationToken = default)
        {
            ValidateSpec(in spec);
            ThrowIfDisposed();
            _activeSpec = spec;
            _hasActiveSpec = true;
            var shooterLaunchSpec = spec.RoomLaunchSpec;
            var launchSpec = ShooterRoomGatewayFlow.ToLaunchSpec(in shooterLaunchSpec);
            var roomId = await _flow.CreateRoomAsync(
                spec.SessionToken,
                launchSpec,
                spec.Timeout,
                cancellationToken).ConfigureAwait(false);
            return await JoinCoreAsync(spec, roomId, launchSpec, cancellationToken).ConfigureAwait(false);
        }

        public async Task<ShooterRoomSessionJoinResult> JoinAsync(
            ShooterRoomSessionLaunchSpec spec,
            string roomId,
            CancellationToken cancellationToken = default)
        {
            ValidateSpec(in spec);
            if (string.IsNullOrWhiteSpace(roomId)) throw new ArgumentException("roomId is required.", nameof(roomId));
            ThrowIfDisposed();
            _activeSpec = spec;
            _hasActiveSpec = true;
            var shooterLaunchSpec = spec.RoomLaunchSpec;
            return await JoinCoreAsync(
                spec,
                roomId,
                ShooterRoomGatewayFlow.ToLaunchSpec(in shooterLaunchSpec),
                cancellationToken).ConfigureAwait(false);
        }

        public async Task SetReadyAsync(string roomId, bool ready, CancellationToken cancellationToken = default)
        {
            var spec = RequireActiveSpec(roomId);
            var result = await _flow.SetReadyAsync(
                spec.SessionToken,
                roomId,
                ready,
                spec.Timeout,
                cancellationToken).ConfigureAwait(false);
            EnsureSuccess(result.Success, result.Message, "set ready");
            await RefreshAsync(roomId, cancellationToken).ConfigureAwait(false);
        }

        public async Task BeginLoadingAsync(string roomId, long? expectedRevision, CancellationToken cancellationToken = default)
        {
            var spec = RequireActiveSpec(roomId);
            var result = await _flow.BeginLoadingAsync(
                new RoomGatewayBeginLoadingRequest(
                    spec.SessionToken,
                    roomId,
                    expectedRevision,
                    Guid.NewGuid().ToString("N")),
                spec.Timeout,
                cancellationToken).ConfigureAwait(false);
            EnsureApplied(result.Success, result.Applied, result.Message, "begin loading");
        }

        public async Task PrepareAndReportAssetsLoadedAsync(
            ShooterRoomSessionLaunchSpec spec,
            ShooterRoomSessionSnapshot snapshot,
            CancellationToken cancellationToken = default)
        {
            ValidateSpec(in spec);
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            ThrowIfDisposed();
            await _shooterFlow.PrepareAndReportAssetsLoadedAsync(
                spec.SessionToken,
                snapshot.RoomId,
                spec.FallbackPlayerId,
                snapshot,
                spec.Timeout,
                cancellationToken).ConfigureAwait(false);
        }

        public async Task CancelLoadingAsync(string roomId, long? expectedRevision, CancellationToken cancellationToken = default)
        {
            var spec = RequireActiveSpec(roomId);
            var result = await _flow.CancelLoadingAsync(
                new RoomGatewayCancelLoadingRequest(
                    spec.SessionToken,
                    roomId,
                    expectedRevision,
                    Guid.NewGuid().ToString("N")),
                spec.Timeout,
                cancellationToken).ConfigureAwait(false);
            EnsureApplied(result.Success, result.Applied, result.Message, "cancel loading");
        }

        public async Task LeaveRoomAsync(string roomId, long? expectedRevision, CancellationToken cancellationToken = default)
        {
            var spec = RequireActiveSpec(roomId);
            var result = await _flow.LeaveRoomAsync(
                new RoomGatewayLeaveRequest(
                    spec.SessionToken,
                    roomId,
                    expectedRevision,
                    Guid.NewGuid().ToString("N")),
                spec.Timeout,
                cancellationToken).ConfigureAwait(false);
            EnsureApplied(result.Success, result.Applied, result.Message, "leave room");
            _hasActiveSpec = false;
        }

        public async Task<ShooterRoomSessionSnapshot> RefreshAsync(
            string roomId,
            CancellationToken cancellationToken = default)
        {
            var spec = RequireActiveSpec(roomId);
            var result = await _flow.GetSnapshotAsync(
                spec.SessionToken,
                roomId,
                spec.Timeout,
                cancellationToken).ConfigureAwait(false);
            EnsureSuccess(result.Success, result.Message, "get room snapshot");
            return RequireStoreSnapshot(roomId);
        }

        public async Task<ShooterRoomSessionSnapshot> WaitForBattleStartAsync(
            string roomId,
            CancellationToken cancellationToken = default)
        {
            var spec = RequireActiveSpec(roomId);
            var result = await _flow.WaitForBattleStartAsync(
                spec.SessionToken,
                roomId,
                TimeSpan.FromSeconds(2),
                spec.Timeout ?? TimeSpan.FromSeconds(135),
                cancellationToken).ConfigureAwait(false);
            EnsureSuccess(result.Success, result.Message, "wait for battle start");
            return RequireStoreSnapshot(roomId);
        }

        private async Task<ShooterRoomSessionJoinResult> JoinCoreAsync(
            ShooterRoomSessionLaunchSpec spec,
            string roomId,
            RoomGatewayLaunchSpec launchSpec,
            CancellationToken cancellationToken)
        {
            var join = await _flow.JoinRoomAsync(
                spec.SessionToken,
                launchSpec.Region,
                launchSpec.ServerId,
                roomId,
                spec.Timeout,
                cancellationToken).ConfigureAwait(false);
            EnsureSuccess(join.Success, join.Message, "join room");
            var playerId = join.CurrentPlayerId == 0u ? spec.FallbackPlayerId : join.CurrentPlayerId;
            if (playerId == 0u)
            {
                throw new InvalidOperationException("Room gateway join returned no authoritative player id.");
            }

            await RefreshAsync(roomId, cancellationToken).ConfigureAwait(false);
            return new ShooterRoomSessionJoinResult(
                roomId,
                join.NumericRoomId,
                playerId,
                ToShooterEntryKind(join.JoinKind),
                join.BattleId,
                join.Message);
        }

        private ShooterRoomSessionSnapshot RequireStoreSnapshot(string roomId)
        {
            var snapshot = _store.Current;
            if (snapshot == null || !string.Equals(snapshot.RoomId, roomId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Room gateway returned no authoritative snapshot for the active room.");
            }

            return snapshot;
        }

        private ShooterRoomSessionLaunchSpec RequireActiveSpec(string roomId)
        {
            ThrowIfDisposed();
            if (!_hasActiveSpec) throw new InvalidOperationException("No active Shooter room session.");
            if (string.IsNullOrWhiteSpace(roomId)) throw new ArgumentException("roomId is required.", nameof(roomId));
            return _activeSpec;
        }

        private static ShooterRoomGatewayEntryKind ToShooterEntryKind(RoomGatewaySessionEntryKind entryKind)
        {
            return entryKind switch
            {
                RoomGatewaySessionEntryKind.Reconnect => ShooterRoomGatewayEntryKind.Reconnect,
                RoomGatewaySessionEntryKind.LateJoin => ShooterRoomGatewayEntryKind.LateJoin,
                _ => ShooterRoomGatewayEntryKind.TeamLobby
            };
        }

        private static void ValidateSpec(in ShooterRoomSessionLaunchSpec spec)
        {
            if (string.IsNullOrWhiteSpace(spec.SessionToken)) throw new ArgumentException("sessionToken is required.", nameof(spec));
            if (spec.FallbackPlayerId == 0u) throw new ArgumentOutOfRangeException(nameof(spec));
        }

        private static void EnsureSuccess(bool success, string message, string operation)
        {
            if (!success)
            {
                throw new InvalidOperationException(string.IsNullOrWhiteSpace(message)
                    ? $"Shooter room {operation} failed."
                    : message);
            }
        }

        private static void EnsureApplied(bool success, bool applied, string message, string operation)
        {
            EnsureSuccess(success, message, operation);
            if (!applied)
            {
                throw new InvalidOperationException(string.IsNullOrWhiteSpace(message)
                    ? $"Shooter room {operation} was not applied."
                    : message);
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(ShooterGatewayRoomSession));
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _shooterFlow.Dispose();
        }
    }
}
