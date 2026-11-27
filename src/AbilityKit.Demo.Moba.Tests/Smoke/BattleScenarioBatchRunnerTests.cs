using System;
using System.Collections.Generic;
using System.IO;
using AbilityKit.BattleScenario;
using AbilityKit.Demo.Moba.BattleScenario;
using AbilityKit.Demo.Moba.BattleScenario.Runner;
using AbilityKit.Demo.Moba.EnvironmentModel;
using AbilityKit.Demo.Moba.Services;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.Smoke;

/// <summary>验证批量运行：一个目录下的 .battlescenario 逐个跑 → 汇总 pass/fail。</summary>
public sealed class BattleScenarioBatchRunnerTests
{
    [Fact]
    public void CompleteSkillRecipe_ExpandsAFullSelfContainedBattleFromOneNode()
    {
        var recipe = new MobaCompleteSkillTestBlock
        {
            EnvironmentProfileId = "arena",
            CasterHeroId = 1002,
            CasterAttributeTemplateId = 1002,
            TargetHeroId = 1001,
            TargetAttributeTemplateId = 1001,
            TargetDistance = 6f,
            SkillId = 10020101,
            Slot = 1,
            AtMs = 250,
            TraceKind = "DamageApply",
            TraceConfigId = 10020101,
            TickRate = 60,
            MaxDurationMs = 8_000,
            SettleDurationMs = 750,
        };
        var document = new BattleScenarioDocument
        {
            CaseId = "complete-skill-recipe",
            Authoring = new List<BattleBlock> { recipe },
        };

        var sections = BattleScenarioAuthoringExpander.Expand(document.Authoring);
        var scenario = BattleScenarioCompiler.Compile(document);
        var assertions = Assert.IsType<MobaBattleScenarioAssertions>(scenario.Expectations);

        Assert.Single(sections.Settings);
        Assert.Equal(3, sections.Setup.Count);
        Assert.Single(sections.Timeline);
        Assert.Single(sections.Assertions);
        Assert.Equal("arena", scenario.EnvironmentProfileId);
        Assert.Equal(60, scenario.Execution.TickRate);
        Assert.Equal(8_000, scenario.Execution.MaxDurationMs);
        Assert.Equal(750, scenario.Execution.SettleDurationMs);
        Assert.Equal(2, scenario.Actors.Count);
        Assert.Equal(1002, scenario.Actors[0].HeroId);
        Assert.Equal(1001, scenario.Actors[1].HeroId);
        Assert.Equal(6f, scenario.Actors[1].Position!.Value.X);
        Assert.Equal(1, scenario.Timeline[0].Slot);
        Assert.Equal(250, scenario.Timeline[0].AtMs);
        Assert.Equal(10020101, Assert.Single(assertions.MustContain).ConfigId);
        Assert.Empty(BattleScenarioDocumentValidator.Validate(document));
    }

    [Fact]
    public void CompleteSkillRecipe_RoundTripsAuthorMetadataWithoutExpandedBlocks()
    {
        var document = new BattleScenarioDocument
        {
            CaseId = "complete-skill-codec",
            Authoring = new List<BattleBlock>
            {
                new MobaCompleteSkillTestBlock
                {
                    CasterHeroId = 1002,
                    CasterAttributeTemplateId = 1002,
                    TargetHeroId = 1001,
                    TargetAttributeTemplateId = 1001,
                    SkillId = 10020101,
                    Slot = 1,
                    TraceConfigId = 10020101,
                },
            },
        };

        var json = BattleScenarioCodec.Serialize(document);
        var back = BattleScenarioCodec.Parse(json);
        var recipe = Assert.IsType<MobaCompleteSkillTestBlock>(Assert.Single(back.Authoring));

        Assert.Equal(10020101, recipe.SkillId);
        Assert.Equal(1, recipe.Slot);
        Assert.Empty(back.Blocks);
        Assert.False(back.Sections.HasBlocks);
    }

    [Fact]
    public void CompleteSkillRecipe_RejectsStateOutcomeWithoutComparator()
    {
        var recipe = new MobaCompleteSkillTestBlock
        {
            CasterHeroId = 1002,
            CasterAttributeTemplateId = 1002,
            TargetHeroId = 1001,
            TargetAttributeTemplateId = 1001,
            SkillId = 10020101,
            Outcome = MobaSkillOutcomeKind.State,
            StateProperty = "hp",
            Comparator = " ",
            ExpectedValue = "500",
        };

        Assert.Contains("state comparator is required", recipe.Validate());
    }

    [Fact]
    public void SkillDamageIntent_ExpandsActionAndAssertionFromOneAuthorBlock()
    {
        var document = new BattleScenarioDocument
        {
            CaseId = "skill-damage-intent",
            ScenarioRef = "duel",
            Authoring = new List<BattleBlock>
            {
                new MobaSkillDamageTestBlock { Slot = 2, DamageConfigId = 10020101 },
            },
        };
        var scene = new BattleSceneDocument
        {
            SceneId = "duel",
            Sections = new BattleScenarioSections
            {
                Setup = new List<BattleBlock>
                {
                    new SpawnActorBlock { Alias = "caster" },
                    new SpawnActorBlock { Alias = "target", TeamId = 2 },
                },
            },
        };

        var scenario = BattleScenarioCompiler.Compile(document, _ => scene);
        var assertions = Assert.IsType<MobaBattleScenarioAssertions>(scenario.Expectations);

        Assert.Equal(2, scenario.Timeline[0].Slot);
        Assert.Equal(10020101, Assert.Single(assertions.MustContain).ConfigId);
    }

    [Theory]
    [InlineData(MobaSkillOutcomeKind.TraceOccurs)]
    [InlineData(MobaSkillOutcomeKind.TraceAbsent)]
    [InlineData(MobaSkillOutcomeKind.State)]
    public void SkillOutcomeIntent_ExpandsSelectedResultContract(MobaSkillOutcomeKind outcome)
    {
        var intent = new MobaSkillOutcomeTestBlock
        {
            Outcome = outcome,
            TraceKind = "DamageApply",
            TraceConfigId = 10020101,
            StateAlias = "target",
            StateProperty = "hp",
            Comparator = "lt",
            ExpectedValue = "1000",
        };
        var document = new BattleScenarioDocument
        {
            CaseId = "skill-outcome-" + outcome,
            Authoring = new List<BattleBlock> { intent },
        };

        var scenario = BattleScenarioCompiler.Compile(document);
        var assertions = Assert.IsType<MobaBattleScenarioAssertions>(scenario.Expectations);

        Assert.Single(scenario.Timeline);
        Assert.Empty(BattleScenarioDocumentValidator.Validate(document));
        switch (outcome)
        {
            case MobaSkillOutcomeKind.TraceOccurs:
                Assert.Single(assertions.MustContain);
                break;
            case MobaSkillOutcomeKind.TraceAbsent:
                Assert.Single(assertions.MustNotContain);
                break;
            case MobaSkillOutcomeKind.State:
                Assert.Single(assertions.State);
                break;
        }
    }

    [Fact]
    public void ArchivedBatch_DistinguishesFailedAssertionsFromExecutionErrorsAndNoAssertions()
    {
        var directory = Path.Combine(Path.GetTempPath(), "bf-archive-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            BattleScenarioCodec.Save(Path.Combine(directory, "smoke.battlescenario"),
                new BattleScenarioDocument { CaseId = "smoke" });
            BattleScenarioCodec.Save(Path.Combine(directory, "failed.battlescenario"), new BattleScenarioDocument
            {
                CaseId = "failed",
                Sections = new BattleScenarioSections
                {
                    Assertions = new List<BattleBlock>
                    {
                        new AssertTraceBlock { Kind = "DamageApply", ConfigId = 999999, MinCount = 1 },
                    },
                },
            });
            File.WriteAllText(Path.Combine(directory, "invalid.battlescenario"), "{");

            var resultRoot = Path.Combine(directory, "results");
            var result = BattleScenarioRunArchive.Run(directory, resultRoot, null, "failed", out var published);
            Assert.Equal(2, result.Failed);
            var index = JsonConvert.DeserializeObject<MobaScenarioRunIndex>(
                File.ReadAllText(Path.Combine(published, MobaScenarioRunIndex.FileName)))!;
            Assert.Equal(1, index.counts.failed);
            Assert.Equal(1, index.counts.errors);
            Assert.Equal(1, index.counts.noAssertions);
            Assert.Equal("dotnet-console", index.host);
            Assert.Equal("failed", index.evidenceMode);
            Assert.Equal(new[] { "failed", "error", "no-assertions" }, index.cases.Select(entry => entry.status));
            Assert.Equal(new[] { "available", "not-captured", "not-retained" }, index.cases.Select(entry => entry.evidenceStatus));
            var evidence = File.ReadAllText(Path.Combine(published, index.cases[0].diagnosticUri));
            Assert.NotNull(MobaBattleDiagnosticArtifactCodec.ImportSnapshot(evidence));
            Assert.All(index.cases, entry => Assert.True(File.Exists(Path.Combine(published, entry.summaryUri))));

            BattleScenarioRunArchive.Run(directory, resultRoot, new[] { "smoke" }, "none", out var second);
            Assert.NotEqual(published, second);
            Assert.Equal(2, Directory.GetDirectories(resultRoot).Length);

            var failedPath = Path.Combine(directory, "failed.battlescenario");
            var changed = BattleScenarioCodec.Load(failedPath);
            Assert.IsType<AssertTraceBlock>(Assert.Single(changed.Sections.Assertions)).MinCount = 2;
            BattleScenarioCodec.Save(failedPath, changed);
            BattleScenarioRunArchive.Run(directory, resultRoot, new[] { "failed" }, "none", out var changedRun);
            var originalDetail = JObject.Parse(File.ReadAllText(Path.Combine(published, index.cases[0].summaryUri)));
            var changedDetail = JObject.Parse(File.ReadAllText(Path.Combine(changedRun, index.cases[0].summaryUri)));
            Assert.Equal((string?)originalDetail["inputFingerprint"], (string?)changedDetail["inputFingerprint"]);
            Assert.NotEqual((string?)originalDetail["expectationsFingerprint"], (string?)changedDetail["expectationsFingerprint"]);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void BatchCli_ReceiptIdentifiesPublishedBatchEvenWhenCaseFails()
    {
        var directory = Path.Combine(Path.GetTempPath(), "bf-receipt-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            File.WriteAllText(Path.Combine(directory, "invalid.battlescenario"), "{");
            var resultRoot = Path.Combine(directory, "analysis library");
            var receipt = Path.Combine(directory, "run-id.txt");
            var exit = Program.Main(new[] { "--batch", directory, "--output-dir", resultRoot,
                "--evidence", "none", "--case", "invalid", "--receipt", receipt });

            Assert.Equal(2, exit);
            var runId = File.ReadAllText(receipt).Trim();
            var index = JsonConvert.DeserializeObject<MobaScenarioRunIndex>(
                File.ReadAllText(Path.Combine(resultRoot, runId, MobaScenarioRunIndex.FileName)))!;
            Assert.Equal(index.runId, runId);
            Assert.Equal("error", Assert.Single(index.cases).status);
            Assert.Equal(Path.Combine(resultRoot, runId), Assert.Single(Directory.GetDirectories(resultRoot)));

            var missingReceipt = Path.Combine(directory, "missing.txt");
            Assert.Equal(2, Program.Main(new[] { "--batch", directory, "--output-dir", resultRoot,
                "--case", "does-not-exist", "--receipt", missingReceipt }));
            Assert.False(File.Exists(missingReceipt));
            Assert.Single(Directory.GetDirectories(resultRoot));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void BatchRun_DirectoryOfFlows_AggregatesVerdict()
    {
        var dir = Path.Combine(Path.GetTempPath(), "bf-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var later = new BattleScenarioDocument
            {
                CaseId = "later-case",
                ScenarioRef = "shared-duel",
                Sections = new BattleScenarioSections
                {
                    Timeline = new List<BattleBlock> { new WaitBlock { AtMs = 0, DurationMs = 100 } },
                },
            };
            var earlier = new BattleScenarioDocument { CaseId = "earlier-case" };
            BattleScenarioCodec.SaveScene(Path.Combine(dir, "shared-duel.battlescene"), new BattleSceneDocument
            {
                SceneId = "shared-duel",
                Sections = new BattleScenarioSections
                {
                    Setup = new List<BattleBlock>
                    {
                        new SpawnActorBlock { Alias = "caster", HeroId = 1001, PlayerId = "player_1" },
                        new SpawnActorBlock { Alias = "target", HeroId = 1001, TeamId = 2 },
                    },
                },
            });
            BattleScenarioCodec.Save(Path.Combine(dir, "z-later.battlescenario"), later);
            BattleScenarioCodec.Save(Path.Combine(dir, "a-earlier.battlescenario"), earlier);

            var result = BattleScenarioBatchRunner.RunDirectory(dir);

            Assert.Equal(2, result.Total);
            Assert.True(result.Passed == 2, string.Join(Environment.NewLine,
                result.Cases.Select(item => item.CaseId + ": " + item.Summary)));
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
