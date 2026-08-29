using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.FrameSync.Rollback;
using AbilityKit.Ability.Host.Extensions.FrameSync;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Game.Flow;
using NUnit.Framework;
using UnityEngine;

namespace AbilityKit.Game.Test.UnitTest
{
    public sealed class BattleInputRuntimeTests
    {
        [Test]
        public void LocalActorResolver_UsesCacheWhenAuthoritativeMappingIsUnavailable()
        {
            var port = new FakeActorResolutionPort
            {
                CachedActorId = 41,
            };
            var resolver = new BattleLocalActorResolver(port);

            var resolved = resolver.TryResolveActorId(out var actorId);

            Assert.That(resolved, Is.True);
            Assert.That(actorId, Is.EqualTo(41));
            Assert.That(port.MapCount, Is.EqualTo(1));
        }

        [Test]
        public void LocalActorResolver_AuthoritativeMappingRepairsForeignCachedActor()
        {
            var port = new FakeActorResolutionPort
            {
                CachedActorId = 41,
                MappedActorId = 73,
            };
            var resolver = new BattleLocalActorResolver(port);

            var resolved = resolver.TryResolveActorId(out var actorId);

            Assert.That(resolved, Is.True);
            Assert.That(actorId, Is.EqualTo(73));
            Assert.That(port.CachedActorId, Is.EqualTo(73));
            Assert.That(port.MapCount, Is.EqualTo(1));
        }

        [Test]
        public void LocalActorResolver_WhenCachedPositionIsStale_RemapsActor()
        {
            var port = new FakeActorResolutionPort
            {
                CachedActorId = 41,
                MappedActorId = 73,
                MappedPosition = new Vector3(10f, 2f, -4f),
            };
            var resolver = new BattleLocalActorResolver(port);

            var resolved = resolver.TryResolveWorldPosition(out var position);

            Assert.That(resolved, Is.True);
            Assert.That(port.CachedActorId, Is.EqualTo(73));
            Assert.That(port.MapCount, Is.EqualTo(1));
            AssertVector3(new Vector3(10f, 2f, -4f), position);
        }

        [Test]
        public void AimProjection_AddsOffsetToActorAndNormalizesDirection()
        {
            var port = new FakeActorResolutionPort
            {
                CachedActorId = 7,
                CachedPosition = new Vector3(5f, 1f, 6f),
            };
            var projection = new BattleAimProjectionService(
                new BattleLocalActorResolver(port));

            var result = projection.Project(3f, 4f);

            AssertVector3(new Vector3(8f, 1f, 10f), result.Position);
            AssertVector3(new Vector3(0.6f, 0f, 0.8f), result.Direction);
        }

        [Test]
        public void InputRuntime_RebindClearsTransientStateAndIgnoresStaleUnbind()
        {
            var first = BattleContext.Rent();
            var second = BattleContext.Rent();
            var runtime = new BattleInputRuntime();
            try
            {
                runtime.Bind(first);
                runtime.BeginMove();
                runtime.SetMove(0.5f, -0.25f);
                runtime.SubmitSkillClick(2);
                runtime.LocalInputQueue.Flush();

                runtime.Bind(second);
                runtime.Unbind(first);

                Assert.That(runtime.Context, Is.SameAs(second));
                Assert.That(runtime.TryReadMove(out _, out _), Is.False);
                Assert.That(runtime.TryConsumeSkillClick(out _), Is.False);
                Assert.That(runtime.LocalInputQueue.LocalFrame, Is.Zero);
                Assert.That(runtime.LocalInputQueue.TryDequeue(out _), Is.False);
            }
            finally
            {
                runtime.Dispose();
                BattleContext.Return(first);
                BattleContext.Return(second);
            }
        }

        [Test]
        public void ContextPoolRelease_UnbindsAndClearsSessionInputOwner()
        {
            var context = BattleContext.Rent();
            var runtime = new BattleInputRuntime();
            context.BindInputRuntime(runtime);
            context.BeginHudMove();
            context.SetHudMove(1f, 0f);
            runtime.LocalInputQueue.Flush();

            BattleContext.Return(context);

            Assert.That(runtime.Context, Is.Null);
            Assert.That(runtime.TryReadMove(out _, out _), Is.False);
            Assert.That(runtime.LocalInputQueue.LocalFrame, Is.Zero);
            runtime.Dispose();
        }

        [Test]
        public void ContextPoolRelease_UnbindsAndClearsSessionPredictionOwner()
        {
            var context = BattleContext.Rent();
            var runtime = new BattlePredictionRuntime();
            var target = new FakeReconcileTarget();
            context.BindPredictionRuntime(runtime);
            runtime.Bind(null, target, null, null);

            BattleContext.Return(context);
            var reusedContext = BattleContext.Rent();

            try
            {
                Assert.That(runtime.Context, Is.Null);
                Assert.That(runtime.ReconcileTarget, Is.Null);
                Assert.That(reusedContext.PredictionReconcileTarget, Is.Null);
            }
            finally
            {
                BattleContext.Return(reusedContext);
            }
        }

        [Test]
        public void PredictionRuntime_RebindClearsPortsAndIgnoresStaleUnbind()
        {
            var first = BattleContext.Rent();
            var second = BattleContext.Rent();
            var runtime = new BattlePredictionRuntime();
            var firstTarget = new FakeReconcileTarget();
            var secondTarget = new FakeReconcileTarget();
            try
            {
                first.BindPredictionRuntime(runtime);
                runtime.Bind(null, firstTarget, null, null);

                second.BindPredictionRuntime(runtime);
                Assert.That(runtime.ReconcileTarget, Is.Null);
                runtime.Bind(null, secondTarget, null, null);

                first.UnbindPredictionRuntime(runtime);

                Assert.That(runtime.Context, Is.SameAs(second));
                Assert.That(runtime.ReconcileTarget, Is.SameAs(secondTarget));
                Assert.That(second.PredictionReconcileTarget, Is.SameAs(secondTarget));
            }
            finally
            {
                first.UnbindPredictionRuntime(runtime);
                second.UnbindPredictionRuntime(runtime);
                BattleContext.Return(first);
                BattleContext.Return(second);
            }
        }

        private static void AssertVector3(Vector3 expected, Vector3 actual)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(0.0001f));
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(0.0001f));
            Assert.That(actual.z, Is.EqualTo(expected.z).Within(0.0001f));
        }

        private sealed class FakeActorResolutionPort : IBattleLocalActorResolutionPort
        {
            public int CachedActorId { get; set; }
            public int MappedActorId { get; set; }
            public Vector3 CachedPosition { get; set; }
            public Vector3 MappedPosition { get; set; }
            public int MapCount { get; private set; }

            public bool TryResolveMappedActorId(out int actorId)
            {
                MapCount++;
                actorId = MappedActorId;
                return actorId > 0;
            }

            public bool TryResolveActorWorldPosition(int actorId, out Vector3 position)
            {
                if (actorId == CachedActorId && CachedPosition != default)
                {
                    position = CachedPosition;
                    return true;
                }

                if (actorId == MappedActorId && MappedPosition != default)
                {
                    position = MappedPosition;
                    return true;
                }

                position = default;
                return false;
            }
        }

        private sealed class FakeReconcileTarget : IClientPredictionReconcileTarget
        {
            public void OnAuthoritativeStateHash(
                WorldId worldId,
                FrameIndex frame,
                WorldStateHash hash)
            {
            }
        }
    }
}
