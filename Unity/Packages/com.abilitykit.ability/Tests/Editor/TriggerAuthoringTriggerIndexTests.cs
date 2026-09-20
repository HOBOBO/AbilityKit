using System.Collections.Generic;
using AbilityKit.Ability.Config.Authoring;
using AbilityKit.Ability.Editor.Utilities;
using NUnit.Framework;
using UnityEngine;

namespace AbilityKit.Ability.Editor.Tests
{
    public sealed class TriggerAuthoringTriggerIndexTests
    {
        [Test]
        public void ScaleBenchmarkGenerator_CreatesStableUniqueNodesAtOneThousandTriggers()
        {
            var first = TriggerAuthoringScaleBenchmark.CreateModule(1000);
            var second = TriggerAuthoringScaleBenchmark.CreateModule(1000);
            var diagnostics = TriggerAuthoringValidator.Validate(first);

            Assert.That(first.Triggers, Has.Count.EqualTo(1000));
            Assert.That(TriggerAuthoringScaleBenchmark.CountNodes(first), Is.EqualTo(21000));
            Assert.That(first.Triggers[0].Actions.NodeId, Is.EqualTo(second.Triggers[0].Actions.NodeId));
            Assert.That(first.Triggers[999].Actions.Children[19].NodeId,
                Is.EqualTo(second.Triggers[999].Actions.Children[19].NodeId));

            var nodeIds = new HashSet<string>();
            for (var i = 0; i < first.Triggers.Count; i++)
            {
                Assert.That(nodeIds.Add(first.Triggers[i].Actions.NodeId), Is.True);
                for (var childIndex = 0; childIndex < first.Triggers[i].Actions.Children.Count; childIndex++)
                    Assert.That(nodeIds.Add(first.Triggers[i].Actions.Children[childIndex].NodeId), Is.True);
            }
            Assert.That(nodeIds, Has.Count.EqualTo(21000));
            Assert.That(diagnostics.Exists(item =>
                item.Severity == TriggerAuthoringDiagnosticSeverity.Error), Is.False);
        }

        [Test]
        public void ScaleBenchmarkGenerator_ValidatesRoundTripsExportsAndSearchesExpectedTrigger()
        {
            var module = TriggerAuthoringScaleBenchmark.CreateModule(100, 4);
            var diagnostics = TriggerAuthoringValidator.Validate(module);
            var errors = diagnostics.FindAll(item =>
                item.Severity == TriggerAuthoringDiagnosticSeverity.Error);

            var sourceJson = TriggerSourceCodecs.ModuleDefault.Serialize(new TriggerAuthoringSourceDocument
            {
                Module = module
            });
            var roundTrip = TriggerSourceCodecs.ModuleDefault.Deserialize(sourceJson);
            var runtime = TriggerAuthoringRuntimeExporter.Build(module);
            var search = TriggerAuthoringTriggerIndex.Build(
                module.Triggers,
                diagnostics,
                null,
                TriggerAuthoringTriggerGroupMode.Flat,
                "Benchmark Trigger 000042");

            Assert.That(errors, Is.Empty);
            Assert.That(roundTrip.Module.Triggers, Has.Count.EqualTo(100));
            Assert.That(TriggerAuthoringScaleBenchmark.CountNodes(roundTrip.Module), Is.EqualTo(500));
            Assert.That(runtime.Success, Is.True, runtime.BuildMessage());
            Assert.That(runtime.ExportedTriggerCount, Is.EqualTo(100));
            Assert.That(search, Has.Count.EqualTo(1));
            Assert.That(search[0].Entries, Has.Count.EqualTo(1));
            Assert.That(search[0].Entries[0].Trigger.Id, Is.EqualTo(43));
        }

        [Test]
        public void ScaleBenchmarkReport_RecordsAllStagesWithoutTimingThresholds()
        {
            var report = TriggerAuthoringScaleBenchmark.Run(10, 3, false);

            Assert.That(report.TriggerCount, Is.EqualTo(10));
            Assert.That(report.NodeCount, Is.EqualTo(40));
            Assert.That(report.SourceJsonBytes, Is.GreaterThan(0));
            Assert.That(report.RuntimeJsonBytes, Is.GreaterThan(0));
            Assert.That(report.RuntimeBuildSuccess, Is.True, report.RuntimeBuildMessage);
            Assert.That(report.Stages, Has.Count.EqualTo(14));
            Assert.That(report.FindStage("searchIndexPrepare").ItemCount, Is.EqualTo(10));
            Assert.That(report.FindStage("runtimePlanBuildPrevalidated").ItemCount, Is.EqualTo(10));
            Assert.That(report.FindStage("searchHit").ItemCount, Is.EqualTo(1));
            Assert.That(report.FindStage("searchMiss").ItemCount, Is.Zero);
            Assert.That(report.Stages.TrueForAll(stage => stage.ElapsedMilliseconds >= 0d), Is.True);
        }

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
            Assert.That(groups.Exists(group => group.Label == "事件 / Buff/buff.apply"), Is.True);
            Assert.That(groups.Exists(group => group.Label == "事件 / Skill/skill.cast.start"), Is.True);
            Assert.That(groups.Exists(group => group.Label == "事件 / 未分配"), Is.True);
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

            Assert.That(groups[0].Label, Is.EqualTo("存在错误"));
            Assert.That(groups[0].Entries[0].Index, Is.EqualTo(1));
            Assert.That(groups.Exists(group => group.Label == "已停用"), Is.True);
            Assert.That(groups.Exists(group => group.Label == "就绪"), Is.True);
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
        public void PreparedSearch_ReusesNestedAndDiagnosticDocumentsAcrossQueries()
        {
            var triggers = CreateTriggers();
            var diagnostics = new List<TriggerAuthoringDiagnostic>
            {
                new TriggerAuthoringDiagnostic(
                    "TRG_PREPARED",
                    TriggerAuthoringDiagnosticSeverity.Warning,
                    "module.triggers[1].actions",
                    "Prepared diagnostic target")
            };
            var prepared = TriggerAuthoringTriggerIndex.PrepareSearch(triggers, diagnostics);

            var nested = TriggerAuthoringTriggerIndex.Build(
                triggers,
                diagnostics,
                null,
                TriggerAuthoringTriggerGroupMode.Flat,
                "nested search target",
                TriggerAuthoringTriggerQuickFilter.All,
                null,
                prepared);
            var diagnostic = TriggerAuthoringTriggerIndex.Build(
                triggers,
                diagnostics,
                null,
                TriggerAuthoringTriggerGroupMode.Flat,
                "TRG_PREPARED",
                TriggerAuthoringTriggerQuickFilter.All,
                null,
                prepared);

            Assert.That(prepared.Count, Is.EqualTo(3));
            Assert.That(nested[0].Entries, Has.Count.EqualTo(1));
            Assert.That(nested[0].Entries[0].Index, Is.Zero);
            Assert.That(diagnostic[0].Entries, Has.Count.EqualTo(1));
            Assert.That(diagnostic[0].Entries[0].Index, Is.EqualTo(1));
        }

        [Test]
        public void Build_AggregatesDiagnosticsInOneTriggerBucketWithoutPrefixCollisions()
        {
            var triggers = new List<TriggerDefinitionData>();
            for (var i = 0; i <= 10; i++)
                triggers.Add(new TriggerDefinitionData { Id = 100 + i, Name = "Trigger " + i });
            var diagnostics = new List<TriggerAuthoringDiagnostic>
            {
                new TriggerAuthoringDiagnostic(
                    "TRG_ONE",
                    TriggerAuthoringDiagnosticSeverity.Warning,
                    "module.triggers[1].actions",
                    "Trigger one warning"),
                new TriggerAuthoringDiagnostic(
                    "TRG_TEN",
                    TriggerAuthoringDiagnosticSeverity.Error,
                    "module.triggers[10]",
                    "Trigger ten error"),
                new TriggerAuthoringDiagnostic(
                    "TRG_MODULE",
                    TriggerAuthoringDiagnosticSeverity.Error,
                    "module.triggers.invalid",
                    "Module-level error")
            };

            var groups = TriggerAuthoringTriggerIndex.Build(
                triggers,
                diagnostics,
                null,
                TriggerAuthoringTriggerGroupMode.Flat,
                string.Empty);
            var entries = groups[0].Entries;

            Assert.That(entries[1].Diagnostics.Errors, Is.Zero);
            Assert.That(entries[1].Diagnostics.Warnings, Is.EqualTo(1));
            Assert.That(entries[10].Diagnostics.Errors, Is.EqualTo(1));
            Assert.That(entries[10].Diagnostics.Warnings, Is.Zero);

            var search = TriggerAuthoringTriggerIndex.Build(
                triggers,
                diagnostics,
                null,
                TriggerAuthoringTriggerGroupMode.Flat,
                "TRG_TEN");
            Assert.That(search[0].Entries, Has.Count.EqualTo(1));
            Assert.That(search[0].Entries[0].Index, Is.EqualTo(10));
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

            Assert.That(groupPath.Exists(group => group.Label == "分组 / Combat/Buffs"), Is.True);
            Assert.That(groupPath.Exists(group => group.Label == "分组 / Combat/Skills"), Is.True);
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

            Assert.That(groups.Exists(group => group.Label == "关键词 / buff"), Is.True);
            Assert.That(groups.Exists(group => group.Label == "关键词 / state"), Is.True);
            Assert.That(groups.Exists(group => group.Label == "关键词 / burst"), Is.True);
            Assert.That(
                groups.Find(group => group.Label == "关键词 / buff").Entries.Exists(entry => entry.Index == 0),
                Is.True);
            Assert.That(
                groups.Find(group => group.Label == "关键词 / state").Entries.Exists(entry => entry.Index == 0),
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
        public void BatchOperations_PaginateGroupsCountsDuplicateMembershipOnce()
        {
            var first = new TriggerAuthoringTriggerIndex.Group("a", "A", "a");
            first.Entries.Add(new TriggerAuthoringTriggerIndex.Entry(
                0,
                new TriggerDefinitionData { Id = 10 },
                default));
            first.Entries.Add(new TriggerAuthoringTriggerIndex.Entry(
                1,
                new TriggerDefinitionData { Id = 11 },
                default));
            var second = new TriggerAuthoringTriggerIndex.Group("b", "B", "b");
            second.Entries.Add(first.Entries[0]);
            second.Entries.Add(new TriggerAuthoringTriggerIndex.Entry(
                2,
                new TriggerDefinitionData { Id = 12 },
                default));

            var firstPage = TriggerAuthoringTriggerBatchOperations.PaginateGroups(
                new[] { first, second },
                0,
                2,
                out var totalCount,
                out var pageCount,
                out var firstPageIndex);
            var lastPage = TriggerAuthoringTriggerBatchOperations.PaginateGroups(
                new[] { first, second },
                99,
                2,
                out _,
                out _,
                out var lastPageIndex);

            Assert.That(totalCount, Is.EqualTo(3));
            Assert.That(pageCount, Is.EqualTo(2));
            Assert.That(firstPageIndex, Is.Zero);
            Assert.That(lastPageIndex, Is.EqualTo(1));
            Assert.That(
                TriggerAuthoringTriggerBatchOperations.CollectVisibleTriggerIndices(firstPage),
                Is.EqualTo(new[] { 0, 1 }));
            Assert.That(
                TriggerAuthoringTriggerBatchOperations.CollectVisibleTriggerIndices(lastPage),
                Is.EqualTo(new[] { 2 }));
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

        [Test]
        public void Build_UsesCurrentTemplateDefinitionInsteadOfStaleInstanceEntryFields()
        {
            var templateAsset = ScriptableObject.CreateInstance<TriggerAuthoringTemplateAsset>();
            try
            {
                templateAsset.Template = new TriggerAuthoringTemplateData
                {
                    TemplateId = "template.index",
                    Definition = new TriggerDefinitionData
                    {
                        Event = "event.current",
                        Priority = 99,
                        Actions = new TriggerNodeData
                        {
                            Kind = TriggerNodeKind.Action,
                            Type = "current_template_action"
                        }
                    }
                };
                var instance = new TriggerDefinitionData
                {
                    Id = 21,
                    Event = "event.stale",
                    Priority = -10,
                    Template = new TriggerTemplateReferenceData { TemplateId = "template.index" }
                };
                var templates = new TriggerTemplateDescriptorCatalog(new[] { templateAsset });

                var groups = TriggerAuthoringTriggerIndex.Build(
                    new[] { instance },
                    null,
                    null,
                    TriggerAuthoringTriggerGroupMode.Event,
                    "current_template_action",
                    TriggerAuthoringTriggerQuickFilter.All,
                    templates);

                Assert.That(groups, Has.Count.EqualTo(1));
                Assert.That(groups[0].Key, Is.EqualTo("event:<unassigned>/event.current"));
                Assert.That(groups[0].Entries[0].Trigger, Is.SameAs(instance));
                Assert.That(groups[0].Entries[0].EffectiveTrigger.Event, Is.EqualTo("event.current"));
                Assert.That(groups[0].Entries[0].EffectiveTrigger.Priority, Is.EqualTo(99));
            }
            finally
            {
                Object.DestroyImmediate(templateAsset);
            }
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
