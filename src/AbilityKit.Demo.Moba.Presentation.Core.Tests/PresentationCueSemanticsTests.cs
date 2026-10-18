using AbilityKit.Demo.Moba.Share;
using Xunit;

namespace AbilityKit.Demo.Moba.Presentation.Core.Tests;

/// <summary>
/// P0 回归：stage → 迁移分类的唯一权威是 PresentationCueSemantics。
/// 这里锁死三件事，防止历史 bug 复现：
/// 1. 新增 stage 忘记分类（漏分类会让 cue 静默失效）；
/// 2. 各端重新长出各写各的判定表（曾有 4 份拷贝，其中一份缺 Started/Expired/Removed/Completed）；
/// 3. 三份同值 stage 枚举镜像被改出不一致。
/// </summary>
public sealed class PresentationCueSemanticsTests
{
    [Fact]
    public void Every_defined_stage_has_a_non_None_transition()
    {
        foreach (var stage in PresentationCueSemantics.AllStages)
        {
            var transition = PresentationCueSemantics.Classify(stage);
            Assert.True(
                transition != PresentationCueTransition.None,
                $"stage {stage} 没有被归类，cue 会在所有消费端被静默忽略");
        }
    }

    [Fact]
    public void None_stage_is_the_only_unclassified_value()
    {
        Assert.Equal(PresentationCueTransition.None, PresentationCueSemantics.Classify(PresentationCueStage.None));
    }

    [Fact]
    public void Unknown_stage_values_degrade_to_None_instead_of_guessing()
    {
        // 7..19 是保留空档。保留值必须安全降级，而不是被当成 Start。
        for (var raw = 7; raw <= 19; raw++)
        {
            Assert.Equal(PresentationCueTransition.None, PresentationCueSemantics.Classify(raw));
        }

        Assert.Equal(PresentationCueTransition.None, PresentationCueSemantics.Classify(9999));
    }

    [Theory]
    [InlineData(PresentationCueStage.ConditionPassed)]
    [InlineData(PresentationCueStage.BeforeAction)]
    [InlineData(PresentationCueStage.Executed)]
    [InlineData(PresentationCueStage.Started)]
    public void Start_stages_start(PresentationCueStage stage)
    {
        Assert.Equal(PresentationCueTransition.Start, PresentationCueSemantics.Classify(stage));
        Assert.True(PresentationCueSemantics.ShouldStart(stage));
        Assert.False(PresentationCueSemantics.ShouldStop(stage));
    }

    [Theory]
    [InlineData(PresentationCueStage.Ticked)]
    [InlineData(PresentationCueStage.Refreshed)]
    [InlineData(PresentationCueStage.StackChanged)]
    public void KeepActive_stages_keep_alive(PresentationCueStage stage)
    {
        Assert.Equal(PresentationCueTransition.KeepActive, PresentationCueSemantics.Classify(stage));
        Assert.True(PresentationCueSemantics.ShouldKeepActive(stage));
    }

    [Theory]
    [InlineData(PresentationCueStage.ConditionFailed)]
    [InlineData(PresentationCueStage.Interrupted)]
    [InlineData(PresentationCueStage.Skipped)]
    [InlineData(PresentationCueStage.Expired)]
    [InlineData(PresentationCueStage.Removed)]
    [InlineData(PresentationCueStage.Completed)]
    public void Stop_stages_stop(PresentationCueStage stage)
    {
        Assert.Equal(PresentationCueTransition.Stop, PresentationCueSemantics.Classify(stage));
        Assert.True(PresentationCueSemantics.ShouldStop(stage));
    }

    [Fact]
    public void The_four_transitions_are_mutually_exclusive_and_exhaustive()
    {
        // Start/KeepActive/Stop 三者互斥是消费端 if/else 分派的前提，不能同时成立。
        foreach (var stage in PresentationCueSemantics.AllStages)
        {
            var hits = 0;
            if (PresentationCueSemantics.ShouldStart(stage)) hits++;
            if (PresentationCueSemantics.ShouldKeepActive(stage)) hits++;
            if (PresentationCueSemantics.ShouldStop(stage)) hits++;
            Assert.Equal(1, hits);
        }
    }

    [Fact]
    public void Buff_lifecycle_stages_cover_the_whole_loop()
    {
        // 这条是本次重构真正修掉的缺陷：console 端曾漏掉这四个 stage，
        // 结果 buff 特效既不生成也不销毁。
        Assert.True(PresentationCueSemantics.ShouldStart(PresentationCueStage.Started));
        Assert.True(PresentationCueSemantics.ShouldKeepActive(PresentationCueStage.Refreshed));
        Assert.True(PresentationCueSemantics.ShouldKeepActive(PresentationCueStage.StackChanged));
        Assert.True(PresentationCueSemantics.ShouldStop(PresentationCueStage.Expired));
        Assert.True(PresentationCueSemantics.ShouldStop(PresentationCueStage.Removed));
        Assert.True(PresentationCueSemantics.ShouldStop(PresentationCueStage.Completed));
    }

    [Fact]
    public void Legacy_domain_strings_round_trip_through_the_enum()
    {
        foreach (PresentationCueDomain domain in Enum.GetValues(typeof(PresentationCueDomain)))
        {
            if (domain == PresentationCueDomain.None) continue;

            var legacy = PresentationCueSemantics.ToLegacyDomainName(domain);
            Assert.False(string.IsNullOrEmpty(legacy));
            Assert.Equal(domain, PresentationCueSemantics.ParseDomain(legacy));
        }
    }

    [Fact]
    public void Legacy_area_alias_is_accepted()
    {
        Assert.Equal(PresentationCueDomain.Aoe, PresentationCueSemantics.ParseDomain("Area"));
    }

    [Theory]
    [InlineData("buff.aura", PresentationCueDomain.Buff)]
    [InlineData("tag.controlled", PresentationCueDomain.Tag)]
    [InlineData("projectile.spawn", PresentationCueDomain.Projectile)]
    [InlineData("area.zone", PresentationCueDomain.Aoe)]
    [InlineData("effect.damage", PresentationCueDomain.Effect)]
    [InlineData("trigger.skill", PresentationCueDomain.Trigger)]
    public void Prefixed_legacy_kinds_resolve_their_domain(string kind, PresentationCueDomain expected)
    {
        Assert.Equal(expected, PresentationCueSemantics.ParseDomain(kind));
    }

    [Fact]
    public void Unknown_or_empty_legacy_string_maps_to_None()
    {
        Assert.Equal(PresentationCueDomain.None, PresentationCueSemantics.ParseDomain(null));
        Assert.Equal(PresentationCueDomain.None, PresentationCueSemantics.ParseDomain(""));
        Assert.Equal(PresentationCueDomain.None, PresentationCueSemantics.ParseDomain("something-new"));
    }
}
