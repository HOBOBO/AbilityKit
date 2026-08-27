using System.Text.RegularExpressions;
using Xunit;

namespace AbilityKit.Protocol.Tests;

/// <summary>
/// 旧 ScriptableObject 协议定义 + 代码生成双轨入口已冻结（superseded by YAML Protocol Workspace）。
/// 本测试做静态守护：旧生成器不得复活、菜单/CreateAssetMenu 不得回归、
/// 一次性迁移读取能力必须保留。防止后续改动无意间重新引入双轨入口。
/// </summary>
public sealed partial class ProtocolEditorLegacyTrackFreezeTests
{
    private const string EditorRootRelative = "Unity/Packages/com.abilitykit.protocol.editor/Editor/ProtocolEditor";

    private const string OfficialWorkspaceMenu = "Tools/AbilityKit/Framework/Protocol/Protocol Workspace";

    private const string OneTimeMigrationMenu =
        "Tools/AbilityKit/Framework/Protocol/Migrate Legacy ProtocolDefinition (one-time)";

    /// <summary>旧生成器会写出的产物标记，包内任何源码都不允许再出现。</summary>
    private static readonly string[] ForbiddenGenerationMarkers =
    {
        "MemoryPackable",
        "OpCodes.g.cs",
        "WireProtocolTypes",
        "GenerateOpCodes",
        "GenerateSnapshotRoutingGlue",
        "GenerateCodecBackendStubs",
    };

    [Fact]
    public void LegacyGeneratorSources_AreRemoved()
    {
        var root = EditorRoot();
        Assert.False(
            Directory.Exists(Path.Combine(root, "Generator")),
            "Editor/ProtocolEditor/Generator 必须保持删除：旧代码生成器（MemoryPack DTO / OpCodes / 路由胶水）不得复活。");
        Assert.False(
            File.Exists(Path.Combine(root, "UI", "SnapshotRoutingImporterWindow.cs")),
            "SnapshotRoutingImporterWindow 已随旧轨冻结删除，不得恢复。");
        Assert.False(
            File.Exists(Path.Combine(root, "UI", "CSharpTypeNameUtility.cs")),
            "CSharpTypeNameUtility 唯一消费者是已删除的导入器，不得恢复。");
    }

    [Fact]
    public void EditorSources_DoNotEmitLegacyGenerationArtifacts()
    {
        var root = RepositoryRoot();
        var violations = new List<string>();
        foreach (var file in EnumerateEditorSources())
        {
            var source = File.ReadAllText(file);
            foreach (var marker in ForbiddenGenerationMarkers)
            {
                if (source.Contains(marker, StringComparison.Ordinal))
                {
                    violations.Add($"{Relative(root, file)}: 仍包含旧生成标记 '{marker}'");
                }
            }
        }

        Assert.True(
            violations.Count == 0,
            "旧生成器产物（MemoryPack DTO / OpCodes / 快照路由胶水）禁止再由本包生成：\n" +
            string.Join("\n", violations));
    }

    [Fact]
    public void ProtocolDefinitionSchema_IsFrozenReadOnly()
    {
        var root = EditorRoot();
        var schemaPath = Path.Combine(root, "Schema", "ProtocolDefinition.cs");
        Assert.True(File.Exists(schemaPath), "缺少 ProtocolDefinition 只读 Schema（迁移读取能力依赖它）。");

        var source = File.ReadAllText(schemaPath);
        // 匹配特性形式而非裸词，避免文档注释里"已移除"的说明文字误报。
        Assert.DoesNotContain("[CreateAssetMenu", source, StringComparison.Ordinal);
        Assert.Contains("[Obsolete", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ProtocolEditorMenus_OnlyOfficialWorkspaceAndOneTimeMigrationRemain()
    {
        var root = RepositoryRoot();
        var allowed = new HashSet<string>(StringComparer.Ordinal) { OfficialWorkspaceMenu, OneTimeMigrationMenu };
        var violations = new List<string>();

        foreach (var file in EnumerateEditorSources())
        {
            foreach (Match match in MenuItemPattern().Matches(File.ReadAllText(file)))
            {
                var menu = match.Groups[1].Value;
                if (!allowed.Contains(menu))
                {
                    violations.Add($"{Relative(root, file)}: 非白名单菜单 '{menu}'");
                }
            }
        }

        Assert.True(
            violations.Count == 0,
            "协议入口只允许 YAML Protocol Workspace 与一次性迁移窗口两个菜单：\n" +
            string.Join("\n", violations));
    }

    [Fact]
    public void OneTimeMigrationCapability_IsRetainedAndUsesOfficialCompilerPath()
    {
        var windowPath = Path.Combine(EditorRoot(), "UI", "ProtocolEditorWindow.cs");
        Assert.True(File.Exists(windowPath), "缺少 ProtocolEditorWindow.cs。");

        var source = File.ReadAllText(windowPath);
        Assert.Contains(OneTimeMigrationMenu, source, StringComparison.Ordinal);
        Assert.Contains("one-time", source, StringComparison.Ordinal);
        // 迁移必须复用官方 compiler 写入路径，而不是重新引入本地生成。
        Assert.Contains("ProtocolCompilerBridge.WriteCatalog", source, StringComparison.Ordinal);
        Assert.Contains("LegacyProtocolDefinitionMigrationWindow", source, StringComparison.Ordinal);
    }

    [Fact]
    public void PackageDocs_DeclareLegacyTrackSuperseded()
    {
        var packageRoot = Path.Combine(RepositoryRoot(), "Unity", "Packages", "com.abilitykit.protocol.editor");

        var readme = File.ReadAllText(Path.Combine(packageRoot, "README.md"));
        Assert.Contains("FROZEN", readme, StringComparison.Ordinal);
        Assert.Contains("superseded", readme, StringComparison.Ordinal);
        Assert.Contains("one-time", readme, StringComparison.Ordinal);

        var document = File.ReadAllText(Path.Combine(
            packageRoot, "Document", "ProtocolEditor协议编辑器模块开发设计文档.md"));
        Assert.Contains("已冻结", document, StringComparison.Ordinal);
    }

    private static string EditorRoot() => Path.Combine(RepositoryRoot(), EditorRootRelative.Replace('/', Path.DirectorySeparatorChar));

    private static IEnumerable<string> EnumerateEditorSources() =>
        Directory.Exists(EditorRoot())
            ? Directory.EnumerateFiles(EditorRoot(), "*.cs", SearchOption.AllDirectories)
            : Array.Empty<string>();

    private static string Relative(string repositoryRoot, string file) =>
        Path.GetRelativePath(repositoryRoot, file).Replace('\\', '/');

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
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
            $"Could not locate repository root from {AppContext.BaseDirectory}.");
    }

    [GeneratedRegex(@"\[MenuItem\(""([^""]+)""\)\]")]
    private static partial Regex MenuItemPattern();
}
