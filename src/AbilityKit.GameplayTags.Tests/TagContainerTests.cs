using System.Collections.Generic;
using System.Linq;
using AbilityKit.GameplayTags;
using Xunit;

namespace AbilityKit.GameplayTags.Tests;

/// <summary>GameplayTagContainer：Add/Remove/Has*、层级匹配语义、集合运算、网络序列化。</summary>
[Collection(TagTestCollection.Name)]
public sealed class TagContainerTests : TagTestBase
{
    // ---------- 基础增删 ----------

    [Fact]
    public void EmptyContainer_IsEmptyWithZeroCount()
    {
        var container = new GameplayTagContainer();

        Assert.Equal(0, container.Count);
        Assert.True(container.IsEmpty);
        Assert.Equal(GameplayTag.None, container.First());
        Assert.Empty(container.ToArray());
    }

    [Fact]
    public void StaticEmpty_IsEmpty()
    {
        Assert.True(GameplayTagContainer.Empty.IsEmpty);
        Assert.Equal(new GameplayTagContainer(), GameplayTagContainer.Empty);
    }

    [Fact]
    public void Add_ReturnsTrueOnlyForNewTag()
    {
        var container = new GameplayTagContainer();
        var tag = T("C.A");

        Assert.True(container.Add(tag));
        Assert.False(container.Add(tag));
        Assert.Equal(1, container.Count);
    }

    [Fact]
    public void Add_IgnoresInvalidTag()
    {
        var container = new GameplayTagContainer();

        Assert.False(container.Add(GameplayTag.None));
        Assert.Equal(0, container.Count);
    }

    [Fact]
    public void Remove_ReturnsTrueOnlyWhenPresent()
    {
        var container = C("C.R");
        var tag = GameplayTagManager.Instance.TryGetTag("C.R", out var t) ? t : GameplayTag.None;

        Assert.True(container.Remove(tag));
        Assert.False(container.Remove(tag));
        Assert.Equal(0, container.Count);
    }

    [Fact]
    public void Remove_IgnoresInvalidTag()
    {
        var container = C("C.RI");

        Assert.False(container.Remove(GameplayTag.None));
        Assert.Equal(1, container.Count);
    }

    [Fact]
    public void Clear_RemovesEverything()
    {
        var container = C("C.CL1", "C.CL2");

        container.Clear();

        Assert.Equal(0, container.Count);
    }

    // ---------- 层级匹配语义 ----------

    [Fact]
    public void HasTag_MatchesExactTag()
    {
        Assert.True(C("H.A").HasTag(T("H.A")));
    }

    [Fact]
    public void HasTag_ParentQuery_MatchesStoredChild()
    {
        // 容器持有 H.A.B 时，查询父级 H.A 命中（UE 语义：持有更具体的标签即满足更宽泛的查询）。
        Assert.True(C("H.A.B").HasTag(T("H.A")));
        Assert.True(C("H.A.B.C").HasTag(T("H.A")));
    }

    [Fact]
    public void HasTag_ChildQuery_DoesNotMatchStoredParent()
    {
        Assert.False(C("H.A").HasTag(T("H.A.B")));
    }

    [Fact]
    public void HasTag_UnrelatedTag_False()
    {
        Assert.False(C("H.A").HasTag(T("H.Other")));
    }

    [Fact]
    public void HasTag_InvalidQuery_False()
    {
        Assert.False(C("H.A").HasTag(GameplayTag.None));
    }

    [Fact]
    public void HasTagExact_RequiresIdenticalTag()
    {
        var container = C("HE.A.B");

        Assert.True(container.HasTagExact(T("HE.A.B")));
        Assert.False(container.HasTagExact(T("HE.A")));
        Assert.False(container.HasTagExact(T("HE.A.B.C")));
    }

    [Fact]
    public void HasTagExact_InvalidQuery_False()
    {
        Assert.False(C("HE.A").HasTagExact(GameplayTag.None));
    }

    // ---------- HasAny / HasAll ----------

    [Fact]
    public void HasAny_Hierarchical_UsesParentChildMatching()
    {
        var container = C("HA.A.B");

        Assert.True(container.HasAny(C("HA.A")));
        Assert.True(container.HasAny(C("HA.A.B")));
        Assert.False(container.HasAny(C("HA.Other")));
    }

    [Fact]
    public void HasAny_Exact_RequiresIdenticalId()
    {
        var container = C("HA2.A.B");

        Assert.False(container.HasAny(C("HA2.A"), exact: true));
        Assert.True(container.HasAny(C("HA2.A.B"), exact: true));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void HasAny_EmptyOrNullOther_False(bool passNull)
    {
        var container = C("HA3.A");

        Assert.False(passNull
            ? container.HasAny(null!)
            : container.HasAny(new GameplayTagContainer()));
    }

    [Fact]
    public void HasAll_RequiresEveryTagToMatch()
    {
        var container = C("HL.A.B", "HL.X");

        Assert.True(container.HasAll(C("HL.A", "HL.X")));
        Assert.True(container.HasAll(C("HL.A.B", "HL.A")));  // 同一支链上的两个祖先都命中
        Assert.False(container.HasAll(C("HL.A", "HL.Missing")));
    }

    [Fact]
    public void HasAll_Exact_RequiresIdenticalIds()
    {
        var container = C("HL2.A.B", "HL2.X");

        Assert.False(container.HasAll(C("HL2.A", "HL2.X"), exact: true));
        Assert.True(container.HasAll(C("HL2.A.B", "HL2.X"), exact: true));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void HasAll_EmptyOrNullOther_True(bool passNull)
    {
        var container = C("HL3.A");

        Assert.True(passNull
            ? container.HasAll(null!)
            : container.HasAll(new GameplayTagContainer()));
    }

    // ---------- 批量增删 ----------

    [Fact]
    public void AppendTags_MergesOtherInPlace_AndToleratesNull()
    {
        var target = C("AP.A");
        var other = C("AP.B", "AP.C");

        target.AppendTags(other);
        Assert.Equal(3, target.Count);
        Assert.True(target.HasAll(other));

        target.AppendTags(null!);
        Assert.Equal(3, target.Count);
    }

    [Fact]
    public void AddRange_SkipsInvalidTags_AndRemoveRange_RemovesListed()
    {
        var container = new GameplayTagContainer();
        var valid = T("AR.A");
        var other = T("AR.B");

        container.AddRange(new[] { valid, GameplayTag.None, other, valid });
        Assert.Equal(2, container.Count);

        container.RemoveRange(new[] { valid, GameplayTag.None });
        Assert.Equal(1, container.Count);
        Assert.True(container.HasTagExact(other));
    }

    [Fact]
    public void RemoveTags_RemovesAllTagsOfOtherContainer()
    {
        var container = C("RT1.A", "RT1.B", "RT1.C");

        container.RemoveTags(C("RT1.A", "RT1.C"));

        Assert.Equal(1, container.Count);
        Assert.True(container.HasTagExact(T("RT1.B")));
    }

    // ---------- 构造 ----------

    [Fact]
    public void Ctor_FromSingleValidTag_ContainsIt()
    {
        var tag = T("CT.A");
        var container = new GameplayTagContainer(tag);

        Assert.Equal(1, container.Count);
        Assert.True(container.HasTagExact(tag));
    }

    [Fact]
    public void Ctor_FromInvalidSingleTag_IsEmpty()
    {
        Assert.Equal(0, new GameplayTagContainer(GameplayTag.None).Count);
    }

    [Fact]
    public void Ctor_FromEnumerable_DedupsAndSkipsInvalid()
    {
        var tag = T("CT2.A");
        var other = T("CT2.B");
        var container = new GameplayTagContainer(new[] { tag, other, tag, GameplayTag.None });

        Assert.Equal(2, container.Count);
    }

    [Fact]
    public void Ctor_FromNullEnumerable_IsEmpty()
    {
        Assert.Equal(0, new GameplayTagContainer(null!).Count);
    }

    // ---------- 集合运算 ----------

    [Fact]
    public void Union_ReturnsNewContainer_AndLeavesOperandsUntouched()
    {
        var a = C("U.A", "U.Common");
        var b = C("U.Common", "U.B");

        var union = a.Union(b);

        Assert.Equal(3, union.Count);
        Assert.Equal(2, a.Count);
        Assert.Equal(2, b.Count);
    }

    [Fact]
    public void Union_WithNull_ReturnsCopyOfSelf()
    {
        var a = C("U2.A");

        var union = a.Union(null!);

        Assert.Equal(1, union.Count);
        Assert.NotSame(a, union);
    }

    [Fact]
    public void Intersect_ReturnsCommonTagsOnly()
    {
        var a = C("I.A", "I.Common");
        var b = C("I.Common", "I.B");

        var intersection = a.Intersect(b);

        Assert.Equal(1, intersection.Count);
        Assert.True(intersection.HasTagExact(T("I.Common")));
    }

    [Fact]
    public void Intersect_WithNull_ReturnsEmpty()
    {
        Assert.Equal(0, C("I2.A").Intersect(null!).Count);
    }

    [Fact]
    public void Except_ReturnsTagsNotInOther()
    {
        var a = C("E.A", "E.Keep");
        var b = C("E.A", "E.Unrelated");

        var diff = a.Except(b);

        Assert.Equal(1, diff.Count);
        Assert.True(diff.HasTagExact(T("E.Keep")));
    }

    [Fact]
    public void Except_WithNull_ReturnsCopyOfSelf()
    {
        var a = C("E2.A");

        var diff = a.Except(null!);

        Assert.Equal(1, diff.Count);
        Assert.NotSame(a, diff);
    }

    // ---------- 相等与枚举 ----------

    [Fact]
    public void Equals_IsOrderInsensitive_ByTagSet()
    {
        var a = C("EQ.A", "EQ.B");
        var b = C("EQ.B", "EQ.A");

        Assert.True(a.Equals(b));
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void Equals_DifferentContentOrCount_False()
    {
        var a = C("EQ2.A");
        var b = C("EQ2.A", "EQ2.B");
        var c = C("EQ2.C");

        Assert.False(a.Equals(b));
        Assert.False(a.Equals(c));
        Assert.False(a.Equals(null!));
    }

    [Fact]
    public void Enumeration_AndToList_YieldAllStoredTags()
    {
        var container = C("EN.A", "EN.B");

        var enumerated = container.ToList();

        Assert.Equal(2, enumerated.Count);
        Assert.All(enumerated, t => Assert.True(container.HasTagExact(t)));
    }

    [Fact]
    public void ImplicitConversion_FromSingleTag()
    {
        var tag = T("IM.A");

        GameplayTagContainer container = tag;

        Assert.Equal(1, container.Count);
        Assert.True(container.HasTagExact(tag));
    }

    // ---------- 运算符 ----------

    [Fact]
    public void Operators_AndOr_MapToHasAnyHasAll()
    {
        var container = C("OP.A.B");
        var parentQuery = C("OP.A");
        var otherQuery = C("OP.Other");

        Assert.True(container & parentQuery);   // & == HasAny
        Assert.False(container & otherQuery);
        Assert.True(container | parentQuery);   // | == HasAll
        Assert.False(container | otherQuery);
    }

    [Fact]
    public void OperatorPlus_Containers_CreatesUnion()
    {
        var a = C("OP2.A");
        var b = C("OP2.B");

        var sum = a + b;

        Assert.Equal(2, sum.Count);
        Assert.Equal(1, a.Count);
    }

    [Fact]
    public void OperatorPlus_Tag_AppendsWhenValid()
    {
        var a = C("OP3.A");

        var withTag = a + T("OP3.B");
        var withNone = a + GameplayTag.None;

        Assert.Equal(2, withTag.Count);
        Assert.Equal(1, withNone.Count);
    }

    [Fact]
    public void OperatorMinus_Containers_CreatesDifference()
    {
        var a = C("OP4.A", "OP4.B");
        var b = C("OP4.A");

        var diff = a - b;

        Assert.Equal(1, diff.Count);
        Assert.True(diff.HasTagExact(T("OP4.B")));
    }

    [Fact]
    public void OperatorMinus_Tag_RemovesIt()
    {
        var a = C("OP5.A", "OP5.B");

        var diff = a - T("OP5.A");

        Assert.Equal(1, diff.Count);
        Assert.True(diff.HasTagExact(T("OP5.B")));
    }

    [Fact]
    public void OperatorMinus_InvalidTag_ReturnsSameInstance()
    {
        var a = C("OP6.A");

        Assert.Same(a, a - GameplayTag.None);
    }

    // ---------- 网络序列化 ----------

    [Fact]
    public void NetSerialize_RoundTripsTagSet()
    {
        var container = C("NET.A", "NET.B", "NET.C.D");
        var writer = new FastBufferWriter();

        container.NetSerialize(writer);

        var restored = new GameplayTagContainer();
        restored.NetDeserialize(new FastBufferReader(writer.ToArray()));

        Assert.True(container.Equals(restored));
        Assert.Equal(3, restored.Count);
    }

    [Fact]
    public void NetSerialize_EmptyContainer_RoundTripsToEmpty()
    {
        var container = new GameplayTagContainer();
        var writer = new FastBufferWriter();

        container.NetSerialize(writer);

        var restored = new GameplayTagContainer();
        restored.NetDeserialize(new FastBufferReader(writer.ToArray()));

        Assert.Equal(0, restored.Count);
    }
}
