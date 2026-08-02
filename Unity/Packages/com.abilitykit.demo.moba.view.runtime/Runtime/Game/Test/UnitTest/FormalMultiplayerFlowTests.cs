#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using AbilityKit.Demo.Common.Rooms;
using AbilityKit.Game.Battle.Agent;
using AbilityKit.Game.Flow;
using NUnit.Framework;
using UnityEngine;

namespace AbilityKit.Game.Test.UnitTest
{
    public sealed class FormalMultiplayerFlowTests
    {
        [Test]
        public void MembershipNotice_FormatsLeaveJoinAndOwnerTransfer()
        {
            var change = new ClientRoomMembershipChange(
                "room-a",
                previousRevision: 10,
                currentRevision: 11,
                joinedAccountIds: new[] { "account-c" },
                leftAccountIds: new[] { "account-b" },
                previousOwnerAccountId: "account-b",
                currentOwnerAccountId: "account-a");

            var notice = FormalLobbyFeature.FormatMembershipNotice(change);

            Assert.That(
                notice,
                Is.EqualTo(
                    "account-b left the room. account-c joined the room. " +
                    "account-a is now room owner."));
        }

        [Test]
        public void LaunchSpec_RequiresValidatedConfigAndAuthenticatedStarterRequest()
        {
            var config = ScriptableObject.CreateInstance<BattleGatewayConfigSO>();
            config.UseGatewayTransport = true;
            var request = new DemoMultiplayerLaunchRequest(
                "gateway.example",
                4200,
                "release",
                "server-a",
                "account-a",
                "session-a",
                TimeSpan.FromSeconds(8));

            try
            {
                var built = FormalLobbyFeature.TryBuildLaunchSpec(
                    config,
                    request,
                    activeSessionToken: string.Empty,
                    out var spec,
                    out var error);

                Assert.That(built, Is.True, error);
                Assert.That(spec, Is.Not.Null);
                Assert.That(spec!.SessionToken, Is.EqualTo("session-a"));
                Assert.That(spec.Region, Is.EqualTo("release"));
                Assert.That(spec.ServerId, Is.EqualTo("server-a"));
                Assert.That(spec.RoomType, Is.EqualTo("moba"));
                Assert.That(spec.GameplayId, Is.EqualTo(1));
                Assert.That(spec.WorldType, Is.EqualTo("moba"));

                request = new DemoMultiplayerLaunchRequest(
                    "gateway.example",
                    4200,
                    "release",
                    "server-a",
                    "account-a",
                    string.Empty,
                    TimeSpan.FromSeconds(8));
                built = FormalLobbyFeature.TryBuildLaunchSpec(
                    config,
                    request,
                    activeSessionToken: string.Empty,
                    out _,
                    out error);

                Assert.That(built, Is.False);
                StringAssert.Contains("authenticated", error);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(config);
            }
        }

        [Test]
        public void LocalPlayerResolution_FallsBackToAuthenticatedAccount()
        {
            var expected = new MultiplayerRoomPlayerSnapshot
            {
                AccountId = "account-a",
                PlayerId = 7,
                HeroId = 1001,
                LobbyReady = true
            };
            var snapshot = new MultiplayerRoomSnapshot
            {
                Players = new[]
                {
                    new MultiplayerRoomPlayerSnapshot { AccountId = "account-b", PlayerId = 8 },
                    expected
                }
            };

            var resolved = FormalLobbyFeature.FindLocalPlayer(
                snapshot,
                localPlayerId: 0,
                accountId: "account-a");

            Assert.That(resolved, Is.SameAs(expected));
        }

        [Test]
        public void AutomaticStart_RequiresReadyOwnerAndRunsOncePerRoom()
        {
            var snapshot = new MultiplayerRoomSnapshot
            {
                RoomId = "room-a",
                Phase = MultiplayerRoomPhase.Lobby,
                CanStart = true
            };

            Assert.That(
                FormalLobbyFeature.ShouldStartAutomatically(
                    enabled: true,
                    MultiplayerRoomFlowState.InLobby,
                    isLocalRoomOwner: true,
                    snapshot,
                    attemptedRoomId: string.Empty,
                    operationBusy: false),
                Is.True);
            Assert.That(
                FormalLobbyFeature.ShouldStartAutomatically(
                    enabled: true,
                    MultiplayerRoomFlowState.InLobby,
                    isLocalRoomOwner: false,
                    snapshot,
                    attemptedRoomId: string.Empty,
                    operationBusy: false),
                Is.False);
            Assert.That(
                FormalLobbyFeature.ShouldStartAutomatically(
                    enabled: true,
                    MultiplayerRoomFlowState.InLobby,
                    isLocalRoomOwner: true,
                    snapshot,
                    attemptedRoomId: "room-a",
                    operationBusy: false),
                Is.False);

            snapshot.CanStart = false;
            Assert.That(
                FormalLobbyFeature.ShouldStartAutomatically(
                    enabled: true,
                    MultiplayerRoomFlowState.InLobby,
                    isLocalRoomOwner: true,
                    snapshot,
                    attemptedRoomId: string.Empty,
                    operationBusy: false),
                Is.False);
        }

        [Test]
        public void LaunchRequest_CanDelegateLobbyAutomationToAnExternalDriver()
        {
            var request = new DemoMultiplayerLaunchRequest(
                "gateway.example",
                4200,
                "release",
                "server-a",
                "account-a",
                "session-a",
                TimeSpan.FromSeconds(8),
                suppressAutomaticLobbyActions: true);

            Assert.That(request.SuppressAutomaticLobbyActions, Is.True);
        }

        [Test]
        public void SnapshotReceivedWhileIdle_DoesNotActivateRoomFlow()
        {
            var provider = new TestSnapshotProvider();
            using var controller = new MultiplayerRoomFlowController(
                new TestRoomSession(),
                provider);

            provider.Publish(new MultiplayerRoomSnapshot
            {
                RoomId = "room-a",
                NumericRoomId = 10,
                Phase = MultiplayerRoomPhase.Lobby
            });

            Assert.That(controller.CurrentState, Is.EqualTo(MultiplayerRoomFlowState.Idle));
            Assert.That(controller.CurrentSnapshot?.RoomId, Is.EqualTo("room-a"));
        }

        [Test]
        public void SnapshotReceivedForActiveLobby_AdvancesAuthoritativePhase()
        {
            var provider = new TestSnapshotProvider();
            using var controller = new MultiplayerRoomFlowController(
                new TestRoomSession(),
                provider);
            var spec = new MultiplayerRoomLaunchSpec
            {
                SessionToken = "session-a",
                Region = "release",
                ServerId = "server-a",
                RoomType = "moba",
                RoomTitle = "MOBA Room",
                MaxPlayers = 2
            };
            controller.StartJoinRoomAsync(spec, "room-a").GetAwaiter().GetResult();

            Assert.That(controller.LocalPlayerId, Is.EqualTo(7u));

            provider.Publish(new MultiplayerRoomSnapshot
            {
                RoomId = "room-a",
                NumericRoomId = 10,
                Phase = MultiplayerRoomPhase.Loading,
                LaunchGeneration = 1
            });

            Assert.That(controller.CurrentState, Is.EqualTo(MultiplayerRoomFlowState.LoadingAssets));
        }

        [Test]
        public void CreateJoinAndCancel_UseOneAuthoritativePlayerIdentity()
        {
            var provider = new TestSnapshotProvider();
            var session = new TestRoomSession(playerId: 17u);
            using var controller = new MultiplayerRoomFlowController(session, provider);
            var spec = CreateLaunchSpec();

            controller.StartCreateRoomAsync(spec).GetAwaiter().GetResult();

            Assert.That(controller.CurrentRoomId, Is.EqualTo("room-a"));
            Assert.That(controller.LocalPlayerId, Is.EqualTo(17u));
            Assert.That(controller.IsLocalRoomOwner, Is.True);

            controller.Cancel();

            Assert.That(controller.CurrentRoomId, Is.Empty);
            Assert.That(controller.LocalPlayerId, Is.Zero);
            Assert.That(controller.IsLocalRoomOwner, Is.False);
        }

        [Test]
        public void JoinWithoutAuthoritativePlayerIdentity_FailsTheFlow()
        {
            var provider = new TestSnapshotProvider();
            using var controller = new MultiplayerRoomFlowController(
                new TestRoomSession(playerId: 0u),
                provider);

            var exception = Assert.Throws<InvalidOperationException>(() =>
                controller.StartJoinRoomAsync(CreateLaunchSpec(), "room-a")
                    .GetAwaiter()
                    .GetResult());

            StringAssert.Contains("player id", exception!.Message);
            Assert.That(controller.CurrentState, Is.EqualTo(MultiplayerRoomFlowState.Failed));
            Assert.That(controller.LocalPlayerId, Is.Zero);
        }

        [Test]
        public void LeaveRoom_SuccessClearsIdentityAndReturnsIdle()
        {
            var provider = new TestSnapshotProvider();
            var session = new TestRoomSession();
            using var controller = new MultiplayerRoomFlowController(session, provider);
            controller.StartJoinRoomAsync(CreateLaunchSpec(), "room-a").GetAwaiter().GetResult();
            provider.Publish(new MultiplayerRoomSnapshot
            {
                RoomId = "room-a",
                NumericRoomId = 10,
                Phase = MultiplayerRoomPhase.Lobby
            });

            controller.LeaveRoomAsync().GetAwaiter().GetResult();

            Assert.That(session.LeaveCalls, Is.EqualTo(1));
            Assert.That(controller.CurrentState, Is.EqualTo(MultiplayerRoomFlowState.Idle));
            Assert.That(controller.CurrentRoomId, Is.Empty);
            Assert.That(controller.LocalPlayerId, Is.Zero);
            Assert.That(controller.CurrentSnapshot, Is.Null);
        }

        [Test]
        public void LeaveRoom_FailurePreservesIdentityAndLobbyState()
        {
            var provider = new TestSnapshotProvider();
            var session = new TestRoomSession { LeaveException = new InvalidOperationException("leave rejected") };
            using var controller = new MultiplayerRoomFlowController(session, provider);
            controller.StartJoinRoomAsync(CreateLaunchSpec(), "room-a").GetAwaiter().GetResult();
            provider.Publish(new MultiplayerRoomSnapshot
            {
                RoomId = "room-a",
                NumericRoomId = 10,
                Phase = MultiplayerRoomPhase.Lobby
            });

            var exception = Assert.Throws<InvalidOperationException>(() =>
                controller.LeaveRoomAsync().GetAwaiter().GetResult());

            Assert.That(exception!.Message, Is.EqualTo("leave rejected"));
            Assert.That(controller.CurrentState, Is.EqualTo(MultiplayerRoomFlowState.InLobby));
            Assert.That(controller.CurrentRoomId, Is.EqualTo("room-a"));
            Assert.That(controller.LocalPlayerId, Is.EqualTo(7u));
            Assert.That(controller.CurrentSnapshot, Is.Not.Null);
        }

        private static MultiplayerRoomLaunchSpec CreateLaunchSpec()
        {
            return new MultiplayerRoomLaunchSpec
            {
                SessionToken = "session-a",
                Region = "release",
                ServerId = "server-a",
                RoomType = "moba",
                RoomTitle = "MOBA Room",
                MaxPlayers = 2
            };
        }

        private sealed class TestSnapshotProvider : IRoomSnapshotProvider
        {
            public MultiplayerRoomSnapshot? Current { get; private set; }

            public event Action<MultiplayerRoomSnapshot>? OnSnapshotChanged;

            public void Publish(MultiplayerRoomSnapshot snapshot)
            {
                Current = snapshot;
                OnSnapshotChanged?.Invoke(snapshot);
            }
        }

        private sealed class TestRoomSession : IMultiplayerRoomSession
        {
            private readonly uint _playerId;

            public TestRoomSession(uint playerId = 7u)
            {
                _playerId = playerId;
            }

            public int LeaveCalls { get; private set; }
            public Exception? LeaveException { get; set; }

            public Task<MultiplayerRoomRestoreResult> RestoreAsync(
                MultiplayerRoomLaunchSpec spec,
                uint fallbackPlayerId,
                CancellationToken cancellationToken)
            {
                return Task.FromResult(default(MultiplayerRoomRestoreResult));
            }

            public Task<string> CreateRoomAsync(
                MultiplayerRoomLaunchSpec spec,
                CancellationToken cancellationToken)
            {
                return Task.FromResult("room-a");
            }

            public Task<MultiplayerRoomJoinResult> JoinRoomAsync(
                MultiplayerRoomLaunchSpec spec,
                string roomId,
                CancellationToken cancellationToken)
            {
                return Task.FromResult(new MultiplayerRoomJoinResult(
                    roomId,
                    numericRoomId: 10UL,
                    _playerId));
            }

            public Task ConfigureLoadoutAsync(
                string roomId,
                MultiplayerLoadoutSpec loadout,
                CancellationToken cancellationToken)
            {
                return Task.CompletedTask;
            }

            public Task LeaveRoomAsync(string roomId, CancellationToken cancellationToken)
            {
                LeaveCalls++;
                if (LeaveException != null) throw LeaveException;
                return Task.CompletedTask;
            }

            public Task SetReadyAsync(
                string roomId,
                bool ready,
                CancellationToken cancellationToken)
            {
                return Task.CompletedTask;
            }

            public Task BeginLoadingAsync(string roomId, CancellationToken cancellationToken)
            {
                return Task.CompletedTask;
            }

            public Task ReportAssetsLoadedAsync(string roomId, CancellationToken cancellationToken)
            {
                return Task.CompletedTask;
            }

            public Task ReportLoadingProgressAsync(
                string roomId,
                int progress,
                CancellationToken cancellationToken)
            {
                return Task.CompletedTask;
            }

            public Task CancelLoadingAsync(string roomId, CancellationToken cancellationToken)
            {
                return Task.CompletedTask;
            }

            public Task WaitForBattleStartAsync(string roomId, CancellationToken cancellationToken)
            {
                return Task.CompletedTask;
            }
        }
    }
}
