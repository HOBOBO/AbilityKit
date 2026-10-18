using System.Text.RegularExpressions;
using AbilityKit.Entitas.CodeGen.Configuration;

namespace AbilityKit.Entitas.CodeGen.Scanning;

/// <summary>
/// 类型索引：短名 → 全限定名集合。仅做存在性判定与消歧，
/// 最终取舍由 <see cref="TypeResolver"/> 按文件 using 顺序决定。
/// </summary>
public static class TypeIndex
{
    private static readonly Regex NamespaceRegex = new(@"^\s*namespace\s+(?<ns>[A-Za-z_][\w\.]*)", RegexOptions.Compiled);

    private static readonly Regex TypeDeclRegex = new(
        @"^\s*(?:\[[^\]]*\]\s*)*((?:(?:public|internal|private|protected|file)\s+)?(?:(?:sealed|abstract|partial|static|readonly)\s+)*(?:class|struct|interface|enum|record)\s+(?<name>[A-Za-z_]\w*))",
        RegexOptions.Compiled);

    public static Dictionary<string, HashSet<string>> Build(IEnumerable<string> roots, IEnumerable<string>? skipDirs = null)
    {
        var skip = (skipDirs ?? CodeGenConfig.Defaults.SkipDirs).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var index = new Dictionary<string, HashSet<string>>(4096, StringComparer.Ordinal);
        foreach (var root in roots)
        {
            if (!Directory.Exists(root)) continue;
            foreach (var file in EnumerateCsFiles(root, skip))
            {
                ScanFileForTypes(file, index);
            }
        }
        return index;
    }

    private static IEnumerable<string> EnumerateCsFiles(string root, HashSet<string> skip)
    {
        var pending = new Stack<string>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            var dir = pending.Pop();
            foreach (var sub in Directory.EnumerateDirectories(dir))
            {
                if (skip.Contains(Path.GetFileName(sub))) continue;
                pending.Push(sub);
            }
            foreach (var file in Directory.EnumerateFiles(dir, "*.cs"))
            {
                yield return file;
            }
        }
    }

    private static void ScanFileForTypes(string path, Dictionary<string, HashSet<string>> index)
    {
        string ns = "";
        foreach (var line in File.ReadLines(path))
        {
            var nsMatch = NamespaceRegex.Match(line);
            if (nsMatch.Success)
            {
                ns = nsMatch.Groups["ns"].Value;
                continue;
            }

            var typeMatch = TypeDeclRegex.Match(line);
            if (typeMatch.Success)
            {
                var name = typeMatch.Groups["name"].Value;
                var fqn = ns.Length == 0 ? name : ns + "." + name;
                if (!index.TryGetValue(name, out var set))
                {
                    index[name] = set = new HashSet<string>(StringComparer.Ordinal);
                }
                set.Add(fqn);
            }
        }
    }
}
