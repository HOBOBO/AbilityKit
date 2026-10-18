using AbilityKit.Entitas.CodeGen.Diagnostics;
using AbilityKit.Entitas.CodeGen.Runner;

namespace AbilityKit.Entitas.CodeGen;

/// <summary>
/// Entitas 组件代码生成器 CLI（设计时工具，取代已失管的 Jenny 链路）。
/// 编排逻辑在 <see cref="CodeGenRunner"/>（测试直接调用）；本文件只做参数解析。
///
/// 用法（--project 与 --config 二选一；都不给时若 tools/ 下只有一个项目配置则自动选中）：
///   dotnet run --project tools/AbilityKit.Entitas.CodeGen -- --project moba            # 生成并对比（默认，安全）
///   dotnet run --project tools/AbilityKit.Entitas.CodeGen -- --project moba --validate  # 只校验配置/源/pin
///   dotnet run --project tools/AbilityKit.Entitas.CodeGen -- --project moba --write     # 写草稿目录 local/Logs/...
///   dotnet run --project tools/AbilityKit.Entitas.CodeGen -- --project moba --update-pin
///   dotnet run --project tools/AbilityKit.Entitas.CodeGen -- --project moba --export-config local/Logs/effective.json
///   dotnet run --project tools/AbilityKit.Entitas.CodeGen -- --project moba --update-baseline    # 模板演进后固化新基线
///   dotnet run --project tools/AbilityKit.Entitas.CodeGen -- --project moba --diff-generated-dir # 报告与示例包目录的差异（不影响退出码）
///
/// 退出码：0 一致 / 1 漂移或差异 / 2 用法或配置错误 / 3 源码校验失败 / 4 内部错误。
/// </summary>
internal static class Program
{
    private static int Main(string[] args)
    {
        string? configPath = null;
        string? project = null;
        var write = false;
        var updatePin = false;
        var verbose = false;
        var validateOnly = false;
        var forceGeneratedDir = false;
        string? outDir = null;
        string? exportConfigPath = null;
        var updateBaseline = false;
        var diffGeneratedDir = false;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--config": configPath = Arg(args, ++i); break;
                case "--project": project = Arg(args, ++i); break;
                case "--write": write = true; break;
                case "--update-pin": updatePin = true; break;
                case "--update-baseline": updateBaseline = true; break;
                case "--diff-generated-dir": diffGeneratedDir = true; break;
                case "--verbose": verbose = true; break;
                case "--validate": validateOnly = true; break;
                case "--force-generated-dir": forceGeneratedDir = true; break;
                case "--export-config": exportConfigPath = Arg(args, ++i); break;
                case "--out": outDir = Arg(args, ++i); break;
                case "--check": break; // 默认行为，接受显式传入
                default:
                    Console.Error.WriteLine($"[error] 未知参数: {args[i]}");
                    return ExitCodes.UsageOrConfig;
            }
        }

        return CodeGenRunner.Run(new RunOptions(
            ConfigPath: configPath,
            Project: project,
            Write: write,
            OutDir: outDir,
            UpdatePin: updatePin,
            ValidateOnly: validateOnly,
            Verbose: verbose,
            ForceGeneratedDir: forceGeneratedDir,
            ExportConfigPath: exportConfigPath,
            UpdateBaseline: updateBaseline,
            DiffGeneratedDir: diffGeneratedDir), new RealConsoleSink());
    }

    private static string Arg(string[] args, int i) =>
        i < args.Length ? args[i] : throw new CodeGenException("参数缺值", ExitCodes.UsageOrConfig);
}
