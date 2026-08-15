using System;
using System.Collections.Generic;
using AbilityKit.Continuous;
using AbilityKit.Demo.Moba.Config.BattleDemo.MO;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Share.Config;
using NUnit.Framework;

namespace AbilityKit.Demo.Moba.Diagnostics.Tests
{
    public sealed class MobaContinuousTransactionTests
    {
        [Test]
        public void ActiveSnapshot_PreservesRegistrationOrder()
        {
            var manager = new DefaultContinuousManager();
            var first = CreateRuntime(1, 1001);
            var second = CreateRuntime(2, 1002);
            var third = CreateRuntime(3, 1003);

            Assert.That(manager.TryActivate(first), Is.True);
            Assert.That(manager.TryActivate(second), Is.True);
            Assert.That(manager.TryActivate(third), Is.True);

            var active = manager.GetAllActiveContinuous();

            Assert.That(active.Count, Is.EqualTo(3));
            Assert.That(active[0], Is.SameAs(first));
            Assert.That(active[1], Is.SameAs(second));
            Assert.That(active[2], Is.SameAs(third));
        }

        [Test]
        public void Tick_WhenMultipleIntervalsElapsed_CatchesUpInHandlerOrder()
        {
            var calls = new List<string>();
            var firstHandler = new RecordingHandler("first", calls);
            var secondHandler = new RecordingHandler("second", calls);
            var processor = new MobaContinuousTickProcessor(
                new IMobaContinuousIntervalHandler[] { firstHandler, secondHandler });
            var runtime = CreateRuntime(intervalSeconds: 1, sourceContextId: 2001);
            runtime.Activate();

            processor.Tick(runtime, 3.25f);

            Assert.That(calls, Is.EqualTo(new[]
            {
                "first", "second",
                "first", "second",
                "first", "second",
            }));
            Assert.That(runtime.IntervalRemainingSeconds, Is.EqualTo(0.75f).Within(0.0001f));
        }

        [Test]
        public void Tick_WhenCatchUpBudgetIsExceeded_PreservesNegativeRemainderForNextTick()
        {
            var handler = new RecordingHandler("tick", new List<string>());
            var processor = new MobaContinuousTickProcessor(
                new IMobaContinuousIntervalHandler[] { handler });
            var runtime = CreateRuntime(intervalSeconds: 1, sourceContextId: 3001);
            runtime.Activate();

            processor.Tick(runtime, MobaContinuousTickProcessor.MaxIntervalExecutionsPerTick + 2.25f);

            Assert.That(handler.CallCount, Is.EqualTo(MobaContinuousTickProcessor.MaxIntervalExecutionsPerTick));
            Assert.That(runtime.IntervalRemainingSeconds, Is.EqualTo(-1.25f).Within(0.0001f));

            processor.Tick(runtime, 0.25f);

            Assert.That(handler.CallCount, Is.EqualTo(MobaContinuousTickProcessor.MaxIntervalExecutionsPerTick + 2));
            Assert.That(runtime.IntervalRemainingSeconds, Is.EqualTo(0.5f).Within(0.0001f));
        }

        [Test]
        public void Register_WhenBinderFails_RollsBackIndexesAndCompensatesAttemptedBindersInReverseOrder()
        {
            var calls = new List<string>();
            var first = new RecordingBinder("first", calls);
            var failing = new RecordingBinder("failing", calls, throwOnRegistered: true);
            var manager = new DefaultContinuousManager(
                lifecycleBinders: new IContinuousLifecycleBinder[] { first, failing });
            var runtime = CreateRuntime(1, 4001);

            var error = Assert.Throws<InvalidOperationException>(() => manager.Register(runtime));

            Assert.That(error.Message, Is.EqualTo("binder registration failed"));
            Assert.That(manager.TotalCount, Is.Zero);
            Assert.That(manager.ActiveCount, Is.Zero);
            Assert.That(manager.GetAllContinuous(), Is.Empty);
            Assert.That(manager.GetOwnerContinuous(runtime.Config.OwnerId), Is.Empty);
            Assert.That(calls, Is.EqualTo(new[]
            {
                "first.register",
                "failing.register",
                "failing.unregister",
                "first.unregister",
            }));
        }

        private static MobaTriggerIntervalContinuousRuntime CreateRuntime(
            int intervalSeconds,
            long sourceContextId)
        {
            var process = new ContinuousProcessMO(new ContinuousProcessDTO
            {
                Id = (int)sourceContextId,
                Name = "continuous-transaction-test",
                DurationMs = 120000,
                IntervalMs = intervalSeconds * 1000,
                IntervalTriggerIds = new[] { 9001 },
                TriggerIds = Array.Empty<int>(),
                TagNames = Array.Empty<string>(),
                Modifiers = Array.Empty<ContinuousModifierDTO>(),
            });
            return new MobaTriggerIntervalContinuousRuntime(
                process,
                sourceActorId: 11,
                targetActorId: 12,
                sourceContextId: sourceContextId,
                requirements: null);
        }

        private sealed class RecordingHandler : IMobaContinuousIntervalHandler
        {
            private readonly string _name;
            private readonly List<string> _calls;

            public RecordingHandler(string name, List<string> calls)
            {
                _name = name;
                _calls = calls;
            }

            public int CallCount { get; private set; }

            public bool CanHandle(IContinuous continuous)
            {
                return continuous is MobaTriggerIntervalContinuousRuntime;
            }

            public void OnInterval(
                IContinuous continuous,
                IMobaContinuousPeriodicConfig periodicConfig,
                in MobaCombatExecutionContext executionContext)
            {
                CallCount++;
                _calls.Add(_name);
            }
        }

        private sealed class RecordingBinder : IContinuousLifecycleBinder
        {
            private readonly string _name;
            private readonly List<string> _calls;
            private readonly bool _throwOnRegistered;

            public RecordingBinder(string name, List<string> calls, bool throwOnRegistered = false)
            {
                _name = name;
                _calls = calls;
                _throwOnRegistered = throwOnRegistered;
            }

            public void OnRegistered(IContinuous continuous, IContinuousManager manager)
            {
                _calls.Add(_name + ".register");
                if (_throwOnRegistered)
                {
                    throw new InvalidOperationException("binder registration failed");
                }
            }

            public void OnActivated(IContinuous continuous, IContinuousManager manager)
            {
            }

            public void OnPaused(IContinuous continuous, IContinuousManager manager)
            {
            }

            public void OnResumed(IContinuous continuous, IContinuousManager manager)
            {
            }

            public void OnEnded(
                IContinuous continuous,
                ContinuousEndReason reason,
                IContinuousManager manager)
            {
            }

            public void OnUnregistered(
                IContinuous continuous,
                ContinuousEndReason reason,
                IContinuousManager manager)
            {
                _calls.Add(_name + ".unregister");
            }
        }
    }
}
