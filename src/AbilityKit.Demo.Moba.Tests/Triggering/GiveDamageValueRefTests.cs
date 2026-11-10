using AbilityKit.Ability.World.DI;
using AbilityKit.Demo.Moba.Services.Triggering.PlanActions;
using AbilityKit.Deterministic;
using AbilityKit.Triggering.Payload;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Plan;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.Triggering;

public sealed class GiveDamageValueRefTests
{
    [Fact]
    public void Schema_accepts_numeric_ref_and_rejects_invalid_or_conflicting_arguments()
    {
        var payloadRef = new KeyValuePair<string, ActionArgValue>(
            "damage_value_ref", ActionArgValue.Of(NumericValueRef.PayloadField(920030), "damage_value_ref"));
        Assert.True(GiveDamageSchema.Instance.TryValidateArgs(new[] { payloadRef }, out var validError), validError);

        var wrongType = new KeyValuePair<string, ActionArgValue>(
            "damage_value_ref", ActionArgValue.OfString("elapsed", "damage_value_ref"));
        Assert.False(GiveDamageSchema.Instance.TryValidateArgs(new[] { wrongType }, out var typeError));
        Assert.Contains("must be numeric", typeError);

        var combined = new[]
        {
            payloadRef,
            new KeyValuePair<string, ActionArgValue>("magnitude_type", ActionArgValue.OfConst(1, "magnitude_type"))
        };
        Assert.False(GiveDamageSchema.Instance.TryValidateArgs(combined, out var combinedError));
        Assert.Contains("cannot be combined with magnitude", combinedError);
    }

    [Fact]
    public void Schema_requires_flight_snapshot_for_rank_coefficient_and_validates_formula_limits()
    {
        var flightRankParameter = new KeyValuePair<string, ActionArgValue>(
            "damage_value_ref_skill_level_parameter", ActionArgValue.OfString("flight_ramp", "damage_value_ref_skill_level_parameter"));
        var payloadRef = new KeyValuePair<string, ActionArgValue>(
            "damage_value_ref", ActionArgValue.Of(NumericValueRef.PayloadField(920030), "damage_value_ref"));
        Assert.False(GiveDamageSchema.Instance.TryValidateArgs(new[] { flightRankParameter }, out var missingRefError));
        Assert.Contains("requires damage_value_ref", missingRefError);

        var reversedBounds = new[]
        {
            new KeyValuePair<string, ActionArgValue>("damage_value", ActionArgValue.OfConst(100, "damage_value")),
            new KeyValuePair<string, ActionArgValue>("target_missing_hp_ratio_coefficient", ActionArgValue.OfConst(1, "target_missing_hp_ratio_coefficient")),
            new KeyValuePair<string, ActionArgValue>("target_missing_hp_bonus_min", ActionArgValue.OfConst(30, "target_missing_hp_bonus_min")),
            new KeyValuePair<string, ActionArgValue>("target_missing_hp_bonus_max", ActionArgValue.OfConst(20, "target_missing_hp_bonus_max")),
        };
        Assert.False(GiveDamageSchema.Instance.TryValidateArgs(reversedBounds, out var boundsError));
        Assert.Contains("greater than or equal", boundsError);

        var conflictingScale = new[]
        {
            new KeyValuePair<string, ActionArgValue>("damage_scale", ActionArgValue.OfConst(0.8, "damage_scale")),
            new KeyValuePair<string, ActionArgValue>("damage_scale_skill_level_parameter", ActionArgValue.OfString("secondary_ratio", "damage_scale_skill_level_parameter")),
        };
        Assert.False(GiveDamageSchema.Instance.TryValidateArgs(conflictingScale, out var scaleError));
        Assert.Contains("either a fixed value or a skill level parameter", scaleError);

        var rankedBaseWithFlightBonus = new[]
        {
            new KeyValuePair<string, ActionArgValue>("damage_value_skill_level_parameter", ActionArgValue.OfString("base_damage", "damage_value_skill_level_parameter")),
            payloadRef,
            flightRankParameter,
        };
        Assert.True(GiveDamageSchema.Instance.TryValidateArgs(rankedBaseWithFlightBonus, out var rankedError), rankedError);

        var rankedBaseWithMagnitude = new[]
        {
            new KeyValuePair<string, ActionArgValue>("damage_value_skill_level_parameter", ActionArgValue.OfString("base_damage", "damage_value_skill_level_parameter")),
            new KeyValuePair<string, ActionArgValue>("magnitude_type", ActionArgValue.OfConst(1, "magnitude_type")),
        };
        Assert.False(GiveDamageSchema.Instance.TryValidateArgs(rankedBaseWithMagnitude, out var rankedMagnitudeError));
        Assert.Contains("either a fixed value, a skill level parameter, or magnitude", rankedMagnitudeError);
    }

    [Fact]
    public void Configured_damage_formula_uses_flight_and_per_target_missing_health_then_scales_and_clamps()
    {
        var args = new GiveDamageArgs(
            damageValue: 100,
            reasonParam: 0,
            targetMissingHpBonusMax: 25,
            damageScale: 0.8f,
            damageMax: 110,
            hasTargetMissingHpBonusMax: true,
            hasDamageMax: true);

        var result = GiveDamagePlanActionModule.ApplyDamageFormula(
            Fixed64.FromInt32(100),
            Fixed64.FromInt32(20),
            Fixed64.FromSingle(0.6f),
            Fixed64.FromInt32(100),
            Fixed64.FromInt32(50),
            Fixed64.FromSingle(0.8f),
            args);

        Assert.Equal(110f, result.ToSingle());
    }

    [Fact]
    public void Missing_health_bonus_uses_each_targets_current_missing_health_amount()
    {
        var args = new GiveDamageArgs(damageValue: 50, reasonParam: 0);

        var primaryDamage = GiveDamagePlanActionModule.ApplyDamageFormula(
            Fixed64.FromInt32(50), Fixed64.FromInt32(10), Fixed64.FromSingle(0.6f),
            Fixed64.FromInt32(2000), Fixed64.FromSingle(0.25f), Fixed64.One, args);
        var secondaryDamage = GiveDamagePlanActionModule.ApplyDamageFormula(
            Fixed64.FromInt32(50), Fixed64.FromInt32(10), Fixed64.FromSingle(0.2f),
            Fixed64.FromInt32(1000), Fixed64.FromSingle(0.25f), Fixed64.One, args);

        Assert.Equal(360f, primaryDamage.ToSingle());
        Assert.Equal(110f, secondaryDamage.ToSingle());
    }

    [Fact]
    public void Configured_damage_formula_clamps_missing_health_bonus_before_final_scale()
    {
        var args = new GiveDamageArgs(
            damageValue: 80,
            reasonParam: 0,
            targetMissingHpBonusMin: 5,
            targetMissingHpBonusMax: 30,
            damageScale: 1f,
            hasTargetMissingHpBonusMin: true,
            hasTargetMissingHpBonusMax: true);

        var lowHealthBonus = GiveDamagePlanActionModule.ApplyDamageFormula(
            Fixed64.FromInt32(80), Fixed64.Zero, Fixed64.FromSingle(0.1f), Fixed64.FromInt32(200), Fixed64.FromSingle(0.25f),
            Fixed64.One, args);
        var fullHealthBonus = GiveDamagePlanActionModule.ApplyDamageFormula(
            Fixed64.FromInt32(80), Fixed64.Zero, Fixed64.Zero, Fixed64.FromInt32(200), Fixed64.FromSingle(0.25f),
            Fixed64.One, args);

        Assert.Equal(85f, lowHealthBonus.ToSingle());
        Assert.Equal(85f, fullHealthBonus.ToSingle());
    }

    [Fact]
    public void Damage_value_ref_adds_scaled_and_clamped_projectile_payload_value()
    {
        const int elapsedFramesField = 920031;
        var payloads = new PayloadAccessorRegistry();
        payloads.RegisterDoubleAccessor<object>(new ProjectilePayloadAccessor(elapsedFramesField));
        var ctx = new ExecCtx<IWorldResolver>(null, null, null, null, null, payloads, null, null, null, default, default);
        var requested = Fixed64.FromInt32(80);
        var valueRef = NumericValueRef.PayloadField(elapsedFramesField)
            .WithScale(0.5d)
            .WithMax(15d);

        var success = GiveDamagePlanActionModule.TryAddDamageValueRef(
            new ProjectileHitPayload(40d), valueRef, hasDamageValueRef: true, ctx, ref requested, out var failure);

        Assert.True(success, failure);
        Assert.Equal(95f, requested.ToSingle());
    }

    [Fact]
    public void Damage_value_ref_fails_when_required_payload_field_is_missing()
    {
        var ctx = new ExecCtx<IWorldResolver>(null, null, null, null, null, new PayloadAccessorRegistry(), null, null, null, default, default);
        var requested = Fixed64.FromInt32(80);

        var success = GiveDamagePlanActionModule.TryAddDamageValueRef(
            new object(), NumericValueRef.PayloadField(920032).AsRequired(), hasDamageValueRef: true, ctx,
            ref requested, out var failure);

        Assert.False(success);
        Assert.Contains("damage_value_ref", failure);
        Assert.Equal(80f, requested.ToSingle());
    }

    [Fact]
    public void Damage_value_without_ref_keeps_legacy_requested_damage_unchanged()
    {
        var ctx = new ExecCtx<IWorldResolver>(null, null, null, null, null, null, null, null, null, default, default);
        var requested = Fixed64.FromInt32(80);

        var success = GiveDamagePlanActionModule.TryAddDamageValueRef(
            new object(), default, hasDamageValueRef: false, ctx, ref requested, out var failure);

        Assert.True(success, failure);
        Assert.Equal(80f, requested.ToSingle());
    }

    private sealed record ProjectileHitPayload(double ElapsedFrames);

    private sealed class ProjectilePayloadAccessor : IPayloadDoubleAccessor<object>
    {
        private readonly int _fieldId;

        public ProjectilePayloadAccessor(int fieldId) => _fieldId = fieldId;

        public bool TryGet(in object args, int fieldId, out double value)
        {
            if (fieldId == _fieldId && args is ProjectileHitPayload payload)
            {
                value = payload.ElapsedFrames;
                return true;
            }

            value = default;
            return false;
        }
    }
}
