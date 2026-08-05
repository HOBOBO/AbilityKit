#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using AbilityKit.Demo.Common.Rooms;
using AbilityKit.Game.Battle.Agent;
using AbilityKit.Game.Flow;
using AbilityKit.Network.Abstractions;
using AbilityKit.Protocol.Room;
using NUnit.Framework;
using UnityEngine;

namespace AbilityKit.Game.Test.UnitTest
{
    public sealed class FormalMultiplayerFlowTests
    {
        [Test]
        public void FormalLobbyConfig_DefaultsToOwnerInitiatedMatchStart()
        {
            var config = ScriptableObject.CreateInstance<BattleGatewayConfigSO>();
            try
            {
                Assert.That(config.AutoReadyDefaultLoadout, Is.True);
                Assert.That(config.AutoStartWhenReady, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(config);
            }
        }

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
        public void MembershipNotice_WhenOwnerLeaves_ReportsTransferToRemainingMember()
        {
            var notice = FormalLobbyFeature.FormatMembershipNotice(
                new ClientRoomMembershipChange(
                    "room-a",
                    previousRevision: 20,
                    currentRevision: 21,
                    joinedAccountIds: Array.Empty<string>(),
                    leftAccountIds: new[] { "account-owner" },
                    previousOwnerAccountId: "account-owner",
                    currentOwnerAccountId: "account-member"));

            Assert.That(
                notice,
                Is.EqualTo(
                    "account-owner left the room. " +
                    "account-member is now room owner."));
        }

        [Test]
        public void PlayerStateNotice_FormatsReadyOfflineAndReconnectStates()
        {
            var notice = FormalLobbyFeature.FormatPlayerStateNotice(
                new ClientRoomPlayerStateChanges(
                    "room-a",
                    previousRevision: 1,
                    currentRevision: 2,
                    new[]
                    {
                        new ClientRoomPlayerStateChange(
                            "account-a",
                            previousOnline: true,
                            currentOnline: false,
                            previousReady: true,
                            currentReady: false,
                            previousHeroId: 1001,
                            currentHeroId: 1001),
                        new ClientRoomPlayerStateChange(
                            "account-b",
                            previousOnline: false,
                            currentOnline: true,
                            previousReady: false,
                            currentReady: true,
                            previousHeroId: 1002,
                            currentHeroId: 1002)
                    }));

            Assert.That(
                notice,
                Is.EqualTo(
                    "account-a went offline. account-a is no longer ready. " +
                    "account-b reconnected. account-b is ready."));
        }

        [Test]
        public void LobbyPresentation_LiveReadyOwnerCanStart()
        {
            var owner = new MultiplayerRoomPlayerSnapshot
            {
                AccountId = "account-owner",
                PlayerId = 7,
                HeroId = 1001,
                LobbyReady = true,
                IsOnline = true
            };
            var snapshot = new MultiplayerRoomSnapshot
            {
                RoomId = "room-a",
                OwnerAccountId = owner.AccountId,
                Phase = MultiplayerRoomPhase.Lobby,
                RoomRevision = 12,
                CanStart = true,
                Players = new[]
                {
                    owner,
                    new MultiplayerRoomPlayerSnapshot
                    {
                        AccountId = "account-member",
                        PlayerId = 8,
                        HeroId = 1002,
                        LobbyReady = true,
                        IsOnline = true
                    }
                }
            };

            var state = FormalLobbyFeature.BuildLobbyPresentation(
                snapshot,
                owner,
                isLocalRoomOwner: true,
                maxPlayers: 2,
                minPlayers: 2,
                ConnectionState.Connected,
                snapshotIsStale: false,
                lastSnapshotReceivedAtUnixMs: 1000,
                nowUnixMs: 1500);

            Assert.That(state.RoleLabel, Is.EqualTo("Owner"));
            Assert.That(state.ReadyPlayerCount, Is.EqualTo(2));
            Assert.That(state.OnlinePlayerCount, Is.EqualTo(2));
            Assert.That(state.CanStart, Is.True);
            Assert.That(state.ActionStatus, Is.EqualTo("All players are ready."));
            StringAssert.Contains("Live | Revision 12 | just now", state.SyncStatus);
        }

        [Test]
        public void LobbyPresentation_StaleSnapshotDisablesActionsUntilRefresh()
        {
            var owner = new MultiplayerRoomPlayerSnapshot
            {
                AccountId = "account-owner",
                HeroId = 1001,
                LobbyReady = true,
                IsOnline = true
            };
            var snapshot = new MultiplayerRoomSnapshot
            {
                OwnerAccountId = owner.AccountId,
                Phase = MultiplayerRoomPhase.Lobby,
                RoomRevision = 20,
                CanStart = true,
                Players = new[]
                {
                    owner,
                    new MultiplayerRoomPlayerSnapshot
                    {
                        AccountId = "account-member",
                        HeroId = 1002,
                        LobbyReady = true,
                        IsOnline = true
                    }
                }
            };

            var state = FormalLobbyFeature.BuildLobbyPresentation(
                snapshot,
                owner,
                isLocalRoomOwner: true,
                maxPlayers: 2,
                minPlayers: 2,
                ConnectionState.Connected,
                snapshotIsStale: true,
                lastSnapshotReceivedAtUnixMs: 1000,
                nowUnixMs: 2000);

            Assert.That(state.CanStart, Is.False);
            Assert.That(state.ActionStatus, Is.EqualTo("Synchronizing the latest room state."));
            Assert.That(state.SyncStatus, Is.EqualTo("Room updates: Catching up | Revision 20"));
        }

        [Test]
        public void LobbyPresentation_ReadyMemberWaitsForOwner()
        {
            var member = new MultiplayerRoomPlayerSnapshot
            {
                AccountId = "account-member",
                HeroId = 1002,
                LobbyReady = true,
                IsOnline = true
            };
            var snapshot = new MultiplayerRoomSnapshot
            {
                OwnerAccountId = "account-owner",
                Phase = MultiplayerRoomPhase.Lobby,
                RoomRevision = 5,
                CanStart = true,
                Players = new[]
                {
                    new MultiplayerRoomPlayerSnapshot
                    {
                        AccountId = "account-owner",
                        HeroId = 1001,
                        LobbyReady = true,
                        IsOnline = true
                    },
                    member
                }
            };

            var state = FormalLobbyFeature.BuildLobbyPresentation(
                snapshot,
                member,
                isLocalRoomOwner: false,
                maxPlayers: 2,
                minPlayers: 2,
                ConnectionState.Connected,
                snapshotIsStale: false,
                lastSnapshotReceivedAtUnixMs: 1000,
                nowUnixMs: 4000);

            Assert.That(state.RoleLabel, Is.EqualTo("Member"));
            Assert.That(state.CanStart, Is.False);
            Assert.That(state.ActionStatus, Is.EqualTo("Waiting for room owner to start."));
            StringAssert.Contains("3s ago", state.SyncStatus);
        }

        [Test]
        public void LobbyPresentation_OwnerLeavePromotesLocalMemberAndRefreshesStartState()
        {
            var provider = new TestSnapshotProvider();
            using var controller = new MultiplayerRoomFlowController(
                new TestRoomSession(playerId: 8u),
                provider);
            var spec = CreateLaunchSpec();
            spec.AccountId = "account-member";
            controller.StartJoinRoomAsync(spec, "room-a").GetAwaiter().GetResult();

            var localPlayer = new MultiplayerRoomPlayerSnapshot
            {
                AccountId = "account-member",
                PlayerId = 8,
                HeroId = 1002,
                LobbyReady = true,
                IsOnline = true
            };
            provider.Publish(new MultiplayerRoomSnapshot
            {
                RoomId = "room-a",
                OwnerAccountId = "account-owner",
                Phase = MultiplayerRoomPhase.Lobby,
                RoomRevision = 20,
                CanStart = true,
                Players = new[]
                {
                    new MultiplayerRoomPlayerSnapshot
                    {
                        AccountId = "account-owner",
                        PlayerId = 7,
                        HeroId = 1001,
                        LobbyReady = true,
                        IsOnline = true
                    },
                    localPlayer
                }
            });

            Assert.That(controller.IsLocalRoomOwner, Is.False);

            provider.Publish(new MultiplayerRoomSnapshot
            {
                RoomId = "room-a",
                OwnerAccountId = "account-member",
                Phase = MultiplayerRoomPhase.Lobby,
                RoomRevision = 21,
                CanStart = false,
                Players = new[] { localPlayer }
            });
            var state = FormalLobbyFeature.BuildLobbyPresentation(
                controller.CurrentSnapshot!,
                localPlayer,
                controller.IsLocalRoomOwner,
                maxPlayers: 2,
                minPlayers: 2,
                ConnectionState.Connected,
                snapshotIsStale: false,
                lastSnapshotReceivedAtUnixMs: 1000,
                nowUnixMs: 1100);

            Assert.That(controller.IsLocalRoomOwner, Is.True);
            Assert.That(state.RoleLabel, Is.EqualTo("Owner"));
            Assert.That(state.CanStart, Is.False);
            Assert.That(state.ActionStatus, Is.EqualTo("Waiting for players (1/2)."));
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
                Assert.That(spec.AccountId, Is.EqualTo("account-a"));
                Assert.That(spec.Region, Is.EqualTo("release"));
                Assert.That(spec.ServerId, Is.EqualTo("server-a"));
                Assert.That(spec.RoomType, Is.EqualTo("moba"));
                Assert.That(spec.MinPlayers, Is.EqualTo(2));
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
        public void LocalPlayerResolution_PrefersAuthenticatedAccountOverStalePlayerId()
        {
            var expected = new MultiplayerRoomPlayerSnapshot
            {
                AccountId = "account-owner",
                PlayerId = 17,
                HeroId = 1001,
                LobbyReady = true
            };
            var snapshot = new MultiplayerRoomSnapshot
            {
                Players = new[]
                {
                    new MultiplayerRoomPlayerSnapshot
                    {
                        AccountId = "account-member",
                        PlayerId = 7,
                        HeroId = 1002,
                        LobbyReady = true
                    },
                    expected
                }
            };

            var resolved = FormalLobbyFeature.FindLocalPlayer(
                snapshot,
                localPlayerId: 7,
                accountId: "account-owner");

            Assert.That(resolved, Is.SameAs(expected));
        }

        [Test]
        public void AutomaticStart_RequiresConfiguredPlayerCountAndRunsOncePerRoom()
        {
            var snapshot = new MultiplayerRoomSnapshot
            {
                RoomId = "room-a",
                Phase = MultiplayerRoomPhase.Lobby,
                CanStart = true,
                Players = new[]
                {
                    new MultiplayerRoomPlayerSnapshot { PlayerId = 1, LobbyReady = true }
                }
            };

            Assert.That(
                FormalLobbyFeature.ShouldStartAutomatically(
                    enabled: true,
                    MultiplayerRoomFlowState.InLobby,
                    isLocalRoomOwner: true,
                    snapshot,
                    minPlayers: 2,
                    attemptedRoomId: string.Empty,
                    operationBusy: false),
                Is.False,
                "A stale CanStart=true response must not let a one-player room start.");

            snapshot.Players = new[]
            {
                snapshot.Players[0],
                new MultiplayerRoomPlayerSnapshot { PlayerId = 2, LobbyReady = true }
            };
            Assert.That(
                FormalLobbyFeature.ShouldStartAutomatically(
                    enabled: true,
                    MultiplayerRoomFlowState.InLobby,
                    isLocalRoomOwner: true,
                    snapshot,
                    minPlayers: 2,
                    attemptedRoomId: string.Empty,
                    operationBusy: false),
                Is.True);
            Assert.That(
                FormalLobbyFeature.ShouldStartAutomatically(
                    enabled: true,
                    MultiplayerRoomFlowState.InLobby,
                    isLocalRoomOwner: false,
                    snapshot,
                    minPlayers: 2,
                    attemptedRoomId: string.Empty,
                    operationBusy: false),
                Is.False);
            Assert.That(
                FormalLobbyFeature.ShouldStartAutomatically(
                    enabled: true,
                    MultiplayerRoomFlowState.InLobby,
                    isLocalRoomOwner: true,
                    snapshot,
                    minPlayers: 2,
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
                    minPlayers: 2,
                    attemptedRoomId: string.Empty,
                    operationBusy: false),
                Is.False);
        }

        [Test]
        public void LaunchTags_IncludeConfiguredMinimumPlayers()
        {
            var spec = CreateLaunchSpec();

            var tags = GatewayMultiplayerRoomSession.BuildLaunchTags(spec);

            Assert.That(tags[RoomTagKeys.MinPlayers], Is.EqualTo("2"));
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
                MaxPlayers = 2,
                MinPlayers = 2
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
        public void CreatedRoom_UsesAuthenticatedAccountWhenPlayerIdMappingIsStale()
        {
            var provider = new TestSnapshotProvider();
            var session = new TestRoomSession(playerId: 7u);
            using var controller = new MultiplayerRoomFlowController(session, provider);
            var spec = CreateLaunchSpec();
            spec.AccountId = "account-owner";

            controller.StartCreateRoomAsync(spec).GetAwaiter().GetResult();
            provider.Publish(new MultiplayerRoomSnapshot
            {
                RoomId = "room-a",
                NumericRoomId = 10,
                OwnerAccountId = "account-owner",
                Phase = MultiplayerRoomPhase.Lobby,
                CanStart = true,
                Players = new[]
                {
                    new MultiplayerRoomPlayerSnapshot
                    {
                        AccountId = "account-member",
                        PlayerId = 7,
                        LobbyReady = true,
                        HeroId = 1002
                    },
                    new MultiplayerRoomPlayerSnapshot
                    {
                        AccountId = "account-owner",
                        PlayerId = 17,
                        LobbyReady = true,
                        HeroId = 1001
                    }
                }
            });

            Assert.That(controller.LocalPlayerId, Is.EqualTo(7u));
            Assert.That(controller.LocalAccountId, Is.EqualTo("account-owner"));
            Assert.That(controller.IsLocalRoomOwner, Is.True);

            controller.BeginLoadingAsync().GetAwaiter().GetResult();

            Assert.That(session.BeginLoadingCalls, Is.EqualTo(1));
            Assert.That(controller.CurrentState, Is.EqualTo(MultiplayerRoomFlowState.LoadingAssets));
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
                AccountId = "account-a",
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
            public int BeginLoadingCalls { get; private set; }
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
                BeginLoadingCalls++;
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
