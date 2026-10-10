using System;
using AbilityKit.Ability.Host;
using AbilityKit.Demo.Moba.Share;
using AbilityKit.Game.Flow;
using AbilityKit.Game.Flow.Battle.ViewEvents;
using AbilityKit.Protocol.Moba;
using NUnit.Framework;
using FrameSnapshotDispatcher = AbilityKit.Core.Snapshots.Routing.FrameSnapshotDispatcher;
using BattleViewEventSourceMode = AbilityKit.Game.Flow.BattleViewEventSourceMode;

namespace AbilityKit.Game.Test.UnitTest
{
    public sealed class ViewEventSourceModePolicyTests
    {
        [Test]
        public void Resolve_DefaultsToSnapshotOnly_WhenContextIsNull()
        {
            var policy = new ViewEventSourceModePolicy();

            var mode = policy.Resolve(null);

            Assert.AreEqual(BattleViewEventSourceMode.SnapshotOnly, mode);
        }

        [Test]
        public void ShouldUseTriggerAdapter_MatchesTriggerAndHybridModes()
        {
            var policy = new ViewEventSourceModePolicy();

            Assert.IsFalse(policy.ShouldUseTriggerAdapter(BattleViewEventSourceMode.SnapshotOnly));
            Assert.IsTrue(policy.ShouldUseTriggerAdapter(BattleViewEventSourceMode.TriggerOnly));
            Assert.IsTrue(policy.ShouldUseTriggerAdapter(BattleViewEventSourceMode.Hybrid));
        }

        [Test]
        public void ShouldUseSnapshotAdapter_MatchesSnapshotAndHybridModes()
        {
            var policy = new ViewEventSourceModePolicy();

            Assert.IsTrue(policy.ShouldUseSnapshotAdapter(BattleViewEventSourceMode.SnapshotOnly));
            Assert.IsFalse(policy.ShouldUseSnapshotAdapter(BattleViewEventSourceMode.TriggerOnly));
            Assert.IsTrue(policy.ShouldUseSnapshotAdapter(BattleViewEventSourceMode.Hybrid));
        }

        [Test]
        public void HybridDamagePresentation_HasExactlyOneTransientOwner()
        {
            Assert.IsFalse(BattleDamagePresentationSourcePolicy.ShouldPresentTrigger(BattleViewEventSourceMode.Hybrid));
            Assert.IsTrue(BattleDamagePresentationSourcePolicy.ShouldPresentSnapshot(BattleViewEventSourceMode.Hybrid));
            Assert.IsTrue(BattleDamagePresentationSourcePolicy.ShouldPresentSnapshot(BattleViewEventSourceMode.SnapshotOnly));
            Assert.IsFalse(BattleDamagePresentationSourcePolicy.ShouldPresentTrigger(BattleViewEventSourceMode.SnapshotOnly));
        }

        [Test]
        public void TriggerAdapter_RebindsWhenSessionSourceChanges()
        {
            var context = new BattleContext
            {
                Plan = BattleStartPlanBuilder
                    .ForWorld("view-events", "battle", "client", "p1", 30, 0)
                    .WithSync(BattleSyncMode.Lockstep, BattleViewEventSourceMode.TriggerOnly)
                    .Build(),
            };
            var runtime = (IViewFeatureRuntime)new ConfirmedBattleViewFeature(context);
            runtime.EventSink = new DebugBattleViewEventSink(8);
            var lifecycle = new ViewEventAdapterLifecycle();
            var first = new ProbeTriggerSource { Available = false };
            var second = new ProbeTriggerSource();

            try
            {
                lifecycle.Attach(runtime);
                Assert.IsNull(runtime.TriggerAdapter);

                context.TriggerViewEventSource = first;
                lifecycle.Refresh(runtime);
                Assert.IsNull(runtime.TriggerAdapter);

                first.Available = true;
                lifecycle.Refresh(runtime);
                lifecycle.Refresh(runtime);
                Assert.AreEqual(2, first.SubscribeCount);
                Assert.AreEqual(1, first.SuccessfulSubscribeCount);
                Assert.IsNotNull(runtime.TriggerAdapter);

                context.TriggerViewEventSource = second;
                lifecycle.Refresh(runtime);
                Assert.AreEqual(1, first.DisposeCount);
                Assert.AreEqual(1, second.SubscribeCount);

                context.TriggerViewEventSource = null;
                lifecycle.Refresh(runtime);
                Assert.AreEqual(1, second.DisposeCount);
                Assert.IsNull(runtime.TriggerAdapter);

                context.TriggerViewEventSource = first;
                lifecycle.Refresh(runtime);
                lifecycle.Detach(runtime);
                Assert.AreEqual(2, first.DisposeCount);
                Assert.IsNull(runtime.TriggerAdapter);
            }
            finally
            {
                lifecycle.Detach(runtime);
            }
        }

        [Test]
        public void SnapshotAdapter_RebindsWhenDispatcherChanges()
        {
            var context = new BattleContext
            {
                Plan = BattleStartPlanBuilder
                    .ForWorld("view-events", "battle", "client", "p1", 30, 0)
                    .WithSync(BattleSyncMode.Lockstep, BattleViewEventSourceMode.SnapshotOnly)
                    .Build(),
            };
            var runtime = (IViewFeatureRuntime)new ConfirmedBattleViewFeature(context);
            runtime.EventSink = new DebugBattleViewEventSink(8);
            var lifecycle = new ViewEventAdapterLifecycle();
            var first = CreateViewDispatcher();
            var second = CreateViewDispatcher();
            try
            {
                lifecycle.Attach(runtime);
                Assert.IsNull(runtime.SnapshotAdapter);
                var generation = context.BindSnapshotRouting(first, null, null);
                lifecycle.Refresh(runtime);
                var firstAdapter = runtime.SnapshotAdapter;
                Assert.IsNotNull(firstAdapter);
                lifecycle.Refresh(runtime);
                Assert.AreSame(firstAdapter, runtime.SnapshotAdapter);

                Assert.IsTrue(context.ClearSnapshotRouting(generation, first));
                lifecycle.Refresh(runtime);
                Assert.IsNull(runtime.SnapshotAdapter);

                generation = context.BindSnapshotRouting(second, null, null);
                lifecycle.Refresh(runtime);
                Assert.IsNotNull(runtime.SnapshotAdapter);
                Assert.AreNotSame(firstAdapter, runtime.SnapshotAdapter);
                context.ClearSnapshotRouting(generation, second);
            }
            finally
            {
                lifecycle.Detach(runtime);
                first.Dispose();
                second.Dispose();
            }
        }

        private static FrameSnapshotDispatcher CreateViewDispatcher()
        {
            var dispatcher = new FrameSnapshotDispatcher();
            dispatcher.Register<BattleEnterGameSnapshot>(MobaOpCodes.Snapshot.EnterGame,
                (in WorldStateSnapshot _, out BattleEnterGameSnapshot value) => { value = default; return false; });
            dispatcher.Register<ActorTransformData[]>(MobaOpCodes.Snapshot.ActorTransform,
                (in WorldStateSnapshot _, out ActorTransformData[] value) => { value = null; return false; });
            dispatcher.Register<ProjectileEventData[]>(MobaOpCodes.Snapshot.ProjectileEvent,
                (in WorldStateSnapshot _, out ProjectileEventData[] value) => { value = null; return false; });
            dispatcher.Register<AreaEventData[]>(MobaOpCodes.Snapshot.AreaEvent,
                (in WorldStateSnapshot _, out AreaEventData[] value) => { value = null; return false; });
            dispatcher.Register<DamageEventData[]>(MobaOpCodes.Snapshot.DamageEvent,
                (in WorldStateSnapshot _, out DamageEventData[] value) => { value = null; return false; });
            dispatcher.Register<PresentationCueData[]>(MobaOpCodes.Snapshot.PresentationCue,
                (in WorldStateSnapshot _, out PresentationCueData[] value) => { value = null; return false; });
            return dispatcher;
        }

        private sealed class ProbeTriggerSource : IBattleTriggerViewEventSource
        {
            public int SubscribeCount;
            public int SuccessfulSubscribeCount;
            public int DisposeCount;
            public bool Available = true;

            public IDisposable Subscribe(AbilityKit.Game.Flow.Battle.ViewEvents.IBattleViewEventSink sink)
            {
                SubscribeCount++;
                if (!Available) return null;
                SuccessfulSubscribeCount++;
                return new ProbeSubscription(this);
            }

            private sealed class ProbeSubscription : IDisposable
            {
                private readonly ProbeTriggerSource _owner;
                private bool _disposed;

                public ProbeSubscription(ProbeTriggerSource owner) => _owner = owner;

                public void Dispose()
                {
                    if (_disposed) return;
                    _disposed = true;
                    _owner.DisposeCount++;
                }
            }
        }
    }
}
