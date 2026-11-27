using System;
using System.Collections.Generic;
using System.IO;
using AbilityKit.Triggering.Runtime.Compatibility;
using AbilityKit.Triggering.Validation;
using NUnit.Framework;

namespace AbilityKit.Triggering.Tests
{
    public sealed class RuntimeCompatibilityCatalogTests
    {
        [Test]
        public void Entries_ReusesFormalRootRuntimeCompatibilityCatalog()
        {
            Assert.That(RuntimeCompatibilityCatalog.Entries, Is.SameAs(RootRuntimeCompatibilityCatalog.Entries));
            Assert.That(RuntimeCompatibilityCatalog.DefaultRemovalGate, Is.EqualTo(RootRuntimeCompatibilityCatalog.DefaultRemovalGate));
        }

        [Test]
        public void Entries_AreEmptyAfterRootPlaceholdersRemoved()
        {
            Assert.That(RuntimeCompatibilityCatalog.Entries, Is.Empty);
        }

        [Test]
        public void TryGetEntry_ReturnsFalseForRemovedRootCompatibilityEntry()
        {
            var found = RuntimeCompatibilityCatalog.TryGetEntry("TriggerRunner.cs", out var entry);

            Assert.That(found, Is.False);
            Assert.That(entry, Is.EqualTo(default(CompatibilityEntry)));
        }

        [Test]
        public void HumanReadableCompatibilityDocument_DeclaresNoCurrentRootEntries()
        {
            var path = Path.Combine("Packages", "com.abilitykit.triggering", "Runtime", "Compatibility.md");
            Assert.That(File.Exists(path), Is.True, "Missing human-readable runtime compatibility document.");

            var document = File.ReadAllText(path);
            Assert.That(document, Does.Contain("当前 Runtime 根目录已不再保留 .cs 兼容占位入口"));
        }

        [Test]
        public void CompatibilityReadme_DeclaresCatalogDocumentAndTestSyncRule()
        {
            var path = Path.Combine("Packages", "com.abilitykit.triggering", "Runtime", "Compatibility", "README.md");
            Assert.That(File.Exists(path), Is.True, "Missing runtime compatibility README.");

            var document = File.ReadAllText(path);
            var catalogPath = Path.Combine(Path.GetDirectoryName(path), "RootRuntimeCompatibilityCatalog.cs");
            Assert.That(File.Exists(catalogPath), Is.True, "The compatibility overview must be next to the root catalog source.");
            Assert.That(document, Does.Contain("RootRuntimeCompatibilityCatalog.cs"));
            Assert.That(document, Does.Contain("Runtime/Compatibility.md"));
            Assert.That(document, Does.Contain("相关测试"));
        }

        [Test]
        public void ScanRootEntries_ReturnsValidWhenNoRootEntriesRemain()
        {
            var result = RuntimeCompatibilityCatalog.ScanRootEntries(new List<string>());

            Assert.That(result.IsValid, Is.True);
            Assert.That(result.Issues, Is.Empty);
        }

        [Test]
        public void ScanRootEntries_ReportsMissingCatalogEntry()
        {
            var result = RuntimeCompatibilityCatalog.ScanRootEntries(new[] { "NewRootCompatibilityEntry.cs" });

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Issues, Has.Some.Matches<RuntimeCompatibilityScanIssue>(issue =>
                issue.Code == ValidationErrorCodes.RUNTIME_COMPATIBILITY_ENTRY_MISSING &&
                issue.EntryPath == "NewRootCompatibilityEntry.cs"));
        }

        [Test]
        public void ScanRootEntries_ConvertsIssuesToValidationWarnings()
        {
            var result = RuntimeCompatibilityCatalog.ScanRootEntries(new[] { "UnknownRootEntry.cs" });

            var validation = result.ToValidationResult();

            Assert.That(validation.IsValid, Is.True);
            Assert.That(validation.Warnings, Has.Some.Matches<ValidationIssue>(issue =>
                issue.Code == ValidationErrorCodes.RUNTIME_COMPATIBILITY_ENTRY_MISSING));
        }

        [Test]
        public void PackageExternalSources_DoNotCallHistoricalTriggeringEntries()
        {
            var packagesRoot = FindPackagesRoot();
            var forbidden = new[]
            {
                "AbilityKit.Triggering.Runtime.Scheduler",
                "AbilityKit.Triggering.Runtime.Executable",
                "TriggerDispatcherHub",
                "TriggerDispatcherRegistry",
                "EventBusDispatcher",
                "TimedDispatcher",
                "IEntityFinder"
            };
            var violations = new List<string>();

            foreach (var file in Directory.EnumerateFiles(packagesRoot, "*.cs", SearchOption.AllDirectories))
            {
                var normalized = file.Replace('\\', '/');
                if (normalized.Contains("/com.abilitykit.triggering/", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (normalized.Contains("/Documentation~/", StringComparison.OrdinalIgnoreCase))
                    continue;

                var text = File.ReadAllText(file);
                for (int i = 0; i < forbidden.Length; i++)
                {
                    if (text.Contains(forbidden[i], StringComparison.Ordinal))
                    {
                        violations.Add($"{normalized}: {forbidden[i]}");
                    }
                }
            }

            Assert.That(violations, Is.Empty, "Package-external code must use TriggerRunner, TriggerPlan, the supported action or execution-tree scheduler, and generic ExecCtx numeric resolution instead of historical compatibility entries.");
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
