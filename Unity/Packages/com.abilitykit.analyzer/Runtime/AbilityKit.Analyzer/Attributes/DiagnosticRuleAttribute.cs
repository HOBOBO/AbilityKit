/// <summary>
/// 文件名称: DiagnosticRuleAttribute.cs
/// 
/// 功能描述: 定义诊断规则的特性，用于标记实现诊断检查逻辑的类。
/// 
/// 创建日期: 2026-04-06
/// 修改日期: 2026-04-06
/// </summary>

using System;

using AbilityKit.Analyzer;

namespace AbilityKit.Analyzer.Attributes
{
/// <summary>
/// 标记类为诊断规则的特性。
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class DiagnosticRuleAttribute : Attribute
{
    /// <summary>诊断规则的唯一标识符</summary>
    public string Id { get; }

    /// <summary>诊断规则的简短标题</summary>
    public string Title { get; }

    /// <summary>诊断规则的详细描述</summary>
    public string Description { get; }

    /// <summary>诊断消息的格式字符串</summary>
    public string MessageFormat { get; }

    /// <summary>诊断所属分类</summary>
    public AKDiagnosticCategory Category { get; }

    /// <summary>诊断的默认严重级别</summary>
    public AKDiagnosticSeverity Severity { get; }

    /// <summary>规则是否默认启用</summary>
    public bool IsEnabledByDefault { get; set; } = true;

    /// <summary>指向规则详细文档的 URL</summary>
    public string HelpLink { get; set; }

    public DiagnosticRuleAttribute(
        string id,
        string title,
        string messageFormat,
        AKDiagnosticCategory category,
        AKDiagnosticSeverity severity)
    {
        if (string.IsNullOrEmpty(id))
            throw new ArgumentException("Id cannot be null or empty", nameof(id));
        if (string.IsNullOrEmpty(title))
            throw new ArgumentException("Title cannot be null or empty", nameof(title));
        if (string.IsNullOrEmpty(messageFormat))
            throw new ArgumentException("MessageFormat cannot be null or empty", nameof(messageFormat));

        Id = id;
        Title = title;
        MessageFormat = messageFormat;
        Category = category;
        Severity = severity;
    }
}
}