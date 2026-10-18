using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;

namespace AbilityKit.Demo.Moba.CodeGen.Tests;

public sealed class MobaPortableSourceAnalyzerTests
{
    private const string PackageRoot =
        "C:/repo/Unity/Packages/com.abilitykit.demo.moba.view.runtime/Runtime/";

    [Theory]
    [InlineData("Logic/Flow/Flow.cs", "using UnityEngine; class Flow { Application value; }", 1)]
    [InlineData("Networking/Room/Store.cs", "class Store { object value = UnityEngine.Application.persistentDataPath; }", 1)]
    [InlineData("Logic/Flow/Alias.cs", "using Engine = Godot; class Flow { Engine.Node value; }", 1)]
    [InlineData("Logic/Flow/Flow.cs", "// UnityEngine.Application\nclass Flow { string value = \"UnityEngine\"; }", 0)]
    [InlineData("Platform/Unity/Assets/Store.cs", "class Store { object value = UnityEngine.Application.persistentDataPath; }", 0)]
    public async Task PortableDirectoriesRejectEngineNamespaces(
        string path, string source, int expectedCount)
    {
        const string engineTypes = "namespace UnityEngine { public class Application { public static string persistentDataPath; } } namespace Godot { public class Node { } }";
        var compilation = CSharpCompilation.Create(
            "PortableSourceAnalyzerTests",
            new[]
            {
                CSharpSyntaxTree.ParseText(source, path: PackageRoot + path),
                CSharpSyntaxTree.ParseText(engineTypes, path: PackageRoot + "Platform/Test/EngineTypes.cs")
            },
            new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) },
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var diagnostics = await compilation.WithAnalyzers(
            ImmutableArray.Create<DiagnosticAnalyzer>(new MobaPortableSourceAnalyzer()))
            .GetAnalyzerDiagnosticsAsync();

        Assert.Equal(expectedCount, diagnostics.Count(d =>
            d.Id == MobaDiagnosticIds.PortableSourceEngineDependencyRuleId));
    }
}
