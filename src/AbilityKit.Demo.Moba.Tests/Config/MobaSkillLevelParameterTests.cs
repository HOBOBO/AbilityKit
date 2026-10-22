using AbilityKit.Demo.Moba.Config.BattleDemo.MO;
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
}
