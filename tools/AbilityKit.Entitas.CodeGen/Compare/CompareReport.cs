using System.Text;
using AbilityKit.Entitas.CodeGen.Pipeline;

namespace AbilityKit.Entitas.CodeGen.Compare;

/// <summary>与基准目录对比（内容归一化：去 BOM、统一 LF）。preserved 文件只做存在性校验。</summary>
public sealed class CompareReport
{
    public required List<string> Matches { get; init; }
    public required List<string> ContentDiffs { get; init; }
    public required List<string> Missing { get; init; }
    public required List<string> Extra { get; init; }
    public required List<string> ByteOnlyDifferences { get; init; }
    public bool IsClean => ContentDiffs.Count == 0 && Missing.Count == 0 && Extra.Count == 0;

    public string Summary =>
        $"  对比结果: {Matches.Count} 一致 / {ContentDiffs.Count} 内容差异 / {Missing.Count} 缺失 / {Extra.Count} 多余" +
        (IsClean ? " —— 全部一致 ✅" : " —— 存在差异 ❌");

    public static CompareReport Compare(string targetDir, FileSet expected, IReadOnlyList<string> preservedFiles, bool printDiffs)
    {
        var matches = new List<string>();
        var diffs = new List<string>();
        var missing = new List<string>();
        var byteOnly = new List<string>();

        var targetFiles = Directory.Exists(targetDir)
            ? Directory.EnumerateFiles(targetDir, "*.cs", SearchOption.AllDirectories)
              .Select(f => Path.GetRelativePath(targetDir, f).Replace('\\', '/'))
              .Where(p => !p.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
              .ToHashSet(StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in expected.Ordered)
        {
            var full = Path.Combine(targetDir, file.Path);
            if (!File.Exists(full))
            {
                missing.Add(file.Path);
                continue;
            }

            var actualRaw = File.ReadAllBytes(full);
            var actual = Normalize(actualRaw);
            if (actual == file.Content)
            {
                matches.Add(file.Path);
                if (!IsByteIdentical(actualRaw, file.Content, emitBom: true, crlf: true)) byteOnly.Add(file.Path);
            }
            else
            {
                diffs.Add(file.Path);
                var d = FirstDiff(file.Content, actual);
                if (printDiffs || diffs.Count <= 10)
                    Console.WriteLine($"  [diff] {file.Path}:{d.line}\n    期望: {d.expected}\n    实际: {d.actual}");
            }
        }

        foreach (var path in preservedFiles)
        {
            if (!File.Exists(Path.Combine(targetDir, path))) missing.Add(path + " (custom)");
            else matches.Add(path + " (custom, 未生成)");
        }

        var expectedSet = expected.Ordered.Select(f => f.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var path in expectedSet.Concat(preservedFiles)) targetFiles.Remove(path);
        var extra = targetFiles.OrderBy(p => p, StringComparer.Ordinal).ToList();

        return new CompareReport
        {
            Matches = matches,
            ContentDiffs = diffs,
            Missing = missing,
            Extra = extra,
            ByteOnlyDifferences = byteOnly,
        };
    }

    private static string Normalize(byte[] raw)
    {
        var hasBom = raw.Length >= 3 && raw[0] == 0xEF && raw[1] == 0xBB && raw[2] == 0xBF;
        var text = Encoding.UTF8.GetString(hasBom ? raw[3..] : raw);
        return text.Replace("\r\n", "\n");
    }

    private static bool IsByteIdentical(byte[] raw, string normalized, bool emitBom, bool crlf)
    {
        var hasBom = raw.Length >= 3 && raw[0] == 0xEF && raw[1] == 0xBB && raw[2] == 0xBF;
        var bytes = Encoding.UTF8.GetBytes(crlf ? normalized.Replace("\n", "\r\n") : normalized);
        var offset = hasBom ? 3 : 0;
        return (hasBom == emitBom) && raw.Length == bytes.Length + offset && raw.AsSpan(offset).SequenceEqual(bytes);
    }

    private static (int line, string expected, string actual) FirstDiff(string expected, string actual)
    {
        var eLines = expected.Split('\n');
        var aLines = actual.Split('\n');
        for (var i = 0; i < Math.Max(eLines.Length, aLines.Length); i++)
        {
            var e = i < eLines.Length ? eLines[i] : "<EOF>";
            var a = i < aLines.Length ? aLines[i] : "<EOF>";
            if (e != a) return (i + 1, e.TrimEnd(), a.TrimEnd());
        }
        return (0, "", "");
    }
}
