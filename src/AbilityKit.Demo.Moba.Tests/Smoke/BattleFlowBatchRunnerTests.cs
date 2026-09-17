using System;
using System.Collections.Generic;
using System.IO;
using AbilityKit.BattleFlow;
using AbilityKit.Demo.Moba.BattleFlow;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.Smoke;

/// <summary>验证批量运行：一个目录下的 .battleflow 逐个跑 → 汇总 pass/fail。</summary>
public sealed class BattleFlowBatchRunnerTests
{
    [Fact]
    public void BatchRun_DirectoryOfFlows_AggregatesVerdict()
    {
        var dir = Path.Combine(Path.GetTempPath(), "bf-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var later = new BattleFlowDocument
            {
                CaseId = "later-case",
                Blocks = new List<BattleBlock>
                {
                    new SpawnActorBlock { Alias = "caster", HeroId = 1001, PlayerId = "player_1" },
                    new SpawnActorBlock { Alias = "target", HeroId = 1001, TeamId = 2 },
                },
            };
            var earlier = new BattleFlowDocument { CaseId = "earlier-case" };
            BattleFlowCodec.Save(Path.Combine(dir, "z-later.battleflow"), later);
            BattleFlowCodec.Save(Path.Combine(dir, "a-earlier.battleflow"), earlier);

            var result = BattleFlowBatchRunner.RunDirectory(dir);

            Assert.Equal(2, result.Total);
            Assert.Equal(2, result.Passed);
            Assert.Equal(0, result.Failed);
            Assert.Equal(new[] { "earlier-case", "later-case" }, result.Cases.Select(item => item.CaseId));
            Assert.NotEmpty(result.Cases[0].DeterminismFingerprint);
            Assert.Equal(0, result.Cases[0].NetworkTraceCount);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }
}
