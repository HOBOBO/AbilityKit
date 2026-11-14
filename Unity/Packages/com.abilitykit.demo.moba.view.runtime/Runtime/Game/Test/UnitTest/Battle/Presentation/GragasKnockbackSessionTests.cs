using System;
using System.Collections;
using System.Linq;
using AbilityKit.Ability.Host;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba;
using AbilityKit.Demo.Moba.Attributes;
using AbilityKit.Demo.Moba.Components;
using AbilityKit.Demo.Moba.Gameplay;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Game.Battle.View.Lib.Skill;
using AbilityKit.Game.Flow;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;

namespace AbilityKit.Game.Test.UnitTest
{
    public sealed class GragasKnockbackSessionTests
    {
        [UnityTest]
        public IEnumerator Local_session_E_and_R_move_respawned_enemy_logic_and_rendered_shell()
        {
            DemoGameplayTestLauncher.OpenMobaLocalForPlay();
            yield return new EnterPlayMode();
            Time.captureDeltaTime = 1f / 60f;
            yield return Review();
        }

        private static IEnumerator Review()
        {
            yield return Until(() => GameEntry.IsInitialized, 1800, "startup");
            var entry = GameEntry.Instance;
            var flow = entry.Get<GameFlowDomain>();
            yield return Until(() => flow.CurrentBattlePhase == MobaBattleState.InMatch, 18000, "InMatch");
            Assert.IsTrue(entry.TryGet(out BattleContext ctx));
            var plan = ctx.Plan;
            var player = BattleInputSessionIdentity.ResolvePlayerId((IBattleInputSessionIdentityPort)ctx);
            yield return Until(() => Buttons().Length == 5, 360, "HUD");
            var previous = Button(1).PresentationName;
            var replace = BattleInputCommandFactory.CreateDebugReplaceHero(
                SessionSimRuntimeTuning.ResolveInputSubmitFrame(ctx.LastFrame, in plan), player, 3005);
            Assert.IsTrue(new BattleInputSubmitter(ctx.InputRuntime, player,
                BattleInputSessionIdentity.ResolveWorldId(in plan)).Submit(in replace));
            yield return Until(() => Buttons().Length == 5 && Button(1).PresentationName != previous, 360, "Gragas HUD");
            Assert.IsTrue(ctx.Session.TryGetWorld(out var world));
            var actors = world.Services.Resolve<MobaActorLookupService>();
            Assert.IsTrue(actors.TryGetActorEntity(ctx.LocalActorId, out var caster));
            Assert.IsTrue(world.Services.Resolve<MobaPlayerActorMapService>().TryGetActorId(new PlayerId("p2"), out var target));
            Assert.IsTrue(actors.TryGetActorEntity(target, out var enemy));
            if (enemy.hasActorBrain) enemy.RemoveActorBrain();
            // Exercise the production death subscription with a nonhero killer, then wait
            // for the normal respawn clock before using the real skill buttons.
            var casterType = caster.unitSubType.Value;
            caster.ReplaceUnitSubType(UnitSubType.None);
            try
            {
                var lethal = new AttackInfo
                {
                    AttackerActorId = caster.actorId.Value,
                    TargetActorId = target,
                    DamageType = DamageType.Physical,
                    CritType = CritType.None,
                    ReasonKind = DamageReasonKind.Environment
                };
                lethal.BaseDamage.BaseValue = new MobaAttrs(enemy).Hp * 10f + 1f;
                world.Services.Resolve<DamagePipelineService>().Execute(lethal);
            }
            finally { caster.ReplaceUnitSubType(casterType); }
            Assert.LessOrEqual(new MobaAttrs(enemy).Hp, 0f, "Nonhero kill must enter death");
            var respawns = world.Services.Resolve<MobaGameplayRespawnService>();
            Assert.IsTrue(respawns.CapturePending().Any(r => r.ActorId == target),
                "Rejected scoring must not suppress respawn");
            yield return Until(() => new MobaAttrs(enemy).Hp > 0f, 600, "automatic hero respawn");
            Assert.IsFalse(respawns.CapturePending().Any(r => r.ActorId == target));
            if (enemy.hasActorBrain) enemy.RemoveActorBrain();
            var enemyAttrs = new MobaAttrs(enemy);
            enemyAttrs.Hp = 5000;
            Place(caster, new Vec3(1, 0, -7));
            Place(enemy, new Vec3(1, 0, -4));
            yield return Frames(20);
            yield return Cast(3, ctx, enemy, 1f);
            yield return Frames(90);
            Place(caster, new Vec3(1, 0, -7));
            Place(enemy, new Vec3(1, 0, -1));
            yield return Frames(20);
            yield return Cast(4, ctx, enemy, 6f);
        }

        private static IEnumerator Cast(int slot, BattleContext ctx, ActorEntity enemy, float minimum)
        {
            var input = GameEntry.Instance.Get<BattleInputFeature>();
            var before = input.SkillSubmitSuccessCount;
            var button = Button(slot);
            var pointer = new PointerEventData(EventSystem.current)
            { pointerId = 101, position = RectTransformUtility.WorldToScreenPoint(null, button.transform.position) };
            button.OnPointerDown(pointer);
            yield return Frames(30);
            pointer.position += new Vector2(0, slot == 3 ? 120 : 100);
            button.OnDrag(pointer);
            yield return Frames(3);
            Assert.IsTrue(new BattleHudAimPreviewPositionResolver().TryResolve(ctx, out var aim));
            var destination = slot == 3 ? aim.CasterPosition + aim.AimDirection * 3 :
                aim.CasterPosition + aim.AimDirection * aim.AimDistance;
            Place(enemy, new Vec3(destination.x, destination.y, destination.z));
            var origin = enemy.transform.Value.Position;
            var hp = new MobaAttrs(enemy).Hp;
            button.OnPointerUp(pointer);
            yield return Until(() => input.SkillSubmitSuccessCount == before + 1, 60, "HUD submit " + slot);
            yield return Until(() => new MobaAttrs(enemy).Hp < hp, 180, "impact " + slot);
            yield return Frames(45);
            var delta = enemy.transform.Value.Position - origin;
            Assert.Greater(delta.Magnitude, minimum - .15f, "Enemy logic displacement, slot " + slot);
            Assert.IsTrue(BattleViewActorTransformRegistry.TryGet(enemy.actorId.Value, out var visual));
            Assert.Less(Vector3.Distance(visual, new Vector3(enemy.transform.Value.Position.X,
                enemy.transform.Value.Position.Y, enemy.transform.Value.Position.Z)), .25f, "Rendered shell must follow knockback");
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            Time.captureDeltaTime = 0;
            if (Application.isPlaying) yield return new ExitPlayMode();
            DemoGameplayTestLauncher.Clear();
        }
        private static SkillButtonView[] Buttons() => UnityEngine.Object.FindObjectsOfType<SkillButtonView>()
            .Where(b => b.gameObject.name.StartsWith("Skill", StringComparison.Ordinal)).ToArray();
        private static SkillButtonView Button(int slot) => Buttons().Single(b => b.gameObject.name == "Skill" + slot);
        private static void Place(ActorEntity actor, Vec3 position)
        { var t = actor.transform.Value; actor.ReplaceTransform(new Transform3(position, t.Rotation, t.Scale)); }
        private static IEnumerator Frames(int count)
        { for (var i = 0; i < count; i++) yield return null; }
        private static IEnumerator Until(Func<bool> condition, int frames, string operation)
        { for (var i = 0; i < frames; i++) { if (condition()) yield break; yield return null; } Assert.Fail(operation + " timed out"); }
    }
}
