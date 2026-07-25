using System;
using AbilityKit.Ability.World.DI;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Share.Config;
using AbilityKit.Demo.Moba.Testing;
using NUnit.Framework;

namespace AbilityKit.Game.Test.UnitTest
{
    public sealed class MobaProductionConfigReferenceValidationTests
    {
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
