using System.Collections;
using System.Collections.Generic;
using AbilityKit.Demo.Moba.Share.Config;
using AbilityKit.Game.Battle.Component;
using AbilityKit.Game.Battle.Vfx;
using AbilityKit.Game.Flow;
using AbilityKit.Game.Platform.Unity.Presentation;
using AbilityKit.World.ECS;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AbilityKit.Game.Tests
{
    public sealed class PresentationSpatialUnityTests
    {
        private const string Resource = "ChampionVfx/13.Jhin_Battlecast/Jhin_Battlecast_Q";

        private static void Equal(Vector3 expected, Vector3 actual) => Assert.That(Vector3.Distance(expected, actual), Is.LessThan(0.001f));

        [UnityTest]
        public IEnumerator BothBackendsAgreeOnSocketOffsetsScaleAndSourceUpdates()
        {
            foreach (var space in new[] { PresentationOffsetSpace.TargetLocal, PresentationOffsetSpace.World })
            foreach (var parentScale in new[] { PresentationParentScaleMode.Inherit, PresentationParentScaleMode.Ignore })
            {
                var root = new GameObject("spatial-root");
                root.transform.localScale = Vector3.one * 3;
                var actor = new GameObject("spatial-actor");
                actor.transform.SetPositionAndRotation(new Vector3(3, 2, 4), Quaternion.Euler(0, 90, 0));
                actor.transform.localScale = Vector3.one * 2;
                var socket = new GameObject("hand");
                socket.transform.SetParent(actor.transform, false);
                socket.transform.localPosition = Vector3.up;
                socket.transform.localScale = Vector3.one * 0.5f;
                var attachments = new UnityPresentationAttachmentResolver();
                attachments.Register(42, actor.transform);
                var backend = new PresentationUnityBackend(root.transform, attachments);
                var manager = new BattleVfxManager(new VfxDatabase(new Dictionary<int, VfxDTO>
                { [90013001] = new VfxDTO { Id = 90013001, Resource = Resource, DurationMs = 100000 } }));
                var world = new EntityWorld();
                var vfxRoot = world.Create("vfx-root");
                var target = world.Create("target");
                target.WithRef(new BattleViewGameObjectComponent { GameObject = actor });
                try
                {
                    var request = new PresentationRequest("spatial-parity", 90013001, PresentationVector3.Zero,
                        attachment: new PresentationAttachment(PresentationAttachmentKind.Socket, 42, "hand", new PresentationVector3(1, 0, 0), space),
                        durationMs: 100000, scale: 1.5f, radius: 2f, resourceKey: Resource, parentScaleMode: parentScale);
                    var pending = backend.PlayAsync(request);
                    yield return new WaitUntil(() => pending.IsCompleted);
                    var handle = pending.GetAwaiter().GetResult();
                    Assert.IsNotNull(handle);
                    Assert.IsTrue(manager.TryCreatePresentationVfxEntity(world, vfxRoot, request, target.Id, out var entity));
                    entity.TryGetRef(out BattleViewGameObjectComponent view);
                    var battleVisual = view.GameObject.transform;
                    var unityVisual = socket.transform.GetChild(0);
                    Equal(unityVisual.position, battleVisual.position);
                    Equal(unityVisual.lossyScale, battleVisual.lossyScale);
                    var state = view.GameObject.GetComponent<UnityPresentationInstanceState>();
                    Assert.AreSame(socket.transform, state.AttachmentTarget);
                    var source = new PresentationScaleSource("source.buff", PresentationScaleChannel.Vfx, 0.25f);
                    backend.SetScale(source);
                    state.Sources.Set(source);
                    actor.transform.localScale = Vector3.one * 4;
                    actor.transform.position += Vector3.forward;
                    actor.transform.rotation = Quaternion.Euler(0, 40, 0);
                    manager.Tick(vfxRoot);
                    backend.Tick(0, 0);
                    Equal(unityVisual.position, battleVisual.position);
                    Equal(unityVisual.lossyScale, battleVisual.lossyScale);
                    var scale = battleVisual.lossyScale;
                    for (var i = 0; i < 10; i++) manager.Tick(vfxRoot);
                    Equal(scale, battleVisual.lossyScale);
                    backend.RemoveScale("source.buff");
                    state.Sources.Remove("source.buff");
                    manager.Tick(vfxRoot);
                    Equal(unityVisual.lossyScale, battleVisual.lossyScale);
                }
                finally
                {
                    manager.Clear(vfxRoot);
                    backend.ClearAsync().GetAwaiter().GetResult();
                    if (target.IsValid) target.Destroy();
                    if (vfxRoot.IsValid) vfxRoot.Destroy();
                    Object.DestroyImmediate(actor);
                    Object.DestroyImmediate(root);
                }
            }
        }

        [Test]
        public void OneShotPlacementUsesMissingSocketFallbackAndDoesNotFollowOrInheritScale()
        {
            var actor = new GameObject("one-shot-target");
            actor.transform.position = new Vector3(3, 0, 4);
            actor.transform.localScale = Vector3.one * 2;
            var manager = new BattleVfxManager(new VfxDatabase(new Dictionary<int, VfxDTO>
            { [90013001] = new VfxDTO { Id = 90013001, Resource = Resource, DurationMs = 100000 } }));
            var world = new EntityWorld();
            var root = world.Create("vfx-root");
            var target = world.Create("target");
            target.WithRef(new BattleViewGameObjectComponent { GameObject = actor });
            try
            {
                var request = new PresentationRequest("one-shot", 90013001, PresentationVector3.Zero,
                    attachment: new PresentationAttachment(PresentationAttachmentKind.Socket, 42, "missing", new PresentationVector3(1, 0, 0)),
                    durationMs: 100000, followAttachment: false);
                Assert.IsTrue(manager.TryCreatePresentationVfxEntity(world, root, request, target.Id, out var entity));
                entity.TryGetRef(out BattleViewGameObjectComponent view);
                var state = view.GameObject.GetComponent<UnityPresentationInstanceState>();
                Equal(new Vector3(5, 0, 4), view.GameObject.transform.position);
                Equal(state.BaseScale, view.GameObject.transform.lossyScale);
                actor.transform.position = Vector3.zero;
                manager.Tick(root);
                Equal(new Vector3(5, 0, 4), view.GameObject.transform.position);
                manager.DestroyVfxByFollowTargetActorId(root, 42);
                Assert.IsTrue(world.IsAlive(entity.Id));
            }
            finally { manager.Clear(root); if (target.IsValid) target.Destroy(); if (root.IsValid) root.Destroy(); Object.DestroyImmediate(actor); }
        }

        [Test]
        public void OldDirectApiRetainsWorldOffsetConvention()
        {
            var manager = new BattleVfxManager(new VfxDatabase(new Dictionary<int, VfxDTO>()));
            var world = new EntityWorld();
            var root = world.Create("legacy-root");
            try
            {
                var origin = Vector3.zero;
                var offset = Vector3.right;
                Assert.IsTrue(manager.TryCreateVfxEntity(world, root, 1, default, 0, offset, origin, Quaternion.identity, 100000, out var entity));
                var position = new Vector3(5, 0, 0);
                var forward = Vector3.right;
                manager.SyncFollow(world, entity.Id, position, forward);
                entity.TryGetRef(out BattleViewGameObjectComponent view);
                Equal(new Vector3(6, 0, 0), view.GameObject.transform.position);
            }
            finally { manager.Clear(root); if (root.IsValid) root.Destroy(); }
        }

        [Test]
        public void MissingShellUsesLogicalScaleAndOneShotDoesNotAcquireLateTarget()
        {
            var manager = new BattleVfxManager(new VfxDatabase(new Dictionary<int, VfxDTO>
            { [90013001] = new VfxDTO { Id = 90013001, Resource = Resource, DurationMs = 100000 } }));
            var world = new EntityWorld();
            var root = world.Create("fallback-root");
            var target = world.Create("logical-target");
            var logical = new BattleTransformComponent { Position = new Vector3(3, 0, 0), Forward = Vector3.right, Scale = Vector3.one * 2 };
            target.WithRef(logical);
            try
            {
                var request = new PresentationRequest("logic-fallback", 90013001, PresentationVector3.Zero,
                    attachment: new PresentationAttachment(PresentationAttachmentKind.Socket, 42, "missing", new PresentationVector3(0, 0, 1)), durationMs: 100000);
                Assert.IsTrue(manager.TryCreatePresentationVfxEntity(world, root, request, target.Id, out var entity));
                entity.TryGetRef(out BattleViewGameObjectComponent view);
                var state = view.GameObject.GetComponent<UnityPresentationInstanceState>();
                Equal(new Vector3(5, 0, 0), view.GameObject.transform.position);
                Equal(state.BaseScale * 2, view.GameObject.transform.lossyScale);
                var oneShot = new PresentationRequest("missing-one-shot", 90013001, new PresentationVector3(7, 0, 0),
                    attachment: new PresentationAttachment(PresentationAttachmentKind.Actor, 43), durationMs: 100000, followAttachment: false);
                Assert.IsTrue(manager.TryCreatePresentationVfxEntity(world, root, oneShot, default, out var once));
                once.TryGetRef(out BattleViewFollowComponent follow);
                follow.Target = target.Id;
                manager.Tick(root);
                once.TryGetRef(out BattleViewGameObjectComponent oneView);
                Equal(new Vector3(7, 0, 0), oneView.GameObject.transform.position);
            }
            finally { manager.Clear(root); if (target.IsValid) target.Destroy(); if (root.IsValid) root.Destroy(); }
        }

        [Test]
        public void PooledInstanceClearsOldSourcesAndRetainsPrefabBaseline()
        {
            var manager = new BattleVfxManager(new VfxDatabase(new Dictionary<int, VfxDTO>
            { [90013001] = new VfxDTO { Id = 90013001, Resource = Resource, DurationMs = 100000 } }));
            var world = new EntityWorld();
            var root = world.Create("reuse-root");
            try
            {
                var first = new PresentationRequest("first", 90013001, PresentationVector3.Zero, durationMs: 100000, scale: 2f);
                Assert.IsTrue(manager.TryCreatePresentationVfxEntity(world, root, first, default, out var entity));
                entity.TryGetRef(out BattleViewGameObjectComponent view);
                var original = view.GameObject;
                var state = original.GetComponent<UnityPresentationInstanceState>();
                var baseline = state.BaseScale;
                state.Sources.Set(new PresentationScaleSource("temporary", PresentationScaleChannel.Vfx, 3f));
                manager.Tick(root);
                Equal(baseline * 6, original.transform.lossyScale);
                manager.DestroyVfxEntity(world, entity.Id);
                Assert.IsFalse(state.IsBound);
                var second = new PresentationRequest("second", 90013001, PresentationVector3.Zero, durationMs: 100000, scale: 0.5f);
                Assert.IsTrue(manager.TryCreatePresentationVfxEntity(world, root, second, default, out var reused));
                reused.TryGetRef(out BattleViewGameObjectComponent next);
                Assert.AreSame(original, next.GameObject);
                Equal(baseline * 0.5f, next.GameObject.transform.lossyScale);
                Assert.AreEqual(2, state.Sources.Count);
                Assert.AreEqual("second", state.Request.LogicalKey);
            }
            finally { manager.Clear(root); if (root.IsValid) root.Destroy(); }
        }
    }
}
