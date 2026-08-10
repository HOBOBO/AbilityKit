using System;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.Host.Extensions.Moba.CreateWorld;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Protocol.Moba;
using NUnit.Framework;

namespace AbilityKit.Demo.Moba.Diagnostics.Tests
{
    public sealed class MobaBattleStartupContractTests
    {
        private static readonly PlayerId Player = new PlayerId("player-1");

        [Test]
        public void BattleStartPlan_DeepCopiesConstructionInputsAndPropertyOutputs()
        {
            var skillIds = new[] { 101, 102 };
            var players = new[] { CreateLoadout(skillIds) };
            var payload = new byte[] { 7, 8 };
            var spec = new MobaCreateWorldSpec("match-1", 1, 17, 30, 2, players, 9);

            var plan = new MobaBattleStartPlan(Player, in spec, 21, payload);
            skillIds[0] = 901;
            players[0] = CreateLoadout(new[] { 902 });
            payload[0] = 9;

            var firstSpec = plan.CreateWorldSpec;
            var firstPayload = plan.EnterGamePayload;
            Assert.That(firstSpec.Players[0].SkillIds[0], Is.EqualTo(101));
            Assert.That(firstPayload[0], Is.EqualTo(7));

            firstSpec.Players[0].SkillIds[0] = 801;
            firstSpec.Players[0] = CreateLoadout(new[] { 802 });
            firstPayload[0] = 8;

            var secondSpec = plan.CreateWorldSpec;
            Assert.That(secondSpec.Players[0].SkillIds, Is.EqualTo(new[] { 101, 102 }));
            Assert.That(plan.EnterGamePayload, Is.EqualTo(new byte[] { 7, 8 }));
            Assert.That(secondSpec.Players, Is.Not.SameAs(firstSpec.Players));
            Assert.That(secondSpec.Players[0].SkillIds, Is.Not.SameAs(firstSpec.Players[0].SkillIds));
        }

        [Test]
        public void BattleStartPlanValidator_ReportsValidAndInvalidProtocolProjection()
        {
            var valid = CreatePlan();
            var invalidSpec = new MobaCreateWorldSpec(
                "match-1",
                1,
                17,
                0,
                2,
                new[] { CreateLoadout(new[] { 101 }) },
                9);
            var invalid = new MobaBattleStartPlan(Player, in invalidSpec);

            var validResult = MobaBattleStartPlanValidator.Validate(in valid);
            var invalidResult = MobaBattleStartPlanValidator.Validate(in invalid);

            Assert.That(validResult.Succeeded, Is.True, validResult.Message);
            Assert.That(invalidResult.Succeeded, Is.False);
            StringAssert.Contains("InvalidTickRate", invalidResult.Message);
        }

        [Test]
        public void CommandContractRegistry_DefaultIsReadyAndLegacyDeclarationIsIncomplete()
        {
            var defaults = MobaInputCommandContractRegistry.CreateDefault().Validate();
            var legacy = new MobaInputCommandContractRegistry(MobaInputCommandHandlerRegistry.CreateEmpty());
            legacy.Require(9901, typeof(MobaMoveInputCommandHandler), "LegacyMove");
            var legacyResult = legacy.Validate();

            Assert.That(defaults.Succeeded, Is.True, string.Join("; ", defaults.Errors));
            Assert.That(legacyResult.Succeeded, Is.False);
            Assert.That(legacyResult.Errors, Has.Some.Contains("authority is unspecified"));
            Assert.That(legacyResult.Errors, Has.Some.Contains("payload validator is missing"));
        }

        [Test]
        public void CommandContractRegistry_UsesStableRuntimeFailureCodes()
        {
            const int opCode = 9902;
            var registry = CreateCommandRegistry(opCode);
            var map = new MobaPlayerActorMapService();
            var context = new MobaInputCommandContext(null, map, null, null, null);
            var frame = new FrameIndex(10);

            AssertRejected(
                registry,
                context,
                frame,
                new PlayerInputCommand(frame, Player, 9999, new byte[] { 1 }),
                MobaInputCommandFailureCode.ContractMissing);
            AssertRejected(
                registry,
                context,
                frame,
                new PlayerInputCommand(frame, Player, opCode, new byte[] { 1 }),
                MobaInputCommandFailureCode.AuthorityRejected);

            map.Bind(Player, 101);
            AssertRejected(
                registry,
                context,
                frame,
                new PlayerInputCommand(new FrameIndex(11), Player, opCode, new byte[] { 1 }),
                MobaInputCommandFailureCode.FramePolicyRejected);
            AssertRejected(
                registry,
                context,
                frame,
                new PlayerInputCommand(frame, Player, opCode, Array.Empty<byte>()),
                MobaInputCommandFailureCode.PayloadInvalid);

            var accepted = registry.TryValidateCommand(
                context,
                frame,
                new PlayerInputCommand(frame, Player, opCode, new byte[] { 1 }),
                out var result);
            Assert.That(accepted, Is.True);
            Assert.That(result, Is.EqualTo(default(MobaInputCommandResult)));
        }

        [Test]
        public void SnapshotContractProfiles_ResolveExactThenDefaultAndReportMissingProfiles()
        {
            var exact = new MobaSnapshotOutputContract();
            var fallback = new MobaSnapshotOutputContract();
            var registry = new MobaSnapshotContractProfileRegistry();
            registry.RegisterDefault(fallback);
            registry.Register(7, exact);

            Assert.That(registry.TryResolve(7, out var resolvedExact, out var exactError), Is.True, exactError);
            Assert.That(resolvedExact, Is.SameAs(exact));
            Assert.That(registry.TryResolve(8, out var resolvedFallback, out var fallbackError), Is.True, fallbackError);
            Assert.That(resolvedFallback, Is.SameAs(fallback));

            var empty = new MobaSnapshotContractProfileRegistry();
            Assert.That(empty.TryResolve(8, out _, out var missingError), Is.False);
            StringAssert.Contains("not declared", missingError);
            Assert.That(empty.TryResolve(0, out _, out var invalidError), Is.False);
            StringAssert.Contains("must be positive", invalidError);
            Assert.Throws<ArgumentOutOfRangeException>(() => empty.Register(0, exact));
        }

        private static MobaBattleStartPlan CreatePlan()
        {
            var spec = new MobaCreateWorldSpec(
                "match-1",
                1,
                17,
                30,
                2,
                new[] { CreateLoadout(new[] { 101, 102 }) },
                9);
            return new MobaBattleStartPlan(Player, in spec, 21, new byte[] { 7, 8 });
        }

        private static MobaPlayerLoadout CreateLoadout(int[] skillIds)
        {
            return new MobaPlayerLoadout(
                Player,
                teamId: 1,
                heroId: 1001,
                attributeTemplateId: 2001,
                level: 1,
                basicAttackSkillId: 100,
                skillIds,
                spawnIndex: 0);
        }

        private static MobaInputCommandContractRegistry CreateCommandRegistry(int opCode)
        {
            var registry = new MobaInputCommandContractRegistry(MobaInputCommandHandlerRegistry.CreateEmpty());
            registry.Require(
                opCode,
                typeof(MobaMoveInputCommandHandler),
                "TestMove",
                MobaInputCommandAuthority.BattlePlayer,
                MobaInputCommandFramePolicy.ExactBatchFrame,
                "one-byte-test-payload",
                ValidateOneBytePayload);
            return registry;
        }

        private static bool ValidateOneBytePayload(byte[] payload, out string error)
        {
            var valid = payload != null && payload.Length == 1;
            error = valid ? null : "payload must contain exactly one byte";
            return valid;
        }

        private static void AssertRejected(
            MobaInputCommandContractRegistry registry,
            MobaInputCommandContext context,
            FrameIndex frame,
            PlayerInputCommand command,
            MobaInputCommandFailureCode expected)
        {
            var accepted = registry.TryValidateCommand(context, frame, command, out var result);
            Assert.That(accepted, Is.False);
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.FailureCode, Is.EqualTo(expected));
        }
    }
}
