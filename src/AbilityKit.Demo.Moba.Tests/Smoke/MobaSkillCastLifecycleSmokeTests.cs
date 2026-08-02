using System.Collections.Generic;
using System.Linq;
using AbilityKit.Demo.Moba;
using AbilityKit.Demo.Moba.Attributes;
using AbilityKit.Demo.Moba.Components;
using AbilityKit.Demo.Moba.Console;
using AbilityKit.Demo.Moba.Console.Battle.Config;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Trace;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.Smoke;

public sealed class MobaSkillCastLifecycleSmokeTests
{
    private const int TestSkillId = 9900001;
    private const int TestSkillSlot = 4;

    [Fact]
    public void Dead_actor_cast_rejection_does_not_allocate_runtime_or_root_trace()
    {
        using var battle = StartBattle();
        var services = battle.RuntimeServices!;
        var registry = services.Resolve<MobaActorRegistry>();
        var casts = services.Resolve<SkillCastCoordinator>();
        var runtimes = services.Resolve<MobaSkillCastRuntimeService>();
        var trace = services.Resolve<MobaTraceRegistry>();
        var damage = services.Resolve<DamagePipelineService>();
        var (casterId, enemyId) = FindTestCasterAndEnemy(registry);
        Assert.True(registry.TryGet(casterId, out var caster) && caster != null);

        ExecuteLethalDamage(
            damage,
            enemyId,
            casterId,
            caster.GetMobaAttrs().Hp);

        var runtimeCountBefore = runtimes.Count;
        var traceCountBefore = CountSkillCastRoots(trace, casterId);
        var result = casts.TryCastSkill(casterId, TestSkillId, TestSkillSlot);

        Assert.False(result.Success);
        Assert.False(result.RuntimeHandle.IsValid);
        Assert.Equal(runtimeCountBefore, runtimes.Count);
        Assert.Equal(traceCountBefore, CountSkillCastRoots(trace, casterId));
    }

    [Fact]
    public void Death_during_cast_cancels_runtime_and_ends_root_trace_as_dead()
    {
        using var battle = StartBattle();
        var services = battle.RuntimeServices!;
        var registry = services.Resolve<MobaActorRegistry>();
        var casts = services.Resolve<SkillCastCoordinator>();
        var runtimes = services.Resolve<MobaSkillCastRuntimeService>();
        var trace = services.Resolve<MobaTraceRegistry>();
        var damage = services.Resolve<DamagePipelineService>();
        var (casterId, enemyId) = FindTestCasterAndEnemy(registry);
        Assert.True(registry.TryGet(casterId, out var caster) && caster != null);
        InstallTestSkill(caster);

        var cast = casts.TryCastSkill(casterId, TestSkillId, TestSkillSlot);
        Assert.True(cast.Success, cast.FailReason);
        var runtimeHandle = cast.RuntimeHandle;
        Assert.True(runtimeHandle.IsValid);
        Assert.True(runtimes.TryGet(in runtimeHandle, out _));
        Assert.True(casts.TryGetRunningByInstanceId(
            casterId,
            runtimeHandle.RootTraceContextId,
            out _));

        ExecuteLethalDamage(
            damage,
            enemyId,
            casterId,
            caster.GetMobaAttrs().Hp);
        battle.Tick();

        Assert.False(runtimes.TryGet(in runtimeHandle, out _));
        Assert.False(casts.TryGetRunningByInstanceId(
            casterId,
            runtimeHandle.RootTraceContextId,
            out _));
        Assert.True(trace.TryGetNodeSnapshot(
            runtimeHandle.RootTraceContextId,
            out var root));
        Assert.True(root.IsEnded);
        Assert.Equal((int)TraceLifecycleReason.Cancelled, root.EndReason);
    }

    [Fact]
    public void Despawn_during_cast_removes_actor_runner_and_runtime_state()
    {
        using var battle = StartBattle();
        var services = battle.RuntimeServices!;
        var registry = services.Resolve<MobaActorRegistry>();
        var casts = services.Resolve<SkillCastCoordinator>();
        var runtimes = services.Resolve<MobaSkillCastRuntimeService>();
        var trace = services.Resolve<MobaTraceRegistry>();
        var authority = services.Resolve<MobaAuthorityFrameService>();
        var (casterId, _) = FindTestCasterAndEnemy(registry);
        Assert.True(registry.TryGet(casterId, out var caster) && caster != null);
        InstallTestSkill(caster);

        var cast = casts.TryCastSkill(casterId, TestSkillId, TestSkillSlot);
        Assert.True(cast.Success, cast.FailReason);
        var runtimeHandle = cast.RuntimeHandle;
        Assert.True(runtimes.TryGet(in runtimeHandle, out _));

        var confirmedFrame = authority.ConfirmedFrame.Value;
        caster.AddActorDespawnRequest(
            confirmedFrame,
            confirmedFrame,
            ActorDespawnReason.SceneCleanup,
            0,
            runtimeHandle.RootTraceContextId);
        for (var i = 0; i < 8 && registry.TryGet(casterId, out _); i++)
        {
            battle.Tick();
        }

        Assert.False(registry.TryGet(casterId, out _));
        Assert.False(runtimes.TryGet(in runtimeHandle, out _));

        var snapshots = new List<SkillPipelineRunner.RunningSnapshot>();
        casts.FillRunningSnapshots(casterId, snapshots);
        Assert.Empty(snapshots);
        casts.FillEndedSnapshots(casterId, snapshots);
        Assert.Empty(snapshots);

        Assert.True(trace.TryGetNodeSnapshot(
            runtimeHandle.RootTraceContextId,
            out var root));
        Assert.True(root.IsEnded);
        Assert.Equal((int)TraceLifecycleReason.Dead, root.EndReason);
    }

    private static ConsoleBattleBootstrapper StartBattle()
    {
        var battle = new ConsoleBattleBootstrapper(BattleStartConfig.CreateDefault());
        battle.Initialize();
        battle.Start();
        for (var i = 0; i < 8 && battle.Context.EcsWorld == null; i++)
        {
            battle.Tick();
        }

        battle.SetupBattle();
        for (var i = 0; i < 10; i++)
        {
            battle.Tick();
        }

        Assert.NotNull(battle.RuntimeServices);
        return battle;
    }

    private static (int CasterId, int EnemyId) FindTestCasterAndEnemy(
        MobaActorRegistry registry)
    {
        var caster = registry.Entries.First(entry =>
            entry.Value != null &&
            entry.Value.hasModelId &&
            entry.Value.modelId.Value == 1001 &&
            entry.Value.hasTeam &&
            entry.Value.hasAttributeGroup &&
            entry.Value.hasResourceContainer);
        var enemy = registry.Entries.First(entry =>
            entry.Value != null &&
            entry.Value.hasTeam &&
            entry.Value.team.Value != caster.Value.team.Value &&
            entry.Value.hasAttributeGroup);
        return (caster.Key, enemy.Key);
    }

    private static void InstallTestSkill(ActorEntity caster)
    {
        var activeSkills = caster.skillLoadout.ActiveSkills?.ToArray() ??
                           System.Array.Empty<ActiveSkillRuntime>();
        if (activeSkills.Length < TestSkillSlot)
        {
            System.Array.Resize(ref activeSkills, TestSkillSlot);
        }

        activeSkills[TestSkillSlot - 1] = new ActiveSkillRuntime
        {
            SkillId = TestSkillId,
            Level = 1,
        };
        caster.ReplaceSkillLoadout(
            activeSkills,
            caster.skillLoadout.PassiveSkills);
    }

    private static int CountSkillCastRoots(MobaTraceRegistry trace, int actorId)
    {
        return trace.GetNodesByKind((int)MobaTraceKind.SkillCast).Count(node =>
            node.IsRoot &&
            node.Metadata != null &&
            node.Metadata.SourceActorId == actorId);
    }

    private static DamageResult ExecuteLethalDamage(
        DamagePipelineService damage,
        int attackerActorId,
        int targetActorId,
        float targetHp)
    {
        var attack = new AttackInfo
        {
            AttackerActorId = attackerActorId,
            TargetActorId = targetActorId,
            DamageType = DamageType.Physical,
            ReasonKind = DamageReasonKind.Environment,
        };
        attack.BaseDamage.BaseValue = targetHp * 10f + 1f;
        return damage.Execute(attack);
    }
}
