using System.Collections.Generic;
using AbilityKit.Ability.Config.Authoring;
using AbilityKit.Ability.Editor.Utilities;
using NUnit.Framework;

namespace AbilityKit.Ability.Editor.Tests
{
    public sealed class TriggerAuthoringTriggerIndexTests
    {
        [Test]
        public void Build_GroupsByEventCatalogCategoryAndEvent()
        {
            var groups = TriggerAuthoringTriggerIndex.Build(
                CreateTriggers(),
                new List<TriggerAuthoringDiagnostic>(),
                CreateEvents(),
                TriggerAuthoringTriggerGroupMode.Event,
                string.Empty);

            Assert.That(groups.Count, Is.EqualTo(3));
            Assert.That(groups.Exists(group => group.Label == "Event / Buff/buff.apply"), Is.True);
            Assert.That(groups.Exists(group => group.Label == "Event / Skill/skill.cast.start"), Is.True);
            Assert.That(groups.Exists(group => group.Label == "Event / <unassigned>"), Is.True);
        }

        [Test]
        public void Build_GroupsByStatusWithDiagnosticPrecedence()
        {
            var diagnostics = new List<TriggerAuthoringDiagnostic>
            {
                new TriggerAuthoringDiagnostic(
                    "TRG_TEST",
                    TriggerAuthoringDiagnosticSeverity.Error,
                    "module.triggers[1].actions",
                    "Broken action")
            };

            var groups = TriggerAuthoringTriggerIndex.Build(
                CreateTriggers(),
                diagnostics,
                CreateEvents(),
                TriggerAuthoringTriggerGroupMode.Status,
                string.Empty);

            Assert.That(groups[0].Label, Is.EqualTo("Errors"));
            Assert.That(groups[0].Entries[0].Index, Is.EqualTo(1));
            Assert.That(groups.Exists(group => group.Label == "Disabled"), Is.True);
            Assert.That(groups.Exists(group => group.Label == "Ready"), Is.True);
        }

        [Test]
        public void Build_SearchMatchesNestedNodeValuesAndDiagnostics()
        {
            var diagnostics = new List<TriggerAuthoringDiagnostic>
            {
                new TriggerAuthoringDiagnostic(
                    "TRG_NESTED",
                    TriggerAuthoringDiagnosticSeverity.Warning,
                    "module.triggers[0].actions.children[0]",
                    "Check nested node")
            };

            var nodeSearch = TriggerAuthoringTriggerIndex.Build(
                CreateTriggers(),
                diagnostics,
                CreateEvents(),
                TriggerAuthoringTriggerGroupMode.Flat,
                "debug_log");
            var diagnosticSearch = TriggerAuthoringTriggerIndex.Build(
                CreateTriggers(),
                diagnostics,
                CreateEvents(),
                TriggerAuthoringTriggerGroupMode.Flat,
                "TRG_NESTED");

            Assert.That(nodeSearch.Count, Is.EqualTo(1));
            Assert.That(nodeSearch[0].Entries.Count, Is.EqualTo(1));
            Assert.That(nodeSearch[0].Entries[0].Index, Is.EqualTo(0));
            Assert.That(diagnosticSearch.Count, Is.EqualTo(1));
            Assert.That(diagnosticSearch[0].Entries[0].Index, Is.EqualTo(0));
        }

        [Test]
        public void Build_GroupsByBusinessGroupPathAndSearchesTags()
        {
            var groupPath = TriggerAuthoringTriggerIndex.Build(
                CreateTriggers(),
                new List<TriggerAuthoringDiagnostic>(),
                CreateEvents(),
                TriggerAuthoringTriggerGroupMode.GroupPath,
                string.Empty);
            var tagSearch = TriggerAuthoringTriggerIndex.Build(
                CreateTriggers(),
                new List<TriggerAuthoringDiagnostic>(),
                CreateEvents(),
                TriggerAuthoringTriggerGroupMode.Flat,
                "burst");

            Assert.That(groupPath.Exists(group => group.Label == "Group / Combat/Buffs"), Is.True);
            Assert.That(groupPath.Exists(group => group.Label == "Group / Combat/Skills"), Is.True);
            Assert.That(tagSearch.Count, Is.EqualTo(1));
            Assert.That(tagSearch[0].Entries.Count, Is.EqualTo(1));
            Assert.That(tagSearch[0].Entries[0].Index, Is.EqualTo(1));
        }

        [Test]
        public void Build_GroupsByTagAndAllowsMultiTagMembership()
        {
            var groups = TriggerAuthoringTriggerIndex.Build(
                CreateTriggers(),
                new List<TriggerAuthoringDiagnostic>(),
                CreateEvents(),
                TriggerAuthoringTriggerGroupMode.Tag,
                string.Empty);

            Assert.That(groups.Exists(group => group.Label == "Tag / buff"), Is.True);
            Assert.That(groups.Exists(group => group.Label == "Tag / state"), Is.True);
            Assert.That(groups.Exists(group => group.Label == "Tag / burst"), Is.True);
            Assert.That(
                groups.Find(group => group.Label == "Tag / buff").Entries.Exists(entry => entry.Index == 0),
                Is.True);
            Assert.That(
                groups.Find(group => group.Label == "Tag / state").Entries.Exists(entry => entry.Index == 0),
                Is.True);
        }

        [Test]
        public void BatchOperations_CollectVisibleIndicesOnceAcrossMultiTagGroups()
        {
            var triggers = CreateTriggers();
            var groups = TriggerAuthoringTriggerIndex.Build(
                triggers,
                new List<TriggerAuthoringDiagnostic>(),
                CreateEvents(),
                TriggerAuthoringTriggerGroupMode.Tag,
                string.Empty);

            var indices = TriggerAuthoringTriggerBatchOperations.CollectVisibleTriggerIndices(groups);
            var changed = TriggerAuthoringTriggerBatchOperations.AddTags(triggers, indices, "visible, buff");

            Assert.That(indices, Is.EqualTo(new[] { 0, 1, 2 }));
            Assert.That(TriggerAuthoringTriggerBatchOperations.ContainsVisibleTriggerIndex(indices, 1), Is.True);
            Assert.That(TriggerAuthoringTriggerBatchOperations.ContainsVisibleTriggerIndex(indices, 9), Is.False);
            Assert.That(changed, Is.EqualTo(3));
            Assert.That(triggers[0].Tags.FindAll(tag => tag == "visible").Count, Is.EqualTo(1));
            Assert.That(triggers[0].Tags.FindAll(tag => tag == "buff").Count, Is.EqualTo(1));
        }

        [Test]
        public void BatchOperations_EditEnabledGroupPathTagsAndCopyIds()
        {
            var triggers = CreateTriggers();
            var indices = new List<int> { 0, 2 };

            Assert.That(TriggerAuthoringTriggerBatchOperations.SetEnabled(triggers, indices, false), Is.EqualTo(1));
            Assert.That(TriggerAuthoringTriggerBatchOperations.SetGroupPath(triggers, indices, "Combat/Reworked"), Is.EqualTo(2));
            Assert.That(TriggerAuthoringTriggerBatchOperations.AddTags(triggers, indices, "review, Review, qa"), Is.EqualTo(2));
            Assert.That(TriggerAuthoringTriggerBatchOperations.RemoveTags(triggers, indices, "draft, missing"), Is.EqualTo(1));

            Assert.That(triggers[0].Enabled, Is.False);
            Assert.That(triggers[0].GroupPath, Is.EqualTo("Combat/Reworked"));
            Assert.That(triggers[0].Tags, Does.Contain("review"));
            Assert.That(triggers[0].Tags, Does.Contain("qa"));
            Assert.That(triggers[2].Tags, Does.Not.Contain("draft"));
            Assert.That(TriggerAuthoringTriggerBatchOperations.BuildTriggerIdList(triggers, indices), Is.EqualTo("10, 12"));
        }

        [Test]
        public void Build_AppliesQuickFiltersBeforeGroupingAndSearch()
        {
            var triggers = CreateTriggers();
            triggers.Add(new TriggerDefinitionData
            {
                Id = 13,
                Name = "Needs Metadata",
                Enabled = true,
                GroupPath = string.Empty
            });
            var diagnostics = new List<TriggerAuthoringDiagnostic>
            {
                new TriggerAuthoringDiagnostic(
                    "TRG_TEST",
                    TriggerAuthoringDiagnosticSeverity.Error,
                    "module.triggers[1].actions",
                    "Broken action")
            };

            var errors = TriggerAuthoringTriggerIndex.Build(
                triggers,
                diagnostics,
                CreateEvents(),
                TriggerAuthoringTriggerGroupMode.Flat,
                string.Empty,
                TriggerAuthoringTriggerQuickFilter.Errors);
            var disabled = TriggerAuthoringTriggerIndex.Build(
                triggers,
                diagnostics,
                CreateEvents(),
                TriggerAuthoringTriggerGroupMode.Flat,
                string.Empty,
                TriggerAuthoringTriggerQuickFilter.Disabled);
            var noEvent = TriggerAuthoringTriggerIndex.Build(
                triggers,
                diagnostics,
                CreateEvents(),
                TriggerAuthoringTriggerGroupMode.Flat,
                string.Empty,
                TriggerAuthoringTriggerQuickFilter.NoEvent);
            var noGroup = TriggerAuthoringTriggerIndex.Build(
                triggers,
                diagnostics,
                CreateEvents(),
                TriggerAuthoringTriggerGroupMode.Flat,
                string.Empty,
                TriggerAuthoringTriggerQuickFilter.NoGroup);
            var untagged = TriggerAuthoringTriggerIndex.Build(
                triggers,
                diagnostics,
                CreateEvents(),
                TriggerAuthoringTriggerGroupMode.Flat,
                string.Empty,
                TriggerAuthoringTriggerQuickFilter.Untagged);

            Assert.That(errors[0].Entries[0].Index, Is.EqualTo(1));
            Assert.That(disabled[0].Entries[0].Index, Is.EqualTo(2));
            Assert.That(noEvent[0].Entries.Exists(entry => entry.Index == 2), Is.True);
            Assert.That(noEvent[0].Entries.Exists(entry => entry.Index == 3), Is.True);
            Assert.That(noGroup[0].Entries[0].Index, Is.EqualTo(3));
            Assert.That(untagged[0].Entries[0].Index, Is.EqualTo(3));
        }

        private static List<TriggerDefinitionData> CreateTriggers()
        {
            return new List<TriggerDefinitionData>
            {
                new TriggerDefinitionData
                {
                    Id = 10,
                    Name = "Apply Buff",
                    GroupPath = "Combat/Buffs",
                    Tags = { "buff", "state" },
                    Event = "buff.apply",
                    Phase = "immediate",
                    Scope = "owner",
                    Priority = 10,
                    Actions = new TriggerNodeData
                    {
                        Kind = TriggerNodeKind.Action,
                        Type = "seq",
                        Children =
                        {
                            new TriggerNodeData
                            {
                                Kind = TriggerNodeKind.Action,
                                Type = "debug_log",
                                Arguments =
                                {
                                    new TriggerArgumentData
                                    {
                                        Name = "message",
                                        Value = new TriggerValueRefData
                                        {
                                            Source = TriggerValueSource.Constant,
                                            Type = TriggerValueType.String,
                                            StringValue = "nested search target"
                                        }
                                    }
                                }
                            }
                        }
                    }
                },
                new TriggerDefinitionData
                {
                    Id = 11,
                    Name = "Cast Skill",
                    GroupPath = "Combat/Skills",
                    Tags = { "skill", "burst" },
                    Event = "skill.cast.start",
                    Phase = "late",
                    Scope = "global",
                    Priority = 5
                },
                new TriggerDefinitionData
                {
                    Id = 12,
                    Name = "Disabled Draft",
                    GroupPath = "Drafts",
                    Tags = { "draft" },
                    Enabled = false,
                    Phase = "immediate",
                    Scope = "owner"
                }
            };
        }

        private static TriggerEventDescriptorCatalog CreateEvents()
        {
            return new TriggerEventDescriptorCatalog(new[]
            {
                new TriggerEventDefinitionData
                {
                    Id = "buff.apply",
                    Category = "Buff"
                },
                new TriggerEventDefinitionData
                {
                    Id = "skill.cast.start",
                    Category = "Skill"
                }
            });
        }
    }
}
