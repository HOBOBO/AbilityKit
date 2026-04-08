/// <summary>
/// 文件名称: SafeAccessChecker.cs
/// 
/// 功能描述: 定义安全访问检查的工具类型和辅助方法。
/// 
/// 创建日期: 2026-04-06
/// 修改日期: 2026-04-06
/// </summary>

using System;
using System.Linq;

using AbilityKit.Analyzer;

namespace AbilityKit.Analyzer.SDK.AccessChecks
{
/// <summary>
/// 安全访问检查报告。
/// </summary>
public readonly struct SafeAccessReport
{
    /// <summary>是否不安全</summary>
    public bool IsUnsafe { get; }

    /// <summary>被检查的表达式</summary>
    public string Expression { get; }

    /// <summary>不安全的原因</summary>
    public string Reason { get; }

    /// <summary>建议的修复方案</summary>
    public string SuggestedFix { get; }

    /// <summary>表达式在源代码中的位置</summary>
    public Location Location { get; }

    public SafeAccessReport(
        bool isUnsafe,
        string expression,
        string reason,
        string suggestedFix,
        Location location)
    {
        IsUnsafe = isUnsafe;
        Expression = expression ?? string.Empty;
        Reason = reason ?? string.Empty;
        SuggestedFix = suggestedFix ?? string.Empty;
        Location = location;
    }

    /// <summary>创建安全访问的报告</summary>
    public static SafeAccessReport Safe(string expression)
    {
        return new SafeAccessReport(false, expression, null, null, Location.None);
    }

    /// <summary>创建不安全访问的报告</summary>
    public static SafeAccessReport Unsafe(
        string expression,
        string reason,
        string suggestedFix,
        Location location)
    {
        return new SafeAccessReport(true, expression, reason, suggestedFix, location);
    }
}

/// <summary>
/// 安全访问检查器接口。
/// </summary>
public interface ISafeAccessChecker
{
    /// <summary>检查字段或属性访问是否可能不安全</summary>
    SafeAccessReport CheckAccess(string expression, Location location);

    /// <summary>检查链式成员访问是否可能不安全</summary>
    SafeAccessReport CheckChainedAccess(string expression, int dotCount, Location location);
}

/// <summary>
/// 安全访问模式检测和代码生成的静态工具类。
/// </summary>
public static class SafeAccessPatterns
{
    /// <summary>检测表达式是否应该使用空条件运算符（?.）</summary>
    public static bool ShouldUseNullConditional(string expression)
    {
        return expression.Contains(".") &&
               !expression.Contains("?.") &&
               !expression.Contains("??");
    }

    /// <summary>检测表达式是否需要进行防御性空检查</summary>
    public static bool NeedsDefensiveNullCheck(string expression)
    {
        return expression.Contains(".TryGet") ||
               expression.Contains(".TryGetRef") ||
               expression.Contains(".TryResolve");
    }

    /// <summary>生成不安全表达式的安全版本</summary>
    public static string GenerateSafeExpression(string unsafeExpression)
    {
        if (ShouldUseNullConditional(unsafeExpression))
        {
            var parts = unsafeExpression.Split('.');
            if (parts.Length >= 2)
            {
                return $"{parts[0]}?.{string.Join(".", parts.Skip(1))}";
            }
        }
        return unsafeExpression;
    }

    /// <summary>生成防御性空检查代码块</summary>
    public static string GenerateDefensiveNullCheck(
        string expression,
        string defaultValue,
        string fallbackBlock = null)
    {
        if (!string.IsNullOrEmpty(fallbackBlock))
        {
            return $"if ({expression} == null) {{ {fallbackBlock} }}";
        }
            return $"var _value = {expression} ?? {defaultValue};";
    }
}
}