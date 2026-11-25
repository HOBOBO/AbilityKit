using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Demo.Moba.Share;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Protocol.Moba;
using AbilityKit.Game.Test.UnitTest;
using AbilityKit.Game.Flow;
using AbilityKit.Protocol.Moba.StateSync;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using Dispatcher = AbilityKit.Core.Snapshots.Routing.FrameSnapshotDispatcher;

namespace AbilityKit.Game.Tests
{
    public sealed class MobaOverheadHudTests
    {
        private sealed class Actors : IBattleHudActorPositionResolver
        {
            public readonly Dictionary<int, Vector3> Positions = new Dictionary<int, Vector3>();
            public bool TryGetActorWorldPos(int actorId, out Vector3 pos) => Positions.TryGetValue(actorId, out pos);
        }

        private sealed class Hud : IDisposable
        {
            public readonly GameObject Root = new GameObject("OverheadTest", typeof(RectTransform), typeof(Canvas));
            public readonly GameObject CameraRoot = new GameObject("OverheadCamera", typeof(Camera));
            public readonly Actors Actors = new Actors();
            public readonly BattleHudHpBarController Controller;
            public readonly Dispatcher Dispatcher = new Dispatcher();
            public RectTransform Rect => (RectTransform)Root.transform;
            public Camera Camera => CameraRoot.GetComponent<Camera>();
            public Hud(bool fallback = false)
            {
                Camera.transform.position = new Vector3(0, 0, -10);
                Camera.orthographic = true; Camera.orthographicSize = 8;
                Camera.clearFlags = CameraClearFlags.SolidColor; Camera.backgroundColor = new Color(.035f, .05f, .08f);
                var canvas = Root.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = Camera; canvas.planeDistance = 1;
                var config = new BattleHudConfig(); if (fallback) config.HpBarPrefabPath = "";
                Controller = new BattleHudHpBarController(config, Rect, new BattleHudCanvasProjector(Rect, Camera), Actors);
                ActorIndicatorSnapshotRoute.RegisterDecoder(Dispatcher);
                Dispatcher.Subscribe<ActorIndicatorStateData[]>(ActorIndicatorSnapshotRoute.OpCode,
                    (envelope, rows) => Controller.Apply(((FramePacket)envelope).Frame.Value, rows, 1));
            }
            public void Feed(int frame, params MobaActorIndicatorSnapshotEntry[] rows) => Dispatcher.Feed(
                new FramePacket(new WorldId("overhead-ui"), new FrameIndex(frame), Array.Empty<PlayerInputCommand>(),
                    new WorldStateSnapshot(ActorIndicatorSnapshotRoute.OpCode, MobaActorIndicatorSnapshotCodec.Serialize(rows))));
            public BattleHudOverheadView View(int actor) => Root.GetComponentsInChildren<BattleHudOverheadView>(true)[actor - 1];
            public void Dispose() { Dispatcher.Dispose(); Controller.Clear(); Object.DestroyImmediate(Root); Object.DestroyImmediate(CameraRoot); }
        }

        private static MobaActorIndicatorSnapshotEntry Row(int actor, int layout, int id, float value, float max = 0, bool ready = false, int availability = 1) =>
            new MobaActorIndicatorSnapshotEntry { ActorId = actor, LayoutId = layout, IndicatorId = id,
                Current = value, Max = max, HasMax = max > 0, Ready = ready, Availability = availability };

        /// <summary>
        /// 正式输入耗蓝后由世界系统回复，经实际快照编解码与分发更新正式蓝条。
        /// </summary>
        [Test]
        public void WorldManaRegenerationAfterSkillInputUpdatesPublishedManaBar()
        {
            using var source = MobaSkillConfigTestHarness.CreateForSinglePlayer(new[] { 30070201 },
                worldId: "mana_regeneration_hud", heroId: 3007, attributeTemplateId: 3007);
            source.EnterGameAndWarmup();
            var id = source.AssertPlayerActorBound();
            var actor = source.AssertActorEntity(id);
            if (actor.hasActorBrain) actor.RemoveActorBrain();
            var services = source.World.Services;
            var next = new FrameIndex(source.FrameTime.Frame.Value + 1);
            var input = new SkillInputEvent(1, SkillInputPhase.Press, predictionKey: 1,
                targetFrame: next.Value, inputSequence: 1,
                entityVersion: services.Resolve<MobaActorRegistry>().GetEntityVersion(id));
            Assert.IsTrue(services.Resolve<IMobaInputCoordinator>().TrySubmit(next, new[]
            {
                new PlayerInputCommand(next, source.PlayerId, MobaOpCodes.Input.SkillInput, SkillInputCodec.Serialize(in input))
            }).Succeeded);
            source.Tick(2);
            using var hud = new Hud();
            hud.Actors.Positions[id] = Vector3.zero;
            var spent = Project();
            Assert.Less(spent.Current, spent.Max);
            var view = hud.Root.GetComponentsInChildren<BattleHudOverheadView>(true).Single();
            var fill = view.RowsRoot.GetChild(0).GetComponentInChildren<Image>().transform.GetChild(0).GetComponent<Image>();
            Assert.AreEqual(spent.Current / spent.Max, fill.fillAmount, .00001f);
            source.Tick(150);
            var regenerated = Project();
            Assert.Greater(regenerated.Current, spent.Current);
            Assert.AreEqual(regenerated.Current / regenerated.Max, fill.fillAmount, .00001f);

            MobaActorIndicatorSnapshotEntry Project()
            {
                Assert.IsTrue(services.Resolve<MobaActorIndicatorSnapshotService>().TryGetActiveBaseline(out var packet));
                var rows = MobaActorIndicatorSnapshotCodec.Deserialize(packet.Payload);
                hud.Feed(source.FrameTime.Frame.Value, rows);
                hud.Controller.Tick();
                return rows.Single(row => row.ActorId == id && row.IndicatorId == 2);
            }
        }

        [TestCase(3, 10)] [TestCase(2, 0)] [TestCase(4, 12)] [TestCase(5, 13)]
        public void Published_prefab_consumes_full_wire_state_without_damage_events(int layout, int extra)
        {
            using (var h = new Hud())
            {
                h.Actors.Positions[1] = Vector3.zero;
                var rows = new List<MobaActorIndicatorSnapshotEntry> { Row(1, layout, 1, 700, 2000), Row(1, layout, 2, 120, 600) };
                if (extra > 0) rows.Add(Row(1, layout, extra, extra == 13 ? 125 : 1, extra == 13 ? 0 : 4, extra == 10));
                h.Feed(10, rows.ToArray()); h.Controller.Tick();
                var view = h.View(1);
                Assert.IsTrue(view.IsValid); Assert.IsTrue(view.gameObject.activeSelf);
                Assert.AreEqual(.35f, view.HealthFill.fillAmount, .0001f);
                Assert.AreEqual("700 / 2000", view.ValueText.text);
                Assert.AreEqual(.2f, view.RowsRoot.GetChild(0).GetComponentInChildren<Image>().transform.GetChild(0).GetComponent<Image>().fillAmount, .0001f);
                if (extra == 13) StringAssert.Contains("125", view.RowsRoot.GetChild(1).GetComponentInChildren<Text>().text);
                if (extra == 10) Assert.AreEqual("◆", view.RowsRoot.GetChild(1).GetComponentInChildren<Text>().text);
                h.Feed(9, Row(1, layout, 1, 2000, 2000));
                Assert.AreEqual(.35f, view.HealthFill.fillAmount, .0001f);
                h.Feed(11); Assert.IsFalse(view.gameObject.activeSelf);
            }
        }

        [Test]
        public void Shield_late_panel_overflow_clear_and_old_frames_keep_unique_current_state()
        {
            using (var h = new Hud())
            {
                h.Actors.Positions[1] = Vector3.zero;
                h.Controller.UpdateShields(10, new[] { new ShieldStateData(1, 2500, 3000, 2500, 0, 1, false) });
                h.Feed(10, Row(1, 2, 1, 500, 2000));
                var view = h.View(1);
                Assert.AreEqual(.25f, view.HealthFill.fillAmount);
                Assert.AreEqual(1, view.ShieldFill.fillAmount); Assert.AreEqual("+500", view.ShieldValue.text);
                h.Controller.UpdateShields(11, Array.Empty<ShieldStateData>());
                Assert.IsFalse(view.ShieldFill.gameObject.activeSelf); Assert.IsFalse(view.ShieldValue.gameObject.activeSelf);
                Assert.AreEqual(0, view.ShieldFill.fillAmount);
                h.Controller.UpdateShields(10, new[] { new ShieldStateData(1, 100, 100, 100, 0, 1, false) });
                Assert.IsFalse(view.ShieldFill.gameObject.activeSelf);
            }
        }

        [Test]
        public void Local_counts_death_restore_projection_and_pool_reuse_do_not_leak_children()
        {
            using (var h = new Hud(true))
            {
                h.Actors.Positions[1] = h.Actors.Positions[2] = Vector3.zero;
                h.Feed(10, Row(1, 4, 1, 400, 1000), Row(1, 4, 12, 4, 4, true), Row(2, 4, 1, 400, 1000), Row(2, 4, 12, 4, 4, true));
                var view = h.View(1); var other = h.View(2);
                Assert.IsTrue(view.RowsRoot.GetChild(1).gameObject.activeSelf);
                Assert.IsFalse(other.RowsRoot.GetChild(1).gameObject.activeSelf);
                var cast = BattleHudOverheadView.Rect("old-cast", view.CastSlot, Vector2.one);
                var buff = BattleHudOverheadView.Rect("old-buff", view.BuffSlot, Vector2.one);
                h.Feed(11, Row(1, 4, 1, 0, 1000)); h.Controller.Tick();
                Assert.IsFalse(view.gameObject.activeSelf);
                h.Controller.ResetBaseline();
                h.Feed(3, Row(1, 4, 1, 300, 1000), Row(1, 4, 12, 2, 4)); h.Controller.Tick();
                Assert.IsTrue(view.gameObject.activeSelf); Assert.AreEqual(.3f, view.HealthFill.fillAmount);
                Assert.AreEqual(0, view.CastSlot.childCount); Assert.AreEqual(0, view.BuffSlot.childCount);
                Assert.IsFalse(cast.gameObject.activeSelf); Assert.IsFalse(buff.gameObject.activeSelf);
                h.Actors.Positions[1] = new Vector3(0, 0, -30); h.Controller.Tick(); Assert.IsFalse(view.gameObject.activeSelf);
                h.Controller.RemoveActor(1); h.Feed(4, Row(1, 4, 1, 1000, 1000));
                Assert.IsNull(h.Controller.PanelSlot(1, true));
            }
        }

        [Test]
        public void Seven_hero_layouts_render_and_stable_frames_reuse_objects()
        {
            using (var h = new Hud())
            {
                var texture = new RenderTexture(800, 460, 24);
                h.Camera.targetTexture = texture;
                try
                {
                    var rows = new List<MobaActorIndicatorSnapshotEntry>();
                    var layouts = new[] { 3, 2, 4, 5, 2, 2, 2 };
                    var names = new[] { "Jhin", "Jinx", "Annie", "Veigar", "Gragas", "Karthus", "Lux" };
                    for (var i = 0; i < layouts.Length; i++)
                    {
                        h.Actors.Positions[i + 1] = Vector3.zero;
                        rows.Add(Row(i + 1, layouts[i], 1, 500 + i * 50, 1500));
                        rows.Add(Row(i + 1, layouts[i], 2, 100 + i * 20, 500));
                        var extra = i == 0 ? 10 : i == 2 ? 12 : i == 3 ? 13 : 0;
                        if (extra > 0) rows.Add(Row(i + 1, layouts[i], extra, extra == 13 ? 128 : 4, extra == 13 ? 0 : 4, i == 2));
                    }
                    h.Feed(1, rows.ToArray());
                    h.Controller.UpdateShields(new[] { new ShieldStateData(1, 450, 450, 450, 0, 1, false),
                        new ShieldStateData(3, 1800, 1800, 1800, 0, 1, false) });
                    var decoded = ActorIndicatorSnapshotDecoder.Decode(MobaActorIndicatorSnapshotCodec.Serialize(rows.ToArray()));
                    for (var i = 0; i < layouts.Length; i++) h.Controller.Apply(1, decoded, i + 1);
                    /* 联系表展示每个英雄作为本地角色时的私有行；正常显隐由独立用例断言。 */
                    for (var i = 0; i < layouts.Length; i++)
                    {
                        var view = h.View(i + 1); view.gameObject.SetActive(true);
                        ((RectTransform)view.transform).anchoredPosition = new Vector2((i % 3 - 1) * 245, 140 - i / 3 * 140);
                        var name = BattleHudOverheadView.Text("HeroLabel", view.transform, 16);
                        name.text = names[i]; name.rectTransform.anchoredPosition = new Vector2(0, 42);
                        if (i == 0 || i == 2 || i == 3) view.RowsRoot.GetChild(1).gameObject.SetActive(true);
                    }
                    Canvas.ForceUpdateCanvases(); h.Camera.Render();
                    var directory = EvidenceDirectory();
                    if (directory != null)
                    {
                        Directory.CreateDirectory(directory);
                        var old = RenderTexture.active; RenderTexture.active = texture;
                        var png = new Texture2D(800, 460, TextureFormat.RGB24, false);
                        png.ReadPixels(new Rect(0, 0, 800, 460), 0, 0); png.Apply();
                        File.WriteAllBytes(Path.Combine(directory, "seven-hero-overhead.png"), png.EncodeToPNG());
                        RenderTexture.active = old; Object.DestroyImmediate(png);
                    }
                    var count = h.Root.GetComponentsInChildren<Transform>(true).Length;
                    var timer = Stopwatch.StartNew();
                    for (var frame = 3; frame < 303; frame++) h.Controller.Apply(frame, decoded, 1);
                    timer.Stop();
                    Assert.AreEqual(count, h.Root.GetComponentsInChildren<Transform>(true).Length);
                    TestContext.WriteLine("7 actors, 300 HUD refreshes: " + timer.Elapsed.TotalMilliseconds + " ms; transforms=" + count);
                }
                finally { h.Camera.targetTexture = null; Object.DestroyImmediate(texture); }
            }
        }

        private static string EvidenceDirectory()
        {
            var args = Environment.GetCommandLineArgs();
            for (var i = 0; i + 1 < args.Length; i++) if (args[i] == "-overheadEvidenceDir") return args[i + 1];
            return null;
        }

        [Test]
        public void Spawn_generations_filter_old_state_and_reuse_the_matching_layout()
        {
            using (var h = new Hud(true))
            {
                h.Actors.Positions[1] = Vector3.zero;
                ActorSpawnData Spawn(int version) => new ActorSpawnData(1, 3003, 3003, "actor", 0, 0, 0, 0, 1, 1, 1000, 1000, entityVersion: version);
                h.Controller.ObserveSpawns(10, new[] { Spawn(1) });
                h.Feed(10, Row(1, 4, 1, 500, 1000));
                var first = h.Controller.PanelSlot(1, true).parent;
                h.Controller.RemoveActor(1);
                h.Feed(11, Row(1, 4, 1, 1000, 1000)); Assert.IsNull(h.Controller.PanelSlot(1, true));
                h.Controller.ObserveSpawns(20, new[] { Spawn(2) });
                h.Feed(19, Row(1, 4, 1, 1000, 1000)); Assert.IsNull(h.Controller.PanelSlot(1, true));
                h.Feed(20, Row(1, 4, 1, 200, 1000));
                Assert.AreSame(first, h.Controller.PanelSlot(1, true).parent);
                Assert.IsFalse(h.Controller.AcceptDespawn(19, 1));
                Assert.AreEqual(.2f, h.View(1).HealthFill.fillAmount);
            }
        }

        [Test]
        public void Many_actor_steady_refresh_keeps_objects_stable_and_records_allocation_cost()
        {
            using (var h = new Hud(true))
            {
                var states = new ActorIndicatorStateData[256];
                for (var actor = 1; actor <= 128; actor++)
                {
                    h.Actors.Positions[actor] = Vector3.zero;
                    states[(actor - 1) * 2] = new ActorIndicatorStateData(actor, 2, 1, 500, 1000, true, false, 1);
                    states[(actor - 1) * 2 + 1] = new ActorIndicatorStateData(actor, 2, 2, 100, 500, true, false, 1);
                }
                for (var frame = 0; frame < 5; frame++) h.Controller.Apply(frame, states, 1);
                var count = h.Root.GetComponentsInChildren<Transform>(true).Length;
                var timer = Stopwatch.StartNew();
                var before = GC.GetAllocatedBytesForCurrentThread();
                for (var frame = 5; frame < 305; frame++) h.Controller.Apply(frame, states, 1);
                var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
                timer.Stop();
                Assert.AreEqual(count, h.Root.GetComponentsInChildren<Transform>(true).Length);
                TestContext.WriteLine("128 actors, 300 HUD refreshes: " + timer.Elapsed.TotalMilliseconds + " ms; allocated=" + allocated + " bytes; transforms=" + count);
            }
        }
    }
}
