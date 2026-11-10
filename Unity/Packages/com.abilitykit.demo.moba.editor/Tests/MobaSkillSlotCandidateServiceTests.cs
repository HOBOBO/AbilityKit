using System;
using System.Collections.Generic;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.StateMachine;
using AbilityKit.HFSM.Definition;
using AbilityKit.Modifiers;
using NUnit.Framework;

namespace AbilityKit.Demo.Moba.Diagnostics.Tests
{
    public sealed class MobaSkillSlotCandidateServiceTests
    {
        private const int ActorId = 701;
        private const int OtherActorId = 702;
        private const int Slot = 1;
        private const int BaseSkillId = 1001;
        private const int MinigunSkillId = 2001;
        private const int RocketSkillId = 2002;
        private const int WeaponMode = 9001;

        [Test]
        public void ConditionalCandidatesUseTheEffectiveParameterValue()
        {
            using var candidates = new MobaSkillSlotCandidateService();
            using var parameters = new MobaCharacterHfsmParameterService();
            RegisterProfile(parameters, ActorId, WeaponMode);
            candidates.Add(ActorId, 11, Slot, MinigunSkillId, 10, 0,
                new MobaSkillSlotCandidateCondition(WeaponMode,
                    MobaSkillSlotCandidateCondition.Equal, 0f));
            candidates.Add(ActorId, 12, Slot, RocketSkillId, 10, 1,
                new MobaSkillSlotCandidateCondition(WeaponMode,
                    MobaSkillSlotCandidateCondition.Equal, 1f));

            Assert.That(candidates.Resolve(ActorId, Slot, BaseSkillId, parameters).EffectiveSkillId,
                Is.EqualTo(MinigunSkillId));

            SetOverride(parameters, ActorId, 99, WeaponMode, 1f);
            Assert.That(candidates.Resolve(ActorId, Slot, BaseSkillId, parameters).EffectiveSkillId,
                Is.EqualTo(RocketSkillId));
        }

        [Test]
        public void AllComparisonOperatorsAreDeterministic()
        {
            using var candidates = new MobaSkillSlotCandidateService();
            using var parameters = new MobaCharacterHfsmParameterService();
            RegisterProfile(parameters, ActorId, WeaponMode);
            SetOverride(parameters, ActorId, 99, WeaponMode, 2f);

            var comparisons = new[]
            {
                MobaSkillSlotCandidateCondition.NotEqual,
                MobaSkillSlotCandidateCondition.Greater,
                MobaSkillSlotCandidateCondition.GreaterOrEqual,
                MobaSkillSlotCandidateCondition.LessOrEqual,
            };
            var expected = new[] { 3001, 3002, 3003, 3004 };
            var values = new[] { 1f, 1f, 2f, 2f };
            for (var i = 0; i < comparisons.Length; i++)
            {
                candidates.Add(ActorId, 20 + i, Slot + i + 1, expected[i], 1, i,
                    new MobaSkillSlotCandidateCondition(WeaponMode, comparisons[i], values[i]));
                Assert.That(candidates.Resolve(ActorId, Slot + i + 1, BaseSkillId, parameters).EffectiveSkillId,
                    Is.EqualTo(expected[i]));
            }

            candidates.Add(ActorId, 30, 8, 3010, 1, 0,
                new MobaSkillSlotCandidateCondition(WeaponMode,
                    MobaSkillSlotCandidateCondition.Equal, 2.0002f));
            Assert.That(candidates.Resolve(ActorId, 8, BaseSkillId, parameters).EffectiveSkillId,
                Is.EqualTo(BaseSkillId));
        }

        [Test]
        public void UndeclaredConditionFailsLoudlyInsteadOfFallingBack()
        {
            using var candidates = new MobaSkillSlotCandidateService();
            using var parameters = new MobaCharacterHfsmParameterService();
            candidates.Add(ActorId, 11, Slot, RocketSkillId, 10, 0,
                new MobaSkillSlotCandidateCondition(WeaponMode,
                    MobaSkillSlotCandidateCondition.Equal, 1f));

            var exception = Assert.Throws<InvalidOperationException>(() =>
                candidates.Resolve(ActorId, Slot, BaseSkillId, parameters));
            Assert.That(exception.Message, Does.Contain($"HFSM parameter {WeaponMode}"));
        }

        [Test]
        public void PriorityThenOrderThenSourceIdSelectsTheSameCandidateEveryTime()
        {
            using var candidates = new MobaSkillSlotCandidateService();
            candidates.Add(ActorId, 30, Slot, 3001, 20, 3);
            candidates.Add(ActorId, 20, Slot, 3002, 20, 3);
            candidates.Add(ActorId, 10, Slot, 3003, 20, 2);
            candidates.Add(ActorId, 40, Slot, 3004, 21, 9);

            for (var i = 0; i < 5; i++)
                Assert.That(candidates.Resolve(ActorId, Slot, BaseSkillId).EffectiveSkillId,
                    Is.EqualTo(3004));

            candidates.ClearSource(ActorId, 40);
            Assert.That(candidates.Resolve(ActorId, Slot, BaseSkillId).EffectiveSkillId,
                Is.EqualTo(3003));
        }

        [Test]
        public void ParameterSnapshotRestoreSwitchesSlotWithoutLeakingAcrossActors()
        {
            using var candidates = new MobaSkillSlotCandidateService();
            using var parameters = new MobaCharacterHfsmParameterService();
            RegisterProfile(parameters, ActorId, WeaponMode);
            RegisterProfile(parameters, OtherActorId, WeaponMode);
            candidates.Add(ActorId, 11, Slot, MinigunSkillId, 10, 0,
                new MobaSkillSlotCandidateCondition(WeaponMode,
                    MobaSkillSlotCandidateCondition.Equal, 0f));
            candidates.Add(ActorId, 12, Slot, RocketSkillId, 10, 1,
                new MobaSkillSlotCandidateCondition(WeaponMode,
                    MobaSkillSlotCandidateCondition.Equal, 1f));
            candidates.Add(OtherActorId, 13, Slot, 2999, 10, 0);

            var snapshot = parameters.CaptureSnapshot();
            SetOverride(parameters, ActorId, 99, WeaponMode, 1f);
            Assert.That(candidates.Resolve(ActorId, Slot, BaseSkillId, parameters).EffectiveSkillId,
                Is.EqualTo(RocketSkillId));
            Assert.That(candidates.Resolve(OtherActorId, Slot, BaseSkillId, parameters).EffectiveSkillId,
                Is.EqualTo(2999));

            parameters.RestoreSnapshot(snapshot);
            Assert.That(candidates.Resolve(ActorId, Slot, BaseSkillId, parameters).EffectiveSkillId,
                Is.EqualTo(MinigunSkillId));
            Assert.That(candidates.Resolve(OtherActorId, Slot, BaseSkillId, parameters).EffectiveSkillId,
                Is.EqualTo(2999));
        }

        [Test]
        public void RemovingAnActorDropsItsProjectedCandidates()
        {
            using var candidates = new MobaSkillSlotCandidateService();
            candidates.Add(ActorId, 11, Slot, RocketSkillId, 10, 0);
            Assert.That(candidates.RemoveActor(ActorId), Is.True);
            Assert.That(candidates.Resolve(ActorId, Slot, BaseSkillId).EffectiveSkillId,
                Is.EqualTo(BaseSkillId));
            Assert.That(candidates.RemoveActor(ActorId), Is.False);
        }

        private static void RegisterProfile(MobaCharacterHfsmParameterService parameters, int actorId,
            int parameterId)
        {
            parameters.RegisterActorProfile(actorId, new StateMachineDefinition
            {
                DefinitionId = $"test-profile-{actorId}",
                Parameters = new List<StateMachineParameterDefinition>
                {
                    new StateMachineParameterDefinition
                    {
                        Id = parameterId,
                        Name = "WeaponMode",
                        SupportedOperations = new List<int> { (int)ModifierOp.Override },
                    },
                },
            });
        }

        private static void SetOverride(MobaCharacterHfsmParameterService parameters, int actorId,
            long sourceId, int parameterId, float value)
        {
            var modifier = ModifierData.Override(ModifierKey.FromPacked((uint)parameterId), value,
                sourceId: unchecked((int)sourceId));
            parameters.Set(actorId, sourceId, parameterId, in modifier);
        }
    }
}
