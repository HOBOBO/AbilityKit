using AbilityKit.GameplayTags;
using Xunit;

namespace AbilityKit.GameplayTags.Tests;

/// <summary>GameplayTagQuery 与 GameplayTagQueryBuilder：表达式求值语义、组合行为。</summary>
[Collection(TagTestCollection.Name)]
public sealed class GameplayTagQueryTests : TagTestBase
{
    // ---------- 空查询 ----------

    [Fact]
    public void NoneQuery_IsInvalid_AndMatchesEverything()
    {
        var query = GameplayTagQuery.None;

        Assert.False(query.IsValid);
        Assert.True(query.Matches(C("Q.A")));
        Assert.True(query.Matches(new GameplayTagContainer()));          // 空容器
        Assert.True(query.Matches((GameplayTagContainer)null!));          // null 容器
        Assert.True(query.Matches(GameplayTag.None));                     // 无效标签
        Assert.True(query.Matches((GameplayTagStackContainer)null!));     // null 栈
    }

    [Fact]
    public void EmptyBuilder_BuildsNoneQuery()
    {
        var query = new GameplayTagQueryBuilder().Build();

        Assert.False(query.IsValid);
        Assert.True(query.Matches(C("Q.A")));
    }

    [Fact]
    public void Builder_NoOpCalls_WithEmptyTagLists_DoNotCreateNodes()
    {
        var query = new GameplayTagQueryBuilder()
            .RequireTags()
            .ExcludeTags()
            .And()
            .Or()
            .Not()
            .Build();

        Assert.False(query.IsValid);
    }

    // ---------- 单标签 Require / Exclude ----------

    [Fact]
    public void RequireTags_SingleTag_MatchesContainerHoldingItOrChild()
    {
        var query = new GameplayTagQueryBuilder().RequireTags(T("R.A")).Build();

        Assert.True(query.Matches(C("R.A")));
        Assert.True(query.Matches(C("R.A.B")));   // 层级匹配：持有子级即满足父级查询
        Assert.False(query.Matches(C("R.B")));
    }

    [Fact]
    public void RequireTags_ValidQuery_NeverMatchesEmptyOrNull()
    {
        var query = new GameplayTagQueryBuilder().RequireTags(T("R2.A")).Build();

        Assert.False(query.Matches(new GameplayTagContainer()));
        Assert.False(query.Matches((GameplayTagContainer)null!));
    }

    [Fact]
    public void RequireTags_SingleTagOverload_MatchesSatisfyingSingleTag()
    {
        var query = new GameplayTagQueryBuilder().RequireTags(T("R3.A")).Build();

        Assert.True(query.Matches(T("R3.A")));
        Assert.True(query.Matches(T("R3.A.B")));  // 单标签 A.B 满足 Require(A)
        Assert.False(query.Matches(T("R3.B")));
        Assert.False(query.Matches(GameplayTag.None));
    }

    [Fact]
    public void RequireTags_MultipleTags_UseAnyOfSemantics()
    {
        // 同一 IncludeTags 节点内的多个标签按 HasAny 求值：命中其中一个即通过。
        var query = new GameplayTagQueryBuilder().RequireTags(T("R4.A"), T("R4.B")).Build();

        Assert.True(query.Matches(C("R4.A")));
        Assert.True(query.Matches(C("R4.B")));
        Assert.True(query.Matches(C("R4.A", "R4.B")));
        Assert.False(query.Matches(C("R4.C")));
    }

    [Fact]
    public void ExcludeTags_FailsWhenContainerHoldsTagOrItsChild()
    {
        var query = new GameplayTagQueryBuilder().ExcludeTags(T("E.A")).Build();

        Assert.True(query.Matches(C("E.B")));
        Assert.True(query.Matches(C("E.B", "E.C")));
        Assert.False(query.Matches(C("E.A")));
        Assert.False(query.Matches(C("E.A.B")));   // 层级匹配同样作用于排除
        Assert.False(query.Matches(C("E.B", "E.A")));
    }

    [Fact]
    public void ExcludeTags_MultipleTags_ExcludeAnyOfThem()
    {
        var query = new GameplayTagQueryBuilder().ExcludeTags(T("E2.A"), T("E2.B")).Build();

        Assert.False(query.Matches(C("E2.A")));
        Assert.False(query.Matches(C("E2.B")));
        Assert.True(query.Matches(C("E2.C")));
    }

    // ---------- 组合语义 ----------

    [Fact]
    public void ChainedRequireTags_CreatesConjunction()
    {
        var query = new GameplayTagQueryBuilder()
            .RequireTags(T("CR.A"))
            .RequireTags(T("CR.B"))
            .Build();

        Assert.True(query.Matches(C("CR.A", "CR.B")));
        Assert.True(query.Matches(C("CR.A.B", "CR.B"))); // A 由子级 A.B 满足
        Assert.False(query.Matches(C("CR.A")));
        Assert.False(query.Matches(C("CR.B")));
    }

    [Fact]
    public void Require_And_Exclude_CombineAsConjunction()
    {
        var query = new GameplayTagQueryBuilder()
            .RequireTags(T("RE.A"))
            .ExcludeTags(T("RE.B"))
            .Build();

        Assert.True(query.Matches(C("RE.A", "RE.C")));
        Assert.False(query.Matches(C("RE.A", "RE.B")));
        Assert.False(query.Matches(C("RE.B")));          // 缺少必需标签
        Assert.False(query.Matches(C("RE.C")));
    }

    [Fact]
    public void Or_MatchesAnyBranch()
    {
        var query = new GameplayTagQueryBuilder()
            .Or(T("OR.A"))
            .Or(T("OR.B"))
            .Build();

        Assert.True(query.Matches(C("OR.A")));
        Assert.True(query.Matches(C("OR.B")));
        Assert.True(query.Matches(C("OR.A", "OR.B")));
        Assert.False(query.Matches(C("OR.C")));
        Assert.False(query.Matches(new GameplayTagContainer()));
    }

    [Fact]
    public void RequireTags_AfterOr_JoinsTheOrGroup()
    {
        // 当前实现：Or 之后再链 RequireTags，节点会挂进 Or 的 Children，变成“或”分支之一。
        var query = new GameplayTagQueryBuilder()
            .Or(T("OJ.A"))
            .RequireTags(T("OJ.B"))
            .Build();

        Assert.True(query.Matches(C("OJ.B")));   // 只满足 Require 分支也通过
        Assert.True(query.Matches(C("OJ.A")));
        Assert.False(query.Matches(C("OJ.C")));
    }

    [Fact]
    public void Or_AfterRequire_RegroupsIntoOr()
    {
        // 当前实现：Require 之后再 Or，会以 Or 包裹已有节点，整体退化为“或”。
        var query = new GameplayTagQueryBuilder()
            .RequireTags(T("OG.A"))
            .Or(T("OG.B"))
            .Build();

        Assert.True(query.Matches(C("OG.B")));
        Assert.True(query.Matches(C("OG.A")));
        Assert.False(query.Matches(C("OG.C")));
    }

    [Fact]
    public void Not_ExcludesGivenTags()
    {
        var query = new GameplayTagQueryBuilder().Not(T("NO.A")).Build();

        Assert.True(query.Matches(C("NO.B")));
        Assert.False(query.Matches(C("NO.A")));
        Assert.False(query.Matches(C("NO.A.C")));
    }

    [Fact]
    public void ChainedNot_ComposesAsConjunctionOfNegations()
    {
        var query = new GameplayTagQueryBuilder()
            .Not(T("NO2.A"))
            .Not(T("NO2.B"))
            .Build();

        Assert.True(query.Matches(C("NO2.C")));
        Assert.False(query.Matches(C("NO2.A")));
        Assert.False(query.Matches(C("NO2.B")));
    }

    [Fact]
    public void Not_Require_Exclude_CombineAsConjunction()
    {
        var query = new GameplayTagQueryBuilder()
            .RequireTags(T("NC.A"))
            .Not(T("NC.B"))
            .Build();

        Assert.True(query.Matches(C("NC.A", "NC.C")));
        Assert.False(query.Matches(C("NC.A", "NC.B")));
    }

    // ---------- 栈容器匹配 ----------

    [Fact]
    public void Matches_StackContainer_UsesPositiveStackTags()
    {
        var stacks = new GameplayTagStackContainer();
        stacks.AddStack(T("ST.A.B"), 2);

        var query = new GameplayTagQueryBuilder().RequireTags(T("ST.A")).Build();

        Assert.True(query.Matches(stacks));
    }

    [Fact]
    public void Matches_EmptyOrNullStack_FalseForValidQuery()
    {
        var query = new GameplayTagQueryBuilder().RequireTags(T("ST2.A")).Build();

        Assert.False(query.Matches(new GameplayTagStackContainer()));
        Assert.False(query.Matches((GameplayTagStackContainer)null!));
    }

    // ---------- 相等与描述 ----------

    [Fact]
    public void Equality_DefaultEqualsNone_BuiltQueryDiffers()
    {
        Assert.True(default(GameplayTagQuery) == GameplayTagQuery.None);

        var built = new GameplayTagQueryBuilder().RequireTags(T("EQ.A")).Build();

        Assert.NotEqual(GameplayTagQuery.None, built);
        Assert.False(built == GameplayTagQuery.None);
    }

    [Fact]
    public void Equality_TwoIdenticalBuilds_AreNotReferenceEqual()
    {
        // Equals 比较根节点引用与描述字符串，两个独立构建的等价查询互不相等。
        var first = new GameplayTagQueryBuilder().RequireTags(T("EQ2.A")).Build();
        var second = new GameplayTagQueryBuilder().RequireTags(T("EQ2.A")).Build();

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void ToString_ReturnsDescriptionOrNullFallback()
    {
        Assert.Equal("EmptyQuery", GameplayTagQuery.None.ToString());
        Assert.Equal(string.Empty, new GameplayTagQueryBuilder().RequireTags(T("TS.A")).Build().ToString());
    }
}
