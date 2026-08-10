using System;
using System.Collections.Generic;
using AbilityKit.Demo.Moba.Components;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.Passive;
using AbilityKit.Demo.Moba.Share.Config;
using AbilityKit.Trace;
using NUnit.Framework;

namespace AbilityKit.Demo.Moba.Diagnostics.Tests
{
    public sealed class MobaPassiveSkillLifecycleDiffTests
    {
        private const int ActorId = 901;
        private const int FirstPassiveSkillId = 1001;
        private const int SecondPassiveSkillId = 1002;
        private const int FirstTriggerId = 2001;
        private const int SecondTriggerId = 2002;

        private Contexts _contexts;
        private ActorEntity _entity;
        private MobaPassiveSkillLifecycleService _service;

        [SetUp]
        public void SetUp()
        {
            _contexts = new Contexts();
            _entity = _contexts.actor.CreateEntity();
            _entity.AddActorId(ActorId);
            _entity.AddSkillLoadout(
                Array.Empty<ActiveSkillRuntime>(),
                new[]
                {
                    CreatePassiveRuntime(FirstPassiveSkillId),
                    CreatePassiveRuntime(SecondPassiveSkillId),
                });
            _service = new MobaPassiveSkillLifecycleService(CreateConfigs(), new MobaTraceRegistry());
        }

        [TearDown]
        public void TearDown()
        {
            _service?.UnregisterActor(_entity, 99);
            _service?.Dispose();
            _contexts?.Reset();
        }

        [Test]
        public void SyncActorPassives_WithTwoNewOwners_CommitsPlansOnce()
        {
            _service.SyncActorPassives(_entity, 1);

            Assert.That(_entity.hasOngoingTriggerPlans, Is.True);
            Assert.That(_entity.ongoingTriggerPlans.Revision, Is.EqualTo(1));
            Assert.That(_entity.ongoingTriggerPlans.Active, Has.Count.EqualTo(2));
            Assert.That(FindOwnerKey(FirstTriggerId), Is.Not.Zero);
            Assert.That(FindOwnerKey(SecondTriggerId), Is.Not.Zero);
        }

        [Test]
        public void SyncActorPassives_WhenDesiredPlansAreUnchanged_PreservesComponentAndRevision()
        {
            _service.SyncActorPassives(_entity, 1);
            var component = _entity.ongoingTriggerPlans;
            var plans = component.Active;

            _service.SyncActorPassives(_entity, 2);

            Assert.That(_entity.ongoingTriggerPlans, Is.SameAs(component));
            Assert.That(_entity.ongoingTriggerPlans.Active, Is.SameAs(plans));
            Assert.That(_entity.ongoingTriggerPlans.Revision, Is.EqualTo(1));
        }

        [Test]
        public void SyncActorPassives_WhenOwnerIsRemoved_CommitsOnceAndDropsRemovedBinding()
        {
            _service.SyncActorPassives(_entity, 1);
            var removedOwnerKey = FindOwnerKey(FirstTriggerId);
            var retainedOwnerKey = FindOwnerKey(SecondTriggerId);

            _entity.ReplaceSkillLoadout(
                Array.Empty<ActiveSkillRuntime>(),
                new[] { CreatePassiveRuntime(SecondPassiveSkillId) });
            _service.SyncActorPassives(_entity, 2);

            Assert.That(_entity.hasOngoingTriggerPlans, Is.True);
            Assert.That(_entity.ongoingTriggerPlans.Revision, Is.EqualTo(2));
            Assert.That(_entity.ongoingTriggerPlans.Active, Has.Count.EqualTo(1));
            Assert.That(_entity.ongoingTriggerPlans.Active[0].OwnerKey, Is.EqualTo(retainedOwnerKey));
            Assert.That(_service.IsPassiveOwnerKey(removedOwnerKey), Is.False);
            Assert.That(_service.IsPassiveOwnerKey(retainedOwnerKey), Is.True);
        }

        [Test]
        public void UnregisterActor_RemovesPlansListenersAndOwnerBindings()
        {
            _service.SyncActorPassives(_entity, 1);
            var firstOwnerKey = FindOwnerKey(FirstTriggerId);
            var secondOwnerKey = FindOwnerKey(SecondTriggerId);

            _service.UnregisterActor(_entity, 2);

            Assert.That(_entity.hasOngoingTriggerPlans, Is.False);
            Assert.That(_entity.passiveSkillTriggerListeners.Active, Is.Empty);
            Assert.That(_service.IsPassiveOwnerKey(firstOwnerKey), Is.False);
            Assert.That(_service.IsPassiveOwnerKey(secondOwnerKey), Is.False);
        }

        private long FindOwnerKey(int triggerId)
        {
            var plans = _entity.ongoingTriggerPlans.Active;
            for (int i = 0; i < plans.Count; i++)
            {
                var entry = plans[i];
                if (entry?.TriggerIds == null) continue;
                for (int j = 0; j < entry.TriggerIds.Length; j++)
                {
                    if (entry.TriggerIds[j] == triggerId) return entry.OwnerKey;
                }
            }

            return 0;
        }

        private static PassiveSkillRuntime CreatePassiveRuntime(int passiveSkillId)
        {
            return new PassiveSkillRuntime { PassiveSkillId = passiveSkillId, Level = 1 };
        }

        private static MobaConfigDatabase CreateConfigs()
        {
            var configs = new MobaConfigDatabase();
            var result = configs.ReloadFromDtoArrays(
                new Dictionary<Type, Array>
                {
                    [typeof(PassiveSkillDTO)] = new[]
                    {
                        new PassiveSkillDTO
                        {
                            Id = FirstPassiveSkillId,
                            Name = "passive-diff-first",
                            TriggerIds = new[] { FirstTriggerId },
                            ContinuousProcessIds = Array.Empty<int>(),
                        },
                        new PassiveSkillDTO
                        {
                            Id = SecondPassiveSkillId,
                            Name = "passive-diff-second",
                            TriggerIds = new[] { SecondTriggerId },
                            ContinuousProcessIds = Array.Empty<int>(),
                        },
                    },
                },
                strict: false);
            Assert.That(result.Succeeded, Is.True, result.Error);
            return configs;
        }
    }
}
