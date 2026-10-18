using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;

namespace AbilityKit.Demo.Moba.Presentation.Core.Tests;

public sealed class PureAssemblyBoundaryTests
{
    [Fact]
    public void BattleTransportDoesNotDependOnUnityResources()
    {
        var directory = Path.Combine(FindRepositoryRoot(), "Unity", "Packages",
            "com.abilitykit.demo.moba.view.runtime", "Runtime", "Networking", "Transport");

        foreach (var source in Directory.GetFiles(directory, "*.cs"))
        {
            var content = File.ReadAllText(source);
            Assert.DoesNotContain("UnityEngine", content);
            Assert.DoesNotContain("ResourcesAssetProvider", content);
        }
    }

    [Fact]
    public void PureRuntimeAssembliesExcludeUnityAndMobaGatewayDependencies()
    {
        var root = FindRepositoryRoot();
        var packages = Path.Combine(root, "Unity", "Packages");
        var view = Path.Combine(packages, "com.abilitykit.demo.moba.view.runtime", "Runtime");
        var portable = Path.Combine(view, "Logic");
        var flow = Path.Combine(portable, "Flow");
        var directories = new[]
        {
            Path.Combine(portable, "ClientSession"),
            Path.Combine(portable, "Presentation", "Core"),
            Path.Combine(portable, "Presentation", "Adapter"),
            Path.Combine(portable, "Presentation", "Timeline"),
            Path.Combine(portable, "Presentation", "Gameplay"),
            Path.Combine(portable, "Presentation", "Vfx"),
            Path.Combine(portable, "Contracts", "Time"),
            Path.Combine(portable, "Contracts", "Logging"),
            flow,
            Path.Combine(packages, "com.abilitykit.network.runtime", "Runtime")
        };

        foreach (var directory in directories)
        {
            var asmdef = Assert.Single(Directory.GetFiles(directory, "*.asmdef"));
            using var document = JsonDocument.Parse(File.ReadAllText(asmdef));
            var definition = document.RootElement;
            Assert.True(definition.GetProperty("noEngineReferences").GetBoolean(), asmdef);
            foreach (var reference in definition.GetProperty("references").EnumerateArray())
            {
                var name = reference.GetString() ?? string.Empty;
                Assert.DoesNotContain("Moba.View.Runtime", name);
                if (directory != flow)
                    Assert.DoesNotContain("Protocol.Moba", name);
            }

            foreach (var source in Directory.GetFiles(directory, "*.cs", SearchOption.AllDirectories))
            {
                var text = File.ReadAllText(source);
                Assert.False(Regex.IsMatch(text, @"^\s*using\s+(?:static\s+)?UnityEngine(?:\.|;)", RegexOptions.Multiline), source);
                if (directory != flow)
                    Assert.False(Regex.IsMatch(text, @"^\s*using\s+AbilityKit\.Protocol\.Moba(?:\.|;)", RegexOptions.Multiline), source);
            }
        }
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "Unity", "Packages")))
                return directory.FullName;
        }

        throw new DirectoryNotFoundException("AbilityKit repository root was not found.");
    }
}
