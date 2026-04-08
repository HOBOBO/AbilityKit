/// <summary>
/// 文件名称: DiagnosticReporter.cs
/// 
/// 功能描述: 定义诊断报告器，负责收集、汇总和格式化诊断消息。
/// 
/// 创建日期: 2026-04-06
/// 修改日期: 2026-04-06
/// </summary>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using AbilityKit.Analyzer;

namespace AbilityKit.Analyzer.Reporting
{
/// <summary>
/// 诊断格式化器接口，定义诊断输出的格式化逻辑。
/// </summary>
public interface IDiagnosticFormatter
{
    /// <summary>格式化单个诊断</summary>
    string FormatDiagnostic(AKDiagnostic diagnostic);

    /// <summary>格式化诊断集合</summary>
    string FormatDiagnostics(IEnumerable<AKDiagnostic> diagnostics);

    /// <summary>此格式化器对应的文件扩展名</summary>
    string FileExtension { get; }
}

/// <summary>
/// 纯文本格式的诊断格式化器。
/// </summary>
public sealed class PlainTextDiagnosticFormatter : IDiagnosticFormatter
{
    private readonly bool _includeContext;
    private readonly bool _coloredOutput;

    public PlainTextDiagnosticFormatter(bool includeContext = false, bool coloredOutput = false)
    {
        _includeContext = includeContext;
        _coloredOutput = coloredOutput;
    }

    public string FileExtension => ".txt";

    public string FormatDiagnostic(AKDiagnostic diagnostic)
    {
        var sb = new StringBuilder();

        var severityStr = diagnostic.Descriptor.DefaultSeverity switch
        {
            AKDiagnosticSeverity.Error => "ERROR",
            AKDiagnosticSeverity.Warning => "WARNING",
            AKDiagnosticSeverity.Info => "INFO",
            _ => "HIDDEN"
        };

        if (_coloredOutput)
        {
            var colorCode = diagnostic.Descriptor.DefaultSeverity switch
            {
                AKDiagnosticSeverity.Error => "\x1b[31m",
                AKDiagnosticSeverity.Warning => "\x1b[33m",
                AKDiagnosticSeverity.Info => "\x1b[36m",
                _ => "\x1b[90m"
            };
            sb.AppendLine($"{colorCode}{severityStr}\x1b[0m {diagnostic.Descriptor.Id}: {diagnostic.Location}");
        }
        else
        {
            sb.AppendLine($"{severityStr} {diagnostic.Descriptor.Id}: {diagnostic.Location}");
        }

        sb.AppendLine($"  {diagnostic.GetMessage()}");

        if (_includeContext && !string.IsNullOrEmpty(diagnostic.Context))
        {
            sb.AppendLine($"  Context: {diagnostic.Context}");
        }

        sb.AppendLine();

        return sb.ToString();
    }

    public string FormatDiagnostics(IEnumerable<AKDiagnostic> diagnostics)
    {
        var sb = new StringBuilder();

        foreach (var diagnostic in diagnostics)
        {
            sb.Append(FormatDiagnostic(diagnostic));
        }

        return sb.ToString();
    }
}

/// <summary>
/// JSON 格式的诊断格式化器。
/// </summary>
public sealed class JsonDiagnosticFormatter : IDiagnosticFormatter
{
    public string FileExtension => ".json";

    public string FormatDiagnostic(AKDiagnostic diagnostic)
    {
        return $"{{\"id\":\"{diagnostic.Descriptor.Id}\",\"location\":\"{diagnostic.Location}\",\"message\":\"{EscapeJson(diagnostic.GetMessage())}\",\"severity\":\"{diagnostic.Descriptor.DefaultSeverity}\",\"category\":\"{diagnostic.Descriptor.Category}\"}}";
    }

    public string FormatDiagnostics(IEnumerable<AKDiagnostic> diagnostics)
    {
        var sb = new StringBuilder();
        sb.AppendLine("{");
        sb.AppendLine("  \"diagnostics\": [");

        var first = true;
        foreach (var diagnostic in diagnostics)
        {
            if (!first)
            {
                sb.AppendLine(",");
            }
            sb.Append($"    {FormatDiagnostic(diagnostic)}");
            first = false;
        }

        sb.AppendLine();
        sb.AppendLine("  ],");
        sb.AppendLine($"  \"count\": {diagnostics.Count()}");
        sb.AppendLine("}");

        return sb.ToString();
    }

    private static string EscapeJson(string text)
    {
        return text
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r")
            .Replace("\t", "\\t");
    }
}

/// <summary>
/// 诊断报告器，负责收集诊断并输出格式化报告。
/// </summary>
public sealed class DiagnosticReporter
{
    private readonly List<AKDiagnostic> _diagnostics = new();
    private readonly List<IDiagnosticFormatter> _formatters = new();

    /// <summary>所有已报告的诊断</summary>
    public IReadOnlyList<AKDiagnostic> Diagnostics => _diagnostics;

    /// <summary>按严重级别分类的诊断计数</summary>
    public DiagnosticCounts Counts => new DiagnosticCounts(
        _diagnostics.Count(d => d.Descriptor.DefaultSeverity == AKDiagnosticSeverity.Error),
        _diagnostics.Count(d => d.Descriptor.DefaultSeverity == AKDiagnosticSeverity.Warning),
        _diagnostics.Count(d => d.Descriptor.DefaultSeverity == AKDiagnosticSeverity.Info));

    /// <summary>注册一个格式化器用于输出</summary>
    public void AddFormatter(IDiagnosticFormatter formatter)
    {
        if (formatter != null)
        {
            _formatters.Add(formatter);
        }
    }

    /// <summary>报告一个诊断消息</summary>
    public void Report(AKDiagnostic diagnostic)
    {
        _diagnostics.Add(diagnostic);
    }

    /// <summary>批量报告多个诊断消息</summary>
    public void Report(IEnumerable<AKDiagnostic> diagnostics)
    {
        _diagnostics.AddRange(diagnostics);
    }

    /// <summary>将诊断强制报告为 Error 级别</summary>
    public void ReportAsError(AKDiagnostic diagnostic)
    {
        _diagnostics.Add(diagnostic);
    }

    /// <summary>使用指定的格式化器格式化所有诊断</summary>
    public string FormatAll(IDiagnosticFormatter formatter)
    {
        return formatter.FormatDiagnostics(_diagnostics);
    }

    /// <summary>使用所有已注册的格式化器格式化诊断</summary>
    public string FormatAll()
    {
        var sb = new StringBuilder();
        foreach (var formatter in _formatters)
        {
            sb.AppendLine($"=== {formatter.GetType().Name} ({formatter.FileExtension}) ===");
            sb.AppendLine(FormatAll(formatter));
            sb.AppendLine();
        }
        return sb.ToString();
    }

    /// <summary>清空所有已报告的诊断</summary>
    public void Clear()
    {
        _diagnostics.Clear();
    }

    public override string ToString()
    {
        var counts = Counts;
        return $"Errors: {counts.ErrorCount}, Warnings: {counts.WarningCount}, Info: {counts.InfoCount}";
    }
}

/// <summary>
/// 诊断计数的不可变结构体。
/// </summary>
public readonly struct DiagnosticCounts
{
    /// <summary>Error 级别诊断数量</summary>
    public int ErrorCount { get; }

    /// <summary>Warning 级别诊断数量</summary>
    public int WarningCount { get; }

    /// <summary>Info 级别诊断数量</summary>
    public int InfoCount { get; }

    /// <summary>所有级别诊断的总数</summary>
    public int Total => ErrorCount + WarningCount + InfoCount;

    public DiagnosticCounts(int errorCount, int warningCount, int infoCount)
    {
        ErrorCount = errorCount;
        WarningCount = warningCount;
        InfoCount = infoCount;
    }

    /// <summary>是否存在 Error 级别诊断</summary>
    public bool HasErrors => ErrorCount > 0;

    /// <summary>是否存在 Warning 级别诊断</summary>
    public bool HasWarnings => WarningCount > 0;
}
}