using AbilityKit.GameplayTags;
using Xunit;

namespace AbilityKit.GameplayTags.Tests;

/// <summary>GameplayTag 结构体：值语义、层级匹配、名称与深度工具。</summary>
[Collection(TagTestCollection.Name)]
public sealed class GameplayTagValueTests : TagTestBase
{
    // ---------- None / 构造 ----------

    [Fact]
    public void None_IsInvalid_WithZeroId()
    {
        Assert.False(GameplayTag.None.IsValid);
        Assert.Equal(0, GameplayTag.None.Value);
    }

    [Fact]
    public void None_AllNameAccessors_ReturnEmpty()
    {
        Assert.Equal(string.Empty, GameplayTag.None.TagName);
        Assert.Equal(string.Empty, GameplayTag.None.SimpleName);
        Assert.Equal(string.Empty, GameplayTag.None.ToString());
    }

    [Fact]
    public void FromId_Zero_ReturnsNone()
    {
        Assert.Equal(GameplayTag.None, GameplayTag.FromId(0));
    }

    [Fact]
    public void FromId_RoundTripsValue()
    {
        var tag = T("V.A");

        var fromId = GameplayTag.FromId(tag.Value);

        Assert.Equal(tag, fromId);
        Assert.Equal(tag.Value, fromId.Value);
    }

    [Fact]
    public void FromNetIndex_Zero_ReturnsNone()
    {
        Assert.Equal(GameplayTag.None, GameplayTag.FromNetIndex(0));
    }

    // ---------- 匹配语义 ----------

    [Fact]
    public void Matches_SameTag_True()
    {
        var tag = T("M.A");
        Assert.True(tag.Matches(tag));
    }

    [Fact]
    public void Matches_MoreSpecificTag_MatchesGeneralQuery()
    {
        // A.B.Matches(A)：更具体的标签满足更宽泛的查询。
        Assert.True(T("M.B.C").Matches(T("M.B")));
        Assert.True(T("M.B.C").Matches(T("M.B")));
    }

    [Fact]
    public void Matches_MoreGeneralTag_DoesNotMatchSpecificQuery()
    {
        // A.Matches(A.B)：宽泛标签不满足更具体的查询。
        Assert.False(T("M.C").Matches(T("M.C.D")));
    }

    [Fact]
    public void Matches_Unrelated_False()
    {
        Assert.False(T("M.D").Matches(T("M.E")));
    }

    [Fact]
    public void Matches_InvalidOperands_False()
    {
        Assert.False(GameplayTag.None.Matches(GameplayTag.None));
        Assert.False(T("M.F").Matches(GameplayTag.None));
        Assert.False(GameplayTag.None.Matches(T("M.F")));
    }

    [Fact]
    public void MatchesExact_ComparesIdOnly()
    {
        var a = T("ME.A");

        Assert.True(a.MatchesExact(a));
        Assert.True(a.MatchesExact(T("ME.A")));
        Assert.False(a.MatchesExact(T("ME.B")));
    }

    [Fact]
    public void MatchesExact_IgnoresHierarchy()
    {
        Assert.False(T("ME.C.D").MatchesExact(T("ME.C")));
    }

    [Fact]
    public void MatchesExact_NoneVsNone_True()
    {
        Assert.True(GameplayTag.None.MatchesExact(GameplayTag.None));
    }

    // ---------- 父子关系 ----------

    [Fact]
    public void IsChildOf_Self_False()
    {
        var tag = T("IC.A");
        Assert.False(tag.IsChildOf(tag));
    }

    [Fact]
    public void IsChildOf_Ancestor_True_Descendant_False()
    {
        var child = T("IC.B.C");
        var parent = T("IC.B");

        Assert.True(child.IsChildOf(parent));
        Assert.False(parent.IsChildOf(child));
    }

    [Fact]
    public void IsChildOf_InvalidOperands_False()
    {
        Assert.False(T("IC.D").IsChildOf(GameplayTag.None));
        Assert.False(GameplayTag.None.IsChildOf(T("IC.D")));
    }

    [Fact]
    public void IsParentOf_IsInverseOfIsChildOf()
    {
        var child = T("IP.A.B");
        var parent = T("IP.A");

        Assert.True(parent.IsParentOf(child));
        Assert.False(child.IsParentOf(parent));
    }

    // ---------- 层级导航 ----------

    [Fact]
    public void GetParent_ReturnsImmediateParentOnly()
    {
        var deep = T("GP.A.B.C");

        Assert.Equal(T("GP.A.B"), deep.GetParent());
        Assert.NotEqual(T("GP.A"), deep.GetParent());
    }

    [Fact]
    public void GetParent_RootTag_ReturnsNone()
    {
        // "GP2" 是单段根标签，没有父级。
        Assert.Equal(GameplayTag.None, T("GP2").GetParent());
        Assert.Equal(GameplayTag.None, GameplayTag.None.GetParent());
    }

    [Fact]
    public void GetRootTag_WalksToTopLevel()
    {
        // 根标签是最顶层的单段标签，不是第一级子标签。
        Assert.Equal(T("GR"), T("GR.A.B.C").GetRootTag());
        Assert.Equal(T("GR2"), T("GR2.A").GetRootTag());
    }

    // ---------- 名称工具 ----------

    [Fact]
    public void GetDepth_CountsSegments()
    {
        // GetDepth = 段数（点号数 + 1）。
        Assert.Equal(0, GameplayTag.None.GetDepth());
        Assert.Equal(1, T("D").GetDepth());
        Assert.Equal(2, T("D.A").GetDepth());
        Assert.Equal(3, T("D.B.C").GetDepth());
        Assert.Equal(4, T("D.E.F.G").GetDepth());
    }

    [Fact]
    public void SimpleName_ReturnsLastSegment()
    {
        Assert.Equal("C", T("SN.A.B.C").SimpleName);
        Assert.Equal("A", T("SN.A").SimpleName);
        Assert.Equal(string.Empty, GameplayTag.None.SimpleName);
    }

    [Fact]
    public void ImplicitConversion_ToString_ReturnsFullName()
    {
        var tag = T("IM.A.B");

        string asString = tag;

        Assert.Equal("IM.A.B", asString);
        Assert.Equal("IM.A.B", tag.ToString());
    }

    // ---------- 值语义 ----------

    [Fact]
    public void Equality_ComparesByIdOnly()
    {
        var a = T("EQ.A");
        var b = GameplayTag.FromId(a.Value);

        Assert.True(a == b);
        Assert.True(a.Equals(b));
        Assert.True(a.Equals((object)b));
        Assert.False(a != b);
    }

    [Fact]
    public void Equality_DifferentIds_NotEqual()
    {
        var a = T("EQ2.A");
        var b = T("EQ2.B");

        Assert.True(a != b);
        Assert.False(a.Equals(b));
    }

    [Fact]
    public void GetHashCode_MatchesId()
    {
        var tag = T("HC.A");

        Assert.Equal(tag.Value.GetHashCode(), tag.GetHashCode());
    }

    [Fact]
    public void CompareTo_OrdersById()
    {
        var first = T("CM.A");
        var second = T("CM.B");

        Assert.True(first.CompareTo(second) < 0);
        Assert.True(second.CompareTo(first) > 0);
        Assert.Equal(0, first.CompareTo(first));
    }
}
