using System.Collections.Generic;
using AbilityKit.Game.Battle.Agent;
using NUnit.Framework;

namespace AbilityKit.Game.Test.UnitTest
{
    public sealed class ClientRoomStoreTests
    {
        private static ClientRoomSnapshot NewSnapshot(long revision, long eventSequence, string roomId = "room-1")
        {
            return new ClientRoomSnapshot
            {
                RoomId = roomId,
                Phase = ClientRoomPhase.Lobby,
                RoomRevision = revision,
                LastEventSequence = eventSequence
            };
        }

        private static ClientRoomSnapshot NewMembershipSnapshot(
            long revision,
            string ownerAccountId,
            params string[] members)
        {
            var snapshot = NewSnapshot(revision, revision);
            snapshot.OwnerAccountId = ownerAccountId;
            snapshot.Members = members;
            return snapshot;
        }

        private static ClientRoomPlayer NewPlayer(
            string accountId,
            bool online,
            bool ready,
            int heroId)
        {
            return new ClientRoomPlayer
            {
                AccountId = accountId,
                IsOnline = online,
                LobbyReady = ready,
                HeroId = heroId
            };
        }

        [Test]
        public void FirstApply_SucceedsAndPublishes()
        {
            var store = new ClientRoomStore();
            var published = new List<ClientRoomSnapshot>();
            store.OnSnapshotChanged += s => published.Add(s);

            var snapshot = NewSnapshot(1, 1);

            var result = store.ApplySnapshot(snapshot);

            Assert.AreEqual(ClientRoomSnapshotApplyResult.Applied, result);
            Assert.AreSame(snapshot, store.Current);
            Assert.AreEqual(1, published.Count);
            Assert.IsFalse(store.IsStale);
        }

        [Test]
        public void FirstApply_HighEventSequenceEstablishesCompleteSnapshotBaseline()
        {
            var store = new ClientRoomStore();

            var result = store.ApplySnapshot(NewSnapshot(20, 20));

            Assert.AreEqual(ClientRoomSnapshotApplyResult.Applied, result);
            Assert.AreEqual(20, store.Current.LastEventSequence);
            Assert.IsFalse(store.IsStale);
        }

        [Test]
        public void ApplyOldRevision_IsIgnored()
        {
            var store = new ClientRoomStore();
            store.ApplySnapshot(NewSnapshot(5, 5));

            var result = store.ApplySnapshot(NewSnapshot(3, 3));

            Assert.AreEqual(ClientRoomSnapshotApplyResult.StaleIgnored, result);
            Assert.AreEqual(5, store.Current.RoomRevision);
        }

        [Test]
        public void ApplyDuplicateRevision_IsIdempotent()
        {
            var store = new ClientRoomStore();
            var published = new List<ClientRoomSnapshot>();
            store.OnSnapshotChanged += s => published.Add(s);

            store.ApplySnapshot(NewSnapshot(5, 5));
            var result = store.ApplySnapshot(NewSnapshot(5, 5));

            Assert.AreEqual(ClientRoomSnapshotApplyResult.DuplicateIgnored, result);
            Assert.AreEqual(1, published.Count);
        }

        [Test]
        public void ApplyDuplicateRevision_WithNumericRoomId_PublishesMetadataCompletion()
        {
            var store = new ClientRoomStore();
            var published = new List<ClientRoomSnapshot>();
            store.OnSnapshotChanged += snapshot => published.Add(snapshot);
            store.ApplySnapshot(NewSnapshot(5, 5));
            var completed = NewSnapshot(5, 5);
            completed.NumericRoomId = 9001UL;

            var result = store.ApplySnapshot(completed);

            Assert.AreEqual(ClientRoomSnapshotApplyResult.Applied, result);
            Assert.AreEqual(9001UL, store.Current.NumericRoomId);
            Assert.AreEqual(2, published.Count);
        }

        [Test]
        public void ApplyNewRevision_InheritsNumericRoomIdFromSameRoom()
        {
            var store = new ClientRoomStore();
            var initial = NewSnapshot(5, 5);
            initial.NumericRoomId = 9001UL;
            store.ApplySnapshot(initial);

            var result = store.ApplySnapshot(NewSnapshot(6, 6));

            Assert.AreEqual(ClientRoomSnapshotApplyResult.Applied, result);
            Assert.AreEqual(6, store.Current.RoomRevision);
            Assert.AreEqual(9001UL, store.Current.NumericRoomId);
        }

        [Test]
        public void ApplyNewRevision_IsAccepted()
        {
            var store = new ClientRoomStore();
            store.ApplySnapshot(NewSnapshot(5, 5));

            var result = store.ApplySnapshot(NewSnapshot(6, 6));

            Assert.AreEqual(ClientRoomSnapshotApplyResult.Applied, result);
            Assert.AreEqual(6, store.Current.RoomRevision);
        }

        [Test]
        public void EventSequenceGap_MarksStale()
        {
            var store = new ClientRoomStore();
            store.ApplySnapshot(NewSnapshot(5, 5));

            // 期望 next = 6，实际 8 -> 缺口
            store.ApplySnapshot(NewSnapshot(6, 8));

            Assert.IsTrue(store.IsStale);
        }

        [Test]
        public void MarkRefreshed_ClearsStale()
        {
            var store = new ClientRoomStore();
            store.ApplySnapshot(NewSnapshot(5, 5));
            store.ApplySnapshot(NewSnapshot(6, 8));
            Assert.IsTrue(store.IsStale);

            store.MarkRefreshed();

            Assert.IsFalse(store.IsStale);
        }

        [Test]
        public void OnSnapshotChanged_FiresOnlyOnApplied()
        {
            var store = new ClientRoomStore();
            var published = new List<ClientRoomSnapshot>();
            store.OnSnapshotChanged += s => published.Add(s);

            store.ApplySnapshot(NewSnapshot(1, 1));
            store.ApplySnapshot(NewSnapshot(1, 1)); // duplicate
            store.ApplySnapshot(NewSnapshot(0, 0)); // stale
            store.ApplySnapshot(NewSnapshot(2, 2)); // applied

            Assert.AreEqual(2, published.Count);
            Assert.AreEqual(2, published[1].RoomRevision);
        }

        [Test]
        public void ApplyNewRevision_WhenMemberLeaves_PublishesMembershipChangeOnce()
        {
            var store = new ClientRoomStore();
            var changes = new List<ClientRoomMembershipChange>();
            store.OnMembershipChanged += change => changes.Add(change);
            store.ApplySnapshot(NewMembershipSnapshot(1, "account-a", "account-a", "account-b"));

            store.ApplySnapshot(NewMembershipSnapshot(2, "account-a", "account-a"));

            Assert.AreEqual(1, changes.Count);
            Assert.AreEqual("room-1", changes[0].RoomId);
            Assert.AreEqual(1, changes[0].PreviousRevision);
            Assert.AreEqual(2, changes[0].CurrentRevision);
            CollectionAssert.AreEqual(new[] { "account-b" }, changes[0].LeftAccountIds);
            CollectionAssert.IsEmpty(changes[0].JoinedAccountIds);
            Assert.IsFalse(changes[0].OwnerChanged);
        }

        [Test]
        public void ApplyNewRevision_WhenMemberJoinsAndOwnerChanges_PublishesOneCombinedChange()
        {
            var store = new ClientRoomStore();
            ClientRoomMembershipChange published = null;
            store.OnMembershipChanged += change => published = change;
            store.ApplySnapshot(NewMembershipSnapshot(1, "account-a", "account-a"));

            store.ApplySnapshot(NewMembershipSnapshot(2, "account-b", "account-a", "account-b"));

            Assert.IsNotNull(published);
            CollectionAssert.AreEqual(new[] { "account-b" }, published.JoinedAccountIds);
            CollectionAssert.IsEmpty(published.LeftAccountIds);
            Assert.AreEqual("account-a", published.PreviousOwnerAccountId);
            Assert.AreEqual("account-b", published.CurrentOwnerAccountId);
            Assert.IsTrue(published.OwnerChanged);
        }

        [Test]
        public void ApplyNewRevision_WhenOwnerLeaves_PublishesLeaveAndOwnerTransfer()
        {
            var store = new ClientRoomStore();
            ClientRoomMembershipChange published = null;
            store.OnMembershipChanged += change => published = change;
            store.ApplySnapshot(NewMembershipSnapshot(
                10,
                "account-owner",
                "account-owner",
                "account-member"));

            store.ApplySnapshot(NewMembershipSnapshot(
                11,
                "account-member",
                "account-member"));

            Assert.IsNotNull(published);
            CollectionAssert.AreEqual(new[] { "account-owner" }, published.LeftAccountIds);
            Assert.AreEqual("account-owner", published.PreviousOwnerAccountId);
            Assert.AreEqual("account-member", published.CurrentOwnerAccountId);
            Assert.IsTrue(published.OwnerChanged);
        }

        [Test]
        public void ApplyNewRevision_PublishesReadyOfflineAndReconnectChanges()
        {
            var store = new ClientRoomStore();
            var published = new List<ClientRoomPlayerStateChanges>();
            store.OnPlayerStateChanged += changes => published.Add(changes);
            var initial = NewMembershipSnapshot(1, "account-owner", "account-owner", "account-member");
            initial.Players = new[]
            {
                NewPlayer("account-owner", online: true, ready: true, heroId: 1001),
                NewPlayer("account-member", online: true, ready: false, heroId: 1002)
            };
            store.ApplySnapshot(initial);

            var ready = NewMembershipSnapshot(2, "account-owner", "account-owner", "account-member");
            ready.Players = new[]
            {
                NewPlayer("account-owner", online: true, ready: true, heroId: 1001),
                NewPlayer("account-member", online: true, ready: true, heroId: 1002)
            };
            store.ApplySnapshot(ready);

            var offline = NewMembershipSnapshot(3, "account-owner", "account-owner", "account-member");
            offline.Players = new[]
            {
                NewPlayer("account-owner", online: true, ready: true, heroId: 1001),
                NewPlayer("account-member", online: false, ready: true, heroId: 1002)
            };
            store.ApplySnapshot(offline);

            var reconnected = NewMembershipSnapshot(4, "account-owner", "account-owner", "account-member");
            reconnected.Players = ready.Players;
            store.ApplySnapshot(reconnected);

            Assert.AreEqual(3, published.Count);
            Assert.IsTrue(published[0].Changes[0].ReadyChanged);
            Assert.IsTrue(published[0].Changes[0].CurrentReady);
            Assert.IsTrue(published[1].Changes[0].OnlineChanged);
            Assert.IsFalse(published[1].Changes[0].CurrentOnline);
            Assert.IsTrue(published[1].Changes[0].ReadyChanged);
            Assert.IsTrue(published[2].Changes[0].OnlineChanged);
            Assert.IsTrue(published[2].Changes[0].CurrentOnline);
            Assert.IsTrue(published[2].Changes[0].CurrentReady);
        }

        [Test]
        public void FirstSnapshot_DoesNotPublishMembershipChange()
        {
            var store = new ClientRoomStore();
            var published = 0;
            store.OnMembershipChanged += _ => published++;

            store.ApplySnapshot(NewMembershipSnapshot(1, "account-a", "account-a"));

            Assert.AreEqual(0, published);
        }

        [Test]
        public void DuplicateStaleAndMetadataCompletion_DoNotPublishMembershipChange()
        {
            var store = new ClientRoomStore();
            var published = 0;
            store.OnMembershipChanged += _ => published++;
            store.ApplySnapshot(NewMembershipSnapshot(5, "account-a", "account-a"));

            store.ApplySnapshot(NewMembershipSnapshot(5, "account-b", "account-b"));
            store.ApplySnapshot(NewMembershipSnapshot(4, "account-b", "account-b"));
            var metadataCompletion = NewMembershipSnapshot(5, "account-b", "account-b");
            metadataCompletion.NumericRoomId = 9001UL;
            store.ApplySnapshot(metadataCompletion);

            Assert.AreEqual(0, published);
        }

        [Test]
        public void SnapshotForDifferentRoom_DoesNotPublishMembershipChange()
        {
            var store = new ClientRoomStore();
            var published = 0;
            store.OnMembershipChanged += _ => published++;
            store.ApplySnapshot(NewMembershipSnapshot(1, "account-a", "account-a"));
            var otherRoom = NewMembershipSnapshot(2, "account-b", "account-b");
            otherRoom.RoomId = "room-2";

            var result = store.ApplySnapshot(otherRoom);

            Assert.AreEqual(ClientRoomSnapshotApplyResult.Applied, result);
            Assert.AreEqual(0, published);
        }
    }
}
