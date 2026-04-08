/// <summary>
/// 文件名称: AKDiagnostic.cs
/// 
/// 功能描述: 定义诊断消息的不可变结构，表示一个具体的代码分析结果。
/// 
/// 创建日期: 2026-04-06
/// 修改日期: 2026-04-06
/// </summary>

using System;
using System.Collections.Generic;

namespace AbilityKit.Analyzer
{

/// <summary>
/// 表示一个具体诊断消息的不可变结构体。
/// </summary>
public readonly struct AKDiagnostic : IEquatable<AKDiagnostic>
{
    /// <summary>定义此诊断元数据的描述器</summary>
    public AKDiagnosticDescriptor Descriptor { get; }

    /// <summary>诊断在源代码中的位置</summary>
    public Location Location { get; }

    /// <summary>用于替换消息格式占位符的参数列表</summary>
    public IReadOnlyList<string> Arguments { get; }

    /// <summary>诊断的额外上下文信息</summary>
    public string Context { get; }

    private AKDiagnostic(
        AKDiagnosticDescriptor descriptor,
        Location location,
        IReadOnlyList<string> arguments,
        string context)
    {
        Descriptor = descriptor;
        Location = location;
        Arguments = arguments;
        Context = context ?? string.Empty;
    }

    /// <summary>创建包含描述器、位置和格式化参数的诊断实例</summary>
    public static AKDiagnostic Create(
        AKDiagnosticDescriptor descriptor,
        Location location,
        params string[] arguments)
    {
        if (descriptor == null)
            throw new ArgumentNullException(nameof(descriptor));

        return new AKDiagnostic(
            descriptor,
            location,
            arguments.Length > 0 ? arguments : Array.Empty<string>(),
            null);
    }

    /// <summary>创建包含描述器、位置、上下文和格式化参数的诊断实例</summary>
    public static AKDiagnostic Create(
        AKDiagnosticDescriptor descriptor,
        Location location,
        string context,
        params string[] arguments)
    {
        if (descriptor == null)
            throw new ArgumentNullException(nameof(descriptor));

        return new AKDiagnostic(
            descriptor,
            location,
            arguments.Length > 0 ? arguments : Array.Empty<string>(),
            context);
    }

    /// <summary>获取格式化后的诊断消息文本</summary>
    public string GetMessage()
    {
        var message = Descriptor.MessageFormat;
        if (Arguments.Count == 0) return message;

        for (int i = 0; i < Arguments.Count; i++)
        {
            var placeholder = $"{{{i}}}";
            message = message.Replace(placeholder, Arguments[i] ?? string.Empty);
        }
        return message;
    }

    /// <summary>获取诊断的摘要字符串，格式为 "Id: Message at Location"</summary>
    public string ToSummaryString()
    {
        return $"{Descriptor.Id}: {GetMessage()} at {Location}";
    }

    public bool Equals(AKDiagnostic other)
    {
        return Descriptor.Id == other.Descriptor.Id &&
               Location.Equals(other.Location) &&
               Arguments.Count == other.Arguments.Count;
    }

    public override bool Equals(object obj)
    {
        return obj is AKDiagnostic other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Descriptor.Id, Location);
    }

    public override string ToString()
    {
        return ToSummaryString();
    }

    public static bool operator ==(AKDiagnostic left, AKDiagnostic right) => left.Equals(right);
    public static bool operator !=(AKDiagnostic left, AKDiagnostic right) => !left.Equals(right);
}

}
