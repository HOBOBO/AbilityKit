using System;
using System.IO;
using System.Linq;
using AbilityKit.Demo.Moba.Acceptance;
using Xunit;
using Xunit.Abstractions;

namespace AbilityKit.Demo.Moba.Tests.Smoke;

/// <summary>
/// Seam #4 覆盖扫描：遍历全部期望文件，逐个经 LiveSimAcceptanceScenarioRunner 做 live 判定，
/// 报告哪些已能拿到完整 verdict（needs-trace 清零候选）、哪些仍缺什么。逐例 try/catch 不中断。
/// 这是诊断型测试（恒绿），结果经 ITestOutputHelper 输出，用于决定后续锚点/配置/装配补位。
/// </summary>
[Trait("Gate", "MobaConsoleSmoke")]
[Trait("Category", "Smoke")]
public class LiveSimAcceptanceBatchTests
{
    private readonly ITestOutputHelper _output;
    public LiveSimAcceptanceBatchTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void Live_verify_every_expectation_and_report_coverage()
    {
        var dir = ResolveExpectationsDir();
        var files = Directory.GetFiles(dir, "*.expected.json").OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToArray();
        var passed = 0;
        var failed = 0;

        foreach (var file in files)
        {
            var name = Path.GetFileNameWithoutExtension(file);
            try
            {
                var expectation = AcceptanceJsonCodec.LoadExpectation(file);
                var summary = LiveSimAcceptanceScenarioRunner.Run(expectation);
                if (summary.result.passed)
                {
                    passed++;
                    _output.WriteLine($"[LIVE-PASS] {name}");
                }
                else
                {
                    failed++;
                    _output.WriteLine($"[LIVE-FAIL] {name}  missing={summary.coverage.missingTraceNodes}  actions={summary.coverage.missingActions}");
                }
            }
            catch (Exception ex)
            {
                failed++;
                _output.WriteLine($"[LIVE-ERROR] {name}  {ex.GetType().Name}: {ex.Message}");
            }
        }

        _output.WriteLine($"==> total={files.Length}  livePass={passed}  liveFail/Error={failed}");
    }

    private static string ResolveExpectationsDir()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null)
        {
            var candidate = Path.Combine(dir, "Unity", "Packages", "com.abilitykit.demo.moba.view.runtime",
                "Runtime", "Game", "Test", "Expectations");
            if (Directory.Exists(candidate)) return candidate;
            dir = Path.GetFullPath(Path.Combine(dir, ".."));
        }
        throw new DirectoryNotFoundException("未定位到 MOBA 验收期望目录。");
    }
}
