using System;
using System.Collections.Generic;
using System.Linq;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.World.DI;
using AbilityKit.Attributes.Core;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba;
using AbilityKit.Demo.Moba.Attributes;
using AbilityKit.Demo.Moba.Components;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Demo.Moba.Gameplay;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.EntityConstruction;
using AbilityKit.Demo.Moba.Services.EntityManager;
using AbilityKit.Demo.Moba.Share.Config;
using AbilityKit.Effect;
using AbilityKit.Demo.Moba.Testing;
using AbilityKit.Protocol.Moba;
using AbilityKit.Protocol.Moba.StateSync;
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
        public void TryInitializeFromLoadout_ResolutionFailureLeavesExistingActorStateUnchanged()
        {
            var context = new ActorContext();
            var spec = CreateBuildSpec(100, "player-1");
            var entity = ActorSpawnPipeline.BuildActor(context, in spec).Entity;
            var attributeContext = new AttributeContext();
            var attributeGroup = attributeContext.GetOrCreateGroup("sentinel");
            attributeGroup.SetBase(MobaAttributeIds.HP, 321f);
            var resources = new ResourceContainer
            {
                Map = new Dictionary<ResourceType, ResourceState>
                {
                    [ResourceType.Hp] = new ResourceState { Current = 123f, LastMax = 321f }
                }
            };
            var activeSkills = new[] { new ActiveSkillRuntime { SkillId = 7001, Level = 3 } };
            var passiveSkills = new[] { new PassiveSkillRuntime { PassiveSkillId = 7002, Level = 4 } };
            entity.AddAttributeGroup(attributeGroup, attributeContext);
            entity.AddResourceContainer(resources, true);
            entity.AddSkillLoadout(activeSkills, passiveSkills);
            var config = BuildConfig(includeSecondActiveSkill: false);
            var pipeline = new ActorEntityInitPipeline(new TestWorldResolver(config));
            var loadout = CreateLoadout(new PlayerId("player-1"), CompleteHeroId);

            var succeeded = pipeline.TryInitializeFromLoadout(entity, in loadout, out var error);

            Assert.IsFalse(succeeded);
            StringAssert.Contains(ActiveSkill2Id.ToString(), error);
            Assert.AreSame(attributeGroup, entity.attributeGroup.Group);
            Assert.AreSame(attributeContext, entity.attributeGroup.Ctx);
            Assert.IsTrue(attributeGroup.TryGet(MobaAttributeIds.HP, out var hpAttribute));
            Assert.AreEqual(321f, hpAttribute.BaseValue);
            Assert.AreSame(resources, entity.resourceContainer.Value);
            Assert.AreEqual(123f, resources.Map[ResourceType.Hp].Current);
            Assert.AreSame(activeSkills, entity.skillLoadout.ActiveSkills);
            Assert.AreSame(passiveSkills, entity.skillLoadout.PassiveSkills);
        }

        [Test]
        public void GameplayPrepare_DoesNotPublishRunningStateUntilCommit()
        {
            var gameplay = CreateGameplayService();

            Assert.IsTrue(gameplay.TryPrepareStart(9001, out var error), error);

            Assert.AreEqual(MobaGameplayPhase.NotStarted, gameplay.Phase);
            Assert.IsFalse(gameplay.IsRunning);
            Assert.AreEqual(0, gameplay.CurrentGameplayId);
            Assert.IsNull(gameplay.CurrentGameplay);

            Assert.IsTrue(gameplay.CommitPreparedStart());
            Assert.AreEqual(MobaGameplayPhase.Running, gameplay.Phase);
            Assert.AreEqual(9001, gameplay.CurrentGameplayId);
            Assert.IsNotNull(gameplay.CurrentGameplay);
        }

        [Test]
        public void GameplayCancelPreparedStart_PreventsCommitAndLeavesNotStarted()
        {
            var gameplay = CreateGameplayService();
            Assert.IsTrue(gameplay.TryPrepareStart(9001, out var error), error);

            gameplay.CancelPreparedStart();

            Assert.IsFalse(gameplay.CommitPreparedStart());
            Assert.AreEqual(MobaGameplayPhase.NotStarted, gameplay.Phase);
            Assert.IsFalse(gameplay.IsRunning);
            Assert.AreEqual("gameplay start was not prepared", gameplay.LastStartFailureReason);
        }

        [Test]
        public void GameplayPrepareFailure_LeavesNotStartedAndCanRetryValidConfig()
        {
            var gameplay = CreateGameplayService();

            Assert.IsFalse(gameplay.TryPrepareStart(9999, out var error));
            StringAssert.Contains("missing config", error);
            Assert.AreEqual(MobaGameplayPhase.NotStarted, gameplay.Phase);
            Assert.IsFalse(gameplay.IsRunning);

            Assert.IsTrue(gameplay.TryPrepareStart(9001, out error), error);
            Assert.IsTrue(gameplay.CommitPreparedStart());
            Assert.AreEqual(MobaGameplayPhase.Running, gameplay.Phase);
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

        [Test]
        public void HeroReplacement_StaleRequestBeforeSpawnIsRejectedWithoutSideEffects()
        {
            var context = new ActorContext();
            var registry = new MobaActorRegistry();
            var entities = new MobaEntityManager(null);
            var map = new MobaPlayerActorMapService();
            var player = new PlayerId("player-1");
            var previous = RegisterActor(context, registry, entities, 401, player);
            map.Bind(player, 499);
            var spawn = new RecordingSpawnService(context, registry, entities, player, 402);
            var snapshots = new RecordingSnapshotPrecommit();
            var transaction = CreateTransaction(map, entities, registry, spawn, snapshots);
            var loadout = CreateLoadout(player, CompleteHeroId);
            var request = new MobaHeroReplacementRequest(player, new FrameIndex(10), 401, previous, in loadout);

            var succeeded = transaction.TryReplace(in request, out var result);

            Assert.IsFalse(succeeded);
            Assert.AreEqual(MobaHeroReplacementFailureStage.Validation, result.FailureStage);
            Assert.AreEqual(0, spawn.SpawnCount);
            Assert.AreEqual(0, snapshots.CommitCount);
            Assert.IsTrue(map.TryGetActorId(player, out var mappedActorId));
            Assert.AreEqual(499, mappedActorId);
            Assert.IsFalse(previous.hasActorDespawnRequest);
        }

        [Test]
        public void HeroReplacement_MappingChangeDuringPrecommitCompensatesSpawnWithoutPublishing()
        {
            var context = new ActorContext();
            var registry = new MobaActorRegistry();
            var entities = new MobaEntityManager(null);
            var map = new MobaPlayerActorMapService();
            var player = new PlayerId("player-1");
            var previous = RegisterActor(context, registry, entities, 401, player);
            map.Bind(player, 401);
            var spawn = new RecordingSpawnService(context, registry, entities, player, 402);
            var snapshots = new RecordingSnapshotPrecommit
            {
                OnPrepared = () => map.Bind(player, 499)
            };
            var transaction = CreateTransaction(map, entities, registry, spawn, snapshots);
            var loadout = CreateLoadout(player, CompleteHeroId);
            var request = new MobaHeroReplacementRequest(player, new FrameIndex(10), 401, previous, in loadout);

            var succeeded = transaction.TryReplace(in request, out var result);

            Assert.IsFalse(succeeded);
            Assert.AreEqual(MobaHeroReplacementFailureStage.Commit, result.FailureStage);
            Assert.AreEqual(1, spawn.SpawnCount);
            Assert.AreEqual(0, snapshots.CommitCount);
            Assert.IsTrue(map.TryGetActorId(player, out var mappedActorId));
            Assert.AreEqual(499, mappedActorId);
            Assert.IsFalse(registry.TryGet(402, out _));
            Assert.IsFalse(entities.TryGetActorEntity(402, out _));
            Assert.IsFalse(spawn.LastEntity.isEnabled);
            Assert.IsFalse(previous.hasActorDespawnRequest);
        }

        [Test]
        public void HeroReplacement_PrecommitFailureCompensatesSpawnAndPreservesOldMapping()
        {
            var context = new ActorContext();
            var registry = new MobaActorRegistry();
            var entities = new MobaEntityManager(null);
            var map = new MobaPlayerActorMapService();
            var player = new PlayerId("player-1");
            var previous = RegisterActor(context, registry, entities, 401, player);
            map.Bind(player, 401);
            var spawn = new RecordingSpawnService(context, registry, entities, player, 402);
            var snapshots = new RecordingSnapshotPrecommit { PrepareError = "injected serialization failure" };
            var transaction = CreateTransaction(map, entities, registry, spawn, snapshots);
            var loadout = CreateLoadout(player, CompleteHeroId);
            var request = new MobaHeroReplacementRequest(player, new FrameIndex(10), 401, previous, in loadout);

            var succeeded = transaction.TryReplace(in request, out var result);

            Assert.IsFalse(succeeded);
            Assert.AreEqual(MobaHeroReplacementFailureStage.SnapshotPrecommit, result.FailureStage);
            Assert.IsTrue(map.TryGetActorId(player, out var mappedActorId));
            Assert.AreEqual(401, mappedActorId);
            Assert.IsFalse(registry.TryGet(402, out _));
            Assert.IsFalse(entities.TryGetActorEntity(402, out _));
            Assert.IsFalse(spawn.LastEntity.isEnabled);
            Assert.AreEqual(0, snapshots.CommitCount);
            Assert.IsFalse(previous.hasActorDespawnRequest);
        }

        [Test]
        public void HeroReplacement_RepeatedSuccessKeepsNewestMappingAndMarksPreviousActorsForDespawn()
        {
            var context = new ActorContext();
            var registry = new MobaActorRegistry();
            var entities = new MobaEntityManager(null);
            var map = new MobaPlayerActorMapService();
            var player = new PlayerId("player-1");
            var actor401 = RegisterActor(context, registry, entities, 401, player);
            map.Bind(player, 401);
            var spawn = new RecordingSpawnService(context, registry, entities, player, 402, 403);
            var snapshots = new RecordingSnapshotPrecommit();
            var transaction = CreateTransaction(map, entities, registry, spawn, snapshots);
            var loadout = CreateLoadout(player, CompleteHeroId);
            var firstRequest = new MobaHeroReplacementRequest(player, new FrameIndex(10), 401, actor401, in loadout);

            Assert.IsTrue(transaction.TryReplace(in firstRequest, out var first), first.Error);
            Assert.IsTrue(entities.TryGetActorEntity(first.ActorId, out var actor402));
            var secondRequest = new MobaHeroReplacementRequest(player, new FrameIndex(11), first.ActorId, actor402, in loadout);
            Assert.IsTrue(transaction.TryReplace(in secondRequest, out var second), second.Error);

            Assert.IsTrue(map.TryGetActorId(player, out var mappedActorId));
            Assert.AreEqual(403, mappedActorId);
            Assert.AreEqual(2, snapshots.CommitCount);
            Assert.IsTrue(actor401.hasActorDespawnRequest);
            Assert.AreEqual(402, actor401.actorDespawnRequest.SourceActorId);
            Assert.IsTrue(actor402.hasActorDespawnRequest);
            Assert.AreEqual(403, actor402.actorDespawnRequest.SourceActorId);
            Assert.IsTrue(registry.TryGet(403, out var actor403));
            Assert.IsTrue(actor403.isEnabled);
        }

        [Test]
        public void HeroReplacementSnapshotPrecommit_SerializesBothPayloadsBeforeCommit()
        {
            var phase = new MobaLogicWorldRunGateService();
            var spawnSnapshots = new MobaActorSpawnSnapshotService();
            var changedSnapshots = new MobaPlayerHeroChangedSnapshotService(phase);
            var precommit = new MobaHeroReplacementSnapshotPrecommitService(spawnSnapshots, changedSnapshots);
            var spawnEntry = new MobaActorSpawnSnapshotEntry(501, (int)SpawnEntityKind.Character, CompleteHeroId, 501, 1f, 0f, 2f);
            var changedEntry = new MobaPlayerHeroChangedSnapshotEntry(
                "player-1", 401, 501, 1, CompleteHeroId, AttributeTemplateId, 1, BasicAttackSkillId,
                new[] { ActiveSkill1Id, ActiveSkill2Id });

            var prepared = precommit.TryPrepare(in spawnEntry, in changedEntry, out var batch, out var error);

            Assert.IsTrue(prepared, error);
            CollectionAssert.AreEqual(new[] { 501 }, MobaActorSpawnSnapshotCodec.Deserialize(batch.SpawnPayload).Select(entry => entry.NetId));
            CollectionAssert.AreEqual(new[] { 501 }, MobaPlayerHeroChangedSnapshotCodec.Deserialize(batch.ChangedPayload).Select(entry => entry.ActorId));
        }

        private static MobaHeroReplacementTransactionService CreateTransaction(
            MobaPlayerActorMapService map,
            MobaEntityManager entities,
            MobaActorRegistry registry,
            IMobaActorSpawnService spawn,
            IMobaHeroReplacementSnapshotPrecommit snapshots)
        {
            return new MobaHeroReplacementTransactionService(
                map,
                entities,
                registry,
                spawn,
                new ActorEntityInitPipeline(null),
                snapshots);
        }

        private static ActorEntity RegisterActor(
            ActorContext context,
            MobaActorRegistry registry,
            MobaEntityManager entities,
            int actorId,
            PlayerId player)
        {
            var spec = CreateBuildSpec(actorId, player.Value);
            return ActorSpawnPipeline.BuildActorAndRegister(context, registry, entities, in spec).Entity;
        }

        private sealed class RecordingSpawnService : IMobaActorSpawnService
        {
            private readonly ActorContext _context;
            private readonly MobaActorRegistry _registry;
            private readonly MobaEntityManager _entities;
            private readonly PlayerId _player;
            private readonly Queue<int> _actorIds;

            public ActorEntity LastEntity { get; private set; }
            public int SpawnCount { get; private set; }

            public RecordingSpawnService(
                ActorContext context,
                MobaActorRegistry registry,
                MobaEntityManager entities,
                PlayerId player,
                params int[] actorIds)
            {
                _context = context;
                _registry = registry;
                _entities = entities;
                _player = player;
                _actorIds = new Queue<int>(actorIds);
            }

            public bool TrySpawn(in MobaActorSpawnRequest request, out MobaActorSpawnResult result)
            {
                SpawnCount++;
                var spec = CreateBuildSpec(_actorIds.Dequeue(), _player.Value);
                var built = ActorSpawnPipeline.BuildActorAndRegister(_context, _registry, _entities, in spec);
                LastEntity = built.Entity;
                result = new MobaActorSpawnResult(true, spec.Info.ActorId, built.Entity, in spec, null);
                return true;
            }

            public void Dispose()
            {
            }
        }

        private sealed class RecordingSnapshotPrecommit : IMobaHeroReplacementSnapshotPrecommit
        {
            public string PrepareError;
            public Action OnPrepared;
            public int CommitCount;

            public bool TryPrepare(
                in MobaActorSpawnSnapshotEntry spawnEntry,
                in MobaPlayerHeroChangedSnapshotEntry changedEntry,
                out MobaHeroReplacementSnapshotBatch batch,
                out string error)
            {
                error = PrepareError;
                OnPrepared?.Invoke();
                if (!string.IsNullOrEmpty(error))
                {
                    batch = default;
                    return false;
                }

                batch = new MobaHeroReplacementSnapshotBatch(
                    in spawnEntry,
                    in changedEntry,
                    new byte[] { 1 },
                    new byte[] { 2 });
                return true;
            }

            public void Commit(in MobaHeroReplacementSnapshotBatch batch)
            {
                CommitCount++;
            }

            public void Dispose()
            {
            }
        }

        private static MobaGameplayService CreateGameplayService()
        {
            var config = new MobaTestConfigBuilder()
                .AddDtos(new GameplayDTO
                {
                    Id = 9001,
                    Name = "Prepared Gameplay",
                    TriggerIds = Array.Empty<int>()
                })
                .BuildDatabase();
            var frameTime = new FrameTime();
            frameTime.Reset(new FrameIndex(17), 17f / 30f, 1f / 30f);
            return WorldTestInjector.For(new MobaGameplayService())
                .With<IWorldResolver>(new TestWorldResolver(config))
                .With<IFrameTime>(frameTime)
                .Build();
        }

        private sealed class TestWorldResolver : IWorldResolver
        {
            private readonly MobaConfigDatabase _config;

            public TestWorldResolver(MobaConfigDatabase config)
            {
                _config = config;
            }

            public object Resolve(Type serviceType)
            {
                if (TryResolve(serviceType, out var instance)) return instance;
                throw new InvalidOperationException($"Service not registered: {serviceType}");
            }

            public T Resolve<T>()
            {
                return (T)Resolve(typeof(T));
            }

            public bool TryResolve(Type serviceType, out object instance)
            {
                if (serviceType == typeof(MobaConfigDatabase))
                {
                    instance = _config;
                    return true;
                }

                instance = null;
                return false;
            }

            public bool TryResolve<T>(out T instance)
            {
                if (TryResolve(typeof(T), out var value) && value is T typed)
                {
                    instance = typed;
                    return true;
                }

                instance = default;
                return false;
            }
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
