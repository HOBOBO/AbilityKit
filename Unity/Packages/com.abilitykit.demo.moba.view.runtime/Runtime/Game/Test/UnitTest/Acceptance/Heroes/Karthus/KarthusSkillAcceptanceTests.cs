using System.Collections;
using System.Collections.Generic;
using AbilityKit.Ability.Host;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.StateMachine;
using AbilityKit.Demo.Moba.Share;
using AbilityKit.Demo.Moba.View.Config;
using AbilityKit.Game.Battle.Component;
using AbilityKit.Game.Battle.Entity;
using AbilityKit.Game.Battle.Vfx;
using AbilityKit.Game.Flow;
using AbilityKit.Game.Flow.Battle.ViewEvents;
using AbilityKit.Protocol.Moba;
using AbilityKit.Protocol.Moba.StateSync;
using AbilityKit.World.ECS;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using PresentationCueStage = AbilityKit.Demo.Moba.Share.PresentationCueStage;

namespace AbilityKit.Game.Test.UnitTest
{
    public sealed class KarthusSkillAcceptanceTests
    {
        [UnityTest]
        public IEnumerator PlayMode_real_HUD_releases_QWER_and_death_Q_with_owned_warning()
            => KarthusHudAcceptanceTests.Run();

        [UnityTearDown]
        public IEnumerator CleanupHudReview() => KarthusHudAcceptanceTests.Cleanup();

        [TestCase("Karthus_Model_Idle")]
        [TestCase("Walk")]
        public void Actual_model_pose_keeps_the_full_body_above_ground(string state)
        {
            var prefab = Resources.Load<GameObject>("ChampionVfx/11.Karthus_Project/Karthus_Project_Model");
            var instance = Object.Instantiate(prefab);
            try
            {
                var animator = instance.GetComponent<Animator>();
                animator.Rebind(); animator.Play(state, 0, .5f); animator.Update(0);
                var skin = instance.GetComponentInChildren<SkinnedMeshRenderer>();
                skin.updateWhenOffscreen = true;
                Assert.Greater(skin.bounds.min.y, -.25f, "The robe must not be buried in the floor.");
                Assert.Greater(skin.bounds.max.y, 2.5f, "The rendered body must keep its authored height.");
            }
            finally { Object.DestroyImmediate(instance); }
        }

        [Test]
        public void Published_vfx_instantiate_and_derived_particles_have_their_authored_lifetimes()
        {
            var db=BattleViewFactory.GetOrLoadVfxDb();
            for(var id=90036001;id<=90036016;id++)
            {
                Assert.IsTrue(db.TryGet(id,out var row));
                var key=string.IsNullOrEmpty(row.ResourceKey)?row.Resource:row.ResourceKey;
                var prefab=Resources.Load<GameObject>(key);Assert.IsNotNull(prefab,key);
                var instance=Object.Instantiate(prefab);
                try { Assert.IsNotEmpty(instance.GetComponentsInChildren<Renderer>(true),key); }
                finally { Object.DestroyImmediate(instance); }
            }
            var aura=Resources.Load<GameObject>("moba/presentation/karthus/E_Aura");
            var looping=0;
            foreach(var particle in aura.GetComponentsInChildren<ParticleSystem>(true))
                if(particle.emission.enabled && particle.main.loop) looping++;
            Assert.Greater(looping,0,"The mode must sustain particles after the activation clip ends.");
            foreach(var particle in Resources.Load<GameObject>("moba/presentation/karthus/Q_Warning").GetComponentsInChildren<ParticleSystem>(true))
                if(particle.emission.enabled)
                { Assert.AreEqual(0,particle.main.startDelay.constantMax,.001f); Assert.AreEqual(.75f,particle.main.startLifetime.constantMax,.001f); }
        }

        [TestCase("life/dead/death/spirit.ready",0,"Karthus_Model_Idle")]
        [TestCase("life/dead/death/corpse",0,"Karthus_Model_Death")]
        public void Hfsm_substates_drive_the_actual_animator(string path,int skill,string state)
        {
            var shell=new BattleViewResourceProvider().CreateShellGameObject(3006001,3006);
            try
            {
                var component=Component();
                component.ApplyPresentation(new CharacterActionData(actorId:3006001,entityVersion:1,snapshotFrame:0,
                    path:path,instanceId:1,startFrame:0,localFrame:0,castInstanceId:skill>0?1:0,skillId:skill));
                var view=shell.GetComponent<MonoCharacterHfsmView>()??shell.AddComponent<MonoCharacterHfsmView>();
                view.Bind(component,30);
                Assert.AreEqual(Animator.StringToHash(state),shell.GetComponentInChildren<Animator>(true).GetCurrentAnimatorStateInfo(0).shortNameHash);
            }
            finally { Object.DestroyImmediate(shell); }
        }

        [TestCase(30060090,90036003)]
        [TestCase(30060490,90036005)]
        [TestCase(30060590,90036013)]
        public void Buff_owned_cues_instantiate_once_and_release_with_their_source(int buff,int vfx)
        {
            using(var h=MobaSkillConfigTestHarness.CreateForSinglePlayer(new[]{30060201,30060301,30060401,30060501},worldId:"karthus_buff_"+buff,heroId:3006,attributeTemplateId:3006))
            {
                h.EnterGameAndWarmup();var actor=h.AssertPlayerActorBound();
                var world=new EntityWorld();var root=world.Create("karthus-buff-view");
                var query=new BattleEntityQuery(world,new BattleEntityLookup());var manager=new BattleVfxManager(BattleViewFactory.GetOrLoadVfxDb());
                var handler=new BattlePresentationCueViewEventHandler(world,query,manager,root);
                var buffs=h.World.Services.Resolve<AbilityKit.Demo.Moba.Services.Buffs.MobaBuffService>();
                var started=false;var ended=false;
                try
                {
                    Assert.IsTrue(buffs.ApplyBuffImmediate(actor,buff,actor,100));
                    for(var tick=0;tick<12&&!ended;tick++)
                    {
                        h.Tick(1);var snapshots=new List<WorldStateSnapshot>();
                        h.World.Services.Resolve<IWorldStateSnapshotBatchProvider>().CollectSnapshots(h.FrameTime.Frame,snapshots,32);
                        foreach(var snapshot in snapshots)
                        {
                            if(snapshot.OpCode!=MobaOpCodes.Snapshot.PresentationCue)continue;
                            foreach(var entry in MobaPresentationCueSnapshotCodec.Deserialize(snapshot.Payload))
                            {
                                if(entry.TemplateId!=vfx)continue;var cue=PresentationCueSnapshotMapper.Map(in entry);
                                handler.HandleSnapshot(new[]{cue});
                                if(cue.Stage==PresentationCueStage.Started)
                                {
                                    started=true;Assert.AreEqual(1,root.ChildCount);
                                    Assert.IsTrue(root.GetChild(0).TryGetRef(out BattleVfxComponent effect));Assert.AreEqual(vfx,effect.VfxId);
                                    handler.HandleSnapshot(new[]{cue});Assert.AreEqual(1,root.ChildCount);
                                }
                                else if(BattlePresentationCueResolver.ShouldStop(cue.Stage))ended=true;
                            }
                        }
                    }
                    Assert.IsTrue(started);Assert.IsTrue(ended);Assert.AreEqual(0,root.ChildCount);
                }
                finally { handler.Clear();manager.Clear(root);if(root.IsValid)root.Destroy(); }
            }
        }

        [UnityTest] public IEnumerator Q_actual_timeline_plays_complete_clip_and_restores_idle()=>CastAnimation(1,30060201,"SpellQ",2.5f);
        [UnityTest] public IEnumerator W_actual_timeline_plays_complete_clip_and_restores_idle()=>CastAnimation(2,30060301,"SpellW",2.0833333f/.5f);
        [UnityTest] public IEnumerator E_actual_timeline_plays_complete_clip_and_restores_idle()=>CastAnimation(3,30060401,"SpellE",3.3333333f/.5f);
        [UnityTest] public IEnumerator R_actual_timeline_plays_complete_clip_and_restores_idle()=>CastAnimation(4,30060501,"SpellR",4.791667f/3.25f);
        [UnityTest] public IEnumerator AA_actual_timeline_plays_complete_clip_and_restores_idle()=>CastAnimation(0,30060101,"AutoAttack",3f/1.6f);
        [UnityTest] public IEnumerator Dead_Q_uses_actual_timeline()=>CastAnimation(1,30060602,"SpellQ",2.5f,true);
        [UnityTest] public IEnumerator Dead_W_uses_actual_timeline()=>CastAnimation(2,30060603,"SpellW",2.0833333f/.5f,true);
        [UnityTest] public IEnumerator Dead_E_uses_actual_timeline()=>CastAnimation(3,30060604,"SpellE",3.3333333f/.5f,true);
        [UnityTest] public IEnumerator Dead_R_uses_actual_timeline()=>CastAnimation(4,30060605,"SpellR",4.791667f/3.25f,true);

        private static IEnumerator CastAnimation(int slot,int skill,string key,float speed,bool dead=false)
        {
            using(var h=MobaSkillConfigTestHarness.CreateForSinglePlayer(new[]{30060201,30060301,30060401,30060501},worldId:"karthus_anim_"+skill,heroId:3006,attributeTemplateId:3006))
            {
                h.EnterGameAndWarmup();var actor=h.AssertPlayerActorBound();var target=0;
                if(dead||slot==0)
                    target=h.SpawnScenarioActor("enemy",0,"Hero",2,1001,1001,1,1,1,"karthus_enemy",0,null,0,
                        new MobaAcceptanceVector3Expectation{x=4});
                if(dead)
                {
                    var lethal=new AbilityKit.Demo.Moba.AttackInfo{AttackerActorId=target,TargetActorId=actor,
                        DamageType=AbilityKit.Demo.Moba.DamageType.True,ReasonKind=AbilityKit.Demo.Moba.DamageReasonKind.Skill};
                    lethal.BaseDamage.FixedBaseValue=AbilityKit.Deterministic.Fixed64.FromInt32(100000);
                    h.World.Services.Resolve<MobaCombatEffectService>().DealDamage(lethal);h.Tick(2);
                }
                var casts=h.World.Services.Resolve<SkillCastCoordinator>();
                var result=slot==0?casts.TryCastBySlot(actor,5,new Vec3(4,0,0),Vec3.Right,target):
                    casts.TryCastBySlot(actor,slot,new Vec3(6,0,0),Vec3.Right,0);
                Assert.IsTrue(result.Success,result.FailReason);h.Tick(1);var action=Capture(h,actor);
                Assert.AreEqual(skill,action.SkillId);
                var shell=new BattleViewResourceProvider().CreateShellGameObject(actor,3006);
                try
                {
                    var component=Component();component.ApplyPresentation(action);
                    var view=shell.GetComponent<MonoCharacterHfsmView>()??shell.AddComponent<MonoCharacterHfsmView>();view.Bind(component,h.TickRate);
                    var animator=shell.GetComponentInChildren<Animator>(true);var hash=Animator.StringToHash("Karthus_Model_"+key);
                    for(var f=0;f<60 && animator.GetCurrentAnimatorStateInfo(0).shortNameHash!=hash;f++) yield return null;
                    animator.Update(0);Assert.AreEqual(hash,animator.GetCurrentAnimatorStateInfo(0).shortNameHash);Assert.AreEqual(speed,animator.speed,.002f);
                    // Seek near the end of the complete source clip using its authored speed.
                    var span=slot==0?1.6f:slot==1?1f:slot==4?3.25f:.5f;
                    var nearEnd=Mathf.Max(1,Mathf.FloorToInt(span/h.FixedDelta)-2);
                    view.SeekToFrame(action.StartFrame-1,h.FixedDelta);
                    view.SeekToFrame(action.StartFrame+nearEnd,h.FixedDelta);
                    yield return null;animator.Update(0);
                    Assert.AreEqual(nearEnd*h.FixedDelta/span,animator.GetCurrentAnimatorStateInfo(0).normalizedTime,.03f);
                    h.TickMilliseconds((int)(span*1000)+200);var after=Capture(h,actor);
                    Assert.AreEqual(0,after.CastInstanceId);component.ApplyPresentation(after);view.SeekToFrame(after.SnapshotFrame,h.FixedDelta);animator.Update(0);
                    Assert.AreEqual(Animator.StringToHash("Karthus_Model_Idle"),animator.GetCurrentAnimatorStateInfo(0).shortNameHash);Assert.AreEqual(1,animator.speed,.001f);
                }
                finally { Object.DestroyImmediate(shell); }
            }
        }

        [TestCase(1,30060201,42060201,90036006)]
        [TestCase(1,30060201,42060202,90036008)]
        [TestCase(2,30060301,42060301,90036015)]
        public void Real_area_cues_create_and_release_the_owned_vfx(int slot,int skill,int area,int vfx)
        {
            using(var h=MobaSkillConfigTestHarness.CreateForSinglePlayer(new[]{30060201,30060301,30060401,30060501},worldId:"karthus_area_"+area,heroId:3006,attributeTemplateId:3006))
            {
                h.EnterGameAndWarmup();var actor=h.AssertPlayerActorBound();
                var world=new EntityWorld();var root=world.Create("karthus-area-view");
                var query=new BattleEntityQuery(world,new BattleEntityLookup());var manager=new BattleVfxManager(BattleViewFactory.GetOrLoadVfxDb());
                var handler=new BattlePresentationCueViewEventHandler(world,query,manager,root);var started=false;var ended=false;
                try
                {
                    Assert.IsTrue(h.World.Services.Resolve<SkillCastCoordinator>().TryCastBySlot(actor,slot,new Vec3(6,0,0),Vec3.Right,0).Success);
                    for(var t=0;t<210&&!ended;t++)
                    {
                        h.Tick(1);var snapshots=new List<WorldStateSnapshot>();
                        h.World.Services.Resolve<IWorldStateSnapshotBatchProvider>().CollectSnapshots(h.FrameTime.Frame,snapshots,32);
                        foreach(var snapshot in snapshots)
                        {
                            if(snapshot.OpCode!=MobaOpCodes.Snapshot.PresentationCue)continue;
                            foreach(var entry in MobaPresentationCueSnapshotCodec.Deserialize(snapshot.Payload))
                            {
                                if(entry.TemplateId!=area)continue;var cue=PresentationCueSnapshotMapper.Map(in entry);handler.HandleSnapshot(new[]{cue});
                                if(cue.Stage==PresentationCueStage.Started)
                                {
                                    started=true;Assert.AreEqual(1,root.ChildCount);var entity=root.GetChild(0);
                                    Assert.IsTrue(entity.TryGetRef(out BattleVfxComponent effect));Assert.AreEqual(vfx,effect.VfxId);
                                    Assert.IsTrue(entity.TryGetRef(out BattleViewGameObjectComponent visual));Assert.IsNotNull(visual.GameObject);
                                    Assert.Less(Vector3.Distance(new Vector3(6,0,0),visual.GameObject.transform.position),.05f);
                                    handler.HandleSnapshot(new[]{cue});Assert.AreEqual(1,root.ChildCount);
                                }
                                else if(BattlePresentationCueResolver.ShouldStop(cue.Stage))ended=true;
                            }
                        }
                    }
                    Assert.IsTrue(started);Assert.IsTrue(ended);Assert.AreEqual(0,root.ChildCount);
                }
                finally { handler.Clear();manager.Clear(root);if(root.IsValid)root.Destroy(); }
            }
        }

        private static BattleCharacterHfsmComponent Component()
        { var loader=new ResourcesTextAssetLoader();return new BattleCharacterHfsmComponent(MobaCharacterHfsmProfile.ResolveDefinition(loader,3006),CharacterPresentationActionCatalog.Load(loader),3006); }
        private static CharacterActionData Capture(MobaSkillConfigTestHarness h,int actor)
        {
            var snapshots=new List<WorldStateSnapshot>();h.World.Services.Resolve<IWorldStateSnapshotBatchProvider>().CollectSnapshots(h.FrameTime.Frame,snapshots,32);
            foreach(var snapshot in snapshots)if(CharacterActionSnapshotRoute.TryDecode(snapshot,out var entries))foreach(var entry in entries)if(entry.ActorId==actor)return entry;
            Assert.Fail("Missing real CharacterAction snapshot.");return default;
        }
    }
}

