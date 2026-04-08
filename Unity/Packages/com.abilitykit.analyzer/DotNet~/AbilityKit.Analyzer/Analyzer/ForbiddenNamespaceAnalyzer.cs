using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using AbilityKit.Analyzer.Config;

namespace AbilityKit.Analyzer
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class ForbiddenNamespaceAnalyzer : DiagnosticAnalyzer
    {
        private PackageConstraintsConfig _config;
        private bool _isInitialized;
        private readonly object _initLock = new object();

        static ForbiddenNamespaceAnalyzer()
        {
            try
            {
                var logDir = Path.Combine(Path.GetTempPath(), "AbilityKit.Analyzer");
                Directory.CreateDirectory(logDir);
                var logFile = Path.Combine(logDir, "analyzer.log");
                File.AppendAllText(logFile, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] STATIC Ctor - Assembly loaded\n");
            }
            catch { }
        }

        private static void WriteLog(string message)
        {
            try
            {
                var logDir = Path.Combine(Path.GetTempPath(), "AbilityKit.Analyzer");
                Directory.CreateDirectory(logDir);
                var logFile = Path.Combine(logDir, "analyzer.log");
                File.AppendAllText(logFile, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}\n");
            }
            catch
            {
                // 静默忽略日志写入失败
            }
        }

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(
                DiagnosticRules.ForbiddenNamespaceRule,
                DiagnosticRules.ForbiddenAssemblyRule,
                DiagnosticRules.UnmatchedConstraintPackageRule);

        public override void Initialize(AnalysisContext context)
        {
            WriteLog("Initialize called");
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterCompilationStartAction(OnCompilationStart);
        }

        private void OnCompilationStart(CompilationStartAnalysisContext compilationContext)
        {
            EnsureInitialized();

            var compilation = compilationContext.Compilation;
            var assemblyName = compilation.AssemblyName ?? string.Empty;

            WriteLog($"=== Compilation started: {assemblyName} ===");
            WriteLog($"  GlobalDefaults.Enabled: {_config?.GlobalDefaults?.Enabled}");
            WriteLog($"  GlobalDefaults.ApplyToUnlisted: {_config?.GlobalDefaults?.ApplyToUnlistedPackages}");
            WriteLog($"  Constraints count: {_config?.Constraints?.Count ?? 0}");

            // 打印所有约束
            if (_config?.Constraints != null)
            {
                foreach (var kvp in _config.Constraints)
                {
                    WriteLog($"    [{kvp.Key}] -> PackageName={kvp.Value?.PackageName}, IsEnabled={kvp.Value?.IsEnabled}");
                }
            }

            // [临时调试] 暂时跳过白名单，直接使用全局配置对所有包生效
            PackageConstraint constraint = null;
            if (_config?.GlobalDefaults != null)
            {
                var globalNs = _config.GlobalDefaults.ForbiddenNamespaces ?? new List<string>();
                var globalAsm = _config.GlobalDefaults.ForbiddenAssemblies ?? new List<string>();
                
                constraint = new PackageConstraint
                {
                    PackageName = assemblyName,
                    // 临时添加 UnityEngine 为禁止命名空间用于测试（如果没有配置的话）
                    ForbiddenNamespaces = globalNs.Count > 0 ? globalNs : new List<string> { "UnityEngine" },
                    ForbiddenAssemblies = globalAsm,
                    IsEnabled = true, // 临时强制启用
                    Severity = _config.GlobalDefaults.Severity,
                    CheckUsingAliases = _config.GlobalDefaults.CheckUsingAliases
                };
            }
            // else: 不创建约束，让分析器跳过

            if (constraint == null)
            {
                WriteLog($"  No constraint for {assemblyName} (GetEffectiveConstraint returned null)");
                return;
            }
            if (!constraint.IsEnabled)
            {
                WriteLog($"  Constraint disabled for {assemblyName}");
                return;
            }

            WriteLog($"  Constraint found: forbidden NS count: {constraint.ForbiddenNamespaces?.Count ?? 0}");

            var forbiddenNamespaces = new HashSet<string>(
                constraint.ForbiddenNamespaces ?? new List<string>(),
                StringComparer.OrdinalIgnoreCase);

            var violations = new List<(string ns, string file)>();

            compilationContext.RegisterSyntaxTreeAction(syntaxTreeContext =>
            {
                var filePath = syntaxTreeContext.Tree.FilePath;
                if (IsExcludedPath(filePath))
                    return;

                var root = syntaxTreeContext.Tree.GetRoot(syntaxTreeContext.CancellationToken);
                var usingCount = 0;
                var violationCount = 0;

                foreach (var usingDirective in root.DescendantNodes().OfType<UsingDirectiveSyntax>())
                {
                    var nameSyntax = usingDirective.Name;
                    if (nameSyntax == null)
                        continue;

                    usingCount++;
                    var name = nameSyntax.ToString();
                    if (IsForbiddenNamespace(name, forbiddenNamespaces))
                    {
                        violationCount++;
                        violations.Add((name, filePath));
                        var location = usingDirective.Name.GetLocation();
                        var diagnostic = Diagnostic.Create(
                            DiagnosticRules.ForbiddenNamespaceRule, location,
                            name, compilation.AssemblyName ?? "unknown");
                        WriteLog($"REPORTING: {diagnostic.Id}, Severity={diagnostic.Severity}, Location={location.GetLineSpan().Path}");
                        syntaxTreeContext.ReportDiagnostic(diagnostic);
                        WriteLog($"VIOLATION: {name} in {filePath}");
                    }
                }

                if (usingCount > 0)
                    WriteLog($"Tree {Path.GetFileName(filePath)}: {usingCount} using, {violationCount} violations");
            });

            // 在编译结束时，如果存在违规则阻止编译
            compilationContext.RegisterCompilationEndAction(endContext =>
            {
                if (violations.Count > 0)
                {
                    WriteLog($"COMPILATION ERROR: Found {violations.Count} violation(s) in {assemblyName}");
                    foreach (var (ns, file) in violations)
                    {
                        WriteLog($"  - {ns} in {file}");
                    }

                    // 创建编译错误：使用无效的类型引用来强制编译失败
                    // 这会创建一个真正的编译错误而不是分析器诊断
                    var errorMessage = $"Forbidden namespace violation(s) found in assembly '{assemblyName}': {violations.Count} violation(s). See diagnostic messages for details.";
                    
                    // 方法1: 尝试抛出编译错误
                    var errorDescriptor = new DiagnosticDescriptor(
                        id: "AK_COMPILE_ERROR",
                        title: "Namespace constraint violation - BUILD BLOCKED",
                        messageFormat: errorMessage,
                        category: "AbilityKit.Framework",
                        defaultSeverity: DiagnosticSeverity.Error,
                        isEnabledByDefault: true);

                    // 注册一个永远无法编译的符号引用来强制编译失败
                    var errorLocation = Location.Create(
                        compilationContext.Compilation.AssemblyName + ".dll",
                        new TextSpan(0, 1),
                        new LinePositionSpan(new LinePosition(0, 0), new LinePosition(0, 1)));

                    var compileError = Diagnostic.Create(
                        errorDescriptor,
                        errorLocation);

                    endContext.ReportDiagnostic(compileError);
                    
                    WriteLog($"COMPILATION BLOCKED: {errorMessage}");
                }
            });
        }

        private static bool IsExcludedPath(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                return false;

            var normalized = filePath.Replace('\\', '/');
            return normalized.Contains("/Example/") ||
                   normalized.Contains("/Examples/") ||
                   normalized.Contains("/Tests/") ||
                   normalized.Contains("/Test/");
        }

        private void EnsureInitialized()
        {
            if (_isInitialized)
                return;

            lock (_initLock)
            {
                if (_isInitialized)
                    return;

                try
                {
                    var projectDir = FindProjectRootBySearching();
                    string configPath = null;

                    if (!string.IsNullOrEmpty(projectDir))
                    {
                        configPath = FindConfigFile(projectDir);
                    }

                    if (!string.IsNullOrEmpty(configPath) && File.Exists(configPath))
                    {
                        WriteLog($"Loading config from: {configPath}");
                        _config = LoadConfigFromFile(configPath);
                    }
                    else
                    {
                        WriteLog($"Config file not found at {configPath}, using hardcoded defaults");
                        _config = CreateHardcodedConfig();
                    }
                }
                catch (Exception ex)
                {
                    WriteLog($"Error loading config: {ex.Message}");
                    _config = CreateHardcodedConfig();
                }

                WriteLog($"Config initialized: GlobalDefaults.Enabled={_config?.GlobalDefaults?.Enabled}");
                _isInitialized = true;
            }
        }

        private static PackageConstraintsConfig LoadConfigFromFile(string path)
        {
            var json = File.ReadAllText(path);
            WriteLog($"Attempting to parse config with Newtonsoft.Json from: {path}");

            PackageConstraintsConfig config;
            try
            {
                var settings = new Newtonsoft.Json.JsonSerializerSettings
                {
                    MissingMemberHandling = Newtonsoft.Json.MissingMemberHandling.Ignore,
                    NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore
                };
                config = Newtonsoft.Json.JsonConvert.DeserializeObject<PackageConstraintsConfig>(json, settings);
                WriteLog($"Config parsed: Constraints count={config?.Constraints?.Count ?? 0}, ApplyToUnlisted={config?.GlobalDefaults?.ApplyToUnlistedPackages}");
            }
            catch (Exception ex)
            {
                WriteLog($"Error parsing config: {ex.Message}");
                config = CreateHardcodedConfig();
            }

            if (config == null)
                config = CreateHardcodedConfig();

            return config;
        }

        private static PackageConstraintsConfig CreateHardcodedConfig()
        {
            var config = new PackageConstraintsConfig
            {
                GlobalDefaults = new GlobalConstraintDefaults
                {
                    Enabled = false,
                    ForbiddenNamespaces = new List<string>(),
                    ForbiddenAssemblies = new List<string>(),
                    Severity = AKDiagnosticSeverity.Error,
                    CheckUsingAliases = true,
                    ApplyToUnlistedPackages = false
                }
            };

            WriteLog("Using whitelist config mode");
            return config;
        }

        private static string FindProjectRootBySearching()
        {
            WriteLog("=== Finding project root ===");

            var possibleRoots = new[]
            {
                Path.GetDirectoryName(typeof(ForbiddenNamespaceAnalyzer).Assembly.Location),
                @"C:\Workspace\gitProject\AbilityKit\Unity",
                Environment.CurrentDirectory,
            };

            foreach (var root in possibleRoots)
            {
                var configPath = Path.Combine(root, "Assets", "Config", "PackageConstraints.json");
                WriteLog($"Checking: {configPath}");
                if (File.Exists(configPath))
                {
                    WriteLog($"Found config at: {configPath}, root: {root}");
                    return root;
                }
            }

            var assemblyLocation = typeof(ForbiddenNamespaceAnalyzer).Assembly.Location;
            WriteLog($"Assembly location: {assemblyLocation}");

            if (!string.IsNullOrEmpty(assemblyLocation))
            {
                var dir = Path.GetDirectoryName(assemblyLocation);
                for (int i = 0; i < 20; i++)
                {
                    var configPath = Path.Combine(dir, "Assets", "Config", "PackageConstraints.json");
                    WriteLog($"  Level {i}: checking {configPath}");
                    if (File.Exists(configPath))
                    {
                        WriteLog($"Found project root from assembly: {dir}");
                        return dir;
                    }
                    var parent = Directory.GetParent(dir);
                    if (parent == null) break;
                    dir = parent.FullName;
                }
            }

            WriteLog($"Trying current directory: {Directory.GetCurrentDirectory()}");
            var currentDir = Directory.GetCurrentDirectory();
            for (int i = 0; i < 10; i++)
            {
                var configPath = Path.Combine(currentDir, "Assets", "Config", "PackageConstraints.json");
                if (File.Exists(configPath))
                {
                    WriteLog($"Found project root from CWD: {currentDir}");
                    return currentDir;
                }
                var parent = Directory.GetParent(currentDir);
                if (parent == null) break;
                currentDir = parent.FullName;
            }

            WriteLog("Could not find project root");
            return null;
        }

        private static string FindConfigFile(string projectRoot)
        {
            var searchPaths = new[]
            {
                Path.Combine(projectRoot, "Assets", "Config", "PackageConstraints.json"),
                Path.Combine(projectRoot, "Packages", "com.abilitykit.analyzer", "Config", "PackageConstraints.json")
            };

            foreach (var path in searchPaths)
            {
                if (File.Exists(path))
                    return path;
            }
            return null;
        }

        private static bool IsForbiddenNamespace(string namespaceName, HashSet<string> forbiddenNamespaces)
        {
            if (string.IsNullOrEmpty(namespaceName))
                return false;

            foreach (var forbidden in forbiddenNamespaces)
            {
                if (namespaceName == forbidden || namespaceName.StartsWith(forbidden + "."))
                    return true;
            }
            return false;
        }
    }
}