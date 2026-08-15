using System;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.Host.Extensions.Moba.CreateWorld;
using AbilityKit.Game.Flow;
using AbilityKit.Protocol.Moba;
using AbilityKit.Protocol.Moba.StateSync;
using NUnit.Framework;

namespace AbilityKit.Game.Test.UnitTest
{
    public sealed class BattlePlayerLoadoutStoreTests
    {
        [Test]
        public void Apply_ReplacesHeroAndPreservesSpawnMetadata()
        {
            var store = new BattlePlayerLoadoutStore();
            var startup = new[]
            {
                CreateLoadout("Player-A", heroId: 100, spawnIndex: 3)
            };
            var changed = CreateChange("player-a", actorId: 42, heroId: 200, level: 0);

            var applied = store.Apply(in changed, startup);
            var effective = store.BuildEffective(startup);

            Assert.That(applied, Is.True);
            Assert.That(store.Revision, Is.EqualTo(1));
            Assert.That(effective[0].HeroId, Is.EqualTo(200));
            Assert.That(effective[0].Level, Is.EqualTo(1));
            Assert.That(effective[0].SpawnIndex, Is.EqualTo(3));
            Assert.That(effective[0].SpawnX, Is.EqualTo(10f));
            Assert.That(effective[0].BrainId, Is.EqualTo(9));
            Assert.That(effective[0].EnableBrainOnSpawn, Is.False);
        }

        [Test]
        public void Apply_ConsecutiveChangesPreserveMetadataAndAdvanceRevision()
        {
            var store = new BattlePlayerLoadoutStore();
            var startup = new[] { CreateLoadout("player-a", heroId: 100, spawnIndex: 5) };
            var first = CreateChange("player-a", actorId: 41, heroId: 200, level: 2);
            var second = CreateChange("PLAYER-A", actorId: 42, heroId: 300, level: 4);

            store.Apply(in first, startup);
            store.Apply(in second, startup);
            var effective = store.BuildEffective(startup);

            Assert.That(store.Revision, Is.EqualTo(2));
            Assert.That(effective[0].HeroId, Is.EqualTo(300));
            Assert.That(effective[0].Level, Is.EqualTo(4));
            Assert.That(effective[0].SpawnIndex, Is.EqualTo(5));
        }

        [Test]
        public void InvalidChangeAndClear_DoNotLeakRuntimeState()
        {
            var store = new BattlePlayerLoadoutStore();
            var startup = new[] { CreateLoadout("player-a", heroId: 100, spawnIndex: 1) };
            var invalid = CreateChange(string.Empty, actorId: 0, heroId: 200, level: 2);
            var valid = CreateChange("player-a", actorId: 42, heroId: 300, level: 3);

            Assert.That(store.Apply(in invalid, startup), Is.False);
            Assert.That(store.Revision, Is.Zero);
            store.Apply(in valid, startup);
            store.Clear();
            var effective = store.BuildEffective(startup);

            Assert.That(store.Revision, Is.Zero);
            Assert.That(effective[0].HeroId, Is.EqualTo(100));
        }

        [Test]
        public void PoolReturn_ClearsLoadoutOverridesAndRevision()
        {
            var startup = CreateLoadout("player-a", heroId: 100, spawnIndex: 1);
            var changed = CreateChange("player-a", actorId: 42, heroId: 300, level: 3);
            var context = CreateContext(startup);

            context.ApplyPlayerHeroChanged(in changed);
            Assert.That(context.RuntimePlayerLoadoutRevision, Is.EqualTo(1));
            Assert.That(context.BuildEffectivePlayerLoadouts()[0].HeroId, Is.EqualTo(300));
            BattleContext.Return(context);

            var reused = BattleContext.Rent();
            try
            {
                reused.Plan = CreatePlan(startup);

                Assert.That(reused.RuntimePlayerLoadoutRevision, Is.Zero);
                Assert.That(reused.BuildEffectivePlayerLoadouts()[0].HeroId, Is.EqualTo(100));
            }
            finally
            {
                BattleContext.Return(reused);
            }
        }

        private static BattleContext CreateContext(params MobaPlayerLoadout[] players)
        {
            var context = BattleContext.Rent();
            context.Plan = CreatePlan(players);
            return context;
        }

        private static BattleStartPlan CreatePlan(params MobaPlayerLoadout[] players)
        {
            var playerId = new PlayerId("player-a");
            var launchSpec = new MobaBattleLaunchSpec(
                battleId: "loadout-store-test",
                matchId: "loadout-store-test",
                worldId: "loadout-store-test",
                worldType: "battle",
                clientId: "loadout-store-test-client",
                localPlayerId: playerId,
                mapId: 1,
                gameplayId: 1,
                ruleSetId: 0,
                configVersion: 0,
                protocolVersion: 0,
                randomSeed: 1,
                tickRate: 30,
                inputDelayFrames: 0,
                launchMode: MobaBattleLaunchMode.ViewFastEnter,
                syncMode: MobaBattleLaunchSyncMode.FrameSync,
                authorityMode: MobaBattleLaunchAuthorityMode.LocalAuthority,
                players: players,
                enterGamePayload: Array.Empty<byte>());

            return BattleStartPlanBuilder
                .ForWorld(
                    "loadout-store-test",
                    "battle",
                    "loadout-store-test-client",
                    playerId.Value,
                    tickRate: 30,
                    inputDelayFrames: 0)
                .WithHostMode(BattleHostMode.Local)
                .WithLaunchSpec(in launchSpec)
                .Build();
        }

        private static MobaPlayerLoadout CreateLoadout(
            string playerId,
            int heroId,
            int spawnIndex)
        {
            return new MobaPlayerLoadout(
                new PlayerId(playerId),
                teamId: 1,
                heroId: heroId,
                attributeTemplateId: 20,
                level: 1,
                basicAttackSkillId: 30,
                skillIds: new[] { 40, 41 },
                spawnIndex: spawnIndex,
                unitSubType: 2,
                mainType: 3,
                hasSpawnPosition: 1,
                spawnX: 10f,
                spawnY: 11f,
                spawnZ: 12f,
                brainId: 9,
                enableBrainOnSpawn: false);
        }

        private static MobaPlayerHeroChangedSnapshotEntry CreateChange(
            string playerId,
            int actorId,
            int heroId,
            int level)
        {
            return new MobaPlayerHeroChangedSnapshotEntry(
                playerId,
                previousActorId: actorId - 1,
                actorId: actorId,
                teamId: 2,
                heroId: heroId,
                attributeTemplateId: 21,
                level: level,
                basicAttackSkillId: 31,
                skillIds: new[] { 42, 43 });
        }
    }
}
