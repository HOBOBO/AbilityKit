using AbilityKit.Demo.Moba.Components;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Deterministic;
using NUnit.Framework;

namespace AbilityKit.Demo.Moba.Diagnostics.Tests
{
    public sealed class MobaShieldTransactionTests
    {
        private const int TargetActorId = 11;

        [Test]
        public void PreviewAbsorb_DoesNotMutateShieldState()
        {
            var service = CreateServiceWithShield(40f);
            var plan = service.PreviewAbsorb(
                CreateAttack(),
                Fixed64.FromSingle(25f));

            Assert.That(plan.Absorbed, Is.EqualTo(Fixed64.FromSingle(25f)));
            Assert.That(service.GetTotalRemaining(TargetActorId), Is.EqualTo(40f));
        }

        [Test]
        public void CommitAndRollback_RestoresOriginalShieldValue()
        {
            var service = CreateServiceWithShield(40f);
            var plan = service.PreviewAbsorb(
                CreateAttack(),
                Fixed64.FromSingle(25f));

            Assert.That(service.CommitAbsorb(plan), Is.True);
            Assert.That(service.GetTotalRemaining(TargetActorId), Is.EqualTo(15f));
            service.RollbackAbsorb(plan);
            Assert.That(service.GetTotalRemaining(TargetActorId), Is.EqualTo(40f));
        }

        [Test]
        public void DepletedLayer_IsRemovedOnlyAfterFinalize()
        {
            var service = CreateServiceWithShield(20f);
            var plan = service.PreviewAbsorb(
                CreateAttack(),
                Fixed64.FromSingle(20f));

            Assert.That(service.CommitAbsorb(plan), Is.True);
            Assert.That(service.TryGetContainer(TargetActorId, out var committed), Is.True);
            Assert.That(committed.Layers, Has.Count.EqualTo(1));

            service.FinalizeAbsorb(plan);

            Assert.That(service.TryGetContainer(TargetActorId, out var finalized), Is.True);
            Assert.That(finalized.Layers, Is.Empty);
        }

        private static MobaShieldService CreateServiceWithShield(float value)
        {
            var service = new MobaShieldService();
            service.AddShield(TargetActorId, new ShieldLayer
            {
                ShieldId = 101,
                SourceActorId = 7,
                CurrentValue = Fixed64.FromSingle(value),
                MaxValue = Fixed64.FromSingle(value),
                InitialValue = Fixed64.FromSingle(value),
                AbsorbRatio = Fixed64.One,
                StackingPolicy = ShieldStackingPolicy.Independent,
                ConsumePolicy = ShieldConsumePolicy.PriorityThenOldest,
            });
            return service;
        }

        private static AttackInfo CreateAttack()
        {
            return new AttackInfo
            {
                AttackerActorId = 7,
                TargetActorId = TargetActorId,
                DamageType = DamageType.Physical,
            };
        }
    }
}
