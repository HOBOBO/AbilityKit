using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using Newtonsoft.Json;
using AbilityKit.Analyzer.Configuration;

namespace AbilityKit.Analyzer.Editor
{
    /// <summary>
    /// 在构建时检查命名空间约束，违规时阻止构建。
    /// </summary>
    public class NamespaceConstraintBuildChecker : IPreprocessBuildWithReport
    {
        private const string LogDirectoryName = "AbilityKit.Analyzer";

        public int callbackOrder => -1000;

        public void OnPreprocessBuild(BuildReport report)
        {
            WriteLog($"Build starting: {report.summary.platform}");

            var violations = CheckAllAssemblies();
            if (violations.Count > 0)
            {
                var errorMsg = $"Namespace constraint violations found in {violations.Count} assembly/namespace combination(s). See below for details.\n\n";
                errorMsg += string.Join("\n", violations);

                WriteErrorReport(errorMsg);
                WriteLog($"Found {violations.Count} violations - build will fail");

                throw new BuildFailedException(errorMsg);
            }

            WriteLog("No violations found - build can proceed");
        }

        private static List<string> CheckAllAssemblies()
        {
            var violations = new List<string>();
            var config = LoadConfig();
            var hasExplicitConstraints = config?.Constraints != null && config.Constraints.Count > 0;
            var appliesGlobalDefaults = config?.GlobalDefaults != null &&
                                        config.GlobalDefaults.Enabled &&
                                        config.GlobalDefaults.ApplyToUnlistedPackages;
            if (!hasExplicitConstraints && !appliesGlobalDefaults)
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

                        if (constraint.ForbiddenNamespaces == null || constraint.ForbiddenNamespaces.Count == 0)
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
                            var fileViolations = CheckFile(file, constraint);
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

        private static PackageConstraint GetEffectiveConstraint(PackageConstraintsConfig config, string asmName)
        {
            return config?.GetEffectiveConstraint(asmName);
        }

        private static List<string> CheckFile(string filePath, PackageConstraint constraint)
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
                        if (!string.IsNullOrEmpty(ns) && constraint.IsNamespaceForbidden(ns))
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

        private static bool IsExcluded(string path)
        {
            var n = path.Replace('\\', '/');
            return n.Contains("/Example") || n.Contains("/Examples") ||
                   n.Contains("/Test") || n.Contains("/Tests") ||
                   n.Contains("/~") || n.Contains("/Tests~");
        }

        private static PackageConstraintsConfig LoadConfig()
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
                        return JsonConvert.DeserializeObject<PackageConstraintsConfig>(json);
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
                var logPath = GetLogPath("build-check.log");
                Directory.CreateDirectory(Path.GetDirectoryName(logPath));
                File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss}] {msg}\n");
            }
            catch { }
        }

        private static void WriteErrorReport(string message)
        {
            var errorLogPath = GetLogPath("build-errors.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(errorLogPath));
            File.WriteAllText(errorLogPath, message);
        }

        private static string GetLogPath(string fileName)
        {
            return Path.Combine(GetProjectRoot(), "Logs", LogDirectoryName, fileName);
        }
    }
}
