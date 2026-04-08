using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using Newtonsoft.Json;

namespace AbilityKit.Analyzer.Editor
{
    /// <summary>
    /// 在构建时检查命名空间约束，违规时阻止构建。
    /// </summary>
    public class NamespaceConstraintBuildChecker : IPreprocessBuildWithReport
    {
        private static readonly string LogPath = @"C:\analyzer_build_check.log";
        private static readonly string ErrorLogPath = @"C:\analyzer_build_errors.txt";

        public int callbackOrder => -1000;

        public void OnPreprocessBuild(BuildReport report)
        {
            WriteLog($"Build starting: {report.summary.platform}");

            var violations = CheckAllAssemblies();
            if (violations.Count > 0)
            {
                var errorMsg = $"Namespace constraint violations found in {violations.Count} assembly/namespace combination(s). See below for details.\n\n";
                errorMsg += string.Join("\n", violations);

                File.WriteAllText(ErrorLogPath, errorMsg);
                WriteLog($"Found {violations.Count} violations - build will fail");

                throw new BuildFailedException(errorMsg);
            }

            WriteLog("No violations found - build can proceed");
        }

        private static List<string> CheckAllAssemblies()
        {
            var violations = new List<string>();
            var config = LoadConfig();
            if (config == null || !config.GlobalDefaults.Enabled)
            {
                WriteLog("Config not found or disabled");
                return violations;
            }

            var projectRoot = GetProjectRoot();
            var searchPaths = new[]
            {
                Path.Combine(projectRoot, "Packages"),
                Path.Combine(projectRoot, "Assets")
            };

            foreach (var basePath in searchPaths)
            {
                if (!Directory.Exists(basePath)) continue;

                var asmdefs = Directory.GetFiles(basePath, "*.asmdef", SearchOption.AllDirectories);
                foreach (var asmdef in asmdefs)
                {
                    try
                    {
                        var json = File.ReadAllText(asmdef);
                        var doc = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);
                        if (doc == null) continue;

                        var asmName = doc["name"]?.ToString();
                        if (string.IsNullOrEmpty(asmName)) continue;

                        // Skip Editor assemblies
                        if (asmName.Contains(".Editor")) continue;

                        // 白名单模式：检查是否应该检查此程序集
                        var constraint = GetEffectiveConstraint(config, asmName);
                        if (constraint == null)
                        {
                            WriteLog($"[Whitelist] Skipping {asmName} - not in whitelist");
                            continue;
                        }
                        if (!constraint.IsEnabled)
                        {
                            WriteLog($"[Whitelist] Skipping {asmName} - disabled in config");
                            continue;
                        }

                        var forbidden = new HashSet<string>(constraint.ForbiddenNamespaces ?? new List<string>(),
                            StringComparer.OrdinalIgnoreCase);
                        if (forbidden.Count == 0)
                        {
                            WriteLog($"[Whitelist] Skipping {asmName} - no forbidden namespaces");
                            continue;
                        }

                        var asmDir = Path.GetDirectoryName(asmdef);
                        var sourceFiles = Directory.GetFiles(asmDir, "*.cs", SearchOption.AllDirectories)
                            .Where(f => !IsExcluded(f))
                            .ToList();

                        foreach (var file in sourceFiles)
                        {
                            var fileViolations = CheckFile(file, forbidden);
                            foreach (var v in fileViolations)
                            {
                                var msg = $"AK1001 [{asmName}] {v} in {GetRelativePath(file)}";
                                violations.Add(msg);
                                WriteLog($"VIOLATION: {msg}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        WriteLog($"Error processing {asmdef}: {ex.Message}");
                    }
                }
            }

            return violations;
        }

        private static ConstraintEntry GetEffectiveConstraint(ConfigData config, string asmName)
        {
            // [临时调试] 跳过白名单，直接对所有包生效

            // 1. 检查 Constraints 中是否有精确匹配
            if (config.Constraints.TryGetValue(asmName, out var constraint))
            {
                return constraint;
            }

            // 2. 检查通配符匹配
            foreach (var key in config.Constraints.Keys)
            {
                if (key.Contains("*"))
                {
                    var pattern = key.Replace(".", "\\.").Replace("*", ".*");
                    if (System.Text.RegularExpressions.Regex.IsMatch(asmName, $"^{pattern}$",
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                    {
                        return config.Constraints[key];
                    }
                }
            }

            // [临时调试] 不再检查 ApplyToUnlistedPackages，直接使用全局默认
            // 回退到全局默认
            return new ConstraintEntry
            {
                IsEnabled = config.GlobalDefaults.Enabled,
                ForbiddenNamespaces = config.GlobalDefaults.ForbiddenNamespaces
            };
        }

        private static List<string> CheckFile(string filePath, HashSet<string> forbidden)
        {
            var violations = new List<string>();
            try
            {
                var lines = File.ReadAllLines(filePath);
                for (int i = 0; i < lines.Length; i++)
                {
                    var line = lines[i].Trim();
                    if (line.StartsWith("using "))
                    {
                        var ns = ExtractNamespace(line);
                        if (!string.IsNullOrEmpty(ns) && IsForbidden(ns, forbidden))
                        {
                            violations.Add($"Forbidden namespace '{ns}' at line {i + 1}");
                        }
                    }
                }
            }
            catch { }
            return violations;
        }

        private static string ExtractNamespace(string line)
        {
            var start = line.IndexOf("using ");
            if (start < 0) return null;
            var nsStart = start + "using ".Length;
            var semi = line.IndexOf(';', nsStart);
            if (semi < 0) return null;
            var ns = line.Substring(nsStart, semi - nsStart).Trim();
            return ns.StartsWith("global") ? null : ns;
        }

        private static bool IsForbidden(string ns, HashSet<string> forbidden)
        {
            foreach (var f in forbidden)
            {
                if (ns == f || ns.StartsWith(f + ".")) return true;
            }
            return false;
        }

        private static bool IsExcluded(string path)
        {
            var n = path.Replace('\\', '/');
            return n.Contains("/Example") || n.Contains("/Examples") ||
                   n.Contains("/Test") || n.Contains("/Tests") ||
                   n.Contains("/~") || n.Contains("/Tests~");
        }

        private static ConfigData LoadConfig()
        {
            var paths = new[]
            {
                Path.Combine(GetProjectRoot(), "Assets/Config/PackageConstraints.json"),
                Path.Combine(GetProjectRoot(), "Packages/com.abilitykit.analyzer/Config/PackageConstraints.json"),
            };

            foreach (var p in paths)
            {
                if (File.Exists(p))
                {
                    try
                    {
                        var json = File.ReadAllText(p);
                        return JsonConvert.DeserializeObject<ConfigData>(json);
                    }
                    catch { }
                }
            }
            return null;
        }

        private static string GetProjectRoot()
        {
            var dir = AppDomain.CurrentDomain.BaseDirectory;
            for (int i = 0; i < 10; i++)
            {
                if (Directory.Exists(Path.Combine(dir, "Assets")) && Directory.Exists(Path.Combine(dir, "Packages")))
                    return dir;
                var parent = Directory.GetParent(dir);
                if (parent == null) break;
                dir = parent.FullName;
            }
            return Directory.GetCurrentDirectory();
        }

        private static string GetRelativePath(string fullPath)
        {
            var root = GetProjectRoot();
            return fullPath.StartsWith(root) ? fullPath.Substring(root.Length + 1) : fullPath;
        }

        private static void WriteLog(string msg)
        {
            try
            {
                File.AppendAllText(LogPath, $"[{DateTime.Now:HH:mm:ss}] {msg}\n");
            }
            catch { }
        }
    }

    internal class ConfigData
    {
        public GlobalDefaults GlobalDefaults { get; set; } = new();
        public Dictionary<string, ConstraintEntry> Constraints { get; set; } = new();
    }

    internal class GlobalDefaults
    {
        public bool Enabled { get; set; }
        public List<string> ForbiddenNamespaces { get; set; } = new();
        public bool ApplyToUnlistedPackages { get; set; } = true;
    }

    internal class ConstraintEntry
    {
        public bool IsEnabled { get; set; } = true;
        public List<string> ForbiddenNamespaces { get; set; } = new();
    }
}
