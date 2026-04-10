using System;
using System.Diagnostics;
using AbilityKit.Core.Common.AttributeSystem;
using AbilityKit.Modifiers;
using NUnit.Framework;

namespace AbilityKit.Game.Test.UnitTest
{
    public sealed class AttributeModifierStorageTests
    {
        private static AttributeId CreateTempAttrId(string prefix = "test_attr")
        {
            var name = prefix + "_" + Guid.NewGuid().ToString("N");
            return AttributeRegistry.Instance.Request(name);
        }

        [Test]
        public void AddRemoveModifier_AggregatesAndValueAreCorrect()
        {
            var id = CreateTempAttrId();
            var ctx = new AttributeContext();
            var g = ctx.GetOrCreateGroup("Test");
            g.SetBase(id, 10f);

            var hAdd = g.AddModifier(id, ModifierOp.Add, 5f);
            var hMul = g.AddModifier(id, ModifierOp.Mul, 0.1f);
            var hFinal = g.AddModifier(id, ModifierOp.Add, 2f); // FinalAdd 已合并到 Add

            Assert.AreEqual(18.5f, g.GetValue(id), 0.0001f);

            Assert.IsTrue(g.RemoveModifier(id, hAdd));
            Assert.AreEqual(13f, g.GetValue(id), 0.0001f);

            Assert.IsTrue(g.RemoveModifier(id, hMul));
            Assert.AreEqual(12f, g.GetValue(id), 0.0001f);

            Assert.IsTrue(g.RemoveModifier(id, hFinal));
            Assert.AreEqual(10f, g.GetValue(id), 0.0001f);
        }

        [Test]
        public void ClearModifiers_BySource_RemovesOnlyThatSource()
        {
            var id = CreateTempAttrId();
            var ctx = new AttributeContext();
            var g = ctx.GetOrCreateGroup("Test");
            g.SetBase(id, 10f);

            g.AddModifier(id, ModifierOp.Add, 5f, 1);
            g.AddModifier(id, ModifierOp.Add, 7f, 2);

            Assert.AreEqual(22f, g.GetValue(id), 0.0001f);

            g.ClearModifiers(1);
            Assert.AreEqual(17f, g.GetValue(id), 0.0001f);

            g.ClearModifiers(2);
            Assert.AreEqual(10f, g.GetValue(id), 0.0001f);
        }

        [Test]
        public void RemoveModifier_InvalidHandle_ReturnsFalse()
        {
            var id = CreateTempAttrId();
            var ctx = new AttributeContext();
            var g = ctx.GetOrCreateGroup("Test");

            var ok = g.RemoveModifier(id, 0);
            Assert.IsFalse(ok);
        }

        [Test]
        public void Modifier_AddRemove_Stress_NoExceptions()
        {
            var id = CreateTempAttrId();
            var ctx = new AttributeContext();
            var g = ctx.GetOrCreateGroup("Test");
            g.SetBase(id, 1f);

            var sw = Stopwatch.StartNew();

            const int n = 20000;
            for (int i = 0; i < n; i++)
            {
                var h = g.AddModifier(id, ModifierOp.Add, 1f);
                Assert.IsTrue(h > 0);
                Assert.IsTrue(g.RemoveModifier(id, h));
            }

            sw.Stop();
            TestContext.WriteLine($"Attribute modifier add/remove {n} iterations took {sw.ElapsedMilliseconds} ms");

            Assert.AreEqual(1f, g.GetValue(id), 0.0001f);
        }
    }
}
