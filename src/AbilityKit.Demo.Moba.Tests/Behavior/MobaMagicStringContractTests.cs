using AbilityKit.Demo.Moba.ActionTimeline;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Demo.Moba.Predicates;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Moba.Behavior;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.Behavior;

public sealed class MobaMagicStringContractTests
{
    [Fact]
    public void Predicate_enums_preserve_serialized_integer_values()
    {
        Assert.Equal(0, (int)CombatPredicateTargetMode.Target);
        Assert.Equal(1, (int)CombatPredicateTargetMode.Source);
        Assert.Equal(0, (int)HealthPercentCompareType.LessThan);
        Assert.Equal(1, (int)HealthPercentCompareType.GreaterThan);
    }

    [Fact]
    public void Predicate_string_contracts_preserve_external_values()
    {
        Assert.Equal("has_buff", CombatPredicateContracts.Type.HasBuff);
        Assert.Equal("health_percent", CombatPredicateContracts.Type.HealthPercent);
        Assert.Equal("buff_id", CombatPredicateContracts.Argument.BuffId);
        Assert.Equal("target_mode", CombatPredicateContracts.Argument.TargetMode);
        Assert.Equal("compare_type", CombatPredicateContracts.Argument.CompareType);
        Assert.Equal("predicate:has_buff", CombatPredicateContracts.Function.HasBuff);
        Assert.Equal(
            "predicate:target_is_flying_projectile",
            CombatPredicateContracts.Function.TargetIsFlyingProjectile);
    }

    [Fact]
    public void Behavior_string_contracts_preserve_framework_boundary_values()
    {
        Assert.Equal("Channeling", MobaBehaviorContracts.Phase.Channeling);
        Assert.Equal("Following", MobaBehaviorContracts.State.Following);
        Assert.Equal("TargetInvalid", MobaBehaviorContracts.InterruptReason.TargetInvalid);
        Assert.Equal("TargetDied", MobaBehaviorContracts.InterruptReason.TargetDied);
        Assert.Equal("currentState", MobaBehaviorContracts.ContextKey.CurrentState);
        Assert.Equal("MobaWorldQuery", MobaBehaviorContracts.ContextKey.WorldQuery);
        Assert.Equal("HP", MobaBehaviorContracts.WorldDataKey.HitPoints);
        Assert.Equal("MoveSpeed", MobaBehaviorContracts.WorldDataKey.MoveSpeed);
    }

    [Fact]
    public void Shared_configuration_and_timeline_keys_preserve_external_values()
    {
        Assert.Equal("moba.config", MobaConfigDatabase.ReloadConfigKey);
        Assert.Equal("log", TriggerLogHandler.LogArgumentKey);
    }

    [Fact]
    public void Gameplay_tag_aliases_preserve_legacy_sleep_names_in_blocking_groups()
    {
        Assert.Contains(MobaGameplayTagCatalog.State.Asleep, MobaGameplayTagCatalog.AsleepAliases);
        Assert.Contains("Asleep", MobaGameplayTagCatalog.AsleepAliases);
        Assert.Contains("Sleeping", MobaGameplayTagCatalog.AsleepAliases);
        Assert.Contains("sleeping", MobaGameplayTagCatalog.AsleepAliases);
        Assert.Contains("Sleeping", MobaGameplayTagCatalog.MoveBlockedAliases);
        Assert.Contains("Sleeping", MobaGameplayTagCatalog.CastBlockedAliases);
        Assert.Contains("Sleeping", MobaGameplayTagCatalog.ControlBlockedAliases);
    }
}
