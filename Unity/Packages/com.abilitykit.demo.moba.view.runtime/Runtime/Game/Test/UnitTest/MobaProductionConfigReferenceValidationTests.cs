using System;
using AbilityKit.Ability.World.DI;
using AbilityKit.Game.Flow;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Share.Config;
using AbilityKit.Demo.Moba.Testing;
using AbilityKit.Demo.Moba.View.Config;
using NUnit.Framework;
using UnityEditor;

namespace AbilityKit.Game.Test.UnitTest
{
    public sealed class MobaProductionConfigReferenceValidationTests
    {
        private const string GatewayConfigPath =
            "Packages/com.abilitykit.demo.moba.view.runtime/Configs/BattleStart/BattleGatewayConfig.asset";
        private const string RemotePresetPath =
            "Packages/com.abilitykit.demo.moba.view.runtime/Configs/BattleStart/BattleStartPreset_远程.asset";

        [Test]
        public void PackageResources_DirectTextLoad_ShouldResolveActorStateMachineProfiles()
        {
            var loader = new ResourcesTextAssetLoader();

            Assert.That(loader.TryLoadText("moba/actor_state_machines", out var json), Is.True);
            StringAssert.Contains("ying_zheng_ultimate_projectile", json);
        }

        [Test]
        public void DefaultProductionResources_ShouldPassCompleteRuntimeValidationContract()
        {
            using (var harness = MobaSkillConfigTestHarness.CreateForSinglePlayer(
                       new[] { 10010101 },
                       worldId: "production_runtime_validation",
                       heroId: 1001,
                       attributeTemplateId: 1001,
                       validationMode: MobaRuntimeValidationMode.BootstrapStrict))
            {
                Assert.That(
                    harness.World.Services.TryResolve<IMobaRuntimeValidationHistory>(out var history),
                    Is.True,
                    "Strict bootstrap must register runtime validation history.");
                Assert.That(
                    history.TryGetLastReport(out var report),
                    Is.True,
                    "Strict bootstrap must execute the production validation contract.");
                Assert.That(report.ShouldBlockStartup, Is.False, report.FormatAllEntries());
            }
        }

        [Test]
        public void MultiplayerProductionAssets_ShouldUseCanonicalGatewayProtocol()
        {
            var gateway = AssetDatabase.LoadAssetAtPath<BattleGatewayConfigSO>(GatewayConfigPath);
            var preset = AssetDatabase.LoadAssetAtPath<BattleStartPresetSO>(RemotePresetPath);

            Assert.That(gateway, Is.Not.Null, GatewayConfigPath);
            Assert.That(preset, Is.Not.Null, RemotePresetPath);
            Assert.That(gateway.UseGatewayTransport, Is.True);
            Assert.That(gateway.Host, Is.EqualTo("127.0.0.1"));
            Assert.That(gateway.Port, Is.EqualTo(4000));
            Assert.That(gateway.RoomType, Is.EqualTo("moba"));
            Assert.That(gateway.RoomTitle, Is.EqualTo("MOBA Room"));
            Assert.That(gateway.MaxPlayers, Is.EqualTo(2));
            Assert.That(gateway.MinPlayers, Is.EqualTo(2));
            Assert.That(gateway.RoomListLimit, Is.EqualTo(10));
            Assert.That(gateway.RestoreRoomOnEntry, Is.True);
            Assert.That(gateway.AutoReadyDefaultLoadout, Is.True);
            Assert.That(gateway.DefaultHeroId, Is.EqualTo(1001));
            Assert.That(gateway.DefaultAttributeTemplateId, Is.EqualTo(1001));
            Assert.That(gateway.DefaultBasicAttackSkillId, Is.EqualTo(10010001));
            Assert.That(gateway.DefaultSkillIds, Is.EqualTo(new[] { 10010101, 10010201, 10010301 }));
            Assert.That(gateway.StarterSceneName, Is.EqualTo("MultiplayerStarterScene"));
            Assert.That(preset.HostMode, Is.EqualTo(BattleStartConfig.BattleHostMode.GatewayRemote));
            Assert.That(preset.GatewaySO, Is.SameAs(gateway));
            Assert.That(gateway.TryValidateFormalLobby(out var validationError), Is.True, validationError);

            var launchSpec = gateway.BuildRoomLaunchSpec("session", "release", "server-a");
            Assert.That(launchSpec.SessionToken, Is.EqualTo("session"));
            Assert.That(launchSpec.Region, Is.EqualTo("release"));
            Assert.That(launchSpec.ServerId, Is.EqualTo("server-a"));
            Assert.That(launchSpec.RoomType, Is.EqualTo("moba"));
            Assert.That(launchSpec.RoomTitle, Is.EqualTo("MOBA Room"));
            Assert.That(launchSpec.MaxPlayers, Is.EqualTo(2));
            Assert.That(launchSpec.MinPlayers, Is.EqualTo(2));

            var loadout = gateway.BuildDefaultLoadout();
            Assert.That(loadout.HeroId, Is.EqualTo(1001));
            Assert.That(loadout.TeamId, Is.EqualTo(1));
            Assert.That(loadout.SpawnPointId, Is.EqualTo(0));
            Assert.That(loadout.Level, Is.EqualTo(1));
            Assert.That(loadout.AttributeTemplateId, Is.EqualTo(1001));
            Assert.That(loadout.BasicAttackSkillId, Is.EqualTo(10010001));
            Assert.That(loadout.SkillIds, Is.EqualTo(new[] { 10010101, 10010201, 10010301 }));
        }

        [Test]
        public void SkillButtonTemplate_InvalidRawValues_AreReportedAsNonBlockingWarnings()
        {
            var config = new MobaTestConfigBuilder()
                .AddDtos(new SkillButtonTemplateDTO
                {
                    Id = 7101,
                    Name = "InvalidButtonTemplate",
                    AimMode = 2,
                    IndicatorShape = 9,
                    UsePointMode = -1,
                    DashDistance = -1f,
                    LongPressSeconds = 0f,
                    AimMaxRadius = 0f,
                })
                .BuildDatabase();
            var report = new MobaRuntimeValidationReport();
            var validator = new MobaBattleConfigReferenceValidator();

            validator.Validate(
                new MobaRuntimeValidationContext(new ConfigOnlyWorldResolver(config), "test"),
                report);

            Assert.That(report.ShouldBlockStartup, Is.False, report.FormatAllEntries());
            Assert.That(report.WarningCount, Is.GreaterThanOrEqualTo(4), report.FormatAllEntries());
            AssertWarning(report, "skillButtonTemplate.7101.aimMode");
            AssertWarning(report, "skillButtonTemplate.7101.indicatorShape");
            AssertWarning(report, "skillButtonTemplate.7101.usePointMode");
            AssertWarning(report, "skillButtonTemplate.7101.dashDistance");
            AssertNoWarning(report, "skillButtonTemplate.7101.longPressSeconds");
            AssertNoWarning(report, "skillButtonTemplate.7101.aimMaxRadius");
        }

        private static void AssertWarning(MobaRuntimeValidationReport report, string path)
        {
            foreach (var entry in report.Entries)
            {
                if (entry.Severity == MobaRuntimeValidationSeverity.Warning && entry.Path == path)
                {
                    return;
                }
            }

            Assert.Fail("Expected warning at " + path + ". " + report.FormatAllEntries());
        }

        private static void AssertNoWarning(MobaRuntimeValidationReport report, string path)
        {
            foreach (var entry in report.Entries)
            {
                Assert.That(
                    entry.Severity == MobaRuntimeValidationSeverity.Warning && entry.Path == path,
                    Is.False,
                    report.FormatAllEntries());
            }
        }

        private sealed class ConfigOnlyWorldResolver : IWorldResolver
        {
            private readonly MobaConfigDatabase _config;

            public ConfigOnlyWorldResolver(MobaConfigDatabase config)
            {
                _config = config;
            }

            public object Resolve(Type serviceType)
            {
                if (TryResolve(serviceType, out var service)) return service;
                throw new InvalidOperationException("Service not registered: " + serviceType);
            }

            public T Resolve<T>()
            {
                return (T)Resolve(typeof(T));
            }

            public bool TryResolve(Type serviceType, out object service)
            {
                if (serviceType == typeof(MobaConfigDatabase))
                {
                    service = _config;
                    return true;
                }

                service = null;
                return false;
            }

            public bool TryResolve<T>(out T service)
            {
                if (TryResolve(typeof(T), out var value) && value is T typed)
                {
                    service = typed;
                    return true;
                }

                service = default;
                return false;
            }
        }
    }
}
