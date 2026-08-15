#nullable enable

using AbilityKit.Demo.Common.Rooms;
using AbilityKit.Demo.Shooter.View.PlayMode;
using NUnit.Framework;
using UnityEditor;

namespace AbilityKit.Demo.Shooter.View.Tests
{
    public sealed class DemoMultiplayerConnectionStateTests
    {
        private const string FormalProfilePath =
            "Packages/com.abilitykit.demo.shooter.view.runtime/Configs/ShooterMultiplayerProfile.asset";

        [Test]
        public void MultiplayerLaunchIntentTransfersAuthenticatedRequestToExpectedGameplay()
        {
            var expected = new DemoMultiplayerLaunchRequest(
                "127.0.0.1",
                4000,
                "dev",
                "local",
                "account-1",
                "session-1",
                System.TimeSpan.FromSeconds(5));

            DemoMultiplayerLaunchIntent.Request(DemoMultiplayerGameplay.Shooter, expected);

            Assert.IsTrue(DemoMultiplayerLaunchIntent.TryConsume(
                DemoMultiplayerGameplay.Shooter,
                out var actual));
            Assert.That(actual, Is.SameAs(expected));
            Assert.IsTrue(actual.IsAuthenticated);
            Assert.IsFalse(DemoMultiplayerLaunchIntent.TryConsume(
                DemoMultiplayerGameplay.Shooter,
                out _));
        }

        [Test]
        public void MultiplayerLaunchIntentRejectsAndClearsMismatchedGameplay()
        {
            var request = new DemoMultiplayerLaunchRequest(
                "127.0.0.1",
                4000,
                "dev",
                "local",
                "account-1",
                "session-1",
                System.TimeSpan.FromSeconds(5));
            DemoMultiplayerLaunchIntent.Request(DemoMultiplayerGameplay.Moba, request);

            Assert.IsFalse(DemoMultiplayerLaunchIntent.TryConsume(
                DemoMultiplayerGameplay.Shooter,
                out _));
            Assert.IsFalse(DemoMultiplayerLaunchIntent.TryConsume(
                DemoMultiplayerGameplay.Moba,
                out _));
        }

        [Test]
        public void FormalMultiplayerProfileBuildsProductionSessionOptions()
        {
            var profile = AssetDatabase.LoadAssetAtPath<ShooterMultiplayerProfileSO>(FormalProfilePath);

            Assert.That(profile, Is.Not.Null, FormalProfilePath);
            Assert.That(profile.RoomTitle, Is.EqualTo("Shooter Room"));
            Assert.That(profile.MaxPlayers, Is.EqualTo(4));
            Assert.That(profile.RoomListLimit, Is.EqualTo(10));
            Assert.That(profile.AutoReady, Is.False);
            Assert.That(profile.AutoStart, Is.False);
            Assert.That(profile.StarterSceneName, Is.EqualTo("MultiplayerStarterScene"));

            var options = profile.BuildSessionOptions();
            Assert.That(options.SyncTemplateId, Is.EqualTo("predict-rollback-authority"));
            Assert.That(options.PlayerCount, Is.EqualTo(2));
            Assert.That(options.ControlledPlayerId, Is.EqualTo(1));
        }

        [Test]
        public void AccountStateCreatesStableUniqueDefaultIdentities()
        {
            var state = new DemoMultiplayerAccountState("unity-account", "unity-guest", "reserved-token");
            var accountId = "unity-account";
            var guestId = "unity-guest";

            state.EnsureUniqueDefaultIdentity(ref accountId, ref guestId);

            Assert.That(accountId, Does.StartWith("unity-account-"));
            Assert.That(guestId, Does.StartWith("unity-guest-"));
            Assert.AreEqual(accountId.Substring("unity-account-".Length), guestId.Substring("unity-guest-".Length));
        }

        [Test]
        public void AccountStateTracksSessionOwnerAndRejectsReservedToken()
        {
            var state = new DemoMultiplayerAccountState("account", "guest", "reserved-token");

            state.RecordLogin("account-a");

            Assert.IsTrue(state.HasSessionToken("session-token", "account-a"));
            Assert.IsFalse(state.HasSessionToken("reserved-token", "account-a"));
            Assert.IsFalse(state.HasSessionToken("session-token", "account-b"));

            state.ClearSession();

            Assert.IsFalse(state.HasSessionToken("session-token", "account-a"));
        }

        [Test]
        public void RoomListStateReplacesRoomsAndMaintainsSelectionBounds()
        {
            var state = new DemoRoomListState<string>();

            state.ReplaceRooms(new[] { "room-a", "room-b", "room-c" }, 12);
            Assert.AreEqual(3, state.Count);
            Assert.AreEqual(12, state.NextOffset);
            Assert.IsTrue(state.TrySelect(2, out var selected));
            Assert.AreEqual("room-c", selected);
            Assert.AreEqual(2, state.SelectedIndex);

            state.ReplaceRooms(new[] { "room-a" }, -1);

            Assert.AreEqual(1, state.Count);
            Assert.AreEqual(0, state.NextOffset);
            Assert.AreEqual(0, state.SelectedIndex);
        }

        [Test]
        public void RoomListStateClearsSelectionForEmptyOrInvalidSelection()
        {
            var state = new DemoRoomListState<string>();

            state.ReplaceRooms(new[] { "room-a" }, 1);
            Assert.IsFalse(state.TrySelect(5, out _));
            Assert.AreEqual(-1, state.SelectedIndex);

            state.ReplaceRooms(System.Array.Empty<string>(), 0);

            Assert.AreEqual(0, state.Count);
            Assert.AreEqual(-1, state.SelectedIndex);
        }
    }
}
