using System.Linq;
using AbilityKit.Triggering.Runtime.Config.Plans;
using AbilityKit.Triggering.Runtime.Registry;
using NUnit.Framework;

namespace AbilityKit.Triggering.Tests
{
    /// <summary>
    /// 验证保留的实例注册表契约，不将其作为计划订阅入口。
    /// </summary>
    public sealed class TriggerRegistryTests
    {
        /// <summary>
        /// 注册配置后保留显式编号，并创建可查的实例。
        /// </summary>
        [Test]
        public void RegisterConfig_PreservesExplicitIdAndCreatesInstance()
        {
            using var registry = new TriggerRegistry();
            var config = TriggerPlanConfig.Create(triggerId: 100, eventId: 1);
            using var handle = registry.RegisterConfig(config);

            Assert.That(registry.Count, Is.EqualTo(1));
            Assert.That(handle.TriggerId, Is.EqualTo(100));
            Assert.That(registry.TryGet(100, out var instance), Is.True);
            Assert.That(instance.Spec, Is.SameAs(config));
        }

        /// <summary>
        /// 编号为零的同一配置可注册两次，所得编号为不同的正数。
        /// </summary>
        [Test]
        public void RegisterConfig_AssignsDistinctPositiveIdsForZero()
        {
            using var registry = new TriggerRegistry();
            var config = TriggerPlanConfig.Create(triggerId: 0, eventId: 1);
            using var first = registry.RegisterConfig(config);
            using var second = registry.RegisterConfig(config);

            Assert.That(first.TriggerId, Is.Positive);
            Assert.That(second.TriggerId, Is.Positive.And.Not.EqualTo(first.TriggerId));
            Assert.That(registry.Count, Is.EqualTo(2));
        }

        /// <summary>
        /// 显式注销后，原编号不再能查到实例。
        /// </summary>
        [Test]
        public void Unregister_RemovesRegisteredInstance()
        {
            using var registry = new TriggerRegistry();
            using var handle = registry.RegisterConfig(TriggerPlanConfig.Create(triggerId: 100, eventId: 1));

            Assert.That(registry.Unregister(handle.TriggerId), Is.True);
            Assert.That(registry.Count, Is.Zero);
            Assert.That(registry.TryGet(handle.TriggerId, out _), Is.False);
        }

        /// <summary>
        /// 查询未知编号时返回失败且不返回实例。
        /// </summary>
        [Test]
        public void TryGet_ReturnsFalseAndNullForUnknownId()
        {
            using var registry = new TriggerRegistry();

            Assert.That(registry.TryGet(999, out var instance), Is.False);
            Assert.That(instance, Is.Null);
        }

        /// <summary>
        /// 枚举包含全部已注册的实例，不要求特定顺序。
        /// </summary>
        [Test]
        public void GetAllTriggers_ReturnsAllRegisteredInstances()
        {
            using var registry = new TriggerRegistry();
            registry.RegisterConfig(TriggerPlanConfig.Create(triggerId: 100, eventId: 1));
            registry.RegisterConfig(TriggerPlanConfig.Create(triggerId: 200, eventId: 1));

            Assert.That(registry.GetAllTriggers().Select(instance => instance.Spec.TriggerId), Is.EquivalentTo(new[] { 100, 200 }));
        }

        /// <summary>
        /// 清空移除所有条目，之后仍可继续注册。
        /// </summary>
        [Test]
        public void Clear_RemovesEntriesAndAllowsRegistration()
        {
            using var registry = new TriggerRegistry();
            registry.RegisterConfig(TriggerPlanConfig.Create(triggerId: 100, eventId: 1));
            registry.RegisterConfig(TriggerPlanConfig.Create(triggerId: 200, eventId: 1));

            registry.Clear();

            Assert.That(registry.Count, Is.Zero);
            Assert.That(registry.GetAllTriggers(), Is.Empty);
            using var handle = registry.RegisterConfig(TriggerPlanConfig.Create(triggerId: 100, eventId: 1));
            Assert.That(registry.Count, Is.EqualTo(1));
        }

        /// <summary>
        /// 释放单条句柄只注销对应编号，其他条目仍留在注册表内。
        /// </summary>
        [Test]
        public void HandleDispose_RemovesOnlyItsEntry()
        {
            using var registry = new TriggerRegistry();
            using var first = registry.RegisterConfig(TriggerPlanConfig.Create(triggerId: 100, eventId: 1));
            using var second = registry.RegisterConfig(TriggerPlanConfig.Create(triggerId: 200, eventId: 1));

            first.Dispose();

            Assert.That(registry.Count, Is.EqualTo(1));
            Assert.That(registry.TryGet(first.TriggerId, out _), Is.False);
            Assert.That(registry.TryGet(second.TriggerId, out _), Is.True);
        }

        /// <summary>
        /// 释放整个注册表后，不再保留实例条目。
        /// </summary>
        [Test]
        public void Dispose_RemovesAllEntries()
        {
            using var registry = new TriggerRegistry();
            registry.RegisterConfig(TriggerPlanConfig.Create(triggerId: 100, eventId: 1));
            registry.RegisterConfig(TriggerPlanConfig.Create(triggerId: 200, eventId: 1));

            registry.Dispose();

            Assert.That(registry.Count, Is.Zero);
            Assert.That(registry.GetAllTriggers(), Is.Empty);
        }
    }
}
