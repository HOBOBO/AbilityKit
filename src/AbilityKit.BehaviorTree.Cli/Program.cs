using System;
using System.IO;
using System.Linq;
using AbilityKit.BehaviorTree.Authoring;
using AbilityKit.Demo.Moba.Services.Behavior.BTree;

namespace AbilityKit.BehaviorTree.Cli
{
    public static class Program
    {
        public static int Main(string[] args)
        {
            var manifestPath = args.Length > 0 ? args[0] : "tools/bt-export/moba-bt-manifest.json";
            var repositoryRoot = Directory.GetCurrentDirectory();

            ProjectManifest manifest;
            try
            {
                manifest = AuthoringJson.LoadProjectManifest(File.ReadAllText(manifestPath));
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[BtCli] 清单加载失败 {manifestPath}: {ex.Message}");
                return 2;
            }

            var report = ExportPipeline.ExportProject(manifest, MobaBTreeCatalog.Registry, repositoryRoot);

            foreach (var entry in report)
            {
                var line = $"{entry.Status.ToString().PadRight(15)} {entry.TreeId} -> {entry.Target}";
                if (entry.Message.Length > 0) line += "  (" + entry.Message + ")";
                Console.WriteLine(line);
            }

            var exported = report.Count(e => e.Status == ExportStatus.Exported);
            var unchanged = report.Count(e => e.Status == ExportStatus.Unchanged);
            var failed = report.Count(e => e.Status is ExportStatus.Error or ExportStatus.SkippedNoTargets);
            Console.WriteLine($"导出 {exported} / 未变 {unchanged} / 错误 {failed}。");
            return failed == 0 ? 0 : 1;
        }
    }
}
