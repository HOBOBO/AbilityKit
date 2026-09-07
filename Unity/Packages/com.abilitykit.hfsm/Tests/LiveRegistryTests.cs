using System.Linq;
using NUnit.Framework;
using AbilityKit.HFSM;
using AbilityKit.HFSM.Visualization;

using AbilityKit.HFSM.Definition;
using AbilityKit.HFSM.Runtime;
namespace AbilityKit.Tests
{
    public sealed class LiveRegistryTests
    {
        [Test]
        public void CallbackRegistrationRefreshesSnapshot()
        {
            var fsm = new object();
            var callbackCalls = 0;
            try
            {
                LiveRegistry.Register("runtime", fsm, _ => callbackCalls++);

                LiveRegistry.UpdateSnapshot(fsm);

                Assert.That(callbackCalls, Is.EqualTo(1));
                Assert.That(LiveRegistry.FindEntry("runtime").Snapshot.snapshotTime, Is.GreaterThanOrEqualTo(0f));
            }
            finally
            {
                LiveRegistry.Unregister(fsm);
            }
        }

        [Test]
        public void ReRegistrationReplacesLookupEntryAndProvider()
        {
            var fsm = new object();
            var firstCalls = 0;
            var secondCalls = 0;
            try
            {
                LiveRegistry.Register("first", fsm, _ => firstCalls++);
                LiveRegistry.Register("second", fsm, _ => secondCalls++);

                LiveRegistry.UpdateSnapshot(fsm);

                Assert.That(LiveRegistry.FindEntry("first"), Is.Null);
                Assert.That(LiveRegistry.FindEntry("second"), Is.Not.Null);
                Assert.That(firstCalls, Is.Zero);
                Assert.That(secondCalls, Is.EqualTo(1));
            }
            finally
            {
                LiveRegistry.Unregister(fsm);
            }
        }

        [Test]
        public void ReflectionProviderReadsPublicLegacyStateMachineSurface()
        {
            var fsm = new StateMachine { RegisterForInspection = false };
            fsm.AddState("idle", new State());
            fsm.SetStartState("idle");
            fsm.Init();
            try
            {
                LiveRegistry.Register("legacy", fsm);

                LiveRegistry.UpdateSnapshot(fsm);

                var snapshot = LiveRegistry.FindEntry("legacy").Snapshot;
                Assert.That(snapshot.states.Exists(state => state.name == "idle"), Is.True);
                Assert.That(snapshot.activeStatePaths, Contains.Item("idle"));
            }
            finally
            {
                LiveRegistry.Unregister(fsm);
            }
        }

        [Test]
        public void StateMachineUsesStronglyTypedProviderAndNestedPaths()
        {
            var nested = new StateMachine { RegisterForInspection = false };
            nested.AddState("child", new State());

            var fsm = new StateMachine { RegisterForInspection = false };
            fsm.AddState("sub", nested);
            fsm.AddState("idle", new State());
            fsm.SetStartState("sub");
            fsm.Init();
            try
            {
                LiveRegistry.Register("typed", fsm);
                LiveRegistry.UpdateSnapshot(fsm);

                var entry = LiveRegistry.FindEntry("typed");
                Assert.That(entry.Provider, Is.TypeOf<StateMachineVisualizationProvider>());
                Assert.That(entry.Snapshot.states.Any(state => state.path == "sub" && state.isStateMachine), Is.True);
                Assert.That(entry.Snapshot.states.Any(state => state.path == "sub/child"), Is.True);
                Assert.That(entry.Snapshot.activeStatePaths, Does.Contain("sub"));
                Assert.That(entry.Snapshot.activeStatePaths, Does.Contain("sub/child"));
            }
            finally
            {
                LiveRegistry.Unregister(fsm);
            }
        }

        [Test]
        public void StronglyTypedProviderHandlesUninitializedMachineAndReusesSnapshotCollections()
        {
            var fsm = new StateMachine { RegisterForInspection = false };
            fsm.AddState("idle", new State());
            try
            {
                LiveRegistry.Register("uninitialized", fsm);
                Assert.DoesNotThrow(() => LiveRegistry.UpdateSnapshot(fsm));

                var entry = LiveRegistry.FindEntry("uninitialized");
                var firstStateCount = entry.Snapshot.states.Count;
                LiveRegistry.UpdateSnapshot(fsm);

                Assert.That(entry.Snapshot.activeStatePaths, Is.Empty);
                Assert.That(entry.Snapshot.states.Count, Is.EqualTo(firstStateCount));
                Assert.That(entry.Snapshot.states.Count(state => state.path == "idle"), Is.EqualTo(1));
            }
            finally
            {
                LiveRegistry.Unregister(fsm);
            }
        }

        [Test]
        public void StronglyTypedProviderLimitsRecordedHistory()
        {
            var fsm = new StateMachine { RegisterForInspection = false };
            var provider = new StateMachineVisualizationProvider(fsm, historyCapacity: 2);

            provider.RecordTransition("a", "b", "first");
            provider.RecordTransition("b", "c", "second");
            provider.RecordTransition("c", "d", "third");

            var history = provider.GetHistory(50).ToArray();
            Assert.That(history.Length, Is.EqualTo(2));
            Assert.That(history[0].trigger, Is.EqualTo("second"));
            Assert.That(history[1].trigger, Is.EqualTo("third"));
        }

        [Test]
        public void StronglyTypedProviderCapturesAnyAndForceTransitionMetadata()
        {
            var fsm = new StateMachine { RegisterForInspection = false };
            fsm.AddState("idle", new State());
            fsm.AddState("attack", new State());
            fsm.AddTransitionFromAny(
                new TransitionBase(string.Empty, "attack", forceInstantly: true));
            fsm.Init();

            var snapshot = new StateMachineVisualizationProvider(fsm).GetSnapshot();
            var transition = snapshot.transitions.Single();

            Assert.That(transition.isFromAny, Is.True);
            Assert.That(transition.forceInstantly, Is.True);
            Assert.That(transition.toPath, Is.EqualTo("attack"));
        }

        [Test]
        public void StronglyTypedProviderCapturesAndClearsPendingStatePaths()
        {
            var fsm = new StateMachine { RegisterForInspection = false };
            fsm.AddState(
                "idle",
                new State(
                    canExit: _ => false,
                    needsExitTime: true));
            fsm.AddState("attack", new State());
            fsm.SetStartState("idle");
            fsm.Init();

            var provider = new StateMachineVisualizationProvider(fsm);
            fsm.RequestStateChange("attack");

            var pendingSnapshot = provider.GetSnapshot();
            Assert.That(pendingSnapshot.activeStatePaths, Does.Contain("idle"));
            Assert.That(pendingSnapshot.exitingStatePaths, Does.Contain("idle"));
            Assert.That(pendingSnapshot.pendingStatePaths, Does.Contain("attack"));
            Assert.That(
                pendingSnapshot.FindState("idle").Value.isExiting,
                Is.True);
            Assert.That(
                pendingSnapshot.FindState("attack").Value.isEntering,
                Is.True);

            fsm.StateCanExit();

            var completedSnapshot = provider.GetSnapshot();
            Assert.That(completedSnapshot.activeStatePaths, Does.Contain("attack"));
            Assert.That(completedSnapshot.exitingStatePaths, Is.Empty);
            Assert.That(completedSnapshot.pendingStatePaths, Is.Empty);
            Assert.That(
                completedSnapshot.FindState("idle").Value.isExiting,
                Is.False);
            Assert.That(
                completedSnapshot.FindState("attack").Value.isEntering,
                Is.False);
        }

        [Test]
        public void StronglyTypedProviderDoesNotInventPendingTargetForVerticalExit()
        {
            var fsm = new StateMachine(needsExitTime: true)
            {
                RegisterForInspection = false
            };
            fsm.AddState(
                "idle",
                new State(
                    canExit: _ => false,
                    needsExitTime: true));
            fsm.SetStartState("idle");
            fsm.Init();

            var provider = new StateMachineVisualizationProvider(fsm);
            fsm.RequestExit();

            var snapshot = provider.GetSnapshot();
            Assert.That(fsm.IsPendingExitTransition, Is.True);
            Assert.That(fsm.PendingState, Is.Null);
            Assert.That(snapshot.exitingStatePaths, Does.Contain("idle"));
            Assert.That(snapshot.pendingStatePaths, Is.Empty);
            Assert.That(snapshot.FindState("idle").Value.isExiting, Is.True);
            Assert.That(snapshot.states.Any(state => state.isEntering), Is.False);
        }

        [Test]
        public void StateMachineLifecycleUsesTheSharedInspectionRegistry()
        {
            RuntimeRegistry.AutoRegisterEnabled = true;
            var fsm = new StateMachine();
            fsm.AddState("idle", new State());
            fsm.SetStartState("idle");
            try
            {
                fsm.Init();

                Assert.That(LiveRegistry.GetEntries().Any(entry => ReferenceEquals(entry.Target, fsm)), Is.True);

                fsm.OnExit();
                Assert.That(LiveRegistry.GetEntries().Any(entry => ReferenceEquals(entry.Target, fsm)), Is.False);
            }
            finally
            {
                LiveRegistry.Unregister(fsm);
            }
        }
    }
}
