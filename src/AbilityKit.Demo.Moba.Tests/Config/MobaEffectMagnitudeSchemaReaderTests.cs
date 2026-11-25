using System;
using System.Collections.Generic;
using AbilityKit.Ability.World.DI;
using AbilityKit.Demo.Moba.Services.Triggering.PlanActions;
using AbilityKit.Modifiers;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Plan;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.Config;

public sealed class MobaEffectMagnitudeSchemaReaderTests
{
    [Theory]
    [InlineData("duration_skill_level_parameter", " ")]
    [InlineData("birth_attributes", "[{\"attribute\":0,\"magnitude_terms\":[{\"value\":1}]}]")]
    [InlineData("birth_attributes", "[{\"attribute\":1,\"magnitude_terms\":[{\"type\":6,\"parameter\":\"\"}]}]")]
    [InlineData("birth_attributes", "[{\"attribute\":1,\"magnitude_terms\":[{\"value\":1}]},{\"attribute\":1,\"magnitude_terms\":[{\"value\":2}]}]")]
    public void Invalid_summon_parameterization_fails_schema_validation(string key, string json)
    {
        var args = new[]
        {
            new KeyValuePair<string, ActionArgValue>("summon_id", ActionArgValue.OfConst(1, "summon_id")),
            new KeyValuePair<string, ActionArgValue>(key, ActionArgValue.OfString(json, key))
        };
        Assert.False(SpawnSummonSchema.Instance.TryValidateArgs(args, out var error));
        Assert.False(string.IsNullOrEmpty(error));
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("[{\"type\":99}]")]
    [InlineData("[{\"type\":256}]")]
    [InlineData("[{\"type\":2.5,\"attribute\":4}]")]
    [InlineData("[{\"type\":2,\"attribute\":99999}]")]
    [InlineData("[{\"type\":2,\"attribute\":4,\"source_role\":256}]")]
    [InlineData("[{\"type\":6,\"parameter\":\"\"}]")]
    [InlineData("[{\"type\":2,\"attribute\":0}]")]
    [InlineData("[{\"value\":1,\"unexpected\":2}]")]
    public void Invalid_terms_fail_schema_validation_before_execution(string json)
    {
        var args = new[] { new KeyValuePair<string, ActionArgValue>("magnitude_terms", ActionArgValue.OfString(json, "magnitude_terms")) };
        Assert.False(GiveDamageSchema.Instance.TryValidateArgs(args, out var error));
        Assert.False(string.IsNullOrEmpty(error));
    }

    [Fact]
    public void Ordered_terms_cannot_silently_mix_with_legacy_sources()
    {
        var args = new Dictionary<string, ActionArgValue>
        {
            ["magnitude_terms"] = ActionArgValue.OfString("[{\"type\":0,\"value\":10}]", "magnitude_terms"),
            ["magnitude_value"] = ActionArgValue.OfConst(99, "magnitude_value")
        };
        Assert.Throws<InvalidOperationException>(() => MobaEffectMagnitudeSchemaReader.Read(args, default, 0));
    }

    [Fact]
    public void Ordered_terms_sum_before_returning_one_magnitude_and_preserve_snapshot_capture()
    {
        var args = new Dictionary<string, ActionArgValue>
        {
            ["magnitude_terms"] = ActionArgValue.OfString("[{\"type\":0,\"value\":20,\"coefficient\":2},{\"type\":0,\"value\":60},{\"type\":0,\"value\":25}]", "magnitude_terms"),
            ["magnitude_evaluation"] = ActionArgValue.OfConst(1, "magnitude_evaluation")
        };
        var spec = MobaEffectMagnitudeSchemaReader.Read(args, default, 0);
        Assert.True(AbilityKit.Demo.Moba.Services.Combat.Magnitude.MobaEffectMagnitudeResolver.TryEvaluate(
            spec, default, default, 0, 0, 0, default, out var value, out var capture, out var failure), failure);
        Assert.Equal(125, value);
        Assert.True(capture.IsValid);
        Assert.True(AbilityKit.Demo.Moba.Services.Combat.Magnitude.MobaEffectMagnitudeResolver.TryEvaluate(
            spec, default, default, 0, 0, 0, capture, out var frozen, out _, out failure), failure);
        Assert.Equal(value, frozen);
    }

    [Fact]
    public void Skill_parameter_action_args_map_to_typed_magnitude_spec()
    {
        var args = new Dictionary<string, ActionArgValue>(StringComparer.OrdinalIgnoreCase)
        {
            ["magnitude_type"] = ActionArgValue.OfConst((int)MagnitudeSourceType.SkillLevelParameter, "magnitude_type"),
            ["magnitude_parameter"] = ActionArgValue.OfString("damage", "magnitude_parameter"),
            ["magnitude_coefficient"] = ActionArgValue.OfConst(1.5, "magnitude_coefficient")
        };

        var ctx = default(ExecCtx<IWorldResolver>);
        var spec = MobaEffectMagnitudeSchemaReader.Read(args, in ctx, fallbackValue: 9f);

        Assert.True(spec.Enabled);
        Assert.Equal(MagnitudeSourceType.SkillLevelParameter, spec.BaseSource.Type);
        Assert.Equal(1.5f, spec.BaseSource.Coefficient);
        Assert.Equal("damage", spec.BaseSkillLevelParameter);
    }

    [Fact]
    public void Hero_level_skill_parameter_maps_to_a_named_growth_column()
    {
        var args = new Dictionary<string, ActionArgValue>(StringComparer.OrdinalIgnoreCase)
        {
            ["magnitude_type"] = ActionArgValue.OfConst((int)MagnitudeSourceType.HeroLevelSkillParameter, "magnitude_type"),
            ["magnitude_parameter"] = ActionArgValue.OfString("SummonHealth", "magnitude_parameter"),
            ["magnitude_coefficient"] = ActionArgValue.OfConst(0.5, "magnitude_coefficient")
        };

        var ctx = default(ExecCtx<IWorldResolver>);
        var spec = MobaEffectMagnitudeSchemaReader.Read(args, in ctx, fallbackValue: 9f);

        Assert.Equal(MagnitudeSourceType.HeroLevelSkillParameter, spec.BaseSource.Type);
        Assert.Equal(0.5f, spec.BaseSource.Coefficient);
        Assert.Equal("SummonHealth", spec.BaseSkillLevelParameter);
    }

    [Fact]
    public void Skill_parameter_name_is_rejected_for_a_different_source_type()
    {
        var args = new Dictionary<string, ActionArgValue>(StringComparer.OrdinalIgnoreCase)
        {
            ["magnitude_type"] = ActionArgValue.OfConst((int)MagnitudeSourceType.Fixed, "magnitude_type"),
            ["magnitude_parameter"] = ActionArgValue.OfString("damage", "magnitude_parameter")
        };

        var ctx = default(ExecCtx<IWorldResolver>);
        var error = Assert.Throws<InvalidOperationException>(
            () => MobaEffectMagnitudeSchemaReader.Read(args, in ctx, fallbackValue: 9f));

        Assert.Contains("supplies a skill level parameter", error.Message);
    }

    [Fact]
    public void Empty_skill_parameter_name_is_rejected()
    {
        var args = new Dictionary<string, ActionArgValue>(StringComparer.OrdinalIgnoreCase)
        {
            ["magnitude_type"] = ActionArgValue.OfConst((int)MagnitudeSourceType.SkillLevelParameter, "magnitude_type"),
            ["magnitude_parameter"] = ActionArgValue.OfString("  ", "magnitude_parameter")
        };

        var ctx = default(ExecCtx<IWorldResolver>);
        var error = Assert.Throws<InvalidOperationException>(
            () => MobaEffectMagnitudeSchemaReader.Read(args, in ctx, fallbackValue: 9f));

        Assert.Contains("non-empty", error.Message);
    }

    [Fact]
    public void Fixed_literal_and_context_float_action_args_keep_existing_mapping()
    {
        var fixedArgs = new Dictionary<string, ActionArgValue>(StringComparer.OrdinalIgnoreCase)
        {
            ["magnitude_type"] = ActionArgValue.OfConst((int)MagnitudeSourceType.Fixed, "magnitude_type"),
            ["magnitude_value"] = ActionArgValue.OfConst(27, "magnitude_value")
        };
        var contextArgs = new Dictionary<string, ActionArgValue>(StringComparer.OrdinalIgnoreCase)
        {
            ["magnitude_type"] = ActionArgValue.OfConst((int)MagnitudeSourceType.ContextFloat, "magnitude_type"),
            ["magnitude_context_key"] = ActionArgValue.OfString("legacy:key", "magnitude_context_key"),
            ["magnitude_value"] = ActionArgValue.OfConst(12, "magnitude_value"),
            ["magnitude_coefficient"] = ActionArgValue.OfConst(3, "magnitude_coefficient")
        };

        var ctx = default(ExecCtx<IWorldResolver>);
        var fixedSpec = MobaEffectMagnitudeSchemaReader.Read(fixedArgs, in ctx, fallbackValue: 9f);
        var contextSpec = MobaEffectMagnitudeSchemaReader.Read(contextArgs, in ctx, fallbackValue: 9f);

        Assert.Equal(MagnitudeSourceType.Fixed, fixedSpec.BaseSource.Type);
        Assert.Equal(27f, fixedSpec.BaseSource.Data0);
        Assert.Equal(MagnitudeSourceType.ContextFloat, contextSpec.BaseSource.Type);
        Assert.Equal("legacy:key", contextSpec.BaseSource.ContextFloatKey);
        Assert.Equal(12f, contextSpec.BaseSource.Data0);
        Assert.Equal(3f, contextSpec.BaseSource.Coefficient);
    }
}
