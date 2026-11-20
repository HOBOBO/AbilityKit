using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using AbilityKit.Entitas.CodeGen.Configuration;
using AbilityKit.Entitas.CodeGen.Diagnostics;
using AbilityKit.Entitas.CodeGen.Runner;
using Xunit;

namespace AbilityKit.Entitas.CodeGen.Tests;

public sealed class ManagedDataShapeTests
{
    public static IEnumerable<object[]> UnsupportedShapes()
    {
        var declarations = new[]
        {
            "public class Data\n{\npublic int Value;\n}",
            "public sealed class Data\n{\nprivate int Hidden;\npublic int Value;\n}",
            "public sealed class Data\n{\npublic int Value;\npublic void Tick() {}\n}",
            "public sealed class Data\n{\npublic int Value;\npublic Data() {}\n}",
            "public sealed class Data : object\n{\npublic int Value;\n}",
            "public sealed partial class Data\n{\npublic int Value;\n}",
            "public sealed class Data\n{\npublic Data Next;\n}",
            "public sealed class Data\n{\npublic Dictionary<string, int> Map;\n}",
            "public sealed class Data\n{\npublic Dictionary<long, int> Map;\n}",
            "public sealed class Data\n{\npublic Dictionary<int, object> Map;\n}",
            "public sealed class Data\n{\npublic int Value = 10;\n}",
            "public sealed class Data\n{\npublic int Left, Right;\n}",
            "public sealed class Data\n{\npublic int Value { get; set; }\n}",
            "public sealed class Data\n{\npublic readonly int Value;\n}",
        };
        foreach (var scanner in new[] { "regex", "roslyn" })
            foreach (var declaration in declarations) yield return new object[] { scanner, declaration };
    }

    [Theory]
    [MemberData(nameof(UnsupportedShapes))]
    public void Hidden_behavior_unsupported_keys_and_cycles_fail_closed(string scanner, string declaration)
    {
        using var sandbox = Create(scanner, declaration);
        var exit = sandbox.Run(out var sink);
        Assert.Equal(ExitCodes.SourceValidation, exit);
        Assert.Contains(sink.Lines, line => line.Contains("不可序列化字段"));
    }

    [Theory]
    [InlineData("regex")]
    [InlineData("roslyn")]
    public void Explicit_pure_data_dictionary_and_nullable_array_generate_in_both_scanners(string scanner)
    {
        using var sandbox = Create(scanner, """
            public enum Key { None, First, Second }
            public sealed class Item
            {
                public long Raw;
            }
            public sealed class Data
            {
                public Dictionary<Key, Item> Map;
                public Item[] Items;
                public Dictionary<int, int> Counts;
            }
            """);
        CodeGenRunner.Run(new RunOptions(ConfigPath: sandbox.ConfigPath, Write: true,
            OutDir: sandbox.Abs("generated"), ForceGeneratedDir: true), sandbox.Sink);
        Assert.Equal(ExitCodes.Ok, sandbox.Run(out _));
        var generated = File.ReadAllText(sandbox.Abs("generated/Actor/ActorComponentSerialization.cs"));
        Assert.Contains("keysValueMap.Sort();", generated);
        Assert.Contains("Duplicate dictionary key.", generated);
        Assert.Contains("Unknown enum value.", generated);
        Assert.Contains("ReadPresence(r) ? new Sandbox.Components.Item() : null", generated);
        CompileAndExercise(sandbox, generated);
    }

    private static void CompileAndExercise(TempSandbox sandbox, string generated)
    {
        var declarations = File.ReadAllText(sandbox.Abs("components/DataComponent.cs"))
            .Replace("using Entitas;", "").Replace("[Actor]", "").Replace("[EntitasBlob]", "")
            .Replace(" : IComponent", "");
        const string harness = """
            using System;
            using System.IO;
            using System.Collections.Generic;
            using Sandbox.Components;
            public sealed class BaselineComponent { public int Value; }
            public sealed class ActorEntity
            {
                public bool hasBaseline, hasData;
                public BaselineComponent baseline = new BaselineComponent();
                public DataComponent data = new DataComponent();
                public void ReplaceBaseline(int value) { hasBaseline = true; baseline.Value = value; }
                public void RemoveBaseline() { hasBaseline = false; }
                public void ReplaceData(Data value) { hasData = true; data.Value = value; }
                public void RemoveData() { hasData = false; }
            }
            public static class CodecAcceptance
            {
                private static byte[] Write(ActorEntity entity)
                {
                    using var stream = new MemoryStream();
                    using var writer = new BinaryWriter(stream);
                    ActorComponentSerialization.Write(entity, writer);
                    return stream.ToArray();
                }
                public static void Run()
                {
                    var source = new ActorEntity();
                    source.ReplaceData(new Data {
                        Map = new Dictionary<Key, Item> { [Key.Second] = null, [Key.First] = new Item { Raw = long.MinValue } },
                        Items = new Item[] { null, new Item { Raw = 0 }, new Item { Raw = long.MaxValue } },
                        Counts = new Dictionary<int, int> { [int.MaxValue] = 0, [int.MinValue] = -1 }
                    });
                    var bytes = Write(source);
                    source.data.Value.Counts = new Dictionary<int, int> { [int.MinValue] = -1, [int.MaxValue] = 0 };
                    if (!System.Linq.Enumerable.SequenceEqual(bytes, Write(source))) throw new Exception("Noncanonical dictionary");
                    var target = new ActorEntity();
                    using (var reader = new BinaryReader(new MemoryStream(bytes))) ActorComponentSerialization.Restore(target, reader);
                    var value = target.data.Value;
                    if (!target.hasData || value.Map[Key.Second] != null || value.Map[Key.First].Raw != long.MinValue ||
                        value.Items.Length != 3 || value.Items[0] != null || value.Items[1].Raw != 0 || value.Items[2].Raw != long.MaxValue ||
                        value.Counts[int.MinValue] != -1 || value.Counts[int.MaxValue] != 0 ||
                        ReferenceEquals(source.data.Value.Items, value.Items) || ReferenceEquals(source.data.Value.Map, value.Map))
                        throw new Exception("Incorrect deep restoration");
                    source.data.Value.Items = null;
                    var nullBytes = Write(source);
                    source.data.Value.Items = Array.Empty<Item>();
                    if (System.Linq.Enumerable.SequenceEqual(nullBytes, Write(source))) throw new Exception("Null/empty conflated");
                    source.data.Value.Counts = new Dictionary<int, int>(new CustomComparer());
                    try { Write(source); } catch (InvalidDataException) { return; }
                    throw new Exception("Custom comparer accepted");
                }
                private sealed class CustomComparer : IEqualityComparer<int>
                {
                    public bool Equals(int x, int y) => x == y;
                    public int GetHashCode(int value) => value;
                }
            }
            """;
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Select(path => MetadataReference.CreateFromFile(path));
        var compilation = CSharpCompilation.Create("ManagedCodec_" + Guid.NewGuid().ToString("N"),
            new[] { CSharpSyntaxTree.ParseText(declarations), CSharpSyntaxTree.ParseText(generated), CSharpSyntaxTree.ParseText(harness) },
            references, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        using var assembly = new MemoryStream();
        var emitted = compilation.Emit(assembly);
        Assert.True(emitted.Success, string.Join(Environment.NewLine, emitted.Diagnostics));
        System.Reflection.Assembly.Load(assembly.ToArray()).GetType("CodecAcceptance")!.GetMethod("Run")!.Invoke(null, null);
    }

    [Theory]
    [InlineData("regex")]
    [InlineData("roslyn")]
    public void Dictionary_without_explicit_managed_marker_is_rejected(string scanner)
    {
        using var sandbox = Create(scanner,
            "public sealed class Data\n{\npublic Dictionary<int, int> Map;\n}", managed: false);
        Assert.Equal(ExitCodes.SourceValidation, sandbox.Run(out var sink));
        Assert.Contains(sink.Lines, line => line.Contains("EntitasBlob"));
    }

    private static TempSandbox Create(string scanner, string declaration, bool managed = true)
    {
        var sandbox = new TempSandbox(config =>
        {
            config["scanner"] = scanner;
            config["typeIndex"] = new Dictionary<string, object> { ["roots"] = new[] { "components" } };
        });
        sandbox.WriteFile("DataComponent.cs", "using Entitas;\nusing System.Collections.Generic;\nnamespace Sandbox.Components\n{\n" +
            declaration + "\n[Actor]\n" + (managed ? "[EntitasBlob]\n" : "") +
            "public sealed class DataComponent : IComponent\n{\npublic Data Value;\n}\n}");
        var pin = JsonSerializer.Deserialize<PinFile>(File.ReadAllText(sandbox.PinPath), TempSandbox.Json)!;
        pin.Contexts["Actor"].Components.Add(new PinFile.PinComponent { Name = "Data", Serialize = true });
        File.WriteAllText(sandbox.PinPath, JsonSerializer.Serialize(pin, TempSandbox.Json));
        return sandbox;
    }
}
