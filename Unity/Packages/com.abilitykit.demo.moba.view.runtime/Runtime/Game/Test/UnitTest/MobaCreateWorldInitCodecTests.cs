using System;
using AbilityKit.Ability.Host;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Protocol.MemoryPack;
using AbilityKit.Protocol.Moba;
using AbilityKit.Protocol.Moba.CreateWorld;
using AbilityKit.Protocol.Moba.StateSync;
using NUnit.Framework;

namespace AbilityKit.Game.Test.UnitTest
{
    [TestFixture]
    public sealed class MobaCreateWorldInitCodecTests
    {
        [SetUp]
        public void SetUp()
        {
            MemoryPackWireSerializerInstaller.InstallAsCurrent();
        }

        [Test]
        public void SerializeRoundTrip_PreservesPlayerLoadoutIds()
        {
            var localPlayerId = new PlayerId("p1");
            var players = new[]
            {
                new MobaPlayerLoadout(
                    playerId: localPlayerId,
                    teamId: 1,
                    heroId: 1001,
                    attributeTemplateId: 1001,
                    level: 1,
                    basicAttackSkillId: 1,
                    skillIds: new[] { 10010101, 10010201, 10010301 },
                    spawnIndex: 0,
                    unitSubType: 1,
                    mainType: 1,
                    hasSpawnPosition: 1,
                    spawnX: 0f,
                    spawnY: 0f,
                    spawnZ: 0f,
                    brainId: 100,
                    enableBrainOnSpawn: false)
            };
            var spec = new MobaCreateWorldSpec(
                matchId: "roundtrip_match",
                mapId: 1,
                randomSeed: 123,
                tickRate: 30,
                inputDelayFrames: 0,
                players: players,
                gameplayId: 0);
            var payload = new MobaCreateWorldInitPayload(localPlayerId, in spec, opCode: 0, payload: null);

            var bytes = MobaCreateWorldInitCodec.Serialize(in payload);
            Assert.IsTrue(MobaCreateWorldInitCodec.TryDeserialize(bytes, out var decoded, out var error), error);

            var decodedPlayers = decoded.Spec.Players;
            Assert.IsNotNull(decodedPlayers);
            Assert.AreEqual(1, decodedPlayers.Length);
            Assert.AreEqual(1001, decodedPlayers[0].HeroId);
            Assert.AreEqual(1001, decodedPlayers[0].AttributeTemplateId);
            Assert.AreEqual(1, decodedPlayers[0].Level);
            Assert.AreEqual(1, decodedPlayers[0].BasicAttackSkillId);
            CollectionAssert.AreEqual(new[] { 10010101, 10010201, 10010301 }, decodedPlayers[0].SkillIds);
            Assert.AreEqual(100, decodedPlayers[0].BrainId);
            Assert.IsFalse(decodedPlayers[0].EnableBrainOnSpawn);
        }

        [TestCase(MobaDebugSpawnUnitRelation.Ally)]
        [TestCase(MobaDebugSpawnUnitRelation.Enemy)]
        public void DebugSpawnUnitCodec_RoundTrip_PreservesRelation(MobaDebugSpawnUnitRelation relation)
        {
            var bytes = MobaDebugSpawnUnitCodec.Serialize(relation);

            Assert.IsTrue(MobaDebugSpawnUnitCodec.TryDeserialize(bytes, out var decoded, out var error), error);
            Assert.AreEqual(relation, decoded);
        }

        [Test]
        public void DebugSpawnUnitCodec_RejectsEmptyPayload()
        {
            Assert.IsFalse(MobaDebugSpawnUnitCodec.TryDeserialize(Array.Empty<byte>(), out _, out var error));
            StringAssert.Contains("empty", error);
        }

        [Test]
        public void DebugSpawnUnitCodec_RejectsUnsupportedVersion()
        {
            var bytes = MobaDebugSpawnUnitCodec.Serialize(MobaDebugSpawnUnitRelation.Ally);
            Assert.GreaterOrEqual(bytes.Length, 2);
            bytes[0] = MobaDebugSpawnUnitPayload.CurrentVersion + 1;

            Assert.IsFalse(MobaDebugSpawnUnitCodec.TryDeserialize(bytes, out _, out var error));
            StringAssert.Contains("version", error);
        }

        [Test]
        public void DebugSpawnUnitCodec_RejectsInvalidRelation()
        {
            var bytes = MobaDebugSpawnUnitCodec.Serialize(MobaDebugSpawnUnitRelation.Ally);
            Assert.GreaterOrEqual(bytes.Length, 2);
            bytes[1] = byte.MaxValue;

            Assert.IsFalse(MobaDebugSpawnUnitCodec.TryDeserialize(bytes, out _, out var error));
            StringAssert.Contains("relation", error);
        }

        [Test]
        public void DebugSpawnUnitCodec_SerializeRejectsInvalidRelation()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => MobaDebugSpawnUnitCodec.Serialize((MobaDebugSpawnUnitRelation)0));
        }

        [Test]
        public void DebugReplaceHeroCodec_RoundTrip_PreservesHeroId()
        {
            var bytes = MobaDebugReplaceHeroCodec.Serialize(heroId: 1004);

            Assert.IsTrue(
                MobaDebugReplaceHeroCodec.TryDeserialize(bytes, out var heroId, out var error),
                error);
            Assert.AreEqual(1004, heroId);
        }

        [Test]
        public void DebugReplaceHeroCodec_RejectsInvalidHeroId()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => MobaDebugReplaceHeroCodec.Serialize(heroId: 0));
            Assert.IsFalse(
                MobaDebugReplaceHeroCodec.TryDeserialize(Array.Empty<byte>(), out _, out var error));
            StringAssert.Contains("empty", error);
        }

        [Test]
        public void PlayerHeroChangedSnapshotCodec_RoundTrip_PreservesAllEntries()
        {
            var entries = new[]
            {
                new MobaPlayerHeroChangedSnapshotEntry(
                    "p1", 101, 201, 1, 1003, 1003, 2, 10030011,
                    new[] { 10030101, 10030201, 10030301 }),
                new MobaPlayerHeroChangedSnapshotEntry(
                    "p2", 102, 202, 2, 1004, 1004, 3, 10040011,
                    new[] { 10040101, 10040201, 10040301 }),
            };

            var decoded = MobaPlayerHeroChangedSnapshotCodec.Deserialize(
                MobaPlayerHeroChangedSnapshotCodec.Serialize(entries));

            Assert.AreEqual(2, decoded.Length);
            Assert.AreEqual("p1", decoded[0].PlayerId);
            Assert.AreEqual(201, decoded[0].ActorId);
            CollectionAssert.AreEqual(entries[0].SkillIds, decoded[0].SkillIds);
            Assert.AreEqual("p2", decoded[1].PlayerId);
            Assert.AreEqual(10040011, decoded[1].BasicAttackSkillId);
            CollectionAssert.AreEqual(entries[1].SkillIds, decoded[1].SkillIds);
        }

        [Test]
        public void DefaultInputContracts_RequireDebugHandlers()
        {
            var contracts = MobaInputCommandContractRegistry.CreateDefault();

            Assert.IsTrue(
                contracts.TryGetContract(MobaOpCodes.Input.DebugSpawnUnit, out var spawnContract),
                "debug spawn unit input contract is missing");
            Assert.IsTrue(spawnContract.Required);
            Assert.AreEqual(typeof(MobaDebugSpawnUnitInputCommandHandler), spawnContract.HandlerType);

            Assert.IsTrue(
                contracts.TryGetContract(MobaOpCodes.Input.DebugReplaceHero, out var replaceContract),
                "debug replace hero input contract is missing");
            Assert.IsTrue(replaceContract.Required);
            Assert.AreEqual(typeof(MobaDebugReplaceHeroInputCommandHandler), replaceContract.HandlerType);
            Assert.IsTrue(contracts.Validate().Succeeded);
        }

        [Test]
        public void ValidateGameStartSpec_RejectsMissingBasicAttackSkillId()
        {
            var localPlayerId = new PlayerId("p1");
            var players = new[]
            {
                new MobaPlayerLoadout(
                    playerId: localPlayerId,
                    teamId: 1,
                    heroId: 1001,
                    attributeTemplateId: 1001,
                    level: 1,
                    basicAttackSkillId: 0,
                    skillIds: new[] { 10010101 },
                    spawnIndex: 0)
            };
            var req = new EnterMobaGameReq(
                playerId: localPlayerId,
                matchId: "missing_basic_attack",
                mapId: 1,
                randomSeed: 123,
                tickRate: 30,
                inputDelayFrames: 0,
                players: players);
            var spec = new MobaGameStartSpec(in req);

            var result = MobaGameStartSpecService.ValidateSpec(in spec);

            Assert.IsFalse(result.Succeeded);
            StringAssert.Contains(
                nameof(MobaProtocolValidationCode.InvalidBasicAttackSkillId),
                result.Message);
        }
    }
}
