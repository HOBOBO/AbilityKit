using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace AbilityKit.Triggering.Tests
{
    public sealed class RuntimeCompatibilityLegacyEntryScanTests
    {
        private static readonly Regex HistoricalEntrypoint = new Regex(
            @"AbilityKit\.Triggering\.Runtime\.Scheduler|TriggerDispatcherHub|TriggerDispatcherRegistry|EventBusDispatcher|TimedDispatcher|\b(ExecutableRegistry|ExecutableDescriptor|ConditionDescriptor|ExecutableTriggerDatabase|ScheduledExecutor)\b",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        [TestCase("using AbilityKit.Triggering.Runtime.Executable; ExecutionResult result;", false)]
        [TestCase("using AbilityKit.Triggering.Runtime.Executable; ExecutableRegistry.Get(1);", true)]
        [TestCase("AbilityKit.Triggering.Runtime.Executable.ScheduledExecutor executor;", true)]
        public void HistoricalScan_DistinguishesRetainedResultFromRetiredTypes(string source, bool isForbidden)
        {
            Assert.That(HistoricalEntrypoint.IsMatch(source), Is.EqualTo(isForbidden));
        }

        [Test]
        public void PackageExternalSources_DoNotReferenceHistoricalTriggeringEntrypoints()
        {
            var packagesRoot = FindPackagesRoot();
            var violations = new List<string>();

            foreach (var file in Directory.EnumerateFiles(packagesRoot, "*.cs", SearchOption.AllDirectories))
            {
                var normalized = file.Replace('\\', '/');
                if (normalized.Contains("/com.abilitykit.triggering/", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (normalized.Contains("/Documentation~/", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (normalized.Contains("/Document/", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (normalized.Contains("/Docs/", StringComparison.OrdinalIgnoreCase))
                    continue;

                var match = HistoricalEntrypoint.Match(File.ReadAllText(file));
                if (match.Success)
                {
                    violations.Add($"{normalized}: {match.Value}");
                }
            }

            Assert.That(violations, Is.Empty, "Package-external code must not call historical triggering entrypoints.");
        }

        private static string FindPackagesRoot()
        {
            var candidates = new[]
            {
                Path.Combine("Packages"),
                Path.Combine("Unity", "Packages"),
                Path.Combine("..", "Packages"),
                Path.Combine("..", "Unity", "Packages")
            };

            for (int i = 0; i < candidates.Length; i++)
            {
                var fullPath = Path.GetFullPath(candidates[i]);
                if (Directory.Exists(Path.Combine(fullPath, "com.abilitykit.triggering")))
                    return fullPath;
            }

            Assert.Fail("Unable to locate Unity/Packages root for runtime compatibility caller scan.");
            return null;
        }
    }
}
