using System;
using System.Collections.Generic;
using System.Linq;
using AbilityKit.Ability.Host;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.EntityConstruction;
using AbilityKit.Demo.Moba.Services.EntityManager;
using AbilityKit.Demo.Moba.Share.Config;
using AbilityKit.Demo.Moba.Testing;
using AbilityKit.Protocol.Moba;
using NUnit.Framework;

namespace AbilityKit.Game.Test.UnitTest
{
    public sealed class MobaHeroLoadoutResolverTests
    {
        private const int CompleteHeroId = 1004;
        private const int IncompleteHeroId = 1007;
        private const int AttributeTemplateId = 1004;
        private const int BasicAttackSkillId = 10040011;
        private const int ActiveSkill1Id = 10040101;
        private const int ActiveSkill2Id = 10040201;
        private const int PassiveSkillId = 10040000;

        [Test]
        public void TryResolveHeroConfig_UsesBattleAttributeTemplateAsCanonicalLoadout()
        {
            var config = BuildConfig();

            var resolved = MobaHeroLoadoutResolver.TryResolveHeroConfig(
                config,
                CompleteHeroId,
                out var character,
                out var basicAttackSkillId,
                out var activeSkillIds,
                out var error);

            Assert.IsTrue(resolved, error);
            Assert.AreEqual(CompleteHeroId, character.Id);
            Assert.AreEqual(BasicAttackSkillId, basicAttackSkillId);
            CollectionAssert.AreEqual(
                new[] { ActiveSkill1Id, ActiveSkill2Id },
                activeSkillIds);
        }

        [Test]
        public void ResolvedHeroLoadout_UsesTemplateSkillsAndIgnoresLegacyCharacterSkills()
        {
            var config = BuildConfig();

            var succeeded = MobaResolvedHeroLoadoutResolver.TryResolve(
                config,
                CompleteHeroId,
                out var resolved,
                out var error);

            Assert.IsTrue(succeeded, error);
            Assert.AreEqual(AttributeTemplateId, resolved.AttributeTemplate.Id);
            Assert.AreEqual(BasicAttackSkillId, resolved.BasicAttackSkillId);
            CollectionAssert.AreEqual(new[] { ActiveSkill1Id, ActiveSkill2Id }, resolved.ActiveSkillIds);
            CollectionAssert.AreEqual(new[] { PassiveSkillId }, resolved.PassiveSkillIds);
            CollectionAssert.DoesNotContain(resolved.ActiveSkillIds, 99999991);
            CollectionAssert.DoesNotContain(resolved.PassiveSkillIds, 99999992);
        }

        [Test]
        public void TryResolveHeroConfig_ResolvesNonPrefixBasicAttackFromTemplate()
        {
            var config = BuildConfig();

            var resolved = MobaHeroLoadoutResolver.TryResolveHeroConfig(
                config,
                CompleteHeroId,
                out _,
                out var basicAttackSkillId,
                out _,
                out var error);

            Assert.IsTrue(resolved, error);
            Assert.AreEqual(10040011, basicAttackSkillId);
            Assert.AreNotEqual(10040001, basicAttackSkillId);
        }

        [Test]
        public void TryResolveHeroConfig_RejectsConfiguredBasicAttackWithWrongType()
        {
            var config = BuildConfig(basicAttackType: SkillType.Active);

            var resolved = TryResolve(config, CompleteHeroId, out var error);

            Assert.IsFalse(resolved);
            StringAssert.Contains("not a normal attack", error);
        }

        [Test]
        public void TryResolveHeroConfig_RejectsMissingActiveSkillReference()
        {
            var config = BuildConfig(includeSecondActiveSkill: false);

            var resolved = TryResolve(config, CompleteHeroId, out var error);

            Assert.IsFalse(resolved);
            StringAssert.Contains(ActiveSkill2Id.ToString(), error);
        }

        [Test]
        public void TryResolveHeroConfig_RejectsMissingPassiveSkillReference()
        {
            var config = BuildConfig(includePassiveSkill: false);

            var resolved = TryResolve(config, CompleteHeroId, out var error);

            Assert.IsFalse(resolved);
            StringAssert.Contains(PassiveSkillId.ToString(), error);
        }

        [Test]
        public void HeroCatalogPredicate_ExcludesCharacterWithoutCompleteBattleTemplate()
        {
            var config = BuildConfig(includeIncompleteCharacter: true);

            var selectableHeroIds = config.GetAllCharacters()
                .Where(character => MobaHeroLoadoutResolver.TryResolveHeroConfig(
                    config,
                    character.Id,
                    out _,
                    out _,
                    out _,
                    out _))
                .Select(character => character.Id)
                .OrderBy(heroId => heroId)
                .ToArray();

            CollectionAssert.AreEqual(new[] { CompleteHeroId }, selectableHeroIds);
            CollectionAssert.DoesNotContain(selectableHeroIds, IncompleteHeroId);
        }

        [Test]
        public void BuildActor_InitializerFailureDestroysPartialEntity()
        {
            var context = new ActorContext();
            var spec = CreateBuildSpec(101, "player-1");

            Assert.Throws<InvalidOperationException>(() =>
                ActorSpawnPipeline.BuildActor(
                    context,
                    in spec,
                    initializer: (_, __) => throw new InvalidOperationException("init failed")));

            Assert.AreEqual(0, context.GetEntities().Length);
        }

        [Test]
        public void BuildActorsFromSpecs_LaterFailureRollsBackEarlierRegistrationsAndEntities()
        {
            var context = new ActorContext();
            var registry = new MobaActorRegistry();
            var entities = new MobaEntityManager(null);
            var player1 = new PlayerId("player-1");
            var player2 = new PlayerId("player-2");
            var loadouts = new[]
            {
                CreateLoadout(player1, CompleteHeroId),
                CreateLoadout(player2, CompleteHeroId)
            };
            var specs = new[]
            {
                CreateBuildSpec(201, player1.Value),
                CreateBuildSpec(202, player2.Value)
            };

            Assert.Throws<InvalidOperationException>(() =>
                ActorSpawnPipeline.BuildActorsFromSpecs(
                    context,
                    registry,
                    entities,
                    player1,
                    loadouts,
                    specs,
                    (_, loadout) =>
                    {
                        if (loadout.PlayerId.Equals(player2))
                        {
                            throw new InvalidOperationException("second actor failed");
                        }
                    }));

            Assert.IsFalse(registry.TryGet(201, out _));
            Assert.IsFalse(registry.TryGet(202, out _));
            Assert.IsFalse(entities.TryGetActorEntity(201, out _));
            Assert.IsFalse(entities.TryGetActorEntity(202, out _));
            Assert.AreEqual(0, context.GetEntities().Length);
        }

        [Test]
        public void PlayerActorMap_UnbindRequiresExpectedActorId()
        {
            var map = new MobaPlayerActorMapService();
            var player = new PlayerId("player-1");
            map.Bind(player, 301);
            map.Bind(player, 302);

            Assert.IsFalse(map.Unbind(player, 301));
            Assert.IsTrue(map.TryGetActorId(player, out var currentActorId));
            Assert.AreEqual(302, currentActorId);
            Assert.IsTrue(map.Unbind(player, 302));
            Assert.IsFalse(map.TryGetActorId(player, out _));
        }

        private static bool TryResolve(
            MobaConfigDatabase config,
            int heroId,
            out string error)
        {
            return MobaHeroLoadoutResolver.TryResolveHeroConfig(
                config,
                heroId,
                out _,
                out _,
                out _,
                out error);
        }

        private static MobaConfigDatabase BuildConfig(
            SkillType basicAttackType = SkillType.NormalAttack,
            bool includeSecondActiveSkill = true,
            bool includePassiveSkill = true,
            bool includeIncompleteCharacter = false)
        {
            var characters = new List<CharacterDTO>
            {
                new CharacterDTO
                {
                    Id = CompleteHeroId,
                    Name = "Template Hero",
                    AttributeTemplateId = AttributeTemplateId,
                    SkillIds = new[] { 99999991 },
                    PassiveSkillIds = new[] { 99999992 }
                }
            };
            if (includeIncompleteCharacter)
            {
                characters.Add(new CharacterDTO
                {
                    Id = IncompleteHeroId,
                    Name = "Incomplete Hero",
                    AttributeTemplateId = IncompleteHeroId,
                    SkillIds = new[] { 10070101 }
                });
            }

            var skills = new List<SkillDTO>
            {
                Skill(BasicAttackSkillId, basicAttackType),
                Skill(ActiveSkill1Id, SkillType.Active)
            };
            if (includeSecondActiveSkill)
            {
                skills.Add(Skill(ActiveSkill2Id, SkillType.Ultimate));
            }

            var builder = new MobaTestConfigBuilder()
                .SetDtos(characters)
                .AddDtos(new BattleAttributeTemplateDTO
                {
                    Id = AttributeTemplateId,
                    BasicAttackSkillId = BasicAttackSkillId,
                    ActiveSkills = new[] { ActiveSkill1Id, ActiveSkill2Id },
                    PassiveSkills = new[] { PassiveSkillId }
                })
                .SetDtos(skills);

            if (includePassiveSkill)
            {
                builder.AddDtos(new PassiveSkillDTO
                {
                    Id = PassiveSkillId,
                    Name = "Template Passive"
                });
            }

            return builder.BuildDatabase();
        }

        private static MobaPlayerLoadout CreateLoadout(PlayerId playerId, int heroId)
        {
            return new MobaPlayerLoadout(
                playerId: playerId,
                teamId: 1,
                heroId: heroId,
                attributeTemplateId: AttributeTemplateId,
                level: 1,
                basicAttackSkillId: 99999981,
                skillIds: new[] { 99999982 },
                spawnIndex: 0);
        }

        private static MobaActorBuildSpec CreateBuildSpec(int actorId, string playerId)
        {
            var transform = Transform3.Identity;
            var info = new MobaEntityInfo(
                actorId,
                MobaEntityKind.Hero,
                in transform,
                (Team)1,
                EntityMainType.Unit,
                UnitSubType.Hero,
                new PlayerId(playerId),
                CompleteHeroId);
            return new MobaActorBuildSpec(
                in info,
                MobaActorBuildSourceKind.PlayerLoadout,
                CompleteHeroId,
                ownerActorId: 0);
        }

        private static SkillDTO Skill(int id, SkillType type)
        {
            return new SkillDTO
            {
                Id = id,
                Name = id.ToString(),
                SkillType = (int)type
            };
        }
    }
}
