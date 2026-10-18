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
        public void PresentationSnapshot_SeparatesPersonalAndSharedValue()
        {
            var service = CreateServiceWithShield(12f);
            service.AddSharedShield(new[] { TargetActorId, 12 }, new ShieldLayer
            {
                ShieldId = 401,
                SourceActorId = 7,
                CurrentValue = Fixed64.FromSingle(20f),
                MaxValue = Fixed64.FromSingle(20f),
                InitialValue = Fixed64.FromSingle(20f),
                AbsorbRatio = Fixed64.One,
                SharePolicy = ShieldSharePolicy.SharedTotalPool,
            });

            var snapshot = service.GetPresentationSnapshot(TargetActorId);

            Assert.That(snapshot.ActorId, Is.EqualTo(TargetActorId));
            Assert.That(snapshot.TotalPersonalValue, Is.EqualTo(Fixed64.FromSingle(12f)));
            Assert.That(snapshot.TotalSharedValue, Is.EqualTo(Fixed64.FromSingle(20f)));
            Assert.That(snapshot.TotalVisibleValue, Is.EqualTo(Fixed64.FromSingle(32f)));
            Assert.That(snapshot.PrimaryPoolId, Is.GreaterThan(0));
            Assert.That(snapshot.HasShared, Is.True);
        }

        [Test]
        public void PresentationSnapshot_UsesPoolBalanceAfterSharedConsumption()
        {
            var service = new MobaShieldService();
            service.AddSharedShield(new[] { TargetActorId, 12 }, new ShieldLayer
            {
                ShieldId = 402,
                SourceActorId = 7,
                CurrentValue = Fixed64.FromSingle(30f),
                MaxValue = Fixed64.FromSingle(30f),
                InitialValue = Fixed64.FromSingle(30f),
                AbsorbRatio = Fixed64.One,
                SharePolicy = ShieldSharePolicy.SharedTotalPool,
            });

            var plan = service.PreviewAbsorb(CreateAttack(), Fixed64.FromSingle(8f));
            Assert.That(service.CommitAbsorb(plan), Is.True);

            var first = service.GetPresentationSnapshot(TargetActorId);
            var second = service.GetPresentationSnapshot(12);
            Assert.That(first.TotalSharedValue, Is.EqualTo(Fixed64.FromSingle(22f)));
            Assert.That(second.TotalSharedValue, Is.EqualTo(Fixed64.FromSingle(22f)));
            Assert.That(first.PrimaryPoolVersion, Is.EqualTo(second.PrimaryPoolVersion));
        }

        [Test]
        public void SharedPool_DeduplicatesTargetsWithoutDuplicateBindings()
        {
            var service = new MobaShieldService();
            var bindingId = service.AddSharedShield(new[] { 11, 11, 0, 12, 12 }, new ShieldLayer
            {
                ShieldId = 403,
                SourceActorId = 7,
                CurrentValue = Fixed64.FromSingle(30f),
                MaxValue = Fixed64.FromSingle(30f),
                InitialValue = Fixed64.FromSingle(30f),
                AbsorbRatio = Fixed64.One,
                SharePolicy = ShieldSharePolicy.SharedTotalPool,
            });

            Assert.That(bindingId, Is.GreaterThan(0));
            Assert.That(service.GetLayerCount(11), Is.EqualTo(1));
            Assert.That(service.GetLayerCount(12), Is.EqualTo(1));
            Assert.That(service.TryGetSharedPool(service.GetSharedPoolIdForActor(11), out var pool), Is.True);
            Assert.That(pool.MemberCount, Is.EqualTo(2));
        }

        [Test]
        public void RemoveActor_LeavesAllSharedPoolsWhenMultipleBindingsExist()
        {
            var service = new MobaShieldService();
            var first = service.AddSharedShield(new[] { 11, 12 }, new ShieldLayer
            {
                ShieldId = 404,
                SourceActorId = 7,
                CurrentValue = Fixed64.FromSingle(10f),
                MaxValue = Fixed64.FromSingle(10f),
                InitialValue = Fixed64.FromSingle(10f),
                AbsorbRatio = Fixed64.One,
                SharePolicy = ShieldSharePolicy.SharedTotalPool,
            });
            var second = service.AddSharedShield(new[] { 11, 13 }, new ShieldLayer
            {
                ShieldId = 405,
                SourceActorId = 7,
                CurrentValue = Fixed64.FromSingle(10f),
                MaxValue = Fixed64.FromSingle(10f),
                InitialValue = Fixed64.FromSingle(10f),
                AbsorbRatio = Fixed64.One,
                SharePolicy = ShieldSharePolicy.SharedTotalPool,
            });

            Assert.That(first, Is.GreaterThan(0));
            Assert.That(second, Is.GreaterThan(0));
            Assert.That(service.RemoveActor(11), Is.True);
            Assert.That(service.GetSharedPoolIdForActor(11), Is.Zero);
            Assert.That(service.GetLayerCount(11), Is.Zero);
            Assert.That(service.TryGetSharedPool(service.GetSharedPoolIdForActor(12), out var firstPool), Is.True);
            Assert.That(firstPool.ActiveMemberCount, Is.EqualTo(1));
            Assert.That(service.TryGetSharedPool(service.GetSharedPoolIdForActor(13), out var secondPool), Is.True);
            Assert.That(secondPool.ActiveMemberCount, Is.EqualTo(1));
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
            Assert.That(service.GetLayerCount(TargetActorId), Is.EqualTo(1));

            service.FinalizeAbsorb(plan);

            Assert.That(service.GetLayerCount(TargetActorId), Is.Zero);
        }

        [Test]
        public void ExpiredLayer_IsNotEligibleAtExpireFrame()
        {
            var service = new MobaShieldService();
            service.AddShield(TargetActorId, new ShieldLayer
            {
                ShieldId = 101,
                SourceActorId = 7,
                CurrentValue = Fixed64.FromSingle(20f),
                MaxValue = Fixed64.FromSingle(20f),
                InitialValue = Fixed64.FromSingle(20f),
                StartFrame = 10,
                ExpireFrame = 20,
                AbsorbRatio = Fixed64.One,
                StackingPolicy = ShieldStackingPolicy.Independent,
                ConsumePolicy = ShieldConsumePolicy.PriorityThenOldest,
            });

            var attack = CreateAttack();
            var active = service.PreviewAbsorb(attack, Fixed64.FromSingle(5f), currentFrame: 19);
            var expired = service.PreviewAbsorb(attack, Fixed64.FromSingle(5f), currentFrame: 20);

            Assert.That(active.Absorbed, Is.EqualTo(Fixed64.FromSingle(5f)));
            Assert.That(expired.Absorbed, Is.EqualTo(Fixed64.Zero));
        }

        [Test]
        public void FinalDamage_IsResolvedBeforeShieldConsumption()
        {
            var service = CreateServiceWithShield(10f);
            var attack = CreateAttack();
            attack.FinalDamage.FixedBaseValue = Fixed64.FromSingle(20f);
            var calc = new AttackCalcInfo(attack);
            calc.MitigatedDamage.FixedBaseValue = Fixed64.FromSingle(5f);

            new MobaFinalDamagePipelineStage().Execute(calc);
            new MobaShieldAbsorbPipelineStage(service).Execute(calc);

            Assert.That(calc.ShieldAbsorb.FixedValue, Is.EqualTo(Fixed64.FromSingle(10f)));
            Assert.That(calc.HpDamage.FixedValue, Is.EqualTo(Fixed64.FromSingle(10f)));

            var cappedService = CreateServiceWithShield(10f);
            var cappedAttack = CreateAttack();
            cappedAttack.FinalDamage.FixedBaseValue = Fixed64.FromSingle(4f);
            var cappedCalc = new AttackCalcInfo(cappedAttack);
            cappedCalc.MitigatedDamage.FixedBaseValue = Fixed64.FromSingle(15f);

            new MobaFinalDamagePipelineStage().Execute(cappedCalc);
            new MobaShieldAbsorbPipelineStage(cappedService).Execute(cappedCalc);

            Assert.That(cappedCalc.FinalDamage.FixedValue, Is.EqualTo(Fixed64.FromSingle(4f)));
            Assert.That(cappedCalc.ShieldAbsorb.FixedValue, Is.EqualTo(Fixed64.FromSingle(4f)));
            Assert.That(cappedCalc.HpDamage.FixedValue, Is.Zero);
        }

        [Test]
        public void SharedPool_IsConsumedOnceAcrossDifferentMembers()
        {
            var service = new MobaShieldService();
            var poolBinding = service.AddSharedShield(new[] { 11, 12 }, new ShieldLayer
            {
                ShieldId = 201,
                SourceActorId = 7,
                CurrentValue = Fixed64.FromSingle(30f),
                MaxValue = Fixed64.FromSingle(30f),
                InitialValue = Fixed64.FromSingle(30f),
                AbsorbRatio = Fixed64.One,
                SharePolicy = ShieldSharePolicy.SharedTotalPool,
                StackingPolicy = ShieldStackingPolicy.Independent,
                ConsumePolicy = ShieldConsumePolicy.PriorityThenOldest,
            });

            Assert.That(poolBinding, Is.GreaterThan(0));
            Assert.That(service.GetTotalRemaining(11), Is.EqualTo(30f));
            Assert.That(service.GetTotalRemaining(12), Is.EqualTo(30f));

            var first = service.PreviewAbsorb(new AttackInfo { TargetActorId = 11, DamageType = DamageType.Physical }, Fixed64.FromSingle(20f));
            Assert.That(service.CommitAbsorb(first), Is.True);
            Assert.That(service.GetTotalRemaining(11), Is.EqualTo(10f));
            Assert.That(service.GetTotalRemaining(12), Is.EqualTo(10f));

            var second = service.PreviewAbsorb(new AttackInfo { TargetActorId = 12, DamageType = DamageType.Physical }, Fixed64.FromSingle(15f));
            Assert.That(second.Absorbed, Is.EqualTo(Fixed64.FromSingle(10f)));
        }

        [Test]
        public void SharedPool_LeaveAndJoinChangeEligibilityAtFrameBoundary()
        {
            var service = new MobaShieldService();
            service.AddSharedShield(new[] { 11 }, new ShieldLayer
            {
                ShieldId = 202, SourceActorId = 7,
                CurrentValue = Fixed64.FromSingle(20f), MaxValue = Fixed64.FromSingle(20f), InitialValue = Fixed64.FromSingle(20f),
                SharePolicy = ShieldSharePolicy.SharedTotalPool, AbsorbRatio = Fixed64.One,
            });
            var poolId = service.GetSharedPoolIdForActor(11, 202);
            Assert.That(poolId, Is.GreaterThan(0));
            Assert.That(service.LeaveSharedPool(poolId, 11, 10), Is.True);
            Assert.That(service.PreviewAbsorb(new AttackInfo { TargetActorId = 11 }, Fixed64.FromSingle(5f), 10).Absorbed, Is.Zero);
            Assert.That(service.JoinSharedPool(poolId, 12, 11), Is.True);
            Assert.That(service.PreviewAbsorb(new AttackInfo { TargetActorId = 12 }, Fixed64.FromSingle(5f), 10).Absorbed, Is.Zero);
            Assert.That(service.PreviewAbsorb(new AttackInfo { TargetActorId = 12 }, Fixed64.FromSingle(5f), 11).Absorbed, Is.EqualTo(Fixed64.FromSingle(5f)));
        }

        [Test]
        public void SharedPool_RollbackRestoresAuthoritativePoolBalance()
        {
            var service = new MobaShieldService();
            service.AddSharedShield(new[] { 11, 12 }, new ShieldLayer
            {
                ShieldId = 203, SourceActorId = 7,
                CurrentValue = Fixed64.FromSingle(25f), MaxValue = Fixed64.FromSingle(25f), InitialValue = Fixed64.FromSingle(25f),
                SharePolicy = ShieldSharePolicy.SharedTotalPool, AbsorbRatio = Fixed64.One,
            });
            var plan = service.PreviewAbsorb(new AttackInfo { TargetActorId = 12 }, Fixed64.FromSingle(10f));
            Assert.That(service.CommitAbsorb(plan), Is.True);
            service.RollbackAbsorb(plan);
            Assert.That(service.GetTotalRemaining(11), Is.EqualTo(25f));
            Assert.That(service.GetTotalRemaining(12), Is.EqualTo(25f));
        }

        [Test]
        public void PersonalShield_TransferMovesRemainingValueToTarget()
        {
            var service = CreateServiceWithShield(18f);
            var instanceId = 1;
            Assert.That(service.TransferShield(TargetActorId, instanceId, 12, 3), Is.True);
            Assert.That(service.GetTotalRemaining(TargetActorId), Is.Zero);
            Assert.That(service.GetTotalRemaining(12), Is.EqualTo(18f));
        }

        [Test]
        public void SharedPool_MemberConsumeLimitIsCumulative()
        {
            var service = new MobaShieldService();
            service.AddSharedShield(new[] { 11 }, new ShieldLayer
            {
                ShieldId = 204, SourceActorId = 7,
                CurrentValue = Fixed64.FromSingle(50f), MaxValue = Fixed64.FromSingle(50f), InitialValue = Fixed64.FromSingle(50f),
                SharePolicy = ShieldSharePolicy.SharedTotalPool, AbsorbRatio = Fixed64.One,
            });
            var poolId = service.GetSharedPoolIdForActor(11, 204);
            Assert.That(service.LeaveSharedPool(poolId, 11, 1), Is.True);
            Assert.That(service.JoinSharedPool(poolId, 11, 2, Fixed64.One, Fixed64.FromSingle(10f)), Is.True);

            var first = service.PreviewAbsorb(new AttackInfo { TargetActorId = 11 }, Fixed64.FromSingle(8f), 2);
            Assert.That(service.CommitAbsorb(first), Is.True);
            var second = service.PreviewAbsorb(new AttackInfo { TargetActorId = 11 }, Fixed64.FromSingle(8f), 2);
            Assert.That(second.Absorbed, Is.EqualTo(Fixed64.FromSingle(2f)));
        }

        [Test]
        public void RemoveSharedPool_RemovesEveryActorBinding()
        {
            var service = new MobaShieldService();
            service.AddSharedShield(new[] { 11, 12 }, new ShieldLayer
            {
                ShieldId = 205, SourceActorId = 7,
                CurrentValue = Fixed64.FromSingle(20f), MaxValue = Fixed64.FromSingle(20f), InitialValue = Fixed64.FromSingle(20f),
                SharePolicy = ShieldSharePolicy.SharedTotalPool, AbsorbRatio = Fixed64.One,
            });
            var poolId = service.GetSharedPoolIdForActor(11, 205);
            Assert.That(service.RemoveSharedPool(poolId), Is.True);
            Assert.That(service.GetTotalRemaining(11), Is.Zero);
            Assert.That(service.GetTotalRemaining(12), Is.Zero);
            Assert.That(service.TryGetSharedPool(poolId, out _), Is.False);
        }

        [Test]
        public void RemoveShield_OnSharedBindingReportsSuccessAndDetachesPoolMember()
        {
            var service = new MobaShieldService();
            service.AddSharedShield(new[] { 11, 12 }, new ShieldLayer
            {
                ShieldId = 208, SourceActorId = 7,
                CurrentValue = Fixed64.FromSingle(20f), MaxValue = Fixed64.FromSingle(20f), InitialValue = Fixed64.FromSingle(20f),
                SharePolicy = ShieldSharePolicy.SharedTotalPool, AbsorbRatio = Fixed64.One,
            });
            Assert.That(service.RemoveShield(11, 1), Is.True);
            Assert.That(service.GetSharedPoolIdForActor(11, 208), Is.Zero);
            Assert.That(service.GetSharedPoolIdForActor(12, 208), Is.GreaterThan(0));
        }

        [Test]
        public void SharedPool_ExpiredFrameRemovesBindings()
        {
            var service = new MobaShieldService();
            service.AddSharedShield(new[] { 11, 12 }, new ShieldLayer
            {
                ShieldId = 206, SourceActorId = 7,
                CurrentValue = Fixed64.FromSingle(20f), MaxValue = Fixed64.FromSingle(20f), InitialValue = Fixed64.FromSingle(20f),
                StartFrame = 5, ExpireFrame = 10,
                SharePolicy = ShieldSharePolicy.SharedTotalPool, AbsorbRatio = Fixed64.One,
            });
            Assert.That(service.CleanupExpired(10), Is.GreaterThanOrEqualTo(0));
            Assert.That(service.GetSharedPoolIdForActor(11, 206), Is.Zero);
            Assert.That(service.GetSharedPoolIdForActor(12, 206), Is.Zero);
        }

        [Test]
        public void SharedPool_TransferToExistingMemberDoesNotDetachSource()
        {
            var service = new MobaShieldService();
            service.AddSharedShield(new[] { 11, 12 }, new ShieldLayer
            {
                ShieldId = 207, SourceActorId = 7,
                CurrentValue = Fixed64.FromSingle(20f), MaxValue = Fixed64.FromSingle(20f), InitialValue = Fixed64.FromSingle(20f),
                SharePolicy = ShieldSharePolicy.SharedTotalPool, AbsorbRatio = Fixed64.One,
            });
            Assert.That(service.TransferShield(11, 1, 12, 3), Is.False);
            Assert.That(service.GetSharedPoolIdForActor(11, 207), Is.GreaterThan(0));
            Assert.That(service.GetSharedPoolIdForActor(12, 207), Is.GreaterThan(0));
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
