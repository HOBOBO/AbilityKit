using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using AbilityKit.Entitas.CodeGen.Diagnostics;

namespace AbilityKit.Entitas.CodeGen.Configuration;



/// <summary>
/// 配置读取抽象：格式可扩展（JSON 已实现；将来需要 YAML/其他格式时新增实现即可，工具零依赖原则下按需引入）。
/// </summary>
public interface IConfigReader
{
    /// <summary>严格读取（未知键报错）。读取失败抛 <see cref="CodeGenException"/>（exit 2）。</summary>
    T Read<T>(string path) where T : class;
}

/// <summary>JSON 实现（System.Text.Json，零依赖，严格模式）。</summary>
public sealed class JsonConfigReader : IConfigReader
{
    private static readonly JsonSerializerOptions Strict = new()
    {
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    public T Read<T>(string path) where T : class
    {
        try
        {
            return JsonSerializer.Deserialize<T>(File.ReadAllText(path), Strict)
                   ?? throw new CodeGenException($"配置文件为空: {path}", ExitCodes.UsageOrConfig);
        }
        catch (JsonException ex)
        {
            throw new CodeGenException($"配置文件解析失败（检查 schema/未知键/类型）: {path}\n  {ex.Message}", ExitCodes.UsageOrConfig, ex);
        }
    }
}

/// <summary>
/// 配置定位与加载：
/// - 显式 --config 优先；
/// - --project &lt;name&gt; 按约定解析 &lt;repoRoot&gt;/tools/entitas-codegen.&lt;name&gt;.json（repoRoot 从 CWD 向上找 .git）；
/// - ListProjects 枚举 tools/ 下全部项目配置（gate/CI 全量跑用）。
/// </summary>
public static class ConfigLocator
{
    public const string ConfigFilePattern = "entitas-codegen.*.json";

    /// <summary>解析配置路径：project 与 config 二选一；都不给则找唯一项目配置，多于一个则报错列出。</summary>
    public static string ResolveConfigPath(string? project, string? configPath)
    {
        if (configPath is not null && project is not null)
        {
            throw new CodeGenException("--config 与 --project 只能二选一。", ExitCodes.UsageOrConfig);
        }

        if (configPath is not null)
        {
            var full = Path.GetFullPath(configPath);
            if (!File.Exists(full))
                throw new CodeGenException($"配置文件不存在: {full}", ExitCodes.UsageOrConfig);
            return full;
        }

        var repoRoot = FindRepoRoot(Directory.GetCurrentDirectory());
        if (project is not null)
        {
            var byName = Path.Combine(repoRoot, "tools", $"entitas-codegen.{project}.json");
            if (!File.Exists(byName))
                throw new CodeGenException(
                    $"按约定未找到 tools/entitas-codegen.{project}.json（repoRoot={repoRoot}）。可用项目: {string.Join(", ", ListProjects(repoRoot))}",
                    ExitCodes.UsageOrConfig);
            return byName;
        }

        var projects = ListProjects(repoRoot);
        return projects.Count switch
        {
            0 => throw new CodeGenException($"tools/ 下没有 {ConfigFilePattern} 配置。", ExitCodes.UsageOrConfig),
            1 => projects[0],
            _ => throw new CodeGenException(
                $"存在多个项目配置，请用 --project 指定其一: {string.Join(", ", projects.Select(Path.GetFileName))}", ExitCodes.UsageOrConfig),
        };
    }

    public static List<string> ListProjects(string repoRoot)
    {
        var toolsDir = Path.Combine(repoRoot, "tools");
        return Directory.Exists(toolsDir)
            ? Directory.EnumerateFiles(toolsDir, ConfigFilePattern).OrderBy(p => p, StringComparer.Ordinal).ToList()
            : new List<string>();
    }

    public static string FindRepoRoot(string anchor)
    {
        var dir = Path.GetFullPath(anchor);
        while (dir is not null && !Directory.Exists(Path.Combine(dir, ".git")))
        {
            dir = Path.GetDirectoryName(dir);
        }
        return dir ?? throw new CodeGenException($"从 {anchor} 向上找不到 .git 仓库根。", ExitCodes.UsageOrConfig);
    }

    /// <summary>加载 + 填默认 + 校验，返回 (repoRoot, config)。</summary>
    public static (string repoRoot, string configPath, CodeGenConfig config) Load(string configPath, IConfigReader? reader = null)
    {
        reader ??= new JsonConfigReader();
        var full = Path.GetFullPath(configPath);
        if (!File.Exists(full))
            throw new CodeGenException($"配置文件不存在: {full}", ExitCodes.UsageOrConfig);

        var config = reader.Read<CodeGenConfig>(full);
        var repoRoot = ResolveRepoRootFromConfig(full);
        config.Validate(repoRoot, full);
        return (repoRoot, full, config);
    }

    /// <summary>repoRoot = 从配置文件所在目录向上找到的 .git 根；找不到则退回两级目录。</summary>
    public static string ResolveRepoRootFromConfig(string anchorPath)
    {
        var dir = Path.GetDirectoryName(Path.GetFullPath(anchorPath));
        while (dir is not null && !Directory.Exists(Path.Combine(dir, ".git")))
        {
            dir = Path.GetDirectoryName(dir);
        }
        return dir ?? Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(anchorPath))!, "..", ".."));
    }

    /// <summary>
    /// 导出有效配置：默认值填满、路径归一为仓库相对路径。用于评审/CI 固化——
    /// 看到的就是运行时真正生效的配置，不含隐式缺省。
    /// </summary>
    public static string ExportEffective(CodeGenConfig config, string repoRoot)
    {
        var effective = JsonSerializer.SerializeToNode(config.WithDefaultsApplied(), new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        });

        // 路径字段转仓库相对，导出物可移植评审。
        foreach (var (sectionKey, listKey) in new[] { (SourcesPathKey, "components"), (TypeIndexPathKey, "roots") })
        {
            if (effective[sectionKey]?[listKey] is JsonArray arr)
            {
                for (var i = 0; i < arr.Count; i++)
                {
                    if (arr[i] is JsonValue v && v.TryGetValue<string>(out var p))
                    {
                        arr[i] = JsonValue.Create(ToRepoRelative(repoRoot, p));
                    }
                }
            }
        }

        if (effective["output"]?["generatedDir"] is JsonValue gd && gd.TryGetValue<string>(out var gdPath))
        {
            effective["output"]!["generatedDir"] = JsonValue.Create(ToRepoRelative(repoRoot, gdPath));
        }
        if (effective["pin"] is JsonValue pv && pv.TryGetValue<string>(out var pinPath))
        {
            effective["pin"] = JsonValue.Create(ToRepoRelative(repoRoot, pinPath));
        }

        return effective!.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + "\n";
    }

    private const string SourcesPathKey = "sources";
    private const string TypeIndexPathKey = "typeIndex";

    private static string ToRepoRelative(string repoRoot, string path)
    {
        var full = Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(repoRoot, path));
        var rel = Path.GetRelativePath(repoRoot, full).Replace('\\', '/');
        return rel.StartsWith('.') ? path : rel;
    }
}
