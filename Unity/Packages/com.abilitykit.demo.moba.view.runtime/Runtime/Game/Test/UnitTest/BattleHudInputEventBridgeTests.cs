using System.Collections.Generic;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Game.Battle;
using AbilityKit.Game.Battle.Component;
using AbilityKit.Game.Battle.Requests;
using AbilityKit.Game.Battle.Entity;
using AbilityKit.Game.Battle.View;
using AbilityKit.Game.Battle.View.Lib.Skill;
using AbilityKit.Game.Flow;
using AbilityKit.World.ECS;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;

namespace AbilityKit.Game.Test.UnitTest
{
    public sealed class BattleHudInputEventBridgeTests
    {
        [Test]
        public void Dispatcher_ForwardsMoveAndSkillEventsToSink()
        {
            var sink = new RecordingHudInputSink();
            var dispatcher = new BattleHudInputEventDispatcher(sink);

            dispatcher.OnMoveBegin();
            dispatcher.OnMoveDxDzChanged(0.25f, -0.5f);
            dispatcher.OnMoveEnd();
            dispatcher.OnSkillClick(2);
            dispatcher.OnSkillAimStart(3, new Vector2(0.1f, 0.2f));
            dispatcher.OnSkillAimUpdate(3, new Vector2(0.3f, 0.4f));
            dispatcher.OnSkillAimEnd(3, new Vector2(0.5f, 0.6f));
            dispatcher.ResetHudAim();

            Assert.AreEqual(1, sink.BeginMoveCount);
            Assert.AreEqual(1, sink.EndMoveCount);
            Assert.AreEqual(0.25f, sink.MoveDx);
            Assert.AreEqual(-0.5f, sink.MoveDz);
            Assert.AreEqual(2, sink.ClickedSlot);
            Assert.AreEqual(3, sink.ActiveAimSlot);
            Assert.AreEqual(0.3f, sink.ActiveAimDx);
            Assert.AreEqual(0.4f, sink.ActiveAimDz);
            Assert.IsTrue(sink.ActiveAimAiming);
            Assert.AreEqual(3, sink.SubmittedAimSlot);
            Assert.AreEqual(0.5f, sink.SubmittedAimDx);
            Assert.AreEqual(0.6f, sink.SubmittedAimDz);
            Assert.AreEqual(0, sink.ResetAimSlot);
            Assert.IsFalse(sink.ResetAimAiming);
        }

        [Test]
        public void Dispatcher_ScalesTargetCircleAimBySkillRange()
        {
            var sink = new RecordingHudInputSink();
            var dispatcher = new BattleHudInputEventDispatcher(sink);
            dispatcher.SetSkillSpecs(new Dictionary<int, BattleHudSkillPresentationSpec>
            {
                [3] = new BattleHudSkillPresentationSpec(
                    10010301,
                    "TargetSkill",
                    BattleHudSkillPreviewShape.TargetCircle,
                    SkillAimIndicatorShape.TargetCircle,
                    10f,
                    6.8f,
                    3.4f,
                    Color.white,
                    aimMode: SkillAimMode.Point,
                    usePointMode: SkillUsePointMode.TargetPoint,
                    faceToAim: false)
            });

            dispatcher.OnSkillAimUpdate(3, new Vector2(0.5f, 0.25f));
            dispatcher.OnSkillAimEnd(3, new Vector2(1f, 0f));

            Assert.AreEqual(3, sink.ActiveAimSlot);
            Assert.AreEqual(5f, sink.ActiveAimDx, 0.0001f);
            Assert.AreEqual(2.5f, sink.ActiveAimDz, 0.0001f);
            Assert.AreEqual(3, sink.SubmittedAimSlot);
            Assert.AreEqual(10f, sink.SubmittedAimDx, 0.0001f);
            Assert.AreEqual(0f, sink.SubmittedAimDz, 0.0001f);
        }

        [Test]
        public void Dispatcher_KeepsDirectionLineAimNormalized()
        {
            var sink = new RecordingHudInputSink();
            var dispatcher = new BattleHudInputEventDispatcher(sink);
            dispatcher.SetSkillSpecs(new Dictionary<int, BattleHudSkillPresentationSpec>
            {
                [1] = new BattleHudSkillPresentationSpec(
                    10010101,
                    "DirectionSkill",
                    BattleHudSkillPreviewShape.DirectionLine,
                    SkillAimIndicatorShape.DirectionLine,
                    7f,
                    1.8f,
                    0f,
                    Color.white)
            });

            dispatcher.OnSkillAimEnd(1, new Vector2(1f, 0f));

            Assert.AreEqual(1, sink.SubmittedAimSlot);
            Assert.AreEqual(1f, sink.SubmittedAimDx, 0.0001f);
            Assert.AreEqual(0f, sink.SubmittedAimDz, 0.0001f);
        }

        [Test]
        public void Bridge_AimSkillPointerDownSetsPreviewAimAndReleaseKeepsSubmittedPreview()
        {
            using (var fixture = BattleHudInputEventBridgeFixture.Create())
            {
                fixture.Bridge.Bind(fixture.InputUi);

                fixture.PointerDown(fixture.Skill2, new Vector2(10f, 10f));

                Assert.AreEqual(2, fixture.Sink.ActiveAimSlot);
                Assert.IsTrue(fixture.Sink.ActiveAimAiming);

                fixture.PointerUp(fixture.Skill2, new Vector2(40f, 10f));

                Assert.AreEqual(2, fixture.Sink.SubmittedAimSlot);
                Assert.AreEqual(-1, fixture.Sink.ResetAimSlot);
                Assert.IsTrue(fixture.Sink.ResetAimAiming);
            }
        }

        [Test]
        public void Bridge_BindsSkillClickToSinkAndUnbindStopsForwarding()
        {
            using (var fixture = BattleHudInputEventBridgeFixture.Create())
            {
                fixture.Bridge.Bind(fixture.InputUi);

                fixture.Click(fixture.Skill1);
                fixture.Bridge.Unbind();
                fixture.Click(fixture.Skill3);

                Assert.AreEqual(1, fixture.Sink.ClickCount);
                Assert.AreEqual(1, fixture.Sink.ClickedSlot);
            }
        }

        [Test]
        public void Bridge_RebindsWithoutDuplicateSkillClickSubscriptions()
        {
            using (var fixture = BattleHudInputEventBridgeFixture.Create())
            {
                fixture.Bridge.Bind(fixture.InputUi);
                fixture.Bridge.Bind(fixture.InputUi);

                fixture.Click(fixture.Skill1);

                Assert.AreEqual(1, fixture.Sink.ClickCount);
                Assert.AreEqual(1, fixture.Sink.ClickedSlot);
            }
        }

        [Test]
        public void AimPreview_ContextStateKeepsMoziSkill2VisibleThroughSubmissionWindow()
        {
            var world = new EntityWorld();
            var lookup = new BattleEntityLookup();
            var actor = world.Create("mozi");
            actor.WithRef(new BattleTransformComponent
            {
                Position = new Vector3(2f, 0f, 3f),
                Forward = Vector3.right,
            });
            lookup.Bind(new BattleNetId(1004), actor);

            var ctx = BattleContext.Rent();
            var preview = new BattleHudAimPreview();
            preview.SetSkillSpecs(new Dictionary<int, BattleHudSkillPresentationSpec>
            {
                [2] = new BattleHudSkillPresentationSpec(
                    10040201,
                    "墨子-机关重炮",
                    BattleHudSkillPreviewShape.DirectionLine,
                    SkillAimIndicatorShape.DirectionLine,
                    12f,
                    1.4f,
                    0f,
                    new Color(0.18f, 0.95f, 0.72f, 0.34f))
            });
            try
            {
                ctx.LocalActorId = 1004;
                ctx.EntityQuery = new BattleEntityQuery(world, lookup);
                ctx.SetHudSkillAim(2, 1f, 0f, aiming: true);

                preview.Tick(ctx, 0.016f);

                Assert.IsNotNull(preview.PreviewRoot);
                Assert.IsTrue(preview.PreviewRoot.activeSelf);
                var casterRingPosition = preview.PreviewRoot.transform.Find("CasterRing").position;
                Assert.AreEqual(2f, casterRingPosition.x, 0.0001f);
                Assert.AreEqual(3f, casterRingPosition.z, 0.0001f);

                ctx.SubmitHudSkillAim(2, 1f, 0f);
                Assert.IsTrue(BattleHudInputSource.TryConsumeSkillAimSubmit(ctx, out var submitted));
                Assert.AreEqual(2, submitted.Slot);

                preview.Tick(ctx, 0.016f);
                Assert.IsTrue(preview.PreviewRoot.activeSelf);

                preview.Tick(ctx, 0.5f);
                Assert.IsFalse(preview.PreviewRoot.activeSelf);

                ctx.SetHudSkillAim(2, 0f, 1f, aiming: true);
                preview.Tick(ctx, 0.016f);
                Assert.IsTrue(preview.PreviewRoot.activeSelf);

                ctx.CancelHudSkillAim();
                preview.Tick(ctx, 0.016f);
                Assert.IsFalse(preview.PreviewRoot.activeSelf);
            }
            finally
            {
                preview.Clear();
                BattleContext.Return(ctx);
            }
        }

        [Test]
        public void AimPreview_ResolvesMappedLocalActorWhenContextActorIdIsMissing()
        {
            var world = new EntityWorld();
            var lookup = new BattleEntityLookup();
            var actor = world.Create("lian-po");
            actor.WithRef(new BattleTransformComponent
            {
                Position = new Vector3(3f, 0f, 5f),
                Forward = Vector3.forward,
            });
            lookup.Bind(new BattleNetId(1001), actor);

            var plan = new TestBattleBootstrapper().Build();
            var worldId = new WorldId("aim-preview-mapped-actor");
            var session = new BattleLogicSession(new BattleLogicSessionOptions
            {
                WorldId = worldId,
                PlayerId = "p1",
                ScanAllLoadedAssemblies = true,
                AutoCreateWorld = false,
                AutoJoin = false,
            });
            var createWorld = plan.CreateWorld;
            var worldOptions = SessionMobaWorldBootstrapFactory.CreateWorldOptions(
                plan,
                worldId,
                registerWorldInitData: false);
            session.CreateWorld(new CreateWorldRequest(
                worldOptions,
                createWorld.OpCode,
                createWorld.Payload));
            var ctx = BattleContext.Rent();
            var preview = new BattleHudAimPreview();
            preview.SetSkillSpecs(new Dictionary<int, BattleHudSkillPresentationSpec>
            {
                [3] = new BattleHudSkillPresentationSpec(
                    10010301,
                    "廉颇-天崩地裂",
                    BattleHudSkillPreviewShape.TargetCircle,
                    SkillAimIndicatorShape.TargetCircle,
                    10f,
                    6.8f,
                    3.4f,
                    new Color(0.95f, 0.68f, 0.18f, 0.3f))
            });

            try
            {
                Assert.IsTrue(session.TryGetWorld(out var logicWorld));
                Assert.IsTrue(logicWorld.Services.TryResolve<MobaPlayerActorMapService>(out var playerActors));
                playerActors.Bind(new PlayerId("p1"), 1001);

                ctx.Session = session;
                ctx.LocalControlPlayerId = "p1";
                ctx.LocalActorId = 0;
                ctx.EntityQuery = new BattleEntityQuery(world, lookup);
                ctx.SetHudSkillAim(3, 2f, 0f, aiming: true);

                preview.Tick(ctx, 0.016f);

                Assert.AreEqual(1001, ctx.LocalActorId);
                Assert.IsNotNull(preview.PreviewRoot);
                Assert.IsTrue(preview.PreviewRoot.activeSelf);
                var circle = preview.PreviewRoot.transform.Find("Circle");
                Assert.IsTrue(circle.gameObject.activeSelf);
                Assert.AreEqual(5f, circle.position.x, 0.0001f);
                Assert.AreEqual(5f, circle.position.z, 0.0001f);
            }
            finally
            {
                preview.Clear();
                BattleContext.Return(ctx);
                session.Dispose();
            }
        }

        [Test]
        public void AimPreview_MissingSkillSpecDoesNotInferShapeFromSlot()
        {
            var world = new EntityWorld();
            var lookup = new BattleEntityLookup();
            var actor = world.Create("xiao-qiao");
            actor.WithRef(new BattleTransformComponent
            {
                Position = new Vector3(2f, 0f, 3f),
                Forward = Vector3.right,
            });
            lookup.Bind(new BattleNetId(1002), actor);

            var ctx = BattleContext.Rent();
            var preview = new BattleHudAimPreview();
            try
            {
                ctx.LocalActorId = 1002;
                ctx.EntityQuery = new BattleEntityQuery(world, lookup);
                ctx.SetHudSkillAim(2, 4f, 0f, aiming: true);

                preview.Tick(ctx, 0.016f);

                Assert.IsNull(preview.PreviewRoot);
            }
            finally
            {
                preview.Clear();
                BattleContext.Return(ctx);
            }
        }

        [Test]
        public void AimIndicator_TargetCircleShowsSelectionRangeAndSelectedArea()
        {
            var root = new GameObject("TargetPointIndicator", typeof(RectTransform));
            var ring = new GameObject("SelectionRange", typeof(RectTransform), typeof(UnityEngine.UI.Image)).GetComponent<RectTransform>();
            var dot = new GameObject("TargetDot", typeof(RectTransform), typeof(UnityEngine.UI.Image)).GetComponent<RectTransform>();
            var range = new GameObject("TargetArea", typeof(RectTransform), typeof(UnityEngine.UI.Image)).GetComponent<RectTransform>();
            ring.SetParent(root.transform, false);
            dot.SetParent(root.transform, false);
            range.SetParent(root.transform, false);
            var indicator = root.AddComponent<SkillAimIndicatorView>();
            indicator.Initialize(ring, dot, range);

            try
            {
                var config = SkillButtonConfig.Default;
                config.EnableAim = true;
                config.AimMode = SkillAimMode.Point;
                config.UsePointMode = SkillUsePointMode.TargetPoint;
                config.IndicatorShape = SkillAimIndicatorShape.TargetCircle;
                config.IndicatorWidthPixels = 72f;

                indicator.SetFromTo(new Vector2(20f, 30f), new Vector2(140f, 30f), 100f, config);

                Assert.IsTrue(ring.gameObject.activeSelf);
                Assert.AreEqual(new Vector2(20f, 30f), ring.anchoredPosition);
                Assert.AreEqual(new Vector2(200f, 200f), ring.sizeDelta);
                Assert.IsTrue(range.gameObject.activeSelf);
                Assert.AreEqual(new Vector2(120f, 30f), range.anchoredPosition);
                Assert.AreEqual(new Vector2(76f, 76f), range.sizeDelta);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void AimIndicator_DirectionAreaShowsFixedRectangularFootprint()
        {
            var root = new GameObject("DirectionAreaIndicator", typeof(RectTransform));
            var ring = new GameObject("CasterAnchor", typeof(RectTransform), typeof(UnityEngine.UI.Image)).GetComponent<RectTransform>();
            var dot = new GameObject("UnusedDot", typeof(RectTransform), typeof(UnityEngine.UI.Image)).GetComponent<RectTransform>();
            var range = new GameObject("DirectionArea", typeof(RectTransform), typeof(UnityEngine.UI.Image)).GetComponent<RectTransform>();
            ring.SetParent(root.transform, false);
            dot.SetParent(root.transform, false);
            range.SetParent(root.transform, false);
            var indicator = root.AddComponent<SkillAimIndicatorView>();
            indicator.Initialize(ring, dot, range);

            try
            {
                var config = SkillButtonConfig.Default;
                config.EnableAim = true;
                config.IndicatorShape = SkillAimIndicatorShape.DirectionArea;
                config.IndicatorLengthPixels = 264f;
                config.IndicatorWidthPixels = 48f;

                indicator.SetFromTo(new Vector2(20f, 30f), new Vector2(20f, 130f), 220f, config);

                Assert.IsTrue(ring.gameObject.activeSelf);
                Assert.IsFalse(dot.gameObject.activeSelf);
                Assert.IsTrue(range.gameObject.activeSelf);
                Assert.AreEqual(new Vector2(20f, 30f), range.anchoredPosition);
                Assert.AreEqual(new Vector2(264f, 48f), range.sizeDelta);
                Assert.AreEqual(new Vector2(0f, 0.5f), range.pivot);
                Assert.AreEqual(90f, range.localEulerAngles.z, 0.0001f);
                Assert.AreSame(SkillAimIndicatorSprites.DirectionArea, range.GetComponent<UnityEngine.UI.Image>().sprite);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void AimPreviewObject_DirectionAreaShowsDajiSkill1RectangularFootprint()
        {
            var factory = new BattleHudAimPreviewObjectFactory();
            var preview = factory.Create();
            try
            {
                var spec = new BattleHudSkillPresentationSpec(
                    10050101,
                    "妲己-灵魂冲击",
                    BattleHudSkillPreviewShape.DirectionArea,
                    SkillAimIndicatorShape.DirectionArea,
                    12f,
                    2f,
                    0f,
                    new Color(0.2f, 0.75f, 1f, 0.3f));
                var state = new BattleHudAimPreviewState(1, new Vector3(2f, 0f, 3f), Vector3.right, 4f);

                preview.Apply(state, spec);

                var line = preview.Root.transform.Find("Line");
                var dot = preview.Root.transform.Find("Dot");
                var casterRing = preview.Root.transform.Find("CasterRing");
                var edgeRing = preview.Root.transform.Find("EdgeRing");

                Assert.IsTrue(preview.Root.activeSelf);
                Assert.IsTrue(line.gameObject.activeSelf);
                Assert.IsFalse(dot.gameObject.activeSelf);
                Assert.IsTrue(casterRing.gameObject.activeSelf);
                Assert.IsTrue(edgeRing.gameObject.activeSelf);
                Assert.AreEqual(new Vector3(2f, 0.035f, 12f), line.localScale);
                Assert.AreEqual(new Vector3(8f, 0.12f, 3f), line.position);
                Assert.AreEqual(new Vector3(14f, 0.215f, 3f), edgeRing.position);
            }
            finally
            {
                Object.DestroyImmediate(preview.Root);
            }
        }

        [Test]
        public void AimPreviewObject_TargetCircleShowsXiaoQiaoSkill2AtSelectedPoint()
        {
            var factory = new BattleHudAimPreviewObjectFactory();
            var preview = factory.Create();
            try
            {
                var spec = new BattleHudSkillPresentationSpec(
                    10020201,
                    "小乔-甜蜜恋风",
                    BattleHudSkillPreviewShape.TargetCircle,
                    SkillAimIndicatorShape.TargetCircle,
                    8f,
                    5.6f,
                    2.8f,
                    new Color(0.95f, 0.68f, 0.18f, 0.3f));
                var state = new BattleHudAimPreviewState(
                    2,
                    new Vector3(2f, 0f, 3f),
                    Vector3.right,
                    4f);

                preview.Apply(state, spec);

                var line = preview.Root.transform.Find("Line");
                var circle = preview.Root.transform.Find("Circle");
                var dot = preview.Root.transform.Find("Dot");
                var edgeRing = preview.Root.transform.Find("EdgeRing");

                Assert.IsTrue(preview.Root.activeSelf);
                Assert.IsFalse(line.gameObject.activeSelf);
                Assert.IsTrue(circle.gameObject.activeSelf);
                Assert.IsTrue(dot.gameObject.activeSelf);
                Assert.IsTrue(edgeRing.gameObject.activeSelf);
                Assert.AreEqual(6f, circle.position.x, 0.0001f);
                Assert.AreEqual(3f, circle.position.z, 0.0001f);
                Assert.AreEqual(circle.position.x, dot.position.x, 0.0001f);
                Assert.AreEqual(circle.position.z, dot.position.z, 0.0001f);
                Assert.AreEqual(circle.position.x, edgeRing.position.x, 0.0001f);
                Assert.AreEqual(circle.position.z, edgeRing.position.z, 0.0001f);
            }
            finally
            {
                Object.DestroyImmediate(preview.Root);
            }
        }

        [Test]
        public void AimPreviewObject_DirectionLineShowsMoziSkill2WorldArrow()
        {
            var factory = new BattleHudAimPreviewObjectFactory();
            var preview = factory.Create();
            try
            {
                var spec = new BattleHudSkillPresentationSpec(
                    10040201,
                    "墨子-机关重炮",
                    BattleHudSkillPreviewShape.DirectionLine,
                    SkillAimIndicatorShape.DirectionLine,
                    12f,
                    1.4f,
                    0f,
                    new Color(0.18f, 0.95f, 0.72f, 0.34f));
                var state = new BattleHudAimPreviewState(2, new Vector3(2f, 0f, 3f), Vector3.right, 1f);

                preview.Apply(state, spec);

                var line = preview.Root.transform.Find("Line");
                var dot = preview.Root.transform.Find("Dot");
                var casterRing = preview.Root.transform.Find("CasterRing");
                var edgeRing = preview.Root.transform.Find("EdgeRing");

                Assert.IsTrue(preview.Root.activeSelf);
                Assert.IsNotNull(line);
                Assert.IsNotNull(dot);
                Assert.IsNotNull(casterRing);
                Assert.IsNotNull(edgeRing);
                Assert.IsTrue(line.gameObject.activeSelf);
                Assert.IsTrue(dot.gameObject.activeSelf);
                Assert.IsTrue(casterRing.gameObject.activeSelf);
                Assert.IsTrue(edgeRing.gameObject.activeSelf);
                Assert.AreEqual(new Vector3(1.4f, 0.035f, 12f), line.localScale);
                Assert.AreEqual(new Vector3(8f, 0.12f, 3f), line.position);
                Assert.AreEqual(new Vector3(14f, 0.155f, 3f), dot.position);

                var renderer = line.GetComponent<Renderer>();
                Assert.IsNotNull(renderer);
                Assert.IsNotNull(renderer.sharedMaterial);
                Assert.AreEqual((int)UnityEngine.Rendering.RenderQueue.Overlay, renderer.sharedMaterial.renderQueue);
            }
            finally
            {
                Object.DestroyImmediate(preview.Root);
            }
        }

        [Test]
        public void AimPreviewPositionResolver_UsesReadPortWithoutBattleContext()
        {
            var input = new FakeAimPreviewReadPort
            {
                Slot = 2,
                AimDx = 3f,
                AimDz = 4f,
                SubmissionVersion = 7,
                CasterX = 10f,
                CasterY = 1f,
                CasterZ = 20f,
            };
            var resolver = new BattleHudAimPreviewPositionResolver();

            Assert.IsTrue(resolver.TryResolve(input, out var state));
            Assert.AreEqual(2, state.Slot);
            Assert.AreEqual(new Vector3(10f, 1f, 20f), state.CasterPosition);
            Assert.AreEqual(new Vector3(0.6f, 0f, 0.8f), state.AimDirection);
            Assert.AreEqual(5f, state.AimDistance, 0.0001f);
            Assert.AreEqual(7, state.SubmissionVersion);
        }

        [Test]
        public void AimPreviewPositionResolver_ReusesLastCasterPositionWhenLookupTemporarilyFails()
        {
            var input = new FakeAimPreviewReadPort
            {
                Slot = 1,
                AimDx = 1f,
                CasterX = 2f,
                CasterY = 0.5f,
                CasterZ = 3f,
            };
            var resolver = new BattleHudAimPreviewPositionResolver();

            Assert.IsTrue(resolver.TryResolve(input, out var first));
            input.CanResolveCasterPosition = false;
            input.AimDx = 0f;
            input.AimDz = 2f;

            Assert.IsTrue(resolver.TryResolve(input, out var second));
            Assert.AreEqual(first.CasterPosition, second.CasterPosition);
            Assert.AreEqual(Vector3.forward, second.AimDirection);
            Assert.AreEqual(2f, second.AimDistance, 0.0001f);
        }

        private sealed class FakeAimPreviewReadPort : IBattleHudAimPreviewReadPort
        {
            public int Slot { get; set; }
            public float AimDx { get; set; }
            public float AimDz { get; set; }
            public int SubmissionVersion { get; set; }
            public bool CanResolveCasterPosition { get; set; } = true;
            public float CasterX { get; set; }
            public float CasterY { get; set; }
            public float CasterZ { get; set; }

            public bool TryReadAimPreview(
                out int slot,
                out float dx,
                out float dz,
                out int submissionVersion)
            {
                slot = Slot;
                dx = AimDx;
                dz = AimDz;
                submissionVersion = SubmissionVersion;
                return true;
            }

            public bool TryResolveLocalActorWorldPosition(
                out float x,
                out float y,
                out float z)
            {
                x = CasterX;
                y = CasterY;
                z = CasterZ;
                return CanResolveCasterPosition;
            }
        }

        private sealed class BattleHudInputEventBridgeFixture : System.IDisposable
        {
            private readonly GameObject _root;

            private BattleHudInputEventBridgeFixture(
                GameObject root,
                BattleHudInputEventBridge bridge,
                BattleHudInputUi inputUi,
                RecordingHudInputSink sink,
                SkillButtonView skill1,
                SkillButtonView skill2,
                SkillButtonView skill3)
            {
                _root = root;
                Bridge = bridge;
                InputUi = inputUi;
                Sink = sink;
                Skill1 = skill1;
                Skill2 = skill2;
                Skill3 = skill3;
            }

            public BattleHudInputEventBridge Bridge { get; }
            public BattleHudInputUi InputUi { get; }
            public RecordingHudInputSink Sink { get; }
            public SkillButtonView Skill1 { get; }
            public SkillButtonView Skill2 { get; }
            public SkillButtonView Skill3 { get; }

            public static BattleHudInputEventBridgeFixture Create()
            {
                var root = new GameObject("BattleHudInputEventBridgeTests.Root", typeof(RectTransform));
                var inputView = root.AddComponent<BattleHudInputView>();
                var skill1 = CreateSkill(root.transform, "Skill1");
                var skill2 = CreateSkill(root.transform, "Skill2");
                var skill3 = CreateSkill(root.transform, "Skill3");
                inputView.Initialize(null, skill1, skill2, skill3);

                var skillAimMapper = new BattleHudSkillAimInputMapper();
                skillAimMapper.Initialize(inputView, null);
                var inputUi = new BattleHudInputUi(root, inputView, null, null, skillAimMapper, new[] { skill1, skill2, skill3 }, null);
                var sink = new RecordingHudInputSink();
                var bridge = new BattleHudInputEventBridge(sink);

                return new BattleHudInputEventBridgeFixture(root, bridge, inputUi, sink, skill1, skill2, skill3);
            }

            public void Click(SkillButtonView skill)
            {
                PointerDown(skill, new Vector2(10f, 10f));
                PointerUp(skill, new Vector2(10f, 10f));
            }

            public void PointerDown(SkillButtonView skill, Vector2 position)
            {
                skill.OnPointerDown(Pointer(1, position));
            }

            public void PointerUp(SkillButtonView skill, Vector2 position)
            {
                skill.OnPointerUp(Pointer(1, position));
            }

            public void Dispose()
            {
                Bridge.Dispose();
                Object.DestroyImmediate(_root);
            }

            private static PointerEventData Pointer(int pointerId, Vector2 position)
            {
                return new PointerEventData(EventSystem.current)
                {
                    pointerId = pointerId,
                    position = position
                };
            }

            private static SkillButtonView CreateSkill(Transform parent, string name)
            {
                var go = new GameObject(name, typeof(RectTransform));
                go.transform.SetParent(parent, false);
                var rect = (RectTransform)go.transform;
                var skill = go.AddComponent<SkillButtonView>();
                var config = SkillButtonConfig.Default;
                config.EnableAim = name == "Skill2";
                config.IndicatorShape = config.EnableAim ? SkillAimIndicatorShape.DirectionLine : SkillAimIndicatorShape.Hidden;
                skill.Initialize(rect, parent as RectTransform, null, config);
                return skill;
            }
        }

        private sealed class RecordingHudInputSink : IBattleHudInputSink
        {
            public int BeginMoveCount { get; private set; }
            public int EndMoveCount { get; private set; }
            public float MoveDx { get; private set; }
            public float MoveDz { get; private set; }
            public int ClickCount { get; private set; }
            public int ClickedSlot { get; private set; }
            public int ActiveAimSlot { get; private set; }
            public float ActiveAimDx { get; private set; }
            public float ActiveAimDz { get; private set; }
            public bool ActiveAimAiming { get; private set; }
            public int SubmittedAimSlot { get; private set; }
            public float SubmittedAimDx { get; private set; }
            public float SubmittedAimDz { get; private set; }
            public int ResetAimSlot { get; private set; } = -1;
            public bool ResetAimAiming { get; private set; } = true;

            public void BeginHudMove()
            {
                BeginMoveCount++;
            }

            public void EndHudMove()
            {
                EndMoveCount++;
            }

            public void SetHudMove(float dx, float dz)
            {
                MoveDx = dx;
                MoveDz = dz;
            }

            public void SubmitHudSkillClick(int slot)
            {
                ClickCount++;
                ClickedSlot = slot;
            }

            public void SetHudSkillAim(int slot, float dx, float dz, bool aiming)
            {
                if (slot == 0)
                {
                    ResetAimSlot = slot;
                    ResetAimAiming = aiming;
                    return;
                }

                ActiveAimSlot = slot;
                ActiveAimDx = dx;
                ActiveAimDz = dz;
                ActiveAimAiming = aiming;
            }

            public void CancelHudSkillAim()
            {
                ResetAimSlot = 0;
                ResetAimAiming = false;
            }

            public void SubmitHudSkillAim(int slot, float aimDx, float aimDz)
            {
                SubmittedAimSlot = slot;
                SubmittedAimDx = aimDx;
                SubmittedAimDz = aimDz;
            }
        }
    }
}
