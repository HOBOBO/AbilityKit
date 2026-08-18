using System.Collections.Generic;
using System.Linq;
using AbilityKit.GameplayTags;
using Xunit;

namespace AbilityKit.GameplayTags.Tests;

/// <summary>GameplayTagStack / GameplayTagStackContainer / GameplayTagDelta：计数栈与增量合并。</summary>
[Collection(TagTestCollection.Name)]
public sealed class StackDeltaTests : TagTestBase
{
    // ---------- GameplayTagStack 结构体 ----------

    [Fact]
    public void Stack_Default_IsInvalidAndEmpty()
    {
        var stack = default(GameplayTagStack);

        Assert.Equal(GameplayTag.None, stack.Tag);
        Assert.Equal(0, stack.Count);
        Assert.False(stack.IsValid);
        Assert.True(stack.IsEmpty);
    }

    [Fact]
    public void Stack_Ctor_ClampsNegativeCountToZero()
    {
        Assert.Equal(0, new GameplayTagStack(T("SK.A"), -5).Count);
    }

    [Fact]
    public void Stack_Increment_AddsPositiveAmountsOnly()
    {
        var stack = new GameplayTagStack(T("SK.B"));

        stack.Increment();
        Assert.Equal(1, stack.Count);

        stack.Increment(4);
        Assert.Equal(5, stack.Count);

        stack.Increment(-3);
        Assert.Equal(5, stack.Count);
    }

    [Fact]
    public void Stack_Decrement_NeverGoesBelowZero()
    {
        var stack = new GameplayTagStack(T("SK.C"), 2);

        stack.Decrement();
        Assert.Equal(1, stack.Count);

        stack.Decrement(10);
        Assert.Equal(0, stack.Count);
    }

    [Fact]
    public void Stack_SetCount_ClampsNegativeToZero()
    {
        var stack = new GameplayTagStack(T("SK.D"), 3);

        stack.SetCount(-1);
        Assert.Equal(0, stack.Count);

        stack.SetCount(7);
        Assert.Equal(7, stack.Count);
    }

    [Fact]
    public void Stack_ValidTagWithZeroCount_IsValidButEmpty()
    {
        var stack = new GameplayTagStack(T("SK.E"), 0);

        Assert.True(stack.IsValid);
        Assert.True(stack.IsEmpty);
    }

    [Fact]
    public void Stack_Equality_ComparesTagAndCount()
    {
        var tag = T("SK.F");
        var a = new GameplayTagStack(tag, 2);
        var b = new GameplayTagStack(T("SK.F"), 2);
        var c = new GameplayTagStack(tag, 3);

        Assert.True(a == b);
        Assert.True(a.Equals(b));
        Assert.True(a.Equals((object)b));
        Assert.False(a != b);
        Assert.True(a != c);
    }

    [Fact]
    public void Stack_ToString_UsesTagNameAndCount()
    {
        Assert.Equal("SK.G x2", new GameplayTagStack(T("SK.G"), 2).ToString());
    }

    // ---------- GameplayTagStackContainer ----------

    [Fact]
    public void AddStack_DefaultsToOne_AndAccumulates()
    {
        var stacks = new GameplayTagStackContainer();
        var tag = T("AS.A");

        stacks.AddStack(tag);
        Assert.Equal(1, stacks.GetStackCount(tag));

        stacks.AddStack(tag, 4);
        Assert.Equal(5, stacks.GetStackCount(tag));
        Assert.Equal(1, stacks.Count);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void AddStack_NonPositiveCount_IsNoOp(int count)
    {
        var stacks = new GameplayTagStackContainer();

        stacks.AddStack(T("AS.B"), count);

        Assert.Equal(0, stacks.Count);
        Assert.Equal(0, stacks.GetStackCount(T("AS.B")));
    }

    [Fact]
    public void AddStack_InvalidTag_IsNoOp()
    {
        var stacks = new GameplayTagStackContainer();

        stacks.AddStack(GameplayTag.None, 3);

        Assert.Equal(0, stacks.Count);
    }

    [Fact]
    public void RemoveStack_Decrements_ButKeepsEntryWhilePositive()
    {
        var stacks = new GameplayTagStackContainer();
        var tag = T("RS.A");
        stacks.AddStack(tag, 3);

        stacks.RemoveStack(tag);

        Assert.Equal(2, stacks.GetStackCount(tag));
        Assert.Equal(1, stacks.Count);
    }

    [Fact]
    public void RemoveStack_AtOrBelowZero_RemovesEntry()
    {
        var stacks = new GameplayTagStackContainer();
        var tag = T("RS.B");
        stacks.AddStack(tag, 2);

        stacks.RemoveStack(tag, 2);
        Assert.Equal(0, stacks.GetStackCount(tag));
        Assert.Equal(0, stacks.Count);
        Assert.False(stacks.HasTag(tag));

        // 再移除已不存在的栈：无操作，不出现负计数
        stacks.RemoveStack(tag, 5);
        Assert.Equal(0, stacks.GetStackCount(tag));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-2)]
    public void RemoveStack_NonPositiveCount_IsNoOp(int count)
    {
        var stacks = new GameplayTagStackContainer();
        var tag = T("RS.C");
        stacks.AddStack(tag, 2);

        stacks.RemoveStack(tag, count);

        Assert.Equal(2, stacks.GetStackCount(tag));
    }

    [Fact]
    public void RemoveStack_UnknownTag_IsNoOp()
    {
        var stacks = new GameplayTagStackContainer();

        stacks.RemoveStack(T("RS.D"), 1);

        Assert.Equal(0, stacks.Count);
    }

    [Fact]
    public void SetStackCount_SetsOrRemovesEntry()
    {
        var stacks = new GameplayTagStackContainer();
        var tag = T("SS.A");

        stacks.SetStackCount(tag, 4);
        Assert.Equal(4, stacks.GetStackCount(tag));

        stacks.SetStackCount(tag, 0);
        Assert.Equal(0, stacks.GetStackCount(tag));
        Assert.Equal(0, stacks.Count);

        stacks.SetStackCount(tag, -3);
        Assert.Equal(0, stacks.GetStackCount(tag));
    }

    [Fact]
    public void SetStackCount_InvalidTag_IsNoOp()
    {
        var stacks = new GameplayTagStackContainer();

        stacks.SetStackCount(GameplayTag.None, 5);

        Assert.Equal(0, stacks.Count);
    }

    [Fact]
    public void HasTag_ReflectsPresenceOfPositiveStack()
    {
        var stacks = new GameplayTagStackContainer();
        var tag = T("HS.A");

        Assert.False(stacks.HasTag(tag));
        Assert.False(stacks.HasTag(GameplayTag.None));

        stacks.AddStack(tag, 1);
        Assert.True(stacks.HasTag(tag));
    }

    [Fact]
    public void GetStackCount_UnknownOrInvalid_ReturnsZero()
    {
        var stacks = new GameplayTagStackContainer();
        stacks.AddStack(T("GS.A"), 2);

        Assert.Equal(0, stacks.GetStackCount(T("GS.Missing")));
        Assert.Equal(0, stacks.GetStackCount(GameplayTag.None));
    }

    [Fact]
    public void Count_And_TotalCount_AggregateAcrossStacks()
    {
        var stacks = new GameplayTagStackContainer();
        stacks.AddStack(T("AG.A"), 2);
        stacks.AddStack(T("AG.B"), 3);

        Assert.Equal(2, stacks.Count);
        Assert.Equal(5, stacks.TotalCount);
    }

    [Fact]
    public void Clear_RemovesAllStacks()
    {
        var stacks = new GameplayTagStackContainer();
        stacks.AddStack(T("CL.A"), 2);

        stacks.Clear();

        Assert.Equal(0, stacks.Count);
        Assert.Equal(0, stacks.TotalCount);
    }

    [Fact]
    public void AddStacks_BatchAdds_AndSkipsZeroCounts_AndToleratesNull()
    {
        var stacks = new GameplayTagStackContainer();
        var tag = T("BS.A");

        stacks.AddStacks(new[] { new GameplayTagStack(tag, 2), new GameplayTagStack(T("BS.B"), 0) });
        Assert.Equal(1, stacks.Count);            // 零计数项被跳过
        Assert.Equal(2, stacks.GetStackCount(tag));

        stacks.AddStacks(null!);
        Assert.Equal(1, stacks.Count);
    }

    [Fact]
    public void ToContainer_ContainsPositiveStacks_AndSupportsHierarchicalQueries()
    {
        var stacks = new GameplayTagStackContainer();
        stacks.AddStack(T("TC.A.B"), 2);

        var container = stacks.ToContainer();

        Assert.Equal(1, container.Count);
        Assert.True(container.HasTag(T("TC.A")));       // 层级查询命中子级
        Assert.True(container.HasTagExact(T("TC.A.B")));
    }

    [Fact]
    public void ToList_And_Enumeration_YieldTagAndCountPairs()
    {
        var stacks = new GameplayTagStackContainer();
        stacks.AddStack(T("TL.A"), 2);
        stacks.AddStack(T("TL.B"), 3);

        var list = stacks.ToList();
        var enumerated = stacks.ToList(); // 与枚举一致（都按存储的键值对）

        Assert.Equal(2, list.Count);
        Assert.Equal(2, enumerated.Count);
        Assert.Contains(list, s => s.Tag == T("TL.A") && s.Count == 2);
        Assert.Contains(list, s => s.Tag == T("TL.B") && s.Count == 3);
    }

    [Fact]
    public void StackContainer_Equality_ComparesAllStacks()
    {
        var a = new GameplayTagStackContainer();
        a.AddStack(T("EQ.A"), 2);
        var b = new GameplayTagStackContainer();
        b.AddStack(T("EQ.A"), 2);
        var c = new GameplayTagStackContainer();
        c.AddStack(T("EQ.A"), 3);

        Assert.True(a.Equals(b));
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
        Assert.False(a.Equals(c));
        Assert.False(a.Equals(null!));
    }

    // ---------- GameplayTagDelta ----------

    [Fact]
    public void Delta_EmptyAndDefault_AreEmpty()
    {
        Assert.True(GameplayTagDelta.Empty.IsEmpty);
        Assert.True(default(GameplayTagDelta).IsEmpty);
    }

    [Fact]
    public void Delta_Ctor_ExposesAddedAndRemoved()
    {
        var added = C("DL.A");
        var removed = C("DL.B");

        var delta = new GameplayTagDelta(added, removed);

        Assert.Same(added, delta.Added);
        Assert.Same(removed, delta.Removed);
        Assert.False(delta.IsEmpty);
    }

    [Fact]
    public void Delta_Merge_UnionsBothSides()
    {
        var first = new GameplayTagDelta(C("DM.A", "DM.Common"), C("DM.X"));
        var second = new GameplayTagDelta(C("DM.B"), C("DM.Y", "DM.X"));

        var merged = first + second;

        Assert.Equal(3, merged.Added.Count);
        Assert.Equal(2, merged.Removed.Count);
        Assert.True(merged.Added.HasTagExact(T("DM.A")));
        Assert.True(merged.Added.HasTagExact(T("DM.Common")));
        Assert.True(merged.Added.HasTagExact(T("DM.B")));
        Assert.True(merged.Removed.HasTagExact(T("DM.X")));
        Assert.True(merged.Removed.HasTagExact(T("DM.Y")));
    }

    [Fact]
    public void Delta_Merge_WithEmpty_PreservesOther()
    {
        var delta = new GameplayTagDelta(C("DM2.A"), C("DM2.B"));

        var merged = delta + GameplayTagDelta.Empty;

        Assert.True(merged.Added.HasTagExact(T("DM2.A")));
        Assert.True(merged.Removed.HasTagExact(T("DM2.B")));
        Assert.False(merged.IsEmpty);
    }

    [Fact]
    public void Delta_Merge_DoesNotCancelAddedAgainstRemoved()
    {
        // 合并是纯累加语义：一边 Added、另一边 Removed 的同一标签会同时出现在两侧。
        var addOnly = new GameplayTagDelta(C("DM3.A"), null);
        var removeOnly = new GameplayTagDelta(null, C("DM3.A"));

        var merged = addOnly + removeOnly;

        Assert.True(merged.Added.HasTagExact(T("DM3.A")));
        Assert.True(merged.Removed.HasTagExact(T("DM3.A")));
        Assert.False(merged.IsEmpty);
    }

    [Fact]
    public void Delta_Merge_EmptyPlusEmpty_StaysEmpty()
    {
        var merged = GameplayTagDelta.Empty + GameplayTagDelta.Empty;

        Assert.True(merged.IsEmpty);
        Assert.Equal(0, merged.Added.Count);
        Assert.Equal(0, merged.Removed.Count);
    }
}
