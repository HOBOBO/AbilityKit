using System.Text.RegularExpressions;
using AbilityKit.Entitas.CodeGen.Configuration;
using AbilityKit.Entitas.CodeGen.Diagnostics;

namespace AbilityKit.Entitas.CodeGen.Scanning;

/// <summary>
/// 零依赖正则扫描器。语法覆盖面按 Domain/Components 的实际书写风格裁剪；
/// 遇到不认识的语法一律报错而非静默跳过——生成器宁可失败，不可漏生成。
/// </summary>
public sealed class Scanner : IComponentSourceScanner
{
    public bool ProvidesResolvedFieldTypes => false;

    private static readonly Regex ClassDeclRegex = new(
        @"^\s*(?:public\s+|internal\s+)?(?<modifiers>(?:sealed\s+|static\s+|abstract\s+|partial\s+)*)class\s+(?<name>[A-Za-z_]\w*)\s*:\s*IComponent\b",
        RegexOptions.Compiled);

    private static readonly Regex AttributeLineRegex = new(@"^\s*\[(?<attr>[^\]]+)\]\s*$", RegexOptions.Compiled);

    private static readonly Regex FieldLineRegex = new(
        @"^\s*public\s+(?!const\b|static\b|readonly\b)(?<type>[A-Za-z_][\w<>,\.\[\]\s]*?)\s+(?<name>[A-Za-z_]\w*)\s*(?:=[^;]*)?;",
        RegexOptions.Compiled);

    private static readonly Regex AccessorLineRegex = new(
        @"^\s*(?:public|private|internal|protected)\s+[\w<>,\.\[\]\s]+?\s+[A-Za-z_]\w*\s*\{", RegexOptions.Compiled);

    private static readonly Regex FieldPrefixRegex = new(
        @"^\s*(?:public|private|internal|protected)\s", RegexOptions.Compiled);

    private static readonly Regex NamespaceRegex = new(@"^\s*namespace\s+(?<ns>[A-Za-z_][\w\.]*)", RegexOptions.Compiled);
    private static readonly Regex UsingRegex = new(@"^\s*using\s+(?:static\s+)?(?<alias>[A-Za-z_]\w*\s*=\s*)?(?<ns>[A-Za-z_][\w\.]*)\s*;", RegexOptions.Compiled);

    public List<RawComponent> Scan(IEnumerable<string> sourceDirs)
    {
        var result = new List<RawComponent>();
        foreach (var dir in sourceDirs)
        {
            if (!Directory.Exists(dir))
                throw new CodeGenException($"组件源目录不存在: {dir}", ExitCodes.UsageOrConfig);
            foreach (var file in Directory.EnumerateFiles(dir, "*.cs", SearchOption.AllDirectories))
            {
                ScanComponentFile(file, result);
            }
        }
        return result;
    }

    public sealed class RawComponent(string componentName, string @class, string ns, List<string> usings,
        List<RawField> fields, HashSet<string> attributes, string sourceFile, bool sealedClass)
    {
        public string ComponentName { get; } = componentName;
        public string Class { get; } = @class;
        public string Namespace { get; } = ns;
        public List<string> Usings { get; } = usings;
        public List<RawField> Fields { get; } = fields;
        public HashSet<string> Attributes { get; } = attributes;
        public string SourceFile { get; } = sourceFile;
        public bool SealedClass { get; } = sealedClass;
    }

    public sealed class RawField(string typeText, string name, string sourceFile, int line)
    {
        public string TypeText { get; } = typeText;
        public string Name { get; } = name;
        public string SourceFile { get; } = sourceFile;
        public int Line { get; } = line;
    }

    /// <summary>
    /// 非组件的普通类型（嵌套数据类/枚举），供序列化生成器展开字段。
    /// kind: "class" | "struct" | "enum"。
    /// </summary>
    public sealed class RawPlainType(string name, string ns, string kind, List<RawField> fields, List<string> usings, string sourceFile)
    {
        public string Name { get; } = name;
        public string Namespace { get; } = ns;
        public string Kind { get; } = kind;
        public List<RawField> Fields { get; } = fields;
        public List<string> Usings { get; } = usings;
        public string SourceFile { get; } = sourceFile;
    }

    private static readonly Regex PlainTypeDeclRegex = new(
        @"^\s*(?:public\s+|internal\s+)?(?<modifiers>(?:sealed\s+|static\s+|abstract\s+|partial\s+|readonly\s+)*)?(?<kind>class|struct|enum)\s+(?<name>[A-Za-z_]\w*)",
        RegexOptions.Compiled);

    /// <summary>扫描普通类型（跳过 IComponent 类——那是组件扫描的事）。字段宽松提取。</summary>
    public List<RawPlainType> ScanPlainTypes(IEnumerable<string> sourceDirs)
    {
        var result = new List<RawPlainType>();
        foreach (var dir in sourceDirs)
        {
            if (!Directory.Exists(dir))
                throw new CodeGenException($"源目录不存在: {dir}", ExitCodes.UsageOrConfig);
            foreach (var file in Directory.EnumerateFiles(dir, "*.cs", SearchOption.AllDirectories))
            {
                var lines = File.ReadAllLines(file);
                string ns = "";
                var usings = new List<string>();
                for (var i = 0; i < lines.Length; i++)
                {
                    var nsMatch = NamespaceRegex.Match(lines[i]);
                    if (nsMatch.Success) { ns = nsMatch.Groups["ns"].Value; continue; }

                    var usingMatch = UsingRegex.Match(lines[i]);
                    if (usingMatch.Success)
                    {
                        if (!usingMatch.Groups["alias"].Success) usings.Add(usingMatch.Groups["ns"].Value);
                        continue;
                    }

                    var match = PlainTypeDeclRegex.Match(lines[i]);
                    if (!match.Success) continue;
                    var name = match.Groups["name"].Value;
                    if (name.EndsWith("Component", StringComparison.Ordinal)) continue; // 组件由 Scan 处理
                    if (result.Any(t => t.Name == name && t.Namespace == ns)) continue; // 同文件嵌套/重复声明取首个

                    var kind = match.Groups["kind"].Value;
                    var fields = kind == "enum"
                        ? new List<RawField>()
                        : ScanClassBody(lines, i, file, name, strict: false);
                    result.Add(new RawPlainType(name, ns, kind, fields, usings.ToList(), file));
                }
            }
        }
        return result;
    }

    private static void ScanComponentFile(string path, List<RawComponent> result)
    {
        var lines = File.ReadAllLines(path);
        string ns = "";
        var usings = new List<string>();

        for (var i = 0; i < lines.Length; i++)
        {
            var nsMatch = NamespaceRegex.Match(lines[i]);
            if (nsMatch.Success)
            {
                ns = nsMatch.Groups["ns"].Value;
                continue;
            }

            var usingMatch = UsingRegex.Match(lines[i]);
            if (usingMatch.Success)
            {
                if (usingMatch.Groups["alias"].Success)
                    throw new CodeGenException($"不支持 using 别名（{path}:{i + 1}）：{lines[i].Trim()}", ExitCodes.SourceValidation);
                usings.Add(usingMatch.Groups["ns"].Value);
                continue;
            }

            var classMatch = ClassDeclRegex.Match(lines[i]);
            if (!classMatch.Success) continue;

            var className = classMatch.Groups["name"].Value;
            if (!className.EndsWith("Component", StringComparison.Ordinal))
                throw new CodeGenException(
                    $"IComponent 类名必须以 Component 结尾（{path}:{i + 1}）：{className}", ExitCodes.SourceValidation);

            var modifiers = classMatch.Groups["modifiers"].Value;
            var isSealed = Regex.IsMatch(modifiers, @"\bsealed\b");

            // 向上收集连续 attribute 行（[Actor] / [PrimaryEntityIndex] 等）。
            var attributes = new HashSet<string>(StringComparer.Ordinal);
            for (var a = i - 1; a >= 0; a--)
            {
                var attrMatch = AttributeLineRegex.Match(lines[a]);
                if (!attrMatch.Success) break;
                attributes.Add(attrMatch.Groups["attr"].Value.Trim());
            }

            var fields = ScanClassBody(lines, i, path, className);
            result.Add(new RawComponent(
                componentName: className[..^"Component".Length],
                @class: className,
                ns, usings.ToList(), fields, attributes, path, isSealed));
        }
    }

    /// <summary>类体扫描 + 源校验。strict=组件（违规抛异常，绝不静默跳过）；非 strict=普通类型（只取字段，其余行跳过）。</summary>
    private static List<RawField> ScanClassBody(string[] lines, int classLine, string path, string className, bool strict = true)
    {
        var fields = new List<RawField>();
        var depth = 0;
        var started = false;

        for (var i = classLine; i < lines.Length; i++)
        {
            foreach (var ch in lines[i])
            {
                if (ch == '{') { depth++; started = true; }
                else if (ch == '}') depth--;
            }

            if (started && depth == 0) break;
            if (i == classLine) continue;

            var line = StripComment(lines[i]);
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith("[")) continue; // 空行/字段上的 attribute

            if (AccessorLineRegex.IsMatch(line))
            {
                if (!strict) continue;
                throw new CodeGenException(
                    $"组件不允许属性（Entitas 组件用公共字段，属性会被静默漏生成）：{path}:{i + 1}: {trimmed}", ExitCodes.SourceValidation);
            }

            if (trimmed.Contains('('))
            {
                if (!strict) continue;
                throw new CodeGenException(
                    $"组件不允许方法/构造（{path}:{i + 1}）: {trimmed} —— 自定义行为放 partial 手写文件并在 pin 标 custom", ExitCodes.SourceValidation);
            }

            var fieldMatch = FieldLineRegex.Match(line);
            if (fieldMatch.Success)
            {
                fields.Add(new RawField(fieldMatch.Groups["type"].Value.Trim(), fieldMatch.Groups["name"].Value, path, i + 1));
                continue;
            }

            if (FieldPrefixRegex.IsMatch(line) && trimmed.Contains(';'))
            {
                if (!strict) continue;
                throw new CodeGenException(
                    $"无法解析的字段行（非 public / 多声明 / 修饰符不支持）：{path}:{i + 1}: {trimmed}", ExitCodes.SourceValidation);
            }
        }

        return fields;
    }

    private static string StripComment(string line)
    {
        var idx = line.IndexOf("//", StringComparison.Ordinal);
        return idx >= 0 ? line[..idx] : line;
    }
}
