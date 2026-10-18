using AbilityKit.Entitas.CodeGen.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AbilityKit.Entitas.CodeGen.Scanning;

/// <summary>
/// 语义级组件扫描器（Roslyn）。FQN 由编译器符号解析——正则扫描器的
/// "using 顺序 + 类型索引"启发式（别名不支持、歧义靠唯一短名兜底）在这里不复存在。
///
/// 编译闭包策略：组件源 + 组件文件 using 到的命名空间的声明目录源 + BCL 引用。
/// 闭包不完整时 Roslyn 仍能对已声明类型给出符号；解析不到的类型会以 error type 形式
/// 出现在 FQN 中，由生成/基线阶段暴露。
/// </summary>
public sealed class RoslynComponentScanner : IComponentSourceScanner
{
    private static readonly SymbolDisplayFormat FqnFormat = new(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes);

    private static readonly string[] SkipDirs = { "obj", "bin", "node_modules", "Library", ".git", "TestResults", "Bee", "Logs", "DotNet~", "Samples", "Samples~" };

    public bool ProvidesResolvedFieldTypes => true;

    public List<Scanner.RawComponent> Scan(IEnumerable<string> sourceDirs)
    {
        var (components, _) = ScanInternal(sourceDirs.ToList());
        return components;
    }

    public List<Scanner.RawPlainType> ScanPlainTypes(IEnumerable<string> sourceDirs)
    {
        var (_, plainTypes) = ScanInternal(sourceDirs.ToList());
        return plainTypes;
    }

    private (List<Scanner.RawComponent> Components, List<Scanner.RawPlainType> PlainTypes) ScanInternal(List<string> sourceDirs)
    {
        foreach (var dir in sourceDirs)
        {
            if (!Directory.Exists(dir))
                throw new CodeGenException($"组件源目录不存在: {dir}", ExitCodes.UsageOrConfig);
        }

        var componentFiles = sourceDirs
            .SelectMany(d => Directory.EnumerateFiles(d, "*.cs", SearchOption.AllDirectories))
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var closureFiles = BuildClosure(componentFiles, sourceDirs)
            .SelectMany(SafeEnumerate)
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Except(componentFiles, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var trees = componentFiles.Concat(closureFiles)
            .Select(f => CSharpSyntaxTree.ParseText(File.ReadAllText(f), path: f))
            .ToList();
        var compilation = CSharpCompilation.Create(
            "EntitasCodeGenScan",
            trees,
            new[]
            {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(System.Collections.Generic.List<>).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(System.Linq.Enumerable).Assembly.Location),
            },
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));

        var componentTreePaths = componentFiles.ToHashSet(StringComparer.OrdinalIgnoreCase);

        var components = new List<Scanner.RawComponent>();
        var plainTypes = new List<Scanner.RawPlainType>();

        foreach (var tree in compilation.SyntaxTrees)
        {
            var isComponentSource = componentTreePaths.Contains(Path.GetFullPath(tree.FilePath));
            var root = tree.GetCompilationUnitRoot();
            var model = compilation.GetSemanticModel(tree);

            foreach (var typeDecl in root.DescendantNodes().OfType<BaseTypeDeclarationSyntax>())
            {
                if (typeDecl.Parent is BaseTypeDeclarationSyntax) continue; // 嵌套类型不扫（与 regex 扫描器约定一致）
                if (!isComponentSource) continue;                        // 组件与普通类型只认组件源目录

                var symbol = model.GetDeclaredSymbol(typeDecl);
                if (symbol is null) continue;
                var ns = symbol.ContainingNamespace.IsGlobalNamespace ? "" : symbol.ContainingNamespace.ToDisplayString();
                var name = symbol.Name;
                var line = typeDecl.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                var isComponent = IsComponentDeclaration(typeDecl);

                if (isComponent && !name.EndsWith("Component", StringComparison.Ordinal))
                {
                    throw new CodeGenException(
                        $"IComponent 类名必须以 Component 结尾（{tree.FilePath}:{line}）：{name}",
                        ExitCodes.SourceValidation);
                }

                var fields = ExtractFields(typeDecl, model, strict: isComponent, tree.FilePath);

                if (isComponent)
                {
                    components.Add(new Scanner.RawComponent(
                        componentName: name[..^"Component".Length],
                        @class: name,
                        ns,
                        UsingsOf(root),
                        fields,
                        AttributesOf(typeDecl),
                        tree.FilePath,
                        sealedClass: typeDecl.Modifiers.Any(SyntaxKind.SealedKeyword)));
                }
                else
                {
                    var kind = typeDecl.Kind() switch
                    {
                        SyntaxKind.StructDeclaration => "struct",
                        SyntaxKind.EnumDeclaration => "enum",
                        SyntaxKind.InterfaceDeclaration => "interface",
                        _ => "class",
                    };
                    plainTypes.Add(new Scanner.RawPlainType(name, ns, kind, fields, UsingsOf(root), tree.FilePath));
                }
            }
        }

        return (components, plainTypes);
    }

    private static bool IsComponentDeclaration(BaseTypeDeclarationSyntax typeDecl) =>
        typeDecl.BaseList?.Types.Any(t => t.Type is SimpleNameSyntax n && n.Identifier.ValueText == "IComponent") == true;

    private static List<Scanner.RawField> ExtractFields(
        BaseTypeDeclarationSyntax typeDecl, SemanticModel model, bool strict, string path)
    {
        var fields = new List<Scanner.RawField>();

        foreach (var field in typeDecl.ChildNodes().OfType<FieldDeclarationSyntax>())
        {
            var line = field.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
            var isPublic = field.Modifiers.Any(SyntaxKind.PublicKeyword);
            var isStaticOrConst = field.Modifiers.Any(SyntaxKind.ConstKeyword) || field.Modifiers.Any(SyntaxKind.StaticKeyword);

            if (strict && !isPublic)
            {
                var declared = string.Join(", ", field.Declaration.Variables.Select(v => v.Identifier.ValueText));
                throw new CodeGenException(
                    $"无法解析的字段行（非 public / 修饰符不支持）：{path}:{line}: {declared}", ExitCodes.SourceValidation);
            }
            if (strict && isStaticOrConst)
            {
                throw new CodeGenException($"组件不允许 const/static 字段：{path}:{line}", ExitCodes.SourceValidation);
            }
            if (!isPublic || isStaticOrConst) continue; // 普通类型宽松：只取公共实例字段

            foreach (var variable in field.Declaration.Variables)
            {
                var declaredSymbol = model.GetDeclaredSymbol(variable) as IFieldSymbol;
                var typeFqn = declaredSymbol is not null
                    ? declaredSymbol.Type.ToDisplayString(FqnFormat)
                    : field.Declaration.Type.ToString();
                fields.Add(new Scanner.RawField(typeFqn, variable.Identifier.ValueText, path, line));
            }
        }

        return fields;
    }

    private static HashSet<string> AttributesOf(BaseTypeDeclarationSyntax typeDecl)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        foreach (var list in typeDecl.AttributeLists)
        {
            foreach (var attr in list.Attributes)
            {
                var name = attr.Name.ToString();
                if (name.EndsWith("Attribute", StringComparison.Ordinal)) name = name[..^"Attribute".Length];
                result.Add(name);
            }
        }
        return result;
    }

    private static List<string> UsingsOf(CompilationUnitSyntax root) =>
        root.Usings
            .Where(u => u.Alias is null && u.GlobalKeyword.IsKind(SyntaxKind.None) && u.StaticKeyword.IsKind(SyntaxKind.None))
            .Select(u => u.Name.ToString())
            .ToList();

    /// <summary>
    /// 闭包策略：收集组件字段类型语法里出现的短名，在候选根（限定在 Unity/Packages 子树）一遍
    /// 头扫描定位"声明了这些短名的目录"，闭包=这些目录。FQN 只需要类型自身的符号，
    /// 不需要它的传递依赖——闭包因此保持极小（通常几十个文件），整个扫描秒级。
    /// </summary>
    private static HashSet<string> BuildClosure(List<string> componentFiles, List<string> sourceDirs)
    {
        // 1) 从组件文件语法提取字段类型短名（含泛型参数与数组元素）。
        var wantedNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var file in componentFiles)
        {
            var root = CSharpSyntaxTree.ParseText(File.ReadAllText(file), path: file).GetCompilationUnitRoot();
            foreach (var typeSyntax in root.DescendantNodes().OfType<TypeDeclarationSyntax>())
            {
                foreach (var field in typeSyntax.Members.OfType<FieldDeclarationSyntax>())
                {
                    CollectNames(field.Declaration.Type, wantedNames);
                }
            }
        }

        // 2) 候选根：sourceDirs 的祖先，止于 Packages 根（不爬出包体系）。
        var candidateRoots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var dir in sourceDirs)
        {
            var d = Path.GetFullPath(dir);
            while (d is not null)
            {
                var parent = Path.GetDirectoryName(d);
                var reachedPackages = parent is not null &&
                    string.Equals(Path.GetFileName(parent), "Packages", StringComparison.OrdinalIgnoreCase);
                if (reachedPackages)
                {
                    candidateRoots.Add(parent); // Unity/Packages 根，到此为止
                    break;
                }
                candidateRoots.Add(d);
                d = parent;
            }
        }

        // 3) 一遍头扫描：声明了任一 wanted 短名的目录进闭包。
        var closure = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var queue = new Queue<string>(candidateRoots.Where(Directory.Exists).OrderBy(p => p, StringComparer.OrdinalIgnoreCase));
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        while (queue.Count > 0)
        {
            var dir = queue.Dequeue();
            if (!visited.Add(dir)) continue;
            foreach (var sub in Directory.EnumerateDirectories(dir))
            {
                if (SkipDirs.Contains(Path.GetFileName(sub))) continue;
                queue.Enqueue(sub);
            }
            foreach (var file in Directory.EnumerateFiles(dir, "*.cs"))
            {
                if (DeclaresAnyWanted(file, wantedNames))
                {
                    closure.Add(dir);
                    break;
                }
            }
        }
        return closure;
    }

    private static void CollectNames(TypeSyntax type, HashSet<string> names)
    {
        switch (type)
        {
            case PredefinedTypeSyntax:
                return; // 基元关键字
            case ArrayTypeSyntax array:
                CollectNames(array.ElementType, names);
                return;
            case GenericNameSyntax generic:
                names.Add(generic.Identifier.ValueText);
                foreach (var arg in generic.TypeArgumentList.Arguments) CollectNames(arg, names);
                return;
            case QualifiedNameSyntax qualified:
                CollectNames(qualified.Right, names);
                return;
            case IdentifierNameSyntax identifier:
                names.Add(identifier.Identifier.ValueText);
                return;
        }
    }

    private static bool DeclaresAnyWanted(string file, HashSet<string> wantedNames)
    {
        // 全文件扫描（不做头部窗口截断）：同文件多类型、声明在文件深处（>80 行）都覆盖。
        foreach (var line in File.ReadLines(file))
        {
            var match = System.Text.RegularExpressions.Regex.Match(
                line, @"\s(public|internal)\s+(?:sealed |static |abstract |partial |readonly )*(class|struct|interface|enum|record)\s+([A-Za-z_]\w*)");
            if (match.Success && wantedNames.Contains(match.Groups[3].Value)) return true;
        }
        return false;
    }

    private static IEnumerable<string> SafeEnumerate(string dir) =>
        Directory.EnumerateFiles(dir, "*.cs", SearchOption.AllDirectories);
}
