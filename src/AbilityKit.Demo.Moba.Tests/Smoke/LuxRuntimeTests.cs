using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Acceptance.LiveSim;
using AbilityKit.Demo.Moba.Attributes;
using AbilityKit.Demo.Moba.Console;
using AbilityKit.Demo.Moba.Console.Battle.Config;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.Area;
using AbilityKit.Demo.Moba.Services.Buffs;
using AbilityKit.Game.Test.UnitTest;
using AbilityKit.Demo.Moba;
using AbilityKit.Protocol.Moba;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.FrameSync.Rollback;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Demo.Moba.Rollback;
using System.Reflection;
using AbilityKit.Deterministic;
using AbilityKit.Trace;
using AbilityKit.Demo.Moba.Share;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.Smoke;

/// <summary>Exercises production tables, imported Trigger plans and actual battle systems.</summary>
public sealed class LuxRuntimeTests
{
    [Theory]
    [InlineData(0,1)]
    [InlineData(1,0)]
    [InlineData(0,-1)]
    public void R_authoritative_presentation_frame_survives_auxiliary_recast_and_whole_world_restore(float dx,float dz)
    {
        using var b=new Battle();
        b.Cast(3,new Vec3(100,0,106));b.Setup.TickMilliseconds(1500);
        var direction=new Vec3(dx,0,dz);
        Assert.True(b.Services.Resolve<SkillCastCoordinator>().TryCastBySlot(b.Caster,4,
            new Vec3(100+dx*20,0,100+dz*20),direction,0).Success);
        b.Setup.Tick(2);
        var action=b.Actor(b.Caster).characterHfsm.Runtime.Action;
        var casts=b.Services.Resolve<MobaSkillCastRuntimeService>();
        Assert.True(casts.TryGetPresentationFrame(action.CastInstanceId,b.Caster,out var original));
        Assert.Equal(100,original.OriginX,3);Assert.Equal(100,original.OriginZ,3);
        Assert.Equal(dx,original.ForwardX,3);Assert.Equal(dz,original.ForwardZ,3);
        Assert.True(casts.TryGetByContext(action.CastInstanceId,out var cast));
        Assert.True(casts.TryCapturePresentationFrame(cast.Handle,b.Caster,new Vec3(8,0,9),Vec3.Right));
        Assert.True(casts.TryGetPresentationFrame(action.CastInstanceId,b.Caster,out var same));Assert.Equal(original,same);
        var rollback=b.CreateRollback();var checkpoint=rollback.Capture(b.Services.Resolve<IFrameTime>().Frame);
        b.Cast(3,new Vec3(100,0,106));b.Setup.Tick(2);
        Assert.Equal(action.CastInstanceId,b.Actor(b.Caster).characterHfsm.Runtime.Action.CastInstanceId);
        Assert.True(casts.TryGetPresentationFrame(action.CastInstanceId,b.Caster,out same));Assert.Equal(original,same);
        b.Set(b.Caster,"hp",0);b.Setup.Tick(2);
        Assert.False(casts.TryGetPresentationFrame(action.CastInstanceId,b.Caster,out _));
        rollback.Restore(checkpoint);
        Assert.True(casts.TryGetPresentationFrame(action.CastInstanceId,b.Caster,out var restored));Assert.Equal(original,restored);
        b.Setup.Tick(2);
        Assert.True(b.Services.Resolve<MobaCharacterActionSnapshotService>().TryGetSnapshot(
            new FrameIndex(b.Services.Resolve<IFrameTime>().Frame.Value+1),out var snapshot));
        Assert.True(CharacterActionSnapshotCodec.TryDecode(snapshot.Payload,out var entries));
        Assert.Equal(original,Assert.Single(entries,e=>e.ActorId==b.Caster).CastFrame);
    }

    [Fact]
    public void Q_hits_first_two_enemies_roots_and_marks_each_once()
    {
        using var b = new Battle();
        var a = b.Spawn("a",1001,2,100,103);
        var ally = b.Spawn("ally",1001,1,100,102);
        var c = b.Spawn("c",1001,2,100,106);
        var d = b.Spawn("d",1001,2,100,109);
        b.Set(b.Caster,"MAGIC_ATTACK",100); b.Setup.Tick(2);
        var hp=b.Setup.GetActorHp(a);
        b.Cast(1,new Vec3(100,0,111)); b.Setup.TickMilliseconds(1600);
        Assert.InRange(hp-b.Setup.GetActorHp(a),144.98f,145.02f);
        Assert.InRange(b.Setup.GetActorMaxHp(c)-b.Setup.GetActorHp(c),144.98f,145.02f);
        Assert.Equal(b.Setup.GetActorMaxHp(d),b.Setup.GetActorHp(d));
        Assert.Equal(b.Setup.GetActorMaxHp(ally),b.Setup.GetActorHp(ally));
        Assert.Equal(1,b.Setup.CountActorBuffs(a,30070290));
        Assert.Equal(1,b.Setup.CountActorBuffs(c,30070090));
        Assert.Equal(2,b.Count(MobaExecutionKind.DamageApply,30070201));
    }

    [Fact]
    public void Passive_consumes_only_current_caster_version_with_current_ap()
    {
        using var b = new Battle();
        var enemy=b.Spawn("a",1001,2,100,103);
        var other=b.Spawn("other",3007,1,110,100);
        var buffs=b.Services.Resolve<MobaBuffService>();
        Assert.True(buffs.ApplyBuffImmediate(enemy,30070090,b.Caster,6000));
        Assert.True(buffs.ApplyBuffImmediate(enemy,30070090,other,6000));
        b.Set(b.Caster,"MAGIC_ATTACK",200); b.Setup.Tick(2);
        var hp=b.Setup.GetActorHp(enemy);
        b.Cast(5,new Vec3(100,0,103),enemy);b.Setup.TickMilliseconds(1600);
        Assert.InRange(hp-b.Setup.GetActorHp(enemy),143.98f,144.02f); // 54 + 30 + .3 * 200.
        Assert.Equal(1,b.Setup.CountActorBuffs(enemy,30070090));
        Assert.False(buffs.TryReadSourceBuff(enemy,30070090,b.Caster,out _));
        Assert.True(buffs.TryReadSourceBuff(enemy,30070090,other,out _));
        Assert.Equal(1,b.Count(MobaExecutionKind.DamageApply,30070000));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void E_flight_queue_or_deployed_recast_explodes_once_without_repaying(bool inFlight)
    {
        using var b=new Battle();
        var enemy=b.Spawn("a",1001,2,100,110);
        b.Set(b.Caster,"MAGIC_ATTACK",100);b.Set(enemy,"MOVE_SPEED",100);b.Setup.Tick(2);
        var hp=b.Setup.GetActorHp(enemy);
        b.Cast(3,new Vec3(100,0,110));
        b.Until(()=>b.Setup.CountActorBuffs(b.Caster,30070491)==1);
        b.Setup.Tick(1); // Launcher commits its projectile on the next simulation boundary.
        if(!inFlight) {b.Until(()=>b.Areas(42070401).Count==1);b.Setup.TickMilliseconds(500);}
        var mana=b.Setup.GetActorMana(b.Caster);var cooldown=b.CooldownEnd(3);
        var input=new SkillInputEvent(3,SkillInputPhase.Press);
        Assert.True(b.Services.Resolve<SkillCastCoordinator>().TryHandleInputResult(b.Caster,in input).Success);
        b.Until(()=>b.Count(MobaExecutionKind.DamageApply,30070401)==1);
        b.Setup.TickMilliseconds(1100);
        Assert.InRange(hp-b.Setup.GetActorHp(enemy),144.98f,145.02f);
        Assert.Equal(mana,b.Setup.GetActorMana(b.Caster));
        Assert.Equal(cooldown,b.CooldownEnd(3));
        Assert.Equal(1,b.Count(MobaExecutionKind.DamageApply,30070401));
        Assert.Equal(0,b.Param(4)); Assert.Empty(b.Areas(42070401));
        Assert.Equal(100,new MobaAttrs(b.Actor(enemy)).MoveSpeed,3);
        b.Setup.TickMilliseconds(6000);
        Assert.Equal(1,b.Count(MobaExecutionKind.DamageApply,30070401));
    }

    [Fact]
    public void Passive_samples_hero_level_at_claim_and_fully_absorbed_hits_still_consume()
    {
        using var b=new Battle();var target=b.Spawn("a",1001,2,100,103);
        var buffs=b.Services.Resolve<MobaBuffService>();
        Assert.True(buffs.ApplyBuffImmediate(target,30070090,b.Caster,6000));
        b.Actor(b.Caster).ReplaceChampionLevel(9);
        b.Set(b.Caster,"MAGIC_ATTACK",100);b.Setup.Tick(2);
        var hp=b.Setup.GetActorHp(target);
        b.Services.Resolve<MobaShieldService>().AddShield(target,new AbilityKit.Demo.Moba.Components.ShieldLayer
        {ShieldId=7001,SourceActorId=target,TargetActorId=target,CurrentValue=1000,MaxValue=1000,InitialValue=1000,AbsorbRatio=1});
        b.Cast(5,new Vec3(100,0,103),target);b.Setup.TickMilliseconds(1600);
        Assert.Equal(hp,b.Setup.GetActorHp(target));
        Assert.False(buffs.TryReadSourceBuff(target,30070090,b.Caster,out _));
        Assert.Equal(1,b.Count(MobaExecutionKind.DamageApply,30070000));
        Assert.InRange(1000-b.Shield(target),193.98f,194.02f); // 54 + (30 + 8*10 + .3*100).
    }

    [Fact]
    public void W_contacts_dense_allies_without_budget_truncation_and_death_grants_no_return_shield()
    {
        using var b=new Battle();
        var allies=Enumerable.Range(0,24).Select(i=>b.Spawn("ally"+i,1001,1,100,103+i*.1f)).ToArray();
        b.Cast(2,new Vec3(100,0,111));b.Setup.TickMilliseconds(700);
        foreach(var ally in allies) Assert.Equal(40,b.Shield(ally),2);
        b.Set(b.Caster,"hp",0);b.Setup.TickMilliseconds(900);
        foreach(var ally in allies) Assert.Equal(40,b.Shield(ally),2);
    }

    [Fact]
    public void E_timeout_starts_on_landing_and_slow_lingers_one_second()
    {
        using var b=new Battle();
        var enemy=b.Spawn("a",1001,2,100,110);
        b.Set(enemy,"MOVE_SPEED",100);b.Setup.Tick(2);
        b.Cast(3,new Vec3(100,0,110));b.Until(()=>b.Areas(42070401).Count==1);
        b.Setup.TickMilliseconds(300);
        Assert.InRange(new MobaAttrs(b.Actor(enemy)).MoveSpeed,74.98f,75.02f);
        b.Setup.TickMilliseconds(4400);Assert.Single(b.Areas(42070401));
        b.Setup.TickMilliseconds(500);Assert.Empty(b.Areas(42070401));
        Assert.Equal(1,b.Count(MobaExecutionKind.DamageApply,30070401));
        Assert.InRange(new MobaAttrs(b.Actor(enemy)).MoveSpeed,74.98f,75.02f);
        b.Setup.TickMilliseconds(1100);Assert.Equal(100,new MobaAttrs(b.Actor(enemy)).MoveSpeed,3);
    }

    [Fact]
    public void W_shields_self_and_friendly_heroes_on_both_legs_then_expires()
    {
        using var b=new Battle();
        var ally=b.Spawn("ally",1001,1,100,105);
        var enemy=b.Spawn("enemy",1001,2,100,106);
        b.Set(b.Caster,"MAGIC_ATTACK",100);b.Setup.Tick(2);
        b.Cast(2,new Vec3(100,0,101)); // A nearby aim point must not shorten the route.
        b.Setup.TickMilliseconds(600);
        Assert.Equal(80,b.Shield(b.Caster),2); Assert.Equal(80,b.Shield(ally),2);
        Assert.Equal(0,b.Shield(enemy));
        b.Setup.TickMilliseconds(800);
        Assert.Equal(160,b.Shield(b.Caster),2);Assert.Equal(160,b.Shield(ally),2);
        b.Setup.TickMilliseconds(2600);Assert.Equal(0,b.Shield(b.Caster));Assert.Equal(0,b.Shield(ally));
    }

    [Fact]
    public void R_hits_full_forward_rectangle_and_consumes_then_refreshes_own_mark()
    {
        using var b=new Battle();
        var near=b.Spawn("near",1001,2,100,105);
        var far=b.Spawn("far",1001,2,100,130);
        var behind=b.Spawn("behind",1001,2,100,97);
        var side=b.Spawn("side",1001,2,103,110);
        b.Set(b.Caster,"MAGIC_ATTACK",100);b.Setup.Tick(2);
        Assert.True(b.Services.Resolve<MobaBuffService>().ApplyBuffImmediate(near,30070090,b.Caster,6000));
        var hp=b.Setup.GetActorHp(near);var farHp=b.Setup.GetActorHp(far);
        b.Cast(4,new Vec3(100,0,130)); b.Setup.TickMilliseconds(800);
        Assert.Equal(hp,b.Setup.GetActorHp(near));
        b.Setup.TickMilliseconds(500);
        Assert.InRange(hp-b.Setup.GetActorHp(near),479.98f,480.02f);
        Assert.InRange(farHp-b.Setup.GetActorHp(far),419.98f,420.02f);
        Assert.Equal(b.Setup.GetActorMaxHp(behind),b.Setup.GetActorHp(behind));
        Assert.Equal(b.Setup.GetActorMaxHp(side),b.Setup.GetActorHp(side));
        Assert.Equal(1,b.Setup.CountActorBuffs(near,30070090));
        Assert.Equal(1,b.Setup.CountActorBuffs(far,30070090));
        b.Setup.TickMilliseconds(2000);Assert.Equal(2,b.Count(MobaExecutionKind.DamageApply,30070501));
    }

    [Fact]
    public void Death_cleans_deployed_orb_without_explosion()
    {
        using var b=new Battle();b.Spawn("a",1001,2,100,110);
        b.Cast(3,new Vec3(100,0,110));b.Until(()=>b.Areas(42070401).Count==1);
        b.Set(b.Caster,"hp",0);b.Setup.TickMilliseconds(6000);
        Assert.Empty(b.Areas(42070401));Assert.Equal(0,b.Count(MobaExecutionKind.DamageApply,30070401));
    }

    [Fact]
    public void R_preserves_frozen_frame_through_stun_and_parallel_E_recast()
    {
        using var b=new Battle();
        var target=b.Spawn("a",1001,2,100,110);
        b.Cast(3,new Vec3(100,0,110));b.Setup.TickMilliseconds(1500);
        Assert.Single(b.Areas(42070401));
        b.Cast(4,new Vec3(100,0,130));b.Setup.Tick(2);
        Assert.True(b.Services.Resolve<SkillCastCoordinator>().TryGetLatestRunning(b.Caster,out var before));
        Assert.Equal(30070501,before.SkillId);
        b.Cast(3,Vec3.Zero);b.Setup.Tick(2);
        Assert.True(b.Services.Resolve<SkillCastCoordinator>().TryGetLatestRunning(b.Caster,out var after));
        Assert.Equal(before.InstanceId,after.InstanceId);Assert.Equal(30070501,after.SkillId);
        var transform=b.Actor(b.Caster).transform.Value;
        b.Actor(b.Caster).ReplaceTransform(new Transform3(new Vec3(110,0,100),transform.Rotation,transform.Scale));
        Assert.True(b.Services.Resolve<MobaBuffService>().ApplyBuffImmediate(b.Caster,30050490,target,2000));
        b.Setup.TickMilliseconds(1100);
        Assert.Equal(1,b.Count(MobaExecutionKind.DamageApply,30070401));
        Assert.True(b.Count(MobaExecutionKind.DamageApply,30070501)==1,b.Dump());
        Assert.Equal(30070501,b.Actor(b.Caster).characterHfsm.Runtime.Action.SkillId);
        Assert.Empty(b.Areas(42070401));
        Assert.False(b.Services.Resolve<SkillCastCoordinator>().TryCastBySlot(b.Caster,1,new Vec3(100,0,110),Vec3.Forward,0).Success);
    }

    [Fact]
    public void R_death_before_submission_revokes_the_frozen_frame_and_never_damages()
    {
        using var b=new Battle();b.Spawn("a",1001,2,100,110);
        b.Cast(4,new Vec3(100,0,130));b.Setup.TickMilliseconds(500);
        b.Set(b.Caster,"hp",0);b.Setup.TickMilliseconds(3000);
        Assert.Empty(b.Areas(42070501));Assert.Equal(0,b.Count(MobaExecutionKind.DamageApply,30070501));
    }

    [Fact]
    public void Refreshed_source_Buff_rejects_stale_claim_and_claim_is_single_use()
    {
        using var b=new Battle();var target=b.Spawn("a",1001,2,100,103);
        var buffs=b.Services.Resolve<MobaBuffService>();
        Assert.True(buffs.ApplyBuffImmediate(target,30070090,b.Caster,6000));
        Assert.True(buffs.TryReadSourceBuff(target,30070090,b.Caster,out var old));
        Assert.True(buffs.ApplyBuffImmediate(target,30070090,b.Caster,6000));
        Assert.False(buffs.TryClaimSourceBuff(in old));
        Assert.True(buffs.TryReadSourceBuff(target,30070090,b.Caster,out var current));
        Assert.True(buffs.TryClaimSourceBuff(in current));Assert.False(buffs.TryClaimSourceBuff(in current));
        Assert.Equal(0,b.Setup.CountActorBuffs(target,30070090));
    }

    [Fact]
    public void Source_mark_identity_and_claim_eligibility_restore_with_the_whole_world()
    {
        using var b=new Battle();var target=b.Spawn("a",1001,2,100,103);
        var other=b.Spawn("other",3007,1,110,100);
        var buffs=b.Services.Resolve<MobaBuffService>();
        Assert.True(buffs.ApplyBuffImmediate(target,30070090,b.Caster,6000));
        Assert.True(buffs.ApplyBuffImmediate(target,30070090,other,6000));
        Assert.True(buffs.TryReadSourceBuff(target,30070090,b.Caster,out var original));
        var coordinator=b.CreateRollback();var frame=b.Services.Resolve<IFrameTime>().Frame;
        var checkpoint=coordinator.Capture(frame);
        Assert.True(buffs.ApplyBuffImmediate(target,30070090,b.Caster,6000));
        Assert.False(buffs.TryClaimSourceBuff(in original));
        Assert.True(buffs.TryReadSourceBuff(target,30070090,b.Caster,out var changed));
        Assert.True(buffs.TryClaimSourceBuff(in changed));
        coordinator.Restore(checkpoint);
        Assert.True(buffs.TryReadSourceBuff(target,30070090,b.Caster,out var restored));
        Assert.Equal(original.RuntimeContextId,restored.RuntimeContextId);
        Assert.Equal(original.RuntimeContextVersion,restored.RuntimeContextVersion);
        Assert.True(buffs.TryClaimSourceBuff(in restored));
        Assert.False(buffs.TryClaimSourceBuff(in restored));
        Assert.True(buffs.TryReadSourceBuff(target,30070090,other,out _));
    }

    [Fact]
    public void W_return_adds_to_remaining_absorbed_shield_and_refreshes_its_expiry()
    {
        using var b=new Battle();var enemy=b.Spawn("a",1001,2,110,100);
        b.Cast(2,new Vec3(100,0,111));b.Setup.TickMilliseconds(400);
        Assert.Equal(40,b.Shield(b.Caster),2);
        var hit=new AttackInfo{AttackerActorId=enemy,TargetActorId=b.Caster,DamageType=DamageType.True,ReasonKind=DamageReasonKind.Skill};
        hit.BaseDamage.FixedBaseValue=Fixed64.FromInt32(30);
        b.Services.Resolve<MobaCombatEffectService>().DealDamage(hit);
        Assert.Equal(10,b.Shield(b.Caster),2);
        b.Setup.TickMilliseconds(1000);Assert.Equal(50,b.Shield(b.Caster),2);
        b.Setup.TickMilliseconds(2000);Assert.Equal(50,b.Shield(b.Caster),2);
        b.Setup.TickMilliseconds(600);Assert.Equal(0,b.Shield(b.Caster));
    }

    private sealed class Battle : IDisposable
    {
        private readonly ConsoleBattleBootstrapper bootstrap;
        public LiveSimSetupActionExecutor Setup {get;}
        public int Caster {get;}
        public AbilityKit.Ability.World.DI.IWorldResolver Services=>bootstrap.RuntimeServices!;
        public Battle()
        {
            bootstrap=new ConsoleBattleBootstrapper(BattleStartConfig.CreateDefault(),additionalModules:new[]{new MobaTraceAdapterModule()});
            bootstrap.Initialize();bootstrap.Start();
            for(var i=0;i<8&&bootstrap.Context.EcsWorld==null;i++)bootstrap.Tick();
            bootstrap.SetupBattle();for(var i=0;i<10;i++)bootstrap.Tick();
            Setup=new LiveSimSetupActionExecutor(bootstrap);Caster=Spawn("caster",3007,1,100,100);Setup.Tick(2);
        }
        public int Spawn(string alias,int hero,int team,float x,float z)
        {
            Setup.Execute(new MobaAcceptanceSetupActionExpectation{action="spawn_actor",alias=alias,heroId=hero,attributeTemplateId=hero,
                playerId="lux_"+alias,teamId=team,position=new MobaAcceptanceVector3Expectation{x=x,z=z}});
            Assert.True(Setup.TryGetActorId(alias,out var id));Setup.DisableActorBrain(id);
            Set(id,"MAGIC_DEFENSE",0);Set(id,"PHYSICS_DEFENSE",0);Set(id,"PER_SECOND_BLOOD_R",0);Set(id,"PER_SECOND_MANA_R",0);
            return id;
        }
        public void Set(int id,string key,float value)=>Setup.SetScenarioActorAttribute(id,key,value);
        public void Cast(int slot,Vec3 point,int target=0)
        {
            var result=Services.Resolve<SkillCastCoordinator>().TryCastBySlot(Caster,slot,point,Vec3.Forward,target);
            Assert.True(result.Success,result.FailReason+"\n"+Dump());
        }
        public List<MobaAreaRuntimeInfo> Areas(int template)
        {
            var list=new List<MobaAreaRuntimeInfo>();Services.Resolve<MobaAreaRuntimeService>().TryGetAreas(list,Caster,template);return list;
        }
        public ActorEntity Actor(int id) {Assert.True(Services.Resolve<MobaActorLookupService>().TryGetActorEntity(id,out var a));return a;}
        public float Param(int id)=>Actor(Caster).characterHfsm.Runtime.ResolveParameter(id);
        public long CooldownEnd(int slot)=>Actor(Caster).skillLoadout.ActiveSkills[slot-1].CooldownEndTimeMs;
        public float Shield(int actor)=>Services.Resolve<MobaShieldService>().GetTotalRemaining(actor);
        public RollbackCoordinator CreateRollback()
        {
            var world=(IWorld)typeof(ConsoleBattleBootstrapper).GetField("_runtimeWorld",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(bootstrap)!;
            return new RollbackCoordinator(MobaRollbackRegistryBuilder.Create(world),new RollbackSnapshotRingBuffer(4));
        }
        public int Count(MobaExecutionKind kind,int config)=>Services.Resolve<MobaTraceRegistry>().GetNodesByKind((int)kind).Count(n=>n.Metadata?.ConfigId==config);
        public void Until(Func<bool> condition) {for(var i=0;i<240&&!condition();i++)Setup.Tick(1);Assert.True(condition(),Dump());}
        public string Dump()=>string.Join("\n",Services.Resolve<IMobaBattleDiagnosticsService>().GetWarningsSnapshot().Select(w=>w.Message))+"\n"+
            string.Join("\n",Enum.GetValues<MobaExecutionKind>().SelectMany(k=>Services.Resolve<MobaTraceRegistry>().GetNodesByKind((int)k))
                .Where(n=>n.Metadata?.SourceActorId==Caster).Select(n=>n.Metadata!.ToDisplayString()));
        public void Dispose()=>bootstrap.Dispose();
    }
}
