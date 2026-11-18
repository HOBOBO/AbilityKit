using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

/*
 * 使用语法树检查自有框架源码，避免把字符串中的注释示例识别为真实注释。
 * 检查是只读的；迁移期间可先输出清单，再按包启用完整门禁。
 */
if (args.Length == 1 && args[0] == "--self-test")
{
    SelfTest();
    return 0;
}

if (args.Length < 1 || !Directory.Exists(args[0]))
{
    Console.Error.WriteLine("用法：dotnet run --project tools/AbilityKit.PackageQuality -- <源码目录> [--inventory <JSON路径>]");
    return 2;
}

var root = Path.GetFullPath(args[0]);
var inventory = new List<object>();
var failures = new List<string>();
var files = Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
    .Where(path => !Path.GetRelativePath(root, path).Split(Path.DirectorySeparatorChar)
        .Any(part => part is "bin" or "obj" or "Generated" or "node_modules"))
    .Order(StringComparer.Ordinal).ToArray();

foreach (var file in files)
{
    var source = File.ReadAllText(file);
    var relative = Path.GetRelativePath(root, file).Replace('\\', '/');
    var variants = new[] { Array.Empty<string>(), new[] { "UNITY_EDITOR", "UNITY_5_3_OR_NEWER", "UNITY_2022_3_OR_NEWER" } };
    var seen = new HashSet<string>(StringComparer.Ordinal);
    foreach (var symbols in variants)
    {
        var tree = CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(
            documentationMode: DocumentationMode.Diagnose, preprocessorSymbols: symbols));
        var syntax = tree.GetRoot();
        foreach (var diagnostic in tree.GetDiagnostics().Where(item => item.Severity == DiagnosticSeverity.Error))
        {
            failures.Add($"{relative}: {diagnostic}");
        }

        foreach (var member in syntax.DescendantNodes().Where(IsDocumentable))
        {
            if (!seen.Add($"member:{member.SpanStart}")) continue;
            var docs = member.GetLeadingTrivia().FirstOrDefault(trivia => trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia));
            var line = tree.GetLineSpan(member.Span).StartLinePosition.Line + 1;
            inventory.Add(new
            {
                file = relative,
                line,
                start = member.SpanStart,
                kind = member.Kind().ToString(),
                name = MemberName(member),
                owner = string.Join(".", member.Ancestors().OfType<TypeDeclarationSyntax>().Reverse().Select(type => type.Identifier.ValueText)),
                signature = member.ToString().Split('\n')[0].Trim(),
                required = RequiresDocumentation(member),
                docs = docs.ToFullString(),
                docStart = docs.RawKind == 0 ? -1 : docs.FullSpan.Start,
                docLength = docs.FullSpan.Length
            });
            if (RequiresDocumentation(member) && docs.RawKind == 0)
            {
                failures.Add($"{relative}:{line}: {MemberName(member)} 缺少中文多行 XML 注释。");
            }
        }

        foreach (var trivia in syntax.DescendantTrivia())
        {
            if (!IsComment(trivia) || !seen.Add($"comment:{trivia.SpanStart}")) continue;
            var error = CheckComment(trivia);
            if (error != null)
            {
                var line = tree.GetLineSpan(trivia.Span).StartLinePosition.Line + 1;
                failures.Add($"{relative}:{line}: {error}");
            }
        }
    }
}

var inventoryIndex = Array.IndexOf(args, "--inventory");
if (inventoryIndex >= 0)
{
    if (inventoryIndex + 1 >= args.Length) return 2;
    File.WriteAllText(args[inventoryIndex + 1], JsonSerializer.Serialize(inventory, new JsonSerializerOptions { WriteIndented = true }));
    Console.WriteLine($"已盘点 {files.Length} 个源码文件，{inventory.Count} 个声明，{failures.Distinct().Count()} 项注释问题。");
    return 0;
}

foreach (var failure in failures.Distinct()) Console.Error.WriteLine(failure);
Console.WriteLine($"中文多行注释检查：{files.Length} 个源码文件，{failures.Distinct().Count()} 项问题。");
return failures.Count == 0 ? 0 : 1;

/*
 * 可见性按整个包含类型链判断，私有嵌套实现的公共成员不是包的公开契约。
 * 类型自身仍应说明职责；接口成员和枚举值使用其隐式公开可见性。
 */
static bool IsDocumentable(SyntaxNode node) => node is BaseTypeDeclarationSyntax or DelegateDeclarationSyntax
    or BaseMethodDeclarationSyntax or BasePropertyDeclarationSyntax or BaseFieldDeclarationSyntax or EnumMemberDeclarationSyntax;

static bool RequiresDocumentation(SyntaxNode node)
{
    if (node is BaseTypeDeclarationSyntax or DelegateDeclarationSyntax) return true;
    foreach (var ancestor in node.Ancestors().OfType<BaseTypeDeclarationSyntax>())
    {
        if (!ancestor.Modifiers.Any(token => token.IsKind(SyntaxKind.PublicKeyword) || token.IsKind(SyntaxKind.ProtectedKeyword))) return false;
    }
    if (node is EnumMemberDeclarationSyntax || node.Parent is InterfaceDeclarationSyntax) return true;
    return node is MemberDeclarationSyntax member && member.Modifiers.Any(token =>
        token.IsKind(SyntaxKind.PublicKeyword) || token.IsKind(SyntaxKind.ProtectedKeyword));
}

static string MemberName(SyntaxNode node) => node switch
{
    BaseTypeDeclarationSyntax type => type.Identifier.ValueText,
    DelegateDeclarationSyntax declaration => declaration.Identifier.ValueText,
    MethodDeclarationSyntax method => method.Identifier.ValueText,
    ConstructorDeclarationSyntax constructor => constructor.Identifier.ValueText,
    DestructorDeclarationSyntax destructor => "~" + destructor.Identifier.ValueText,
    PropertyDeclarationSyntax property => property.Identifier.ValueText,
    EventDeclarationSyntax declaration => declaration.Identifier.ValueText,
    IndexerDeclarationSyntax => "this",
    BaseFieldDeclarationSyntax field => string.Join(",", field.Declaration.Variables.Select(variable => variable.Identifier.ValueText)),
    OperatorDeclarationSyntax declaration => "operator " + declaration.OperatorToken.Text,
    ConversionOperatorDeclarationSyntax declaration => "operator " + declaration.Type,
    EnumMemberDeclarationSyntax declaration => declaration.Identifier.ValueText,
    _ => node.Kind().ToString()
};

static bool IsComment(SyntaxTrivia trivia) => trivia.IsKind(SyntaxKind.SingleLineCommentTrivia)
    || trivia.IsKind(SyntaxKind.MultiLineCommentTrivia) || trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia)
    || trivia.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia);

static string? CheckComment(SyntaxTrivia trivia)
{
    var text = trivia.ToFullString();
    if (!Regex.IsMatch(text, @"[\u3400-\u9fff]")) return "注释应使用中文说明。";
    if (trivia.IsKind(SyntaxKind.SingleLineCommentTrivia)) return "实现说明应使用多行块注释。";
    if (text.TrimEnd().Split('\n').Length < 3) return "注释应展开为多行。";
    if (Regex.IsMatch(text, @"<summary>[^\r\n]*</summary>")) return "summary 标签和正文应分行书写。";
    return null;
}

/*
 * 这些夹具验证检查器本身的判定边界，尤其防止私有实现和字符串内容造成误报。
 * 它们不替代被检查包的编译、公开 API 兼容检查或行为测试。
 */
static void SelfTest()
{
    var cases = new (string Source, bool Pass)[]
    {
        ("/// <summary>\n/// 中文说明。\n/// </summary>\npublic class C {}", true),
        ("/// <summary>中文说明。</summary>\npublic class C {}", false),
        ("/// <summary>\n/// English only.\n/// </summary>\npublic class C {}", false),
        ("// 中文说明\npublic class C {}", false),
        ("/*\n * 中文说明。\n */\npublic class C {}", true),
        ("public class C { private string x = \"// not a comment\"; }", true),
    };
    foreach (var item in cases)
    {
        var root = CSharpSyntaxTree.ParseText(item.Source).GetRoot();
        var pass = root.DescendantTrivia().Where(IsComment).All(trivia => CheckComment(trivia) == null);
        if (pass != item.Pass) throw new InvalidOperationException("注释样式夹具失败：" + item.Source);
    }
    var visibility = CSharpSyntaxTree.ParseText("public class C { private class Hidden { public void Run() {} } } public interface I { void Run(); } public enum E { Value }").GetRoot();
    var methods = visibility.DescendantNodes().OfType<MethodDeclarationSyntax>().ToArray();
    if (RequiresDocumentation(methods[0]) || !RequiresDocumentation(methods[1])
        || !RequiresDocumentation(visibility.DescendantNodes().OfType<EnumMemberDeclarationSyntax>().Single()))
        throw new InvalidOperationException("声明可见性夹具失败。");
    Console.WriteLine("注释检查器自检通过：6 个词法样式场景，3 个声明可见性场景。");
}
