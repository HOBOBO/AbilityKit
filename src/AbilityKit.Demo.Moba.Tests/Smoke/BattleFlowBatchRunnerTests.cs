using System;
using System.Collections.Generic;
using System.IO;
using AbilityKit.BattleFlow;
using AbilityKit.Demo.Moba.BattleFlow;
using AbilityKit.Demo.Moba.EnvironmentModel;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.Smoke;

/// <summary>验证批量运行：一个目录下的 .battleflow 逐个跑 → 汇总 pass/fail。</summary>
public sealed class BattleFlowBatchRunnerTests
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
        var document = new BattleFlowDocument
        {
            CaseId = "complete-skill-recipe",
            Authoring = new List<BattleBlock> { recipe },
        };

        var sections = BattleFlowAuthoringExpander.Expand(document.Authoring);
        var scenario = BattleFlowCompiler.Compile(document);
        var assertions = Assert.IsType<MobaBattleFlowAssertions>(scenario.Expectations);

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
        Assert.Empty(BattleFlowDocumentValidator.Validate(document));
    }

    [Fact]
    public void CompleteSkillRecipe_RoundTripsAuthorMetadataWithoutExpandedBlocks()
    {
        var document = new BattleFlowDocument
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

        var json = BattleFlowCodec.Serialize(document);
        var back = BattleFlowCodec.Parse(json);
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
        var document = new BattleFlowDocument
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
            Sections = new BattleFlowSections
            {
                Setup = new List<BattleBlock>
                {
                    new SpawnActorBlock { Alias = "caster" },
                    new SpawnActorBlock { Alias = "target", TeamId = 2 },
                },
            },
        };

        var scenario = BattleFlowCompiler.Compile(document, _ => scene);
        var assertions = Assert.IsType<MobaBattleFlowAssertions>(scenario.Expectations);

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
        var document = new BattleFlowDocument
        {
            CaseId = "skill-outcome-" + outcome,
            Authoring = new List<BattleBlock> { intent },
        };

        var scenario = BattleFlowCompiler.Compile(document);
        var assertions = Assert.IsType<MobaBattleFlowAssertions>(scenario.Expectations);

        Assert.Single(scenario.Timeline);
        Assert.Empty(BattleFlowDocumentValidator.Validate(document));
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
    public void BatchRun_DirectoryOfFlows_AggregatesVerdict()
    {
        var dir = Path.Combine(Path.GetTempPath(), "bf-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var later = new BattleFlowDocument
            {
                CaseId = "later-case",
                ScenarioRef = "shared-duel",
                Sections = new BattleFlowSections
                {
                    Timeline = new List<BattleBlock> { new WaitBlock { AtMs = 0, DurationMs = 100 } },
                },
            };
            var earlier = new BattleFlowDocument { CaseId = "earlier-case" };
            BattleFlowCodec.SaveScene(Path.Combine(dir, "shared-duel.battlescene"), new BattleSceneDocument
            {
                SceneId = "shared-duel",
                Sections = new BattleFlowSections
                {
                    Setup = new List<BattleBlock>
                    {
                        new SpawnActorBlock { Alias = "caster", HeroId = 1001, PlayerId = "player_1" },
                        new SpawnActorBlock { Alias = "target", HeroId = 1001, TeamId = 2 },
                    },
                },
            });
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
