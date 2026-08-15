#nullable enable

using System.Collections.Generic;
using AbilityKit.Demo.Shooter.View.PlayMode;
using NUnit.Framework;

namespace AbilityKit.Demo.Shooter.View.Tests
{
    public sealed class ShooterRoomSessionStoreChangeTests
    {
        [Test]
        public void OwnerLeaves_PublishesLeaveAndOwnershipTransfer()
        {
            using var store = new ShooterRoomSessionStore();
            ShooterRoomSessionChange? observed = null;
            store.RoomChanged += change => observed = change;

            Assert.That(store.TryApply(Snapshot(
                revision: 1,
                owner: "owner",
                phase: ShooterRoomSessionPhase.Lobby,
                reason: "Created",
                Member("owner", 1, online: true, ready: true),
                Member("peer", 2, online: true, ready: true))), Is.True);
            Assert.That(store.TryApply(Snapshot(
                revision: 2,
                owner: "peer",
                phase: ShooterRoomSessionPhase.Lobby,
                reason: "MemberLeft",
                Member("peer", 2, online: true, ready: true))), Is.True);

            Assert.That(observed, Is.Not.Null);
            Assert.That(observed!.LeftAccountIds, Is.EqualTo(new[] { "owner" }));
            Assert.That(observed.JoinedAccountIds, Is.Empty);
            Assert.That(observed.OwnerChanged, Is.True);
            Assert.That(observed.PreviousOwnerAccountId, Is.EqualTo("owner"));
            Assert.That(observed.CurrentOwnerAccountId, Is.EqualTo("peer"));
            Assert.That(
                ShooterFormalMultiplayerController.FormatRoomNotice(observed),
                Is.EqualTo("owner left the room. peer is now room owner."));
        }

        [Test]
        public void MemberGoesOfflineAndLosesReady_PublishesBothStateChanges()
        {
            using var store = new ShooterRoomSessionStore();
            ShooterRoomSessionChange? observed = null;
            store.RoomChanged += change => observed = change;
            store.TryApply(Snapshot(
                revision: 10,
                owner: "owner",
                phase: ShooterRoomSessionPhase.Lobby,
                reason: "Ready",
                Member("owner", 1, online: true, ready: true),
                Member("peer", 2, online: true, ready: true)));

            store.TryApply(Snapshot(
                revision: 11,
                owner: "owner",
                phase: ShooterRoomSessionPhase.Lobby,
                reason: "Offline",
                Member("owner", 1, online: true, ready: true),
                Member("peer", 2, online: false, ready: false)));

            Assert.That(observed, Is.Not.Null);
            Assert.That(observed!.MemberChanges, Has.Count.EqualTo(1));
            var member = observed.MemberChanges[0];
            Assert.That(member.AccountId, Is.EqualTo("peer"));
            Assert.That(member.OnlineChanged, Is.True);
            Assert.That(member.CurrentOnline, Is.False);
            Assert.That(member.ReadyChanged, Is.True);
            Assert.That(member.CurrentReady, Is.False);
            Assert.That(
                ShooterFormalMultiplayerController.FormatRoomNotice(observed),
                Is.EqualTo("peer went offline. peer is no longer ready."));
        }

        [Test]
        public void LoadingMemberLeaves_PublishesLobbyRollbackReason()
        {
            using var store = new ShooterRoomSessionStore();
            ShooterRoomSessionChange? observed = null;
            store.RoomChanged += change => observed = change;
            store.TryApply(Snapshot(
                revision: 20,
                owner: "owner",
                phase: ShooterRoomSessionPhase.Loading,
                reason: "BeginLoading",
                Member("owner", 1, online: true, ready: true),
                Member("peer", 2, online: true, ready: true)));

            store.TryApply(Snapshot(
                revision: 21,
                owner: "owner",
                phase: ShooterRoomSessionPhase.Lobby,
                reason: "LockedMemberLeft",
                Member("owner", 1, online: true, ready: true)));

            Assert.That(observed, Is.Not.Null);
            Assert.That(observed!.PreviousPhase, Is.EqualTo(ShooterRoomSessionPhase.Loading));
            Assert.That(observed.CurrentPhase, Is.EqualTo(ShooterRoomSessionPhase.Lobby));
            Assert.That(observed.PhaseChanged, Is.True);
            Assert.That(observed.PhaseReason, Is.EqualTo("LockedMemberLeft"));
            Assert.That(observed.LeftAccountIds, Is.EqualTo(new[] { "peer" }));
            Assert.That(
                ShooterFormalMultiplayerController.FormatRoomNotice(observed),
                Is.EqualTo("peer left the room. Loading was cancelled because a player left."));
        }

        [Test]
        public void DuplicateAndStaleSnapshots_DoNotPublishChanges()
        {
            using var store = new ShooterRoomSessionStore();
            var changeCount = 0;
            store.RoomChanged += _ => changeCount++;
            store.TryApply(Snapshot(
                revision: 5,
                owner: "owner",
                phase: ShooterRoomSessionPhase.Lobby,
                reason: "Created",
                Member("owner", 1, online: true, ready: false)));

            Assert.That(store.TryApply(Snapshot(
                revision: 5,
                owner: "peer",
                phase: ShooterRoomSessionPhase.Lobby,
                reason: "Duplicate",
                Member("peer", 2, online: true, ready: true))), Is.False);
            Assert.That(store.TryApply(Snapshot(
                revision: 4,
                owner: "peer",
                phase: ShooterRoomSessionPhase.Lobby,
                reason: "Stale",
                Member("peer", 2, online: true, ready: true))), Is.False);
            Assert.That(changeCount, Is.Zero);
        }

        private static ShooterGatewayStagedRoomSnapshot Snapshot(
            long revision,
            string owner,
            ShooterRoomSessionPhase phase,
            string reason,
            params ShooterGatewayStagedRoomPlayerSnapshot[] players)
        {
            var anchor = default(ShooterGatewayWorldStartAnchor);
            return new ShooterGatewayStagedRoomSnapshot(
                "room-1",
                (int)phase,
                reason,
                launchGeneration: 1,
                loadingDeadlineUnixMs: 0,
                launchManifestHash: string.Empty,
                launchManifestVersion: 0,
                lastStartFailureCode: string.Empty,
                roomRevision: revision,
                lastEventSequence: revision,
                canStart: false,
                battleId: string.Empty,
                worldId: 0,
                in anchor,
                owner,
                new List<ShooterGatewayStagedRoomPlayerSnapshot>(players));
        }

        private static ShooterGatewayStagedRoomPlayerSnapshot Member(
            string accountId,
            uint playerId,
            bool online,
            bool ready)
        {
            return new ShooterGatewayStagedRoomPlayerSnapshot(
                accountId,
                playerId,
                online,
                ready,
                assetsLoaded: false,
                loadingProgress: 0);
        }
    }
}
