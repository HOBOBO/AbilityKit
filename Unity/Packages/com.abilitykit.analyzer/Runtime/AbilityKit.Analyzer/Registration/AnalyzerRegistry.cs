/// <summary>
/// 文件名称: AnalyzerRegistry.cs
/// 
/// 功能描述: 定义分析器规则的全局注册表，提供规则的集中管理和查询接口。
/// 
/// 创建日期: 2026-04-06
/// 修改日期: 2026-04-06
/// </summary>

using System;
using System.Collections.Generic;
using System.Linq;

using AbilityKit.Analyzer;

namespace AbilityKit.Analyzer.Registration
{

/// <summary>
/// 分析器规则的全局注册表。
/// </summary>
public static class AnalyzerRegistry
{
    private static readonly object LockObj = new();
    private static readonly Dictionary<string, AKDiagnosticDescriptor> _descriptors = new();
    private static readonly List<IAnalyzerRule> _rules = new();
    private static readonly Dictionary<string, bool> _enabledRules = new();

    /// <summary>所有已注册规则描述器</summary>
    public static IReadOnlyDictionary<string, AKDiagnosticDescriptor> Descriptors
    {
        get
        {
            lock (LockObj)
            {
                return _descriptors;
            }
        }
    }

    /// <summary>所有已注册规则实例</summary>
    public static IReadOnlyList<IAnalyzerRule> Rules
    {
        get
        {
            lock (LockObj)
            {
                return _rules;
            }
        }
    }

    /// <summary>注册一个分析器规则</summary>
    public static void Register(IAnalyzerRule rule)
    {
        if (rule == null)
            throw new ArgumentNullException(nameof(rule));

        lock (LockObj)
        {
            if (_descriptors.ContainsKey(rule.RuleId))
            {
                throw new InvalidOperationException(
                    $"Rule with ID '{rule.RuleId}' has already been registered.");
            }

            var ctx = new AnalyzerRegistrationContext();
            rule.Initialize(ctx);

            _descriptors[rule.RuleId] = rule.Descriptor;
            _rules.Add(rule);
            _enabledRules[rule.RuleId] = rule.Descriptor.IsEnabledByDefault;
        }
    }

    /// <summary>批量注册多个分析器规则</summary>
    public static void Register(IEnumerable<IAnalyzerRule> rules)
    {
        foreach (var rule in rules)
        {
            Register(rule);
        }
    }

    /// <summary>从注册表中移除指定 ID 的规则</summary>
    public static bool Unregister(string ruleId)
    {
        lock (LockObj)
        {
            if (!_descriptors.ContainsKey(ruleId))
                return false;

            _descriptors.Remove(ruleId);
            _enabledRules.Remove(ruleId);

            var rule = _rules.FirstOrDefault(r => r.RuleId == ruleId);
            if (rule != null)
            {
                _rules.Remove(rule);
            }

            return true;
        }
    }

    /// <summary>检查指定规则是否已启用</summary>
    public static bool IsEnabled(string ruleId)
    {
        lock (LockObj)
        {
            return _enabledRules.TryGetValue(ruleId, out var enabled) && enabled;
        }
    }

    /// <summary>设置指定规则的启用状态</summary>
    public static void SetEnabled(string ruleId, bool enabled)
    {
        lock (LockObj)
        {
            if (_enabledRules.ContainsKey(ruleId))
            {
                _enabledRules[ruleId] = enabled;
            }
        }
    }

    /// <summary>获取指定规则 ID 的描述器</summary>
    public static AKDiagnosticDescriptor GetDescriptor(string ruleId)
    {
        lock (LockObj)
        {
            return _descriptors.TryGetValue(ruleId, out var descriptor)
                ? descriptor
                : null;
        }
    }

    /// <summary>获取指定 ID 的规则实例</summary>
    public static IAnalyzerRule GetRule(string ruleId)
    {
        lock (LockObj)
        {
            return _rules.FirstOrDefault(r => r.RuleId == ruleId);
        }
    }

    /// <summary>获取指定分类下的所有规则</summary>
    public static IEnumerable<IAnalyzerRule> GetRulesByCategory(AKDiagnosticCategory category)
    {
        lock (LockObj)
        {
            return _rules.Where(r => r.Descriptor.Category == category).ToList();
        }
    }

    /// <summary>获取所有已启用的规则</summary>
    public static IEnumerable<IAnalyzerRule> GetEnabledRules()
    {
        lock (LockObj)
        {
            return _rules.Where(r => IsEnabled(r.RuleId)).ToList();
        }
    }

    /// <summary>清空注册表</summary>
    public static void Clear()
    {
        lock (LockObj)
        {
            _descriptors.Clear();
            _rules.Clear();
            _enabledRules.Clear();
        }
    }
}

}