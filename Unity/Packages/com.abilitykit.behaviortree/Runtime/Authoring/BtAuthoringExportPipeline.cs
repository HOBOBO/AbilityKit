using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using AbilityKit.BehaviorTree.Authoring.Model;
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
            string repositoryRoot)
        {
            if (trees == null) throw new ArgumentNullException(nameof(trees));
            if (registry == null) throw new ArgumentNullException(nameof(registry));
            var root = string.IsNullOrEmpty(repositoryRoot)
                ? Directory.GetCurrentDirectory()
                : repositoryRoot;

            var report = new List<ExportReportEntry>();
            var targets = exportTargetDirectories ?? Array.Empty<string>();

            foreach (var pair in trees)
            {
                var treeId = pair.Key;
                var document = pair.Value;

                if (targets.Count == 0)
                {
                    report.Add(new ExportReportEntry(treeId, "<none>", ExportStatus.SkippedNoTargets,
                        "项目目录资产未配置导出目标。"));
                    continue;
                }

                var json = TreeExporter.Export(document, registry, out var errors);
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
                    new("<manifest>", "<none>", ExportStatus.Error, "Manifest is null."),
                };
            }

            var sourceDirectory = ResolveDirectory(manifest.SourceDirectory, repositoryRoot);
            var report = new List<ExportReportEntry>();
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

                report.AddRange(ExportAll(
                    new[] { new KeyValuePair<string, AuthoringSourceDocument>(treeId, document) },
                    manifest.ExportTargets,
                    registry,
                    repositoryRoot));
            }
            return report;
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

    [System.Obsolete("Use AbilityKit.BehaviorTree.Authoring.ExportStatus.", false)]
    public enum BtExportStatus
    {
        Exported = 0,
        Unchanged = 1,
        Error = 2,
        SkippedNoTargets = 3,
    }

    [System.Obsolete("Use AbilityKit.BehaviorTree.Authoring.ExportReportEntry.", false)]
    public sealed class BtExportReportEntry
    {
        public string TreeId { get; }
        public string Target { get; }
        public BtExportStatus Status { get; }
        public string Message { get; }

        public BtExportReportEntry(string treeId, string target, BtExportStatus status, string message)
        {
            TreeId = treeId;
            Target = target;
            Status = status;
            Message = message ?? "";
        }
    }

    [System.Obsolete("Use AbilityKit.BehaviorTree.Authoring.ExportPipeline.", false)]
    public static class BtAuthoringExportPipeline
    {
#pragma warning disable CS0618
        public static List<BtExportReportEntry> ExportAll(
            IEnumerable<KeyValuePair<string, BtAuthoringSourceDocument>> trees,
            IReadOnlyList<string> exportTargetDirectories,
            BtNodeRegistry registry,
            string repositoryRoot)
        {
            var migratedTrees = new List<KeyValuePair<string, AuthoringSourceDocument>>();
            foreach (var pair in trees)
            {
                migratedTrees.Add(new KeyValuePair<string, AuthoringSourceDocument>(
                    pair.Key,
                    AuthoringCompatibility.ToModel(pair.Value)));
            }

            return AuthoringCompatibility.ToLegacy(ExportPipeline.ExportAll(
                migratedTrees,
                exportTargetDirectories,
                AbilityKit.BehaviorTree.Registry.NodeRegistry.FromLegacy(registry),
                repositoryRoot));
        }

        public static string ResolveDirectory(string target, string repositoryRoot)
            => ExportPipeline.ResolveDirectory(target, repositoryRoot);

        public static List<BtExportReportEntry> ExportProject(
            BtAuthoringProjectManifest manifest,
            BtNodeRegistry registry,
            string repositoryRoot)
            => AuthoringCompatibility.ToLegacy(ExportPipeline.ExportProject(
                AuthoringCompatibility.ToModel(manifest),
                AbilityKit.BehaviorTree.Registry.NodeRegistry.FromLegacy(registry),
                repositoryRoot));

        public static List<string> ValidateUniqueTreeIds(IEnumerable<string> treeIds)
            => ExportPipeline.ValidateUniqueTreeIds(treeIds);

        public static string HashContent(string content)
            => ExportPipeline.HashContent(content);
#pragma warning restore CS0618
    }
}
