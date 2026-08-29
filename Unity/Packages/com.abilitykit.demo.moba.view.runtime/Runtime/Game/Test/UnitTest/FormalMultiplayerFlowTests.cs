#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using AbilityKit.Demo.Common.Rooms;
using AbilityKit.Game.Battle.Agent;
using AbilityKit.Game.Flow;
using AbilityKit.Network.Abstractions;
using AbilityKit.Network.Room;
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
        public void RestoredIdentity_UsesAuthenticatedAccountSnapshot()
        {
            var snapshot = new RoomGatewaySnapshot
            {
                Players = new[]
                {
                    new RoomGatewayPlayerSnapshot { AccountId = "account-member", PlayerId = 8u },
                    new RoomGatewayPlayerSnapshot { AccountId = "account-owner", PlayerId = 17u },
                }
            };

            var playerId = GatewayMultiplayerRoomSession.ResolveAuthoritativeRestoredPlayerId(
                snapshot,
                "account-owner",
                serverPlayerId: 17u);

            Assert.That(playerId, Is.EqualTo(17u));
        }

        [TestCase(0u)]
        [TestCase(8u)]
        public void RestoredIdentity_MissingOrForeignServerIdentityFailsClosed(uint serverPlayerId)
        {
            var snapshot = new RoomGatewaySnapshot
            {
                Players = new[]
                {
                    new RoomGatewayPlayerSnapshot { AccountId = "account-member", PlayerId = 8u },
                    new RoomGatewayPlayerSnapshot { AccountId = "account-owner", PlayerId = 17u },
                }
            };

            Assert.Throws<InvalidOperationException>(() =>
                GatewayMultiplayerRoomSession.ResolveAuthoritativeRestoredPlayerId(
                    snapshot,
                    "account-owner",
                    serverPlayerId));
        }

        [Test]
        public void DefaultLoadout_UsesSecondHeroForSecondJoinOrdinal()
        {
            var first = new MultiplayerLoadoutSpec(
                1001,
                1,
                0,
                1,
                1001,
                10010001,
                new[] { 10010101, 10010201, 10010301 });
            var second = new MultiplayerLoadoutSpec(
                1002,
                2,
                0,
                1,
                1002,
                10020001,
                new[] { 10020101, 10020201, 10020301 });
            var snapshot = new MultiplayerRoomSnapshot
            {
                Players = new[]
                {
                    new MultiplayerRoomPlayerSnapshot { PlayerId = 7, JoinOrdinal = 1 },
                    new MultiplayerRoomPlayerSnapshot { PlayerId = 8, JoinOrdinal = 2 }
                }
            };

            var ownerLoadout = FormalLobbyFeature.ResolveAvailableDefaultLoadout(
                first,
                second,
                snapshot,
                localPlayerId: 7);
            var memberLoadout = FormalLobbyFeature.ResolveAvailableDefaultLoadout(
                first,
                second,
                snapshot,
                localPlayerId: 8);

            Assert.That(ownerLoadout.HeroId, Is.EqualTo(1001));
            Assert.That(ownerLoadout.TeamId, Is.EqualTo(1));
            Assert.That(memberLoadout.HeroId, Is.EqualTo(1002));
            Assert.That(memberLoadout.TeamId, Is.EqualTo(2));
            Assert.That(memberLoadout.AttributeTemplateId, Is.EqualTo(1002));
            Assert.That(memberLoadout.BasicAttackSkillId, Is.EqualTo(10020001));
            Assert.That(memberLoadout.SkillIds, Is.EqualTo(new[] { 10020101, 10020201, 10020301 }));
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

        [TestCase("LockedMemberLeft", "Loading was cancelled because a player left.")]
        [TestCase("LoadingTimeout", "Loading timed out. The room returned to the lobby.")]
        [TestCase("ManualCancellation", "Loading was cancelled. The room returned to the lobby.")]
        public void PhaseRollbackNotice_FormatsAuthoritativeLobbyReason(
            string phaseReason,
            string expected)
        {
            var notice = FormalLobbyFeature.FormatPhaseRollbackNotice(
                new ClientRoomSnapshot { Phase = ClientRoomPhase.Loading },
                new ClientRoomSnapshot
                {
                    Phase = ClientRoomPhase.Lobby,
                    PhaseReason = phaseReason
                });

            Assert.That(notice, Is.EqualTo(expected));
        }

        [Test]
        public void PhaseRollbackNotice_NonRollbackDoesNotPublishNotice()
        {
            Assert.That(
                FormalLobbyFeature.FormatPhaseRollbackNotice(
                    new ClientRoomSnapshot { Phase = ClientRoomPhase.Lobby },
                    new ClientRoomSnapshot { Phase = ClientRoomPhase.Lobby }),
                Is.Empty);
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
            Assert.That(state.CanReady, Is.False);
            Assert.That(state.CanNotReady, Is.True);
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
            Assert.That(state.CanNotReady, Is.False);
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
            Assert.That(state.CanReady, Is.False);
            Assert.That(state.CanNotReady, Is.True);
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
                Assert.That(spec.SyncTemplateId, Is.EqualTo("frame-sync-authority"));
                Assert.That(spec.SyncModel, Is.EqualTo(1));

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
        public void LaunchSpec_ProjectsFrameSyncTemplateAndModelToGateway()
        {
            var spec = CreateLaunchSpec();
            spec.SyncTemplateId = "frame-sync-authority";
            spec.SyncModel = 1;

            var projected = GatewayRoomProtocolMapper.ToLaunchSpec(spec);

            Assert.That(projected.SyncTemplateId, Is.EqualTo("frame-sync-authority"));
            Assert.That(projected.SyncModel, Is.EqualTo(1));
            Assert.That(projected.Tags, Is.Not.Null);
            Assert.That(projected.Tags!["syncTemplateId"], Is.EqualTo("frame-sync-authority"));
            Assert.That(projected.Tags["syncModel"], Is.EqualTo("1"));
        }

        [Test]
        public void GatewayMembership_InvalidCommit_PreservesPreviousIdentity()
        {
            var membership = new GatewayRoomMembership();
            membership.Commit("room-a", 10UL, 7u);

            Assert.Throws<InvalidOperationException>(() =>
                membership.Commit("room-b", 11UL, 0u));

            Assert.That(membership.RoomId, Is.EqualTo("room-a"));
            Assert.That(membership.NumericRoomId, Is.EqualTo(10UL));
            Assert.That(membership.PlayerId, Is.EqualTo(7u));
        }

        [Test]
        public void GatewayMembership_Clear_ResetsIdentityTuple()
        {
            var membership = new GatewayRoomMembership();
            membership.Commit("room-a", 10UL, 7u);

            membership.Clear();

            Assert.That(membership.RoomId, Is.Empty);
            Assert.That(membership.NumericRoomId, Is.Zero);
            Assert.That(membership.PlayerId, Is.Zero);
        }

        [Test]
        public void CheckpointStore_InvalidSave_PreservesValidCheckpoint()
        {
            var store = new MobaReliableBattleEventCheckpointStore();
            var expected = new MobaReliableBattleEventCheckpoint("battle-a", "epoch-a", 12L);
            store.Save(in expected);
            var invalid = default(MobaReliableBattleEventCheckpoint);

            store.Save(in invalid);

            Assert.That(store.TryLoad("battle-a", out var actual), Is.True);
            Assert.That(actual.BattleId, Is.EqualTo("battle-a"));
            Assert.That(actual.Epoch, Is.EqualTo("epoch-a"));
            Assert.That(actual.LastAcknowledgedSequence, Is.EqualTo(12L));
        }

        [Test]
        public void CheckpointStore_BattleIdMatch_IsOrdinalAndExact()
        {
            var store = new MobaReliableBattleEventCheckpointStore();
            var checkpoint = new MobaReliableBattleEventCheckpoint("battle-a", "epoch-a", 1L);
            store.Save(in checkpoint);

            Assert.That(store.TryLoad("battle-a", out _), Is.True);
            Assert.That(store.TryLoad("BATTLE-A", out _), Is.False);
            Assert.That(store.TryLoad("battle-b", out _), Is.False);
        }

        [TestCase((int)RoomGatewayStagedRestoreNextStep.SetReadyAndBeginLoading, MultiplayerRoomRestoreNextStep.SetReadyAndBeginLoading)]
        [TestCase((int)RoomGatewayStagedRestoreNextStep.ReportAssetsLoaded, MultiplayerRoomRestoreNextStep.ReportAssetsLoaded)]
        [TestCase((int)RoomGatewayStagedRestoreNextStep.WaitForBattleStart, MultiplayerRoomRestoreNextStep.WaitForBattleStart)]
        [TestCase((int)RoomGatewayStagedRestoreNextStep.SubscribeStateSync, MultiplayerRoomRestoreNextStep.EnterBattle)]
        [TestCase(999, MultiplayerRoomRestoreNextStep.None)]
        public void GatewayMapper_NextStepMapping_IsExplicit(
            int wireValue,
            MultiplayerRoomRestoreNextStep expected)
        {
            Assert.That(
                GatewayRoomProtocolMapper.ToNextStep((RoomGatewayStagedRestoreNextStep)wireValue),
                Is.EqualTo(expected));
        }

        [Test]
        public void GatewayMapper_UnknownRestoreEnums_FallBackSafely()
        {
            Assert.That(
                GatewayRoomProtocolMapper.ToEntryKind((RoomGatewaySessionEntryKind)999),
                Is.EqualTo(MultiplayerRoomEntryKind.TeamLobby));
            Assert.That(
                GatewayRoomProtocolMapper.ToRestoreStatus((RoomGatewaySessionRestoreStatus)999),
                Is.EqualTo(MultiplayerRoomRestoreStatus.Restored));
            Assert.That(
                GatewayRoomProtocolMapper.ToRestoreErrorCode((RoomGatewaySessionRestoreErrorCode)999),
                Is.EqualTo(MultiplayerRoomRestoreErrorCode.None));
        }

        [Test]
        public void GatewayMapper_SnapshotProjection_DeepCopiesCollections()
        {
            var members = new[] { "account-a" };
            var skillIds = new[] { 1001, 1002 };
            var players = new[]
            {
                new RoomGatewayPlayerSnapshot
                {
                    AccountId = "account-a",
                    PlayerId = 7u,
                    SkillIds = skillIds,
                    LobbyReady = true
                }
            };
            var source = new RoomGatewaySnapshot
            {
                RoomId = "room-a",
                Members = members,
                Players = players,
                RoomRevision = 3L
            };

            var projected = GatewayRoomProtocolMapper.ToClientSnapshot(source, 10UL);
            members[0] = "mutated-account";
            skillIds[0] = 9999;
            players[0] = new RoomGatewayPlayerSnapshot { AccountId = "replacement" };

            Assert.That(projected.NumericRoomId, Is.EqualTo(10UL));
            Assert.That(projected.Members[0], Is.EqualTo("account-a"));
            Assert.That(projected.Players[0].AccountId, Is.EqualTo("account-a"));
            Assert.That(projected.Players[0].SkillIds[0], Is.EqualTo(1001));
            Assert.That(projected.Players[0].Ready, Is.True);
        }

        [Test]
        public void GatewayMapper_NullSnapshot_IsRejected()
        {
            Assert.Throws<ArgumentNullException>(() =>
                GatewayRoomProtocolMapper.ToClientSnapshot(null!, 10UL));
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
            Assert.That(FormalLobbyFeature.ShouldRunAutomaticLobbyActions(request), Is.False);
            Assert.That(FormalLobbyFeature.ShouldRunAutomaticLobbyActions(null), Is.True);
        }

        [Test]
        public void BattleEntry_WaitsForRestoreResultWhenInBattleSnapshotArrivesEarly()
        {
            Assert.That(
                FormalLobbyFeature.ShouldDeferBattleEntryForRestore(
                    restoreRoomOnEntry: true,
                    initializationStarted: true,
                    operationBusy: true,
                    restoreResult: null),
                Is.True,
                "An early InBattle snapshot must not consume the battle-entry gate before restore metadata arrives.");

            var restored = new MultiplayerRoomRestoreResult(
                "room-a",
                numericRoomId: 10UL,
                playerId: 7u,
                MultiplayerRoomPhase.InBattle,
                MultiplayerRoomRestoreNextStep.EnterBattle,
                MultiplayerRoomEntryKind.Reconnect,
                canStart: false,
                message: string.Empty,
                MultiplayerRoomRestoreStatus.Restored,
                MultiplayerRoomRestoreErrorCode.None);

            Assert.That(
                FormalLobbyFeature.ShouldDeferBattleEntryForRestore(
                    restoreRoomOnEntry: true,
                    initializationStarted: true,
                    operationBusy: true,
                    restoreResult: restored),
                Is.False);
            Assert.That(FormalLobbyFeature.ShouldUseColdStartRecovery(restored), Is.True);
        }

        [Test]
        public void BattleEntry_NonRestoreFlowIsNotBlockedByAnotherLobbyOperation()
        {
            Assert.That(
                FormalLobbyFeature.ShouldDeferBattleEntryForRestore(
                    restoreRoomOnEntry: false,
                    initializationStarted: true,
                    operationBusy: true,
                    restoreResult: null),
                Is.False);
            Assert.That(FormalLobbyFeature.ShouldUseColdStartRecovery(null), Is.False);
        }

        [Test]
        public void RestoredActiveBattle_UsesColdRecoveryEvenWhenLegacyEntryKindIsTeamLobby()
        {
            var restored = new MultiplayerRoomRestoreResult(
                "room-a",
                numericRoomId: 10UL,
                playerId: 7u,
                MultiplayerRoomPhase.InBattle,
                MultiplayerRoomRestoreNextStep.EnterBattle,
                MultiplayerRoomEntryKind.TeamLobby,
                canStart: false,
                message: string.Empty,
                MultiplayerRoomRestoreStatus.Restored,
                MultiplayerRoomRestoreErrorCode.None);

            Assert.That(FormalLobbyFeature.ShouldUseColdStartRecovery(restored), Is.True,
                "A new process restoring an active battle always needs frame-0 lockstep replay; entry-kind metadata must not disable recovery.");
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

        [TestCase("LockedMemberLeft", "")]
        [TestCase("LoadingTimeout", "Room loading timed out before all players finished loading.")]
        public void AuthoritativeLoadingRollback_ReturnsActiveFlowToLobby(
            string phaseReason,
            string expectedError)
        {
            var provider = new TestSnapshotProvider();
            using var controller = new MultiplayerRoomFlowController(
                new TestRoomSession(),
                provider);
            controller.StartJoinRoomAsync(CreateLaunchSpec(), "room-a").GetAwaiter().GetResult();
            provider.Publish(new MultiplayerRoomSnapshot
            {
                RoomId = "room-a",
                NumericRoomId = 10,
                Phase = MultiplayerRoomPhase.Loading,
                LaunchGeneration = 1
            });

            provider.Publish(new MultiplayerRoomSnapshot
            {
                RoomId = "room-a",
                NumericRoomId = 10,
                Phase = MultiplayerRoomPhase.Lobby,
                PhaseReason = phaseReason,
                LaunchGeneration = 1
            });

            Assert.That(controller.CurrentState, Is.EqualTo(MultiplayerRoomFlowState.InLobby));
            Assert.That(controller.LocalLoadingProgress, Is.Zero);
            Assert.That(controller.CurrentLoadingAssetKey, Is.Empty);
            Assert.That(controller.LastError, Is.EqualTo(expectedError));
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

        [Test]
        public void StageRuntime_SameGeneration_ReusesRunningTask()
        {
            using var runtime = new MultiplayerRoomStageRuntime();
            var completion = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var runCount = 0;

            var first = runtime.ResumeAsync(7, _ =>
            {
                runCount++;
                return completion.Task;
            });
            var second = runtime.ResumeAsync(7, _ =>
            {
                runCount++;
                return Task.CompletedTask;
            });

            Assert.That(second, Is.SameAs(first));
            Assert.That(runCount, Is.EqualTo(1));
            completion.SetResult(true);
            first.GetAwaiter().GetResult();
        }

        [Test]
        public void StageRuntime_NewGeneration_CancelsAndWaitsForPreviousStage()
        {
            VerifyNewGenerationCancelsAndWaitsForPreviousStageAsync()
                .GetAwaiter()
                .GetResult();
        }

        private static async Task VerifyNewGenerationCancelsAndWaitsForPreviousStageAsync()
        {
            using var runtime = new MultiplayerRoomStageRuntime();
            var started = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var canceled = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var release = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var nextStarted = false;

            var previous = runtime.ResumeAsync(1, async ct =>
            {
                using var registration = ct.Register(() => canceled.TrySetResult(true));
                started.SetResult(true);
                await release.Task.ConfigureAwait(false);
                ct.ThrowIfCancellationRequested();
            });
            await started.Task.ConfigureAwait(false);

            var next = runtime.ResumeAsync(2, _ =>
            {
                nextStarted = true;
                return Task.CompletedTask;
            });
            await canceled.Task.ConfigureAwait(false);

            Assert.That(nextStarted, Is.False);
            release.SetResult(true);
            await next.ConfigureAwait(false);
            Assert.That(nextStarted, Is.True);
            Assert.That(previous.IsCanceled, Is.True);
        }

        [Test]
        public void AssetRuntime_ProgressIsMonotonicAndCancelReleasesAssets()
        {
            var loader = new TestAssetLoader();
            var runtime = new MultiplayerAssetLoadingRuntime(loader);
            var snapshot = new MultiplayerRoomSnapshot
            {
                RoomId = "room-a",
                Phase = MultiplayerRoomPhase.Loading,
                LaunchGeneration = 3
            };

            runtime.LoadAsync(
                    snapshot,
                    (_, _) => Task.CompletedTask,
                    CancellationToken.None)
                .GetAwaiter()
                .GetResult();

            Assert.That(runtime.Progress, Is.EqualTo(100));
            Assert.That(runtime.CurrentAssetKey, Is.EqualTo("asset-a"));

            runtime.Cancel(releaseAssets: true);

            Assert.That(runtime.Progress, Is.Zero);
            Assert.That(runtime.CurrentAssetKey, Is.Empty);
            Assert.That(loader.ReleaseCalls, Is.EqualTo(1));
        }

        [Test]
        public void AssetRuntime_CancelRejectsLateProgressAndCancelsUpload()
        {
            VerifyCancelRejectsLateProgressAndCancelsUploadAsync()
                .GetAwaiter()
                .GetResult();
        }

        private static async Task VerifyCancelRejectsLateProgressAndCancelsUploadAsync()
        {
            var loader = new ControllableAssetLoader();
            var runtime = new MultiplayerAssetLoadingRuntime(loader);
            var uploadCalls = 0;
            var snapshot = new MultiplayerRoomSnapshot
            {
                RoomId = "room-a",
                Phase = MultiplayerRoomPhase.Loading,
                LaunchGeneration = 4
            };

            var loading = runtime.LoadAsync(
                snapshot,
                (_, _) =>
                {
                    uploadCalls++;
                    return Task.CompletedTask;
                },
                CancellationToken.None);
            runtime.Cancel(releaseAssets: true);
            loader.Report(new MultiplayerAssetLoadProgress(80, 2, 2, "asset-late"));

            Assert.That(runtime.Progress, Is.Zero);
            Assert.That(runtime.CurrentAssetKey, Is.Empty);
            Assert.That(uploadCalls, Is.Zero);
            Assert.That(loader.ReleaseCalls, Is.EqualTo(1));

            loader.Complete();
            try
            {
                await loading.ConfigureAwait(false);
                Assert.Fail("Canceled loading should not complete successfully.");
            }
            catch (OperationCanceledException)
            {
            }
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

        private sealed class TestAssetLoader : IMultiplayerBattleAssetLoader
        {
            public int ReleaseCalls { get; private set; }

            public Task LoadAsync(
                MultiplayerRoomSnapshot snapshot,
                IProgress<MultiplayerAssetLoadProgress> progress,
                CancellationToken cancellationToken)
            {
                progress.Report(new MultiplayerAssetLoadProgress(60, 1, 2, "asset-a"));
                progress.Report(new MultiplayerAssetLoadProgress(20, 1, 2, "asset-stale"));
                return Task.CompletedTask;
            }

            public void Release()
            {
                ReleaseCalls++;
            }
        }

        private sealed class ControllableAssetLoader : IMultiplayerBattleAssetLoader
        {
            private readonly TaskCompletionSource<bool> _completion =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            private IProgress<MultiplayerAssetLoadProgress>? _progress;

            public int ReleaseCalls { get; private set; }

            public Task LoadAsync(
                MultiplayerRoomSnapshot snapshot,
                IProgress<MultiplayerAssetLoadProgress> progress,
                CancellationToken cancellationToken)
            {
                _progress = progress;
                return _completion.Task;
            }

            public void Report(MultiplayerAssetLoadProgress progress)
            {
                _progress?.Report(progress);
            }

            public void Complete()
            {
                _completion.TrySetResult(true);
            }

            public void Release()
            {
                ReleaseCalls++;
            }
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
