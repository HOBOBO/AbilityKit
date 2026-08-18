using AbilityKit.GameplayTags;
using Xunit;

namespace AbilityKit.GameplayTags.Tests;

/// <summary>GameplayTagRequirements 与 ContinuousTagRequirements：前置条件、阻塞条件、精确模式。</summary>
[Collection(TagTestCollection.Name)]
public sealed class RequirementsTests : TagTestBase
{
    // ---------- GameplayTagRequirements：容器重载 ----------

    [Fact]
    public void DefaultRequirements_AreSatisfiedByAnyNonNullContainer()
    {
        var requirements = new GameplayTagRequirements();

        Assert.True(requirements.IsSatisfiedBy(C("DR.A")));
        Assert.True(requirements.IsSatisfiedBy(new GameplayTagContainer()));
    }

    [Fact]
    public void IsSatisfiedBy_NullContainer_IsAlwaysFalse()
    {
        var requirements = new GameplayTagRequirements();

        Assert.False(requirements.IsSatisfiedBy((GameplayTagContainer)null!));
    }

    [Fact]
    public void Require_SatisfiedByMatchingOrMoreSpecificTags()
    {
        var requirements = GameplayTagRequirements.Require(T("RQ.A"));

        Assert.True(requirements.IsSatisfiedBy(C("RQ.A")));
        Assert.True(requirements.IsSatisfiedBy(C("RQ.A.B")));   // 层级匹配
        Assert.True(requirements.IsSatisfiedBy(C("RQ.A.B", "RQ.Other")));
        Assert.False(requirements.IsSatisfiedBy(C("RQ.B")));
        Assert.False(requirements.IsSatisfiedBy(new GameplayTagContainer()));
    }

    [Fact]
    public void Require_MultipleTags_NeedAllOfThem()
    {
        var requirements = GameplayTagRequirements.Require(T("RQ2.A"), T("RQ2.B"));

        Assert.True(requirements.IsSatisfiedBy(C("RQ2.A", "RQ2.B")));
        Assert.True(requirements.IsSatisfiedBy(C("RQ2.A.C", "RQ2.B")));  // 各自层级匹配
        Assert.False(requirements.IsSatisfiedBy(C("RQ2.A")));
        Assert.False(requirements.IsSatisfiedBy(C("RQ2.B")));
    }

    [Fact]
    public void Block_RejectsMatchingOrMoreSpecificTags()
    {
        var requirements = GameplayTagRequirements.Block(T("BL.A"));

        Assert.True(requirements.IsSatisfiedBy(C("BL.B")));
        Assert.True(requirements.IsSatisfiedBy(new GameplayTagContainer()));  // 空容器不被阻塞
        Assert.False(requirements.IsSatisfiedBy(C("BL.A")));
        Assert.False(requirements.IsSatisfiedBy(C("BL.A.C")));                // 子级同样触发阻塞
        Assert.False(requirements.IsSatisfiedBy(C("BL.B", "BL.A")));
    }

    [Fact]
    public void Combined_RequireAndBlock_BothApply()
    {
        var requirements = new GameplayTagRequirements(C("CB.R"), C("CB.B"));

        Assert.True(requirements.IsSatisfiedBy(C("CB.R")));
        Assert.True(requirements.IsSatisfiedBy(C("CB.R.C", "CB.X")));
        Assert.False(requirements.IsSatisfiedBy(C("CB.R", "CB.B")));   // 命中阻塞
        Assert.False(requirements.IsSatisfiedBy(C("CB.X")));           // 缺少必需
    }

    [Fact]
    public void Exact_Required_NeedsIdenticalId()
    {
        var exact = new GameplayTagRequirements(C("EX.A"), null, exact: true);
        var hierarchical = new GameplayTagRequirements(C("EX.A"), null, exact: false);

        Assert.False(exact.IsSatisfiedBy(C("EX.A.B")));
        Assert.True(hierarchical.IsSatisfiedBy(C("EX.A.B")));
        Assert.True(exact.IsSatisfiedBy(C("EX.A")));
    }

    [Fact]
    public void Exact_Blocked_TriggeredByIdenticalIdOnly()
    {
        var exact = new GameplayTagRequirements(null, C("EX2.A"), exact: true);
        var hierarchical = new GameplayTagRequirements(null, C("EX2.A"), exact: false);

        Assert.True(exact.IsSatisfiedBy(C("EX2.A.B")));    // 子级不触发精确阻塞
        Assert.False(hierarchical.IsSatisfiedBy(C("EX2.A.B")));
        Assert.False(exact.IsSatisfiedBy(C("EX2.A")));
    }

    [Fact]
    public void EmptyRequireAndBlockContainers_SatisfiedByEverything()
    {
        var requirements = new GameplayTagRequirements(new GameplayTagContainer(), new GameplayTagContainer());

        Assert.True(requirements.IsSatisfiedBy(C("EB.A")));
        Assert.True(requirements.IsSatisfiedBy(new GameplayTagContainer()));
    }

    // ---------- GameplayTagRequirements：单标签重载 ----------

    [Fact]
    public void SingleTagOverload_Require_MatchesSameTagOnly()
    {
        var requirements = GameplayTagRequirements.Require(T("ST.A"));

        Assert.True(requirements.IsSatisfiedBy(T("ST.A")));
        Assert.False(requirements.IsSatisfiedBy(T("ST.B")));
        Assert.False(requirements.IsSatisfiedBy(GameplayTag.None));
    }

    [Fact]
    public void SingleTagOverload_Block_BlocksExactTagOnly()
    {
        var requirements = GameplayTagRequirements.Block(T("ST2.A"));

        Assert.False(requirements.IsSatisfiedBy(T("ST2.A")));
        Assert.True(requirements.IsSatisfiedBy(T("ST2.A.B")));  // 单标签重载不做层级阻塞
        Assert.True(requirements.IsSatisfiedBy(T("ST2.B")));
    }

    // ---------- ContinuousTagRequirements ----------

    [Fact]
    public void Continuous_Default_AllowsActivation_NeverRemoves_AlwaysOngoing()
    {
        var requirements = new ContinuousTagRequirements();

        Assert.True(requirements.CanActivate(C("CT.A")));
        Assert.True(requirements.CanActivate(null!));
        Assert.False(requirements.ShouldRemove(C("CT.A")));
        Assert.False(requirements.ShouldRemove(null!));
        Assert.True(requirements.IsOngoingSatisfied(C("CT.A")));
        Assert.True(requirements.IsOngoingSatisfied(null!));
        Assert.NotNull(requirements.ApplicationTags);
        Assert.NotNull(requirements.RemovalTags);
    }

    [Fact]
    public void CanActivate_RespectsActivationRequirements()
    {
        var requirements = new ContinuousTagRequirements
        {
            ActivationRequired = new GameplayTagRequirements(C("CA.R"), C("CA.B")),
        };

        Assert.True(requirements.CanActivate(C("CA.R")));
        Assert.True(requirements.CanActivate(C("CA.R.C")));
        Assert.False(requirements.CanActivate(C("CA.R", "CA.B")));
        Assert.False(requirements.CanActivate(C("CA.X")));
        Assert.True(requirements.CanActivate(null!));   // null 视为无限制
    }

    [Fact]
    public void ShouldRemove_TriggersOnRemovalRequired_Hierarchically()
    {
        var requirements = new ContinuousTagRequirements
        {
            RemovalRequired = GameplayTagRequirements.Require(T("SR.A")),
        };

        Assert.True(requirements.ShouldRemove(C("SR.A")));
        Assert.True(requirements.ShouldRemove(C("SR.A.B")));
        Assert.False(requirements.ShouldRemove(C("SR.B")));
        Assert.False(requirements.ShouldRemove(null!));
    }

    [Fact]
    public void ShouldRemove_IgnoresBlockOnlyRemovalRequirements()
    {
        // 仅设置 Blocked 的移除需求（Required 为 null）永远不会触发移除。
        var requirements = new ContinuousTagRequirements
        {
            RemovalRequired = GameplayTagRequirements.Block(T("SR2.A")),
        };

        Assert.False(requirements.ShouldRemove(C("SR2.A")));
        Assert.False(requirements.ShouldRemove(C("SR2.X")));
    }

    [Fact]
    public void IsOngoingSatisfied_RespectsRequiredAndBlocked()
    {
        var requirements = new ContinuousTagRequirements
        {
            OngoingRequired = new GameplayTagRequirements(C("OG.R"), C("OG.B")),
        };

        Assert.True(requirements.IsOngoingSatisfied(C("OG.R")));
        Assert.True(requirements.IsOngoingSatisfied(C("OG.R.C")));
        Assert.False(requirements.IsOngoingSatisfied(C("OG.R", "OG.B")));   // 命中阻塞
        Assert.False(requirements.IsOngoingSatisfied(C("OG.B")));           // 缺少必需
        Assert.True(requirements.IsOngoingSatisfied(null!));                // null 视为无限制
    }
}
