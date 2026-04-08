/// <summary>
/// 文件名称: AKDiagnosticDescriptor.cs
/// 
/// 功能描述: 定义诊断规则的元数据描述器，包含规则的标识、标题、描述和默认严重级别。
/// 
/// 创建日期: 2026-04-06
/// 修改日期: 2026-04-06
/// </summary>

using System;

namespace AbilityKit.Analyzer
{
/// <summary>
/// 诊断规则的元数据描述器。
/// </summary>
public sealed class AKDiagnosticDescriptor
{
    /// <summary>诊断规则的唯一标识符（如 AK0001）</summary>
    public string Id { get; }

    /// <summary>诊断规则的简短标题</summary>
    public string Title { get; }

    /// <summary>诊断规则的详细描述</summary>
    public string Description { get; }

    /// <summary>诊断消息的格式字符串，支持 {0}, {1} 等占位符</summary>
    public string MessageFormat { get; }

    /// <summary>诊断所属分类</summary>
    public AKDiagnosticCategory Category { get; }

    /// <summary>诊断的默认严重级别</summary>
    public AKDiagnosticSeverity DefaultSeverity { get; }

    /// <summary>诊断规则是否默认启用</summary>
    public bool IsEnabledByDefault { get; }

    /// <summary>指向诊断详细信息的 URL</summary>
    public string HelpLink { get; }

    public AKDiagnosticDescriptor(
        string id,
        string title,
        string description,
        string messageFormat,
        AKDiagnosticCategory category,
        AKDiagnosticSeverity defaultSeverity,
        bool isEnabledByDefault = true,
        string helpLink = null)
    {
        if (string.IsNullOrEmpty(id))
            throw new ArgumentNullException(nameof(id));
        if (string.IsNullOrEmpty(title))
            throw new ArgumentNullException(nameof(title));
        if (string.IsNullOrEmpty(messageFormat))
            throw new ArgumentNullException(nameof(messageFormat));

        Id = id;
        Title = title;
        Description = description ?? string.Empty;
        MessageFormat = messageFormat;
        Category = category;
        DefaultSeverity = defaultSeverity;
        IsEnabledByDefault = isEnabledByDefault;
        HelpLink = helpLink;
    }

    /// <summary>基于此描述器创建诊断实例</summary>
    public AKDiagnostic CreateDiagnostic(Location location, params string[] arguments)
    {
        return AKDiagnostic.Create(this, location, arguments);
    }

    public override string ToString()
    {
        return $"{Id}: {Title} [{Category}]";
    }
}
}