using System.Linq;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Core.Eventing;
using AbilityKit.Demo.Moba;
using AbilityKit.Demo.Moba.Components;
using AbilityKit.Demo.Moba.Gameplay.Triggering;
using AbilityKit.Demo.Moba.Rollback;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Console;
using AbilityKit.Demo.Moba.Console.Battle.Config;
using AbilityKit.Deterministic;
using AbilityKit.Triggering.Eventing;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.Smoke;

public sealed class MobaShieldDamagePayloadSmokeTests
{
    [Fact]
    public void Damage_after_apply_exposes_committed_shield_absorb_and_rollback_restores_it_for_replay()
    {
        using var battle = StartBattle();
        var services = battle.RuntimeServices!;
        var actors = services.Resolve<MobaActorRegistry>();
        var shields = services.Resolve<MobaShieldService>();
        var damage = services.Resolve<DamagePipelineService>();
        var attacker = actors.Entries.First(entry => entry.Value != null && entry.Value.hasTeam && entry.Value.hasResourceContainer);
        var target = actors.Entries.First(entry => entry.Value != null && entry.Value.hasTeam && entry.Value.hasResourceContainer &&
            entry.Value.team.Value != attacker.Value.team.Value);

        shields.AddShield(target.Key, new ShieldLayer
        {
            ShieldId = 900001,
            SourceActorId = attacker.Key,
            OwnerActorId = attacker.Key,
            TargetActorId = target.Key,
            CurrentValue = Fixed64.FromInt32(6),
            MaxValue = Fixed64.FromInt32(6),
            InitialValue = Fixed64.FromInt32(6),
            AbsorbRatio = Fixed64.One,
            DamageTypeMask = (int)(DamageType.Physical | DamageType.Magic | DamageType.True),
            StartFrame = 0,
            ExpireFrame = int.MaxValue,
            RemoveWhenDepleted = true,
            StackingPolicy = ShieldStackingPolicy.Independent,
            ConsumePolicy = ShieldConsumePolicy.PriorityThenOldest,
        });

        var frame = new FrameIndex(battle.Context.LastFrame);
        var shieldRollback = new MobaShieldRollbackProvider(actors, shields);
        var shieldCheckpoint = shieldRollback.Export(frame);
        DamageResult afterApply = null;
        var afterApplyEventId = TriggeringIdUtil.GetEventEid(DamagePipelineEvents.AfterApply);
        using var subscription = services.Resolve<IEventBus>()
            .Subscribe(new EventKey<DamageResult>(afterApplyEventId), result => afterApply = result);
        var accessor = new MobaBattlePayloadAccessor();
        var shieldAbsorbField = MobaBattlePayloadFields.FieldId(MobaBattlePayloadFields.ShieldAbsorb);
        double payloadAbsorb;

        var firstResult = damage.Execute(CreateTrueDamage(attacker.Key, target.Key, 10f));

        Assert.NotNull(firstResult);
        Assert.Equal(6f, firstResult.ShieldAbsorb, 3);
        Assert.Same(firstResult, afterApply);
        Assert.True(accessor.TryGet(in afterApply, shieldAbsorbField, out payloadAbsorb));
        Assert.Equal(6d, payloadAbsorb, 3);
        Assert.Equal(0, shields.GetPresentationSnapshot(target.Key).TotalVisibleValue.ToSingle(), 3);

        shieldRollback.Import(frame, shieldCheckpoint);

        Assert.Equal(6, shields.GetPresentationSnapshot(target.Key).TotalVisibleValue.ToSingle(), 3);
        afterApply = null;
        var replayResult = damage.Execute(CreateTrueDamage(attacker.Key, target.Key, 10f));
        Assert.NotNull(replayResult);
        Assert.Equal(6f, replayResult.ShieldAbsorb, 3);
        Assert.Same(replayResult, afterApply);
        Assert.True(accessor.TryGet(in afterApply, shieldAbsorbField, out payloadAbsorb));
        Assert.Equal(6d, payloadAbsorb, 3);
    }

    private static AttackInfo CreateTrueDamage(int attackerActorId, int targetActorId, float amount)
    {
        var attack = new AttackInfo
        {
            AttackerActorId = attackerActorId,
            TargetActorId = targetActorId,
            DamageType = DamageType.True,
            CritType = CritType.None,
            ReasonKind = DamageReasonKind.Skill,
        };
        attack.BaseDamage.BaseValue = amount;
        return attack;
    }

    private static ConsoleBattleBootstrapper StartBattle()
    {
        var battle = new ConsoleBattleBootstrapper(BattleStartConfig.CreateDefault());
        battle.Initialize();
        battle.Start();
        for (var i = 0; i < 8 && battle.Context.EcsWorld == null; i++) battle.Tick();
        battle.SetupBattle();
        for (var i = 0; i < 10; i++) battle.Tick();
        return battle;
    }
}
