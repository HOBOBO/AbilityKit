#if UNITY_EDITOR
using System.Collections.Generic;
using AbilityKit.Ability.Config.Authoring;
using AbilityKit.Ability.Editor.Panels;
using AbilityKit.Ability.Editor.Utilities;
using NUnit.Framework;

namespace AbilityKit.Ability.Editor.Tests
{
    public sealed class TriggerAuthoringTriggerTableModelTests
    {
        [Test]
        public void BuildRows_SortsDisplayedValuesAndKeepsModuleIndices()
        {
            var first = new TriggerDefinitionData { Id = 20, Name = "治疗", Priority = 2 };
            var second = new TriggerDefinitionData { Id = 10, Name = "伤害", Priority = 8 };
            var entries = new List<TriggerAuthoringTriggerIndex.Entry>
            {
                new TriggerAuthoringTriggerIndex.Entry(
                    4,
                    first,
                    new TriggerAuthoringTriggerIndex.DiagnosticSummary(0, 1)),
                new TriggerAuthoringTriggerIndex.Entry(
                    1,
                    second,
                    new TriggerAuthoringTriggerIndex.DiagnosticSummary(1, 0))
            };

            var rows = TriggerAuthoringTriggerTableModel.BuildRows(
                entries,
                TriggerAuthoringTriggerTableColumn.Priority,
                false);

            Assert.That(rows, Has.Count.EqualTo(2));
            Assert.That(rows[0].Trigger, Is.SameAs(second));
            Assert.That(rows[0].Index, Is.EqualTo(1));
            Assert.That(rows[1].Trigger, Is.SameAs(first));
            Assert.That(rows[1].Index, Is.EqualTo(4));
        }

        [Test]
        public void CollectIndices_UsesExplicitRowsInsteadOfWholeFilterResult()
        {
            var entries = new List<TriggerAuthoringTriggerIndex.Entry>
            {
                Entry(0, 100),
                Entry(1, 200),
                Entry(2, 300)
            };
            var rows = TriggerAuthoringTriggerTableModel.BuildRows(
                entries,
                TriggerAuthoringTriggerTableColumn.Id,
                true);

            var indices = TriggerAuthoringTriggerTableModel.CollectIndices(
                rows,
                new[] { rows[0].RowId, rows[2].RowId });

            Assert.That(indices, Is.EqualTo(new[] { 0, 2 }));
        }

        [Test]
        public void TemplateRow_UsesEffectiveValuesButIdentifiesReadOnlyColumns()
        {
            var instance = new TriggerDefinitionData
            {
                Id = 10,
                Name = "模板实例",
                Template = new TriggerTemplateReferenceData { TemplateId = "damage.function" }
            };
            var effective = new TriggerDefinitionData
            {
                Id = 10,
                Name = "模板实例",
                Event = "damage.received",
                Priority = 50
            };
            var entries = new[]
            {
                new TriggerAuthoringTriggerIndex.Entry(
                    0,
                    instance,
                    effective,
                    new TriggerAuthoringTriggerIndex.DiagnosticSummary(0, 0))
            };

            var rows = TriggerAuthoringTriggerTableModel.BuildRows(
                entries,
                TriggerAuthoringTriggerTableColumn.Id,
                true);

            Assert.That(rows[0].UsesTemplate, Is.True);
            Assert.That(rows[0].EffectiveTrigger.Event, Is.EqualTo("damage.received"));
            Assert.That(rows[0].EffectiveTrigger.Priority, Is.EqualTo(50));
        }

        [Test]
        public void BuildRows_SortsWholeResultBeforePageCanBeTaken()
        {
            var entries = new List<TriggerAuthoringTriggerIndex.Entry>
            {
                Entry(0, 50),
                Entry(1, 10),
                Entry(2, 40),
                Entry(3, 20),
                Entry(4, 30)
            };

            var rows = TriggerAuthoringTriggerTableModel.BuildRows(
                entries,
                TriggerAuthoringTriggerTableColumn.Id,
                true);
            var secondPage = rows.GetRange(2, 2);

            Assert.That(secondPage[0].Trigger.Id, Is.EqualTo(30));
            Assert.That(secondPage[1].Trigger.Id, Is.EqualTo(40));
        }

        private static TriggerAuthoringTriggerIndex.Entry Entry(int index, int id)
        {
            var trigger = new TriggerDefinitionData { Id = id, Name = "Trigger " + id };
            return new TriggerAuthoringTriggerIndex.Entry(
                index,
                trigger,
                new TriggerAuthoringTriggerIndex.DiagnosticSummary(0, 0));
        }
    }
}
#endif
