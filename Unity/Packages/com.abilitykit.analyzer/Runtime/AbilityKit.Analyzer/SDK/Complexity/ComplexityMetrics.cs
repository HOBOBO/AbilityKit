/// <summary>
/// 文件名称: ComplexityMetrics.cs
/// 
/// 功能描述: 定义代码复杂度度量工具，包括圈复杂度计算和方法长度检查。
/// 
/// 创建日期: 2026-04-06
/// 修改日期: 2026-04-06
/// </summary>

using System;
using System.Collections.Generic;

using AbilityKit.Analyzer;

namespace AbilityKit.Analyzer.SDK.Complexity
{
/// <summary>
/// 圈复杂度计算工具。
/// </summary>
public static class CyclomaticComplexity
{
    /// <summary>根据方法的语句类型列表计算圈复杂度</summary>
    public static int Calculate(IEnumerable<StatementKind> statements)
    {
        if (statements == null) return 1;

        int complexity = 1;

        foreach (var statement in statements)
        {
            complexity += GetComplexityIncrement(statement);
        }

        return complexity;
    }

    private static int GetComplexityIncrement(StatementKind kind)
    {
        return kind switch
        {
            StatementKind.If => 1,
            StatementKind.ElseIf => 1,
            StatementKind.While => 1,
            StatementKind.For => 1,
            StatementKind.Foreach => 1,
            StatementKind.Case => 1,
            StatementKind.Catch => 1,
            StatementKind.ConditionalExpression => 1,
            StatementKind.LogicalAnd => 1,
            StatementKind.LogicalOr => 1,
            StatementKind.NullCoalescing => 1,
            StatementKind.QueryExpression => 1,
            _ => 0
        };
    }

    /// <summary>根据圈复杂度值获取对应的风险等级</summary>
    public static ComplexityRiskLevel GetRiskLevel(int complexity)
    {
        return complexity switch
        {
            <= 10 => ComplexityRiskLevel.Low,
            <= 20 => ComplexityRiskLevel.Moderate,
            <= 50 => ComplexityRiskLevel.High,
            _ => ComplexityRiskLevel.VeryHigh
        };
    }
}

/// <summary>
/// 语句类型枚举。
/// </summary>
public enum StatementKind
{
    None,
    If,
    ElseIf,
    While,
    For,
    Foreach,
    Case,
    Catch,
    ConditionalExpression,
    LogicalAnd,
    LogicalOr,
    NullCoalescing,
    QueryExpression
}

/// <summary>
/// 复杂度风险等级。
/// </summary>
public enum ComplexityRiskLevel
{
    /// <summary>低风险（1-10）</summary>
    Low,

    /// <summary>中等风险（11-20）</summary>
    Moderate,

    /// <summary>高风险（21-50）</summary>
    High,

    /// <summary>极高风险（>50）</summary>
    VeryHigh
}

/// <summary>
/// 方法长度度量工具。
/// </summary>
public static class MethodLengthMetrics
{
    /// <summary>默认最大方法长度</summary>
    public const int DefaultMaxLength = 100;

    /// <summary>警告阈值（最大长度的 80%）</summary>
    public const double WarningThreshold = 0.8;

    /// <summary>检查方法是否超过最大长度限制</summary>
    public static bool ExceedsMaximum(int lineCount, int maxLength = DefaultMaxLength)
    {
        return lineCount > maxLength;
    }

    /// <summary>检查方法是否应该触发警告</summary>
    public static bool ShouldWarn(int lineCount, int maxLength = DefaultMaxLength)
    {
        return lineCount > maxLength * WarningThreshold;
    }

    /// <summary>根据方法长度返回对应的诊断严重级别</summary>
    public static AKDiagnosticSeverity GetSeverity(int lineCount, int maxLength = DefaultMaxLength)
    {
        if (lineCount > maxLength) return AKDiagnosticSeverity.Error;
        if (ShouldWarn(lineCount, maxLength)) return AKDiagnosticSeverity.Warning;
        return AKDiagnosticSeverity.Hidden;
    }
}
}