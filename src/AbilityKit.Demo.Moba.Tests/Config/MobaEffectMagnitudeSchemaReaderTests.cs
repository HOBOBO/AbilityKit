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
