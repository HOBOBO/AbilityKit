/// <summary>
/// 文件名称: CodeGenUtilities.cs
/// 
/// 功能描述: 定义代码生成所需的辅助工具，包括类型名处理、语法节点处理和缩进工具。
/// 
/// 创建日期: 2026-04-06
/// 修改日期: 2026-04-06
/// </summary>

using System;
using System.Linq;
using System.Text;

namespace AbilityKit.CodeGen.Utilities
{
/// <summary>
/// 类型名处理工具。
/// </summary>
public static class TypeNameHelper
{
    /// <summary>获取带泛型参数的类型全名</summary>
    public static string GetFullTypeName(string ns, string name, int genericArgCount = 0)
    {
        if (genericArgCount <= 0) return $"{ns}.{name}";
        var args = string.Join(", ", new string[genericArgCount]);
        return $"{ns}.{name}`{genericArgCount}<{args}>";
    }

    /// <summary>获取 C# 关键字或原始类型名</summary>
    public static string GetKeywordOrName(string typeName)
    {
        return typeName switch
        {
            "System.String" => "string",
            "System.Int32" => "int",
            "System.Int64" => "long",
            "System.Boolean" => "bool",
            "System.Double" => "double",
            "System.Single" => "float",
            "System.Decimal" => "decimal",
            "System.Char" => "char",
            "System.Byte" => "byte",
            "System.SByte" => "sbyte",
            "System.Int16" => "short",
            "System.UInt16" => "ushort",
            "System.UInt32" => "uint",
            "System.UInt64" => "ulong",
            "System.Object" => "object",
            "System.Void" => "void",
            _ => typeName
        };
    }

    /// <summary>将值类型转为可空类型</summary>
    public static string MakeNullable(string typeName)
    {
        if (IsValueType(typeName) && !typeName.EndsWith("?"))
        {
            return $"{typeName}?";
        }
        return typeName;
    }

    /// <summary>判断类型名是否为值类型</summary>
    public static bool IsValueType(string typeName)
    {
        return typeName is
            "System.Int32" or
            "System.Int64" or
            "System.Boolean" or
            "System.Double" or
            "System.Single" or
            "System.Decimal" or
            "System.Char" or
            "System.Byte" or
            "System.SByte" or
            "System.Int16" or
            "System.UInt16" or
            "System.UInt32" or
            "System.UInt64" or
            "System.DateTime" or
            "System.Guid" or
            "System.TimeSpan";
    }

    /// <summary>获取类型的数组表示</summary>
    public static string MakeArray(string typeName, int rank = 1)
    {
        if (rank == 1) return $"{typeName}[]";
        var brackets = new StringBuilder();
        for (int i = 0; i < rank; i++)
        {
            brackets.Append("[]");
        }
        return $"{typeName}{brackets}";
    }

    /// <summary>将命名空间转换为文件路径</summary>
    public static string NamespaceToPath(string ns, string rootNamespace = null)
    {
        if (string.IsNullOrEmpty(ns)) return string.Empty;

        if (!string.IsNullOrEmpty(rootNamespace) && ns.StartsWith(rootNamespace))
        {
            ns = ns.Substring(rootNamespace.Length).TrimStart('.');
        }

        return ns.Replace('.', '/');
    }
}

/// <summary>
/// 语法节点处理工具。
/// </summary>
public static class SyntaxNodeHelper
{
    /// <summary>获取安全的标识符名称</summary>
    public static string GetSafeIdentifier(string name)
    {
        if (string.IsNullOrEmpty(name)) return "_";

        var sb = new StringBuilder(name.Length);
        bool first = true;

        foreach (var c in name)
        {
            if (char.IsLetter(c) || (!first && char.IsDigit(c)))
            {
                sb.Append(c);
            }
            else if (c == '_')
            {
                sb.Append('_');
            }
            else
            {
                sb.Append('_');
            }
            first = false;
        }

        var result = sb.ToString();
        if (char.IsDigit(result[0]))
        {
            result = "_" + result;
        }

        return result;
    }

    /// <summary>将名称转换为 PascalCase</summary>
    public static string ToPascalCase(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;

        var words = name.Split(new[] { '_', ' ' }, StringSplitOptions.RemoveEmptyEntries);
        var sb = new StringBuilder();

        foreach (var word in words)
        {
            if (word.Length > 0)
            {
                sb.Append(char.ToUpper(word[0]));
                if (word.Length > 1)
                {
                    sb.Append(word.Substring(1).ToLower());
                }
            }
        }

        return sb.ToString();
    }

    /// <summary>将名称转换为 camelCase</summary>
    public static string ToCamelCase(string name)
    {
        var pascal = ToPascalCase(name);
        if (pascal.Length > 0)
        {
            return char.ToLower(pascal[0]) + pascal.Substring(1);
        }
        return pascal;
    }

    /// <summary>将名称转换为 SCREAMING_SNAKE_CASE</summary>
    public static string ToScreamingSnakeCase(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;

        var sb = new StringBuilder(name.Length * 2);
        bool lastWasUpper = false;

        foreach (var c in name)
        {
            if (char.IsUpper(c))
            {
                if (!lastWasUpper && sb.Length > 0)
                {
                    sb.Append('_');
                }
                sb.Append(c);
                lastWasUpper = true;
            }
            else
            {
                sb.Append(char.ToUpper(c));
                lastWasUpper = false;
            }
        }

        return sb.ToString();
    }
}
}

/// <summary>
/// 缩进处理工具。
/// </summary>
public static class IndentationHelper
{
    /// <summary>标准缩进字符串（4 空格）</summary>
    public const string StandardIndent = "    ";

    /// <summary>Tab 缩进字符串</summary>
    public const string TabIndent = "\t";

    /// <summary>缩进单行</summary>
    public static string Indent(string line, int levels = 1, string indent = StandardIndent)
    {
        if (string.IsNullOrEmpty(line)) return line;
        var prefix = string.Concat(System.Linq.Enumerable.Repeat(indent, levels));
        return $"{prefix}{line}";
    }

    /// <summary>缩进多行</summary>
    public static string IndentLines(string text, int levels = 1, string indent = StandardIndent)
    {
        if (string.IsNullOrEmpty(text)) return text;

        var lines = text.Split('\n');
        var sb = new StringBuilder();

        for (int i = 0; i < lines.Length; i++)
        {
            if (i > 0) sb.Append('\n');
            if (!string.IsNullOrEmpty(lines[i]))
            {
                sb.Append(Indent(lines[i], levels, indent));
            }
        }

        return sb.ToString();
    }

    /// <summary>取消缩进</summary>
    public static string Unindent(string text, int levels = 1, string indent = StandardIndent)
    {
        if (string.IsNullOrEmpty(text)) return text;

        var indentToRemove = string.Concat(System.Linq.Enumerable.Repeat(indent, levels));
        var lines = text.Split('\n');
        var sb = new StringBuilder();

        for (int i = 0; i < lines.Length; i++)
        {
            if (i > 0) sb.Append('\n');
            var line = lines[i];
            if (line.StartsWith(indentToRemove))
            {
                sb.Append(line.Substring(indentToRemove.Length));
            }
            else
            {
                sb.Append(line);
            }
        }

        return sb.ToString();
    }
}