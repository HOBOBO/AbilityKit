using System.Linq;
using AbilityKit.GameplayTags;
using Xunit;

namespace AbilityKit.GameplayTags.Tests;

/// <summary>GameplayTagManager：注册 / 规范化 / 层级关系 / NetIndex / 事件通知。</summary>
[Collection(TagTestCollection.Name)]
public sealed class TagManagerTests : TagTestBase
{
    // ---------- 注册与规范化 ----------

    [Fact]
    public void RequestTag_ReturnsValidTagWithRequestedName()
    {
        var tag = T("A.B");

        Assert.True(tag.IsValid);
        Assert.Equal("A.B", tag.TagName);
        Assert.Equal(tag, GameplayTagManager.Instance.GetTagFromId(tag.Value));
    }

    [Fact]
    public void RequestTag_TrimsSurroundingWhitespace()
    {
        var trimmed = T("A.B");
        var padded = GameplayTagManager.Instance.RequestTag("  A.B  ");

        Assert.Equal(trimmed, padded);
        Assert.Equal("A.B", padded.TagName);
    }

    [Fact]
    public void RequestTag_IsCaseSensitive()
    {
        var lower = T("case.a");
        var upper = T("Case.A");

        Assert.NotEqual(lower, upper);
        // 大小写敏感：两个叶子标签各自注册，且各自隐式注册单段父标签，共 4 个。
        Assert.Equal(4, GameplayTagManager.Instance.GetAllTagNames().Count);
        Assert.Contains("case.a", GameplayTagManager.Instance.GetAllTagNames());
        Assert.Contains("Case.A", GameplayTagManager.Instance.GetAllTagNames());
    }

    [Fact]
    public void RequestTag_AllowsNonAsciiNames()
    {
        var tag = T("测试.标签");

        Assert.True(tag.IsValid);
        Assert.Equal("测试.标签", tag.TagName);
    }

    [Fact]
    public void RequestTag_Duplicate_ReturnsSameIdAndKeepsCount()
    {
        var first = T("D.A");
        int countAfterFirst = GameplayTagManager.Instance.GetAllTagNames().Count;

        var second = T("D.A");

        Assert.Equal(first, second);
        Assert.Equal(first.Value, second.Value);
        Assert.Equal(countAfterFirst, GameplayTagManager.Instance.GetAllTagNames().Count);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(".")]
    [InlineData("..")]
    [InlineData(".A")]
    [InlineData("A.")]
    [InlineData("A..B")]
    [InlineData("A.B..C")]
    [InlineData("A B")]
    [InlineData("A.B C.D")]
    public void RequestTag_InvalidName_ThrowsArgumentException(string? name)
    {
        Assert.Throws<ArgumentException>(() => GameplayTagManager.Instance.RequestTag(name!));
    }

    [Fact]
    public void RequestTag_DeepTag_ImplicitlyRegistersAllAncestors()
    {
        T("I.J.K");

        var names = GameplayTagManager.Instance.GetAllTagNames();
        Assert.Equal(new[] { "I", "I.J", "I.J.K" }, names.OrderBy(n => n, System.StringComparer.Ordinal).ToArray());
    }

    [Fact]
    public void RequestTag_ImplicitParentsGetLowerIdsThanChild()
    {
        var deep = T("P.Q.R");

        Assert.True(GameplayTagManager.Instance.TryGetTag("P", out var root));
        Assert.True(GameplayTagManager.Instance.TryGetTag("P.Q", out var mid));

        Assert.True(root.Value < mid.Value);
        Assert.True(mid.Value < deep.Value);
    }

    [Fact]
    public void RegisterTags_RegistersValidEntriesAndSwallowsInvalid()
    {
        GameplayTagManager.Instance.RegisterTags(new[] { "S.A", "", "S..B", "S.C" });

        var names = GameplayTagManager.Instance.GetAllTagNames()
            .OrderBy(n => n, System.StringComparer.Ordinal).ToArray();
        Assert.Equal(new[] { "S", "S.A", "S.C" }, names);
    }

    [Fact]
    public void RegisterTags_NullCollection_IsNoOp()
    {
        GameplayTagManager.Instance.RegisterTags(null!);

        Assert.Equal(1, GameplayTagManager.Instance.TagCount); // 仅根占位
    }

    // ---------- TryGetTag / GetTagFromId ----------

    [Fact]
    public void TryGetTag_FindsRegisteredTag_EvenWithSurroundingWhitespace()
    {
        var registered = T("G.A");

        Assert.True(GameplayTagManager.Instance.TryGetTag("G.A", out var exact));
        Assert.Equal(registered, exact);

        Assert.True(GameplayTagManager.Instance.TryGetTag("  G.A ", out var padded));
        Assert.Equal(registered, padded);
    }

    [Fact]
    public void TryGetTag_MissesUnregisteredOrInvalidNames()
    {
        Assert.False(GameplayTagManager.Instance.TryGetTag("Missing.A", out var missing));
        Assert.Equal(GameplayTag.None, missing);

        Assert.False(GameplayTagManager.Instance.TryGetTag("", out var empty));
        Assert.Equal(GameplayTag.None, empty);

        Assert.False(GameplayTagManager.Instance.TryGetTag(null!, out var nullName));
        Assert.Equal(GameplayTag.None, nullName);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(1000000)]
    public void GetTagFromId_OutOfRange_ReturnsNone(int id)
    {
        Assert.Equal(GameplayTag.None, GameplayTagManager.Instance.GetTagFromId(id));
    }

    // ---------- NetIndex ----------

    [Fact]
    public void NetIndices_AreAssignedSequentiallyFromOne()
    {
        // 隐式父标签也占用 NetIndex："N"=1，"N.One"=2，"N.Two"=3。
        var root = T("N");
        var first = T("N.One");
        var second = T("N.Two");

        Assert.Equal(root, GameplayTagManager.Instance.GetTagFromNetIndex(1));
        Assert.Equal(first, GameplayTagManager.Instance.GetTagFromNetIndex(2));
        Assert.Equal(second, GameplayTagManager.Instance.GetTagFromNetIndex(3));
    }

    [Fact]
    public void NetIndices_ImplicitParentsGetTheirOwnIndex()
    {
        T("NP.A.B");

        Assert.True(GameplayTagManager.Instance.TryGetTag("NP", out var root));
        Assert.True(GameplayTagManager.Instance.TryGetTag("NP.A", out var mid));

        Assert.Equal(root, GameplayTagManager.Instance.GetTagFromNetIndex(1));
        Assert.Equal(mid, GameplayTagManager.Instance.GetTagFromNetIndex(2));
    }

    [Fact]
    public void GetTagFromNetIndex_ZeroOrUnused_ReturnsNone()
    {
        T("NU.A");

        Assert.Equal(GameplayTag.None, GameplayTagManager.Instance.GetTagFromNetIndex(0));
        Assert.Equal(GameplayTag.None, GameplayTagManager.Instance.GetTagFromNetIndex(999));
    }

    [Fact]
    public void NextNetIndex_RestartsAtOneAfterReset()
    {
        T("NN.A");
        T("NN.B");

        GameplayTagManager.Instance.Reset();

        Assert.Equal((ushort)1, GameplayTagManager.Instance.NextNetIndex);
    }

    // ---------- 层级遍历 ----------

    [Fact]
    public void GetAncestors_DeepTag_ReturnsEveryAncestor()
    {
        T("AN.A.B.C");

        Assert.True(GameplayTagManager.Instance.TryGetTag("AN.A.B.C", out var tag));
        var names = SortedNames(GameplayTagManager.Instance.GetAncestors(tag));

        // 祖先不含自身，包含全部上层：AN、AN.A、AN.A.B。
        Assert.Equal(new[] { "AN", "AN.A", "AN.A.B" }, names);
    }

    [Fact]
    public void GetAncestors_RootTag_ReturnsEmpty()
    {
        var root = T("ANR");

        Assert.Empty(GameplayTagManager.Instance.GetAncestors(root));
    }

    [Fact]
    public void GetDescendants_TraversesDepthFirstInRegistrationOrder()
    {
        T("DE.A");
        T("DE.A.B");
        T("DE.C");

        Assert.True(GameplayTagManager.Instance.TryGetTag("DE", out var root));
        var names = GameplayTagManager.Instance.GetDescendants(root).Select(t => t.TagName).ToArray();

        Assert.Equal(new[] { "DE.A", "DE.A.B", "DE.C" }, names);
    }

    [Fact]
    public void GetChildren_ReturnsDirectChildrenOnly()
    {
        T("CH.A.B");

        Assert.True(GameplayTagManager.Instance.TryGetTag("CH.A", out var parent));
        var children = GameplayTagManager.Instance.GetChildren(parent).Select(t => t.TagName).ToArray();

        Assert.Equal(new[] { "CH.A.B" }, children);
    }

    [Fact]
    public void GetSiblings_ReturnsSiblingsOfSameParent_ExcludingSelf()
    {
        T("SI.A.B");
        T("SI.A.C");
        T("SI.D");

        Assert.True(GameplayTagManager.Instance.TryGetTag("SI.A.B", out var tag));
        var siblings = GameplayTagManager.Instance.GetSiblings(tag).Select(t => t.TagName).ToArray();

        Assert.Equal(new[] { "SI.A.C" }, siblings);
    }

    [Fact]
    public void GetSiblings_RootTag_ReturnsEmpty()
    {
        T("SR");
        T("SR2");

        Assert.True(GameplayTagManager.Instance.TryGetTag("SR", out var tag));
        Assert.Empty(GameplayTagManager.Instance.GetSiblings(tag));
    }

    [Fact]
    public void GetRootTags_ReturnsTopLevelTagsOnly()
    {
        T("RT.A.B");
        T("RT.C");
        T("RT2.D");

        var roots = SortedNames(GameplayTagManager.Instance.GetRootTags());

        Assert.Equal(new[] { "RT", "RT2" }, roots);
    }

    // ---------- 全量列举 / 重置 ----------

    [Fact]
    public void GetAllTagNames_ExcludesRootPlaceholder()
    {
        T("GA.A");

        var names = GameplayTagManager.Instance.GetAllTagNames();

        Assert.Equal(new[] { "GA", "GA.A" }, names.OrderBy(n => n, System.StringComparer.Ordinal).ToArray());
        Assert.DoesNotContain(string.Empty, names);
    }

    [Fact]
    public void GetAllTags_MatchesGetAllTagNames()
    {
        T("GA2.A.B");

        var tags = GameplayTagManager.Instance.GetAllTags();

        Assert.Equal(GameplayTagManager.Instance.GetAllTagNames().Count, tags.Count);
        Assert.All(tags, t => Assert.True(t.IsValid));
    }

    [Fact]
    public void Reset_ClearsAllRegistrations()
    {
        T("RS.A");
        Assert.True(GameplayTagManager.Instance.TryGetTag("RS.A", out _));

        GameplayTagManager.Instance.Reset();

        Assert.False(GameplayTagManager.Instance.TryGetTag("RS.A", out _));
        Assert.False(GameplayTagManager.Instance.TryGetTag("RS", out _));
    }

    [Fact]
    public void TagCount_IncludesRootPlaceholder_AfterReset()
    {
        // Reset 后 _byName 中仍保留空串根占位（TagName 为 "" 的 id 0 节点）。
        Assert.Equal(1, GameplayTagManager.Instance.TagCount);
        Assert.Empty(GameplayTagManager.Instance.GetAllTagNames());
    }

    // ---------- 事件通知 ----------

    [Fact]
    public void RegisterTagsWithNotification_FiresAddedOnlyForNewTags()
    {
        var listener = new RecordingTagChangeListener();
        GameplayTagManager.Instance.AddListener(listener);

        GameplayTagManager.Instance.RegisterTagsWithNotification(new[] { "NO.A", "NO.A" });

        Assert.Single(listener.Events);
        Assert.Equal("NO.A", listener.Events[0].Tag.TagName);
        Assert.Equal(GameplayTagChangeType.Added, listener.Events[0].ChangeType);
    }

    [Fact]
    public void RegisterTagsWithNotification_DoesNotNotifyForImplicitParents()
    {
        var listener = new RecordingTagChangeListener();
        GameplayTagManager.Instance.AddListener(listener);

        GameplayTagManager.Instance.RegisterTagsWithNotification(new[] { "NO2.A.B" });

        // 隐式注册的父级 NO2.A / NO2 不产生通知，仅被请求的标签本身通知。
        Assert.Single(listener.Events);
        Assert.Equal("NO2.A.B", listener.Events[0].Tag.TagName);
    }

    [Fact]
    public void RegisterTagsWithNotification_SwallowsListenerExceptions_AndContinuesOtherListeners()
    {
        var throwing = new ThrowingTagChangeListener();
        var recording = new RecordingTagChangeListener();
        var manager = GameplayTagManager.Instance;
        manager.AddListener(throwing);
        manager.AddListener(recording);

        manager.RegisterTagsWithNotification(new[] { "NO3.A" });

        Assert.Single(recording.Events);
    }

    [Fact]
    public void AddListener_IgnoresDuplicates()
    {
        var listener = new RecordingTagChangeListener();
        var manager = GameplayTagManager.Instance;
        manager.AddListener(listener);
        manager.AddListener(listener);

        manager.RegisterTagsWithNotification(new[] { "NO4.A", "NO4.B" });

        Assert.Equal(2, listener.Events.Count);
    }

    [Fact]
    public void RemoveListener_StopsFurtherNotifications()
    {
        var listener = new RecordingTagChangeListener();
        var manager = GameplayTagManager.Instance;
        manager.AddListener(listener);

        manager.RegisterTagsWithNotification(new[] { "NO5.A" });
        manager.RemoveListener(listener);
        manager.RegisterTagsWithNotification(new[] { "NO5.B" });

        Assert.Single(listener.Events);
    }

    [Fact]
    public void RegisterTagsWithNotification_NullCollection_IsNoOp()
    {
        GameplayTagManager.Instance.RegisterTagsWithNotification(null!);
        Assert.Equal(1, GameplayTagManager.Instance.TagCount);
    }
}
