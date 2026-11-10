using AbilityKit.Demo.Moba.Config.BattleDemo.MO;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Share.Config;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.Config;

public sealed class MobaSkillLevelParameterTests
{
    [Fact]
    public void Skill_level_parameters_resolve_by_name_and_level()
    {
        var table = new SkillLevelTableMO(new SkillLevelTableDTO
        {
            Id = 101,
            ParamNames = new[] { "damage", "radius" },
            Levels = new[]
            {
                new SkillLevelDTO { Params = new[] { 120f, 2.5f } },
                new SkillLevelDTO { Params = new[] { 180f, 3f } }
            }
        });

        Assert.True(table.TryGetParameter(2, "DAMAGE", out var damage));
        Assert.Equal(180f, damage);
        Assert.True(table.TryGetParameter(1, "radius", out var radius));
        Assert.Equal(2.5f, radius);
        Assert.False(table.TryGetParameter(1, "unknown", out _));
    }

    [Fact]
    public void Skill_level_parameter_lookup_rejects_short_level_rows()
    {
        var table = new SkillLevelTableMO(new SkillLevelTableDTO
        {
            Id = 102,
            ParamNames = new[] { "damage", "radius" },
            Levels = new[] { new SkillLevelDTO { Params = new[] { 120f } } }
        });

        Assert.False(table.TryGetParameter(1, "radius", out _));
    }

    [Fact]
    public void Skill_level_snapshot_is_immutable_and_resolves_names_case_insensitively()
    {
        var table = CreateTable(new[] { "damage", "radius" }, new[] { 120f, 2.5f });
        Assert.True(table.TryCreateParameterSnapshot(1, 2011, out var snapshot, out var error));
        Assert.Null(error);
        Assert.Equal(2, snapshot.Count);
        Assert.True(snapshot.TryGetValue("DAMAGE", out var damage));
        Assert.Equal(120f, damage);

        var copy = snapshot.ToArray();
        copy[0] = new MobaSkillLevelParameterValue("damage", 999f);
        Assert.True(snapshot.TryGetValue("damage", out damage));
        Assert.Equal(120f, damage);
        Assert.False(snapshot.TryGetValue(" ", out _));
        Assert.False(snapshot.TryGetValue("unknown", out _));
    }

    [Fact]
    public void Skill_level_snapshot_accepts_legacy_empty_parameter_rows()
    {
        var table = CreateTable(Array.Empty<string>(), Array.Empty<float>());

        Assert.True(table.TryCreateParameterSnapshot(1, 2012, out var snapshot, out var error));
        Assert.Null(error);
        Assert.Same(MobaSkillLevelParameterSnapshot.Empty, snapshot);
    }

    [Theory]
    [InlineData(new[] { "damage", "DAMAGE" }, new[] { 1f, 2f })]
    [InlineData(new[] { "damage", " " }, new[] { 1f, 2f })]
    [InlineData(new[] { "damage", "radius" }, new[] { 1f })]
    [InlineData(new[] { "damage" }, new[] { float.NaN })]
    [InlineData(new[] { "damage" }, new[] { float.PositiveInfinity })]
    public void Skill_level_snapshot_rejects_invalid_declarations_or_values(string[] names, float[] values)
    {
        var table = CreateTable(names, values);

        Assert.False(table.TryCreateParameterSnapshot(1, 2013, out var snapshot, out var error));
        Assert.Same(MobaSkillLevelParameterSnapshot.Empty, snapshot);
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    [Fact]
    public void Skill_level_snapshot_rejects_out_of_range_level()
    {
        var table = CreateTable(new[] { "damage" }, new[] { 1f });

        Assert.False(table.TryCreateParameterSnapshot(2, 2014, out _, out var error));
        Assert.Contains("level=2", error);
    }

    private static SkillLevelTableMO CreateTable(string[] names, float[] values) => new(new SkillLevelTableDTO
    {
        Id = 103,
        ParamNames = names,
        Levels = new[] { new SkillLevelDTO { Params = values } }
    });
}
