/// <summary>
/// 文件名称: DiagnosticInfo.cs
/// 
/// 功能描述: 定义代码生成过程中产生的诊断信息的数据结构。
/// 这是 AbilityKit.Analyzer.AKDiagnostic 的简化版本，不依赖 Roslyn。
/// 
/// 创建日期: 2026-04-07
/// 修改日期: 2026-04-07
/// </summary>

using System;

namespace AbilityKit.CodeGen.Core
{
/// <summary>
/// 代码生成诊断信息的严重级别。
/// </summary>
public enum DiagnosticSeverity
{
    /// <summary>错误，阻止生成</summary>
    Error = 0,
    /// <summary>警告，不阻止但建议修复</summary>
    Warning = 1,
    /// <summary>信息，仅供参考</summary>
    Info = 2
}

/// <summary>
/// 代码生成过程中产生的诊断信息。
/// </summary>
public readonly struct DiagnosticInfo : IEquatable<DiagnosticInfo>
{
    /// <summary>诊断的唯一标识符</summary>
    public string Id { get; }

    /// <summary>诊断消息</summary>
    public string Message { get; }

    /// <summary>诊断的严重级别</summary>
    public DiagnosticSeverity Severity { get; }

    /// <summary>源文件路径</summary>
    public string FilePath { get; }

    /// <summary>行号（1-based）</summary>
    public int Line { get; }

    /// <summary>列号（1-based）</summary>
    public int Column { get; }

    public DiagnosticInfo(string id, string message, DiagnosticSeverity severity, string filePath = null, int line = 0, int column = 0)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
        Message = message ?? string.Empty;
        Severity = severity;
        FilePath = filePath ?? string.Empty;
        Line = line;
        Column = column;
    }

    /// <summary>创建错误诊断</summary>
    public static DiagnosticInfo Error(string id, string message, string filePath = null, int line = 0, int column = 0)
    {
        return new DiagnosticInfo(id, message, DiagnosticSeverity.Error, filePath, line, column);
    }

    /// <summary>创建警告诊断</summary>
    public static DiagnosticInfo Warning(string id, string message, string filePath = null, int line = 0, int column = 0)
    {
        return new DiagnosticInfo(id, message, DiagnosticSeverity.Warning, filePath, line, column);
    }

    /// <summary>创建信息诊断</summary>
    public static DiagnosticInfo Info(string id, string message, string filePath = null, int line = 0, int column = 0)
    {
        return new DiagnosticInfo(id, message, DiagnosticSeverity.Info, filePath, line, column);
    }

    public bool Equals(DiagnosticInfo other)
    {
        return Id == other.Id &&
               Message == other.Message &&
               Severity == other.Severity &&
               FilePath == other.FilePath &&
               Line == other.Line &&
               Column == other.Column;
    }

    public override bool Equals(object obj)
    {
        return obj is DiagnosticInfo other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Id, Message, Severity, FilePath, Line, Column);
    }

    public override string ToString()
    {
        var location = string.IsNullOrEmpty(FilePath) ? "" : $"{FilePath}({Line},{Column}): ";
        return $"{location}[{Severity}] {Id}: {Message}";
    }
}
}
