using System;
using System.Collections.Generic;
using System.Linq;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Attributes;
using AbilityKit.Demo.Moba.Components;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Demo.Moba.Config.BattleDemo;
using AbilityKit.Demo.Moba.Config.BattleDemo.MO;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.EntityConstruction;
using AbilityKit.Demo.Moba.Share.Config;
using AbilityKit.Deterministic;
using AbilityKit.Modifiers;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.Smoke;

public sealed partial class SevenHeroAlignmentRuntimeTests
{
    [Theory]
    [InlineData(3001, 1.2f, .16f)] [InlineData(3002, 1.34f, .2f)]
    [InlineData(3003, 1.6f, .16f)] [InlineData(3004, 1.6f, .16f)]
    [InlineData(3005, 1.2f, .16f)] [InlineData(3006, 1.6f, .16f)]
    [InlineData(3007, 1.8f, .16f)]
    public void Regeneration_published_rates_keep_fractions_and_level_growth(int hero, float rate, float growth)
    {
        using var b = new Battle(hero, enableRegeneration: true);
        var template = b.Services.Resolve<MobaConfigDatabase>().GetTable<BattleAttributeTemplateMO>().Get(hero);
        var initializer = new MobaActorAttributeInitializer();
        foreach (var level in new[] { 1, 9, 18 })
        {
            var attributes = initializer.Prepare(template, level);
            Assert.Equal(rate + growth * (level - 1), attributes.Group.GetValue(MobaAttributeIds.MANA_REGEN_PER_SECOND), 5);
        }
        b.Set(b.Caster, "mana", 0);
        var time = b.Services.Resolve<IFrameTime>();
        var expected = Fixed64.Zero;
        for (var i = 0; i < 150; i++)
        {
            b.Setup.Tick(1);
            expected += MobaResourceFixedConvert.ToFixed(rate) * MobaResourceFixedConvert.ToFixed(time.DeltaTime);
            Assert.Equal(expected.RawValue, ManaState(b).Current.RawValue);
        }
        Assert.InRange(b.Setup.GetActorMana(b.Caster), rate * 5 - .001f, rate * 5 + .001f);
    }

    [Fact]
    public void Regeneration_uses_effective_modifiers_and_live_max_without_stale_fallback()
    {
        using var b = new Battle(3007, enableRegeneration: true);
        b.Set(b.Caster, "mana", 100);
        var actor = b.Actor(b.Caster);
        var group = actor.attributeGroup.Group;
        var handle = group.AddModifier(MobaAttributeIds.MANA_REGEN_PER_SECOND, ModifierOp.Add, 3, 9001);
        var before = ManaState(b).Current;
        b.Setup.Tick(1);
        Assert.Equal((before + Increment(b, 4.8f)).RawValue, ManaState(b).Current.RawValue);
        Assert.True(group.RemoveModifier(MobaAttributeIds.MANA_REGEN_PER_SECOND, handle));
        before = ManaState(b).Current;
        b.Setup.Tick(1);
        Assert.Equal((before + Increment(b, 1.8f)).RawValue, ManaState(b).Current.RawValue);
        b.Set(b.Caster, "max_mana", 100.02f);
        b.Set(b.Caster, "mana", 100);
        ManaState(b).LastMax = MobaResourceFixedConvert.ToFixed(10000);
        b.Setup.Tick(1);
        Assert.Equal(MobaResourceFixedConvert.ToFixed(100.02f), ManaState(b).Current);
        b.Set(b.Caster, "max_mana", 0);
        ManaState(b).LastMax = MobaResourceFixedConvert.ToFixed(10000);
        before = ManaState(b).Current;
        b.Setup.Tick(3);
        Assert.Equal(before, ManaState(b).Current);
        b.Set(b.Caster, "max_mana", 50);
        b.Setup.Tick(1);
        Assert.Equal(before, ManaState(b).Current); // 回复不得纠正其它机制产生的超上限。
        b.Set(b.Caster, "max_mana", 600);
        b.Set(b.Caster, "MANA_REGEN_PER_SECOND", -2);
        b.Setup.Tick(3);
        Assert.Equal(before, ManaState(b).Current);
    }

    [Fact]
    public void Regeneration_dead_gate_and_zero_delta_do_not_accumulate_compensation()
    {
        using var b = new Battle(3006, enableRegeneration: true);
        b.Set(b.Caster, "mana", 100);
        var service = b.Services.Resolve<MobaResourceRegenerationService>();
        var before = ManaState(b).Current;
        service.Tick(0);
        Assert.Equal(before, ManaState(b).Current);
        var gate = b.Services.Resolve<MobaLogicWorldRunGateService>();
        gate.Reset(); service.Tick(30);
        Assert.Equal(before, ManaState(b).Current);
        gate.SetInGame();
        b.Set(b.Caster, "hp", 0);
        service.Tick(30);
        Assert.Equal(before, ManaState(b).Current);
        b.Set(b.Caster, "hp", 500);
        b.Setup.Tick(1);
        Assert.Equal((before + Increment(b, 1.6f)).RawValue, ManaState(b).Current.RawValue);
        Assert.Throws<ArgumentOutOfRangeException>(() => service.Tick(float.NaN));
        b.Actor(b.Caster).resourceContainer.Value.Map.Remove(ResourceType.Mana);
        b.Setup.Tick(2);
        Assert.False(b.Actor(b.Caster).resourceContainer.Value.Map.ContainsKey(ResourceType.Mana));
    }

    [Fact]
    public void Regeneration_full_world_restore_is_silent_and_replay_matches_every_raw_frame_twice()
    {
        using var b = new Battle(3007, enableRegeneration: true);
        b.Set(b.Caster, "mana", 100);
        var frame = b.Services.Resolve<IFrameTime>().Frame;
        var coordinator = b.CreateRollbackCoordinator();
        Assert.True(coordinator.CaptureAndStore(frame));
        var before = ManaState(b).Current.RawValue;
        var samples = new List<long>();
        for (var i = 0; i < 12; i++) { b.Setup.Tick(1); samples.Add(ManaState(b).Current.RawValue); }
        for (var attempt = 0; attempt < 2; attempt++)
        {
            b.Restore(coordinator, frame);
            Assert.Equal(before, ManaState(b).Current.RawValue);
            foreach (var expected in samples)
            {
                b.Setup.Tick(1);
                Assert.Equal(expected, ManaState(b).Current.RawValue);
            }
        }
    }

    [Fact]
    public void Regeneration_cast_consumes_mana_and_published_indicator_tracks_regeneration()
    {
        using var b = new Battle(3007, targetZ: 20, enableRegeneration: true);
        var full = ManaState(b).Current;
        Assert.True(b.Skills.TryCastBySlot(b.Caster, 1, new Vec3(0, 0, 20), Vec3.Forward).Success);
        b.Setup.Tick(1);
        var afterCast = ManaState(b).Current;
        Assert.True(afterCast < full);
        b.Setup.Tick(30);
        Assert.True(ManaState(b).Current > afterCast);
        var row = Assert.Single(ReadOverhead(b), e => e.ActorId == b.Caster && e.IndicatorId == 2);
        Assert.Equal(b.Setup.GetActorMana(b.Caster), row.Current);
        Assert.Equal(b.Actor(b.Caster).attributeGroup.Group.GetValue(MobaAttributeIds.MAX_MANA), row.Max);
    }

    private static ResourceState ManaState(Battle b) => b.Actor(b.Caster).resourceContainer.Value.Map[ResourceType.Mana];
    private static Fixed64 Increment(Battle b, float rate) => MobaResourceFixedConvert.ToFixed(rate) *
        MobaResourceFixedConvert.ToFixed(b.Services.Resolve<IFrameTime>().DeltaTime);

    [Fact]
    public void Regeneration_movement_continues_and_aura_pays_after_frame_regeneration()
    {
        using var b = new Battle(3006, targetZ: 20, enableRegeneration: true);
        b.Set(b.Caster, "mana", 100);
        var actor = b.Actor(b.Caster);
        var origin = actor.transform.Value.Position;
        actor.ReplaceMoveInput(1, 0);
        b.Setup.Tick(15);
        Assert.True(actor.transform.Value.Position.X > origin.X);
        Assert.Equal((Fixed64.FromInt32(100) + Increment(b, 1.6f) * Fixed64.FromInt32(15)).RawValue,
            ManaState(b).Current.RawValue);
        actor.ReplaceMoveInput(0, 0);
        Assert.True(b.Skills.TryCastBySlot(b.Caster, 3, default, Vec3.Forward).Success);
        var paid = 0;
        for (var i = 0; i < 30; i++)
        {
            var before = ManaState(b).Current;
            b.Setup.Tick(1);
            var spent = before + Increment(b, 1.6f) - ManaState(b).Current;
            if (spent == Fixed64.Zero) continue;
            Assert.Equal(Fixed64.FromSingle(7.5f), spent);
            paid++;
        }
        Assert.InRange(paid, 3, 4);
    }

    [Fact]
    public void Regeneration_during_published_long_channel_is_never_paused_by_cast_state()
    {
        using var b = new Battle(3006, targetZ: 20, enableRegeneration: true);
        b.Set(b.Caster, "mana", 300);
        Assert.True(b.Skills.TryCastBySlot(b.Caster, 4, default, Vec3.Forward).Success);
        for (var i = 0; i < 60; i++)
        {
            var before = ManaState(b).Current;
            b.Setup.Tick(1);
            var spent = before + Increment(b, 1.6f) - ManaState(b).Current;
            Assert.True(spent == Fixed64.Zero || spent == Fixed64.FromInt32(100),
                $"引导第 {i + 1} 帧必须正常回复，费用由原提交时机消费一次；实际差额 raw={spent.RawValue}");
        }
    }

    [Fact]
    public void Regeneration_energy_rule_reuses_executor_and_never_regenerates_hp_ammo_or_mana_implicitly()
    {
        using var b = new Battle(3001);
        var config = new MobaConfigDatabase();
        var rows = MobaConfigRegistry.Instance.Tables.ToDictionary(table => table.DtoType,
            table => Array.CreateInstance(table.DtoType, 0));
        rows[typeof(ResourceRegenerationRuleDTO)] = new[] { new ResourceRegenerationRuleDTO
            { Id = 1, ResourceType = (int)ResourceType.Energy, FlatPerSecondAttribute = 24 } };
        rows[typeof(AttrTypeDTO)] = new[] { new AttrTypeDTO { Id = 24, Key = "MANA_REGEN_PER_SECOND", ValueKind = 0 } };
        config.LoadFromDtoArrays(rows);
        var actor = b.Actor(b.Caster);
        var map = actor.resourceContainer.Value.Map;
        map[ResourceType.Energy] = new ResourceState { Current = Fixed64.Zero, LastMax = Fixed64.FromInt32(100) };
        map[ResourceType.Ammo] = new ResourceState { Current = Fixed64.FromInt32(2), LastMax = Fixed64.FromInt32(4) };
        b.Set(b.Caster, "mana", 100);
        b.Set(b.Caster, "MANA_REGEN_PER_SECOND", 2.5f);
        var hp = map[ResourceType.Hp].Current;
        using var service = new MobaResourceRegenerationService(b.Services.Resolve<MobaActorRegistry>(),
            b.Services.Resolve<MobaLogicWorldRunGateService>(), config);
        service.Tick(.2f);
        Assert.Equal(MobaResourceFixedConvert.ToFixed(2.5f) * MobaResourceFixedConvert.ToFixed(.2f), map[ResourceType.Energy].Current);
        Assert.Equal(hp, map[ResourceType.Hp].Current);
        Assert.Equal(Fixed64.FromInt32(2), map[ResourceType.Ammo].Current);
        Assert.Equal(Fixed64.FromInt32(100), map[ResourceType.Mana].Current);
    }
}
