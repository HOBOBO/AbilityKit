/// <summary>
/// 文件名称: NamingConventions.cs
/// 
/// 功能描述: 定义命名约定的验证接口和标准实现。
/// 
/// 创建日期: 2026-04-06
/// 修改日期: 2026-04-06
/// </summary>

using System.Text.RegularExpressions;

using AbilityKit.Analyzer.Attributes;

namespace AbilityKit.Analyzer.SDK
{

/// <summary>
/// 命名约定验证接口。
/// </summary>
public interface INamingConvention
{
    /// <summary>验证名称是否符合此命名约定</summary>
    bool IsValid(string name);

    /// <summary>命名约定的文字描述</summary>
    string Description { get; }

    /// <summary>符合此约定的名称示例</summary>
    string Example { get; }
}

/// <summary>
/// 标准命名约定的静态访问点。
/// </summary>
public static class NamingConventions
{
    private static readonly PascalCaseConvention _pascalCase = new();
    private static readonly CamelCaseConvention _camelCase = new();
    private static readonly ScreamingSnakeCaseConvention _screamingSnakeCase = new();
    private static readonly SnakeCaseConvention _snakeCase = new();
    private static readonly InterfaceNamingConvention _interfaceNaming = new();
    private static readonly SubFeatureNamingConvention _subFeatureNaming = new();

    /// <summary>PascalCase 约定实例</summary>
    public static INamingConvention PascalCase => _pascalCase;

    /// <summary>camelCase 约定实例</summary>
    public static INamingConvention CamelCase => _camelCase;

    /// <summary>SCREAMING_SNAKE_CASE 约定实例</summary>
    public static INamingConvention ScreamingSnakeCase => _screamingSnakeCase;

    /// <summary>SNAKE_CASE 约定实例</summary>
    public static INamingConvention SnakeCase => _snakeCase;

    /// <summary>接口命名约定实例</summary>
    public static INamingConvention Interface => _interfaceNaming;

    /// <summary>SubFeature 命名约定实例</summary>
    public static INamingConvention SubFeature => _subFeatureNaming;

    /// <summary>根据命名约定类型获取对应的约定实例</summary>
    public static INamingConvention GetConvention(NamingConventionKind kind)
    {
        return kind switch
        {
            NamingConventionKind.PascalCase => _pascalCase,
            NamingConventionKind.CamelCase => _camelCase,
            NamingConventionKind.ScreamingSnakeCase => _screamingSnakeCase,
            NamingConventionKind.SnakeCase => _snakeCase,
            NamingConventionKind.Interface => _interfaceNaming,
            NamingConventionKind.SubFeature => _subFeatureNaming,
            _ => _pascalCase
        };
    }
}

internal sealed class PascalCaseConvention : INamingConvention
{
    private static readonly Regex Pattern = new(@"^[A-Z][a-zA-Z0-9]*$", RegexOptions.Compiled);

    public bool IsValid(string name)
    {
        if (string.IsNullOrEmpty(name)) return false;
        return Pattern.IsMatch(name);
    }

    public string Description => "PascalCase: 首字母大写，每个单词首字母大写";
    public string Example => "MyClassName, BattleSessionFeature";
}

internal sealed class CamelCaseConvention : INamingConvention
{
    private static readonly Regex Pattern = new(@"^[a-z][a-zA-Z0-9]*$", RegexOptions.Compiled);

    public bool IsValid(string name)
    {
        if (string.IsNullOrEmpty(name)) return false;
        return Pattern.IsMatch(name);
    }

    public string Description => "camelCase: 首字母小写，每个单词首字母大写";
    public string Example => "myFieldName, skillPipeline";
}

internal sealed class ScreamingSnakeCaseConvention : INamingConvention
{
    private static readonly Regex Pattern = new(@"^[A-Z][A-Z0-9]*(_[A-Z][A-Z0-9]*)*$", RegexOptions.Compiled);

    public bool IsValid(string name)
    {
        if (string.IsNullOrEmpty(name)) return false;
        return Pattern.IsMatch(name);
    }

    public string Description => "SCREAMING_SNAKE_CASE: 全大写，单词间用下划线分隔";
    public string Example => "MAX_VALUE, DEFAULT_CAPACITY";
}

internal sealed class SnakeCaseConvention : INamingConvention
{
    private static readonly Regex Pattern = new(@"^[a-z][a-z0-9]*(_[a-z][a-z0-9]*)*$", RegexOptions.Compiled);

    public bool IsValid(string name)
    {
        if (string.IsNullOrEmpty(name)) return false;
        return Pattern.IsMatch(name);
    }

    public string Description => "snake_case: 全小写，单词间用下划线分隔";
    public string Example => "max_value, initial_capacity";
}

internal sealed class InterfaceNamingConvention : INamingConvention
{
    private static readonly Regex Pattern = new(@"^I[A-Z][a-zA-Z0-9]*$", RegexOptions.Compiled);

    public bool IsValid(string name)
    {
        if (string.IsNullOrEmpty(name)) return false;
        return Pattern.IsMatch(name);
    }

    public string Description => "Interface: 必须以 'I' 开头";
    public string Example => "ISessionFeature, IGameModule";
}

internal sealed class SubFeatureNamingConvention : INamingConvention
{
    private static readonly Regex Pattern = new(@"^[A-Z][a-zA-Z]*SubFeature$", RegexOptions.Compiled);

    public bool IsValid(string name)
    {
        if (string.IsNullOrEmpty(name)) return false;
        return Pattern.IsMatch(name);
    }

    public string Description => "SubFeature: 必须以 'SubFeature' 结尾";
    public string Example => "SessionEventsSubFeature, SessionLifecycleSubFeature";
}

}
