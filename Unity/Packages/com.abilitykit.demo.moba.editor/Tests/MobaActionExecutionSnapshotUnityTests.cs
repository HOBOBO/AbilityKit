using System;
using System.Reflection;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Context;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Trace;
using NUnit.Framework;

namespace AbilityKit.Demo.Moba.Diagnostics.Tests
{
    public sealed class MobaActionExecutionSnapshotUnityTests
    {
        [TestCase(true, false, BattleDiagnosticActionOutcome.Completed)]
        [TestCase(false, false, BattleDiagnosticActionOutcome.Failed)]
        [TestCase(false, true, BattleDiagnosticActionOutcome.Aborted)]
        public void ExecutionService_CapturesBeforeIdentityResetAndTraceEnd(bool succeeded, bool aborted, BattleDiagnosticActionOutcome outcome)
        {
            using (var trace = new MobaTraceRegistry())
            using (var store = new MobaActionExecutionSnapshotStore(trace, null, null, null, 8, () => true))
            {
                var service = Service(trace, store);
                BeginEffect(service);
                service.EnterActionExecution(0, 301);
                var id = service.CurrentActionChain[0];
                var entry = Reference(trace, id);
                if (aborted) Invoke(service, "EndCurrentTrace", (int)TraceLifecycleReason.Failed);
                else service.ExitActionExecution(0, 301, succeeded);
                var facts = store.Read(Reference(trace, id));
                Assert.That(facts.IsCaptured, Is.True);
                Assert.That(facts.HasAfter, Is.True);
                Assert.That(facts.ActionId, Is.EqualTo(301));
                Assert.That(facts.Outcome, Is.EqualTo(outcome));
                Assert.That(facts.Frame, Is.EqualTo(10));
                Assert.That(store.Read(entry).HasAfter, Is.False);
                Assert.That(trace.TryGetNodeSnapshot(id, out var node), Is.True);
                Assert.That(node.IsEnded, Is.True);
                service.ExitActionExecution(0, 301, succeeded);
                Assert.That(store.Read(Reference(trace, id)), Is.EqualTo(facts));
            }
        }

        [Test]
        public void NestedEffect_ExecutionServicePreservesSeparateActionPairs()
        {
            using (var trace = new MobaTraceRegistry())
            using (var store = new MobaActionExecutionSnapshotStore(trace, null, null, null, 8, () => true))
            {
                var service = Service(trace, store);
                BeginEffect(service);
                service.EnterActionExecution(0, 301);
                var outer = service.CurrentActionChain[0];
                BeginEffect(service, outer);
                service.EnterActionExecution(1, 302);
                var inner = service.CurrentActionChain[0];
                service.ExitActionExecution(1, 302, false);
                Invoke(service, "EndCurrentTrace", (int)TraceLifecycleReason.Completed);
                service.ExitActionExecution(0, 301, true);
                Assert.That(store.Read(Reference(trace, outer)).Outcome, Is.EqualTo(BattleDiagnosticActionOutcome.Completed));
                Assert.That(store.Read(Reference(trace, inner)).Outcome, Is.EqualTo(BattleDiagnosticActionOutcome.Failed));
            }
        }

        [Test]
        public void ThrowingObservation_DoesNotPreventNormalOrAbortedExecution()
        {
            using (var trace = new MobaTraceRegistry())
            {
                var hook = new ThrowingHook();
                var service = Service(trace, hook);
                BeginEffect(service);
                Assert.DoesNotThrow(() => service.EnterActionExecution(0, 301));
                Assert.DoesNotThrow(() => service.ExitActionExecution(0, 301, true));
                Assert.DoesNotThrow(() => service.EnterActionExecution(1, 302));
                Assert.DoesNotThrow(() => Invoke(service, "EndCurrentTrace", (int)TraceLifecycleReason.Failed));
                Assert.That(hook.Starts, Is.EqualTo(2));
                Assert.That(hook.Ends, Is.EqualTo(2));
            }
        }

        private static MobaEffectExecutionService Service(MobaTraceRegistry trace, IMobaActionExecutionSnapshotHook hook)
        {
            var service = new MobaEffectExecutionService();
            typeof(MobaEffectExecutionService).GetProperty("Trace").SetValue(service, trace);
            var time = new FrameTime();
            time.Reset(new FrameIndex(10), 0f, 0.02f);
            typeof(MobaEffectExecutionService).GetField("_frameTime", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(service, time);
            typeof(MobaEffectExecutionService).GetField("_actionSnapshotHook", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(service, hook);
            return service;
        }
        private static void BeginEffect(MobaEffectExecutionService service, long parent = 0)
        {
            var lineage = new MobaEffectLineageInput(EffectContextKind.Skill, MobaTraceKind.SkillEffect,
                7, 9, parent, 0, 0, 101);
            Invoke(service, "BeginEffectTraceScope", 101, 201, lineage);
        }
        private static void Invoke(object target, string name, params object[] args) =>
            target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);
        private static ContextSnapshotReference Reference(MobaTraceRegistry trace, long id)
        {
            Assert.That(trace.TryGetNodeSnapshot(id, out var node), Is.True);
            return ((MobaTraceMetadata)node.Metadata).ActionSnapshot;
        }
        private sealed class ThrowingHook : IMobaActionExecutionSnapshotHook
        {
            public int Starts;
            public int Ends;
            public void OnActionStarted(long contextId, int actionIndex, long actionId, long sourceActorId, long targetActorId, int frame)
            { Starts++; throw new InvalidOperationException("observation failure"); }
            public void OnActionEnded(long contextId, int actionIndex, long actionId, bool succeeded, bool aborted, int frame)
            { Ends++; throw new InvalidOperationException("observation failure"); }
        }
    }
}
