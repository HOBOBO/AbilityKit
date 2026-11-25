namespace AbilityKit.Protocol.CatalogCompiler.Emit;

public enum MemoryPackExportMismatchKind
{
    MissingFile,
    StaleFile,
    ExtraFile
}

public sealed record MemoryPackExportMismatch(
    MemoryPackExportMismatchKind Kind,
    string FileName,
    string Detail);

public sealed class MemoryPackExportVerification
{
    public IReadOnlyList<MemoryPackExportMismatch> Mismatches { get; }

    public bool IsCurrent => Mismatches.Count == 0;

    public MemoryPackExportVerification(IReadOnlyList<MemoryPackExportMismatch> mismatches)
    {
        Mismatches = mismatches;
    }
}

/// <summary>
/// 将确定的内存导出计划与导出目录入库文件比较。比较前统一 CRLF/LF：仓库没有 .gitattributes，core.autocrlf 会改写检出文件，直接比较字节会依赖各机器 Git 配置，而非生成内容。
/// </summary>
public static class MemoryPackExportVerifier
{
    public const string ManifestFileName = "protocol-export.json";

    /// <summary>
    /// 导出在目录内拥有的文件。门禁忽略其他文件，例如 Unity .meta。
    /// </summary>
    public static bool IsManagedFile(string fileName) =>
        fileName.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase) ||
        fileName.EndsWith(".proto", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(fileName, ManifestFileName, StringComparison.OrdinalIgnoreCase);

    public static MemoryPackExportVerification Verify(
        string directory,
        IReadOnlyDictionary<string, string> plannedFiles)
    {
        ArgumentNullException.ThrowIfNull(directory);
        ArgumentNullException.ThrowIfNull(plannedFiles);

        var mismatches = new List<MemoryPackExportMismatch>();
        foreach (var planned in plannedFiles.OrderBy(file => file.Key, StringComparer.Ordinal))
        {
            var path = Path.Combine(directory, planned.Key);
            if (!File.Exists(path))
            {
                mismatches.Add(new MemoryPackExportMismatch(
                    MemoryPackExportMismatchKind.MissingFile,
                    planned.Key,
                    "planned by the deterministic export but not committed"));
                continue;
            }

            var current = File.ReadAllText(path, System.Text.Encoding.UTF8);
            if (!string.Equals(NormalizeLineEndings(current), NormalizeLineEndings(planned.Value),
                    StringComparison.Ordinal))
            {
                mismatches.Add(new MemoryPackExportMismatch(
                    MemoryPackExportMismatchKind.StaleFile,
                    planned.Key,
                    "committed content differs from the deterministic export"));
            }
        }

        if (Directory.Exists(directory))
        {
            var plannedNames = new HashSet<string>(
                plannedFiles.Keys, StringComparer.OrdinalIgnoreCase);
            var extraFiles = Directory.EnumerateFiles(directory, "*", SearchOption.TopDirectoryOnly)
                .Select(Path.GetFileName)
                .OfType<string>()
                .Where(name => IsManagedFile(name) && !plannedNames.Contains(name))
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase);
            foreach (var extra in extraFiles)
                mismatches.Add(new MemoryPackExportMismatch(
                    MemoryPackExportMismatchKind.ExtraFile,
                    extra,
                    "committed export file is no longer generated; remove it"));
        }

        mismatches.Sort((left, right) =>
        {
            var byKind = ((int)left.Kind).CompareTo((int)right.Kind);
            return byKind != 0 ? byKind : string.CompareOrdinal(left.FileName, right.FileName);
        });
        return new MemoryPackExportVerification(mismatches);
    }

    public static string NormalizeLineEndings(string value) =>
        value.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\r", "\n", StringComparison.Ordinal);
}
