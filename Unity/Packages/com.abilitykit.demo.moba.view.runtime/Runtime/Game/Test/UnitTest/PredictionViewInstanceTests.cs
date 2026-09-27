using System.Collections.Generic;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Core.Logging;
using AbilityKit.Game.Battle.Component;
using AbilityKit.Game.Battle.Entity;
using AbilityKit.Game.Battle.Hierarchy;
using AbilityKit.Game.Flow;
using AbilityKit.Game.Flow.Battle.ViewEvents;
using AbilityKit.Network.Battle.Projection;
using NUnit.Framework;
using UnityEngine;

namespace AbilityKit.Game.Test.UnitTest
{
    public sealed class PredictionViewInstanceTests
    {
        [Test]
        public void TwoPredictionProjections_CreateUpdateAndRemoveActorsIndependently()
        {
            var first = ConfirmedViewContextFactory.Create(null, default);
            var second = ConfirmedViewContextFactory.Create(null, default);
            try
            {
                var producer = new TestProjectionProducer();
                var firstBridge = new PredictionViewBridge(first.EntityWorld, first.EntityLookup,
                    first, new Vector3(100, 0, 0));
                var secondBridge = new PredictionViewBridge(second.EntityWorld, second.EntityLookup,
                    second, new Vector3(-100, 0, 0));

                producer.Set(1, 3);
                firstBridge.SyncAllActors(producer);
                secondBridge.SyncAllActors(producer);

                Assert.That(Position(first, 1).x, Is.EqualTo(103));
                Assert.That(Position(second, 1).x, Is.EqualTo(-97));
                Assert.That(first.DirtyEntities.Count, Is.GreaterThan(0));
                Assert.That(second.DirtyEntities.Count, Is.GreaterThan(0));

                producer.Set(1, 7);
                firstBridge.SyncAllActors(producer);
                Assert.That(Position(first, 1).x, Is.EqualTo(107));
                Assert.That(Position(second, 1).x, Is.EqualTo(-97));

                producer.Clear();
                firstBridge.SyncAllActors(producer);
                Assert.That(first.EntityLookup.TryResolve(first.EntityWorld,
                    new BattleNetId(1), out _), Is.False);
                Assert.That(second.EntityLookup.TryResolve(second.EntityWorld,
                    new BattleNetId(1), out _), Is.True);

                secondBridge.SyncAllActors(producer);
                Assert.That(second.EntityLookup.TryResolve(second.EntityWorld,
                    new BattleNetId(1), out _), Is.False);
            }
            finally
            {
                ConfirmedViewContextDisposer.Dispose(first, entity => entity.Destroy());
                ConfirmedViewContextDisposer.Dispose(second, entity => entity.Destroy());
            }
        }

        [Test]
        public void ViewSessionCreatesIndependentResourceProviders()
        {
            var session = BattlePresentationSessionContext.CreateDefault();
            var first = session.CreateViewResources();
            var second = session.CreateViewResources();

            Assert.That(first, Is.Not.SameAs(second));
            Assert.That(first, Is.Not.SameAs(session.Resources));
        }

        [Test]
        public void PredictionViewOwner_AttachesTwoInstancesAndDisposesSeparately()
        {
            var world = new AbilityKit.World.ECS.EntityWorld();
            var root = world.Create("flow-root");
            var flow = new GameFlowDomain((IGameHost)null, root);
            var owner = new BattlePresentationSessionResources();
            var source = BattleContext.Rent();
            var leftProducer = new TestProjectionProducer();
            var rightProducer = new TestProjectionProducer();
            var leftSource = new TestProjectionViewSource(new WorldId("left"), leftProducer);
            var rightSource = new TestProjectionViewSource(new WorldId("right"), rightProducer);
            try
            {
                Assert.That(owner.AddProjectionView("left", BattleProjectionViewRole.Prediction,
                    leftSource, source, flow,
                    new Vector3(100, 0, 0), entity => entity.Destroy()), Is.True);
                Assert.That(root.TryGetRef(out ProjectedBattleViewFeature firstBound), Is.True);
                var actorOnlyView = (IViewFeatureRuntime)firstBound;
                Assert.That(actorOnlyView.Vfx, Is.Null);
                Assert.That(actorOnlyView.EventSink, Is.Null);
                Assert.That(actorOnlyView.AreaVfxPool, Is.Null);
                Assert.That(actorOnlyView.CameraController, Is.Null);
                Assert.That(owner.AddProjectionView("right", BattleProjectionViewRole.Auxiliary,
                    rightSource, source, flow,
                    new Vector3(-100, 0, 0), entity => entity.Destroy(),
                    BattleProjectionViewCapabilities.Events), Is.True);
                Assert.That(owner.ProjectionViewCount, Is.EqualTo(2));
                Assert.That(owner.PredictionViewCount, Is.EqualTo(1));
                Assert.That(owner.AddProjectionView("left", BattleProjectionViewRole.Auxiliary,
                    rightSource, source, flow,
                    Vector3.zero, entity => entity.Destroy()), Is.False);
                Assert.That(owner.AddProjectionView("missing-events", BattleProjectionViewRole.Auxiliary,
                    new ActorOnlyProjectionViewSource(leftProducer), source, flow,
                    Vector3.zero, entity => entity.Destroy(),
                    BattleProjectionViewCapabilities.Events), Is.False);
                Assert.That(owner.ProjectionViewCount, Is.EqualTo(2));
                Assert.That(root.TryGetRef(out ConfirmedBattleViewFeature confirmedBound), Is.False);
                Assert.That(owner.TryGetProjectionView("right", out var rightInfo), Is.True);
                Assert.That(rightInfo.Role, Is.EqualTo(BattleProjectionViewRole.Auxiliary));
                Assert.That(rightInfo.SourceWorldId, Is.EqualTo(rightSource.WorldId));
                Assert.That(rightInfo.Capabilities & BattleProjectionViewCapabilities.Vfx,
                    Is.EqualTo(BattleProjectionViewCapabilities.Vfx));
                var registered = new List<BattleProjectionViewInfo>();
                owner.GetProjectionViews(registered);
                Assert.That(registered.Count, Is.EqualTo(2));
                Assert.That(owner.TryGetProjectionContext("left", out var leftContext), Is.True);
                Assert.That(owner.TryGetProjectionContext("right", out var rightContext), Is.True);

                leftProducer.Set(1, 3);
                rightProducer.Set(2, 7);
                owner.SyncProjectionViews();
                Assert.That(Position(leftContext, 1).x, Is.EqualTo(103));
                Assert.That(Position(rightContext, 2).x, Is.EqualTo(-93));
                Assert.That(leftContext.EntityLookup.TryResolve(leftContext.EntityWorld,
                    new BattleNetId(2), out _), Is.False);
                Assert.That(leftSource.SubscribeCount, Is.Zero);
                Assert.That(rightSource.SubscribeCount, Is.EqualTo(1));
                Assert.That(rightSource.LastEventContext.WorldOffset.x, Is.EqualTo(-100));
                Assert.That(rightSource.LastEventContext.CurrentFrame, Is.EqualTo(10));

                leftSource.Producer = null;
                owner.SyncProjectionViews();
                Assert.That(leftContext.EntityLookup.TryResolve(leftContext.EntityWorld,
                    new BattleNetId(1), out _), Is.False);
                Assert.That(Position(rightContext, 2).x, Is.EqualTo(-93));
                Assert.That(owner.ProjectionViewCount, Is.EqualTo(2));
                leftSource.Producer = leftProducer;
                owner.SyncProjectionViews();
                Assert.That(Position(leftContext, 1).x, Is.EqualTo(103));

                Assert.That(rightContext.EntityLookup.TryResolve(rightContext.EntityWorld,
                    new BattleNetId(2), out var oldRight), Is.True);
                rightSource.ProjectionEpoch++;
                owner.SyncProjectionViews();
                Assert.That(oldRight.IsValid, Is.False);
                Assert.That(Position(rightContext, 2).x, Is.EqualTo(-93));
                Assert.That(rightSource.SubscribeCount, Is.EqualTo(2));
                Assert.That(rightSource.DisposeCount, Is.EqualTo(1));
                Assert.That(rightSource.LastEventContext.ProjectionEpoch, Is.EqualTo(1));

                rightSource.Producer = null;
                owner.SyncProjectionViews();
                Assert.That(rightContext.EntityLookup.TryResolve(rightContext.EntityWorld,
                    new BattleNetId(2), out _), Is.False);
                Assert.That(rightSource.DisposeCount, Is.EqualTo(2));
                rightSource.Producer = rightProducer;
                owner.SyncProjectionViews();
                Assert.That(Position(rightContext, 2).x, Is.EqualTo(-93));
                Assert.That(rightSource.SubscribeCount, Is.EqualTo(3));

                leftSource.Producer = leftProducer;
                leftSource.ThrowOnRead = true;
                var originalSink = Log.Sink;
                try
                {
                    Log.SetSink(NullLogSink.Instance);
                    Assert.DoesNotThrow(owner.SyncProjectionViews);
                }
                finally
                {
                    Log.SetSink(originalSink);
                }
                Assert.That(Position(rightContext, 2).x, Is.EqualTo(-93));
                leftSource.ThrowOnRead = false;
                owner.SyncProjectionViews();
                Assert.That(Position(leftContext, 1).x, Is.EqualTo(103));

                var roots = Object.FindObjectsOfType<BattleViewHierarchyRoot>(true);
                Assert.That(roots.Length, Is.EqualTo(2));
                Assert.That(roots[0], Is.Not.SameAs(roots[1]));

                Assert.That(owner.RemoveProjectionView("left"), Is.True);
                Assert.That(owner.ProjectionViewCount, Is.EqualTo(1));
                Assert.That(root.TryGetRef(out ProjectedBattleViewFeature remainingBound), Is.True);
                Assert.That(remainingBound, Is.Not.SameAs(firstBound));
                Assert.That(owner.RemoveProjectionView("left"), Is.False);
                owner.DisposeProjectionViews();
                owner.DisposeProjectionViews();
                Assert.That(owner.ProjectionViewCount, Is.Zero);
                Assert.That(rightSource.DisposeCount, Is.EqualTo(3));
                Assert.That(root.TryGetRef(out ProjectedBattleViewFeature projectedAfterDispose), Is.False);
            }
            finally
            {
                owner.DisposeProjectionViews();
                BattleContext.Return(source);
                if (root.IsValid) root.Destroy();
                flow.Shutdown();
            }
        }

        private sealed class TestProjectionViewSource : IBattleProjectionViewSource,
            IBattleProjectionViewEpochSource, IBattleProjectionViewEventSource
        {
            public TestProjectionViewSource(WorldId worldId, IActorProjectionProducer producer)
            {
                WorldId = worldId;
                Producer = producer;
            }

            public WorldId WorldId { get; }
            public IActorProjectionProducer Producer { get; set; }
            public long ProjectionEpoch { get; set; }
            public int SubscribeCount { get; private set; }
            public int DisposeCount { get; private set; }
            public BattleProjectionViewEventContext LastEventContext { get; private set; }
            public bool ThrowOnRead { get; set; }

            public System.IDisposable SubscribeEvents(IBattleViewEventSink sink,
                BattleProjectionViewEventContext context)
            {
                Assert.That(sink, Is.Not.Null);
                LastEventContext = context;
                SubscribeCount++;
                return new TestSubscription(() => DisposeCount++);
            }

            public bool TryGetProjection(out IActorProjectionProducer producer, out int frame)
            {
                if (ThrowOnRead) throw new System.InvalidOperationException("source unavailable");
                producer = Producer;
                frame = 10;
                return producer != null;
            }
        }

        private sealed class ActorOnlyProjectionViewSource : IBattleProjectionViewSource
        {
            private readonly IActorProjectionProducer _producer;

            public ActorOnlyProjectionViewSource(IActorProjectionProducer producer) =>
                _producer = producer;

            public WorldId WorldId => new WorldId("actor-only");

            public bool TryGetProjection(out IActorProjectionProducer producer, out int frame)
            {
                producer = _producer;
                frame = 0;
                return true;
            }
        }

        private sealed class TestSubscription : System.IDisposable
        {
            private readonly System.Action _dispose;
            private bool _disposed;

            public TestSubscription(System.Action dispose) => _dispose = dispose;

            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                _dispose();
            }
        }

        private static Vector3 Position(BattleContext context, int actorId)
        {
            Assert.That(context.EntityLookup.TryResolve(context.EntityWorld,
                new BattleNetId(actorId), out var entity), Is.True);
            Assert.That(entity.TryGetRef(out BattleTransformComponent transform), Is.True);
            return transform.Position;
        }

        private sealed class TestProjectionProducer : IActorProjectionProducer
        {
            private readonly Dictionary<int, float> _positions = new Dictionary<int, float>();

            public void Set(int actorId, float x) => _positions[actorId] = x;
            public void Clear() => _positions.Clear();

            public ActorProjectionData ExtractFull(int actorId) =>
                _positions.TryGetValue(actorId, out var x)
                    ? Create(actorId, x, ActorProjectionFields.FullState)
                    : default;

            public ActorProjectionData ExtractSpawn(int actorId) =>
                _positions.TryGetValue(actorId, out var x)
                    ? Create(actorId, x, ActorProjectionFields.SpawnInfo)
                    : default;

            public void ExtractAll(List<ActorProjectionData> buffer)
            {
                buffer.Clear();
                foreach (var actor in _positions)
                    buffer.Add(ExtractFull(actor.Key));
            }

            private static ActorProjectionData Create(int actorId, float x,
                ActorProjectionFields fields) =>
                new ActorProjectionData(actorId, x, 0, 0, 0, 0, 0, 1,
                    1, 1, 1, 100, 100, 1, 0, 0, 1, 42, 0, fields);
        }
    }
}
