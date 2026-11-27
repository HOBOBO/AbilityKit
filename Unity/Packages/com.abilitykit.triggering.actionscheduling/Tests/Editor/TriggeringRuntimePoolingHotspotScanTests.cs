using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace AbilityKit.Triggering.ActionScheduling.Tests
{
    public sealed class TriggeringRuntimePoolingHotspotScanTests
    {
        private static readonly Regex PooledRuntimeAllocationPattern = new Regex(
            @"new\s+(ActionInstance|DefaultActionExecutor|QueuedActionExecutor|RetryActionExecutor)\s*\(",
            RegexOptions.Compiled);

        [Test]
        public void RuntimePooledHotspots_OnlyAllocateInPoolsOrFallbackPaths()
        {
            var packageRoot = FindPackageRoot();
            var runtimeRoot = Path.Combine(packageRoot, "Runtime");
            var allowed = new HashSet<string>(StringComparer.Ordinal)
            {
                Normalize(Path.Combine(runtimeRoot, "ActionSchedulerPools.cs")),
                Normalize(Path.Combine(runtimeRoot, "ActionSchedulerFactory.cs"))
            };
            var violations = new List<string>();

            foreach (var file in Directory.EnumerateFiles(runtimeRoot, "*.cs", SearchOption.AllDirectories))
            {
                var normalized = Normalize(file);
                var text = File.ReadAllText(file);
                var matches = PooledRuntimeAllocationPattern.Matches(text);
                if (matches.Count == 0)
                    continue;

                if (!allowed.Remove(normalized))
                {
                    violations.Add(normalized);
                    continue;
                }

                if (normalized.EndsWith("/ActionSchedulerFactory.cs", StringComparison.Ordinal))
                {
                    AssertFallbackPath(text, "pools != null", "ActionSchedulerFactory.cs");
                }
                else if (normalized.EndsWith("/ActionSchedulerPools.cs", StringComparison.Ordinal))
                {
                    StringAssert.Contains("Scope.GetPool", text);
                }
            }

            Assert.That(violations, Is.Empty, "Pooled scheduler allocations must stay behind ActionSchedulerPools or explicit no-pool fallback paths.");
            Assert.That(allowed, Is.Empty, "The scan must visit both expected allocation sites in the source package.");
        }

        private static void AssertFallbackPath(string text, string guard, string fileName)
        {
            StringAssert.Contains(guard, text, fileName + " must keep pooled and no-pool paths explicit.");
        }

        private static string Normalize(string path)
        {
            return Path.GetFullPath(path).Replace('\\', '/');
        }

        private static string FindPackageRoot()
        {
            const string packageName = "com.abilitykit.triggering.actionscheduling";
            var candidates = new[]
            {
                Path.Combine("Packages", packageName),
                Path.Combine("Unity", "Packages", packageName),
                Path.Combine("..", "Packages", packageName),
                Path.Combine("..", "Unity", "Packages", packageName)
            };

            for (int i = 0; i < candidates.Length; i++)
            {
                var fullPath = Path.GetFullPath(candidates[i]);
                if (Directory.Exists(fullPath))
                    return fullPath;
            }

            Assert.Fail("Unable to locate " + packageName + " source package root.");
            return null;
        }
    }
}
