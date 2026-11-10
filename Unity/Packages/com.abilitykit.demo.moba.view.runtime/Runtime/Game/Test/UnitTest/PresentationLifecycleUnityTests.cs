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
    public sealed class PresentationLifecycleUnityTests
    {
        private const string Resource = "ChampionVfx/13.Jhin_Battlecast/Jhin_Battlecast_Q";

        private sealed class LifetimeProbe : IPresentationLifetimeAdapter<GameObject>
        {
            public bool Complete;
            public bool TryIsComplete(GameObject visual, out bool complete) { complete = Complete; return true; }
            public bool TryBeginDrain(GameObject visual) => true;
        }

        private static PresentationRequest Request(PresentationLifecyclePolicy policy, int actorId = 0, string key = "lifecycle") =>
            new PresentationRequest(key, 90013001, PresentationVector3.Zero,
                attachment: actorId > 0 ? new PresentationAttachment(PresentationAttachmentKind.Actor, actorId) : default,
                durationMs: 100000, resourceKey: Resource, assetKind: PresentationAssetKind.Particle, lifecycle: policy);

        [UnityTest]
        public IEnumerator BackendRejectsAndReplacesSameKeyAfterConcurrentLoads()
        {
            var root = new GameObject("lifecycle-root");
            var backend = new PresentationUnityBackend(root.transform);
            try
            {
                var reject = Request(new PresentationLifecyclePolicy(stack: PresentationStackPolicy.RejectSameKey));
                var first = backend.PlayAsync(reject);
                var duplicate = backend.PlayAsync(reject);
                yield return new WaitUntil(() => first.IsCompleted && duplicate.IsCompleted);
                var a = first.GetAwaiter().GetResult();
                var b = duplicate.GetAwaiter().GetResult();
                Assert.AreEqual(1, root.transform.childCount);
                Assert.IsTrue((a == null) != (b == null));
                var original = a ?? b;
                var replace = backend.PlayAsync(Request(new PresentationLifecyclePolicy(stack: PresentationStackPolicy.ReplaceSameKey)));
                yield return new WaitUntil(() => replace.IsCompleted);
                Assert.IsNotNull(replace.GetAwaiter().GetResult());
                Assert.IsFalse(backend.IsAlive(original.BackendHandle));
                Assert.AreEqual(1, root.transform.childCount);
            }
            finally { backend.ClearAsync().GetAwaiter().GetResult(); Object.DestroyImmediate(root); }
        }

        [UnityTest]
        public IEnumerator BackendDrainTracksLiveTailAndUsesUnscaledTimeout()
        {
            var root = new GameObject("drain-root");
            var probe = new LifetimeProbe();
            var backend = new PresentationUnityBackend(root.transform, lifetimeAdapter: probe);
            try
            {
                var policy = new PresentationLifecyclePolicy(stop: PresentationStopPolicy.Drain, stopGraceMs: 1000);
                var play = backend.PlayAsync(Request(policy));
                yield return new WaitUntil(() => play.IsCompleted);
                var handle = play.GetAwaiter().GetResult();
                handle.StopAsync().GetAwaiter().GetResult();
                Assert.IsTrue(backend.IsAlive(handle.BackendHandle));
                backend.Tick(0f, 0.75f);
                backend.StopAsync(handle.BackendHandle).GetAwaiter().GetResult();
                Assert.IsTrue(backend.IsAlive(handle.BackendHandle));
                backend.Tick(0f, 0.25f);
                Assert.IsFalse(backend.IsAlive(handle.BackendHandle));
                Assert.AreEqual(0, root.transform.childCount);
                var second = backend.PlayAsync(Request(policy));
                yield return new WaitUntil(() => second.IsCompleted);
                var tail = second.GetAwaiter().GetResult();
                tail.StopAsync().GetAwaiter().GetResult();
                probe.Complete = true;
                backend.Tick(0f, 0f);
                Assert.IsFalse(backend.IsAlive(tail.BackendHandle));
            }
            finally { backend.ClearAsync().GetAwaiter().GetResult(); Object.DestroyImmediate(root); }
        }

        [UnityTest]
        public IEnumerator BackendClearInvalidatesPendingLoadsAndAllowsFreshRequests()
        {
            var root = new GameObject("load-reset-root");
            var backend = new PresentationUnityBackend(root.transform);
            try
            {
                var pending = backend.PlayAsync(Request(default));
                backend.ClearAsync().GetAwaiter().GetResult();
                yield return new WaitUntil(() => pending.IsCompleted);
                var old = pending.GetAwaiter().GetResult();
                Assert.IsTrue(old == null || !backend.IsAlive(old.BackendHandle));
                Assert.AreEqual(0, root.transform.childCount);
                var fresh = backend.PlayAsync(Request(default));
                yield return new WaitUntil(() => fresh.IsCompleted);
                Assert.IsNotNull(fresh.GetAwaiter().GetResult());
            }
            finally { backend.ClearAsync().GetAwaiter().GetResult(); Object.DestroyImmediate(root); }
        }

        [UnityTest]
        public IEnumerator TargetLossPreservesPoseAndOnlyRebindPolicyResumesFollowing()
        {
            foreach (var loss in new[] { PresentationTargetLossPolicy.Stop, PresentationTargetLossPolicy.Detach, PresentationTargetLossPolicy.Rebind })
            {
                var root = new GameObject("loss-root");
                var actor = new GameObject("loss-actor");
                actor.transform.position = new Vector3(4, 2, 3);
                actor.transform.localScale = Vector3.one * 2;
                var attachments = new UnityPresentationAttachmentResolver();
                attachments.Register(42, actor.transform);
                var backend = new PresentationUnityBackend(root.transform, attachments);
                try
                {
                    var play = backend.PlayAsync(Request(new PresentationLifecyclePolicy(targetLoss: loss), 42));
                    yield return new WaitUntil(() => play.IsCompleted);
                    var handle = play.GetAwaiter().GetResult();
                    Assert.AreEqual(0, actor.transform.childCount);
                    var visual = root.transform.GetChild(0);
                    var pose = visual.position;
                    var scale = visual.lossyScale;
                    Object.DestroyImmediate(actor);
                    backend.Tick(0, 0);
                    if (loss == PresentationTargetLossPolicy.Stop) Assert.IsFalse(backend.IsAlive(handle.BackendHandle));
                    else
                    {
                        Assert.IsTrue(backend.IsAlive(handle.BackendHandle));
                        Assert.AreEqual(pose, visual.position);
                        backend.Update(handle.BackendHandle, Request(new PresentationLifecyclePolicy(targetLoss: loss), 42));
                        Assert.AreEqual(pose, visual.position, "Updating a missing attachment must preserve its last pose.");
                        actor = new GameObject("replacement-actor");
                        actor.transform.position = new Vector3(8, 3, 9);
                        attachments.Register(42, actor.transform);
                        backend.Tick(0, 0);
                        Assert.AreEqual(loss == PresentationTargetLossPolicy.Rebind ? actor.transform.position : pose, visual.position);
                        if (loss == PresentationTargetLossPolicy.Detach)
                        {
                            backend.SetScale(new PresentationScaleSource("skin", PresentationScaleChannel.Vfx, 2));
                            Assert.That(visual.lossyScale.x, Is.EqualTo(scale.x * 2).Within(0.001f));
                        }
                    }
                }
                finally { backend.ClearAsync().GetAwaiter().GetResult(); if (actor != null) Object.DestroyImmediate(actor); Object.DestroyImmediate(root); }
            }
        }

        [Test]
        public void ParticleDrainStopsEmissionAndPoolReuseRestoresAnimatorBaseline()
        {
            var pool = new BattleVfxGameObjectPool(_ =>
            {
                var go = new GameObject("pooled-drain");
                go.AddComponent<ParticleSystem>();
                go.AddComponent<Animator>();
                var child = new GameObject("disabled-animator");
                child.transform.SetParent(go.transform);
                child.AddComponent<Animator>().enabled = false;
                return go;
            });
            try
            {
                Assert.IsTrue(pool.TryRent(1, out var visual));
                var particle = visual.GetComponent<ParticleSystem>();
                particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                particle.Play();
                particle.Emit(5);
                var adapter = new UnityPresentationLifetimeAdapter();
                Assert.IsTrue(adapter.TryBeginDrain(visual));
                Assert.IsFalse(particle.isEmitting);
                Assert.IsFalse(visual.GetComponent<Animator>().enabled);
                Assert.IsTrue(pool.Return(1, visual));
                Assert.IsTrue(pool.TryRent(1, out var reused));
                Assert.AreSame(visual, reused);
                Assert.IsTrue(reused.GetComponent<Animator>().enabled);
                Assert.IsFalse(reused.transform.GetChild(0).GetComponent<Animator>().enabled);
            }
            finally { pool.Clear(); }
        }

        [Test]
        public void BattleManagerStackReplacementCleansEntityAndPolicyLedger()
        {
            var manager = new BattleVfxManager(new VfxDatabase(new Dictionary<int, VfxDTO>
            { [90013001] = new VfxDTO { Id = 90013001, Resource = Resource, DurationMs = 100000 } }));
            var world = new EntityWorld();
            var root = world.Create("lifecycle-vfx");
            try
            {
                var reject = Request(new PresentationLifecyclePolicy(stack: PresentationStackPolicy.RejectSameKey));
                Assert.IsTrue(manager.TryCreatePresentationVfxEntity(world, root, reject, default, out var first));
                Assert.IsFalse(manager.TryCreatePresentationVfxEntity(world, root, reject, default, out _));
                var replace = Request(new PresentationLifecyclePolicy(stack: PresentationStackPolicy.ReplaceSameKey));
                Assert.IsTrue(manager.TryCreatePresentationVfxEntity(world, root, replace, default, out var second));
                Assert.IsFalse(world.IsAlive(first.Id));
                Assert.IsTrue(second.TryGetRef(out BattlePresentationLifetimeComponent lifetime));
                Assert.IsFalse(lifetime.State.IsStopping);
                manager.Clear(root);
                Assert.IsTrue(manager.TryCreatePresentationVfxEntity(world, root, reject, default, out _));
            }
            finally { manager.Clear(root); if (root.IsValid) root.Destroy(); }
        }

        [TestCase(PresentationTargetLossPolicy.Stop, false, false)]
        [TestCase(PresentationTargetLossPolicy.Detach, true, true)]
        [TestCase(PresentationTargetLossPolicy.Rebind, true, false)]
        public void BattleManagerTargetRemovalHonorsTemplatePolicy(PresentationTargetLossPolicy loss, bool survives, bool detached)
        {
            var manager = new BattleVfxManager(new VfxDatabase(new Dictionary<int, VfxDTO>
            { [90013001] = new VfxDTO { Id = 90013001, Resource = Resource, DurationMs = 100000 } }));
            var world = new EntityWorld();
            var root = world.Create("target-removal-root");
            try
            {
                var request = Request(new PresentationLifecyclePolicy(targetLoss: loss), 42);
                Assert.IsTrue(manager.TryCreatePresentationVfxEntity(world, root, request, default, out var entity));
                manager.DestroyVfxByFollowTargetActorId(root, 42);
                Assert.AreEqual(survives, world.IsAlive(entity.Id));
                if (!survives) return;
                entity.TryGetRef(out BattlePresentationLifetimeComponent lifetime);
                Assert.AreEqual(detached, lifetime.State.IsDetached);
                entity.TryGetRef(out BattleViewFollowComponent follow);
                Assert.AreEqual(detached ? 0 : 42, follow.TargetActorId);
            }
            finally { manager.Clear(root); if (root.IsValid) root.Destroy(); }
        }

        [Test]
        public void BattleManagerDrainRetainsEntityUntilResourceCompletionAndReuseStartsFresh()
        {
            var probe = new LifetimeProbe();
            var manager = new BattleVfxManager(new VfxDatabase(new Dictionary<int, VfxDTO>
            { [90013001] = new VfxDTO { Id = 90013001, Resource = Resource, DurationMs = 100000 } }),
                null, null, null, probe);
            var world = new EntityWorld();
            var root = world.Create("battle-drain-root");
            try
            {
                var request = Request(new PresentationLifecyclePolicy(stop: PresentationStopPolicy.Drain));
                Assert.IsTrue(manager.TryCreatePresentationVfxEntity(world, root, request, default, out var entity));
                manager.StopPresentationVfxEntity(world, entity.Id);
                Assert.IsTrue(world.IsAlive(entity.Id));
                manager.Tick(root);
                Assert.IsTrue(world.IsAlive(entity.Id));
                probe.Complete = true;
                manager.Tick(root);
                Assert.IsFalse(world.IsAlive(entity.Id));
                Assert.IsTrue(manager.TryCreatePresentationVfxEntity(world, root, request, default, out var reused));
                reused.TryGetRef(out BattlePresentationLifetimeComponent lifetime);
                Assert.IsFalse(lifetime.State.IsStopping);
                Assert.IsFalse(lifetime.State.IsFinished);
            }
            finally { manager.Clear(root); if (root.IsValid) root.Destroy(); }
        }
    }
}
