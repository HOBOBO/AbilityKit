/// <summary>
/// 文件名称: SuppressDiagnosticAttribute.cs
/// 
/// 功能描述: 定义诊断抑制特性，用于在特定代码元素上禁用指定的诊断规则。
/// 
/// 创建日期: 2026-04-06
/// 修改日期: 2026-04-06
/// </summary>

using System;

namespace AbilityKit.Analyzer.Attributes
{
/// <summary>
/// 标记代码元素以抑制特定诊断规则。
/// </summary>
[AttributeUsage(
    AttributeTargets.Method |
    AttributeTargets.Class |
    AttributeTargets.Struct |
    AttributeTargets.Property |
    AttributeTargets.Field |
    AttributeTargets.Interface,
    AllowMultiple = true,
    Inherited = false)]
public sealed class SuppressDiagnosticAttribute : Attribute
{
    /// <summary>要抑制的诊断规则 ID</summary>
    public string DiagnosticId { get; }

    /// <summary>抑制此诊断的理由</summary>
    public string Justification { get; set; }

    public SuppressDiagnosticAttribute(string diagnosticId)
    {
        if (string.IsNullOrEmpty(diagnosticId))
            throw new ArgumentException("DiagnosticId cannot be null or empty", nameof(diagnosticId));

        DiagnosticId = diagnosticId;
    }
}
}