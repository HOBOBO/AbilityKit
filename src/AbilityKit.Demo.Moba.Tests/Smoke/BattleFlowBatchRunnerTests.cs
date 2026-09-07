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
            var doc = new BattleFlowDocument
            {
                CaseId = "batch-smoke",
                Blocks = new List<BattleBlock>
                {
                    new SpawnActorBlock { Alias = "caster", HeroId = 1001, PlayerId = "player_1" },
                    new SpawnActorBlock { Alias = "target", HeroId = 1001, TeamId = 2 },
                },
            };
            BattleFlowCodec.Save(Path.Combine(dir, "batch-smoke.battleflow"), doc);

            var result = BattleFlowBatchRunner.RunDirectory(dir);

            Assert.Equal(1, result.Total);
            Assert.Equal(1, result.Passed);
            Assert.Equal(0, result.Failed);
            Assert.Equal("batch-smoke", result.Cases[0].CaseId);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }
}
