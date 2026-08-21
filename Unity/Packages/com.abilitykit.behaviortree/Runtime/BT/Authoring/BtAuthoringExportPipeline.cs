using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace AbilityKit.BehaviorTree.Authoring
{
    /// <summary>导出目标状态。</summary>
    public enum BtExportStatus
    {
        Exported = 0,
        Unchanged = 1,
        Error = 2,
        SkippedNoTargets = 3,
    }

    /// <summary>单树单目标的导出结果。</summary>
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

    /// <summary>
    /// 批量导出管线（纯 C#）：授权文档集 -> 校验 -> JSON -> 全部导出目标扇出，内容一致跳过写盘。
    /// 编辑器壳只负责收集资产与展示报告；目标目录相对仓库根解析。
    /// </summary>
    public static class BtAuthoringExportPipeline
    {
        /// <summary>
        /// 导出一批树到全部目标。校验失败的树对每个目标记 Error（不清空旧产物）；无目标记 SkippedNoTargets。
        /// </summary>
        public static List<BtExportReportEntry> ExportAll(
            IEnumerable<KeyValuePair<string, BtAuthoringSourceDocument>> trees,
            IReadOnlyList<string> exportTargetDirectories,
            BtNodeRegistry registry,
            string repositoryRoot)
        {
            if (trees == null) throw new ArgumentNullException(nameof(trees));
            if (registry == null) throw new ArgumentNullException(nameof(registry));
            var root = string.IsNullOrEmpty(repositoryRoot)
                ? Directory.GetCurrentDirectory()
                : repositoryRoot;

            var report = new List<BtExportReportEntry>();
            var targets = exportTargetDirectories ?? Array.Empty<string>();

            foreach (var pair in trees)
            {
                var treeId = pair.Key;
                var document = pair.Value;

                if (targets.Count == 0)
                {
                    report.Add(new BtExportReportEntry(treeId, "<none>", BtExportStatus.SkippedNoTargets,
                        "项目目录资产未配置导出目标。"));
                    continue;
                }

                var json = BtTreeExporter.Export(document, registry, out var errors);
                if (json == null)
                {
                    foreach (var target in targets)
                    {
                        report.Add(new BtExportReportEntry(treeId, target, BtExportStatus.Error,
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

        private static BtExportReportEntry ExportOne(string treeId, string json, string target, string repositoryRoot)
        {
            string directory;
            try
            {
                directory = ResolveDirectory(target, repositoryRoot);
                Directory.CreateDirectory(directory);
            }
            catch (Exception ex)
            {
                return new BtExportReportEntry(treeId, target, BtExportStatus.Error,
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
                        return new BtExportReportEntry(treeId, target, BtExportStatus.Unchanged, "");
                    }
                }

                File.WriteAllText(filePath, json);
                return new BtExportReportEntry(treeId, target, BtExportStatus.Exported, filePath);
            }
            catch (Exception ex)
            {
                return new BtExportReportEntry(treeId, target, BtExportStatus.Error,
                    "写盘失败: " + ex.Message);
            }
        }

        /// <summary>相对仓库根解析目录；绝对路径原样。</summary>
        public static string ResolveDirectory(string target, string repositoryRoot)
        {
            if (string.IsNullOrWhiteSpace(target))
                throw new ArgumentException("导出目标目录不能为空。", nameof(target));
            return Path.IsPathRooted(target)
                ? Path.GetFullPath(target)
                : Path.GetFullPath(Path.Combine(repositoryRoot, target));
        }

        /// <summary>
        /// 清单驱动的 headless 导出：从源目录按 TreeId 读取运行时 JSON，转授权文档后扇出到全部目标。
        /// 无 Unity 依赖，CLI/CI/AI 脚本可直接调用；缺源文件对每个目标记 Error，缺目标记 SkippedNoTargets。
        /// </summary>
        public static List<BtExportReportEntry> ExportProject(
            BtAuthoringProjectManifest manifest,
            BtNodeRegistry registry,
            string repositoryRoot)
        {
            if (manifest == null)
            {
                return new List<BtExportReportEntry>
                {
                    new BtExportReportEntry("<manifest>", "<none>", BtExportStatus.Error, "Manifest is null."),
                };
            }

            var sourceDirectory = ResolveDirectory(manifest.SourceDirectory, repositoryRoot);
            var report = new List<BtExportReportEntry>();
            foreach (var treeId in manifest.Trees)
            {
                var sourcePath = Path.Combine(sourceDirectory, treeId + ".json");
                if (!File.Exists(sourcePath))
                {
                    foreach (var target in manifest.ExportTargets)
                    {
                        report.Add(new BtExportReportEntry(treeId, target, BtExportStatus.Error,
                            "源文件不存在: " + sourcePath));
                    }
                    continue;
                }

                var definition = BtTreeJson.Load(File.ReadAllText(sourcePath));
                var document = BtTreeExporter.Import(definition);
                report.AddRange(ExportAll(
                    new[] { new KeyValuePair<string, BtAuthoringSourceDocument>(treeId, document) },
                    manifest.ExportTargets,
                    registry,
                    repositoryRoot));
            }
            return report;
        }

        /// <summary>TreeId 唯一性校验；返回错误列表（空 = 通过）。</summary>
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

        /// <summary>内容指纹（导出增量与源同步共用语义）。</summary>
        public static string HashContent(string content)
        {
            using var sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(content ?? "")));
        }
    }
}
