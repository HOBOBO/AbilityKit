using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Newtonsoft.Json;

namespace AbilityKit.Analyzer.Editor
{
    /// <summary>
    /// 在编译时执行命名空间约束检查，通过 CompilerMessage 报告错误。
    /// </summary>
    [System.Reflection.Obfuscation(Exclude = true)]
    public static class NamespaceConstraintChecker
    {
        private static readonly List<CompilerError> _errors = new();
        private static readonly object _lock = new();
        private static readonly string LogPath = @"C:\analyzer_debug.log";

        static NamespaceConstraintChecker()
        {
            WriteLog("Static ctor called");
        }

        private static void WriteLog(string msg)
        {
            try { File.AppendAllText(LogPath, $"[{DateTime.Now:HH:mm:ss}] {msg}\n"); } catch { }
        }

        public static void CheckAssembly(string assemblyName, string assemblyPath)
        {
            lock (_lock)
            {
                _errors.Clear();
            }

            WriteLog($"Checking assembly: {assemblyName} at {assemblyPath}");

            var config = LoadConfig();
            if (config == null || !config.GlobalDefaults.Enabled)
            {
                WriteLog("No config or disabled");
                return;
            }

            var constraint = GetConstraint(config, assemblyName);
            if (constraint == null || !constraint.IsEnabled)
            {
                WriteLog($"No constraint for {assemblyName}");
                return;
            }

            var sourceFiles = CollectSourceFiles(assemblyPath, assemblyName);
            foreach (var file in sourceFiles)
            {
                CheckFile(file, constraint, assemblyName);
            }

            if (_errors.Count > 0)
            {
                WriteLog($"Found {_errors.Count} violations in {assemblyName}");
            }
        }

        internal static CompilerError[] GetErrors()
        {
            lock (_lock)
            {
                var result = _errors.ToArray();
                _errors.Clear();
                return result;
            }
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

        private static ForbiddenConstraint GetConstraint(ConfigData config, string assemblyName)
        {
            if (config.Constraints != null && config.Constraints.TryGetValue(assemblyName, out var c) && c.IsEnabled)
                return c;

            if (!config.GlobalDefaults.Enabled)
                return null;

            return new ForbiddenConstraint
            {
                ForbiddenNamespaces = config.GlobalDefaults.ForbiddenNamespaces ?? new List<string>(),
                IsEnabled = true
            };
        }

        private static List<string> CollectSourceFiles(string assemblyPath, string assemblyName)
        {
            var files = new List<string>();
            var baseDir = GetProjectRoot();

            var searchPaths = new[] {
                Path.Combine(baseDir, "Packages"),
                Path.Combine(baseDir, "Assets")
            };

            foreach (var basePath in searchPaths)
            {
                if (!Directory.Exists(basePath)) continue;

                var asmdefFiles = Directory.GetFiles(basePath, "*.asmdef", SearchOption.AllDirectories);
                foreach (var asmdef in asmdefFiles)
                {
                    try
                    {
                        var json = File.ReadAllText(asmdef);
                        var doc = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);
                        if (doc.TryGetValue("name", out var nameObj) && nameObj?.ToString() == assemblyName)
                        {
                            var asmdefDir = Path.GetDirectoryName(asmdef);
                            if (Directory.Exists(asmdefDir))
                            {
                                files.AddRange(Directory.GetFiles(asmdefDir, "*.cs", SearchOption.AllDirectories)
                                    .Where(f => !IsExcluded(f)));
                            }
                            break;
                        }
                    }
                    catch { }
                }
            }

            return files;
        }

        private static bool IsExcluded(string path)
        {
            var n = path.Replace('\\', '/');
            return n.Contains("/Example") || n.Contains("/Examples") || n.Contains("/Test") || n.Contains("/Tests");
        }

        private static void CheckFile(string filePath, ForbiddenConstraint constraint, string assemblyName)
        {
            try
            {
                var lines = File.ReadAllLines(filePath);
                for (int i = 0; i < lines.Length; i++)
                {
                    var line = lines[i];
                    if (line.TrimStart().StartsWith("using "))
                    {
                        var ns = ExtractNamespace(line);
                        if (!string.IsNullOrEmpty(ns) && IsForbidden(ns, constraint.ForbiddenNamespaces))
                        {
                            var msg = $"AK1001 Forbidden namespace '{ns}' in {assemblyName} at {filePath}:{i + 1}";
                            WriteLog($"VIOLATION: {msg}");

                            lock (_lock)
                            {
                                _errors.Add(new CompilerError
                                {
                                    file = filePath,
                                    line = i + 1,
                                    errorCode = "AK1001",
                                    message = $"Forbidden namespace '{ns}' in assembly '{assemblyName}'"
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                WriteLog($"Error checking {filePath}: {ex.Message}");
            }
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

        private static bool IsForbidden(string ns, List<string> forbidden)
        {
            foreach (var f in forbidden)
            {
                if (ns == f || ns.StartsWith(f + ".")) return true;
            }
            return false;
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
    }

    internal class CompilerError
    {
        public string file { get; set; }
        public int line { get; set; }
        public string errorCode { get; set; }
        public string message { get; set; }
    }

    internal class ForbiddenConstraint
    {
        public List<string> ForbiddenNamespaces { get; set; } = new();
        public bool IsEnabled { get; set; }
    }

    internal class ConfigData
    {
        public GlobalDefaults GlobalDefaults { get; set; } = new();
        public Dictionary<string, ForbiddenConstraint> Constraints { get; set; } = new();
    }

    internal class GlobalDefaults
    {
        public bool Enabled { get; set; }
        public List<string> ForbiddenNamespaces { get; set; } = new();
    }
}
