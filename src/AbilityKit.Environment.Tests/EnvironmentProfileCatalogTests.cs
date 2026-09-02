using System;
using System.Collections.Generic;
using AbilityKit.EnvironmentModel;
using Xunit;

namespace AbilityKit.Environment.Tests;

/// <summary>
/// 以「一个项目如何消费这套机制」的方式演示：测试声明一套 MOBA 风格的 taxonomy（关注点 + 取值域）并据此组合 Profile，
/// 覆盖组合、校验、原语承载与展开，以及 binder 边界。框架代码不含任何 MOBA 专属内容——taxonomy 与展开映射都是测试里的数据。
/// </summary>
public sealed class EnvironmentProfileCatalogTests
{
    private static EnvironmentProfileCatalog BuildTaxonomy()
    {
        return new EnvironmentProfileCatalog()
            .AddConcern(new EnvironmentConcern("unit-class", new[] { "hero", "minion", "jungle", "summon", "neutral" }, "单位类别"))
            .AddConcern(new EnvironmentConcern("target-shape", new[] { "single", "group", "structure", "none" }, "目标形态"))
            .AddConcern(new EnvironmentConcern("geometry", new[] { "open", "walled", "obstacle", "destructible" }, "场景几何"))
            .AddConcern(new EnvironmentConcern("state", new[] { "full", "wounded", "armored", "cc-immune" }, "状态挂载"));
    }

    [Fact]
    public void ComposeGroups_组间叠加_ResolvesToFlatSelections()
    {
        var catalog = BuildTaxonomy();
        catalog.AddProfile(new EnvironmentProfile
        {
            Id = "jungle-camp",
            Selections = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["unit-class"] = "jungle",
                ["target-shape"] = "group",
                ["geometry"] = "walled",
                ["state"] = "full",
            },
        });

        Assert.True(catalog.TryResolve("jungle-camp", out var resolved));
        Assert.Equal("jungle", resolved.Selections["unit-class"]);
        Assert.Equal("group", resolved.Selections["target-shape"]);
        Assert.Equal("walled", resolved.Selections["geometry"]);
        Assert.Equal("full", resolved.Selections["state"]);
    }

    [Fact]
    public void BaseProfile_MergesFirst_AndDerivedWins()
    {
        var catalog = BuildTaxonomy();
        catalog
            .AddProfile(new EnvironmentProfile
            {
                Id = "training-ground",
                Selections = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["geometry"] = "open",
                    ["state"] = "full",
                },
            })
            .AddProfile(new EnvironmentProfile
            {
                Id = "jungle-camp",
                BaseProfileId = "training-ground",
                Selections = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["unit-class"] = "jungle",
                    ["geometry"] = "walled", // 覆盖 base
                },
            });

        Assert.True(catalog.TryResolve("jungle-camp", out var resolved));
        Assert.Equal("walled", resolved.Selections["geometry"]);
        Assert.Equal("full", resolved.Selections["state"]);       // 继承
        Assert.Equal("jungle", resolved.Selections["unit-class"]);
    }

    [Fact]
    public void Profile_CarriesExplicitPrimitives_ResolvedContainsThem()
    {
        var catalog = BuildTaxonomy();
        var spawn = new SpawnPrimitive
        {
            EntityKind = "jungle_warrior",
            Alias = "j1",
            Count = 3,
            Components = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["hp"] = "5000" },
        };
        catalog.AddProfile(new EnvironmentProfile { Id = "camp", Primitives = new EnvironmentPrimitive[] { spawn } });

        Assert.True(catalog.TryResolve("camp", out var resolved));
        Assert.Single(resolved.Primitives);
        Assert.Same(spawn, resolved.Primitives[0]);
    }

    [Fact]
    public void Resolve_WithExpander_ExpandsSelectionsIntoPrimitives()
    {
        var catalog = BuildTaxonomy();
        catalog.AddProfile(new EnvironmentProfile
        {
            Id = "jungle-camp",
            Selections = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["unit-class"] = "jungle" },
        });

        var expander = new JungleExpander();
        Assert.True(catalog.TryResolve("jungle-camp", expander, out var resolved));

        Assert.Contains(resolved.Primitives, p => p is SpawnPrimitive s && s.EntityKind == "jungle_warrior" && s.Count == 3);
    }

    [Fact]
    public void BaseProfile_PrimitivesAreMerged()
    {
        var catalog = BuildTaxonomy();
        catalog
            .AddProfile(new EnvironmentProfile
            {
                Id = "base",
                Primitives = new EnvironmentPrimitive[] { new ObstaclePrimitive { Shape = "box", Size = new EnvironmentVector3(2, 2, 2) } },
            })
            .AddProfile(new EnvironmentProfile
            {
                Id = "derived",
                BaseProfileId = "base",
                Primitives = new EnvironmentPrimitive[] { new SpawnPrimitive { EntityKind = "jungle_warrior" } },
            });

        Assert.True(catalog.TryResolve("derived", out var resolved));
        Assert.Equal(2, resolved.Primitives.Count);
        Assert.IsType<ObstaclePrimitive>(resolved.Primitives[0]); // base 在前
        Assert.IsType<SpawnPrimitive>(resolved.Primitives[1]);
    }

    [Fact]
    public void Validate_ValueOutsideDomain_ReportsError()
    {
        var catalog = BuildTaxonomy();
        catalog.AddProfile(new EnvironmentProfile
        {
            Id = "bad",
            Selections = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["unit-class"] = "dragon" },
        });

        Assert.Contains(catalog.Validate(), e => e.Contains("outside concern 'unit-class'"));
    }

    [Fact]
    public void Validate_UnknownConcern_ReportsError()
    {
        var catalog = BuildTaxonomy();
        catalog.AddProfile(new EnvironmentProfile
        {
            Id = "bad",
            Selections = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["weather"] = "rain" },
        });

        Assert.Contains(catalog.Validate(), e => e.Contains("unknown concern 'weather'"));
    }

    [Fact]
    public void Validate_MissingBaseProfile_ReportsError()
    {
        var catalog = BuildTaxonomy();
        catalog.AddProfile(new EnvironmentProfile { Id = "orphan", BaseProfileId = "nope" });

        Assert.Contains(catalog.Validate(), e => e.Contains("base 'nope' was not found"));
    }

    [Fact]
    public void Validate_BaseCycle_ReportsError()
    {
        var catalog = BuildTaxonomy();
        catalog
            .AddProfile(new EnvironmentProfile { Id = "a", BaseProfileId = "b" })
            .AddProfile(new EnvironmentProfile { Id = "b", BaseProfileId = "a" });

        Assert.Contains(catalog.Validate(), e => e.Contains("cycle"));
    }

    [Fact]
    public void Validate_SpawnMissingEntityKind_ReportsError()
    {
        var catalog = BuildTaxonomy();
        catalog.AddProfile(new EnvironmentProfile
        {
            Id = "bad",
            Primitives = new EnvironmentPrimitive[] { new SpawnPrimitive { Count = 1 } },
        });

        Assert.Contains(catalog.Validate(), e => e.Contains("spawn entityKind is required"));
    }

    [Fact]
    public void Validate_ModifierMissingTargetAlias_ReportsError()
    {
        var catalog = BuildTaxonomy();
        catalog.AddProfile(new EnvironmentProfile
        {
            Id = "bad",
            Primitives = new EnvironmentPrimitive[] { new ModifierPrimitive { Operation = "add", Value = "20" } },
        });

        Assert.Contains(catalog.Validate(), e => e.Contains("modifier targetAlias is required"));
    }

    [Fact]
    public void Resolve_UnknownProfile_ReturnsFalse()
    {
        Assert.False(BuildTaxonomy().TryResolve("missing", out _));
    }

    [Fact]
    public void Binder_ReceivesFlatResolvedProfile()
    {
        var catalog = BuildTaxonomy();
        catalog.AddProfile(new EnvironmentProfile
        {
            Id = "jungle-camp",
            Selections = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["unit-class"] = "jungle" },
        });

        var binder = new RecordingBinder();
        Assert.True(catalog.TryResolve("jungle-camp", out var resolved));
        binder.Bind(in resolved);

        Assert.NotNull(binder.Last);
        Assert.Equal("jungle-camp", binder.Last!.ProfileId);
        Assert.Equal("jungle", binder.Last.Selections["unit-class"]);
    }

    [Fact]
    public void Binder_ReturnsHandlesByAlias()
    {
        var catalog = BuildTaxonomy();
        catalog.AddProfile(new EnvironmentProfile
        {
            Id = "camp",
            Primitives = new EnvironmentPrimitive[]
            {
                new SpawnPrimitive { EntityKind = "jungle_warrior", Alias = "j1" },
                new SpawnPrimitive { EntityKind = "jungle_elite", Alias = "elite" },
            },
        });

        Assert.True(catalog.TryResolve("camp", out var resolved));
        var binder = new HandleBinder();
        var result = binder.Bind(in resolved);

        Assert.True(result.TryGetHandle("j1", out var j1));
        Assert.True(result.TryGetHandle("elite", out var elite));
        Assert.NotEqual(j1, elite);
        Assert.False(result.TryGetHandle("missing", out _));
    }

    /// <summary>项目侧的「常用组 → 原语」展开：unit-class:jungle → 3 个野怪生成原语。</summary>
    private sealed class JungleExpander : IEnvironmentGroupExpander
    {
        public bool TryExpand(string concernId, string value, out IReadOnlyList<EnvironmentPrimitive> primitives)
        {
            if (string.Equals(concernId, "unit-class", StringComparison.OrdinalIgnoreCase)
                && string.Equals(value, "jungle", StringComparison.OrdinalIgnoreCase))
            {
                primitives = new[]
                {
                    new SpawnPrimitive
                    {
                        EntityKind = "jungle_warrior",
                        Alias = "j1",
                        Count = 3,
                        Tags = new[] { "jungle" },
                    },
                };
                return true;
            }
            primitives = Array.Empty<EnvironmentPrimitive>();
            return false;
        }
    }

    private sealed class RecordingBinder : IEnvironmentProfileBinder<int>
    {
        public ResolvedEnvironmentProfile? Last { get; private set; }
        public EnvironmentBindResult<int> Bind(in ResolvedEnvironmentProfile profile)
        {
            Last = profile;
            return new EnvironmentBindResult<int>();
        }
    }

    private sealed class HandleBinder : IEnvironmentProfileBinder<int>
    {
        public EnvironmentBindResult<int> Bind(in ResolvedEnvironmentProfile profile)
        {
            var handles = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var nextId = 1000;
            foreach (var primitive in profile.Primitives)
            {
                if (primitive is SpawnPrimitive spawn && !string.IsNullOrEmpty(spawn.Alias))
                    handles[spawn.Alias] = nextId++;
            }
            return new EnvironmentBindResult<int>(handles);
        }
    }
}
