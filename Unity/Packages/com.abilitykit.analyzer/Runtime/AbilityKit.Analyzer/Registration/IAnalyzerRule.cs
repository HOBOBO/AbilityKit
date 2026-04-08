/// <summary>
/// 文件名称: IAnalyzerRule.cs
/// 
/// 功能描述: 定义分析器规则的核心接口和注册上下文。
/// 
/// 创建日期: 2026-04-06
/// 修改日期: 2026-04-06
/// </summary>

using System;
using System.Collections.Generic;
using System.Reflection;

using AbilityKit.Analyzer;
using AbilityKit.Analyzer.Attributes;

namespace AbilityKit.Analyzer.Registration
{
/// <summary>
/// 分析器规则的抽象接口。
/// </summary>
public interface IAnalyzerRule
{
    /// <summary>规则的唯一标识符</summary>
    string RuleId { get; }

    /// <summary>规则的所有元数据描述器</summary>
    AKDiagnosticDescriptor Descriptor { get; }

    /// <summary>初始化规则</summary>
    void Initialize(AnalyzerRegistrationContext context);
}

/// <summary>
/// 规则注册上下文。
/// </summary>
public sealed class AnalyzerRegistrationContext
{
    private readonly List<IAnalyzerRule> _rules = new();
    private readonly HashSet<string> _registeredIds = new();

    /// <summary>所有已注册规则</summary>
    public IReadOnlyList<IAnalyzerRule> Rules => _rules;

    /// <summary>注册一个新规则</summary>
    public void RegisterRule(IAnalyzerRule rule)
    {
        if (rule == null)
            throw new ArgumentNullException(nameof(rule));

        if (_registeredIds.Contains(rule.RuleId))
        {
            throw new InvalidOperationException(
                $"A rule with ID '{rule.RuleId}' has already been registered.");
        }

        _rules.Add(rule);
        _registeredIds.Add(rule.RuleId);
    }

    /// <summary>尝试注册规则，失败时不抛出异常</summary>
    public bool TryRegisterRule(IAnalyzerRule rule)
    {
        if (rule == null || _registeredIds.Contains(rule.RuleId))
            return false;

        _rules.Add(rule);
        _registeredIds.Add(rule.RuleId);
        return true;
    }

    /// <summary>检查指定 ID 的规则是否已注册</summary>
    public bool IsRuleRegistered(string ruleId)
    {
        return _registeredIds.Contains(ruleId);
    }

    /// <summary>清空所有已注册的规则</summary>
    public void Clear()
    {
        _rules.Clear();
        _registeredIds.Clear();
    }
}

/// <summary>
/// 规则注册的扩展方法。
/// </summary>
public static class AnalyzerRuleExtensions
{
    /// <summary>从 [DiagnosticRule] 特性创建并注册规则</summary>
    public static void RegisterRuleFromAttribute(
        this AnalyzerRegistrationContext context,
        Type ruleType)
    {
        var attr = ruleType.GetCustomAttribute(typeof(DiagnosticRuleAttribute), false)
            as DiagnosticRuleAttribute;

        if (attr == null)
            throw new InvalidOperationException(
                $"Type {ruleType.FullName} does not have a DiagnosticRuleAttribute.");

        var descriptor = new AKDiagnosticDescriptor(
            id: attr.Id,
            title: attr.Title,
            description: attr.Description,
            messageFormat: attr.MessageFormat,
            category: attr.Category,
            defaultSeverity: attr.Severity,
            isEnabledByDefault: attr.IsEnabledByDefault,
            helpLink: attr.HelpLink);
    }
}
}