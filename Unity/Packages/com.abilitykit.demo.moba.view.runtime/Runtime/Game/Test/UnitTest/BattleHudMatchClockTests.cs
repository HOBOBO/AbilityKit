using System;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Demo.Moba.Gameplay;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Share;
using AbilityKit.Game.Flow;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using FrameSnapshotDispatcher = AbilityKit.Core.Snapshots.Routing.FrameSnapshotDispatcher;

namespace AbilityKit.Game.Test.UnitTest
{
    public sealed class BattleHudMatchClockTests
    {
        [Test]
        public void HudSnapshotController_RebindsWhenDispatcherChanges()
        {
            var context = BattleContext.Rent();
            var first = new FrameSnapshotDispatcher();
            var second = new FrameSnapshotDispatcher();
            GameplayClockSnapshotRoute.RegisterDecoder(first);
            GameplayClockSnapshotRoute.RegisterDecoder(second);
            var controller = new BattleHudSnapshotController();
            var received = 0;
            try
            {
                var generation = context.BindSnapshotRouting(first, null, null);
                Assert.IsTrue(controller.Bind(context, null, null, null,
                    gameplayClockReceived: _ => received++));
                Assert.IsTrue(controller.IsBoundTo(context));
                Feed(first, 1);
                Assert.AreEqual(1, received);

                Assert.IsTrue(context.ClearSnapshotRouting(generation, first));
                Assert.IsFalse(controller.IsBoundTo(context));
                Assert.IsFalse(controller.Bind(context, null, null, null,
                    gameplayClockReceived: _ => received++));
                Feed(first, 2);
                Assert.AreEqual(1, received);

                generation = context.BindSnapshotRouting(second, null, null);
                Assert.IsTrue(controller.Bind(context, null, null, null,
                    gameplayClockReceived: _ => received++));
                Assert.IsTrue(controller.IsBoundTo(context));
                Feed(second, 3);
                Feed(first, 4);
                Assert.AreEqual(2, received);
                context.ClearSnapshotRouting(generation, second);
            }
            finally
            {
                controller.Dispose();
                BattleContext.Return(context);
                first.Dispose();
                second.Dispose();
            }

            void Feed(FrameSnapshotDispatcher dispatcher, int frame)
            {
                var clock = new GameplayClockData(1, GameplayClockPhase.Running, 90000,
                    90000 - frame * 1000, "match", frame);
                dispatcher.Feed(new FramePacket(new WorldId("hud-route"), new FrameIndex(frame),
                    Array.Empty<PlayerInputCommand>(), new WorldStateSnapshot(
                        GameplayClockSnapshotRoute.OpCode, GameplayClockSnapshotCodec.Encode(in clock))));
            }
        }

        [Test]
        public void Hud_count_up_clock_displays_elapsed_time_from_zero()
        {
            var root = new GameObject("HudTestRoot", typeof(RectTransform));
            var clock = new BattleHudMatchClock();
            try
            {
                clock.Bind((RectTransform)root.transform);
                var data = new GameplayClockData(1, GameplayClockPhase.Running, 0, 0,
                    elapsedMilliseconds: 12500);
                clock.Apply(in data);

                var label = root.GetComponentInChildren<Text>(true);
                Assert.IsTrue(label.transform.parent.gameObject.activeSelf);
                Assert.AreEqual("00:12", label.text);
            }
            finally
            {
                clock.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void Hud_count_down_clock_keeps_displaying_remaining_time()
        {
            var root = new GameObject("HudTestRoot", typeof(RectTransform));
            var clock = new BattleHudMatchClock();
            try
            {
                clock.Bind((RectTransform)root.transform);
                var data = new GameplayClockData(2, GameplayClockPhase.Running, 60000, 59500,
                    elapsedMilliseconds: 500);
                clock.Apply(in data);

                var label = root.GetComponentInChildren<Text>(true);
                Assert.IsTrue(label.transform.parent.gameObject.activeSelf);
                Assert.AreEqual("01:00", label.text);
            }
            finally
            {
                clock.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void Hud_clock_receives_snapshots_without_a_logic_world()
        {
            var hero = new HeroSkillContract("Daji", 1005, 1005,
                new[] { 10050101, 10050201, 10050301 });
            using (var harness = HeroSkillHeadlessContract.CreateHarness(hero, "moba_hud_clock_world"))
            {
                harness.EnterGameAndWarmup(reason: "HUD match clock acceptance");
                var gameplay = harness.World.Services.Resolve<MobaGameplayService>();
                var emitter = new MobaGameplayClockSnapshotService(gameplay);
                var context = BattleContext.Rent();
                var snapshots = new FrameSnapshotDispatcher();
                var generation = context.BindSnapshotRouting(snapshots, null, null);
                GameplayClockSnapshotRoute.RegisterDecoder(snapshots);
                var root = new GameObject("HudTestRoot", typeof(RectTransform));
                var clock = new BattleHudMatchClock();
                var controller = new BattleHudSnapshotController();
                try
                {
                    Assert.IsFalse(context.TryGetRuntimeWorld(out _));
                    clock.Bind((RectTransform)root.transform);
                    Assert.IsTrue(controller.Bind(context, null, null, null, null,
                        state => clock.Apply(state)));

                    var label = root.GetComponentInChildren<Text>(true);
                    Assert.IsNotNull(label);
                    Assert.IsFalse(label.transform.parent.gameObject.activeSelf);

                    Feed(1);
                    Assert.IsTrue(label.transform.parent.gameObject.activeSelf);
                    Assert.AreEqual(Format(gameplay.GetClockState().ElapsedSeconds), label.text);

                    var before = label.text;
                    gameplay.Tick(1f);
                    Feed(2);
                    Assert.AreNotEqual(before, label.text);
                    Assert.AreEqual(Format(gameplay.GetClockState().ElapsedSeconds), label.text);

                    Assert.AreEqual(0, gameplay.CurrentGameplay.DefaultDurationMs);
                    Assert.IsTrue(gameplay.End("test", 0));
                    Feed(3);
                    Assert.AreEqual(MobaGameplayPhase.Ended, gameplay.Phase);
                    Assert.AreEqual(Format(gameplay.GetClockState().ElapsedSeconds), label.text);

                    gameplay.Reset();
                    Feed(4);
                    Assert.IsFalse(label.transform.parent.gameObject.activeSelf);
                }
                finally
                {
                    controller.Dispose();
                    clock.Dispose();
                    context.ClearSnapshotRouting(generation, snapshots);
                    BattleContext.Return(context);
                    snapshots.Dispose();
                    emitter.Dispose();
                    UnityEngine.Object.DestroyImmediate(root);
                }

                void Feed(int frame)
                {
                    Assert.IsTrue(emitter.TryGetSnapshot(new FrameIndex(frame), out var snapshot));
                    snapshots.Feed(new FramePacket(new WorldId("hud_clock"),
                        new FrameIndex(frame), Array.Empty<PlayerInputCommand>(), snapshot));
                }
            }
        }

        private static string Format(int remainingMilliseconds)
        {
            var seconds = remainingMilliseconds / 1000 +
                (remainingMilliseconds % 1000 == 0 ? 0 : 1);
            return BattleHudMatchClock.FormatTime(seconds);
        }

        private static string Format(float elapsedSeconds) =>
            BattleHudMatchClock.FormatTime((int)Math.Max(0, elapsedSeconds));

        [Test]
        public void Hud_match_state_receives_score_and_result_without_a_logic_world()
        {
            var hero = new HeroSkillContract("Daji", 1005, 1005,
                new[] { 10050101, 10050201, 10050301 });
            using (var harness = HeroSkillHeadlessContract.CreateHarness(hero, "moba_hud_match_world"))
            {
                harness.EnterGameAndWarmup(reason: "HUD match state acceptance");
                var gameplay = harness.World.Services.Resolve<MobaGameplayService>();
                var variables = harness.World.Services.Resolve<MobaGameplayVariableService>();
                var emitter = new MobaGameplayMatchSnapshotService(gameplay, variables);
                var context = BattleContext.Rent();
                var snapshots = new FrameSnapshotDispatcher();
                var generation = context.BindSnapshotRouting(snapshots, null, null);
                GameplayMatchSnapshotRoute.RegisterDecoder(snapshots);
                var root = new GameObject("HudTestRoot", typeof(RectTransform));
                var match = new BattleHudMatchState();
                var controller = new BattleHudSnapshotController();
                try
                {
                    Assert.IsFalse(context.TryGetRuntimeWorld(out _));
                    match.Bind((RectTransform)root.transform);
                    Assert.IsTrue(controller.Bind(context, null, null, null,
                        (Action<PresentationCueData[]>)null,
                        gameplayMatchReceived: state => match.Apply(state)));
                    var score = root.transform.Find("MatchState/Score").GetComponent<Text>();
                    var outcome = root.transform.Find("MatchState/Result/Outcome").GetComponent<Text>();

                    Feed(1);
                    Assert.AreEqual("0 : 0", score.text);
                    Assert.IsFalse(outcome.transform.parent.gameObject.activeSelf);

                    variables.Set(GameplayScoreKeys.Team1, 2d);
                    Feed(2);
                    Assert.AreEqual("2 : 0", score.text);

                    Assert.IsTrue(gameplay.End("score_limit", 1));
                    Feed(3);
                    Assert.IsTrue(outcome.transform.parent.gameObject.activeSelf);
                    Assert.AreEqual("队伍 1 获胜", outcome.text);

                    gameplay.Reset();
                    Feed(4);
                    Assert.IsFalse(score.transform.parent.gameObject.activeSelf);
                }
                finally
                {
                    controller.Dispose();
                    match.Dispose();
                    context.ClearSnapshotRouting(generation, snapshots);
                    BattleContext.Return(context);
                    snapshots.Dispose();
                    emitter.Dispose();
                    UnityEngine.Object.DestroyImmediate(root);
                }

                void Feed(int frame)
                {
                    Assert.IsTrue(emitter.TryGetSnapshot(new FrameIndex(frame), out var snapshot));
                    snapshots.Feed(new FramePacket(new WorldId("hud_match"),
                        new FrameIndex(frame), Array.Empty<PlayerInputCommand>(), snapshot));
                }
            }
        }
    }
}
