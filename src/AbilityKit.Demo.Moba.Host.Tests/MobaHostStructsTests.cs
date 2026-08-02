using AbilityKit.Ability.Host.Extensions.Moba.Struct;
using Xunit;

namespace AbilityKit.Demo.Moba.Host.Tests;

/// <summary>
/// demo.moba.host 包 Moba host struct 的直接契约测试（首份脱离 demo 的覆盖）。
/// </summary>
public sealed class MobaHostStructsTests
{
    [Fact]
    public void MobaHostSpawnData_constructor_and_CreateLocalPlayer()
    {
        var s = new MobaHostSpawnData(99, 1001, 2, 1f, 0f, 2f, "TestHero");
        Assert.Equal(99, s.PlayerId);
        Assert.Equal(1001, s.HeroId);
        Assert.Equal(2, s.TeamId);
        Assert.Equal(1f, s.X);
        Assert.Equal(2f, s.Z);
        Assert.Equal("TestHero", s.Name);

        var local = MobaHostSpawnData.CreateLocalPlayer(1, 2001, 5f, 7f);
        Assert.Equal(1, local.PlayerId);
        Assert.Equal(2001, local.HeroId);
        Assert.Equal(1, local.TeamId);         // 本地玩家强制 teamId=1
        Assert.Equal(5f, local.X);
        Assert.Equal(7f, local.Z);
        Assert.Equal(0f, local.Y);             // Y 强制为 0
        Assert.Equal("LocalPlayer", local.Name);
    }

    [Fact]
    public void MobaRoomLoadoutOverrides_default_has_no_override()
    {
        var def = new MobaRoomLoadoutOverrides(0, 0, 0, null);
        Assert.False(def.HasAnyOverride);
    }

    [Fact]
    public void MobaRoomLoadoutOverrides_with_level_has_override()
    {
        var ov = new MobaRoomLoadoutOverrides(5, 0, 0, null);
        Assert.True(ov.HasAnyOverride);
    }

    [Fact]
    public void MobaRoomLoadoutOverrides_with_skills_has_override()
    {
        var ov = new MobaRoomLoadoutOverrides(0, 0, 0, new[] { 5001, 5002 });
        Assert.True(ov.HasAnyOverride);
    }
}
