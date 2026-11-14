using AbilityKit.Core.Mathematics;
using AbilityKit.Core.Eventing;
using AbilityKit.Demo.Moba.Acceptance.LiveSim;
using AbilityKit.Demo.Moba.Attributes;
using AbilityKit.Demo.Moba.Components;
using AbilityKit.Demo.Moba.Console;
using AbilityKit.Demo.Moba.Console.Battle.Config;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Share;
using AbilityKit.Deterministic;
using AbilityKit.Game.Test.UnitTest;
using AbilityKit.Trace;
using Xunit;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.FrameSync.Rollback;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Demo.Moba.Rollback;

namespace AbilityKit.Demo.Moba.Tests.Smoke;

public sealed class KarthusRuntimeTests
{
    [Fact]
    public void Basic_attack_launches_and_applies_one_physical_hit()
    {
        using var b=new Battle();var enemy=b.Spawn("enemy",1001,2,100,104);
        b.Set(b.Caster,"PHYSICS_ATTACK",100);b.Setup.Tick(2);var hp=b.Setup.GetActorHp(enemy);
        var result=b.Services.Resolve<SkillCastCoordinator>().TryCastBySlot(b.Caster,5,new Vec3(100,0,104),Vec3.Forward,enemy);
        Assert.True(result.Success,result.FailReason);b.Setup.TickMilliseconds(2000);
        Assert.Equal(1,b.Count(30060101));Assert.Equal(100,hp-b.Setup.GetActorHp(enemy),3);
    }
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Q_counts_one_frozen_explosion_batch_and_doubles_only_an_isolated_hit(int targets)
    {
        using var b = new Battle();
        var enemies = Enumerable.Range(0, targets).Select(i => b.Spawn("target"+i, 1001, 2, 100+i*.3f, 106)).ToArray();
        b.Set(b.Caster,"MAGIC_ATTACK",100); b.Setup.Tick(2);
        var hp=enemies.Select(b.Setup.GetActorHp).ToArray();
        b.Cast(1,new Vec3(100,0,106));
        b.Setup.TickMilliseconds(700);
        Assert.Equal(0,b.Count(30060201));
        b.Setup.TickMilliseconds(700);
        Assert.Equal(targets,b.Count(30060201));
        for(var i=0;i<targets;i++)
            Assert.InRange(hp[i]-b.Setup.GetActorHp(enemies[i]), targets==1?159.99f:79.99f,targets==1?160.01f:80.01f);
    }

    [Fact]
    public void E_pays_even_without_targets_and_stops_before_an_unaffordable_tick()
    {
        using var b=new Battle(); b.Set(b.Caster,"mana",15); b.Setup.Tick(2);
        b.Cast(3,Vec3.Zero); b.Setup.TickMilliseconds(900);
        Assert.InRange(b.Mana(),0,0.001f);
        Assert.Equal(0,b.Setup.CountActorBuffs(b.Caster,30060490));
        Assert.Equal(0,b.Count(30060401));
    }

    [Fact]
    public void E_active_close_has_one_free_final_tick_and_removes_the_mode()
    {
        using var b=new Battle(); var enemy=b.Spawn("enemy",1001,2,100,103);
        b.Setup.Tick(2); b.Cast(3,Vec3.Zero); b.Setup.TickMilliseconds(600);
        var before=b.Count(30060401); var mana=b.Mana();
        b.Cast(3,Vec3.Zero); b.Setup.TickMilliseconds(100);
        Assert.True(before+1==b.Count(30060401),$"before={before} after={b.Count(30060401)} buff={b.Setup.CountActorBuffs(b.Caster,30060490)}\n"+b.Report());
        Assert.Equal(mana,b.Mana(),3);
        Assert.Equal(0,b.Setup.CountActorBuffs(b.Caster,30060490));
        b.Setup.TickMilliseconds(1000); Assert.Equal(before+1,b.Count(30060401));
    }

    [Fact]
    public void P_keeps_hp_zero_replaces_slots_and_casts_freely_then_expires()
    {
        using var b=new Battle(); var enemy=b.Spawn("enemy",1001,2,100,106);
        b.Set(b.Caster,"mana",0); b.Kill(enemy,suspendRespawn:true); b.Setup.Tick(2);
        var rules=b.Services.Resolve<MobaCombatRulesService>();
        Assert.False(rules.IsAlive(b.Caster)); Assert.False(rules.CanMove(b.Caster)); Assert.False(rules.CanAttack(b.Caster));
        Assert.Equal(0,b.Setup.GetActorHp(b.Caster));
        Assert.Equal(1,b.Setup.CountActorBuffs(b.Caster,30060090));
        Assert.Equal(1,b.Setup.CountActorBuffs(b.Caster,30060490));
        var loadout=b.Services.Resolve<MobaSkillLoadoutService>();
        Assert.True(loadout.TryResolveSlot(b.Caster,1,out var slot)); Assert.Equal(30060602,slot.EffectiveSkillId);
        b.Cast(1,new Vec3(100,0,106)); b.Setup.TickMilliseconds(1500);
        Assert.Equal(1,b.Count(30060201)); Assert.Equal(0,b.Mana());
        b.Setup.TickMilliseconds(6000);
        Assert.Equal(0,b.Setup.CountActorBuffs(b.Caster,30060090));
        Assert.Equal(0,b.Setup.CountActorBuffs(b.Caster,30060490));
        Assert.EndsWith("corpse",b.Actor(b.Caster).characterHfsm.Runtime.Action.Path);
        Assert.False(b.TryCast(1,new Vec3(100,0,106)).Success);
    }

    [Fact]
    public void P_replacement_does_not_reset_an_existing_slot_cooldown()
    {
        using var b=new Battle(); var enemy=b.Spawn("enemy",1001,2,100,106);
        b.Cast(1,new Vec3(100,0,106)); b.Setup.TickMilliseconds(300);
        var cooldown=b.Actor(b.Caster).skillLoadout.ActiveSkills[0].CooldownEndTimeMs;
        b.Kill(enemy); b.Setup.Tick(2);
        Assert.Equal(cooldown,b.Actor(b.Caster).skillLoadout.ActiveSkills[0].CooldownEndTimeMs);
        Assert.False(b.TryCast(1,new Vec3(100,0,106)).Success);
        b.Setup.TickMilliseconds(1200); b.Cast(1,new Vec3(100,0,106));
    }

    [Fact]
    public void W_applies_real_slow_and_magic_resistance_reduction_then_restores_them()
    {
        using var b=new Battle(); var enemy=b.Spawn("enemy",1001,2,102,106);
        b.Set(enemy,"MOVE_SPEED",100); b.Set(enemy,"MAGIC_DEFENSE",100); b.Setup.Tick(2);
        b.Cast(2,new Vec3(100,0,106)); b.Setup.TickMilliseconds(700);
        Assert.InRange(new MobaAttrs(b.Actor(enemy)).MagicDefense,74.99f,75.01f);
        Assert.InRange(new MobaAttrs(b.Actor(enemy)).MoveSpeed,59.99f,70f);
        b.Setup.TickMilliseconds(6000);
        Assert.Equal(100,new MobaAttrs(b.Actor(enemy)).MoveSpeed,3);
        Assert.Equal(100,new MobaAttrs(b.Actor(enemy)).MagicDefense,3);
    }

    [Fact]
    public void R_hits_all_distant_enemy_heroes_without_a_default_target_cap()
    {
        using var b=new Battle();
        var enemies=Enumerable.Range(0,35).Select(i=>b.Spawn("enemy"+i,1001,2,200+i*2,200)).ToArray();
        b.Setup.Tick(2); var hp=enemies.Select(b.Setup.GetActorHp).ToArray();
        var ally=b.Spawn("ally",1001,1,105,105);
        var minion=b.Spawn("minion",1001,2,105,107);b.Actor(minion).ReplaceUnitSubType(AbilityKit.Demo.Moba.UnitSubType.Minion);
        b.Cast(4,Vec3.Zero);b.Setup.TickMilliseconds(500);
        foreach(var enemy in enemies)Assert.Equal(1,b.Setup.CountActorBuffs(enemy,30060590));
        Assert.Equal(0,b.Setup.CountActorBuffs(ally,30060590));Assert.Equal(0,b.Setup.CountActorBuffs(minion,30060590));
        b.Setup.TickMilliseconds(2500); Assert.Equal(0,b.Count(30060501));
        b.Setup.TickMilliseconds(600); Assert.True(b.Count(30060501)>=35);
        for(var i=0;i<enemies.Length;i++)
        { Assert.InRange(hp[i]-b.Setup.GetActorHp(enemies[i]),199.99f,200.01f);Assert.Equal(0,b.Setup.CountActorBuffs(enemies[i],30060590)); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void R_warning_cancellation_removes_only_its_caster_source_even_after_team_change(bool dead)
    {
        using var b=new Battle();var enemy=b.Spawn("enemy",1001,2,108,110);
        var other=b.Spawn("other",3006,1,110,110);
        if(dead){b.Kill(enemy,suspendRespawn:true);b.Setup.Tick(2);}
        b.Cast(4,Vec3.Zero);b.Setup.TickMilliseconds(500);
        Assert.Equal(1,b.Setup.CountActorBuffs(enemy,30060590));
        Assert.True(b.Services.Resolve<AbilityKit.Demo.Moba.Services.Buffs.MobaBuffService>().ApplyBuffImmediate(enemy,30060590,other,3000));
        Assert.Equal(2,b.Setup.CountActorBuffs(enemy,30060590));
        b.Actor(enemy).ReplaceTeam(AbilityKit.Demo.Moba.Team.Team1);
        Assert.True(b.Services.Resolve<SkillCastCoordinator>().CancelBySlot(b.Caster,4));b.Setup.Tick(1);
        Assert.True(b.Setup.CountActorBuffs(enemy,30060590)==1,b.Report());
        b.Setup.TickMilliseconds(3500);Assert.Equal(0,b.Setup.CountActorBuffs(enemy,30060590));Assert.Equal(0,b.Count(30060501));
    }

    [Fact]
    public void P_rejects_R_when_its_remaining_window_cannot_cover_the_submission_span()
    {
        using var b=new Battle(); var enemy=b.Spawn("enemy",1001,2,100,110);
        b.Kill(enemy); b.Setup.TickMilliseconds(4000);
        Assert.False(b.TryCast(4,Vec3.Zero).Success);
        Assert.Equal(0,b.Count(30060501));
    }

    [Theory]
    [InlineData(1,2,true)]
    [InlineData(2,2,true)]
    [InlineData(3,2,true)]
    [InlineData(4,2,true)]
    [InlineData(5,2,false)]
    [InlineData(6,2,false)]
    [InlineData(7,2,false)]
    [InlineData(1,1,false)]
    public void E_kill_refund_requires_an_enemy_combat_unit(int subtype,int team,bool refunded)
    {
        using var b=new Battle();
        var target=b.Spawn("victim",1001,team,100,109);
        b.Actor(target).ReplaceUnitSubType((AbilityKit.Demo.Moba.UnitSubType)subtype);
        b.Set(b.Caster,"mana",0); b.Setup.Tick(2);
        b.Fatal(b.Caster,target);
        Assert.True(Math.Abs((refunded?10:0)-b.Mana())<.001f,b.Report());
    }

    [Fact]
    public void E_kill_refund_does_not_reward_another_killer()
    {
        using var b=new Battle(); var target=b.Spawn("victim",1001,2,100,109);
        var ally=b.Spawn("ally",1001,1,105,105);
        b.Set(b.Caster,"mana",0);b.Setup.Tick(2);b.Fatal(ally,target);
        Assert.Equal(0,b.Mana());
    }

    [Fact]
    public void E_next_tick_samples_current_ap_and_rank()
    {
        using var b=new Battle();var target=b.Spawn("target",1001,2,100,103);
        b.Set(b.Caster,"MAGIC_ATTACK",0);b.Setup.Tick(2);b.Cast(3,Vec3.Zero);
        b.Setup.TickMilliseconds(550);var hp=b.Setup.GetActorHp(target);var mana=b.Mana();
        b.Actor(b.Caster).skillLoadout.ActiveSkills[2].Level=2;
        b.Set(b.Caster,"MAGIC_ATTACK",100);b.Setup.TickMilliseconds(250);
        Assert.InRange(hp-b.Setup.GetActorHp(target),17.49f,17.51f);
        Assert.InRange(mana-b.Mana(),10.49f,10.51f);
    }

    [Fact]
    public void P_real_respawn_clears_spirit_aura_and_restores_paid_slots()
    {
        using var b=new Battle();var enemy=b.Spawn("enemy",1001,2,100,109);
        b.Kill(enemy);b.Setup.Tick(2);
        Assert.Equal(1,b.Setup.CountActorBuffs(b.Caster,30060090));
        b.Setup.TickMilliseconds(6200);
        Assert.True(b.Services.Resolve<MobaCombatRulesService>().IsAlive(b.Caster));
        Assert.Equal(0,b.Setup.CountActorBuffs(b.Caster,30060090));
        Assert.Equal(0,b.Setup.CountActorBuffs(b.Caster,30060490));
        var loadout=b.Services.Resolve<MobaSkillLoadoutService>();
        Assert.True(loadout.TryResolveSlot(b.Caster,1,out var slot));Assert.Equal(30060201,slot.EffectiveSkillId);
        b.Set(b.Caster,"mana",0);Assert.False(b.TryCast(1,new Vec3(100,0,106)).Success);
        b.Kill(enemy,suspendRespawn:true);b.Setup.Tick(2);
        Assert.Equal(1,b.Setup.CountActorBuffs(b.Caster,30060090));
    }

    [Fact]
    public void P_does_not_enable_an_unlearned_E_even_when_the_dead_slot_is_pressed()
    {
        using var b=new Battle();var enemy=b.Spawn("enemy",1001,2,100,109);
        b.Actor(b.Caster).skillLoadout.ActiveSkills[2].Level=0;
        b.Kill(enemy,suspendRespawn:true);b.Setup.Tick(2);
        Assert.Equal(0,b.Setup.CountActorBuffs(b.Caster,30060490));
        b.Cast(3,Vec3.Zero);b.Setup.TickMilliseconds(600);
        Assert.Equal(0,b.Setup.CountActorBuffs(b.Caster,30060490));
    }

    [Fact]
    public void P_learned_E_runs_free_and_toggle_input_cannot_close_it()
    {
        using var b=new Battle();var enemy=b.Spawn("enemy",1001,2,100,103);
        b.Set(b.Caster,"mana",0);b.Kill(enemy,suspendRespawn:true);b.Setup.TickMilliseconds(600);
        Assert.True(b.Count(30060401)>0);Assert.Equal(0,b.Mana());
        b.Cast(3,Vec3.Zero);b.Setup.TickMilliseconds(600);
        Assert.Equal(0,b.Mana());
        Assert.Equal(1,b.Setup.CountActorBuffs(b.Caster,30060490));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Q_committed_explosion_survives_owner_death_or_spirit_expiry(bool atSpiritExpiry)
    {
        using var b=new Battle();var enemy=b.Spawn("enemy",1001,2,100,106);
        if(atSpiritExpiry){b.Kill(enemy,suspendRespawn:true);b.Setup.TickMilliseconds(6400);}
        b.Cast(1,new Vec3(100,0,106));b.Setup.TickMilliseconds(350);
        if(!atSpiritExpiry)b.Kill(enemy,suspendRespawn:true);
        b.Setup.TickMilliseconds(1100);Assert.Equal(1,b.Count(30060201));
    }

    [Fact]
    public void Ordinary_dead_hero_cannot_cast_and_R_death_cancels_before_damage()
    {
        using var b=new Battle();var enemy=b.Spawn("enemy",1001,2,100,110);
        b.Fatal(b.Caster,enemy);b.Setup.Tick(2);
        Assert.False(b.Services.Resolve<SkillCastCoordinator>().TryCastBySlot(enemy,1,Vec3.Zero,Vec3.Forward,0).Success);
        var survivor=b.Spawn("survivor",1001,2,100,112);
        b.Cast(4,Vec3.Zero);b.Setup.TickMilliseconds(1000);
        Assert.Equal(1,b.Setup.CountActorBuffs(survivor,30060590));b.Kill(survivor,suspendRespawn:true);
        b.Setup.Tick(2);Assert.Equal(0,b.Setup.CountActorBuffs(survivor,30060590));
        b.Setup.TickMilliseconds(3000);Assert.Equal(0,b.Count(30060501));
    }

    [Fact]
    public void P_deadline_equality_rejects_R_and_pending_expiration_wins_execution()
    {
        using var b=new Battle();var enemy=b.Spawn("enemy",1001,2,100,110);
        b.Kill(enemy,suspendRespawn:true);b.Setup.Tick(2);
        var actor=b.Actor(b.Caster);var binding=actor.characterHfsm.Runtime.DeathCasting;
        Assert.True(binding.TryGetPermission(actor,out var permission));var clock=permission.Continuous;
        var elapsed=clock.DurationRaw!.Value-clock.RemainingRaw!.Value;
        clock.RestoreLocalDuration(elapsed+Fixed64.FromDouble(3.25).RawValue);
        Assert.False(binding.CanCast(actor,30060605,true));
        clock.RestoreLocalDuration(clock.DurationRaw!.Value+1);
        Assert.True(binding.CanCast(actor,30060605,true));
        var advance=AbilityKit.Core.Mathematics.DeterministicMathBridge.ToFixed(b.Setup.FixedDelta).RawValue;
        clock.RestoreLocalDuration(elapsed+advance);
        Assert.True(binding.CanCast(actor,30060602,false));
        Assert.False(binding.CanCast(actor,30060602,false,b.Setup.FixedDelta));
    }

    [Fact]
    public void P_and_E_resume_from_the_formal_whole_world_checkpoint()
    {
        using var b=new Battle();var enemy=b.Spawn("enemy",1001,2,100,103);
        b.Set(b.Caster,"mana",0);b.Kill(enemy,suspendRespawn:true);b.Setup.Tick(2);
        var restore=b.Checkpoint();
        b.Setup.TickMilliseconds(600);var expected=b.Setup.GetActorHp(enemy);
        restore();
        Assert.Equal(1,b.Setup.CountActorBuffs(b.Caster,30060090));
        Assert.Equal(1,b.Setup.CountActorBuffs(b.Caster,30060490));
        b.Setup.TickMilliseconds(600);Assert.Equal(expected,b.Setup.GetActorHp(enemy),3);
        Assert.Equal(0,b.Mana());
        b.Setup.TickMilliseconds(7000);Assert.Equal(0,b.Setup.CountActorBuffs(b.Caster,30060090));
    }

    private sealed class Battle : IDisposable
    {
        private readonly ConsoleBattleBootstrapper bootstrap;
        public LiveSimSetupActionExecutor Setup {get;}
        public AbilityKit.Ability.World.DI.IWorldResolver Services => bootstrap.RuntimeServices!;
        public int Caster {get;}
        public Battle()
        {
            bootstrap=new ConsoleBattleBootstrapper(BattleStartConfig.CreateDefault(),additionalModules:new[]{new MobaTraceAdapterModule()});
            bootstrap.Initialize();bootstrap.Start();
            for(var i=0;i<8&&bootstrap.Context.EcsWorld==null;i++)bootstrap.Tick();
            bootstrap.SetupBattle();for(var i=0;i<10;i++)bootstrap.Tick();
            Setup=new LiveSimSetupActionExecutor(bootstrap);Caster=Spawn("caster",3006,1,100,100);Setup.Tick(2);

        }
        public int Spawn(string alias,int hero,int team,float x,float z)
        {
            Setup.Execute(new MobaAcceptanceSetupActionExpectation{action="spawn_actor",alias=alias,heroId=hero,
                attributeTemplateId=hero,playerId="karthus_"+alias,teamId=team,position=new MobaAcceptanceVector3Expectation{x=x,z=z}});
            Assert.True(Setup.TryGetActorId(alias,out var id));Setup.DisableActorBrain(id);
            Set(id,"MAGIC_DEFENSE",0);Set(id,"PHYSICS_DEFENSE",0);Set(id,"PER_SECOND_BLOOD_R",0);Set(id,"PER_SECOND_MANA_R",0);return id;
        }
        public void Set(int actor,string attribute,float value)=>Setup.SetScenarioActorAttribute(actor,attribute,value);
        public ActorEntity Actor(int id){Assert.True(Services.Resolve<MobaActorLookupService>().TryGetActorEntity(id,out var actor));return actor;}
        public float Mana()=>new MobaAttrs(Actor(Caster)).Mana;
        public Action Checkpoint()
        {
            var world=(IWorld)typeof(ConsoleBattleBootstrapper).GetField("_runtimeWorld",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.GetValue(bootstrap)!;
            var coordinator=new RollbackCoordinator(MobaRollbackRegistryBuilder.Create(world),new RollbackSnapshotRingBuffer(2));
            var frame=new FrameIndex(bootstrap.Context.LastFrame);
            Assert.True(coordinator.CaptureAndStore(frame));
            return ()=>Assert.True(bootstrap.TryRestoreSimulationFrame(coordinator,frame,out var result),result.ToString());
        }
        public MobaSkillCastResult TryCast(int slot,Vec3 point)=>Services.Resolve<SkillCastCoordinator>().TryCastBySlot(Caster,slot,point,Vec3.Forward,0);
        public void Cast(int slot,Vec3 point){var r=TryCast(slot,point);Assert.True(r.Success,r.FailReason+"\n"+Dump());}
        public int Count(int skill)=>Services.Resolve<MobaTraceRegistry>().GetNodesByKind((int)MobaExecutionKind.DamageApply).Count(n=>n.Metadata?.ConfigId==skill);
        public void Kill(int enemy,bool suspendRespawn=false)
        {
            var respawn=Services.Resolve<AbilityKit.Demo.Moba.Gameplay.MobaGameplayRespawnService>();
            var before=respawn.ExportState(default);
            Fatal(enemy,Caster);
            // This scenario isolates the seven-second expiry; normal revival has a separate test.
            if(suspendRespawn) respawn.ImportState(default,before);
        }
        public void Fatal(int attacker,int target)
        {
            var lethal=new AttackInfo{AttackerActorId=attacker,TargetActorId=target,DamageType=DamageType.True,ReasonKind=DamageReasonKind.Skill,ReasonParam=1};
            lethal.BaseDamage.FixedBaseValue=Fixed64.FromInt32((int)Setup.GetActorHp(target)+1000);Assert.NotNull(Services.Resolve<MobaCombatEffectService>().DealDamage(lethal));
        }
        private string Dump()=>string.Join("\n",Services.Resolve<IMobaBattleDiagnosticsService>().GetWarningsSnapshot().Select(w=>w.Message));
        public string Report()
        {
            var subs=Services.Resolve<AbilityKit.Demo.Moba.Services.Triggering.MobaTriggerPlanSubscriptionService>();
            var keys=new List<long>();subs.CopyOwnerKeysForTrigger(30060421,keys);
            var text=Dump();
            var stats=Services.Resolve<AbilityKit.Demo.Moba.Runtime.Application.Services.Triggering.MobaTriggerExecutionGateway>().Stats;
            text+=$"\nsubscriptions={keys.Count} apply={stats.OwnerApplied}/{stats.OwnerApplyRequests} stop={stats.OwnerStopped}/{stats.OwnerStopRequests} last={stats.LastOwnerOperation}:{stats.LastOwnerKey}:{stats.LastSource}";
            var records=Services.Resolve<AbilityKit.Triggering.Runtime.Plan.Json.TriggerPlanJsonDatabase>().Records;
            foreach(var record in records.Where(r=>r.TriggerId==30060421||r.TriggerId==30060401))
                text+=$"\nplan={record.TriggerId} event={record.EventName}:{record.EventId} scope={record.Scope} root={record.ExecutionRoot?.GetType().Name}";
            var a=Actor(Caster);
            text+="\npassives="+string.Join(",",a.skillLoadout.PassiveSkills.Select(p=>p.PassiveSkillId));
            text+=" ongoing="+(a.hasOngoingTriggerPlans?string.Join(";",a.ongoingTriggerPlans.Active.Select(p=>$"{p.OwnerKey}:{string.Join(',',p.TriggerIds)}")):"none");
            foreach(var key in keys) if(subs.TryGetLastEvaluation(key,30060421,out var e))
                text+=$"\nowner={key} count={e.Count} gate={e.GatePassed} source={e.SourceResolved} plan={e.PlanPassed}";
            text+="\n"+string.Join("\n",Services.Resolve<MobaTraceRegistry>().GetNodesByKind((int)MobaExecutionKind.EffectExecution)
                .Where(n=>n.Metadata?.ConfigId>=30060000&&n.Metadata?.ConfigId<30061000).Select(n=>n.Metadata.ToDisplayString()));
            return text;
        }
        public void Dispose()=>bootstrap.Dispose();
    }
}

