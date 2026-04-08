using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using Newtonsoft.Json;

namespace AbilityKit.Analyzer.Editor
{
    /// <summary>
    /// 在编辑器检查命名空间约束，违规时显示编译错误。
    /// </summary>
    public class NamespaceConstraintEditorChecker : MonoBehaviour
    {
        private static readonly string LogPath = @"C:\analyzer_editor_check.log";

        /// <summary>
        /// 手动触发检查，可在菜单中调用
        /// </summary>
        [MenuItem("AbilityKit/Analyzer/检查命名空间约束")]
        public static void CheckConstraints()
        {
            var violations = CheckAllAssemblies();
            
            if (violations.Count == 0)
            {
                Debug.Log("[AbilityKit.Analyzer] 未发现命名空间约束违规");
                return;
            }

            // 按文件分组显示错误
            var byFile = new Dictionary<string, List<string>>();
            foreach (var v in violations)
            {
                var parts = v.Split(new[] { " in " }, StringSplitOptions.None);
                if (parts.Length < 2) continue;
                
                var message = parts[0];
                var file = parts[1];
                
                if (!byFile.ContainsKey(file))
                    byFile[file] = new List<string>();
                byFile[file].Add(message);
            }

            // 发送编译错误到 Unity Console
            foreach (var kvp in byFile)
            {
                foreach (var msg in kvp.Value)
                {
                    var fullMsg = $"AK1001 [Namespace Constraint] {msg}";
                    Debug.LogError(fullMsg, UnityEngine.Object.FindObjectOfType<UnityEditor.SceneView>());
                }
            }

            Debug.LogError($"[AbilityKit.Analyzer] 发现 {violations.Count} 个命名空间约束违规，请查看上方错误详情");
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

            var forbidden = new HashSet<string>(
                config.GlobalDefaults.ForbiddenNamespaces ?? new List<string>(),
                StringComparer.OrdinalIgnoreCase);

            if (forbidden.Count == 0)
            {
                WriteLog("No forbidden namespaces defined");
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

                        var asmDir = Path.GetDirectoryName(asmdef);
                        var sourceFiles = Directory.GetFiles(asmDir, "*.cs", SearchOption.AllDirectories)
                            .Where(f => !IsExcluded(f))
                            .ToList();

                        foreach (var file in sourceFiles)
                        {
                            var fileViolations = CheckFile(file, forbidden);
                            foreach (var v in fileViolations)
                            {
                                var msg = $"Forbidden namespace '{v.Namespace}' in {asmName} (line {v.Line})";
                                violations.Add($"{msg} in {GetRelativePath(file)}");
                                WriteLog($"VIOLATION: {msg} in {file}");
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

        private static List<NamespaceViolation> CheckFile(string filePath, HashSet<string> forbidden)
        {
            var violations = new List<NamespaceViolation>();
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
                            violations.Add(new NamespaceViolation { Namespace = ns, Line = i + 1 });
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

        private struct NamespaceViolation
        {
            public string Namespace;
            public int Line;
        }
    }
}
