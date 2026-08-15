#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace AbilityKit.Demo.Shooter.View.Tests
{
    public sealed class ShooterRoomGatewayFlowTests
    {
        [TestCase(ShooterGatewayRoomJoinKind.Reconnect)]
        [TestCase(ShooterGatewayRoomJoinKind.LateJoin)]
        public void RunningBattleJoinSubscribesWithoutRepeatingLobbyCommands(
            ShooterGatewayRoomJoinKind joinKind)
        {
            const string sessionToken = "session-1";
            const string roomId = "room-1";
            const string battleId = "battle-1";
            const ulong numericRoomId = 17ul;
            const ulong worldId = 23ul;
            const uint authoritativePlayerId = 2u;
            const long serverNowTicks = 123456L;
            var anchor = new ShooterGatewayWorldStartAnchor(1000L, 60L, 5, 1d / 60d);
            var client = new RunningBattleRoomClient(new ShooterGatewayJoinRoomResult(
                success: true,
                roomId,
                numericRoomId,
                in anchor,
                message: string.Empty,
                battleId,
                canStart: false,
                joinKind,
                serverNowTicks,
                worldId,
                authoritativePlayerId));

            using var flow = new ShooterRoomGatewayFlow(client);
            var result = flow.JoinReadyStartAndSubscribeAsync(
                    sessionToken,
                    roomId,
                    ShooterRoomLaunchSpec.CreateDefault("client-1"),
                    playerId: 1u,
                    timeout: TimeSpan.FromSeconds(1))
                .GetAwaiter()
                .GetResult();

            Assert.That(result.RoomId, Is.EqualTo(roomId));
            Assert.That(result.BattleId, Is.EqualTo(battleId));
            Assert.That(result.WorldId, Is.EqualTo(worldId));
            Assert.That(result.PlayerId, Is.EqualTo(authoritativePlayerId));
            Assert.That(result.Started, Is.True);
            Assert.That(result.Subscribed, Is.True);
            Assert.That(client.JoinCalls, Is.EqualTo(1));
            Assert.That(client.SubscribeCalls, Is.EqualTo(1));
            Assert.That(client.LastSubscription.SessionToken, Is.EqualTo(sessionToken));
            Assert.That(client.LastSubscription.RoomId, Is.EqualTo(roomId));
            Assert.That(client.LastSubscription.BattleId, Is.EqualTo(battleId));
            Assert.That(client.ReadyCalls, Is.Zero);
            Assert.That(client.BeginLoadingCalls, Is.Zero);
            Assert.That(client.StartBattleCalls, Is.Zero);
        }

        private sealed class RunningBattleRoomClient : IShooterRoomGatewayRoomClient
        {
            private readonly ShooterGatewayJoinRoomResult _joinResult;

            public RunningBattleRoomClient(ShooterGatewayJoinRoomResult joinResult)
            {
                _joinResult = joinResult;
            }

            public int JoinCalls { get; private set; }
            public int SubscribeCalls { get; private set; }
            public int ReadyCalls { get; private set; }
            public int BeginLoadingCalls { get; private set; }
            public int StartBattleCalls { get; private set; }
            public ShooterGatewayStateSyncSubscriptionRequest LastSubscription { get; private set; }

            public Task<ShooterGatewayGuestLoginResult> GuestLoginAsync(
                ShooterGatewayGuestLoginRequest request,
                TimeSpan? timeout = null,
                CancellationToken cancellationToken = default) =>
                Task.FromResult(default(ShooterGatewayGuestLoginResult));

            public Task<ShooterGatewayAccountLoginResult> AccountLoginAsync(
                ShooterGatewayAccountLoginRequest request,
                TimeSpan? timeout = null,
                CancellationToken cancellationToken = default) =>
                Task.FromResult(default(ShooterGatewayAccountLoginResult));

            public Task<ShooterGatewayListRoomsResult> ListRoomsAsync(
                ShooterGatewayListRoomsRequest request,
                TimeSpan? timeout = null,
                CancellationToken cancellationToken = default) =>
                Task.FromResult(default(ShooterGatewayListRoomsResult));

            public Task<ShooterGatewayCreateRoomResult> CreateRoomAsync(
                ShooterGatewayCreateRoomRequest request,
                TimeSpan? timeout = null,
                CancellationToken cancellationToken = default) =>
                Task.FromResult(default(ShooterGatewayCreateRoomResult));

            public Task<ShooterGatewayJoinRoomResult> JoinRoomAsync(
                ShooterGatewayJoinRoomRequest request,
                TimeSpan? timeout = null,
                CancellationToken cancellationToken = default)
            {
                JoinCalls++;
                return Task.FromResult(_joinResult);
            }

            public Task<ShooterGatewayRoomSnapshotResult> SetReadyAsync(
                ShooterGatewayReadyRequest request,
                TimeSpan? timeout = null,
                CancellationToken cancellationToken = default)
            {
                ReadyCalls++;
                return Task.FromResult(default(ShooterGatewayRoomSnapshotResult));
            }

            public Task<ShooterGatewayStartBattleResult> StartBattleAsync(
                ShooterGatewayStartBattleRequest request,
                TimeSpan? timeout = null,
                CancellationToken cancellationToken = default)
            {
                StartBattleCalls++;
                return Task.FromResult(default(ShooterGatewayStartBattleResult));
            }

            public Task<ShooterGatewayRoomOperationResult> BeginLoadingAsync(
                ShooterGatewayBeginLoadingRequest request,
                TimeSpan? timeout = null,
                CancellationToken cancellationToken = default)
            {
                BeginLoadingCalls++;
                return Task.FromResult(default(ShooterGatewayRoomOperationResult));
            }

            public Task<ShooterGatewayRoomOperationResult> ReportAssetsLoadedAsync(
                ShooterGatewayReportAssetsLoadedRequest request,
                TimeSpan? timeout = null,
                CancellationToken cancellationToken = default) =>
                Task.FromResult(default(ShooterGatewayRoomOperationResult));

            public Task<ShooterGatewayRoomOperationResult> ReportLoadingProgressAsync(
                ShooterGatewayReportLoadingProgressRequest request,
                TimeSpan? timeout = null,
                CancellationToken cancellationToken = default) =>
                Task.FromResult(default(ShooterGatewayRoomOperationResult));

            public Task<ShooterGatewayRoomOperationResult> CancelLoadingAsync(
                ShooterGatewayCancelLoadingRequest request,
                TimeSpan? timeout = null,
                CancellationToken cancellationToken = default) =>
                Task.FromResult(default(ShooterGatewayRoomOperationResult));

            public Task<ShooterGatewayRoomOperationResult> LeaveRoomAsync(
                ShooterGatewayLeaveRoomRequest request,
                TimeSpan? timeout = null,
                CancellationToken cancellationToken = default) =>
                Task.FromResult(default(ShooterGatewayRoomOperationResult));

            public Task<ShooterGatewayGetRoomSnapshotResult> GetSnapshotAsync(
                ShooterGatewayGetRoomSnapshotRequest request,
                TimeSpan? timeout = null,
                CancellationToken cancellationToken = default) =>
                Task.FromResult(default(ShooterGatewayGetRoomSnapshotResult));

            public Task<ShooterGatewayStateSyncSubscriptionResult> SubscribeStateSyncAsync(
                ShooterGatewayStateSyncSubscriptionRequest request,
                TimeSpan? timeout = null,
                CancellationToken cancellationToken = default)
            {
                SubscribeCalls++;
                LastSubscription = request;
                return Task.FromResult(new ShooterGatewayStateSyncSubscriptionResult(true, string.Empty));
            }

            public Task<ShooterGatewayReliableBattleEventAckResult> AcknowledgeReliableBattleEventsAsync(
                ShooterGatewayReliableBattleEventAckRequest request,
                TimeSpan? timeout = null,
                CancellationToken cancellationToken = default) =>
                Task.FromResult(default(ShooterGatewayReliableBattleEventAckResult));

            public Task<ShooterGatewayFullStateSyncRequestResult> RequestFullStateSyncAsync(
                ShooterGatewayFullStateSyncRequest request,
                TimeSpan? timeout = null,
                CancellationToken cancellationToken = default) =>
                Task.FromResult(default(ShooterGatewayFullStateSyncRequestResult));

            public Task<ShooterGatewayRestoreRoomResult> RestoreRoomAsync(
                ShooterGatewayRestoreRoomRequest request,
                TimeSpan? timeout = null,
                CancellationToken cancellationToken = default) =>
                Task.FromResult(default(ShooterGatewayRestoreRoomResult));
        }
    }
}
