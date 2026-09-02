#if UNITY_EDITOR

#nullable enable

using System;
using NUnit.Framework;

namespace AbilityKit.Pipeline.Editor.Tests
{
    public sealed class PipelineDebuggerContextModelTests
    {
        [Test]
        public void Rebuild_ProjectsSortedUnionWithFallbacks()
        {
            var model = new PipelineDebuggerContextModel();

            model.Rebuild(
                Values(("Zulu", "before"), ("Alpha", "same")),
                Values(("Alpha", "same"), ("Beta", "added")),
                showOnlyChanged: false,
                search: null);

            Assert.That(model.VisibleRows.Count, Is.EqualTo(3));
            AssertRow(model.VisibleRows[0], "Alpha", "same", "same", false);
            AssertRow(
                model.VisibleRows[1],
                "Beta",
                PipelineDebuggerContextModel.InitialValueFallback,
                "added",
                true);
            AssertRow(
                model.VisibleRows[2],
                "Zulu",
                "before",
                PipelineDebuggerContextModel.CurrentValueFallback,
                true);
        }

        [Test]
        public void Rebuild_AppliesChangedAndSearchFiltersTogether()
        {
            var model = new PipelineDebuggerContextModel();

            model.Rebuild(
                Values(("Health", "10"), ("Mana", "5"), ("Name", "Mage")),
                Values(("Health", "8"), ("Mana", "5"), ("Name", "Wizard")),
                showOnlyChanged: true,
                search: "wizard");

            Assert.That(model.VisibleRows.Count, Is.EqualTo(1));
            AssertRow(model.VisibleRows[0], "Name", "Mage", "Wizard", true);
        }

        [Test]
        public void Rebuild_ClearsPreviousProjection()
        {
            var model = new PipelineDebuggerContextModel();
            model.Rebuild(
                Values(("First", "1")),
                Values(("First", "2")),
                showOnlyChanged: false,
                search: null);

            model.Rebuild(
                Array.Empty<EditorPipelineRegistry.DebugValue>(),
                Values(("Second", "2")),
                showOnlyChanged: false,
                search: null);

            Assert.That(model.VisibleRows.Count, Is.EqualTo(1));
            Assert.That(model.VisibleRows[0].Name, Is.EqualTo("Second"));
        }

        [Test]
        public void Rebuild_PreservesOrdinalNameIdentity()
        {
            var model = new PipelineDebuggerContextModel();

            model.Rebuild(
                Values(("Value", "upper")),
                Values(("value", "lower")),
                showOnlyChanged: false,
                search: null);

            Assert.That(model.VisibleRows.Count, Is.EqualTo(2));
            Assert.That(model.VisibleRows[0].Name, Is.EqualTo("Value"));
            Assert.That(model.VisibleRows[1].Name, Is.EqualTo("value"));
        }

        [Test]
        public void BuildVisibleText_PreservesExistingCopyFormat()
        {
            var model = new PipelineDebuggerContextModel();
            model.Rebuild(
                Values(("Changed", "old"), ("Stable", "same")),
                Values(("Changed", "new"), ("Stable", "same")),
                showOnlyChanged: false,
                search: null);

            Assert.That(
                model.BuildVisibleText(),
                Is.EqualTo(
                    "Changed = old -> new" + Environment.NewLine
                    + "Stable = same"));
        }

        [Test]
        public void Rebuild_RejectsNullSnapshots()
        {
            var model = new PipelineDebuggerContextModel();
            var values = Array.Empty<EditorPipelineRegistry.DebugValue>();

            Assert.Throws<ArgumentNullException>(() => model.Rebuild(
                null!,
                values,
                showOnlyChanged: false,
                search: null));
            Assert.Throws<ArgumentNullException>(() => model.Rebuild(
                values,
                null!,
                showOnlyChanged: false,
                search: null));
        }

        private static EditorPipelineRegistry.DebugValue[] Values(
            params (string Name, string Value)[] values)
        {
            var result = new EditorPipelineRegistry.DebugValue[values.Length];
            for (int i = 0; i < values.Length; i++)
            {
                result[i] = new EditorPipelineRegistry.DebugValue(
                    values[i].Name,
                    values[i].Value);
            }

            return result;
        }

        private static void AssertRow(
            PipelineDebuggerContextRow row,
            string name,
            string initial,
            string current,
            bool changed)
        {
            Assert.That(row.Name, Is.EqualTo(name));
            Assert.That(row.InitialValue, Is.EqualTo(initial));
            Assert.That(row.CurrentValue, Is.EqualTo(current));
            Assert.That(row.IsChanged, Is.EqualTo(changed));
        }
    }
}

#endif
