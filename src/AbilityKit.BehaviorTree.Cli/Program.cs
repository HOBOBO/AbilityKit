using System;
using System.IO;
using System.Linq;
using AbilityKit.BehaviorTree;
using AbilityKit.BehaviorTree.Authoring;
using AbilityKit.Demo.Moba.Services.Behavior.BTree;

namespace AbilityKit.BehaviorTree.Cli
{
    /// <summary>
    /// 行为树 headless 导出 CLI：按项目清单把授权源导出为纯运行时 IR 并扇出到全部目标，无需 Unity。
    /// 用法：dotnet run --project src/AbilityKit.BehaviorTree.Cli -- tools/bt-export/moba-bt-manifest.json
    /// 退出码 0=成功；1=存在错误/无目标。
    /// </summary>
    public static class Program
    {
        public static int Main(string[] args)
        {
            var manifestPath = args.Length > 0 ? args[0] : "tools/bt-export/moba-bt-manifest.json";
            var repositoryRoot = Directory.GetCurrentDirectory();

            BtAuthoringProjectManifest manifest;
            try
            {
                manifest = BtAuthoringJson.LoadProjectManifest(File.ReadAllText(manifestPath));
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[BtCli] 清单加载失败 {manifestPath}: {ex.Message}");
                return 2;
            }

            // 注册中心：MOBA 目录（内置 + 本程序集领域节点）。通用宿主可用自己的注册中心调用 ExportProject。
            var registry = MobaBTreeCatalog.Registry;

            var report = BtAuthoringExportPipeline.ExportProject(manifest, registry, repositoryRoot);

            foreach (var entry in report)
            {
                var line = $"{entry.Status.ToString().PadRight(15)} {entry.TreeId} -> {entry.Target}";
                if (entry.Message.Length > 0) line += "  (" + entry.Message + ")";
                Console.WriteLine(line);
            }

            var exported = report.Count(e => e.Status == BtExportStatus.Exported);
            var unchanged = report.Count(e => e.Status == BtExportStatus.Unchanged);
            var failed = report.Count(e => e.Status is BtExportStatus.Error or BtExportStatus.SkippedNoTargets);
            Console.WriteLine($"导出 {exported} / 未变 {unchanged} / 错误 {failed}。");
            return failed == 0 ? 0 : 1;
        }
    }
}
