using System;
using System.Collections;
using System.IO;
using System.Linq;
using AbilityKit.Ability.Host;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba;
using AbilityKit.Demo.Moba.Attributes;
using AbilityKit.Demo.Moba.Components;
using AbilityKit.Demo.Moba.Gameplay;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.Buffs;
using AbilityKit.Deterministic;
using AbilityKit.Game.Battle.Component;
using AbilityKit.Game.Battle.View.Lib.Skill;
using AbilityKit.Game.Battle.Vfx;
using AbilityKit.Game.Flow;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace AbilityKit.Game.Test.UnitTest
{
    /// <summary>Real local battle, production HUD pointer events and rendered review frames.</summary>
    internal static class KarthusHudAcceptanceTests
    {
        public static IEnumerator Run()
        {
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
            yield return Until(() => flow.CurrentBattlePhase == MobaBattleState.InMatch, 18000, "local InMatch");
            Assert.IsTrue(entry.TryGet(out BattleContext ctx));
            var plan = ctx.Plan;
            var player = BattleInputSessionIdentity.ResolvePlayerId((IBattleInputSessionIdentityPort)ctx);
            yield return Until(() => Buttons().Length == 5, 360, "initial HUD loadout");
            var previousQ = Button(1).PresentationName;
            var replace = BattleInputCommandFactory.CreateDebugReplaceHero(
                SessionSimRuntimeTuning.ResolveInputSubmitFrame(ctx.LastFrame, in plan), player, 3006);
            Assert.IsTrue(new BattleInputSubmitter(ctx.InputRuntime, player,
                BattleInputSessionIdentity.ResolveWorldId(in plan)).Submit(in replace));
            yield return Until(() => Buttons().Length == 5 && Button(1).PresentationName != previousQ, 360, "Karthus HUD loadout");
            var livingQ = Button(1).PresentationName;
            Assert.IsTrue(ctx.Session.TryGetWorld(out var world));
            var actors = world.Services.Resolve<MobaActorLookupService>();
            Assert.IsTrue(actors.TryGetActorEntity(ctx.LocalActorId, out var caster));
            var players = world.Services.Resolve<MobaPlayerActorMapService>();
            Assert.IsTrue(players.TryGetActorId(new PlayerId("p2"), out var enemyId));
            Assert.IsTrue(actors.TryGetActorEntity(enemyId, out var enemy));
            if (enemy.hasActorBrain) enemy.RemoveActorBrain();
            // Open ground to keep the authored wall and target decals visible.
            Place(caster, new Vec3(1, 0, -7));
            Place(enemy, new Vec3(1, 0, -1));
            var attributes = new MobaAttrs(caster);
            attributes.Mana = 467;
            var enemyAttributes = new MobaAttrs(enemy); enemyAttributes.Hp = 5000;
            yield return Frames(20);
            var reviewCamera = Camera.main;
            Assert.IsNotNull(reviewCamera);
            reviewCamera.transform.position = new Vector3(1, 18, -20);
            reviewCamera.transform.LookAt(new Vector3(1, 0, -2));
            reviewCamera.orthographic = true;
            reviewCamera.orthographicSize = 8.5f;
            var directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../../local/Logs/karthus-hud-acceptance",
                DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff")));
            Directory.CreateDirectory(directory);
            Capture(directory, "01-ready");

            var input = entry.Get<BattleInputFeature>();
            var q = Button(1);
            var pointer = Pointer(q);
            q.OnPointerDown(pointer);
            yield return Frames(30);
            pointer.position += new Vector2(0, 100);
            q.OnDrag(pointer);
            yield return Frames(3);
            Assert.IsTrue(new BattleHudAimPreviewPositionResolver().TryResolve(ctx, out var aim));
            var target = aim.CasterPosition + aim.AimDirection * aim.AimDistance;
            Place(enemy, new Vec3(target.x, target.y, target.z));
            Capture(directory, "02-q-aim");
            var qHp = new MobaAttrs(enemy).Hp;
            var submitted = input.SkillSubmitSuccessCount;
            q.OnPointerUp(pointer);
            yield return Until(() => input.SkillSubmitSuccessCount == submitted + 1, 30, "Q HUD submit");
            yield return Frames(20);
            Capture(directory, "03-q-warning");
            yield return Until(() => new MobaAttrs(enemy).Hp < qHp, 180, "Q delayed damage");
            yield return Frames(5);
            Capture(directory, "04-q-explosion");
            yield return Frames(55);

            var w = Button(2);
            var wallSkill = caster.skillLoadout.ActiveSkills[1];
            Assert.AreEqual(30060301, wallSkill.SkillId);
            Assert.AreEqual(1, wallSkill.Level);
            pointer = Pointer(w); w.OnPointerDown(pointer);
            yield return Frames(30);
            pointer.position += new Vector2(0, 90); w.OnDrag(pointer);
            yield return Frames(3);
            AssertWallAim(ctx, 8f);
            Capture(directory, "05-w-aim-rank-one");
            wallSkill.Level = 5;
            yield return Until(() => FindWallPreview()?.transform.parent.Find("Line").localScale.z >= 11.99f,
                90, "rank-five W HUD preview from the live skill snapshot");
            AssertWallAim(ctx, 12f);
            Capture(directory, "05-w-aim-rank-five");
            wallSkill.Level = 1;
            yield return Until(() => FindWallPreview()?.transform.parent.Find("Line").localScale.z <= 8.01f,
                90, "rank-one W HUD preview restored before release");
            w.OnPointerUp(pointer);
            yield return Frames(60); Capture(directory, "06-w-wall");
            yield return Frames(40);

            Place(enemy, caster.transform.Value.Position + Vec3.Forward * 3);
            Click(Button(3));
            yield return Frames(40);
            Assert.IsTrue(caster.hasBuffs && caster.buffs.Active.Any(b => b.BuffId == 30060490));
            Capture(directory, "07-e-aura");
            Click(Button(3)); yield return Frames(40);
            Assert.IsFalse(caster.hasBuffs && caster.buffs.Active.Any(b => b.BuffId == 30060490));

            attributes.Mana = 467;
            var hp = new MobaAttrs(enemy).Hp;
            Click(Button(4));
            yield return Frames(55);
            Assert.IsTrue(enemy.hasBuffs && enemy.buffs.Active.Any(b => b.BuffId == 30060590));
            Capture(directory, "08-r-target-warning");
            yield return Until(() => new MobaAttrs(enemy).Hp < hp, 300, "R impact");
            yield return Frames(3);
            Assert.IsFalse(enemy.hasBuffs && enemy.buffs.Active.Any(b => b.BuffId == 30060590));
            Assert.Less(new MobaAttrs(enemy).Hp, hp, "The real HUD R must damage its enemy target.");
            Capture(directory, "09-r-hit-and-cooldown");

            attributes.Mana = 0;
            var respawn = world.Services.Resolve<MobaGameplayRespawnService>();
            var beforeDeath = respawn.ExportState(default);
            var lethal = new AttackInfo { AttackerActorId = enemyId, TargetActorId = ctx.LocalActorId,
                DamageType = DamageType.True, ReasonKind = DamageReasonKind.Skill };
            lethal.BaseDamage.FixedBaseValue = Fixed64.FromInt32(100000);
            world.Services.Resolve<MobaCombatEffectService>().DealDamage(lethal);
            respawn.ImportState(default, beforeDeath);
            var rules = world.Services.Resolve<MobaCombatRulesService>();
            yield return Until(() => Button(1).PresentationName != livingQ && HasBuff(30060090) &&
                PlaysModelState("Karthus_Model_Idle") && HasVisibleParticles(90036003), 60, "spirit idle and death aura");
            var spiritFrame = ctx.LastFrame;
            Assert.AreEqual(0, attributes.Hp, .001f);
            Assert.IsFalse(rules.CanMove(ctx.LocalActorId));
            Assert.IsFalse(rules.CanAttack(ctx.LocalActorId));
            Capture(directory, "10-death-slots-and-aura");
            submitted = input.SkillSubmitSuccessCount;
            q = Button(1); pointer = Pointer(q); q.OnPointerDown(pointer);
            yield return Frames(30); pointer.position += new Vector2(0, 70); q.OnDrag(pointer);
            yield return Frames(3); q.OnPointerUp(pointer);
            yield return Until(() => input.SkillSubmitSuccessCount == submitted + 1, 30, "death Q HUD submit");
            yield return Until(() => PlaysModelState("Karthus_Model_SpellQ") && HasVisibleParticles(90036006),
                60, "spirit Q animation and warning");
            Capture(directory, "11-death-q-warning");
            yield return Until(() => HasVisibleParticles(90036008), 90, "spirit Q explosion");
            Assert.AreEqual(0, attributes.Mana, .001f);
            Capture(directory, "12-death-q-explosion");
            yield return Until(() => !HasBuff(30060090) && !HasBuff(30060490) &&
                PlaysModelState("Karthus_Model_Death") && !IsActiveVfx(90036003) && !IsActiveVfx(90036005),
                540, "spirit expiration, corpse pose and aura removal");
            var expiredFrame = ctx.LastFrame;
            Assert.That(expiredFrame - spiritFrame, Is.InRange(200, 220), "Expected the configured seven-second spirit window at 30 Hz.");
            Assert.AreEqual(0, attributes.Hp, .001f);
            Capture(directory, "13-spirit-expired-corpse");
            yield return Until(() => !IsActiveVfx(90036018), 120, "E deactivation shockwave cleanup");
            Assert.IsTrue(PlaysModelState("Karthus_Model_Death"));
            Capture(directory, "14-corpse-after-shockwave");
            File.WriteAllText(Path.Combine(directory, "review.txt"),
                "Real PlayMode local battle; production HUD Q/W aim, E toggle, R click and death Q.\n" +
                "Images render the battle camera together with the actual HUD canvases.\n" +
                "The fixture suppresses respawn after applying real lethal damage so the natural spirit expiry is observable.\n" +
                $"Spirit first visible at frame {spiritFrame}; corpse and aura removal at frame {expiredFrame}.\n" +
                "Final appearance requires visual inspection of the PNG files.\n");
            Debug.Log($"[KarthusHudAcceptance] {directory}");

            bool HasBuff(int id) => caster.hasBuffs && caster.buffs.Active?.Any(buff => buff.BuffId == id) == true;
        }

        private static BattleAreaLengthScaleBinding FindWallPreview() =>
            Object.FindObjectsOfType<BattleAreaLengthScaleBinding>()
                .SingleOrDefault(binding => binding.transform.parent != null && binding.transform.parent.name == "SkillAimPreview");

        private static void AssertWallAim(BattleContext context, float expectedLength)
        {
            Assert.IsTrue(new BattleHudAimPreviewPositionResolver().TryResolve(context, out var aim));
            Assert.AreEqual(2, aim.Slot);
            var wall = FindWallPreview();
            Assert.IsNotNull(wall);
            var root = wall.transform.parent;
            var rectangle = root.Find("Line");
            Assert.IsTrue(rectangle.gameObject.activeInHierarchy);
            Assert.IsFalse(root.Find("Circle").gameObject.activeSelf);
            Assert.IsFalse(root.Find("EdgeRing").gameObject.activeSelf);
            Assert.AreEqual(expectedLength, rectangle.lossyScale.z, .01f);
            Assert.AreEqual(.5f, rectangle.lossyScale.x, .01f);
            Assert.AreEqual(0f, Vector3.Dot(rectangle.forward, aim.AimDirection.normalized), .01f);
            var expectedCenter = aim.CasterPosition + aim.AimDirection.normalized * aim.AimDistance;
            Assert.AreEqual(expectedCenter.x, rectangle.position.x, .01f);
            Assert.AreEqual(expectedCenter.z, rectangle.position.z, .01f);

            var pillars = wall.GetComponentsInChildren<MeshRenderer>()
                .Where(renderer => renderer.enabled && renderer.name == "pCylinder8").ToArray();
            Assert.AreEqual(2, pillars.Length);
            var span = pillars[1].transform.position - pillars[0].transform.position;
            Assert.AreEqual(expectedLength, Mathf.Abs(Vector3.Dot(span, rectangle.forward)), .02f);
            Assert.AreEqual(0f, Vector3.Dot(span, rectangle.right), .02f);
        }

        private static bool PlaysModelState(string state)
        {
            var stateHash = Animator.StringToHash(state);
            return Object.FindObjectsOfType<Animator>().Any(animator => animator.isInitialized &&
                animator.GetCurrentAnimatorStateInfo(0).shortNameHash == stateHash &&
                animator.GetComponentsInChildren<SkinnedMeshRenderer>().Any(renderer => renderer.enabled));
        }

        private static bool HasVisibleParticles(int id) => Object.FindObjectsOfType<BattleVfxPoolableTag>()
            .Any(tag => tag.VfxId == id && tag.GetComponentsInChildren<ParticleSystem>()
                .Any(particle => particle.particleCount > 0 && particle.GetComponent<ParticleSystemRenderer>()?.enabled == true));

        private static bool IsActiveVfx(int id) => Object.FindObjectsOfType<BattleVfxPoolableTag>().Any(tag => tag.VfxId == id);

        private static SkillButtonView[] Buttons() => Object.FindObjectsOfType<SkillButtonView>()
            .Where(b => b.gameObject.name.StartsWith("Skill", StringComparison.Ordinal)).ToArray();
        private static SkillButtonView Button(int slot) => Buttons().Single(b => b.gameObject.name == "Skill" + slot);
        private static PointerEventData Pointer(SkillButtonView button) => new PointerEventData(EventSystem.current)
        { pointerId = 101, position = RectTransformUtility.WorldToScreenPoint(null, button.transform.position) };
        private static void Click(SkillButtonView button)
        { var pointer = Pointer(button); button.OnPointerDown(pointer); button.OnPointerUp(pointer); }
        private static void Place(ActorEntity actor, Vec3 position)
        { var t = actor.transform.Value; actor.ReplaceTransform(new Transform3(position, t.Rotation, t.Scale)); }
        private static IEnumerator Frames(int count)
        { for (var i = 0; i < count; i++) { RenderReviewFrame(); yield return null; } }
        private static IEnumerator Until(Func<bool> condition, int frames, string operation)
        { for (var i = 0; i < frames; i++) { if (condition()) yield break; RenderReviewFrame(); yield return null; } Assert.Fail(operation + " timed out."); }

        private static void RenderReviewFrame()
        {
            // Batchmode does not repaint a Game view. Burst-only particle
            // systems pause while culled, so a sporadic screenshot alone cannot
            // represent their playback in a continuously rendered player.
            if (Application.isPlaying && Camera.main != null) Camera.main.Render();
        }

        private static void Capture(string directory, string name)
        {
            Assert.AreNotEqual(UnityEngine.Rendering.GraphicsDeviceType.Null, SystemInfo.graphicsDeviceType,
                "HUD appearance capture requires a graphics device; run without -nographics.");
            var camera = Camera.main;
            Assert.IsNotNull(camera);
            var particles = new System.Text.StringBuilder();
            foreach (var particle in Object.FindObjectsOfType<ParticleSystem>())
            {
                var names = new System.Collections.Generic.List<string>();
                for (var t = particle.transform; t != null; t = t.parent) names.Insert(0, t.name);
                particles.AppendLine(string.Join("/", names) + " pos=" + particle.transform.position + " scale=" + particle.transform.lossyScale +
                    " count=" + particle.particleCount + " playing=" + particle.isPlaying + " paused=" + particle.isPaused + " time=" + particle.time +
                    " space=" + particle.main.simulationSpace + " scaling=" + particle.main.scalingMode + " emission=" + particle.emission.enabled +
                    " culling=" + particle.main.cullingMode + " speed=" + particle.main.simulationSpeed);
                var renderer = particle.GetComponent<ParticleSystemRenderer>();
                if (renderer != null)
                    particles.AppendLine("  renderer=" + renderer.enabled + " mode=" + renderer.renderMode + " alignment=" + renderer.alignment +
                        " bounds=" + renderer.bounds + " material=" + renderer.sharedMaterial?.name + " mesh=" + renderer.mesh?.name);
                var live = new ParticleSystem.Particle[Math.Min(1, particle.particleCount)];
                if (live.Length > 0 && particle.GetParticles(live) > 0)
                    particles.AppendLine("  first position=" + live[0].position + " size=" + live[0].GetCurrentSize3D(particle) +
                        " color=" + live[0].GetCurrentColor(particle) + " rotation=" + live[0].rotation3D);
            }
            File.WriteAllText(Path.Combine(directory, name + "-particles.txt"), particles.ToString());
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
