/// <summary>
/// 文件名称: NamespaceConstraintPostProcessor.cs
/// 
/// 功能描述: Unity 程序集编译后处理器，在程序集编译完成后执行命名空间约束检查。
/// 提供构建时验证，支持 CI/CD 集成，自动检测所有编译的程序集并报告违规。
/// 
/// 创建日期: 2026-04-06
/// 修改日期: 2026-04-06
/// </summary>

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

using UnityEngine;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;

using Newtonsoft.Json.Linq;

using AbilityKit.Analyzer;
using AbilityKit.Analyzer.Configuration;

namespace AbilityKit.Analyzer.Editor
{

/// <summary>
/// 程序集编译后处理器，执行命名空间约束检查。
/// </summary>
public sealed class NamespaceConstraintPostProcessor
{
    private const string LogPrefix = "[NamespaceConstraint]";
    private ConstraintLoader _loader;
    private int _violationsCount;
    private int _warningsCount;
    private readonly List<string> _violationMessages = new();

    /// <summary>
    /// 在程序集编译完成后执行约束检查。
    /// </summary>
    /// <param name="assemblies">已编译的程序集名称列表</param>
    public void OnAssemblyCompilationCompleted(List<string> assemblies)
    {
        if (assemblies == null || assemblies.Count == 0)
            return;

        RunChecks(assemblies);
    }

    /// <summary>
    /// 在编译回调中使用的方法。
    /// 如果有违规，会将详细信息记录到 Console。
    /// </summary>
    public void OnCompilationFinished(List<string> assemblies)
    {
        if (assemblies == null || assemblies.Count == 0)
            return;

        RunChecks(assemblies);

        // 将违规汇总为单条错误（方便 CI 识别）
        if (_violationsCount > 0)
        {
            Debug.LogError(
                $"[AK1001] Namespace constraint violations: {_violationsCount} error(s), {_warningsCount} warning(s). " +
                $"See above for details. Build will continue but violations must be fixed.");
        }
    }

    private void RunChecks(List<string> assemblies)
    {

        _loader = new ConstraintLoader();

        var config = _loader.Load();
        
        // TODO: 临时注释配置检查逻辑，全量分析
        // if (config == null ||
        //     ((config.Constraints == null || config.Constraints.Count == 0) && !config.GlobalDefaults.Enabled))
        // {
        //     // 无配置，跳过
        //     return;
        // }

        _violationsCount = 0;
        _warningsCount = 0;
        _violationMessages.Clear();

        foreach (var assemblyName in assemblies)
        {
            CheckAssemblyConstraints(assemblyName, config);
        }

        ReportResults();
    }

    private void CheckAssemblyConstraints(string assemblyName, PackageConstraintsConfig config)
    {
        // 白名单模式：只检查配置中明确列出的包
        var constraint = _loader.GetConstraint(assemblyName);
        
        // GetConstraint 已经包含了通配符匹配逻辑
        // 如果包不在配置中，GetConstraint 返回 null
        if (constraint == null || !constraint.IsEnabled)
            return;

        var asmdefPath = FindAsmdefPath(assemblyName);
        if (string.IsNullOrEmpty(asmdefPath) || !File.Exists(asmdefPath))
            return;

        var sourceFiles = CollectSourceFiles(asmdefPath);

        foreach (var file in sourceFiles)
        {
            CheckFileViolations(file, constraint, assemblyName);
        }
    }

    private string FindAsmdefPath(string assemblyName)
    {
        var baseDir = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        var searchPaths = new[]
        {
            Path.Combine(baseDir, "Packages"),
            Path.Combine(baseDir, "Assets")
        };

        foreach (var basePath in searchPaths)
        {
            if (!Directory.Exists(basePath))
                continue;

            var files = Directory.GetFiles(basePath, "*.asmdef", SearchOption.AllDirectories);
            foreach (var file in files)
            {
                try
                {
                    var json = File.ReadAllText(file);
                    var doc = JObject.Parse(json);
                    var nameProp = doc["name"];
                    if (nameProp != null && nameProp.Value<string>() == assemblyName)
                        return file;
                }
                catch
                {
                    // 跳过无效的 asmdef 文件
                }
            }
        }

        return null;
    }

    private List<string> CollectSourceFiles(string asmdefPath)
    {
        var sourceFiles = new List<string>();
        var asmdefDir = Path.GetDirectoryName(asmdefPath);

            // 从 asmdef 读取 includeSources
            try
            {
                var json = File.ReadAllText(asmdefPath);
                var doc = JObject.Parse(json);
                var includeElement = doc["includeSources"] as JArray;

                if (includeElement != null)
                {
                    foreach (var item in includeElement)
                    {
                        if (item.Type == JTokenType.String)
                        {
                            var relativePath = item.Value<string>();
                            if (!string.IsNullOrEmpty(relativePath))
                            {
                                var fullPath = Path.Combine(asmdefDir, relativePath);
                                if (File.Exists(fullPath))
                                    sourceFiles.Add(fullPath);
                            }
                        }
                    }
                }
        }
        catch
        {
            // 忽略解析错误，使用默认扫描
        }

        // 扫描 asmdef 所在目录本身（处理 asmdef 在包根目录的情况，如 AbilityKit.Modifiers）
        if (Directory.Exists(asmdefDir))
        {
            sourceFiles.AddRange(Directory.GetFiles(asmdefDir, "*.cs", SearchOption.AllDirectories));
        }

        return sourceFiles.Distinct().ToList();
    }

    private void CheckFileViolations(string filePath, PackageConstraint constraint, string assemblyName)
    {
        if (!File.Exists(filePath))
            return;

        try
        {
            var lines = File.ReadAllLines(filePath);
            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i];

                // 检查 using 指令
                if (line.TrimStart().StartsWith("using "))
                {
                    var ns = ExtractNamespaceFromUsing(line);
                    if (!string.IsNullOrEmpty(ns) && constraint.IsNamespaceForbidden(ns))
                    {
                        ReportViolation(
                            filePath,
                            i + 1,
                            ns,
                            assemblyName,
                            constraint.ForbiddenNamespaces,
                            constraint.Severity);
                    }
                }

                // 检查 using 别名
                if (line.TrimStart().StartsWith("using ") && line.Contains(" = "))
                {
                    var aliasStart = line.IndexOf("= ");
                    if (aliasStart >= 0)
                    {
                        var aliasTarget = line.Substring(aliasStart + 2).Trim().TrimEnd(';');
                        if (constraint.IsNamespaceForbidden(aliasTarget))
                        {
                            ReportViolation(
                                filePath,
                                i + 1,
                                aliasTarget,
                                assemblyName,
                                constraint.ForbiddenNamespaces,
                                constraint.Severity);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"{LogPrefix} Failed to scan file '{filePath}': {ex.Message}");
        }
    }

    private static string ExtractNamespaceFromUsing(string line)
    {
        var startIndex = line.IndexOf("using ");
        if (startIndex < 0)
            return null;

        var nsStart = startIndex + "using ".Length;
        var semiColon = line.IndexOf(';', nsStart);
        if (semiColon < 0)
            return null;

        var ns = line.Substring(nsStart, semiColon - nsStart).Trim();

        // 跳过 global using 别名
        if (ns.StartsWith("global"))
            return null;

        return ns;
    }

    private void ReportViolation(
        string filePath,
        int lineNumber,
        string forbiddenNamespace,
        string assemblyName,
        List<string> allForbidden,
        AKDiagnosticSeverity severity)
    {
        var message = $"{LogPrefix} [{severity}] AK1001 Forbidden namespace '{forbiddenNamespace}' " +
                      $"in '{assemblyName}' at {filePath}:{lineNumber}. " +
                      $"This assembly restricts: {string.Join(", ", allForbidden)}";

        _violationMessages.Add(message);

        if (severity == AKDiagnosticSeverity.Error)
            _violationsCount++;
        else
            _warningsCount++;

        // 使用带文件位置的日志，确保可以点击跳转到错误位置
        try
        {
            var unityObject = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(
                GetRelativePath(filePath));
            if (unityObject != null)
            {
                if (severity == AKDiagnosticSeverity.Error)
                    Debug.LogError(message, unityObject);
                else
                    Debug.LogWarning(message, unityObject);
            }
            else
            {
                if (severity == AKDiagnosticSeverity.Error)
                    Debug.LogError(message);
                else
                    Debug.LogWarning(message);
            }
        }
        catch
        {
            if (severity == AKDiagnosticSeverity.Error)
                Debug.LogError(message);
            else
                Debug.LogWarning(message);
        }
    }

    private static string GetRelativePath(string fullPath)
    {
        var baseDir = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        if (fullPath.StartsWith(baseDir))
            return fullPath.Substring(baseDir.Length + 1);
        return fullPath;
    }

    private void ReportResults()
    {
        if (_violationsCount > 0 || _warningsCount > 0)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"=== Namespace Constraint Check Results ===");
            sb.AppendLine($"Violations: {_violationsCount}  Warnings: {_warningsCount}");
            sb.AppendLine();

            foreach (var msg in _violationMessages)
            {
                sb.AppendLine(msg);
                Debug.LogWarning(msg);
            }

            sb.AppendLine();
            if (_violationsCount > 0)
            {
                sb.AppendLine("BUILD FAILED: Namespace constraint violations found.");
                Debug.LogError(sb.ToString());
            }
            else
            {
                Debug.Log(sb.ToString());
            }
        }
    }

    /// <summary>
    /// 获取当前违规数量。
    /// </summary>
    public int ViolationsCount => _violationsCount;

    /// <summary>
    /// 获取当前警告数量。
    /// </summary>
    public int WarningsCount => _warningsCount;
}

/// <summary>
/// 主动触发的约束检查命令（可通过菜单或快捷键调用）。
/// </summary>
public static class ConstraintCheckMenu
{
    private const string MenuPath = "Window/AbilityKit/Check Namespace Constraints";

    [MenuItem(MenuPath)]
    private static void RunConstraintCheck()
    {
        var processor = new NamespaceConstraintPostProcessor();

        // 获取当前项目中所有已定义的 asmdef 程序集名称
        var assemblies = new List<string>();
        var baseDir = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        var searchPaths = new[]
        {
            Path.Combine(baseDir, "Packages"),
            Path.Combine(baseDir, "Assets")
        };

        foreach (var basePath in searchPaths)
        {
            if (!Directory.Exists(basePath))
                continue;

            var files = Directory.GetFiles(basePath, "*.asmdef", SearchOption.AllDirectories);
            foreach (var file in files)
            {
                try
                {
                    var json = File.ReadAllText(file);
                    var doc = JObject.Parse(json);
                    var nameProp = doc["name"];
                    if (nameProp != null)
                    {
                        var name = nameProp.Value<string>();
                        if (!string.IsNullOrEmpty(name) &&
                            !assemblies.Contains(name) &&
                            !name.EndsWith(".Tests") &&
                            !name.EndsWith(".Editor"))
                        {
                            assemblies.Add(name);
                        }
                    }
                }
                catch
                {
                    // 忽略无效文件
                }
            }
        }

        processor.OnAssemblyCompilationCompleted(assemblies);

        var total = processor.ViolationsCount + processor.WarningsCount;
        if (total == 0)
        {
            EditorUtility.DisplayDialog(
                "Namespace Constraint Check",
                "All checks passed. No violations found.",
                "OK");
        }
        else
        {
            EditorUtility.DisplayDialog(
                "Namespace Constraint Check",
                $"Found {processor.ViolationsCount} violation(s) and {processor.WarningsCount} warning(s).\nCheck the Console for details.",
                "OK");
        }
    }

    [MenuItem(MenuPath, true)]
    private static bool RunConstraintCheckValidate()
    {
        return true;
    }
}

/// <summary>
/// 在编译时自动执行的命名空间约束检查。
/// 通过 Unity 的 CompilationPipeline 在每个程序集编译完成后检查源码，
/// 将违规报告为编译错误，从而在 IDE 中实时显示。
/// </summary>
[InitializeOnLoad]
public static class CompileTimeConstraintChecker
{
    private static readonly object LockObj = new();
    private static bool _isChecking;

    static CompileTimeConstraintChecker()
    {
        // 监听每个程序集编译完成事件
        CompilationPipeline.assemblyCompilationFinished += OnAssemblyCompilationFinished;
    }

    private static void OnAssemblyCompilationFinished(string assemblyPath, CompilerMessage[] messages)
    {
        // 跳过 Editor 程序集（Editor 程序集可以引用 UnityEngine）
        if (assemblyPath.Contains(".Editor."))
            return;

        // 避免重入
        lock (LockObj)
        {
            if (_isChecking)
                return;
            _isChecking = true;
        }

        try
        {
            var assemblyName = Path.GetFileNameWithoutExtension(assemblyPath);
            var processor = new NamespaceConstraintPostProcessor();
            processor.OnCompilationFinished(new List<string> { assemblyName });
        }
        finally
        {
            lock (LockObj)
            {
                _isChecking = false;
            }
        }
    }
}

} // namespace

#endif // UNITY_EDITOR
