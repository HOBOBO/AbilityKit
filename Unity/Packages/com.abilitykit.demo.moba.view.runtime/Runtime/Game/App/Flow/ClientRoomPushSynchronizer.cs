#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using AbilityKit.Game.Battle.Agent;

namespace AbilityKit.Game.Flow
{
    /// <summary>
    /// Applies complete authoritative Room snapshots delivered by server push.
    /// </summary>
    public sealed class ClientRoomPushSynchronizer
    {
        private readonly IGatewayRoomClient _client;
        private readonly ClientRoomStore _store;
        private long _handledPushCount;
        private long _appliedPushCount;
        private long _refreshFallbackCount;
        private long _lastPushRevision;
        private long _lastPushUtcTicks;

        public long HandledPushCount => Interlocked.Read(ref _handledPushCount);
        public long AppliedPushCount => Interlocked.Read(ref _appliedPushCount);
        public long RefreshFallbackCount => Interlocked.Read(ref _refreshFallbackCount);
        public long LastPushRevision => Interlocked.Read(ref _lastPushRevision);
        public DateTime? LastPushUtc
        {
            get
            {
                var ticks = Interlocked.Read(ref _lastPushUtcTicks);
                return ticks > 0 ? new DateTime(ticks, DateTimeKind.Utc) : null;
            }
        }

        public ClientRoomPushSynchronizer(
            IGatewayRoomClient client,
            ClientRoomStore store,
            Func<CancellationToken, Task> refreshSnapshotAsync)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _ = refreshSnapshotAsync ?? throw new ArgumentNullException(nameof(refreshSnapshotAsync));
        }

        public Task<bool> HandleServerPushAsync(
            uint opCode,
            ArraySegment<byte> payload,
            CancellationToken cancellationToken = default)
        {
            if (!_client.IsRoomStateChangedPush(opCode))
            {
                return Task.FromResult(false);
            }

            cancellationToken.ThrowIfCancellationRequested();

            var snapshot = _client.DeserializeRoomStateChangedPush(payload);
            Interlocked.Increment(ref _handledPushCount);
            Interlocked.Exchange(ref _lastPushRevision, snapshot.RoomRevision);
            Interlocked.Exchange(ref _lastPushUtcTicks, DateTime.UtcNow.Ticks);
            if (_store.ApplySnapshot(snapshot) == ClientRoomSnapshotApplyResult.Applied)
            {
                Interlocked.Increment(ref _appliedPushCount);
            }

            if (_store.IsStale)
            {
                // Every RoomStateChanged payload is a complete authoritative snapshot.
                // Coalescing may skip revisions, but the newest snapshot is already the
                // recovery baseline and must not start a request from the receive callback.
                _store.MarkRefreshed();
            }

            return Task.FromResult(true);
        }
    }
}
