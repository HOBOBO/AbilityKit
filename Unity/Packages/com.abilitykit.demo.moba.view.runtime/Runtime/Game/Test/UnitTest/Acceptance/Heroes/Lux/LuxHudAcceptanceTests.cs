using System;
using System.Collections;
using System.IO;
using System.Linq;
using AbilityKit.Ability.Host;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba;
using AbilityKit.Demo.Moba.Attributes;
using AbilityKit.Demo.Moba.Components;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.Buffs;
using AbilityKit.Deterministic;
using AbilityKit.Game.Battle.View.Lib.Skill;
using AbilityKit.Game.Flow;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace AbilityKit.Game.Test.UnitTest
{
    /// <summary>Real local battle, production HUD pointer events and rendered review frames.</summary>
    internal static class LuxHudAcceptanceTests
    {
        public static IEnumerator Run()
        {
            DemoGameplayTestLauncher.OpenMobaLocalForPlay();
            yield return new EnterPlayMode();
            Time.captureDeltaTime = 1f / 60f;
            yield return Review();
        }

        public static IEnumerator Cleanup()
        {
            Time.captureDeltaTime = 0;
            if (Application.isPlaying) yield return new ExitPlayMode();
            DemoGameplayTestLauncher.Clear();
        }

        private static IEnumerator Review()
        {
            yield return Until(() => GameEntry.IsInitialized, 1800, "GameEntry startup");
            var entry = GameEntry.Instance;
            var flow = entry.Get<GameFlowDomain>();
            yield return Until(() => flow.CurrentPhase == MobaRootState.Lobby || flow.CurrentPhase == MobaRootState.Battle, 1800, "local startup");
            if(flow.CurrentPhase==MobaRootState.Lobby)flow.EnterBattle(new TestBattleBootstrapper());
            yield return Until(() => flow.CurrentBattlePhase == MobaBattleState.InMatch, 18000, "local InMatch");
            Assert.IsTrue(entry.TryGet(out BattleContext ctx));
            var plan = ctx.Plan;
            var player = BattleInputSessionIdentity.ResolvePlayerId((IBattleInputSessionIdentityPort)ctx);
            var replace = BattleInputCommandFactory.CreateDebugReplaceHero(
                SessionSimRuntimeTuning.ResolveInputSubmitFrame(ctx.LastFrame, in plan), player, 3007);
            Assert.IsTrue(new BattleInputSubmitter(ctx.InputRuntime, player,
                BattleInputSessionIdentity.ResolveWorldId(in plan)).Submit(in replace));
            yield return Until(() => Buttons().Length == 5, 360, "Lux HUD loadout");
            Assert.IsTrue(ctx.Session.TryGetWorld(out var world));
            var actors = world.Services.Resolve<MobaActorLookupService>();
            Assert.IsTrue(actors.TryGetActorEntity(ctx.LocalActorId, out var caster));
            var players = world.Services.Resolve<MobaPlayerActorMapService>();
            Assert.IsTrue(players.TryGetActorId(new PlayerId("p2"), out var enemyId));
            Assert.IsTrue(actors.TryGetActorEntity(enemyId, out var enemy));
            if (enemy.hasActorBrain) enemy.RemoveActorBrain();
            Place(caster, new Vec3(7, 0, -4));
            Place(enemy, new Vec3(7, 0, 2));
            var attributes = new MobaAttrs(caster);
            attributes.Mana = 467;
            var enemyAttributes = new MobaAttrs(enemy); enemyAttributes.Hp = 5000;
            yield return Frames(20);
            var directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../../local/Logs/lux-hud"));
            var args=Environment.GetCommandLineArgs();var outputIndex=Array.IndexOf(args,"-luxHudOutput");
            if(outputIndex>=0)directory=args[outputIndex+1];
            Directory.CreateDirectory(directory);
            foreach(var renderer in Object.FindObjectsOfType<SkinnedMeshRenderer>())
                Debug.Log("[LuxHudReview] mesh="+renderer.name+" bounds="+renderer.bounds.size+" scale="+renderer.transform.lossyScale);
            Capture(directory, "01-ready");

            var input = entry.Get<BattleInputFeature>();
            foreach(var token in new[]{" Q "," W "," E "," R "})
            {
                attributes.Mana=480;
                var button=Button(token);var pointer=Pointer(button);
                button.OnPointerDown(pointer);yield return Frames(20);
                pointer.position+=new Vector2(0,100);button.OnDrag(pointer);yield return Frames(3);
                Assert.IsTrue(new BattleHudAimPreviewPositionResolver().TryResolve(ctx,out var aim));
                var distance=token==" E "?aim.AimDistance:6f;
                var target=aim.CasterPosition+aim.AimDirection*distance;
                Place(enemy,new Vec3(target.x,target.y,target.z));
                var hp=new MobaAttrs(enemy).Hp;var submitted=input.SkillSubmitSuccessCount;
                Capture(directory,token.Trim()+"-aim");button.OnPointerUp(pointer);
                yield return Until(()=>input.SkillSubmitSuccessCount==submitted+1,60,token+" HUD submit");
                if(token==" R ")
                {
                    Animator castBeam=null;
                    for(var sample=1;sample<=12;sample++)
                    {
                        yield return Frames(6);Capture(directory,"R-frame-"+(sample*6).ToString("D2"));
                        foreach(var beam in Object.FindObjectsOfType<Animator>().Where(a=>a.runtimeAnimatorController!=null&&a.runtimeAnimatorController.name=="R_Channel"))
                        {
                            var state=beam.GetComponentInParent<AbilityKit.Game.Platform.Unity.Presentation.UnityPresentationInstanceState>();
                            if(state&&state.Request.TemplateId==90037014)
                            {
                                Assert.Greater(Vector3.Dot(aim.AimDirection.normalized,beam.transform.forward),.999f,
                                    "Actual HUD beam must agree with the authoritative aimed rectangle.");
                                Assert.Less(Vector3.Distance(aim.CasterPosition,beam.transform.position),.1f);
                                if(castBeam==null) castBeam=beam;
                                else Assert.AreSame(castBeam,beam,"Forward battle snapshots must not restart the cast beam.");
                                if(sample>=8) Assert.Greater(beam.GetCurrentAnimatorStateInfo(0).normalizedTime,.1f,
                                    "The actual beam controller must advance beyond its opening frame.");
                            }
                            Debug.Log("[LuxHudReview] R frame="+(sample*6)+" origin="+beam.transform.position+" forward="+beam.transform.forward+
                                " time="+beam.GetCurrentAnimatorStateInfo(0).normalizedTime+" particles="+beam.GetComponentsInChildren<ParticleSystem>().Sum(p=>p.particleCount)+
                                " key="+(state?state.Request.LogicalKey:"none")+" follow="+(state&&state.Request.FollowAttachment)+
                                " parent="+(beam.transform.parent?beam.transform.parent.name:"null")+" actionStart="+caster.characterHfsm.Runtime.Action.StartFrame+
                                " actionLocal="+caster.characterHfsm.Runtime.Action.LocalFrame);
                        }
                    }
                }
                else yield return Frames(30);
                Capture(directory,token.Trim()+"-cast");
                if(token==" W ")
                {
                    Assert.Greater(world.Services.Resolve<MobaShieldService>().GetTotalRemaining(ctx.LocalActorId),0);
                    yield return Frames(320);
                }
                else if(token==" E ")
                {
                    yield return Until(()=>caster.hasBuffs&&caster.buffs.Active.Any(b=>b.BuffId==30070492),120,"E deployed");
                    yield return Frames(24);
                    Capture(directory,"E-deployed");
                    var mana=attributes.Mana;var recastSubmits=input.SkillSubmitSuccessCount;
                    Click(Button(" E "));
                    yield return Until(()=>input.SkillSubmitSuccessCount==recastSubmits+1,60,"E recast HUD submit");
                    yield return Until(()=>!caster.buffs.Active.Any(b=>b.BuffId==30070492),120,"E recast terminal");
                    Assert.AreEqual(mana,attributes.Mana,.05f);
                    Assert.IsFalse(caster.buffs.Active.Any(b=>b.BuffId==30070492));
                    yield return Frames(12);Capture(directory,"E-explosion");yield return Frames(100);
                    Assert.Less(new MobaAttrs(enemy).Hp,hp);
                }
                else
                {
                    if(token==" R ") {yield return Frames(9);Capture(directory,"R-beam");}
                    yield return Frames(80);
                    Assert.Less(new MobaAttrs(enemy).Hp,hp,token+" must damage the real HUD-selected enemy.");
                    Capture(directory,token.Trim()+"-complete");
                }
            }
            File.WriteAllText(Path.Combine(directory,"review.txt"),
                "Real PlayMode local battle; production HUD Q/W/E/R directional/point aim and E free recast.\n"+
                "Assertions: input submission, Q/R damage, W self shield, E deployment/damage and recast cleanup.\n"+
                "PNG frames render the actual battle camera and HUD; visual review is separate.\n");
        }

        private static SkillButtonView[] Buttons() => Object.FindObjectsOfType<SkillButtonView>()
            .Where(b => b.PresentationName.StartsWith("Lux ", StringComparison.Ordinal)).ToArray();
        private static SkillButtonView Button(string token) => Buttons().Single(b => b.PresentationName.Contains(token));
        private static PointerEventData Pointer(SkillButtonView button) => new PointerEventData(EventSystem.current)
        { pointerId = 101, position = RectTransformUtility.WorldToScreenPoint(null, button.transform.position) };
        private static void Click(SkillButtonView button)
        { var pointer = Pointer(button); button.OnPointerDown(pointer); button.OnPointerUp(pointer); }
        private static void Place(ActorEntity actor, Vec3 position)
        { var t = actor.transform.Value; actor.ReplaceTransform(new Transform3(position, t.Rotation, t.Scale)); }
        private static IEnumerator Frames(int count)
        { for (var i = 0; i < count; i++) yield return null; }
        private static IEnumerator Until(Func<bool> condition, int frames, string operation)
        { for (var i = 0; i < frames; i++) { if (condition()) yield break; yield return null; } Assert.Fail(operation + " timed out."); }

        private static void Capture(string directory, string name)
        {
            Assert.AreNotEqual(UnityEngine.Rendering.GraphicsDeviceType.Null, SystemInfo.graphicsDeviceType,
                "HUD appearance capture requires a graphics device; run without -nographics.");
            var camera = Camera.main;
            Assert.IsNotNull(camera);
            var canvases = Object.FindObjectsOfType<Canvas>().Where(c => c.renderMode == RenderMode.ScreenSpaceOverlay).ToArray();
            var rt = new RenderTexture(1280, 720, 24);
            var oldTarget = camera.targetTexture; var oldActive = RenderTexture.active;
            var texture = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = rt;
                foreach (var canvas in canvases)
                { canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1; }
                Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active = rt;
                texture.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); texture.Apply();
                File.WriteAllBytes(Path.Combine(directory, name + ".png"), texture.EncodeToPNG());
            }
            finally
            {
                foreach (var canvas in canvases) { canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.worldCamera = null; }
                camera.targetTexture = oldTarget; RenderTexture.active = oldActive;
                Object.DestroyImmediate(texture); rt.Release(); Object.DestroyImmediate(rt); Canvas.ForceUpdateCanvases();
            }
        }
    }
}
