using System;
using System.Collections.Generic;
using AbilityKit.Triggering.Eventing;
using AbilityKit.Triggering.Blackboard;
using AbilityKit.Triggering.Payload;
using AbilityKit.Triggering.Variables.Numeric;
using AbilityKit.Triggering.Variables.Numeric.Expression;
using AbilityKit.Triggering.Runtime.Abstractions;
using AbilityKit.Triggering.Runtime.Config;
using AbilityKit.Triggering.Runtime.Plan;
using AbilityKit.Triggering.Runtime.Random;
using AbilityKit.Triggering.Runtime.Time;
using AbilityKit.Triggering.Runtime.Context;
using NUnit.Framework;

namespace AbilityKit.Triggering.Tests
{
    /// <summary>
    /// TriggerDispatcherHub 测试
    /// </summary>
    public class TriggerDispatcherHubTests
    {
        private TriggerDispatcherHub _hub;
        private TriggerContext _context;
        private int _executedCount;

        [SetUp]
        public void Setup()
        {
            _hub = new TriggerDispatcherHub();
            _context = new TriggerContext(
                blackboards: new NullBlackboardResolver(),
                eventBus: new EventBus(),
                frameClock: new UnityFrameClock(),
                random: new DeterministicRandom(),
                functions: new FunctionRegistry(),
                actions: new ActionRegistry(),
                payloads: new PayloadAccessorRegistry(),
                idNames: new IdNameRegistry(),
                numericDomains: new NumericVarDomainRegistry(),
                numericFunctions: new NumericRpnFunctionRegistry());
            _hub.SetContext(_context);
            _executedCount = 0;
        }

        [TearDown]
        public void Teardown()
        {
            _hub?.Dispose();
        }

        [Test]
        public void RegisterAuto_WithTransient_ShouldUseEventDispatcher()
        {
            var plan = new TriggerPlan<object>(
                phase: 0,
                priority: 0,
                triggerId: 1,
                actions: Array.Empty<ActionCallPlan>(),
                interruptPriority: 0,
                cue: null,
                schedule: ScheduleModePlan.None); // Transient by default

            TriggerPredicate<object> predicate = null;
            TriggerExecutor<object> executor = (args, ctx) => _executedCount++;

            _hub.RegisterAuto(plan, predicate, executor);

            Assert.That(_hub.TotalRegisteredCount, Is.EqualTo(1));
            Assert.That(_hub.Event, Is.Not.Null);
        }

        [Test]
        public void RegisterAuto_WithContinuous_ShouldUseTimedDispatcher()
        {
            var plan = new TriggerPlan<object>(
                phase: 0,
                priority: 0,
                triggerId: 1,
                actions: Array.Empty<ActionCallPlan>(),
                interruptPriority: 0,
                cue: null,
                schedule: ScheduleModePlan.Continuous(16.667f, -1));

            TriggerPredicate<object> predicate = null;
            TriggerExecutor<object> executor = (args, ctx) => _executedCount++;

            _hub.RegisterAuto(plan, predicate, executor);

            Assert.That(_hub.TotalRegisteredCount, Is.EqualTo(1));
            Assert.That(_hub.Timed, Is.Not.Null);
            Assert.That(_hub.Timed.RegisteredCount, Is.EqualTo(1));
        }

        [Test]
        public void Update_WithTimedDispatcher_ShouldIncrementExecution()
        {
            var plan = new TriggerPlan<object>(
                phase: 0,
                priority: 0,
                triggerId: 1,
                actions: Array.Empty<ActionCallPlan>(),
                interruptPriority: 0,
                cue: null,
                schedule: ScheduleModePlan.Continuous(0f, 3)); // 每帧执行，最多3次

            TriggerExecutor<object> executor = (args, ctx) => _executedCount++;

            _hub.RegisterAuto(plan, null, executor);

            // 更新3帧
            _hub.Update(0, _context);
            _hub.Update(16, _context);
            _hub.Update(32, _context);

            Assert.That(_executedCount, Is.EqualTo(3));
        }

        [Test]
        public void Unregister_ShouldRemoveFromAllDispatchers()
        {
            var plan = new TriggerPlan<object>(
                phase: 0,
                priority: 0,
                triggerId: 1,
                actions: Array.Empty<ActionCallPlan>());

            TriggerExecutor<object> executor = (args, ctx) => { };

            _hub.RegisterAuto(plan, null, executor);

            Assert.That(_hub.TotalRegisteredCount, Is.EqualTo(1));

            var result = _hub.Unregister(1);

            Assert.That(result, Is.True);
            Assert.That(_hub.TotalRegisteredCount, Is.EqualTo(0));
        }

        [Test]
        public void GetActiveContinuousCount_ShouldReturnCorrectCount()
        {
            var plan1 = new TriggerPlan<object>(
                phase: 0, priority: 0, triggerId: 1,
                actions: Array.Empty<ActionCallPlan>(),
                schedule: ScheduleModePlan.Continuous(16.667f, -1));

            var plan2 = new TriggerPlan<object>(
                phase: 0, priority: 0, triggerId: 2,
                actions: Array.Empty<ActionCallPlan>(),
                schedule: ScheduleModePlan.Continuous(16.667f, -1));

            TriggerExecutor<object> executor = (args, ctx) => { };

            _hub.RegisterAuto(plan1, null, executor);
            _hub.RegisterAuto(plan2, null, executor);

            Assert.That(_hub.GetActiveContinuousCount(), Is.EqualTo(2));
        }

        [Test]
        public void InterruptAllContinuous_ShouldStopAllContinuous()
        {
            var plan = new TriggerPlan<object>(
                phase: 0, priority: 0, triggerId: 1,
                actions: Array.Empty<ActionCallPlan>(),
                schedule: ScheduleModePlan.Continuous(16.667f, 10)); // 10次

            TriggerExecutor<object> executor = (args, ctx) => _executedCount++;

            _hub.RegisterAuto(plan, null, executor);

            _hub.Update(0, _context); // 执行1次

            _hub.InterruptAllContinuous("Test interrupt");

            Assert.That(_hub.GetActiveContinuousCount(), Is.EqualTo(0));
            Assert.That(_executedCount, Is.EqualTo(1));
        }

        [Test]
        public void Update_WithPredicate_ShouldEvaluateBeforeExecution()
        {
            var plan = new TriggerPlan<object>(
                phase: 0, priority: 0, triggerId: 1,
                actions: Array.Empty<ActionCallPlan>(),
                schedule: ScheduleModePlan.Continuous(16.667f, -1));

            bool predicateResult = false;

            TriggerPredicate<object> predicate = (args, ctx) => predicateResult;
            TriggerExecutor<object> executor = (args, ctx) => _executedCount++;

            _hub.RegisterAuto(plan, predicate, executor);

            _hub.Update(0, _context); // predicateResult = false, 不执行
            Assert.That(_executedCount, Is.EqualTo(0));

            predicateResult = true;
            _hub.Update(16, _context); // predicateResult = true, 执行
            Assert.That(_executedCount, Is.EqualTo(1));
        }
    }

    /// <summary>
    /// 空黑板解析器（测试用）
    /// </summary>
    internal sealed class NullBlackboardResolver : IBlackboardResolver
    {
        public T Read<T>(string key) => default;
        public bool Write<T>(string key, T value) => false;
        public bool TryRead<T>(string key, out T value) { value = default; return false; }
        public bool HasKey(string key) => false;
    }
}