using System.Reflection;
using System.Security.Cryptography;
using AbilityKit.Analyzer;
using AbilityKit.Demo.Moba.CodeGen;
using AbilityKit.SourceGenerator;
using Xunit;

namespace AbilityKit.CodeGen.Tests;

public sealed class MobaCodeGenOwnershipTests
{
    [Fact]
    public void MobaCompileTimeTypes_AreOwnedByDedicatedAssembly()
    {
        var mobaAssembly = typeof(MobaPlanActionManifestGenerator).Assembly;
        Assert.Equal("AbilityKit.Demo.Moba.CodeGen", mobaAssembly.GetName().Name);

        var expectedTypes = new[]
        {
            typeof(MobaBattleRouteManifestGenerator),
            typeof(MobaBattleRouteAnalyzer),
            typeof(MobaConfigTableManifestGenerator),
            typeof(MobaConfigTableAnalyzer),
            typeof(MobaBootstrapStageManifestGenerator),
            typeof(MobaBootstrapStageAnalyzer),
            typeof(MobaBTreeNodeManifestGenerator),
            typeof(MobaBTreeNodeAnalyzer),
            typeof(MobaEventMappingManifestGenerator),
            typeof(MobaEventMappingAnalyzer),
            typeof(MobaPayloadFieldIdsGenerator),
            typeof(MobaPayloadFieldIdsAnalyzer),
            typeof(MobaPlanActionManifestGenerator),
            typeof(MobaProjectileEmitterManifestGenerator),
            typeof(MobaProjectileEmitterAnalyzer),
            typeof(MobaSnapshotEmitterManifestGenerator),
            typeof(MobaSnapshotEmitterAnalyzer),
            typeof(MobaTargetQueryFactoryManifestGenerator),
            typeof(MobaTargetQueryFactoryAnalyzer),
            typeof(MobaPlanActionModuleAnalyzer),
            typeof(MobaDiagnosticIds),
            typeof(MobaDiagnosticRules),
        };

        Assert.All(expectedTypes, type => Assert.Same(mobaAssembly, type.Assembly));
    }

    [Fact]
    public void FrameworkRoslynAssemblies_DoNotContainMobaTypes()
    {
        var sourceGeneratorAssembly = typeof(SourceGeneratorAssemblyMarker).Assembly;
        var analyzerAssembly = typeof(ForbiddenNamespaceAnalyzer).Assembly;

        Assert.Equal("AbilityKit.SourceGenerator", sourceGeneratorAssembly.GetName().Name);
        Assert.Equal("AbilityKit.Analyzer.Plugin", analyzerAssembly.GetName().Name);
        Assert.DoesNotContain(sourceGeneratorAssembly.GetTypes(), IsMobaType);
        Assert.DoesNotContain(analyzerAssembly.GetTypes(), IsMobaType);
    }

    [Fact]
    public void PackageRootDlls_MatchCurrentBuildOutputs()
    {
        AssertPackageDllMatches(typeof(SourceGeneratorAssemblyMarker).Assembly,
            "Unity/Packages/com.abilitykit.codegen/AbilityKit.SourceGenerator.dll");
        AssertPackageDllMatches(typeof(ForbiddenNamespaceAnalyzer).Assembly,
            "Unity/Packages/com.abilitykit.analyzer/AbilityKit.Analyzer.Plugin.dll");
        AssertPackageDllMatches(typeof(MobaPlanActionManifestGenerator).Assembly,
            "Unity/Packages/com.abilitykit.demo.moba.codegen/AbilityKit.Demo.Moba.CodeGen.dll");
    }

    [Fact]
    public void MobaGenerators_DoNotPublishDiagnostics()
    {
        var generatorDirectory = Path.Combine(
            FindRepositoryRoot(),
            "Unity",
            "Packages",
            "com.abilitykit.demo.moba.codegen",
            "DotNet~",
            "AbilityKit.Demo.Moba.CodeGen",
            "Generators");

        foreach (var generatorFile in Directory.GetFiles(generatorDirectory, "*.cs"))
        {
            var source = File.ReadAllText(generatorFile);
            Assert.DoesNotContain("ReportDiagnostic(", source, StringComparison.Ordinal);
            Assert.DoesNotContain("new DiagnosticDescriptor(", source, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void MobaGeneratorAnalyzerPairs_UseSharedContracts()
    {
        var projectDirectory = Path.Combine(
            FindRepositoryRoot(),
            "Unity",
            "Packages",
            "com.abilitykit.demo.moba.codegen",
            "DotNet~",
            "AbilityKit.Demo.Moba.CodeGen");
        var pairs = new[]
        {
            new ContractPair("MobaBattleRouteManifestGenerator.cs", "MobaBattleRouteAnalyzer.cs", "MobaBattleRouteContract"),
            new ContractPair("MobaConfigTableManifestGenerator.cs", "MobaConfigTableAnalyzer.cs", "MobaConfigTableContract"),
            new ContractPair("MobaBootstrapStageManifestGenerator.cs", "MobaBootstrapStageAnalyzer.cs", "MobaBootstrapStageContract"),
            new ContractPair("MobaBTreeNodeManifestGenerator.cs", "MobaBTreeNodeAnalyzer.cs", "MobaBTreeNodeContract"),
            new ContractPair("MobaEventMappingManifestGenerator.cs", "MobaEventMappingAnalyzer.cs", "MobaEventMappingContract"),
            new ContractPair("MobaPayloadFieldIdsGenerator.cs", "MobaPayloadFieldIdsAnalyzer.cs", "MobaPayloadFieldIdsContract"),
            new ContractPair("MobaPlanActionManifestGenerator.cs", "MobaPlanActionModuleAnalyzer.cs", "MobaPlanActionContract"),
            new ContractPair("MobaProjectileEmitterManifestGenerator.cs", "MobaProjectileEmitterAnalyzer.cs", "MobaProjectileEmitterContract"),
            new ContractPair("MobaSnapshotEmitterManifestGenerator.cs", "MobaSnapshotEmitterAnalyzer.cs", "MobaSnapshotEmitterContract"),
            new ContractPair("MobaTargetQueryFactoryManifestGenerator.cs", "MobaTargetQueryFactoryAnalyzer.cs", "MobaTargetQueryFactoryContract"),
        };

        foreach (var pair in pairs)
        {
            var generatorSource = File.ReadAllText(Path.Combine(projectDirectory, "Generators", pair.GeneratorFile));
            var analyzerSource = File.ReadAllText(Path.Combine(projectDirectory, "Analyzers", pair.AnalyzerFile));
            Assert.Contains(pair.ContractName, generatorSource, StringComparison.Ordinal);
            Assert.Contains(pair.ContractName, analyzerSource, StringComparison.Ordinal);
        }
    }

    private static bool IsMobaType(Type type)
    {
        return type.FullName?.Contains("Moba", StringComparison.OrdinalIgnoreCase) == true;
    }

    private static void AssertPackageDllMatches(Assembly assembly, string packageDllRelativePath)
    {
        var packageDllPath = Path.Combine(FindRepositoryRoot(),
            packageDllRelativePath.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(packageDllPath), $"Package DLL was not found: {packageDllPath}");

        var buildHash = SHA256.HashData(File.ReadAllBytes(assembly.Location));
        var packageHash = SHA256.HashData(File.ReadAllBytes(packageDllPath));
        Assert.Equal(buildHash, packageHash);
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory != null;
             directory = directory.Parent)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "Unity")) &&
                Directory.Exists(Path.Combine(directory.FullName, "src")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Could not locate the AbilityKit repository root.");
    }

    private sealed class ContractPair
    {
        public ContractPair(string generatorFile, string analyzerFile, string contractName)
        {
            GeneratorFile = generatorFile;
            AnalyzerFile = analyzerFile;
            ContractName = contractName;
        }

        public string GeneratorFile { get; }
        public string AnalyzerFile { get; }
        public string ContractName { get; }
    }
}
