using System.Text.RegularExpressions;
using Xunit;

namespace AbilityKit.Network.Sdk.Tests;

public sealed class SampleNetworkArchitectureTests
{
    private static readonly Regex PrivatePushHandlerMap = new(
        @"Dictionary\s*<\s*uint\s*,\s*(?:Action|Func|List\s*<\s*Action|HashSet\s*<\s*Action)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    [Fact]
    public void DemoRuntimeSourcesDelegateRequestLifecycleAndPushRoutingToFrameworkOwners()
    {
        var repositoryRoot = FindRepositoryRoot(AppContext.BaseDirectory);
        var packagesRoot = Path.Combine(repositoryRoot, "Unity", "Packages");
        var demoPackages = Directory.EnumerateDirectories(
            packagesRoot,
            "com.abilitykit.demo.*",
            SearchOption.TopDirectoryOnly);

        var violations = new List<string>();
        foreach (var package in demoPackages)
        {
            var runtimeRoot = Path.Combine(package, "Runtime");
            if (!Directory.Exists(runtimeRoot))
            {
                continue;
            }

            foreach (var file in Directory.EnumerateFiles(runtimeRoot, "*.cs", SearchOption.AllDirectories))
            {
                if (IsTestSource(runtimeRoot, file))
                {
                    continue;
                }

                var source = File.ReadAllText(file);
                if (source.Contains("new RequestClient(", StringComparison.Ordinal))
                {
                    violations.Add(Relative(repositoryRoot, file) + ": directly constructs RequestClient");
                }

                if (PrivatePushHandlerMap.IsMatch(source))
                {
                    violations.Add(Relative(repositoryRoot, file) + ": owns an opcode-to-push-handler dictionary");
                }
            }
        }

        Assert.True(
            violations.Count == 0,
            "Demo runtime network wrappers must use NetworkSdkClient/framework routers.\n" +
            string.Join("\n", violations));
    }

    private static bool IsTestSource(string runtimeRoot, string file)
    {
        var relative = Path.GetRelativePath(runtimeRoot, file);
        var segments = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return segments.Any(segment =>
            string.Equals(segment, "Test", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(segment, "Tests", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(segment, "UnitTest", StringComparison.OrdinalIgnoreCase));
    }

    private static string Relative(string repositoryRoot, string file) =>
        Path.GetRelativePath(repositoryRoot, file).Replace('\\', '/');

    private static string FindRepositoryRoot(string startDirectory)
    {
        var directory = new DirectoryInfo(startDirectory);
        while (directory != null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, ".git")) &&
                Directory.Exists(Path.Combine(directory.FullName, "Unity")) &&
                Directory.Exists(Path.Combine(directory.FullName, "src")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException(
            $"Could not locate repository root from {startDirectory}.");
    }
}
