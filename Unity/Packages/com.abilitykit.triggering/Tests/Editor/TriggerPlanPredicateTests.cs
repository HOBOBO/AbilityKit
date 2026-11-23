using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime.Plan;
using NUnit.Framework;

namespace AbilityKit.Triggering.Tests
{
    /// <summary>
    /// 验证正式计划的条件标记由实际传入的条件决定。
    /// </summary>
    public sealed class TriggerPlanPredicateTests
    {
        /// <summary>
        /// 创建时未传入条件的计划应标记为无条件。
        /// </summary>
        [Test]
        public void Create_WithoutPredicate_HasNoneKind()
        {
            var plan = TriggerPlan<object>.Create();

            Assert.That(plan.HasPredicate, Is.False);
            Assert.That(plan.PredicateKind, Is.EqualTo(EPredicateKind.None));
        }

        /// <summary>
        /// 指定函数条件时标记条件类型并保留函数编号。
        /// </summary>
        [Test]
        public void When_WithFunctionPredicate_PreservesFunctionId()
        {
            var predicateId = new FunctionId(100);
            var plan = TriggerPlan<object>.When(phase: 0, priority: 0, predicateId: predicateId);

            Assert.That(plan.HasPredicate, Is.True);
            Assert.That(plan.PredicateKind, Is.EqualTo(EPredicateKind.Function));
            Assert.That(plan.PredicateId, Is.EqualTo(predicateId));
        }
    }
}
