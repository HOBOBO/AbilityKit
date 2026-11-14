using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AbilityKit.Ability.Host;
using AbilityKit.Combat.Projectile;
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
    public sealed class LuxSkillAcceptanceTests
    {
        [UnityTest] public IEnumerator PlayMode_real_HUD_releases_QWER_and_free_E_recast()=>LuxHudAcceptanceTests.Run();
        [UnityTearDown] public IEnumerator CleanupHudReview()=>LuxHudAcceptanceTests.Cleanup();
        [Test]
        public void Published_vfx_instantiate_and_derived_particles_have_their_authored_lifetimes()
        {
            var db=BattleViewFactory.GetOrLoadVfxDb();
            for(var id=90037001;id<=90037019;id++)
            {
                Assert.IsTrue(db.TryGet(id,out var row));
                var key=string.IsNullOrEmpty(row.ResourceKey)?row.Resource:row.ResourceKey;
                var prefab=Resources.Load<GameObject>(key);Assert.IsNotNull(prefab,key);
                var instance=Object.Instantiate(prefab);
                try { Assert.IsNotEmpty(instance.GetComponentsInChildren<Renderer>(true),key); }
                finally { Object.DestroyImmediate(instance); }
            }
            foreach(var key in new[]{"E_Charge","Q_Cage","Passive_Mark","Q_Projectile","W_Projectile","E_Projectile"})
            {
                var prefab=Resources.Load<GameObject>("moba/presentation/lux/"+key);
                var looping=0;
                foreach(var particle in prefab.GetComponentsInChildren<ParticleSystem>(true))
                    if(particle.emission.enabled && particle.main.loop) looping++;
                Assert.Greater(looping,0,key+" must sustain its independently owned lifetime.");
            }
            var beam=Resources.Load<GameObject>("moba/presentation/lux/R_Channel");
            var mainBeam=beam.transform.Find("Fx_ULTIMATE/PS_Beam").GetComponent<ParticleSystem>();
            Assert.IsTrue(mainBeam.emission.enabled,"Main beam geometry must emit, not only its accent particles.");
            Assert.AreEqual(1f,mainBeam.main.startDelay.constantMax,.001f);
            Assert.Greater(beam.transform.Find("Fx_ULTIMATE/PS_Area").position.y,.02f,
                "The ground telegraph needs its authored height, independently of the beam group's inverted Y axis.");
        }

        [TestCase("Lux_Model_Idle")]
        [TestCase("Walk")]
        [TestCase("Lux_Model_AutoAttack")]
        [TestCase("Lux_Model_SpellQ")]
        [TestCase("Lux_Model_SpellW")]
        [TestCase("Lux_Model_SpellE")]
        [TestCase("Lux_Model_SpellR")]
        public void Animated_body_stays_above_ground_at_normal_model_scale(string state)
        {
            var instance=Object.Instantiate(Resources.Load<GameObject>("ChampionVfx/12.Lux_DarkStar/Lux_DarkStar_Model"));
            var mesh=new Mesh();
            try
            {
                var animator=instance.GetComponent<Animator>();animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
                animator.Rebind();animator.Update(0);
                var skin=instance.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single();
                foreach(var sample in new[]{0f,.25f,.5f,.9f})
                {
                    animator.Play(Animator.StringToHash(state),0,sample);animator.Update(0);
                    Assert.AreEqual(Animator.StringToHash(state),animator.GetCurrentAnimatorStateInfo(0).shortNameHash);
                    skin.BakeMesh(mesh,true);
                    var points=mesh.vertices.Select(v=>skin.transform.TransformPoint(v)).ToArray();
                    var min=points.Min(v=>v.y);var max=points.Max(v=>v.y);
                    Assert.Greater(min,-.15f,state+" sample="+sample+" minY="+min);
                    Assert.Greater(max-min,1.4f,"The rig must retain its real scale.");
                    Assert.Less(max-min,2.8f,"The walk must not overwrite rig scale.");
                }
            }
            finally {Object.DestroyImmediate(mesh);Object.DestroyImmediate(instance);}
        }

        [TestCase("Fx_ULTIMATE/PS_Area",.3f)]
        [TestCase("Fx_ULTIMATE/PS_Area",.9f)]
        [TestCase("Fx_ULTIMATE/PS_Beam",1.15f)]
        [TestCase("Fx_ULTIMATE/PS_Beam",1.45f)]
        [TestCase("Fx_ULTIMATE/PS_BeamLight",1.15f)]
        public void R_rendered_plane_matches_the_frozen_hit_rectangle(string path,float seconds)
        {
            var instance=Object.Instantiate(Resources.Load<GameObject>("moba/presentation/lux/R_Channel"));
            var camera=new GameObject("beam geometry camera").AddComponent<Camera>();
            var mesh=new Mesh();
            try
            {
                var animator=instance.GetComponent<Animator>();animator.Rebind();animator.Update(0);
                animator.Play(0,0,seconds);animator.Update(0);
                var particle=instance.transform.Find(path).GetComponent<ParticleSystem>();
                particle.useAutoRandomSeed=false;particle.randomSeed=42;particle.Simulate(seconds,false,true,true);
                Assert.Greater(particle.particleCount,0,path);
                particle.GetComponent<ParticleSystemRenderer>().BakeMesh(mesh,camera,true);mesh.RecalculateBounds();
                Assert.AreEqual(33.4f,mesh.bounds.size.z,.03f,"Measure rendered vertices, not the conservative renderer bounds.");
                Assert.LessOrEqual(mesh.bounds.size.x,2.01f);Assert.Greater(mesh.bounds.size.x,.2f);
                Assert.Greater(mesh.bounds.min.y+particle.transform.position.y,.02f);
                Assert.AreEqual(0,mesh.bounds.min.z+particle.transform.position.z,.03f);
            }
            finally {Object.DestroyImmediate(mesh);Object.DestroyImmediate(instance);Object.DestroyImmediate(camera.gameObject);}
        }

        [TestCase(.5f)] [TestCase(2.5f)] [TestCase(4.5f)]
        public void E_rendered_range_ring_keeps_the_actual_three_point_one_meter_radius(float seconds)
        {
            var instance=Object.Instantiate(Resources.Load<GameObject>("moba/presentation/lux/E_Charge"));
            // The production area cue supplies its authoritative radius.
            instance.transform.localScale*=3.1f;
            var camera=new GameObject("orb geometry camera").AddComponent<Camera>();var mesh=new Mesh();
            try
            {
                var particle=instance.transform.Find("FX_E_Charge/PS_RangeCircle").GetComponent<ParticleSystem>();
                particle.useAutoRandomSeed=false;particle.randomSeed=42;particle.Simulate(seconds,false,true,true);
                Assert.Greater(particle.particleCount,0);
                particle.GetComponent<ParticleSystemRenderer>().BakeMesh(mesh,camera,true);mesh.RecalculateBounds();
                // The circular texture lives on a rotated quad. Its diameter
                // is the quad edge length; its world AABB includes the corners.
                var vertices=mesh.vertices;
                Assert.AreEqual(4,vertices.Length);
                var distances=(from a in vertices from b in vertices let d=Vector3.Distance(a,b) where d>.001f select d).ToArray();
                Assert.AreEqual(6.2f,distances.Min(),.03f);
                Assert.AreEqual(6.2f*Mathf.Sqrt(2),distances.Max(),.03f);
                Assert.Greater(mesh.bounds.min.y+particle.transform.position.y,.02f);
            }
            finally {Object.DestroyImmediate(mesh);Object.DestroyImmediate(instance);Object.DestroyImmediate(camera.gameObject);}
        }

        [TestCase("life/alive/action/idle",0,"Lux_Model_Idle")]
        [TestCase("life/alive/action/orb.flight",0,"Lux_Model_Idle")]
        [TestCase("life/alive/action/orb.deployed",0,"Lux_Model_Idle")]
        [TestCase("life/dead",0,"Lux_Model_Death")]
        public void Hfsm_substates_drive_the_actual_animator(string path,int skill,string state)
        {
            var shell=new BattleViewResourceProvider().CreateShellGameObject(3007001,3007);
            try
            {
                var component=Component();
                component.ApplyPresentation(new CharacterActionData(actorId:3007001,entityVersion:1,snapshotFrame:0,
                    path:path,instanceId:1,startFrame:0,localFrame:0,castInstanceId:skill>0?1:0,skillId:skill));
                var view=shell.GetComponent<MonoCharacterHfsmView>()??shell.AddComponent<MonoCharacterHfsmView>();
                view.Bind(component,30);
                Assert.AreEqual(Animator.StringToHash(state),shell.GetComponentInChildren<Animator>(true).GetCurrentAnimatorStateInfo(0).shortNameHash);
            }
            finally { Object.DestroyImmediate(shell); }
        }

        [TestCase(30070090,90037008)]
        [TestCase(30070290,90037010)]
        public void Buff_owned_cues_instantiate_once_and_release_with_their_source(int buff,int vfx)
        {
            using(var h=MobaSkillConfigTestHarness.CreateForSinglePlayer(new[]{30070201,30070301,30070401,30070501},worldId:"lux_buff_"+buff,heroId:3007,attributeTemplateId:3007))
            {
                h.EnterGameAndWarmup();var actor=h.AssertPlayerActorBound();
                var world=new EntityWorld();var root=world.Create("lux-buff-view");
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

        [UnityTest] public IEnumerator Q_actual_timeline_plays_complete_clip_and_restores_idle()=>CastAnimation(1,30070201,"SpellQ",1.25f);
        [UnityTest] public IEnumerator W_actual_timeline_plays_complete_clip_and_restores_idle()=>CastAnimation(2,30070301,"SpellW",5.041667f);
        [UnityTest] public IEnumerator E_actual_timeline_plays_complete_clip_and_restores_idle()=>CastAnimation(3,30070401,"SpellE",1.25f);
        [UnityTest] public IEnumerator R_actual_timeline_plays_complete_clip_and_restores_idle()=>CastAnimation(4,30070501,"SpellR",2.5f);
        [UnityTest] public IEnumerator R_forward_aim_uses_authoritative_frame()=>CastAnimation(4,30070501,"SpellR",2.5f,Vec3.Forward);
        [UnityTest] public IEnumerator R_reverse_aim_uses_authoritative_frame()=>CastAnimation(4,30070501,"SpellR",2.5f,new Vec3(0,0,-1));
        [UnityTest] public IEnumerator AA_actual_timeline_plays_complete_clip_and_restores_idle()=>CastAnimation(5,30070101,"AutoAttack",1.25f);

        private static IEnumerator CastAnimation(int slot,int skill,string key,float span,Vec3? aimedDirection=null)
        {
            using(var h=MobaSkillConfigTestHarness.CreateForSinglePlayer(new[]{30070201,30070301,30070401,30070501},worldId:"lux_anim_"+skill,heroId:3007,attributeTemplateId:3007))
            {
                h.EnterGameAndWarmup();var actor=h.AssertPlayerActorBound();var target=0;
                Assert.IsTrue(h.World.Services.Resolve<MobaActorLookupService>().TryGetActorEntity(actor,out var caster));
                var casterTransform=caster.transform.Value;
                caster.ReplaceTransform(new Transform3(new Vec3(6,0,8),casterTransform.Rotation,casterTransform.Scale));
                var direction=aimedDirection??Vec3.Right;
                if(slot==5) target=h.SpawnScenarioActor("enemy",0,"Hero",2,1001,1001,1,1,1,"lux_enemy",0,null,0,new MobaAcceptanceVector3Expectation{x=9,z=8});
                var result=h.World.Services.Resolve<SkillCastCoordinator>().TryCastBySlot(actor,slot,new Vec3(6,0,8)+direction*6,direction,target);
                Assert.IsTrue(result.Success,result.FailReason);h.Tick(1);var action=Capture(h,actor);
                Assert.AreEqual(skill,action.SkillId);
                var shell=new BattleViewResourceProvider().CreateShellGameObject(actor,3007);
                shell.transform.position=new Vector3(6,0,8);
                shell.transform.rotation=Quaternion.Euler(0,-30,0);
                try
                {
                    Assert.IsNotEmpty(shell.GetComponentsInChildren<SkinnedMeshRenderer>(true));
                    if(skill==30070501) Assert.IsNotNull(Resources.Load<GameObject>("moba/presentation/lux/R_Channel"));
                    var animator=shell.GetComponentInChildren<Animator>(true);
                    animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
                    var component=Component();component.ApplyPresentation(action);
                    var view=shell.GetComponent<MonoCharacterHfsmView>()??shell.AddComponent<MonoCharacterHfsmView>();view.Bind(component,h.TickRate);
                    var hash=Animator.StringToHash("Lux_Model_"+key);
                    for(var f=0;f<60 && animator.GetCurrentAnimatorStateInfo(0).shortNameHash!=hash;f++) yield return null;
                    animator.Update(0);Assert.AreEqual(hash,animator.GetCurrentAnimatorStateInfo(0).shortNameHash);Assert.AreEqual(1,animator.speed,.002f);
                    if(skill==30070501)
                    {
                        for(var wait=0;wait<60&&!Object.FindObjectsOfType<Animator>().Any(a=>a.runtimeAnimatorController!=null&&a.runtimeAnimatorController.name=="R_Channel");wait++) yield return null;
                        var beam=Object.FindObjectsOfType<Animator>().Single(a=>a.runtimeAnimatorController!=null&&a.runtimeAnimatorController.name=="R_Channel");
                        var inspection=beam.GetComponentInParent<AbilityKit.Game.Platform.Unity.Presentation.UnityPresentationInstanceState>();
                        Assert.IsNotNull(inspection);
                        Assert.IsFalse(inspection.Request.FollowAttachment,"Template must resolve placement-only semantics.");
                        Debug.Log("[LuxBeam] parent="+(beam.transform.parent?beam.transform.parent.name:"null")+" backend="+inspection.Backend+" key="+inspection.Request.LogicalKey+" template="+inspection.Request.TemplateId);
                        Assert.IsTrue(action.CastFrame.HasValue);
                        Assert.Less(Vector3.Distance(new Vector3(action.CastFrame.OriginX,action.CastFrame.OriginY,action.CastFrame.OriginZ),beam.transform.position),.01f,"Beam must use the authoritative nonzero Cast origin.");
                        Assert.Greater(Vector3.Dot(new Vector3(direction.X,direction.Y,direction.Z),beam.transform.forward),.999f,"Beam must use authoritative aim, independently of model facing.");
                        var frozen=beam.transform.position;var forward=beam.transform.forward;
                        shell.transform.position+=Vector3.left*3;
                        yield return null;
                        Assert.Less(Vector3.Distance(frozen,beam.transform.position),.01f,"Beam origin must remain frozen after creation.");
                        Assert.Greater(Vector3.Dot(forward,beam.transform.forward),.999f);
                        view.SeekToFrame(action.StartFrame+5,h.FixedDelta);
                        view.Bind(component,h.TickRate);
                        yield return null;
                        Assert.IsTrue(beam,"Binding an older snapshot of the same action must keep the live beam instance.");
                    }
                    var midpoint=Mathf.FloorToInt(span/(2*h.FixedDelta));
                    view.SeekToFrame(action.StartFrame+midpoint,h.FixedDelta);
                    Assert.AreEqual(midpoint*h.FixedDelta/span,animator.GetCurrentAnimatorStateInfo(0).normalizedTime,.01f);
                    var nearEnd=Mathf.Max(1,Mathf.FloorToInt(span/h.FixedDelta)-2);
                    view.SeekToFrame(action.StartFrame-1,h.FixedDelta);view.SeekToFrame(action.StartFrame+nearEnd,h.FixedDelta);
                    animator.Update(0);
                    var sample=component.Evaluate(action.StartFrame+nearEnd,h.TickRate).Value;
                    Debug.Log("[LuxAnimationSample] skill="+skill+" tick="+h.TickRate+" delta="+h.FixedDelta+" start="+action.StartFrame+
                        " sample="+sample.LocalFrame+" frames="+sample.FrameCount+" authored="+sample.AuthoredFrameCount+" fps="+sample.AuthoredFrameRate+
                        " length="+animator.GetCurrentAnimatorStateInfo(0).length+" normalized="+animator.GetCurrentAnimatorStateInfo(0).normalizedTime+
                        " clips="+string.Join(",",animator.GetCurrentAnimatorClipInfo(0).Select(c=>c.clip.name+":"+c.clip.length)));
                    Assert.AreEqual(nearEnd*h.FixedDelta/span,animator.GetCurrentAnimatorStateInfo(0).normalizedTime,.03f);
                    h.TickMilliseconds((int)(span*1000)+200);var after=Capture(h,actor);
                    Assert.AreEqual(0,after.CastInstanceId);component.ApplyPresentation(after);view.SeekToFrame(after.SnapshotFrame,h.FixedDelta);animator.Update(0);
                    Assert.AreEqual(Animator.StringToHash("Lux_Model_Idle"),animator.GetCurrentAnimatorStateInfo(0).shortNameHash);
                }
                finally { Object.DestroyImmediate(shell); }
            }
        }

        [TestCase(3,30070401,42070401,90037004)]
        [TestCase(3,30070401,42070402,90037005)]
        public void Real_area_cues_create_and_release_the_owned_vfx(int slot,int skill,int area,int vfx)
        {
            using(var h=MobaSkillConfigTestHarness.CreateForSinglePlayer(new[]{30070201,30070301,30070401,30070501},worldId:"lux_area_"+area,heroId:3007,attributeTemplateId:3007))
            {
                h.EnterGameAndWarmup();var actor=h.AssertPlayerActorBound();
                var world=new EntityWorld();var root=world.Create("lux-area-view");
                var query=new BattleEntityQuery(world,new BattleEntityLookup());var manager=new BattleVfxManager(BattleViewFactory.GetOrLoadVfxDb());
                var handler=new BattlePresentationCueViewEventHandler(world,query,manager,root);var started=false;var ended=false;
                try
                {
                    Assert.IsTrue(h.World.Services.Resolve<SkillCastCoordinator>().TryCastBySlot(actor,slot,new Vec3(6,0,0),Vec3.Right,0).Success);
                    for(var t=0;t<260&&!ended;t++)
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
                                    if(area==42070401)
                                    {
                                        var ring=visual.GameObject.transform.Find("FX_E_Charge/PS_RangeCircle").GetComponent<ParticleSystem>();
                                        var camera=new GameObject("real area geometry camera").AddComponent<Camera>();var mesh=new Mesh();
                                        try
                                        {
                                            ring.Simulate(.5f,false,true,true);
                                            ring.GetComponent<ParticleSystemRenderer>().BakeMesh(mesh,camera,true);
                                            var vertices=mesh.vertices;Assert.AreEqual(4,vertices.Length);
                                            var edges=(from a in vertices from b in vertices let d=Vector3.Distance(a,b) where d>.001f select d).ToArray();
                                            Assert.AreEqual(6.2f,edges.Min(),.03f,"The actual area cue must apply radius exactly once.");
                                            Assert.AreEqual(.03f,ring.transform.position.y,.01f);
                                            var sphere=visual.GameObject.transform.Find("FX_E_Charge/PS_MainSphere");
                                            Assert.Greater(sphere.position.y,.6f);Assert.Less(sphere.position.y,1.2f);
                                        }
                                        finally{Object.DestroyImmediate(mesh);Object.DestroyImmediate(camera.gameObject);}
                                    }
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
        { var loader=new ResourcesTextAssetLoader();return new BattleCharacterHfsmComponent(MobaCharacterHfsmProfile.ResolveDefinition(loader,3007),CharacterPresentationActionCatalog.Load(loader),3007); }
        private static CharacterActionData Capture(MobaSkillConfigTestHarness h,int actor)
        {
            var snapshots=new List<WorldStateSnapshot>();h.World.Services.Resolve<IWorldStateSnapshotBatchProvider>().CollectSnapshots(h.FrameTime.Frame,snapshots,32);
            foreach(var snapshot in snapshots)if(CharacterActionSnapshotRoute.TryDecode(snapshot,out var entries))foreach(var entry in entries)if(entry.ActorId==actor)return entry;
            Assert.Fail("Missing real CharacterAction snapshot.");return default;
        }

        [UnityTest] public IEnumerator Q_projectile_snapshot_creates_follows_and_releases_vfx()=>Projectile(1,30070201,32070201,90037012);
        [UnityTest] public IEnumerator W_projectile_snapshot_creates_follows_and_releases_vfx()=>Projectile(2,30070301,32070301,90037017);
        [UnityTest] public IEnumerator E_projectile_snapshot_creates_follows_and_releases_vfx()=>Projectile(3,30070401,32070401,90037006);

        private static IEnumerator Projectile(int slot,int skill,int projectile,int vfxId)
        {
            using(var h=MobaSkillConfigTestHarness.CreateForSinglePlayer(new[]{30070201,30070301,30070401,30070501},worldId:"lux_projectile_"+skill,heroId:3007,attributeTemplateId:3007))
            {
                h.EnterGameAndWarmup();var actor=h.AssertPlayerActorBound();
                var world=new EntityWorld();var root=world.Create("lux-projectile-view");
                var db=BattleViewFactory.GetOrLoadVfxDb();var manager=new BattleVfxManager(db);
                var query=new BattleEntityQuery(world,new BattleEntityLookup());
                var handler=new BattleProjectileViewEventHandler(world,query,manager,in root,new BattleViewResourceProvider(h.Config,db));
                var spawned=false;var exited=false;var instance=default(IEntityId);
                try
                {
                    Assert.IsTrue(h.World.Services.Resolve<SkillCastCoordinator>().TryCastBySlot(actor,slot,new Vec3(6,0,0),Vec3.Right,0).Success);
                    for(var tick=0;tick<180&&!exited;tick++)
                    {
                        h.Tick(1);var snapshots=new List<WorldStateSnapshot>();
                        h.World.Services.Resolve<IWorldStateSnapshotBatchProvider>().CollectSnapshots(h.FrameTime.Frame,snapshots,32);
                        foreach(var snapshot in snapshots)
                        {
                            if(snapshot.OpCode!=MobaOpCodes.Snapshot.ProjectileEvent)continue;
                            foreach(var entry in MobaProjectileEventSnapshotCodec.Deserialize(snapshot.Payload))
                            {
                                if(entry.TemplateId!=projectile)continue;
                                handler.HandleSnapshot(new[]{ProjectileEventSnapshotMapper.Map(in entry)});
                                if(entry.Kind==(int)ProjectileEventKind.Spawn)
                                {
                                    spawned=true;Assert.AreEqual(1,root.ChildCount);var entity=root.GetChild(0);instance=entity.Id;
                                    Assert.IsTrue(entity.TryGetRef(out BattleVfxComponent effect));Assert.AreEqual(vfxId,effect.VfxId);
                                    Assert.IsTrue(entity.TryGetRef(out BattleViewGameObjectComponent view));Assert.IsNotNull(view.GameObject);
                                    Assert.IsNotEmpty(view.GameObject.GetComponentsInChildren<Renderer>(true));
                                    Assert.IsTrue(entity.TryGetRef(out BattleViewFollowComponent follow));Assert.AreEqual(entry.ProjectileActorId,follow.TargetActorId);
                                    foreach(var animator in view.GameObject.GetComponentsInChildren<Animator>(true))
                                        Assert.AreEqual(0,animator.parameterCount,"Missile VFX play their default authored state.");
                                }
                                if(entry.Kind==(int)ProjectileEventKind.Exit)
                                { exited=true;Assert.IsFalse(world.IsAlive(instance)); }
                            }
                        }
                    }
                    Assert.IsTrue(spawned);Assert.IsTrue(exited);Assert.AreEqual(0,root.ChildCount);
                }
                finally {manager.Clear(root);if(root.IsValid)root.Destroy();}
            }
            yield return null;
        }
    }
}

