using System.Linq;
using AbilityKit.GameplayTags;
using Xunit;

namespace AbilityKit.GameplayTags.Tests;

/// <summary>GameplayTagTemplate / TagTemplateRegistry / TagTemplateRuntime：模板定义、注册与运行时快照。</summary>
[Collection(TagTestCollection.Name)]
public sealed class TemplateTests : TagTestBase
{
    // ---------- GameplayTagTemplate ----------

    [Fact]
    public void Template_Ctor_Empty_AllListsEmpty()
    {
        var template = new GameplayTagTemplate();

        Assert.Empty(template.GrantTags);
        Assert.Empty(template.RemoveTags);
        Assert.Equal(string.Empty, template.Description);
        Assert.Equal(0, template.GetGrantContainer().Count);
        Assert.Equal(0, template.GetRemoveContainer().Count);
    }

    [Fact]
    public void Template_Ctor_WithLists_PreservesContent()
    {
        var grant = new[] { T("TP.G1"), T("TP.G2") };
        var remove = new[] { T("TP.R1") };
        var required = new[] { T("TP.Req") };
        var blocked = new[] { T("TP.Blk") };

        var template = new GameplayTagTemplate(grant, remove, required, blocked, "描述");

        Assert.Equal(grant, template.GrantTags);
        Assert.Equal(remove, template.RemoveTags);

        var requiredContainer = template.Requirements.Required;
        Assert.Equal(1, requiredContainer.Count);
        Assert.True(requiredContainer.HasTagExact(required[0]));
    }

    [Fact]
    public void Template_Ctor_NullArguments_DefaultToEmpty()
    {
        var template = new GameplayTagTemplate(null, null, null, null, null);

        Assert.Empty(template.GrantTags);
        Assert.Empty(template.RemoveTags);
        Assert.Equal(string.Empty, template.Description);
    }

    [Fact]
    public void Template_AddGrantTag_DedupsAndIgnoresInvalid()
    {
        var template = new GameplayTagTemplate();
        var tag = T("TP2.A");

        template.AddGrantTag(tag);
        template.AddGrantTag(tag);
        template.AddGrantTag(GameplayTag.None);

        Assert.Single(template.GrantTags);
    }

    [Fact]
    public void Template_AddRemoveTag_DedupsAndIgnoresInvalid()
    {
        var template = new GameplayTagTemplate();
        var tag = T("TP3.A");

        template.AddRemoveTag(tag);
        template.AddRemoveTag(tag);
        template.AddRemoveTag(GameplayTag.None);

        Assert.Single(template.RemoveTags);
    }

    [Fact]
    public void Template_AddRequiredTag_DedupsAndIgnoresInvalid()
    {
        var template = new GameplayTagTemplate();
        var tag = T("TP4.A");

        template.AddRequiredTag(tag);
        template.AddRequiredTag(tag);
        template.AddRequiredTag(GameplayTag.None);

        var required = template.Requirements.Required;
        Assert.Equal(1, required.Count);
    }

    [Fact]
    public void Template_AddBlockedTag_DedupsAndIgnoresInvalid()
    {
        var template = new GameplayTagTemplate();
        var tag = T("TP5.A");

        template.AddBlockedTag(tag);
        template.AddBlockedTag(tag);
        template.AddBlockedTag(GameplayTag.None);

        Assert.Equal(1, template.Requirements.Blocked.Count);
    }

    [Fact]
    public void Template_GetGrantContainer_FiltersInvalidAndDedups()
    {
        var tag = T("TP6.A");
        var template = new GameplayTagTemplate(new[] { tag, tag, GameplayTag.None });

        var container = template.GetGrantContainer();

        Assert.Equal(1, container.Count);
        Assert.True(container.HasTagExact(tag));
    }

    [Fact]
    public void Template_GetRemoveContainer_ReturnsConfiguredTags()
    {
        var template = new GameplayTagTemplate(removeTags: new[] { T("TP7.A"), T("TP7.B") });

        var container = template.GetRemoveContainer();

        Assert.Equal(2, container.Count);
    }

    [Fact]
    public void Template_Requirements_ExposeRequiredAndBlockedSemantics()
    {
        var template = new GameplayTagTemplate(
            requiredTags: new[] { T("TP8.Req") },
            blockedTags: new[] { T("TP8.Blk") });

        var requirements = template.Requirements;

        Assert.True(requirements.IsSatisfiedBy(C("TP8.Req")));
        Assert.True(requirements.IsSatisfiedBy(C("TP8.Req.C")));
        Assert.False(requirements.IsSatisfiedBy(C("TP8.Req", "TP8.Blk")));
        Assert.False(requirements.IsSatisfiedBy(C("TP8.X")));
    }

    [Fact]
    public void Template_CreateRuntime_MirrorsCtor()
    {
        var grant = new[] { T("TP9.G") };
        var remove = new[] { T("TP9.R") };

        var template = GameplayTagTemplate.CreateRuntime(grant, remove, null, null, "runtime");

        Assert.Equal(grant, template.GrantTags);
        Assert.Equal(remove, template.RemoveTags);
        Assert.Equal("runtime", template.Description);
    }

    // ---------- TagTemplateRegistry ----------

    [Fact]
    public void Registry_Register_ReturnsDistinctIds_AndCountsTemplates()
    {
        var registry = TagTemplateRegistry.Instance;
        var templateA = new GameplayTagTemplate();
        var templateB = new GameplayTagTemplate();

        int idA = registry.Register("tpl.A", templateA);
        int idB = registry.Register("tpl.B", templateB);

        Assert.NotEqual(idA, idB);
        Assert.Equal(2, registry.Count);
        Assert.Contains(idA, registry.GetAllIds());
        Assert.Contains(idB, registry.GetAllIds());
    }

    [Fact]
    public void Registry_TryGet_ByIdAndByName()
    {
        var registry = TagTemplateRegistry.Instance;
        var template = new GameplayTagTemplate(description: "unique");
        int id = registry.Register("tpl.get", template);

        Assert.True(registry.TryGet(id, out var byId));
        Assert.Same(template, byId);

        Assert.True(registry.TryGet("tpl.get", out var byName));
        Assert.Same(template, byName);
    }

    [Fact]
    public void Registry_TryGet_Unknown_ReturnsFalse()
    {
        var registry = TagTemplateRegistry.Instance;

        Assert.False(registry.TryGet(424242, out var byId));
        Assert.Null(byId);

        Assert.False(registry.TryGet("tpl.missing", out var byName));
        Assert.Null(byName);
    }

    [Fact]
    public void Registry_RegisterSameName_KeepsIdAndReplacesTemplate()
    {
        var registry = TagTemplateRegistry.Instance;
        var first = new GameplayTagTemplate(description: "first");
        var second = new GameplayTagTemplate(description: "second");

        int id1 = registry.Register("tpl.replace", first);
        int id2 = registry.Register("tpl.replace", second);

        Assert.Equal(id1, id2);
        Assert.Equal(1, registry.Count);

        Assert.True(registry.TryGet(id1, out var current));
        Assert.Same(second, current);
    }

    [Fact]
    public void Registry_RegisterNullTemplate_Throws()
    {
        Assert.Throws<System.ArgumentNullException>(
            () => TagTemplateRegistry.Instance.Register("tpl.null", null!));
    }

    [Fact]
    public void Registry_Clear_ResetsCountAndIdSequence()
    {
        var registry = TagTemplateRegistry.Instance;
        registry.Register("tpl.old", new GameplayTagTemplate());

        registry.Clear();

        Assert.Equal(0, registry.Count);

        int nextId = registry.Register("tpl.new", new GameplayTagTemplate());
        Assert.Equal(1, nextId);
    }

    // ---------- TagTemplateRuntime ----------

    [Fact]
    public void Runtime_FromTemplate_CopiesAllData()
    {
        var template = new GameplayTagTemplate(
            grantTags: new[] { T("RT.G1"), T("RT.G2") },
            removeTags: new[] { T("RT.R1") },
            requiredTags: new[] { T("RT.Req") },
            blockedTags: new[] { T("RT.Blk") },
            description: "rt");

        var runtime = TagTemplateRuntime.FromTemplate(7, "rt.tpl", template);

        Assert.Equal(7, runtime.Id);
        Assert.Equal("rt.tpl", runtime.Name);
        Assert.Equal(2, runtime.GrantTags.Count);
        Assert.Equal(1, runtime.RemoveTags.Count);
        Assert.True(runtime.Requirements.IsSatisfiedBy(C("RT.Req")));
        Assert.False(runtime.Requirements.IsSatisfiedBy(C("RT.Req", "RT.Blk")));
    }

    [Fact]
    public void Runtime_Ctor_NullContainers_DefaultToEmpty()
    {
        var runtime = new TagTemplateRuntime(1, "n", GameplayTagRequirements.Require(T("RT2.A")), null, null);

        Assert.Equal(0, runtime.GrantTags.Count);
        Assert.Equal(0, runtime.RemoveTags.Count);
    }
}
