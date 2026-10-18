using System.Text.RegularExpressions;
using AbilityKit.Entitas.CodeGen.Diagnostics;

namespace AbilityKit.Entitas.CodeGen.Scanning;

/// <summary>把字段类型短名解析为全限定名，对齐 Jenny 产物的命名风格（无 global:: 前缀、基元用关键字）。</summary>
public static class TypeResolver
{
    private static readonly Dictionary<string, string> PrimitiveKeywords = new(StringComparer.Ordinal)
    {
        ["bool"] = "bool", ["byte"] = "byte", ["sbyte"] = "sbyte", ["char"] = "char",
        ["decimal"] = "decimal", ["double"] = "double", ["float"] = "float", ["int"] = "int",
        ["uint"] = "uint", ["long"] = "long", ["ulong"] = "ulong", ["short"] = "short",
        ["ushort"] = "ushort", ["object"] = "object", ["string"] = "string",
    };

    /// <summary>BCL 泛型容器——不在源码索引里（索引只扫 Unity/Packages 源码），按短名直映射。</summary>
    private static readonly Dictionary<string, string> BclTypes = new(StringComparer.Ordinal)
    {
        ["List"] = "System.Collections.Generic.List",
        ["Dictionary"] = "System.Collections.Generic.Dictionary",
        ["HashSet"] = "System.Collections.Generic.HashSet",
        ["Queue"] = "System.Collections.Generic.Queue",
        ["Stack"] = "System.Collections.Generic.Stack",
        ["KeyValuePair"] = "System.Collections.Generic.KeyValuePair",
        ["IReadOnlyList"] = "System.Collections.Generic.IReadOnlyList",
        ["IReadOnlyCollection"] = "System.Collections.Generic.IReadOnlyCollection",
        ["IEnumerable"] = "System.Collections.Generic.IEnumerable",
        ["IList"] = "System.Collections.Generic.IList",
        ["ICollection"] = "System.Collections.Generic.ICollection",
        ["ReadOnlyCollection"] = "System.Collections.ObjectModel.ReadOnlyCollection",
    };

    public static string Resolve(string typeText, string fileNamespace, List<string> usings,
        Dictionary<string, HashSet<string>> index, string sourceFile, int line)
    {
        typeText = Regex.Replace(typeText, @"\s+", "");
        return ResolveCore(typeText, fileNamespace, usings, index, sourceFile, line);
    }

    private static string ResolveCore(string typeText, string fileNamespace, List<string> usings,
        Dictionary<string, HashSet<string>> index, string sourceFile, int line)
    {
        if (PrimitiveKeywords.ContainsKey(typeText)) return typeText;

        // 数组：ActiveSkillRuntime[] → FQN[]
        if (typeText.EndsWith("[]", StringComparison.Ordinal))
        {
            var element = ResolveCore(typeText[..^2], fileNamespace, usings, index, sourceFile, line);
            return element + "[]";
        }

        // 泛型：List<BuffRuntime> → System.Collections.Generic.List<FQN>
        var lt = typeText.IndexOf('<');
        if (lt >= 0)
        {
            if (!typeText.EndsWith(">", StringComparison.Ordinal))
                throw Error(sourceFile, line, typeText, "泛型类型格式不认识");
            var container = typeText[..lt];
            var argsText = typeText[(lt + 1)..^1];
            var args = SplitTopLevel(argsText);
            var resolvedContainer = ResolveSimpleName(container, fileNamespace, usings, index, sourceFile, line, quiet: true);
            var resolvedArgs = args.Select(a => ResolveCore(a, fileNamespace, usings, index, sourceFile, line));
            return resolvedContainer + "<" + string.Join(", ", resolvedArgs) + ">";
        }

        if (typeText.Contains('.'))
        {
            // 已经是（部分）限定名：直接查索引验证。
            if (index.TryGetValue(PathTail(typeText), out var set) && set.Contains(typeText))
                return typeText;
            throw Error(sourceFile, line, typeText, "限定名未在类型索引中找到");
        }

        return ResolveSimpleName(typeText, fileNamespace, usings, index, sourceFile, line);
    }

    private static string ResolveSimpleName(string name, string fileNamespace, List<string> usings,
        Dictionary<string, HashSet<string>> index, string sourceFile, int line, bool quiet = false)
    {
        if (BclTypes.TryGetValue(name, out var bcl)) return bcl;

        // 候选顺序与 C# 名称解析一致：本命名空间 → using 顺序 → 全局命名空间。
        var candidates = new List<string>();
        if (fileNamespace.Length > 0) candidates.Add(fileNamespace);
        candidates.AddRange(usings);
        candidates.Add("");

        foreach (var ns in candidates)
        {
            var fqn = ns.Length == 0 ? name : ns + "." + name;
            if (index.TryGetValue(name, out var set) && set.Contains(fqn))
                return fqn;
        }

        // 宽限路径：using 全部未命中但短名全局唯一——采用并提示（新组件作者忘了 using 也能过）。
        if (index.TryGetValue(name, out var unique) && unique.Count == 1)
        {
            if (!quiet) Console.WriteLine($"[warn] {Path.GetFileName(sourceFile)}:{line} 类型 {name} 未通过 using 命中，按全局唯一短名解析为 {unique.First()}");
            return unique.First();
        }

        throw Error(sourceFile, line, name, unique is { Count: > 1 }
            ? "短名歧义（多个命名空间同名），需在组件文件加精确 using"
            : "未在类型索引中找到（检查配置的 typeIndex.roots）");
    }

    private static List<string> SplitTopLevel(string argsText)
    {
        var parts = new List<string>();
        var depth = 0;
        var start = 0;
        for (var i = 0; i < argsText.Length; i++)
        {
            switch (argsText[i])
            {
                case '<': depth++; break;
                case '>': depth--; break;
                case ',' when depth == 0:
                    parts.Add(argsText[start..i]);
                    start = i + 1;
                    break;
            }
        }
        parts.Add(argsText[start..]);
        return parts;
    }

    private static string PathTail(string dotted) => dotted[(dotted.LastIndexOf('.') + 1)..];

    private static CodeGenException Error(string file, int line, string type, string reason) =>
        new($"类型解析失败（{file}:{line}）: {type} —— {reason}", ExitCodes.SourceValidation);
}
