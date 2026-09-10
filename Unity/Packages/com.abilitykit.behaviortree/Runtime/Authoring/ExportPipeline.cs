using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using AbilityKit.BehaviorTree.Authoring.Model;
using AbilityKit.BehaviorTree.Definition;
using AbilityKit.BehaviorTree.Execution;
using AbilityKit.BehaviorTree.Registry;
using AbilityKit.BehaviorTree.Serialization;

namespace AbilityKit.BehaviorTree.Authoring
{
    public enum ExportStatus
    {
        Exported = 0,
        Unchanged = 1,
        Error = 2,
        SkippedNoTargets = 3,
    }

    public sealed class ExportReportEntry
    {
        public string TreeId { get; }
        public string Target { get; }
        public ExportStatus Status { get; }
        public string Message { get; }

        public ExportReportEntry(string treeId, string target, ExportStatus status, string message)
        {
            TreeId = treeId;
            Target = target;
            Status = status;
            Message = message ?? "";
        }
    }

    public static class ExportPipeline
    {
        public static List<ExportReportEntry> ExportAll(
            IEnumerable<KeyValuePair<string, AuthoringSourceDocument>> trees,
            IReadOnlyList<string> exportTargetDirectories,
            NodeRegistry registry,
            string repositoryRoot,
            IEnumerable<KeyValuePair<string, AuthoringSourceDocument>>? resolverTrees = null)
        {
            if (trees == null) throw new ArgumentNullException(nameof(trees));
            if (registry == null) throw new ArgumentNullException(nameof(registry));
            var root = string.IsNullOrEmpty(repositoryRoot)
                ? Directory.GetCurrentDirectory()
                : repositoryRoot;

            var report = new List<ExportReportEntry>();
            var targets = exportTargetDirectories ?? Array.Empty<string>();
            var materializedTrees = new List<KeyValuePair<string, AuthoringSourceDocument>>(trees);
            var resolverDocuments = resolverTrees == null
                ? materializedTrees
                : new List<KeyValuePair<string, AuthoringSourceDocument>>(resolverTrees);
            var resolver = new DocumentTreeDefinitionResolver(resolverDocuments);

            foreach (var pair in materializedTrees)
            {
                var treeId = pair.Key;
                var document = pair.Value;

                if (targets.Count == 0)
                {
                    report.Add(new ExportReportEntry(treeId, "<none>", ExportStatus.SkippedNoTargets,
                        "项目目录资产未配置导出目标。"));
                    continue;
                }

                var json = TreeExporter.Export(document, registry, out var errors, resolver);
                if (json == null)
                {
                    foreach (var target in targets)
                    {
                        report.Add(new ExportReportEntry(treeId, target, ExportStatus.Error,
                            string.Join("; ", errors)));
                    }
                    continue;
                }

                foreach (var target in targets)
                {
                    report.Add(ExportOne(treeId, json, target, root));
                }
            }

            return report;
        }

        private static ExportReportEntry ExportOne(string treeId, string json, string target, string repositoryRoot)
        {
            string directory;
            try
            {
                directory = ResolveDirectory(target, repositoryRoot);
                Directory.CreateDirectory(directory);
            }
            catch (Exception ex)
            {
                return new ExportReportEntry(treeId, target, ExportStatus.Error,
                    "目标目录无法创建: " + ex.Message);
            }

            var filePath = Path.Combine(directory, treeId + ".json");
            try
            {
                if (File.Exists(filePath))
                {
                    var existing = File.ReadAllText(filePath);
                    if (string.Equals(existing, json, StringComparison.Ordinal))
                    {
                        return new ExportReportEntry(treeId, target, ExportStatus.Unchanged, "");
                    }
                }

                File.WriteAllText(filePath, json);
                return new ExportReportEntry(treeId, target, ExportStatus.Exported, filePath);
            }
            catch (Exception ex)
            {
                return new ExportReportEntry(treeId, target, ExportStatus.Error,
                    "写盘失败: " + ex.Message);
            }
        }

        public static string ResolveDirectory(string target, string repositoryRoot)
        {
            if (string.IsNullOrWhiteSpace(target))
                throw new ArgumentException("导出目标目录不能为空。", nameof(target));
            return Path.IsPathRooted(target)
                ? Path.GetFullPath(target)
                : Path.GetFullPath(Path.Combine(repositoryRoot, target));
        }

        public static List<ExportReportEntry> ExportProject(
            ProjectManifest manifest,
            NodeRegistry registry,
            string repositoryRoot)
        {
            if (manifest == null)
            {
                return new List<ExportReportEntry>
                {
                    new("<manifest>", "<none>", ExportStatus.Error, "项目清单为空。"),
                };
            }

            var sourceDirectory = ResolveDirectory(manifest.SourceDirectory, repositoryRoot);
            var report = new List<ExportReportEntry>();
            var documents = new List<KeyValuePair<string, AuthoringSourceDocument>>();
            foreach (var treeId in manifest.Trees)
            {
                var sourcePath = Path.Combine(sourceDirectory, treeId + ".json");
                if (!File.Exists(sourcePath))
                {
                    foreach (var target in manifest.ExportTargets)
                    {
                        report.Add(new ExportReportEntry(treeId, target, ExportStatus.Error,
                            "源文件不存在: " + sourcePath));
                    }
                    continue;
                }

                AuthoringSourceDocument document;
                try
                {
                    var sourceJson = File.ReadAllText(sourcePath);
                    document = manifest.SourceKind switch
                    {
                        SourceKind.AuthoringDocument => AuthoringJson.Load(sourceJson),
                        SourceKind.RuntimeDefinition => TreeExporter.Import(TreeJson.Load(sourceJson)),
                        _ => throw new InvalidOperationException($"不支持的行为树源类型: {manifest.SourceKind}."),
                    };
                }
                catch (Exception ex)
                {
                    foreach (var target in manifest.ExportTargets)
                    {
                        report.Add(new ExportReportEntry(treeId, target, ExportStatus.Error,
                            "源文件加载失败: " + ex.Message));
                    }
                    continue;
                }

                if (!string.Equals(document.Tree.TreeId, treeId, StringComparison.Ordinal))
                {
                    foreach (var target in manifest.ExportTargets)
                    {
                        report.Add(new ExportReportEntry(treeId, target, ExportStatus.Error,
                            $"清单 TreeId '{treeId}' 与源文档 TreeId '{document.Tree.TreeId}' 不一致。"));
                    }
                    continue;
                }

                documents.Add(new KeyValuePair<string, AuthoringSourceDocument>(treeId, document));
            }
            report.AddRange(ExportAll(
                documents,
                manifest.ExportTargets,
                registry,
                repositoryRoot));
            return report;
        }

        private sealed class DocumentTreeDefinitionResolver : TreeDefinitionResolver
        {
            private readonly Dictionary<string, TreeDefinition> _definitions =
                new(StringComparer.Ordinal);

            public DocumentTreeDefinitionResolver(
                IEnumerable<KeyValuePair<string, AuthoringSourceDocument>> documents)
            {
                foreach (var pair in documents)
                {
                    var document = pair.Value;
                    if (document?.Tree == null) continue;
                    var treeId = document.Tree.TreeId;
                    if (string.IsNullOrWhiteSpace(treeId) || _definitions.ContainsKey(treeId)) continue;
                    _definitions.Add(treeId, TreeExporter.ToRuntimeDefinition(document));
                }
            }

            public bool TryResolve(string treeId, out TreeDefinition definition)
            {
                if (_definitions.TryGetValue(treeId, out var source))
                {
                    definition = source.DeepClone();
                    return true;
                }

                definition = null!;
                return false;
            }
        }

        public static List<string> ValidateUniqueTreeIds(IEnumerable<string> treeIds)
        {
            var errors = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var treeId in treeIds)
            {
                if (string.IsNullOrWhiteSpace(treeId))
                {
                    errors.Add("存在空 TreeId。");
                    continue;
                }
                if (!seen.Add(treeId))
                {
                    errors.Add($"TreeId '{treeId}' 重复注册。");
                }
            }
            return errors;
        }

        public static string HashContent(string content)
        {
            using var sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(content ?? "")));
        }
    }

}
