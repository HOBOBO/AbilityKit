using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;

namespace AbilityKit.Demo.Moba.CodeGen.Tests;

/// <summary>
/// Entitas 组件护栏 analyzer（AKSG9201 blob 字段 / AKSG9202 非 public 字段）。
/// 白名单与 tools/AbilityKit.Entitas.CodeGen 的可序列化判定同构。
/// </summary>
public sealed class EntitasComponentGuardAnalyzerTests
{
    private const string Shims = """
        namespace Entitas { public interface IComponent { } }
        namespace AbilityKit.Demo.Moba.Components
        {
            public sealed class EntitasBlobAttribute : Attribute { }
        }
        """;

    private static async Task<Diagnostic[]> RunAsync(string source)
    {
        var compilation = CSharpCompilation.Create(
            "EntitasComponentGuardAnalyzerTests",
            new[]
            {
                CSharpSyntaxTree.ParseText(source, path: "Components/TestComponent.cs"),
                CSharpSyntaxTree.ParseText(Shims, path: "Components/Shims.cs"),
            },
            new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) },
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var diagnostics = await compilation.WithAnalyzers(
            ImmutableArray.Create<DiagnosticAnalyzer>(new EntitasComponentGuardAnalyzer()))
            .GetAnalyzerDiagnosticsAsync();
        return diagnostics
            .Where(d => d.Id is "AKSG9201" or "AKSG9202")
            .OrderBy(d => d.Location.SourceSpan.Start)
            .ToArray();
    }

    private static string Comp(string body) =>
        "using Entitas;\nnamespace AbilityKit.Demo.Moba.Components\n{\n    public sealed class TestComponent : IComponent\n    {\n" +
        body + "\n    }\n}\n";

    [Fact]
    public async Task PlainValueFields_Pass()
    {
        var diagnostics = await RunAsync(Comp(
@"        public int Count;
        public long EndMs;
        public float DamageMul;
        public bool Enabled;
        public string Name;
        public int[] BuffIds;
        public GuardVec3 Aim;
        public Stage Stage;

        public enum Stage { Windup, Active }

        public struct GuardVec3 { public float X; public float Y; public float Z; }

        public sealed class Nested { public int A; public GuardVec3 B; public Nested[] Others; }"));

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task BlobField_WithoutMarker_IsFlagged()
    {
        var diagnostics = await RunAsync(Comp(
@"        public BuffRuntime Active;

        public sealed class BuffRuntime { public object Opaque; }"));

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("AKSG9201", diagnostic.Id);
        Assert.Contains("Active", diagnostic.GetMessage());
    }

    [Fact]
    public async Task BlobMarker_OnComponent_Suppresses()
    {
        var diagnostics = await RunAsync(
@"using Entitas;
using AbilityKit.Demo.Moba.Components;
namespace AbilityKit.Demo.Moba.Components
{
    [EntitasBlob]
    public sealed class TestComponent : IComponent
    {
        public BuffRuntime Active;
    }

    public sealed class BuffRuntime { public object Opaque; }
}");

        Assert.Empty(diagnostics.Where(d => d.Id == "AKSG9201"));
    }

    [Fact]
    public async Task BlobMarker_OnFieldType_Suppresses()
    {
        var diagnostics = await RunAsync(
@"using Entitas;
namespace Test
{
    public sealed class TestComponent : Entitas.IComponent
    {
        public Runtime.Opaque Payload;
    }
}
namespace Runtime
{
    [AbilityKit.Demo.Moba.Components.EntitasBlob]
    public sealed class Opaque { public object Anything; }
}");

        Assert.Empty(diagnostics.Where(d => d.Id == "AKSG9201"));
    }

    [Fact]
    public async Task NonPublicField_IsFlagged()
    {
        var diagnostics = await RunAsync(Comp(
@"        public int Count;
        private int hidden;"));

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("AKSG9202", diagnostic.Id);
        Assert.Contains("hidden", diagnostic.GetMessage());
    }

    [Fact]
    public async Task InterfaceField_IsFlagged()
    {
        var diagnostics = await RunAsync(Comp(
@"        public ISolver Solver;

        public interface ISolver { void Solve(); }"));

        Assert.Single(diagnostics.Where(d => d.Id == "AKSG9201"));
    }
}
