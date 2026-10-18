using System.Text.Json;
using AbilityKit.Entitas.CodeGen;
using AbilityKit.Entitas.CodeGen.Configuration;
using AbilityKit.Entitas.CodeGen.Model;
using AbilityKit.Entitas.CodeGen.Runner;
using AbilityKit.Entitas.CodeGen.Scanning;
using AbilityKit.Entitas.CodeGen.Templating;
using Xunit;

namespace AbilityKit.Entitas.CodeGen.Tests;

public static class TestRepo
{
    /// <summary>测试程序集向上找 .git 根（与工具的 repoRoot 推导一致）。</summary>
    public static string Root()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, ".git")))
        {
            dir = dir.Parent;
        }
        return dir?.FullName ?? throw new InvalidOperationException("找不到仓库根（.git）");
    }
}

/// <summary>
/// Golden 基线：全管线跑真实 moba 配置，对照 checked-in 的生成基线清单（生成器自洽）。
/// 与示例包目录的字节兼容已按裁定放弃——模板演进改基线（--update-baseline），示例包差异用
/// --diff-generated-dir 按需查看，接入替换时消化。
/// </summary>
public sealed class GoldenBaselineTests
{
    [Fact]
    public void FullPipeline_MatchesCheckedInBaseline()
    {
        var configPath = Path.Combine(TestRepo.Root(), "tools", "entitas-codegen.moba.json");
        var sink = new Diagnostics.CapturingConsoleSink();

        var exit = CodeGenRunner.Run(new RunOptions(ConfigPath: configPath), sink);

        Assert.True(exit == Diagnostics.ExitCodes.Ok, $"退出码 {exit}，输出:\n{string.Join('\n', sink.Lines)}");
    }

    [Fact]
    public void DiffGeneratedDir_IsInformationalOnly()
    {
        var configPath = Path.Combine(TestRepo.Root(), "tools", "entitas-codegen.moba.json");
        var sink = new Diagnostics.CapturingConsoleSink();

        // 与示例包目录的差异是接入前的预期增量，不影响退出码（无论差异多少都应为 0）。
        var exit = CodeGenRunner.Run(new RunOptions(ConfigPath: configPath, DiffGeneratedDir: true), sink);

        Assert.Equal(Diagnostics.ExitCodes.Ok, exit);
        Assert.Contains(sink.Lines, l => l.Contains("不影响退出码"));
    }

    [Fact]
    public void ProjectDiscovery_ResolvesConfigByConvention()
    {
        var sink = new Diagnostics.CapturingConsoleSink();
        var exit = CodeGenRunner.Run(new RunOptions(Project: "moba", ValidateOnly: true), sink);

        Assert.Equal(Diagnostics.ExitCodes.Ok, exit);
        Assert.Contains(sink.Lines, l => l.Contains("Actor: 43 组件"));
    }

    [Fact]
    public void Validate_PrintsModelSummary()
    {
        var configPath = Path.Combine(TestRepo.Root(), "tools", "entitas-codegen.moba.json");
        var sink = new Diagnostics.CapturingConsoleSink();

        var exit = CodeGenRunner.Run(new RunOptions(ConfigPath: configPath, ValidateOnly: true), sink);

        Assert.Equal(Diagnostics.ExitCodes.Ok, exit);
        Assert.Contains(sink.Lines, l => l.Contains("Actor: 43 组件"));
        Assert.Contains(sink.Lines, l => l.Contains("1 custom 手写"));
        Assert.Contains(sink.Lines, l => l.Contains("guard=UNITY_EDITOR"));
    }
}

/// <summary>序列化策略三档（业务选项）：off 通用中立 / opt-in 主动加入 / auto 默认全量+豁免制。</summary>
public sealed class SerializationModeTests
{
    [Fact]
    public void AutoMode_FlatComponentWithoutExemption_AutoEnrolls()
    {
        using var sandbox = new TempSandbox(config => config["serialization"] = new Dictionary<string, object> { ["mode"] = "auto" });
        // 基线组件是平值 int 字段，auto 模式无 coverage → 自动入集（无需任何声明）。
        var exit = sandbox.Run(out var sink);
        Assert.True(exit == Diagnostics.ExitCodes.Ok, string.Join("\n", sink.Lines));
        var generated = File.ReadAllText(Path.Combine(sandbox.Abs("generated"), "Actor", "ActorComponentSerialization.cs"));
        Assert.Contains("w.Write(e.hasBaseline);", generated);
    }

    [Fact]
    public void AutoMode_BlobWithoutExemption_Fails()
    {
        using var sandbox = new TempSandbox(config => config["serialization"] = new Dictionary<string, object> { ["mode"] = "auto" });
        sandbox.WriteFile("BlobProbeComponent.cs", """
            using System.Collections.Generic;
            using Entitas;
            namespace Sandbox.Components
            {
                [Actor]
                public sealed class BlobProbeComponent : IComponent
                {
                    public List<string> Names;
                }
            }
            """);
        var pinJson = JsonSerializer.Deserialize<PinFile>(File.ReadAllText(sandbox.PinPath), TempSandbox.Json);
        pinJson!.Contexts["Actor"].Components.Add(new PinFile.PinComponent { Name = "BlobProbe" }); // 不给 coverage
        File.WriteAllText(sandbox.PinPath, JsonSerializer.Serialize(pinJson, TempSandbox.Json));

        // 不给 coverage 豁免 → 不可序列化且未豁免 → fail-closed。
        var exit = sandbox.Run(out var sink);
        Assert.Equal(Diagnostics.ExitCodes.SourceValidation, exit);
        Assert.Contains(sink.Lines, l => l.Contains("BlobProbe") && l.Contains("coverage 声明豁免"));
    }

    [Fact]
    public void AutoMode_ExemptionRespected()
    {
        using var sandbox = new TempSandbox(config => config["serialization"] = new Dictionary<string, object> { ["mode"] = "auto" });
        sandbox.WriteComponent("ExemptProbe", "public int Value;");
        var pinJson = JsonSerializer.Deserialize<PinFile>(File.ReadAllText(sandbox.PinPath), TempSandbox.Json);
        pinJson!.Contexts["Actor"].Components.Add(new PinFile.PinComponent { Name = "ExemptProbe", Coverage = "immutable" });
        File.WriteAllText(sandbox.PinPath, JsonSerializer.Serialize(pinJson, TempSandbox.Json));

        CodeGenRunner.Run(new RunOptions(ConfigPath: sandbox.ConfigPath, Write: true, OutDir: sandbox.Abs("generated"), ForceGeneratedDir: true), sandbox.Sink);
        var exit = sandbox.Run(out var sink);
        Assert.True(exit == Diagnostics.ExitCodes.Ok, string.Join("\n", sink.Lines));
        var generated = File.ReadAllText(Path.Combine(sandbox.Abs("generated"), "Actor", "ActorComponentSerialization.cs"));
        Assert.DoesNotContain("ExemptProbe", generated.Substring(generated.IndexOf("public static void Write")));
    }

    [Fact]
    public void AutoMode_SerializeFlagRejected()
    {
        using var sandbox = new TempSandbox(config => config["serialization"] = new Dictionary<string, object> { ["mode"] = "auto" });
        var pinJson = JsonSerializer.Deserialize<PinFile>(File.ReadAllText(sandbox.PinPath), TempSandbox.Json);
        pinJson!.Contexts["Actor"].Components[0].Serialize = true;
        File.WriteAllText(sandbox.PinPath, JsonSerializer.Serialize(pinJson, TempSandbox.Json));

        var exit = sandbox.Run(out var sink);
        Assert.Equal(Diagnostics.ExitCodes.UsageOrConfig, exit);
        Assert.Contains(sink.Lines, l => l.Contains("auto 模式下 serialize 字段无效"));
    }

    [Fact]
    public void OffMode_RejectsPinSerializationFields()
    {
        using var sandbox = new TempSandbox(config => config["serialization"] = new Dictionary<string, object> { ["mode"] = "off" });
        // off 模式基线本应干净；手工塞回 serialize 标志 → 策略必须拒绝（未启用的选项不该悄悄生效）。
        var pinJson = JsonSerializer.Deserialize<PinFile>(File.ReadAllText(sandbox.PinPath), TempSandbox.Json);
        pinJson!.Contexts["Actor"].Components[0].Serialize = true;
        File.WriteAllText(sandbox.PinPath, JsonSerializer.Serialize(pinJson, TempSandbox.Json));

        var exit = sandbox.Run(out var sink);
        Assert.Equal(Diagnostics.ExitCodes.UsageOrConfig, exit);
        Assert.Contains(sink.Lines, l => l.Contains("serialization.mode=off 时 pin 不应出现 serialize/coverage 字段"));
    }
}

/// <summary>序列化已知类型（Vec3 等）。</summary>
public sealed class CoverageAuditTests
{
    [Fact]
    public void Vec3KnownType_GeneratesComponentSerialization()
    {
        using var sandbox = new TempSandbox(config =>
        {
            // 类型索引指向真实 core 包以解析 Vec3。
            config["typeIndex"] = new Dictionary<string, object>
            {
                ["roots"] = new[] { Path.Combine(TestRepo.Root(), "Unity", "Packages", "com.abilitykit.core") },
            };
        });
        sandbox.WriteFile("Aim2Component.cs", """
            using Entitas;
            using AbilityKit.Core.Mathematics;
            namespace Sandbox.Components
            {
                [Actor]
                public sealed class Aim2Component : IComponent
                {
                    public Vec3 Pos;
                }
            }
            """);
        var pinJson = JsonSerializer.Deserialize<PinFile>(File.ReadAllText(sandbox.PinPath), TempSandbox.Json);
        pinJson!.Contexts["Actor"].Components.Add(new PinFile.PinComponent { Name = "Aim2", Serialize = true });
        File.WriteAllText(sandbox.PinPath, JsonSerializer.Serialize(pinJson, TempSandbox.Json));

        // pin 变了，先刷新沙盒生成基线目录再校验。
        CodeGenRunner.Run(new RunOptions(ConfigPath: sandbox.ConfigPath, Write: true, OutDir: sandbox.Abs("generated"), ForceGeneratedDir: true), sandbox.Sink);

        var exit = sandbox.Run(out var sink);
        Assert.True(exit == Diagnostics.ExitCodes.Ok, string.Join("\n", sink.Lines));
        var generated = File.ReadAllText(Path.Combine(sandbox.Abs("generated"), "Actor", "ActorComponentSerialization.cs"));
        Assert.Contains("new AbilityKit.Core.Mathematics.Vec3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle())", generated);
        Assert.Contains("w.Write(p.Pos.X);", generated);
    }
}

/// <summary>基线清单行为：自洽校验绿、篡改红、--update-baseline 恢复。</summary>
public sealed class BaselineTests
{
    [Fact]
    public void Baseline_CheckUpdateTamperCycle()
    {
        using var sandbox = new TempSandbox();
        var config = JsonSerializer.Deserialize<Dictionary<string, object>>(File.ReadAllText(sandbox.ConfigPath));
        config!["baseline"] = "baseline.json";
        sandbox.WriteConfig(JsonSerializer.Serialize(config));

        // 没有基线文件时直接报错（提示先 --update-baseline）。
        Assert.Equal(Diagnostics.ExitCodes.Internal, sandbox.Run(out _));

        // 固化基线后全绿。
        CodeGenRunner.Run(new RunOptions(ConfigPath: sandbox.ConfigPath, UpdateBaseline: true), sandbox.Sink);
        Assert.Equal(Diagnostics.ExitCodes.Ok, sandbox.Run(out _));

        // 篡改基线哈希 → 红。
        var baselinePath = Path.Combine(sandbox.Root, "baseline.json");
        File.WriteAllText(baselinePath, File.ReadAllText(baselinePath).Replace("\"files\"", "\"xiles\""));
        Assert.Equal(Diagnostics.ExitCodes.DriftOrDiff, sandbox.Run(out _));
    }
}

/// <summary>漂移与 pin 行为（临时沙盒，组件只用基元类型，类型索引可指向空目录）。</summary>
public sealed class DriftAndPinTests() : TempSandboxTests
{
    [Fact]
    public void UnknownComponent_FailsWithDriftExitCode()
    {
        Sandbox.WriteComponent("DriftProbe", "public int Value;");
        var exit = Run(out var sink);

        Assert.Equal(Diagnostics.ExitCodes.DriftOrDiff, exit);
        Assert.Contains(sink.Lines, l => l.Contains("源码发现 Actor.DriftProbe 但 pin 里没有"));
    }

    [Fact]
    public void UpdatePin_AppendsAndRecovers()
    {
        Sandbox.WriteComponent("DriftProbe", "public int Value;");

        CodeGenRunner.Run(new RunOptions(ConfigPath: Sandbox.ConfigPath, UpdatePin: true), Sandbox.Sink);

        var pin = JsonSerializer.Deserialize<PinFile>(File.ReadAllText(Sandbox.PinPath), TempSandbox.Json);
        Assert.Contains(pin!.Contexts["Actor"].Components, p => p.Name == "DriftProbe");

        CodeGenRunner.Run(new RunOptions(ConfigPath: Sandbox.ConfigPath, Write: true, OutDir: Sandbox.Abs("generated"), ForceGeneratedDir: true), Sandbox.Sink);
        var exit = Run(out var sink);
        Assert.True(exit == Diagnostics.ExitCodes.Ok, $"刷新基线后应全绿，输出:\n{string.Join('\n', sink.Lines)}");
    }

    [Fact]
    public void StalePinEntry_Fails()
    {
        var pinJson = JsonSerializer.Deserialize<PinFile>(File.ReadAllText(Sandbox.PinPath), TempSandbox.Json);
        pinJson!.Contexts["Actor"].Components.Add(new PinFile.PinComponent { Name = "Ghost" });
        File.WriteAllText(Sandbox.PinPath, JsonSerializer.Serialize(pinJson, TempSandbox.Json));

        var exit = Run(out var sink);
        Assert.Equal(Diagnostics.ExitCodes.DriftOrDiff, exit);
        Assert.Contains(sink.Lines, l => l.Contains("Actor.Ghost 已不存在于源码"));
    }

    [Fact]
    public void SkippedComponent_DoesNotAppearInOutput()
    {
        Sandbox.WriteComponent("SkippedProbe", "public int Value;");
        var pinJson = JsonSerializer.Deserialize<PinFile>(File.ReadAllText(Sandbox.PinPath), TempSandbox.Json);
        pinJson!.Contexts["Actor"].Components.Add(new PinFile.PinComponent { Name = "SkippedProbe", Generate = false });
        File.WriteAllText(Sandbox.PinPath, JsonSerializer.Serialize(pinJson, TempSandbox.Json));

        var exit = Run(out var sink);
        Assert.Equal(Diagnostics.ExitCodes.Ok, exit);
        Assert.DoesNotContain(sink.Lines, l => l.Contains("SkippedProbe 但 pin 里没有"));
    }

}

/// <summary>源码校验规则：宁可失败，不可漏生成。</summary>
public sealed class SourceValidationTests() : TempSandboxTests
{
    [Fact]
    public void PropertyInComponent_Fails()
    {
        Sandbox.WriteComponent("BadProp", "public int Value { get; set; }");
        var exit = Run(out var sink);
        Assert.Equal(Diagnostics.ExitCodes.SourceValidation, exit);
        Assert.Contains(sink.Lines, l => l.Contains("组件不允许属性"));
    }

    [Fact]
    public void MethodInComponent_Fails()
    {
        Sandbox.WriteComponent("BadMethod", "public int Value;\n        public void Tick() { }");
        var exit = Run(out var sink);
        Assert.Equal(Diagnostics.ExitCodes.SourceValidation, exit);
        Assert.Contains(sink.Lines, l => l.Contains("组件不允许方法"));
    }

    [Fact]
    public void NonPublicField_Fails()
    {
        Sandbox.WriteComponent("BadAccess", "public int Value;\n        private int hidden;");
        var exit = Run(out var sink);
        Assert.Equal(Diagnostics.ExitCodes.SourceValidation, exit);
        Assert.Contains(sink.Lines, l => l.Contains("无法解析的字段行"));
    }

    [Fact]
    public void MissingContextAttribute_Fails()
    {
        Sandbox.WriteFile("NoContext.cs", """
            using Entitas;
            namespace Sandbox.Components
            {
                public sealed class NoContextComponent : IComponent
                {
                    public int Value;
                }
            }
            """);
        var exit = Run(out var sink);
        Assert.Equal(Diagnostics.ExitCodes.SourceValidation, exit);
        Assert.Contains(sink.Lines, l => l.Contains("缺少 context 属性"));
    }
}

/// <summary>配置严格性与发现。</summary>
public sealed class ConfigTests() : TempSandboxTests
{
    [Fact]
    public void UnknownKey_Fails()
    {
        var config = JsonSerializer.Deserialize<Dictionary<string, object>>(File.ReadAllText(Sandbox.ConfigPath));
        config!["typoKey"] = "x";
        Sandbox.WriteConfig(JsonSerializer.Serialize(config));
        var exit = Run(out _);
        Assert.Equal(Diagnostics.ExitCodes.UsageOrConfig, exit);
    }

    [Fact]
    public void WrongSchema_Fails()
    {
        var config = JsonSerializer.Deserialize<Dictionary<string, object>>(File.ReadAllText(Sandbox.ConfigPath));
        config!["schema"] = 1;
        Sandbox.WriteConfig(JsonSerializer.Serialize(config));
        var exit = Run(out var sink);
        Assert.Equal(Diagnostics.ExitCodes.UsageOrConfig, exit);
        Assert.Contains(sink.Lines, l => l.Contains("schema 必须是 2"));
    }

    [Fact]
    public void ProjectAndConfig_AreMutuallyExclusive()
    {
        var exit = CodeGenRunner.Run(new RunOptions(Project: "moba", ConfigPath: Sandbox.ConfigPath), Sandbox.Sink);
        Assert.Equal(Diagnostics.ExitCodes.UsageOrConfig, exit);
    }

    [Fact]
    public void ExportConfig_WritesEffectiveConfigWithDefaults()
    {
        var exportPath = Sandbox.Abs("effective.json");
        var exit = CodeGenRunner.Run(new RunOptions(ConfigPath: Sandbox.ConfigPath, ExportConfigPath: exportPath), Sandbox.Sink);
        Assert.Equal(Diagnostics.ExitCodes.Ok, exit);

        using var doc = JsonDocument.Parse(File.ReadAllText(exportPath));
        var root = doc.RootElement;
        Assert.Equal("components", root.GetProperty("sources").GetProperty("components")[0].GetString()); // 仓库相对路径
        Assert.True(root.GetProperty("editor").GetProperty("emitFeatureClass").GetBoolean());               // 缺省段落显式化
        Assert.True(root.GetProperty("typeIndex").GetProperty("skipDirs").GetArrayLength() > 0);           // 默认值填满
        Assert.Equal("generated", root.GetProperty("output").GetProperty("generatedDir").GetString());
    }
}

/// <summary>编辑器专属代码开关与守卫宏（特定环境生产不需要 Unity Editor 宏的场景）。</summary>
public sealed class EditorOptionsTests
{
    [Fact]
    public void FeatureAndObserver_CanBeDisabled()
    {
        using var sandbox = new TempSandbox(config =>
        {
            config["editor"] = new Dictionary<string, object>
            {
                ["emitContextObserver"] = false,
                ["emitFeatureClass"] = false,
            };
        });

        CodeGenRunner.Run(new RunOptions(ConfigPath: sandbox.ConfigPath, Write: true, OutDir: sandbox.Abs("generated"), ForceGeneratedDir: true), sandbox.Sink);

        Assert.False(File.Exists(Path.Combine(sandbox.Abs("generated"), "Feature.cs")));
        var contexts = File.ReadAllText(Path.Combine(sandbox.Abs("generated"), "Contexts.cs"));
        Assert.DoesNotContain("CreateContextObserver", contexts);
        Assert.Contains("public void Reset()", contexts); // 主 partial 仍在
    }

    [Fact]
    public void GuardSymbols_AreConfigurable()
    {
        using var sandbox = new TempSandbox(config =>
        {
            config["editor"] = new Dictionary<string, object>
            {
                ["defineSymbols"] = new[] { "AK_SERVER", "DEBUG_DRAW" },
            };
        });

        CodeGenRunner.Run(new RunOptions(ConfigPath: sandbox.ConfigPath, Write: true, OutDir: sandbox.Abs("generated"), ForceGeneratedDir: true), sandbox.Sink);

        var contexts = File.ReadAllText(Path.Combine(sandbox.Abs("generated"), "Contexts.cs"));
        Assert.Contains("#if (!ENTITAS_DISABLE_VISUAL_DEBUGGING && AK_SERVER && DEBUG_DRAW)", contexts);
        var feature = File.ReadAllText(Path.Combine(sandbox.Abs("generated"), "Feature.cs"));
        Assert.Contains("&& AK_SERVER && DEBUG_DRAW)", feature);
    }

    [Fact]
    public void EmptyDefineSymbols_Fails()
    {
        // 无效配置测试不能走需要绿基线的沙盒构造——先建正常沙盒，再改坏配置。
        using var sandbox = new TempSandbox();
        var config = JsonSerializer.Deserialize<Dictionary<string, object>>(File.ReadAllText(sandbox.ConfigPath));
        config!["editor"] = new Dictionary<string, object> { ["defineSymbols"] = Array.Empty<string>() };
        sandbox.WriteConfig(JsonSerializer.Serialize(config));

        var exit = sandbox.Run(out var sink);
        Assert.Equal(Diagnostics.ExitCodes.UsageOrConfig, exit);
        Assert.Contains(sink.Lines, l => l.Contains("defineSymbols 不能为空"));
    }
}

/// <summary>类型解析与模板单元。</summary>
public sealed class UnitTests
{
    [Fact]
    public void TypeResolver_PrimitivesArraysGenericsBcl()
    {
        var index = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal)
        {
            ["BuffRuntime"] = new() { "Game.Buffs.BuffRuntime" },
            ["Vec3"] = new() { "Math.Vec3" },
        };
        var usings = new List<string> { "Game.Buffs", "Math" };

        Assert.Equal("int", TypeResolver.Resolve("int", "Ns", usings, index, "f", 1));
        Assert.Equal("Game.Buffs.BuffRuntime[]", TypeResolver.Resolve("BuffRuntime[]", "Ns", usings, index, "f", 1));
        Assert.Equal("System.Collections.Generic.List<Game.Buffs.BuffRuntime>", TypeResolver.Resolve("List<BuffRuntime>", "Ns", usings, index, "f", 1));
        Assert.Equal("System.Collections.Generic.Dictionary<Math.Vec3, int>", TypeResolver.Resolve("Dictionary<Vec3, int>", "Ns", usings, index, "f", 1));
    }

    [Fact]
    public void TypeResolver_AmbiguousShortName_Fails()
    {
        var index = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal)
        {
            ["Dup"] = new() { "A.Dup", "B.Dup" },
        };
        Assert.Throws<Diagnostics.CodeGenException>(() =>
            TypeResolver.Resolve("Dup", "Ns", new List<string>(), index, "f", 1));
    }

    [Fact]
    public void EmptyLookup_HasUniformShape_NoQuirks()
    {
        var output = Templates.ComponentsLookup("Game", new List<Templates.LookupEntry>(), "HEADER");
        // 归一后的规范形态：空 consts 段与有内容时同构（两个段间空行），数组段空即空。
        Assert.Contains("{\n\n\n    public const int TotalComponents = 0;", output);
        Assert.DoesNotContain("{\n\n\n\n", output);
    }

    [Fact]
    public void Matcher_UsesUniformIndent_NoHistoricalQuirk()
    {
        var output = Templates.MatcherClass("Actor", "H");
        Assert.DoesNotContain("\n          return", output); // 历史 10 空格怪癖已归一
        Assert.Equal(4, output.Split("\n        return").Length - 1); // 四处 8 空格 return
    }

    [Fact]
    public void Contexts_UsesCanonicalTypeSyntax()
    {
        var output = Templates.ContextsClass(new List<string> { "Actor" }, "H");
        Assert.Contains("new Entitas.IContext[] { actor }", output); // 历史 "IContext []" 空格已归一
    }

    [Fact]
    public void Attribution_DefaultsToAbilityKit()
    {
        Assert.Equal("abilitykit", new CodeGenConfig.OutputSection().Attribution);
    }

    [Fact]
    public void FlagComponent_UsesIsProperty()
    {
        var def = new ComponentInfo("StunTag", "Ns.StunTagComponent", new List<FieldInfo>(), new HashSet<string> { "Actor" }, "f.cs");
        var output = Templates.ComponentApi("Actor", def, "H1", "H2");
        Assert.Contains("public bool isStunTag {", output);
        Assert.DoesNotContain("AddStunTag", output);
    }

    [Fact]
    public void Attribution_SwitchChangesHeader()
    {
        var model = new CodeGenModel
        {
            Contexts = new List<ContextInfo>(),
            Editor = new ModelEditorOptions(),
            PreservedFiles = new List<string>(),
            SerializablePlainTypes = new Dictionary<string, Model.PlainTypeInfo>(),
            Attribution = "abilitykit",
        };
        Assert.Equal("AbilityKit.Entitas.CodeGen.ContextGenerator",
            HeaderNames.Resolve(model, "Context", "Entitas.CodeGeneration.Plugins.ContextGenerator"));
    }

    [Fact]
    public void Pipeline_IsDeterministicAcrossRuns()
    {
        var def = new ComponentInfo("Probe", "Ns.ProbeComponent",
            new List<FieldInfo> { new("int", "Value") }, new HashSet<string> { "Actor" }, "f.cs");
        var model = new CodeGenModel
        {
            Contexts = new List<ContextInfo>
            {
                new ContextInfo("Actor").Tap(c => c.Components.Add(new PinnedComponent("Probe", false, true, false, null, def))),
            },
            Editor = new ModelEditorOptions(),
            Attribution = "entitas-parity",
            PreservedFiles = new List<string>(),
            SerializablePlainTypes = new Dictionary<string, Model.PlainTypeInfo>(),
        };

        var ctx = new Pipeline.PipelineContext(".", MinimalConfig());
        var files1 = Pipeline.Pipeline.CreateDefault().Run(model, ctx);
        var files2 = Pipeline.Pipeline.CreateDefault().Run(model, ctx);

        Assert.Equal(
            files1.Ordered.Select(f => (f.Path, f.Content)),
            files2.Ordered.Select(f => (f.Path, f.Content)));
    }

    private static CodeGenConfig MinimalConfig() => new()
    {
        Contexts = new List<string> { "Actor" },
        Output = new CodeGenConfig.OutputSection(),
    };
}

/// <summary>测试基类：每测试类一个沙盒实例（xunit 每类实例化一次），类销毁时清理。</summary>
public abstract class TempSandboxTests : IDisposable
{
    protected TempSandbox Sandbox { get; }

    protected TempSandboxTests(Action<Dictionary<string, object>>? configure = null)
    {
        Sandbox = new TempSandbox(configure);
    }

    protected int Run(out Diagnostics.CapturingConsoleSink sink) => Sandbox.Run(out sink);

    public void Dispose() => Sandbox.Dispose();
}

/// <summary>临时沙盒：组件目录 + 空 typeIndex + schema2 配置 + 只有一个基线组件的 pin。</summary>
public sealed class TempSandbox : IDisposable
{
    public string Root { get; }
    public string ConfigPath { get; }
    public string PinPath { get; }
    public Diagnostics.CapturingConsoleSink Sink { get; } = new();

    private readonly Dictionary<string, object> _config;

    public TempSandbox(Action<Dictionary<string, object>>? configure = null)
    {
        Root = Path.Combine(Path.GetTempPath(), "ak-entitas-codegen-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(Root, "components"));
        Directory.CreateDirectory(Path.Combine(Root, "index"));
        // 沙盒自带 .git 标记，使工具把沙盒根当 repoRoot（配置里的相对路径据此解析）。
        Directory.CreateDirectory(Path.Combine(Root, ".git"));

        PinPath = Path.Combine(Root, "pin.json");
        ConfigPath = Path.Combine(Root, "config.json");

        WriteComponent("Baseline", "public int Value;");

        _config = new Dictionary<string, object>
        {
            ["schema"] = 2,
            ["project"] = "sandbox",
            ["sources"] = new Dictionary<string, object> { ["components"] = new[] { "components" } },
            ["typeIndex"] = new Dictionary<string, object> { ["roots"] = new[] { "index" } },
            ["output"] = new Dictionary<string, object> { ["generatedDir"] = "generated" },
            ["serialization"] = new Dictionary<string, object> { ["mode"] = "opt-in" },
            ["contexts"] = new[] { "Actor" },
            ["pin"] = "pin.json",
        };
        configure?.Invoke(_config);

        // pin 基线条目按序列化模式生成（auto/off 模式带 serialize 字段会被策略拒绝）。
        var mode = (_config.TryGetValue("serialization", out var ser) && ser is Dictionary<string, object> sd && sd.TryGetValue("mode", out var m))
            ? (string)m : "opt-in";
        var baselineEntry = mode == "opt-in" ? "{ \"name\": \"Baseline\", \"serialize\": true }" : "{ \"name\": \"Baseline\" }";
        File.WriteAllText(PinPath,
            "{\n  \"contexts\": {\n    \"Actor\": { \"components\": [ " + baselineEntry + " ] }\n  }\n}\n".Replace("\n", "\r\n"));

        WriteConfig(JsonSerializer.Serialize(_config));

        // 预生成基线：让"对比基准"存在，基线 Run 应为 0。
        // 沙盒里 OutDir == generatedDir 是刻意的（模拟接入替换后的正式目录），显式过写保护。
        // init 轮的退出码是 1（写盘前对比基准还不存在，属预期），只看写盘后的 baseline 轮。
        CodeGenRunner.Run(new RunOptions(ConfigPath: ConfigPath, Write: true, OutDir: Abs("generated"), ForceGeneratedDir: true), Sink);
        var baseline = Run(out var baselineSink);
        if (baseline != Diagnostics.ExitCodes.Ok)
            throw new InvalidOperationException(
                $"沙盒基线初始化失败（baseline={baseline}）:\n{string.Join('\n', Sink.Lines)}\n--- baseline run ---\n{string.Join('\n', baselineSink.Lines)}");
    }

    public static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public string Abs(string rel) => Path.Combine(Root, rel);

    public void WriteComponent(string name, string fields) =>
        WriteFile(name + "Component.cs", """
            using Entitas;
            namespace Sandbox.Components
            {
                [Actor]
                public sealed class [[NAME]]Component : IComponent
                {
                    [[FIELDS]]
                }
            }
            """
            .Replace("[[NAME]]", name)
            .Replace("[[FIELDS]]", fields));

    public void WriteFile(string fileName, string content) =>
        File.WriteAllText(Path.Combine(Root, "components", fileName), content.Replace("\n", "\r\n"));

    public void WriteConfig(string json) => File.WriteAllText(ConfigPath, json);

    public int Run(out Diagnostics.CapturingConsoleSink sink)
    {
        sink = new Diagnostics.CapturingConsoleSink();
        return CodeGenRunner.Run(new RunOptions(ConfigPath: ConfigPath), sink);
    }

    public void Dispose()
    {
        try { Directory.Delete(Root, recursive: true); } catch { /* 临时目录清理失败不影响测试 */ }
    }
}

internal static class TapExtension
{
    public static T Tap<T>(this T self, Action<T> action)
    {
        action(self);
        return self;
    }
}

/// <summary>Roslyn 扫描器与 regex 扫描器的 parity：同一真实仓库配置下，生成物必须逐文件一致。</summary>
public sealed class ScannerParityTests
{
    [Fact]
    public void RoslynVsRegex_IdenticalGeneratedOutput()
    {
        var repoRoot = TestRepo.Root();
        var (_, _, config) = AbilityKit.Entitas.CodeGen.Configuration.ConfigLocator.Load(
            Path.Combine(repoRoot, "tools", "entitas-codegen.moba.json"));
        var pin = AbilityKit.Entitas.CodeGen.Configuration.PinFile.LoadOrEmpty(
            config.Resolve(repoRoot, config.Pin));
        var sink = new Diagnostics.CapturingConsoleSink();

        var filesByScanner = new Dictionary<string, List<(string Path, string Content)>>();

        foreach (var scannerName in new[] { "regex", "roslyn" })
        {
            config.Scanner = scannerName;
            var watch = System.Diagnostics.Stopwatch.StartNew();
            var build = ModelBuilder.Build(config, repoRoot, pin, sink);
            Assert.False(build.HasDrift, string.Join("\n", build.DriftFailures));
            var files = Pipeline.Pipeline.CreateDefault().Run(build.Model!, new Pipeline.PipelineContext(repoRoot, config));
            watch.Stop();
            filesByScanner[scannerName] = files.Ordered.Select(f => (f.Path, f.Content)).ToList();
            Console.WriteLine($"[parity] {scannerName}: {files.Count} 文件, {watch.ElapsedMilliseconds}ms");
        }

        var regex = filesByScanner["regex"];
        var roslyn = filesByScanner["roslyn"];

        Assert.Equal(regex.Select(x => x.Path), roslyn.Select(x => x.Path));
        var diffs = regex.Zip(roslyn).Where(pair => pair.First.Content != pair.Second.Content).ToList();
        Assert.True(diffs.Count == 0,
            "扫描器产物不一致:\n" + string.Join("\n", diffs.Select(d => $"  {d.First.Path}")));
    }
}
