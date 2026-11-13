using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Demo.Moba.Share.Config;
using System;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.Triggering;

public sealed class ProjectileAreaTriggerConfigTests
{
    [Theory]
    [InlineData("[{\"Id\":9003}]", 0)]
    [InlineData("[{\"Id\":9003,\"CollisionLayerMask\":1}]", 1)]
    public void Projectile_collision_layer_selection_survives_json_and_runtime_mapping(string json, int expected)
    {
        var dtos = (ProjectileDTO[])LubanConfigGroupDeserializer.Instance.DeserializeFromText(json, typeof(ProjectileDTO));
        var configs = CreateConfigDatabase(null, Assert.Single(dtos));
        Assert.True(configs.TryGetProjectile(9003, out var projectile));
        Assert.Equal(expected, projectile.CollisionLayerMask);
    }

    [Fact]
    public void Luban_area_json_preserves_one_shot_flag_and_defaults_legacy_rows_to_false()
    {
        var deserializer = LubanConfigGroupDeserializer.Instance;
        var withFlag = (AoeDTO[])deserializer.DeserializeFromText(
            "[{\"Id\":9001,\"Name\":\"one_shot\",\"ConsumeOnFirstEnter\":true}]", typeof(AoeDTO));
        var legacy = (AoeDTO[])deserializer.DeserializeFromText(
            "[{\"Id\":9002,\"Name\":\"legacy\"}]", typeof(AoeDTO));

        var configs = new MobaConfigDatabase();
        var result = configs.ReloadFromDtoArrays(new Dictionary<Type, Array>
        {
            [typeof(AoeDTO)] = new[] { withFlag[0], legacy[0] },
        }, strict: false);
        Assert.True(result.Succeeded, result.Error);
        Assert.True(configs.TryGetAoe(9001, out var oneShot));
        Assert.True(oneShot.ConsumeOnFirstEnter);
        Assert.True(configs.TryGetAoe(9002, out var defaultArea));
        Assert.False(defaultArea.ConsumeOnFirstEnter);
    }

    [Fact]
    public void Aoe_trigger_fields_are_loaded_into_runtime_model()
    {
        var configs = CreateConfigDatabase(
            new AoeDTO
            {
                Id = 9001,
                Name = "test_area",
                Radius = 3.5f,
                DelayMs = 1200,
                CollisionLayerMask = 7,
                MaxTargets = 4,
                OnDelayTriggerIds = new[] { 101, 102 },
                OnEnterTriggerIds = new[] { 201 },
                OnExitTriggerIds = new[] { 301 },
                OnIntervalTriggerIds = new[] { 401, 402 },
                IntervalMs = 500,
                ConsumeOnFirstEnter = true,
            },
            null);

        Assert.True(configs.TryGetAoe(9001, out var area));
        Assert.Equal(new[] { 101, 102 }, area.OnDelayTriggerIds);
        Assert.Equal(new[] { 201 }, area.OnEnterTriggerIds);
        Assert.Equal(new[] { 301 }, area.OnExitTriggerIds);
        Assert.Equal(new[] { 401, 402 }, area.OnIntervalTriggerIds);
        Assert.Equal(500, area.IntervalMs);
        Assert.True(area.ConsumeOnFirstEnter);
    }

    [Fact]
    public void Projectile_stage_trigger_fields_are_loaded_into_runtime_model()
    {
        var configs = CreateConfigDatabase(
            null,
            new ProjectileDTO
            {
                Id = 3001,
                Name = "test_projectile",
                Speed = 12f,
                LifetimeMs = 1500,
                MaxDistance = 20f,
                OnHitEffectId = 1001,
                OnSpawnTriggerIds = new[] { 1101 },
                OnHitTriggerIds = new[] { 1201, 1202 },
                OnTickTriggerIds = new[] { 1301 },
                OnExitTriggerIds = new[] { 1401 },
            });

        Assert.True(configs.TryGetProjectile(3001, out var projectile));
        Assert.Equal(1001, projectile.OnHitEffectId);
        Assert.Equal(new[] { 1101 }, projectile.OnSpawnTriggerIds);
        Assert.Equal(new[] { 1201, 1202 }, projectile.OnHitTriggerIds);
        Assert.Equal(new[] { 1301 }, projectile.OnTickTriggerIds);
        Assert.Equal(new[] { 1401 }, projectile.OnExitTriggerIds);
    }

    private static MobaConfigDatabase CreateConfigDatabase(AoeDTO? area, ProjectileDTO? projectile)
    {
        var dtoArraysByType = new Dictionary<Type, Array>();
        if (area != null) dtoArraysByType[typeof(AoeDTO)] = new[] { area };
        if (projectile != null) dtoArraysByType[typeof(ProjectileDTO)] = new[] { projectile };

        var configs = new MobaConfigDatabase();
        var result = configs.ReloadFromDtoArrays(dtoArraysByType, strict: false);
        Assert.True(result.Succeeded, result.Error);
        return configs;
    }
}
