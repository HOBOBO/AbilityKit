using System;
using System.Collections.Generic;
using AbilityKit.Ability.Host;
using AbilityKit.Attributes.Core;
using AbilityKit.Core.Eventing;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Attributes;
using AbilityKit.Demo.Moba.Components;
using AbilityKit.Demo.Moba.Events.Unit;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.EntityConstruction;
using AbilityKit.Demo.Moba.Services.EntityManager;
using AbilityKit.Triggering.Eventing;
using NUnit.Framework;

namespace AbilityKit.Demo.Moba.Diagnostics.Tests
{
    public sealed class MobaHealthCommitTests
    {
        private const int TargetActorId = 801;
        private const float MaxHp = 100f;

        private Contexts _contexts;
        private ActorIdIndex _actorIndex;

        [SetUp]
        public void SetUp()
        {
            _contexts = new Contexts();
            _actorIndex = new ActorIdIndex(_contexts);
        }

        [TearDown]
        public void TearDown()
        {
            _actorIndex.Dispose();
            _contexts.Reset();
        }

        [Test]
        public void CommitDamage_ClampsAtZeroAndPublishesCommittedResultAfterHpMutation()
        {
            var eventBus = new EventBus();
            var service = CreateService(initialHp: 30f, eventBus, out var target);
            MobaHealthChangeResult observed = default;
            var eventCount = 0;
            using var subscription = eventBus.Subscribe(
                CreateHealthCommittedKey(),
                result =>
                {
                    eventCount++;
                    observed = result;
                    Assert.That(target.GetMobaAttrs().Hp, Is.EqualTo(0f));
                });

            var result = service.CommitDamage(
                attackerActorId: 7,
                targetActorId: TargetActorId,
                damageType: 2,
                value: 50f,
                reasonKind: 3,
                reasonParam: 4);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Kind, Is.EqualTo(MobaHealthChangeKind.Damage));
            Assert.That(result.RequestedValue, Is.EqualTo(50f));
            Assert.That(result.AppliedValue, Is.EqualTo(30f));
            Assert.That(result.OldHp, Is.EqualTo(30f));
            Assert.That(result.TargetHp, Is.EqualTo(0f));
            Assert.That(result.TargetMaxHp, Is.EqualTo(MaxHp));
            Assert.That(result.BecameDead, Is.True);
            Assert.That(eventCount, Is.EqualTo(1));
            Assert.That(observed.TargetHp, Is.EqualTo(result.TargetHp));
        }

        [Test]
        public void CommitHeal_ClampsAtMaxHpAndPublishesHealResult()
        {
            var eventBus = new EventBus();
            var service = CreateService(initialHp: 80f, eventBus, out var target);
            var eventCount = 0;
            using var subscription = eventBus.Subscribe(
                CreateHealthCommittedKey(),
                result =>
                {
                    eventCount++;
                    Assert.That(result.Kind, Is.EqualTo(MobaHealthChangeKind.Heal));
                    Assert.That(target.GetMobaAttrs().Hp, Is.EqualTo(MaxHp));
                });

            var result = service.CommitHeal(
                healerActorId: 7,
                targetActorId: TargetActorId,
                healType: 5,
                value: 50f);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Kind, Is.EqualTo(MobaHealthChangeKind.Heal));
            Assert.That(result.AppliedValue, Is.EqualTo(20f));
            Assert.That(result.OldHp, Is.EqualTo(80f));
            Assert.That(result.TargetHp, Is.EqualTo(MaxHp));
            Assert.That(eventCount, Is.EqualTo(1));
        }

        [Test]
        public void CommitHeal_DeadTargetWithoutPermission_DoesNotMutateOrPublish()
        {
            var eventBus = new EventBus();
            var service = CreateService(initialHp: 0f, eventBus, out var target);
            var eventCount = 0;
            using var subscription = eventBus.Subscribe(
                CreateHealthCommittedKey(),
                _ => eventCount++);

            var result = service.CommitHeal(
                healerActorId: 7,
                targetActorId: TargetActorId,
                healType: 5,
                value: 40f);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(target.GetMobaAttrs().Hp, Is.EqualTo(0f));
            Assert.That(eventCount, Is.Zero);
        }

        [Test]
        public void CommitHeal_DeadTargetWithPermission_CommitsRespawnResult()
        {
            var eventBus = new EventBus();
            var service = CreateService(initialHp: 0f, eventBus, out var target);
            MobaHealthChangeResult observed = default;
            using var subscription = eventBus.Subscribe(
                CreateHealthCommittedKey(),
                result => observed = result);

            var result = service.CommitHeal(
                healerActorId: 7,
                targetActorId: TargetActorId,
                healType: 5,
                value: 40f,
                allowDeadTarget: true);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Kind, Is.EqualTo(MobaHealthChangeKind.Respawn));
            Assert.That(result.AppliedValue, Is.EqualTo(40f));
            Assert.That(result.OldHp, Is.EqualTo(0f));
            Assert.That(result.TargetHp, Is.EqualTo(40f));
            Assert.That(result.BecameDead, Is.False);
            Assert.That(target.GetMobaAttrs().Hp, Is.EqualTo(40f));
            Assert.That(observed.Kind, Is.EqualTo(MobaHealthChangeKind.Respawn));
        }

        [Test]
        public void CommitDamage_SubscriberFailurePropagatesAfterHpMutation()
        {
            var eventBus = new EventBus();
            var service = CreateService(initialHp: 60f, eventBus, out var target);
            using var subscription = eventBus.Subscribe<MobaHealthChangeResult>(
                CreateHealthCommittedKey(),
                _ => throw new InvalidOperationException("health subscriber failed"));

            var error = Assert.Throws<InvalidOperationException>(() => service.CommitDamage(
                attackerActorId: 7,
                targetActorId: TargetActorId,
                damageType: 2,
                value: 25f));

            Assert.That(error.Message, Is.EqualTo("health subscriber failed"));
            Assert.That(target.GetMobaAttrs().Hp, Is.EqualTo(35f));
        }

        [Test]
        public void TryRespawn_HealthSubscriberFailure_StillResetsDeathStateAfterCommit()
        {
            var eventBus = new EventBus();
            var registry = new MobaActorRegistry();
            var entities = new MobaEntityManager(eventBus);
            var target = CreateRegisteredActor(registry, entities, initialHp: 30f);
            var actors = new MobaActorLookupService(_actorIndex, registry, entities, _contexts);
            var rules = new MobaCombatRulesService(actors);
            var snapshots = new MobaDamageEventSnapshotService(new MobaLogicWorldRunGateService());
            var damage = new MobaDamageService(actors, snapshots, rules, eventBus: eventBus);
            var deaths = new MobaUnitDeathSubscriber(eventBus, entities);
            var lifecycle = new MobaUnitLifecycleService(actors, entities, deaths, damage);
            var dieCount = 0;
            using var dieSubscription = eventBus.Subscribe(
                CreateUnitDieKey(),
                _ => dieCount++);

            damage.CommitDamage(7, TargetActorId, 2, 30f);
            Assert.That(dieCount, Is.EqualTo(1));

            using (eventBus.Subscribe<MobaHealthChangeResult>(
                       CreateHealthCommittedKey(),
                       result =>
                       {
                           if (result.Kind == MobaHealthChangeKind.Respawn)
                           {
                               throw new InvalidOperationException("respawn health subscriber failed");
                           }
                       }))
            {
                var error = Assert.Throws<InvalidOperationException>(() =>
                    lifecycle.TryRespawn(TargetActorId, healthRatio: 0.5f));
                Assert.That(error.Message, Is.EqualTo("respawn health subscriber failed"));
            }

            Assert.That(target.GetMobaAttrs().Hp, Is.EqualTo(50f));
            damage.CommitDamage(7, TargetActorId, 2, 50f);
            Assert.That(dieCount, Is.EqualTo(2));

            lifecycle.Dispose();
            deaths.Dispose();
            registry.Dispose();
            entities.Dispose();
        }

        private MobaDamageService CreateService(
            float initialHp,
            EventBus eventBus,
            out ActorEntity target)
        {
            var registry = new MobaActorRegistry();
            var entities = new MobaEntityManager(null);
            target = CreateRegisteredActor(registry, entities, initialHp);
            var actors = new MobaActorLookupService(_actorIndex, registry, entities, _contexts);
            var rules = new MobaCombatRulesService(actors);
            var snapshots = new MobaDamageEventSnapshotService(new MobaLogicWorldRunGateService());
            return new MobaDamageService(actors, snapshots, rules, eventBus: eventBus);
        }

        private ActorEntity CreateRegisteredActor(
            MobaActorRegistry registry,
            MobaEntityManager entities,
            float initialHp)
        {
            var entity = _contexts.actor.CreateEntity();
            var attributeContext = new AttributeContext();
            var attributeGroup = attributeContext.GetOrCreateGroup("health-commit-test");
            attributeGroup.SetBase(MobaAttributeIds.MAX_HP, MaxHp);
            var resources = new ResourceContainer
            {
                Map = new Dictionary<ResourceType, ResourceState>
                {
                    [ResourceType.Hp] = new ResourceState
                    {
                        Current = initialHp,
                        LastMax = MaxHp,
                        MaxAttribute = MobaAttributeIds.MAX_HP,
                    },
                },
            };
            var spec = CreateSpec();

            entity.AddActorId(TargetActorId);
            entity.AddTeam(spec.Info.Team);
            entity.AddEntityMainType(spec.Info.MainType);
            entity.AddUnitSubType(spec.Info.UnitSubType);
            entity.AddOwnerPlayerId(spec.Info.OwnerPlayer);
            entity.AddAttributeGroup(attributeGroup, attributeContext);
            entity.AddResourceContainer(resources, true);
            new MobaActorSpawnRegistrar(registry, entities).Register(
                entity,
                in spec,
                registerActor: true,
                registerEntityManager: true,
                registerEntityManagerFromEntity: true);
            return entity;
        }

        private static EventKey<MobaHealthChangeResult> CreateHealthCommittedKey()
        {
            return new EventKey<MobaHealthChangeResult>(
                TriggeringIdUtil.GetEventEid(DamagePipelineEvents.HealthCommitted));
        }

        private static EventKey<UnitDieEventPayload> CreateUnitDieKey()
        {
            return new EventKey<UnitDieEventPayload>(
                TriggeringIdUtil.GetEventEid(MobaUnitTriggering.Events.Die));
        }

        private static MobaActorBuildSpec CreateSpec()
        {
            var transform = Transform3.Identity;
            var info = new MobaEntityInfo(
                TargetActorId,
                MobaEntityKind.Hero,
                in transform,
                (Team)1,
                EntityMainType.Unit,
                UnitSubType.Hero,
                new PlayerId("health-commit-test"),
                templateId: 1001);
            return new MobaActorBuildSpec(
                in info,
                MobaActorBuildSourceKind.PlayerLoadout,
                sourceId: 1001,
                ownerActorId: 0);
        }
    }
}
