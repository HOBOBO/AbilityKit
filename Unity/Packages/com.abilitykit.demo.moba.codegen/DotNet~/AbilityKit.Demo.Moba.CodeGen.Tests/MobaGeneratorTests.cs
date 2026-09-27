using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace AbilityKit.Demo.Moba.CodeGen.Tests;

public sealed class MobaGeneratorTests
{
    private static readonly CSharpParseOptions ParseOptions = new CSharpParseOptions(LanguageVersion.CSharp9);
    private static readonly MetadataReference[] References = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
        .Split(Path.PathSeparator)
        .Select(path => MetadataReference.CreateFromFile(path))
        .ToArray();

    [Fact]
    public void WorldManifest_IsDeterministicAndSorted()
    {
        const string source = """
            using System;
            namespace AbilityKit.Ability.World.DI
            {
                public enum WorldLifetime { Scoped, Singleton, Transient }
                public interface IWorldResolver { object Resolve(Type type); }
                public sealed class WorldContainerBuilder
                {
                    public void TryRegisterType(Type service, Type implementation, WorldLifetime lifetime) { }
                    public void TryRegister(Type service, Type implementation, WorldLifetime lifetime, Func<IWorldResolver, object> factory) { }
                }
            }
            namespace AbilityKit.Ability.World.Services.Attributes
            {
                [Flags] public enum WorldServiceProfile { All = -1 }
                [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
                public sealed class WorldServiceAttribute : Attribute
                {
                    public WorldServiceAttribute(Type type, AbilityKit.Ability.World.DI.WorldLifetime lifetime = 0, bool isDefault = true, WorldServiceProfile profile = WorldServiceProfile.All) { }
                }
            }
            namespace AbilityKit.Ability.World
            {
                public enum WorldSystemPhase { PreExecute, Execute, PostExecute }
                [AttributeUsage(AttributeTargets.Class)] public sealed class WorldSystemAttribute : Attribute
                {
                    public WorldSystemAttribute(int order = 0) { }
                    public WorldSystemPhase Phase { get; set; }
                }
            }
            namespace Entitas { public interface ISystem { } public interface IContexts { } }
            namespace AbilityKit.Demo.Moba.Systems
            {
                internal static partial class MobaGeneratedWorldServiceManifest { }
                internal static partial class MobaGeneratedWorldSystemManifest { }
                internal readonly struct MobaGeneratedWorldSystemDescriptor { }
            }
            namespace AbilityKit.Demo.Moba.Services
            {
                public interface IService { }
                [AbilityKit.Ability.World.Services.Attributes.WorldService(typeof(IService))] public sealed class ZService : IService { }
                [AbilityKit.Ability.World.Services.Attributes.WorldService(typeof(IService), isDefault: false)] public sealed class AService : IService { }
            }
            """;

        var first = Run(new MobaWorldManifestGenerator(), source);
        var second = Run(new MobaWorldManifestGenerator(), source);
        Assert.Equal(first.GeneratedText, second.GeneratedText);
        Assert.True(first.GeneratedText.IndexOf("AService", StringComparison.Ordinal) < first.GeneratedText.IndexOf("ZService", StringComparison.Ordinal));
    }

    [Fact]
    public void RollbackManifest_ReportsDuplicateKeys()
    {
        const string source = """
            using System;
            using System.Collections.Generic;
            namespace AbilityKit.Ability.FrameSync.Rollback
            {
                public interface IRollbackStateProvider { }
                public sealed class RollbackRegistry { }
            }
            namespace AbilityKit.Ability.World.DI { public interface IWorldResolver { } }
            namespace AbilityKit.Demo.Moba.Rollback
            {
                [AttributeUsage(AttributeTargets.Class)] public sealed class MobaRollbackProviderAttribute : Attribute
                { public MobaRollbackProviderAttribute(int key, bool autoResolve = false) { } }
                public readonly struct MobaGeneratedRollbackProviderDescriptor { }
                internal static partial class MobaGeneratedRollbackProviderManifest
                {
                    static partial void AddGeneratedDescriptors(List<MobaGeneratedRollbackProviderDescriptor> descriptors);
                    static partial void AddGeneratedResolved(AbilityKit.Ability.World.DI.IWorldResolver services, AbilityKit.Ability.FrameSync.Rollback.RollbackRegistry registry);
                }
                [MobaRollbackProvider(10)] public sealed class First : AbilityKit.Ability.FrameSync.Rollback.IRollbackStateProvider { }
                [MobaRollbackProvider(10)] public sealed class Second : AbilityKit.Ability.FrameSync.Rollback.IRollbackStateProvider { }
            }
            """;

        var result = Run(new MobaRegistrationManifestGenerator(), source);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == MobaDiagnosticIds.DuplicateRegistrationManifestKeyRuleId);
    }

    [Fact]
    public void RegistrationManifest_DoesNotGenerateForReferencedRuntimeAssembly()
    {
        const string runtimeSource = """
            using System;
            namespace AbilityKit.Demo.Moba.Services.EntityConstruction
            {
                public enum MobaEntityKind { Unknown, Hero }
                public sealed class MobaActorArchetypeRegistry { }
                [AttributeUsage(AttributeTargets.Method)]
                public sealed class MobaActorArchetypeAttribute : Attribute
                { public MobaActorArchetypeAttribute(MobaEntityKind kind) { } }
                public static partial class MobaActorArchetypeAssembler
                {
                    static partial void AddGenerated(MobaActorArchetypeRegistry registry);
                }
            }
            """;
        var runtimeReference = CompileReference("ReferencedMobaRuntime", runtimeSource);

        var result = RunWithAdditionalReferences(
            new MobaRegistrationManifestGenerator(),
            new[] { runtimeReference },
            "public sealed class Consumer { }");

        Assert.Empty(result.GeneratedText);
        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public void PlanActionSchema_GeneratesParsingAndValidation()
    {
        const string source = """
            using System;
            namespace AbilityKit.Demo.Moba.Services.Triggering.PlanActions
            {
                public enum MobaPlanActionArgKind { Int, Float, Bool, BoolNonZero, Enum }
                [AttributeUsage(AttributeTargets.Struct)] public sealed class GenerateMobaPlanActionSchemaAttribute : Attribute
                { public GenerateMobaPlanActionSchemaAttribute(string actionName) { } }
                [AttributeUsage(AttributeTargets.Parameter)] public sealed class MobaPlanActionArgAttribute : Attribute
                {
                    public MobaPlanActionArgAttribute(MobaPlanActionArgKind kind, double defaultValue, bool required, params string[] aliases) { }
                    public string DisplayName { get; set; }
                    public double Min { get; set; } = double.NaN;
                    public double Max { get; set; } = double.NaN;
                    public bool ValidateEnum { get; set; } = true;
                }
                public abstract class MobaPlanActionSchemaBase<T> { }
                [GenerateMobaPlanActionSchema("sample")]
                public readonly struct SampleArgs
                {
                    public SampleArgs([MobaPlanActionArg(MobaPlanActionArgKind.Int, 0d, true, "value", Min = 1d)] int value) { }
                }
                public sealed partial class SampleSchema { }
            }
            """;

        var result = Run(new MobaPlanActionSchemaGenerator(), source);
        Assert.Contains("ReadInt(namedArgs, ctx, 0, \"value\")", result.GeneratedText, StringComparison.Ordinal);
        Assert.Contains("RequireNumericValue", result.GeneratedText, StringComparison.Ordinal);
        Assert.Contains("ValidateNumericRange", result.GeneratedText, StringComparison.Ordinal);
    }

    [Fact]
    public void PayloadAccessor_GeneratesLegacyAndConversionPaths()
    {
        const string source = """
            using System;
            namespace AbilityKit.Demo.Moba
            {
                public enum PayloadAccessorValueKind { Int, Double, Enum, Bool, Fixed64, ClampMinOneInt }
                [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)] public sealed class GeneratePayloadFieldIdsAttribute : Attribute
                { public GeneratePayloadFieldIdsAttribute(Type catalog, string method, bool legacy, params string[] fields) { } }
                [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)] public sealed class GeneratePayloadAccessorAttribute : Attribute
                { public GeneratePayloadAccessorAttribute(Type payload, Type catalog, string field, string member, PayloadAccessorValueKind kind, bool legacy = false) { } }
                public static class Fields
                {
                    public const string Level = "level";
                    public static int FieldId(string value) => 1;
                    public static int LegacyFieldId(string value) => 2;
                }
                public sealed class Payload { public int Level { get; set; } }
                [GeneratePayloadFieldIds(typeof(Fields), "SupportsField", true, nameof(Fields.Level))]
                [GeneratePayloadAccessor(typeof(Payload), typeof(Fields), nameof(Fields.Level), nameof(Payload.Level), PayloadAccessorValueKind.ClampMinOneInt, true)]
                public sealed partial class Accessor { }
            }
            """;

        var result = Run(new MobaPayloadFieldIdsGenerator(), source);
        Assert.Contains("fieldId == LevelId || fieldId == LevelLegacyId", result.GeneratedText, StringComparison.Ordinal);
        Assert.Contains("global::System.Math.Max(1, args.Level)", result.GeneratedText, StringComparison.Ordinal);
    }

    private static GeneratorResult Run(ISourceGenerator generator, params string[] sources)
    {
        return RunWithAdditionalReferences(generator, Array.Empty<MetadataReference>(), sources);
    }

    private static GeneratorResult RunWithAdditionalReferences(
        ISourceGenerator generator,
        IReadOnlyList<MetadataReference> additionalReferences,
        params string[] sources)
    {
        var trees = sources.Select(source => CSharpSyntaxTree.ParseText(source, ParseOptions)).ToArray();
        var compilation = CSharpCompilation.Create(
            "GeneratorTests",
            trees,
            References.Concat(additionalReferences),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new[] { generator }, parseOptions: ParseOptions);
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out var diagnostics);
        var runResult = driver.GetRunResult();
        var generated = string.Join("\n", runResult.Results.SelectMany(result => result.GeneratedSources).Select(source => source.SourceText.ToString()));
        return new GeneratorResult(generated, diagnostics.Concat(runResult.Diagnostics).ToArray());
    }

    private static MetadataReference CompileReference(string assemblyName, string source)
    {
        var compilation = CSharpCompilation.Create(
            assemblyName,
            new[] { CSharpSyntaxTree.ParseText(source, ParseOptions) },
            References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        using var stream = new MemoryStream();
        var result = compilation.Emit(stream);
        if (!result.Success)
            throw new InvalidOperationException(string.Join(Environment.NewLine, result.Diagnostics));
        return MetadataReference.CreateFromImage(stream.ToArray());
    }

    private sealed record GeneratorResult(string GeneratedText, IReadOnlyList<Diagnostic> Diagnostics);
}
