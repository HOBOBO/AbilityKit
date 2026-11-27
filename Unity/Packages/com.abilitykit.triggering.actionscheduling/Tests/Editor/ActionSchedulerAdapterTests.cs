using System.Collections.Generic;
using AbilityKit.Core.Eventing;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime.ActionScheduler;
using AbilityKit.Triggering.Runtime.Config;
using AbilityKit.Triggering.Runtime.Plan;
using AbilityKit.Triggering.Testing;
using NUnit.Framework;

namespace AbilityKit.Triggering.ActionScheduling.Tests
{
    /// <summary>
    /// 验证内置扁平动作调度经显式装配后，由测试宿主推进。
    /// </summary>
    public sealed class ActionSchedulerAdapterTests
    {
        /// <summary>
        /// 验证 Unity 编辑器程序集只导出宿主管理器和池，诊断不会暴露池内实例。
        /// </summary>
        [Test]
        public void UnityAdapter_ExportsOnlyHostTypesAndSupportsPoolDiagnostics()
        {
            Assert.That(typeof(ActionSchedulerManager).Assembly.GetExportedTypes(),
                Is.EquivalentTo(new[] { typeof(ActionSchedulerManager), typeof(ActionSchedulerPools) }));
            using var pools = new ActionSchedulerPools("adapter-test");
            pools.Prewarm();
            Assert.That(pools.GetDebugSnapshots(), Is.Not.Empty);
        }

        /// <summary>
        /// 同一槽位重复登记后，执行最新一次登记的上下文和载荷，而不是推进时的上下文。
        /// </summary>
        [Test]
        public void DelayedPlan_RunsLatestRegistrationOnlyAfterHostAdvances()
        {
            var manager = new ActionSchedulerManager();
            using var harness = new TriggeringTestHarness<int>(1, flatActionScheduler: manager);
            var observed = new List<(int Context, int Payload)>();
            var key = new EventKey<int>(1001);
            var actionId = new ActionId(2001);
            harness.RegisterAction<NamedAction0<int, object, int>>(
                actionId, (payload, _, ctx) => observed.Add((ctx.Context, payload)));
            var plan = new TriggerPlan<int>(0, 0, 3001, actions: new[] { actionId.Delayed(10f) });

            harness.RegisterPlan(key, in plan);
            harness.Publish(key, 4);
            harness.SetContext(2).Publish(key, 5);
            harness.SetContext(9);
            harness.FlushEvents();
            Assert.That(observed, Is.Empty);

            manager.Update(9f, null);
            Assert.That(observed, Is.Empty);
            manager.Update(1f, null);
            manager.Update(10f, null);
            Assert.That(observed, Is.EqualTo(new[] { (2, 5) }));
            manager.Clear();
        }

        /// <summary>
        /// 内置实现按动作配置的间隔和次数推进，不将该行为作为核心要求。
        /// </summary>
        [Test]
        public void PeriodicPlan_UsesScheduleSubPlanForIntervalsAndMaxExecutions()
        {
            var manager = new ActionSchedulerManager();
            using var harness = new TriggeringTestHarness<int>(3, flatActionScheduler: manager);
            var observed = new List<int>();
            var key = new EventKey<int>(1002);
            var actionId = new ActionId(2002);
            harness.RegisterAction<NamedAction0<int, object, int>>(
                actionId, (_, _, ctx) => observed.Add(ctx.Context));
            var call = new ActionCallPlan(
                actionId, 0, default, default, null, EActionScheduleMode.Periodic, 10f, 2, true,
                EActionExecutionPolicy.Immediate);
            var plan = new TriggerPlan<int>(0, 0, 3002, actions: new[] { call });

            harness.RegisterPlan(key, in plan);
            harness.Publish(key, 0);
            manager.Update(9f, null);
            Assert.That(observed, Is.Empty);
            manager.Update(1f, null);
            Assert.That(observed, Is.EqualTo(new[] { 3 }));
            manager.Update(9f, null);
            Assert.That(observed, Is.EqualTo(new[] { 3 }));
            manager.Update(1f, null);
            manager.Update(10f, null);
            Assert.That(observed, Is.EqualTo(new[] { 3, 3 }));
            manager.Clear();
        }
    }
}
