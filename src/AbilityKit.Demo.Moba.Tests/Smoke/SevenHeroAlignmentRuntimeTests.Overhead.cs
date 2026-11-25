using System;
using System.Linq;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.World.Services.Attributes;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Attributes;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Demo.Moba.Config.BattleDemo.MO;
using AbilityKit.Demo.Moba.Gameplay;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.Projection;
using AbilityKit.Demo.Moba.Services.StateMachine;
using AbilityKit.Demo.Moba.Share;
using AbilityKit.Demo.Moba.Share.Config;
using AbilityKit.Modifiers;
using AbilityKit.Protocol.Moba;
using AbilityKit.Protocol.Moba.StateSync;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.Smoke;

public sealed partial class SevenHeroAlignmentRuntimeTests
{
    [Theory]
    [InlineData(3001)] [InlineData(3002)] [InlineData(3003)] [InlineData(3004)]
    [InlineData(3005)] [InlineData(3006)] [InlineData(3007)]
    public void Overhead_published_hero_baseline_tracks_current_hp_mana_and_max_without_damage_events(int hero)
    {
        using var b = new Battle(hero);
        var emitter = b.Services.Resolve<MobaActorIndicatorSnapshotService>();
        var before = ReadOverhead(b);
        Assert.Equal(2, before.Count(e => e.ActorId == b.Caster && e.IndicatorId <= 2));
        b.Set(b.Caster, "max_hp", 2200); b.Set(b.Caster, "hp", 715);
        b.Set(b.Caster, "max_mana", 600); b.Set(b.Caster, "mana", 131);
        var current = ReadOverhead(b);
        var hp = Assert.Single(current, e => e.ActorId == b.Caster && e.IndicatorId == 1);
        var mana = Assert.Single(current, e => e.ActorId == b.Caster && e.IndicatorId == 2);
        Assert.Equal(715, hp.Current); Assert.Equal(2200, hp.Max);
        Assert.Equal(131, mana.Current); Assert.Equal(600, mana.Max);
        var decoded = ActorIndicatorSnapshotDecoder.Decode(MobaActorIndicatorSnapshotCodec.Serialize(current));
        Assert.Contains(decoded, e => e.ActorId == b.Caster && e.Current == 131);
        emitter.RequestActiveBaseline(); Assert.True(emitter.ConsumeActiveBaselineRequest());
        Assert.False(emitter.ConsumeActiveBaselineRequest());
    }

    [Fact]
    public void Overhead_Jhin_follows_effective_ammo_and_R_final_candidate_across_counter_reset()
    {
        using var b = new Battle(3001, 20);
        var parameters = b.Services.Resolve<MobaCharacterHfsmParameterService>();
        for (var slot = 0; slot <= 5; slot++)
        {
            var modifier = ModifierData.Override(ModifierKey.FromPacked(10), slot, sourceId: 9876);
            modifier.Priority = 999; parameters.Set(b.Caster, 9876, 10, in modifier);
            var row = Assert.Single(ReadOverhead(b), e => e.ActorId == b.Caster && e.IndicatorId == 10);
            Assert.Equal(slot <= 1 ? 4 : 5 - slot, row.Current);
        }
        parameters.ClearSource(b.Caster, 9876);
        Assert.True(b.Skills.TryCastBySlot(b.Caster, 4, default, Vec3.Forward).Success);
        b.Setup.TickMilliseconds(700);
        for (var shot = 0; shot <= 3; shot++)
        {
            var row = Assert.Single(ReadOverhead(b), e => e.ActorId == b.Caster && e.IndicatorId == 11);
            Assert.Equal(1, row.Availability); Assert.Equal(4 - shot, row.Current);
            if (shot < 3)
            {
                Assert.True(b.Skills.TryCastBySlot(b.Caster, 5, default, Vec3.Forward).Success);
                b.Setup.TickMilliseconds(1100);
            }
        }
        var variables = b.Services.Resolve<MobaGameplayVariableService>();
        Assert.Equal(0, variables.Get(MobaGameplayVariableKeys.ResolveScopedKey(30010401, b.Caster)));
        Assert.True(b.Skills.TryCastBySlot(b.Caster, 5, default, Vec3.Forward).Success);
        b.Setup.TickMilliseconds(1100);
        Assert.Equal(2, Assert.Single(ReadOverhead(b), e => e.ActorId == b.Caster && e.IndicatorId == 11).Availability);
    }

    [Fact]
    public void Overhead_counter_missing_zero_ready_and_restore_are_value_projections()
    {
        using var b = new Battle(3003);
        var variables = b.Services.Resolve<MobaGameplayVariableService>();
        var key = MobaGameplayVariableKeys.ResolveScopedKey(30030002, b.Caster);
        variables.Set(key, 2); var checkpoint = variables.Capture();
        Assert.Equal(2, Assert.Single(ReadOverhead(b), e => e.ActorId == b.Caster && e.IndicatorId == 12).Current);
        variables.Set(key, 4);
        Assert.True(Assert.Single(ReadOverhead(b), e => e.ActorId == b.Caster && e.IndicatorId == 12).Ready);
        variables.Restore(checkpoint);
        var restored = Assert.Single(ReadOverhead(b), e => e.ActorId == b.Caster && e.IndicatorId == 12);
        Assert.Equal(2, restored.Current); Assert.False(restored.Ready);
    }

    [Fact]
    public void Overhead_Veigar_uses_growth_stacks_not_total_AP_and_zero_max_is_unavailable()
    {
        using var b = new Battle(3004);
        b.Set(b.Caster, "MAGIC_ATTACK", 800);
        var row = Assert.Single(ReadOverhead(b), e => e.ActorId == b.Caster && e.IndicatorId == 13);
        Assert.False(row.HasMax); Assert.Equal(0, row.Current);
        var config = b.Services.Resolve<MobaConfigDatabase>();
        var projector = new MobaActorIndicatorProjector(config, b.Services.Resolve<MobaGameplayVariableService>(), b.Services.Resolve<MobaCharacterHfsmParameterService>());
        b.Set(b.Caster, "max_mana", 0);
        Assert.Equal(0, projector.Project(b.Caster, b.Actor(b.Caster), 2,
            config.GetTable<IndicatorDefinitionMO>().Get(2).Definition).Availability);
        var fractional = new IndicatorDefinitionDTO { Id = 999, SourceKind = IndicatorSourceKind.Resource,
            SourceKey = 2, Max = 100, Scale = .5f };
        Assert.Equal(1, projector.Project(b.Caster, b.Actor(b.Caster), 2, fractional).Availability);
    }

    [Fact]
    public void Overhead_wire_empty_baseline_is_distinct_from_missing_or_invalid_payload()
    {
        var bytes = MobaActorIndicatorSnapshotCodec.Serialize(Array.Empty<MobaActorIndicatorSnapshotEntry>());
        Assert.NotEmpty(bytes); Assert.Empty(MobaActorIndicatorSnapshotCodec.Deserialize(bytes));
        Assert.Throws<ArgumentException>(() => MobaActorIndicatorSnapshotCodec.Deserialize(Array.Empty<byte>()));
        Assert.Throws<ArgumentException>(() => MobaActorIndicatorSnapshotCodec.Serialize(new[] {
            new MobaActorIndicatorSnapshotEntry { ActorId=1, LayoutId=2, IndicatorId=1, Current=float.NaN } }));
        var golden = Convert.FromHexString("01010000000700000002000000020000000000F64200002F440100000001000000");
        var entries = new[] { new MobaActorIndicatorSnapshotEntry { ActorId = 7, LayoutId = 2, IndicatorId = 2,
            Current = 123, Max = 700, HasMax = true, Availability = 1 } };
        Assert.Equal(golden, MobaActorIndicatorSnapshotCodec.Serialize(entries));
        Assert.Equal(123, Assert.Single(MobaActorIndicatorSnapshotCodec.Deserialize(golden)).Current);
    }

    private static MobaActorIndicatorSnapshotEntry[] ReadOverhead(Battle b)
    {
        Assert.True(b.Services.Resolve<MobaActorIndicatorSnapshotService>().TryGetActiveBaseline(out var snapshot));
        Assert.Equal(MobaOpCodes.Snapshot.ActorIndicatorState, snapshot.OpCode);
        return MobaActorIndicatorSnapshotCodec.Deserialize(snapshot.Payload);
    }

    [Fact]
    public void Overhead_frame_input_replay_rebuilds_the_same_mana_and_passive_without_accumulation()
    {
        using var b = new Battle(3003);
        var clock = b.Services.Resolve<IFrameTime>();
        var checkpoint = clock.Frame;
        var coordinator = b.CreateRollbackCoordinator();
        Assert.True(coordinator.CaptureAndStore(checkpoint));
        var before = ReadOverhead(b);
        var frame = new FrameIndex(checkpoint.Value + 1);
        var evt = new SkillInputEvent(1, SkillInputPhase.Press, targetActorId: b.Target,
            predictionKey: 91, targetFrame: frame.Value, inputSequence: 1,
            entityVersion: b.Services.Resolve<MobaActorRegistry>().GetEntityVersion(b.Caster));
        var commands = new[] { new PlayerInputCommand(frame, new PlayerId("alignment_caster"),
            MobaOpCodes.Input.SkillInput, SkillInputCodec.Serialize(in evt)) };
        Assert.True(b.Services.Resolve<IMobaInputCoordinator>().TrySubmit(frame, commands).Succeeded);
        b.Setup.Tick(35);
        var after = MobaActorIndicatorSnapshotCodec.Serialize(ReadOverhead(b));
        Assert.True(Assert.Single(ReadOverhead(b), e => e.ActorId == b.Caster && e.IndicatorId == 2).Current <
            Assert.Single(before, e => e.ActorId == b.Caster && e.IndicatorId == 2).Current);
        b.Restore(coordinator, checkpoint);
        Assert.Equal(MobaActorIndicatorSnapshotCodec.Serialize(before), MobaActorIndicatorSnapshotCodec.Serialize(ReadOverhead(b)));
        var replay = b.Services.Resolve<IWorldInputReplaySink>();
        replay.BeginReplay(checkpoint, new FrameIndex(checkpoint.Value + 35));
        try { replay.Replay(frame, commands); b.Setup.Tick(35); }
        finally { replay.EndReplay(); }
        Assert.Equal(after, MobaActorIndicatorSnapshotCodec.Serialize(ReadOverhead(b)));
    }
}
