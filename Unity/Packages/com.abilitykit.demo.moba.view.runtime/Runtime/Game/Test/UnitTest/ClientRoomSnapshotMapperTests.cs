using System.Collections.Generic;
using System.Linq;
using AbilityKit.Game.Battle.Agent;
using AbilityKit.Protocol.Room;
using NUnit.Framework;

namespace AbilityKit.Game.Test.UnitTest
{
    public sealed class ClientRoomSnapshotMapperTests
    {
        [Test]
        public void ToClientSnapshot_MapsAllFields()
        {
            var wire = new WireRoomSnapshot
            {
                Summary = new WireRoomSummary { RoomId = "room-7", Tags = new Dictionary<string, string>
                    { ["tagBundleVersion"] = "bundle-7", [RoomTagKeys.GameplayId] = "2" } },
                Members = new List<string> { "acc-1", "acc-2" },
                Players = new List<WireRoomPlayerSnapshot>
                {
                    new WireRoomPlayerSnapshot
                    {
                        AccountId = "acc-1",
                        TeamId = 1,
                        HeroId = 1001,
                        PlayerId = 7u,
                        LobbyReady = true,
                        AssetsLoaded = true,
                        IsOnline = true,
                        JoinOrdinal = 3L,
                        LoadedManifestVersion = 2,
                        LoadedManifestHash = "hash-1"
                    }
                },
                CanStart = true,
                BattleId = "battle-9",
                WorldId = 9001ul,
                WorldStartAnchor = new WireWorldStartAnchor
                {
                    StartServerTicks = 100L,
                    ServerTickFrequency = 30L,
                    StartFrame = 5,
                    FixedDeltaSeconds = 0.033
                },
                RoomRevision = 42L,
                LastEventSequence = 17L,
                Phase = 1,
                PhaseReason = "loading-started",
                LaunchGeneration = 9L,
                LoadingDeadlineUnixMs = 1234567890L,
                LaunchManifestHash = "manifest-hash",
                LaunchManifestVersion = 3,
                LastStartFailureCode = "none"
            };

            var snapshot = ClientRoomSnapshotMapper.ToClientSnapshot(wire);

            Assert.AreEqual("room-7", snapshot.RoomId);
            Assert.AreEqual(ClientRoomPhase.Loading, snapshot.Phase);
            Assert.AreEqual("loading-started", snapshot.PhaseReason);
            Assert.AreEqual(9L, snapshot.LaunchGeneration);
            Assert.AreEqual(1234567890L, snapshot.LoadingDeadlineUnixMs);
            Assert.AreEqual("manifest-hash", snapshot.LaunchManifestHash);
            Assert.AreEqual("bundle-7", snapshot.TagBundleVersion);
            Assert.AreEqual(2, snapshot.GameplayId);
            Assert.AreEqual(2, new ClientRoomSnapshotAssetSource(snapshot).GameplayId);
            Assert.AreEqual(3, snapshot.LaunchManifestVersion);
            Assert.AreEqual("none", snapshot.LastStartFailureCode);
            Assert.AreEqual(42L, snapshot.RoomRevision);
            Assert.AreEqual(17L, snapshot.LastEventSequence);
            Assert.IsTrue(snapshot.CanStart);
            Assert.AreEqual("battle-9", snapshot.BattleId);
            Assert.AreEqual(9001ul, snapshot.WorldId);
            Assert.AreEqual(2, snapshot.Members.Count);
            Assert.AreEqual("acc-1", snapshot.Members[0]);
            Assert.AreEqual(100L, snapshot.WorldStartAnchor.StartServerTicks);
            Assert.AreEqual(30L, snapshot.WorldStartAnchor.ServerTickFrequency);
            Assert.AreEqual(5, snapshot.WorldStartAnchor.StartFrame);
            Assert.AreEqual(0.033, snapshot.WorldStartAnchor.FixedDeltaSeconds, 0.0001);
        }

        [Test]
        public void ToClientPlayer_MapsAllFields()
        {
            var wire = new WireRoomPlayerSnapshot
            {
                AccountId = "acc-2",
                TeamId = 2,
                Ready = true,
                HeroId = 2002,
                SpawnPointId = 4,
                Level = 5,
                AttributeTemplateId = 7,
                BasicAttackSkillId = 900,
                SkillIds = new List<int> { 1, 2, 3 },
                PlayerId = 9u,
                LobbyReady = true,
                AssetsLoaded = false,
                IsOnline = true,
                JoinOrdinal = 1L,
                LoadedManifestVersion = 2,
                LoadedManifestHash = "hash-2"
            };

            var player = ClientRoomSnapshotMapper.ToClientPlayer(wire);

            Assert.AreEqual("acc-2", player.AccountId);
            Assert.AreEqual(2, player.TeamId);
            Assert.IsTrue(player.Ready);
            Assert.AreEqual(2002, player.HeroId);
            Assert.AreEqual(4, player.SpawnPointId);
            Assert.AreEqual(5, player.Level);
            Assert.AreEqual(7, player.AttributeTemplateId);
            Assert.AreEqual(900, player.BasicAttackSkillId);
            Assert.AreEqual(new[] { 1, 2, 3 }, player.SkillIds.ToArray());
            Assert.AreEqual(9u, player.PlayerId);
            Assert.IsTrue(player.LobbyReady);
            Assert.IsFalse(player.AssetsLoaded);
            Assert.IsTrue(player.IsOnline);
            Assert.AreEqual(1L, player.JoinOrdinal);
            Assert.AreEqual(2, player.LoadedManifestVersion);
            Assert.AreEqual("hash-2", player.LoadedManifestHash);
        }

        [Test]
        public void ToClientSnapshot_NullPlayers_HandledAsEmpty()
        {
            var wire = new WireRoomSnapshot
            {
                Summary = new WireRoomSummary { RoomId = "room-empty" },
                Members = null,
                Players = null
            };

            var snapshot = ClientRoomSnapshotMapper.ToClientSnapshot(wire);

            Assert.IsNotNull(snapshot.Members);
            Assert.AreEqual(0, snapshot.Members.Count);
            Assert.IsNotNull(snapshot.Players);
            Assert.AreEqual(0, snapshot.Players.Count);
        }

        [Test]
        public void ToClientSnapshot_UnknownPhase_FallsBackToLobby()
        {
            var wire = new WireRoomSnapshot
            {
                Summary = new WireRoomSummary { RoomId = "room-x" },
                Phase = 999
            };

            var snapshot = ClientRoomSnapshotMapper.ToClientSnapshot(wire);

            Assert.AreEqual(ClientRoomPhase.Lobby, snapshot.Phase);
        }
    }
}
