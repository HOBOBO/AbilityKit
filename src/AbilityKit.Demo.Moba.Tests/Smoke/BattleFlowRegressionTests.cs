using System;
using System.IO;
using System.Linq;
using AbilityKit.Demo.Moba.BattleFlow;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.Smoke;

/// <summary>战斗流程回归：批量跑提交的 .battleflow 目录，断言全部通过。挂 gate（Gate=BattleFlow）做 CI 回归。</summary>
public sealed class BattleFlowRegressionTests
{
    [Fact]
    [Trait("Gate", "BattleFlow")]
    public void CommittedFlows_AllPass()
    {
        var dir = ResolveFlowsDir();
        var result = BattleFlowBatchRunner.RunDirectory(dir);

        Assert.True(result.Total > 0, "Flows 目录应至少有一个 .battleflow");
        var failed = result.Cases.Where(c => !c.Passed).ToList();
        Assert.True(failed.Count == 0,
            "有 " + failed.Count + " 个流程失败:\n" + string.Join("\n", failed.Select(c => c.CaseId + ": " + c.Summary)));
    }

    private static string ResolveFlowsDir()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null)
        {
            var candidate = Path.Combine(dir, "src", "AbilityKit.Demo.Moba.BattleFlow.Runner", "Flows");
            if (Directory.Exists(candidate)) return candidate;
            dir = Path.GetFullPath(Path.Combine(dir, ".."));
        }
        throw new DirectoryNotFoundException("找不到 Flows 目录（未定位到仓库根）");
    }
}
