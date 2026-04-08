/// <summary>
/// 文件名称: GenerationResult.cs
/// 
/// 功能描述: 定义代码生成操作的结果对象，包含生成状态、输出文件和诊断信息。
/// 
/// 创建日期: 2026-04-06
/// 修改日期: 2026-04-07
/// </summary>

using System;
using System.Collections.Generic;

using AbilityKit.CodeGen.Core;

namespace AbilityKit.CodeGen.Core
{
/// <summary>
/// 代码生成操作的结果封装类。
/// </summary>
public sealed class GenerationResult
{
    /// <summary>生成操作是否成功</summary>
    public bool Success { get; }

    /// <summary>生成失败时的错误描述</summary>
    public string ErrorMessage { get; }

    /// <summary>生成的所有输出文件</summary>
    public IReadOnlyList<OutputFile> OutputFiles { get; }

    /// <summary>生成过程中产生的诊断信息</summary>
    public IReadOnlyList<DiagnosticInfo> Diagnostics { get; }

    /// <summary>产生此结果的生成器标识</summary>
    public string GeneratorName { get; }

    /// <summary>被标记的目标类型名称</summary>
    public string TargetType { get; }

    private GenerationResult(
        bool success,
        string errorMessage,
        IReadOnlyList<OutputFile> outputFiles,
        IReadOnlyList<DiagnosticInfo> diagnostics,
        string generatorName,
        string targetType)
    {
        Success = success;
        ErrorMessage = errorMessage ?? string.Empty;
        OutputFiles = outputFiles ?? Array.Empty<OutputFile>();
        Diagnostics = diagnostics ?? Array.Empty<DiagnosticInfo>();
        GeneratorName = generatorName ?? string.Empty;
        TargetType = targetType ?? string.Empty;
    }

    /// <summary>创建表示成功生成的结果</summary>
    public static GenerationResult SuccessResult(
        IEnumerable<OutputFile> outputFiles,
        string generatorName,
        string targetType)
    {
        return new GenerationResult(
            true,
            null,
            outputFiles != null ? new List<OutputFile>(outputFiles) : new List<OutputFile>(),
            Array.Empty<DiagnosticInfo>(),
            generatorName,
            targetType);
    }

    /// <summary>创建表示生成失败的结果</summary>
    public static GenerationResult FailureResult(
        string errorMessage,
        IEnumerable<DiagnosticInfo> diagnostics = null,
        string generatorName = null,
        string targetType = null)
    {
        return new GenerationResult(
            false,
            errorMessage,
            Array.Empty<OutputFile>(),
            diagnostics != null ? new List<DiagnosticInfo>(diagnostics) : new List<DiagnosticInfo>(),
            generatorName,
            targetType);
    }

    /// <summary>创建有诊断但无输出文件的结果</summary>
    public static GenerationResult DiagnosticResult(
        IEnumerable<DiagnosticInfo> diagnostics,
        string generatorName,
        string targetType)
    {
        return new GenerationResult(
            true,
            null,
            Array.Empty<OutputFile>(),
            diagnostics != null ? new List<DiagnosticInfo>(diagnostics) : new List<DiagnosticInfo>(),
            generatorName,
            targetType);
    }

    /// <summary>生成代码的总行数</summary>
    public int TotalLines
    {
        get
        {
            int lines = 0;
            foreach (var file in OutputFiles)
            {
                if (!string.IsNullOrEmpty(file.Content))
                {
                    lines += file.Content.Split('\n').Length;
                }
            }
            return lines;
        }
    }

    /// <summary>生成的文件数量</summary>
    public int FileCount => OutputFiles.Count;

    public override string ToString()
    {
        if (!Success)
        {
            return $"Generation failed: {ErrorMessage}";
        }

        return $"Generated {FileCount} file(s) with {TotalLines} lines of code";
    }
}
}
