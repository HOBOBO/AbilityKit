using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AbilityKit.Entitas.CodeGen.Pipeline;

namespace AbilityKit.Entitas.CodeGen.Compare;

/// <summary>
/// 生成基线清单：path → 内容哈希（LF 归一后 SHA-256）。checked-in、无时间戳，确定性可 diff。
/// 用途：把"生成器自洽校验"与"示例包目录对比"解耦——模板演进改基线（--update-baseline），
/// 接入替换前示例包目录的差异是预期内的，不再阻塞绿灯。
/// </summary>
public sealed class BaselineStore
{
    public const int CurrentSchema = 1;

    public int Schema { get; set; } = CurrentSchema;
    public string Project { get; set; } = "";
    public Dictionary<string, string> Files { get; set; } = new(StringComparer.Ordinal);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static BaselineStore FromFiles(string project, FileSet files)
    {
        var store = new BaselineStore { Project = project };
        foreach (var file in files.Ordered)
        {
            store.Files[file.Path] = Hash(file.Content);
        }
        return store;
    }

    public void Save(string path) => File.WriteAllText(path, JsonSerializer.Serialize(this, JsonOptions) + "\n");

    public static BaselineStore Load(string path)
    {
        if (!File.Exists(path))
            throw new InvalidOperationException($"基线清单不存在: {path}（先跑 --update-baseline 生成）");
        return JsonSerializer.Deserialize<BaselineStore>(File.ReadAllText(path), JsonOptions)
               ?? throw new InvalidOperationException($"基线清单解析失败: {path}");
    }

    /// <summary>对比生成结果与基线。返回 (一致, 差异明细)。</summary>
    public (bool Clean, List<string> Differences) Diff(FileSet files)
    {
        var differences = new List<string>();

        foreach (var file in files.Ordered)
        {
            var hash = Hash(file.Content);
            if (!Files.TryGetValue(file.Path, out var baselineHash))
            {
                differences.Add($"新增文件（基线没有）: {file.Path}");
            }
            else if (baselineHash != hash)
            {
                differences.Add($"内容变化: {file.Path}");
            }
        }

        foreach (var path in Files.Keys.Except(files.Ordered.Select(f => f.Path), StringComparer.Ordinal))
        {
            differences.Add($"基线里有但本次未生成: {path}");
        }

        return (differences.Count == 0, differences);
    }

    private static string Hash(string content)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(bytes);
    }
}
