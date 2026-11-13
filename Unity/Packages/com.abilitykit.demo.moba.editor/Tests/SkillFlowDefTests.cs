using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AbilityKit.Ability.Impl.BattleDemo.Moba.Editor;
using AbilityKit.Demo.Moba.Config.BattleDemo;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Demo.Moba.Share.Config;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Demo.Moba.Diagnostics.Tests
{
    public sealed class SkillFlowDefTests
    {
        private const string SerializationRegressionFolder = "Assets/SkillFlowSerializationRegressionTmp";
        private const string SerializationRegressionAssetPath = SerializationRegressionFolder + "/SkillFlow.asset";

        [Test]
        public void CastContracts_SurviveJsonImportAuthoringSaveAndReload()
        {
            const string json = "[{\"Id\":990092,\"ChildChannelId\":17,\"RecastChannelId\":19,\"RequiredBuffQualificationId\":12345,\"Phases\":[" +
                "{\"Type\":19,\"PhaseId\":\"reserve\",\"Economy\":{\"RequireExplicitCommit\":true,\"FailReason\":\"\"}}," +
                "{\"Type\":18,\"PhaseId\":\"commit\",\"CommitPoint\":{\"CommitId\":\"attack\",\"RequireEconomyReservation\":true}}]}]";
            if (!AssetDatabase.IsValidFolder(SerializationRegressionFolder))
                AssetDatabase.CreateFolder("Assets", "SkillFlowSerializationRegressionTmp");
            try
            {
                var dto = (SkillFlowDTO)LubanConfigGroupDeserializer.Instance.DeserializeFromText(json, typeof(SkillFlowDTO)).GetValue(0);
                var asset = ScriptableObject.CreateInstance<SkillFlowSO>();
                asset.dataList = new[] { SkillFlowDef.FromDto(dto) };
                AssetDatabase.CreateAsset(asset, SerializationRegressionAssetPath);
                EditorUtility.SetDirty(asset);
                AssetDatabase.SaveAssets();
                Resources.UnloadAsset(asset);
                var restored = AssetDatabase.LoadAssetAtPath<SkillFlowSO>(SerializationRegressionAssetPath).dataList[0].ToDto();
                Assert.AreEqual(17, restored.ChildChannelId);
                Assert.AreEqual(19, restored.RecastChannelId);
                Assert.AreEqual(12345, restored.RequiredBuffQualificationId);
                Assert.IsTrue(restored.Phases[0].Economy.RequireExplicitCommit);
                Assert.IsTrue(restored.Phases[1].CommitPoint.RequireEconomyReservation);
                Assert.AreEqual("attack", restored.Phases[1].CommitPoint.CommitId);
            }
            finally { AssetDatabase.DeleteAsset(SerializationRegressionFolder); }
        }

        [Test]
        public void RuntimeDtoReimport_PreservesExplicitTimelineReferencesByStablePhaseId()
        {
            var logic = new TextAsset("{\"schemaVersion\":1,\"runtimeType\":\"logic\",\"length\":1.2,\"groups\":[]}");
            try
            {
                var previous = new SkillFlowDef { Id = 990099, Phases = new List<SkillPhaseDef>
                {
                    new SkillTimelinePhaseDef { PhaseId = "cast", MobaLogicTimeline = logic }
                }};
                var dto = new SkillFlowDTO { Id = previous.Id, Phases = new[]
                {
                    new SkillPhaseDTO { Type = (int)SkillPhaseType.Sequence, PhaseId = "sequence", Children = new[]
                    {
                        new SkillPhaseDTO { Type = (int)SkillPhaseType.Timeline, PhaseId = "new-phase", Timeline = new SkillTimelinePhaseDTO() },
                        new SkillPhaseDTO { Type = (int)SkillPhaseType.Timeline, PhaseId = "cast", Timeline = new SkillTimelinePhaseDTO { DurationMs = 500 } }
                    }}
                }};
                var restored = SkillFlowDef.FromDto(dto, previous);
                var children = ((SkillSequencePhaseDef)restored.Phases[0]).Children;
                Assert.IsNull(((SkillTimelinePhaseDef)children[0]).MobaLogicTimeline);
                Assert.AreSame(logic, ((SkillTimelinePhaseDef)children[1]).MobaLogicTimeline);
                Assert.AreEqual(1200, restored.ToDto().Phases[0].Children[1].Timeline.DurationMs);
                dto.Id++;
                Assert.Throws<InvalidOperationException>(() => SkillFlowDef.FromDto(dto, previous));
            }
            finally { UnityEngine.Object.DestroyImmediate(logic); }
        }

        [Test]
        public void SerializeReferencePhases_RemainDistinctAcrossCreateAppendAndReload()
        {
            if (!AssetDatabase.IsValidFolder(SerializationRegressionFolder))
            {
                AssetDatabase.CreateFolder("Assets", "SkillFlowSerializationRegressionTmp");
            }

            try
            {
                var asset = ScriptableObject.CreateInstance<SkillFlowSO>();
                AssetDatabase.CreateAsset(asset, SerializationRegressionAssetPath);
                asset.dataList = new[]
                {
                    new SkillFlowDef
                    {
                        Id = 990001,
                        Name = "serialization-regression",
                        Phases = new List<SkillPhaseDef>
                        {
                            new SkillRulePlanPhaseDef { PhaseId = "release", TriggerIds = new[] { 900101011 } },
                            new SkillTimelinePhaseDef { PhaseId = "cast" },
                        },
                    },
                };
                EditorUtility.SetDirty(asset);
                AssetDatabase.SaveAssets();

                AssertReloadedPhaseGraph();

                asset = AssetDatabase.LoadAssetAtPath<SkillFlowSO>(SerializationRegressionAssetPath);
                var previousFlowDto = asset.dataList[0].ToDto();
                var appendedFlowDto = new SkillFlowDTO
                {
                    Id = 990002,
                    Name = "serialization-regression-appended",
                    Phases = new[]
                    {
                        new SkillPhaseDTO
                        {
                            Type = (int)SkillPhaseType.RulePlan,
                            PhaseId = "appended-release",
                            RulePlan = new SkillRulePlanPhaseDTO { TriggerIds = new[] { 900101012 } },
                        },
                        new SkillPhaseDTO
                        {
                            Type = (int)SkillPhaseType.CommitPoint,
                            PhaseId = "appended-commit",
                            CommitPoint = new SkillCommitPointPhaseDTO { CommitId = "shot" },
                        },
                    },
                };
                asset.dataList = new[]
                {
                    SkillFlowDef.FromDto(previousFlowDto),
                    SkillFlowDef.FromDto(appendedFlowDto),
                };
                EditorUtility.SetDirty(asset);
                AssetDatabase.SaveAssets();

                AssertReloadedPhaseGraph();
            }
            finally
            {
                AssetDatabase.DeleteAsset(SerializationRegressionFolder);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
        }

        [Test]
        public void ProductionFlowAsset_IncrementalAppendPreservesExistingPhaseGraph()
        {
            const string productionAssetPath = "Packages/com.abilitykit.demo.moba.view.runtime/Configs/Moba/SkillFlowCO.asset";
            const string copiedAssetPath = SerializationRegressionFolder + "/SkillFlowProductionCopy.asset";
            var sourcePath = Path.GetFullPath(Path.Combine(
                Application.dataPath, "..", "Packages/com.abilitykit.demo.moba.view.runtime/Configs/MobaSkillFlowAuthoring/Showcases/jinx-q-flow.json"));

            if (!AssetDatabase.IsValidFolder(SerializationRegressionFolder))
            {
                AssetDatabase.CreateFolder("Assets", "SkillFlowSerializationRegressionTmp");
            }

            try
            {
                Assert.That(AssetDatabase.CopyAsset(productionAssetPath, copiedAssetPath), Is.True,
                    "The production SkillFlowCO must be copied before testing the additive write.");
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                var copied = AssetDatabase.LoadAssetAtPath<SkillFlowSO>(copiedAssetPath);
                Assert.That(copied, Is.Not.Null);
                var originalFlows = copied.dataList ?? Array.Empty<SkillFlowDef>();
                var appendedId = checked(originalFlows.Where(flow => flow != null).Max(flow => flow.Id) + 1);

                var values = LubanConfigGroupDeserializer.Instance.DeserializeFromText(
                    File.ReadAllText(sourcePath), typeof(SkillFlowDTO));
                Assert.That(values, Has.Length.EqualTo(1));
                var jinxDto = (SkillFlowDTO)values.GetValue(0);
                jinxDto.Id = appendedId;

                // Match the production syncer's additive path: retain the loaded objects and append a fresh DTO graph.
                copied.dataList = originalFlows.Concat(new[] { SkillFlowDef.FromDto(jinxDto) }).ToArray();
                EditorUtility.SetDirty(copied);
                AssetDatabase.SaveAssets();

                Resources.UnloadAsset(copied);
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                copied = AssetDatabase.LoadAssetAtPath<SkillFlowSO>(copiedAssetPath);
                Assert.That(copied, Is.Not.Null);
                Assert.That(copied.dataList, Has.Length.EqualTo(originalFlows.Length + 1));
                Assert.That(copied.dataList.Count(flow => flow != null && flow.Id == appendedId), Is.EqualTo(1));

                foreach (var flow in copied.dataList)
                {
                    Assert.That(flow, Is.Not.Null);
                    var visited = new HashSet<SkillPhaseDef>();
                    var phaseIds = new HashSet<string>(StringComparer.Ordinal);
                    AssertDistinctPhaseGraph(flow.Id, flow.Phases, visited, phaseIds);
                }
            }
            finally
            {
                AssetDatabase.DeleteAsset(SerializationRegressionFolder);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
        }

        private static void AssertDistinctPhaseGraph(
            int flowId,
            IReadOnlyList<SkillPhaseDef> phases,
            ISet<SkillPhaseDef> visited,
            ISet<string> phaseIds)
        {
            Assert.That(phases, Is.Not.Null, $"SkillFlow {flowId} has no phases.");
            foreach (var phase in phases)
            {
                Assert.That(phase, Is.Not.Null, $"SkillFlow {flowId} contains a null phase.");
                Assert.That(visited.Add(phase), Is.True, $"SkillFlow {flowId} reuses phase '{phase.PhaseId}'.");
                if (!string.IsNullOrWhiteSpace(phase.PhaseId))
                    Assert.That(phaseIds.Add(phase.PhaseId.Trim()), Is.True,
                        $"SkillFlow {flowId} repeats phase ID '{phase.PhaseId}'.");
                if (phase is SkillCompositePhaseDef composite)
                    AssertDistinctPhaseGraph(flowId, composite.Children, visited, phaseIds);
                if (phase is SkillRepeatPhaseDef repeat)
                    AssertDistinctPhaseNode(flowId, repeat.Phase, visited, phaseIds);
            }
        }

        private static void AssertDistinctPhaseNode(
            int flowId,
            SkillPhaseDef phase,
            ISet<SkillPhaseDef> visited,
            ISet<string> phaseIds)
        {
            Assert.That(phase, Is.Not.Null, $"SkillFlow {flowId} contains a null repeat phase.");
            Assert.That(visited.Add(phase), Is.True, $"SkillFlow {flowId} reuses phase '{phase.PhaseId}'.");
            if (!string.IsNullOrWhiteSpace(phase.PhaseId))
                Assert.That(phaseIds.Add(phase.PhaseId.Trim()), Is.True,
                    $"SkillFlow {flowId} repeats phase ID '{phase.PhaseId}'.");
            if (phase is SkillCompositePhaseDef composite)
                AssertDistinctPhaseGraph(flowId, composite.Children, visited, phaseIds);
            if (phase is SkillRepeatPhaseDef repeat)
                AssertDistinctPhaseNode(flowId, repeat.Phase, visited, phaseIds);
        }

        private static void AssertReloadedPhaseGraph()
        {
            Resources.UnloadAsset(AssetDatabase.LoadAssetAtPath<SkillFlowSO>(SerializationRegressionAssetPath));
            AssetDatabase.Refresh();
            var reloaded = AssetDatabase.LoadAssetAtPath<SkillFlowSO>(SerializationRegressionAssetPath);
            Assert.That(reloaded, Is.Not.Null);
            Assert.That(reloaded.dataList, Has.Length.GreaterThanOrEqualTo(1));
            Assert.That(reloaded.dataList[0].Id, Is.EqualTo(990001));
            var phases = reloaded.dataList[0].Phases;
            Assert.That(phases, Is.Not.Null);
            Assert.That(phases, Has.Count.GreaterThanOrEqualTo(2));
            Assert.That(phases[0], Is.Not.Null);
            Assert.That(phases[1], Is.Not.Null);
            Assert.That(ReferenceEquals(phases[0], phases[1]), Is.False,
                "SerializeReference phase entries must deserialize as independent objects.");
            Assert.That(phases[0].PhaseId, Is.EqualTo("release"));
            Assert.That(phases[0], Is.TypeOf<SkillRulePlanPhaseDef>());
            Assert.That(phases[1].PhaseId, Is.EqualTo("cast"));
            Assert.That(phases[1], Is.TypeOf<SkillTimelinePhaseDef>());
            if (reloaded.dataList.Length > 1)
            {
                Assert.That(reloaded.dataList, Has.Length.EqualTo(2));
                Assert.That(reloaded.dataList[1].Id, Is.EqualTo(990002));
                Assert.That(reloaded.dataList[1].Phases, Has.Count.EqualTo(2));
                Assert.That(reloaded.dataList[1].Phases[0].PhaseId, Is.EqualTo("appended-release"));
                Assert.That(reloaded.dataList[1].Phases[0], Is.TypeOf<SkillRulePlanPhaseDef>());
                Assert.That(reloaded.dataList[1].Phases[1].PhaseId, Is.EqualTo("appended-commit"));
                Assert.That(reloaded.dataList[1].Phases[1], Is.TypeOf<SkillCommitPointPhaseDef>());
                foreach (var firstPhase in phases)
                {
                    foreach (var appendedPhase in reloaded.dataList[1].Phases)
                    {
                        Assert.That(ReferenceEquals(firstPhase, appendedPhase), Is.False,
                            "Appending a flow must not reuse phase instances from existing flows.");
                    }
                }
            }
        }

        [Test]
        public void LegacyJsonContinuousTagTemplates_MapConfiguredApplicationTags()
        {
            var continuous = (ContinuousTagTemplateDTO[])LegacyJsonConfigGroupDeserializer.Instance.DeserializeFromText(
                "[{\"Id\":10010001,\"ApplicationTags\":[\"State.SuperArmor\"]}]",
                typeof(ContinuousTagTemplateDTO));
            Assert.That(continuous[0].ApplicationTagNames, Is.EqualTo(new[] { "State.SuperArmor" }));
            var direct = (ContinuousTagTemplateDTO[])JsonNetMobaConfigDtoDeserializer.Instance.DeserializeDtoArray(
                "[{\"Id\":10010001,\"ApplicationTags\":[\"State.SuperArmor\"]}]",
                typeof(ContinuousTagTemplateDTO));
            Assert.That(direct[0].ApplicationTagNames, Is.EqualTo(new[] { "State.SuperArmor" }));
            var named = (ContinuousTagTemplateDTO[])JsonNetMobaConfigDtoDeserializer.Instance.DeserializeDtoArray(
                "[{\"Id\":10010001,\"ApplicationTagNames\":[\"State.SuperArmor\"]}]",
                typeof(ContinuousTagTemplateDTO));
            Assert.That(named[0].ApplicationTagNames, Is.EqualTo(new[] { "State.SuperArmor" }));
        }

        [Test]
        public void P1EconomyShowcase_DeserializesAndIsEditableAsSkillFlowAsset()
        {
            var sourcePath = Path.GetFullPath(Path.Combine(
                Application.dataPath, "..", MobaP1SkillFlowShowcaseSync.SourceAssetPath));
            var array = LubanConfigGroupDeserializer.Instance.DeserializeFromText(
                File.ReadAllText(sourcePath), typeof(SkillFlowDTO));
            Assert.That(array, Has.Length.EqualTo(1));

            var source = (SkillFlowDTO)array.GetValue(0);
            var restored = SkillFlowDef.FromDto(source).ToDto();
            Assert.That(restored.Id, Is.EqualTo(MobaP1SkillFlowShowcaseSync.FlowId));
            Assert.That(restored.Phases[0].Economy.MaxCharges, Is.EqualTo(3));
            Assert.That(restored.Phases[0].Economy.CooldownGroup, Is.EqualTo("mobility"));
            Assert.That(restored.Phases[1].Type, Is.EqualTo((int)SkillPhaseType.Race));
            Assert.That(restored.Phases[2].CommitPoint.CommitId, Is.EqualTo("p1.release"));
            Assert.That(restored.Phases[3].Children[1].Repeat.Phase.Economy.Operation,
                Is.EqualTo((int)SkillEconomyOperation.ConsumeResource));

            var asset = AssetDatabase.LoadAssetAtPath<SkillFlowSO>(MobaP1SkillFlowShowcaseSync.TargetAssetPath);
            Assert.That(asset, Is.Not.Null, "Run MobaP1SkillFlowShowcaseSync.SyncBatch to import the showcase JSON.");
            Assert.That(asset.dataList, Has.Length.EqualTo(1));
            Assert.That(asset.dataList[0].Id, Is.EqualTo(MobaP1SkillFlowShowcaseSync.FlowId));
            Assert.That(asset.dataList[0].Phases[0], Is.TypeOf<SkillEconomyPhaseDef>());
        }

        [Test]
        public void P2SkillProgrammingShowcase_DeserializesAndIsEditableAsSkillFlowAsset()
        {
            var sourcePath = Path.GetFullPath(Path.Combine(
                Application.dataPath, "..", MobaP2SkillProgrammingShowcaseSync.FlowSourceAssetPath));
            var array = LubanConfigGroupDeserializer.Instance.DeserializeFromText(
                File.ReadAllText(sourcePath), typeof(SkillFlowDTO));
            Assert.That(array, Has.Length.EqualTo(1));

            var source = (SkillFlowDTO)array.GetValue(0);
            var restored = SkillFlowDef.FromDto(source).ToDto();
            Assert.That(restored.Id, Is.EqualTo(MobaP2SkillProgrammingShowcaseSync.FlowId));
            Assert.That(restored.Phases[0].RulePlan.TriggerIds,
                Is.EqualTo(new[] { MobaP2SkillProgrammingShowcaseSync.ApplyTriggerId }));
            Assert.That(restored.Phases[1].Type, Is.EqualTo((int)SkillPhaseType.Parallel));
            Assert.That(restored.Phases[1].Children[0].AwaitEvent.Captures, Has.Length.EqualTo(2));
            Assert.That(restored.Phases[1].Children[0].AwaitEvent.Captures[0].Scope,
                Is.EqualTo((int)SkillEventCaptureScope.Cast));
            Assert.That(restored.Phases[1].Children[0].AwaitEvent.Captures[1].Scope,
                Is.EqualTo((int)SkillEventCaptureScope.Target));
            Assert.That(restored.Phases[1].Children[1].DerivedSkill.SkillId, Is.EqualTo(10010101));
            Assert.That(restored.Phases[1].Children[1].DerivedSkill.WaitForCompletion, Is.True);
            Assert.That(restored.Phases[2].Repeat.RepeatCount, Is.EqualTo(2));
            Assert.That(restored.Phases[3].RulePlan.TriggerIds,
                Is.EqualTo(new[] { MobaP2SkillProgrammingShowcaseSync.ClearTriggerId }));

            var asset = AssetDatabase.LoadAssetAtPath<SkillFlowSO>(
                MobaP2SkillProgrammingShowcaseSync.FlowAssetPath);
            Assert.That(asset, Is.Not.Null,
                "Run MobaP2SkillProgrammingShowcaseSync.SyncBatch to import the P2 showcase.");
            Assert.That(asset.dataList, Has.Length.EqualTo(1));
            Assert.That(asset.dataList[0].Id, Is.EqualTo(MobaP2SkillProgrammingShowcaseSync.FlowId));
            Assert.That(asset.dataList[0].Phases[1], Is.TypeOf<SkillParallelPhaseDef>());
        }

        [Test]
        public void ToDto_MapsPipelineMetadataAndFormalTopLevelPhases()
        {
            var flow = new SkillFlowDef
            {
                Id = 10010301,
                Name = "formal-flow",
                PipelineContinuousTagTemplateId = 10010001,
                Phases = new List<SkillPhaseDef>
                {
                    new SkillRulePlanPhaseDef
                    {
                        PhaseId = " release_rules ",
                        TriggerIds = new[] { 900101011, 900101012 },
                        AbortOnFailure = true,
                        FailReason = "skill_release_failed",
                    },
                    new SkillTimelinePhaseDef
                    {
                        PhaseId = "cast",
                        Timeline = new SkillTimelinePhaseDTO
                        {
                            DurationMs = 650,
                            Events = new[]
                            {
                                new SkillTimelineEventDTO
                                {
                                    AtMs = 0,
                                    EffectId = 10010101,
                                    ExecuteMode = 0,
                                    EventTag = "cast_start",
                                },
                            },
                        },
                    },
                },
            };

            var dto = flow.ToDto();

            Assert.That(dto.Id, Is.EqualTo(10010301));
            Assert.That(dto.Name, Is.EqualTo("formal-flow"));
            Assert.That(dto.PipelineContinuousTagTemplateId, Is.EqualTo(10010001));
            Assert.That(dto.Phases, Has.Length.EqualTo(2));

            var rules = dto.Phases[0];
            Assert.That(rules.Type, Is.EqualTo((int)SkillPhaseType.RulePlan));
            Assert.That(rules.PhaseId, Is.EqualTo("release_rules"));
            Assert.That(rules.RulePlan.TriggerIds, Is.EqualTo(new[] { 900101011, 900101012 }));
            Assert.That(rules.RulePlan.AbortOnFailure, Is.True);
            Assert.That(rules.RulePlan.FailReason, Is.EqualTo("skill_release_failed"));

            var timeline = dto.Phases[1];
            Assert.That(timeline.Type, Is.EqualTo((int)SkillPhaseType.Timeline));
            Assert.That(timeline.PhaseId, Is.EqualTo("cast"));
            Assert.That(timeline.Timeline.DurationMs, Is.EqualTo(650));
            Assert.That(timeline.Timeline.Events, Has.Length.EqualTo(1));
            Assert.That(timeline.Timeline.Events[0].EffectId, Is.EqualTo(10010101));
        }

        [Test]
        public void ToDto_MapsRecursiveSequenceParallelRepeatAndControlPhases()
        {
            var repeat = new SkillRepeatPhaseDef
            {
                PhaseId = "repeat_hits",
                RepeatCount = 3,
                IntervalMs = 120,
                Phase = new SkillTimelinePhaseDef
                {
                    PhaseId = "repeat_hit",
                    Timeline = new SkillTimelinePhaseDTO { DurationMs = 80 },
                },
            };
            var parallel = new SkillParallelPhaseDef
            {
                PhaseId = "parallel_branch",
                Children = new List<SkillPhaseDef>
                {
                    repeat,
                    new SkillDelayPhaseDef { PhaseId = "delay", DelayMs = 60 },
                },
            };
            var sequence = new SkillSequencePhaseDef
            {
                PhaseId = "ultimate_sequence",
                Children = new List<SkillPhaseDef>
                {
                    parallel,
                    new SkillWaitUntilPhaseDef
                    {
                        PhaseId = "wait_slots",
                        Condition = "ObservedSlotsIdle",
                        TimeoutMs = 0,
                        CompleteOnTimeout = true,
                        ObservedSlots = new[] { 1, 2 },
                        Arguments = new[]
                        {
                            new SkillWaitConditionArgumentDTO { Name = "mode", Value = "all" },
                        },
                    },
                },
            };

            var dto = sequence.ToDto();

            Assert.That(dto.Type, Is.EqualTo((int)SkillPhaseType.Sequence));
            Assert.That(dto.Children, Has.Length.EqualTo(2));
            Assert.That(dto.Children[0].Type, Is.EqualTo((int)SkillPhaseType.Parallel));
            Assert.That(dto.Children[0].Children, Has.Length.EqualTo(2));

            var repeatDto = dto.Children[0].Children[0];
            Assert.That(repeatDto.Type, Is.EqualTo((int)SkillPhaseType.Repeat));
            Assert.That(repeatDto.Repeat.RepeatCount, Is.EqualTo(3));
            Assert.That(repeatDto.Repeat.IntervalMs, Is.EqualTo(120));
            Assert.That(repeatDto.Repeat.Phase.Type, Is.EqualTo((int)SkillPhaseType.Timeline));
            Assert.That(repeatDto.Repeat.Phase.PhaseId, Is.EqualTo("repeat_hit"));

            var delayDto = dto.Children[0].Children[1];
            Assert.That(delayDto.Type, Is.EqualTo((int)SkillPhaseType.Delay));
            Assert.That(delayDto.Delay.DelayMs, Is.EqualTo(60));

            var waitDto = dto.Children[1];
            Assert.That(waitDto.Type, Is.EqualTo((int)SkillPhaseType.WaitUntil));
            Assert.That(waitDto.WaitUntil.Condition, Is.EqualTo("ObservedSlotsIdle"));
            Assert.That(waitDto.WaitUntil.ObservedSlots, Is.EqualTo(new[] { 1, 2 }));
            Assert.That(waitDto.WaitUntil.Arguments[0].Name, Is.EqualTo("mode"));
        }

        [Test]
        public void ToDto_SkipsNullPhasesAndNormalizesOptionalCollections()
        {
            var flow = new SkillFlowDef
            {
                Phases = new List<SkillPhaseDef>
                {
                    null,
                    new SkillRulePlanPhaseDef { TriggerIds = null },
                    new SkillSequencePhaseDef { Children = null },
                    new SkillWaitUntilPhaseDef { ObservedSlots = null, Arguments = null },
                },
            };

            var dto = flow.ToDto();

            Assert.That(dto.Phases, Has.Length.EqualTo(3));
            Assert.That(dto.Phases[0].RulePlan.TriggerIds, Is.Empty);
            Assert.That(dto.Phases[1].Children, Is.Empty);
            Assert.That(dto.Phases[2].WaitUntil.ObservedSlots, Is.Empty);
            Assert.That(dto.Phases[2].WaitUntil.Arguments, Is.Empty);
        }

        [Test]
        public void FromDtoAndToDto_RoundTripsAdvancedWindowStateMachinePhases()
        {
            var source = new SkillFlowDTO
            {
                Id = 80100001,
                Name = "p1-window-flow",
                Phases = new[]
                {
                    new SkillPhaseDTO
                    {
                        Type = (int)SkillPhaseType.Race,
                        PhaseId = "release-or-timeout",
                        Children = new[]
                        {
                            new SkillPhaseDTO
                            {
                                Type = (int)SkillPhaseType.AwaitEvent,
                                PhaseId = "await-hit",
                                AwaitEvent = new SkillAwaitEventPhaseDTO
                                {
                                    EventId = "projectile.hit",
                                    TimeoutMs = 800,
                                    Filters = new[] { new SkillEventIntFilterDTO { FieldId = 17, UseCasterActorId = true } },
                                },
                            },
                            new SkillPhaseDTO
                            {
                                Type = (int)SkillPhaseType.Window,
                                PhaseId = "charge",
                                Window = new SkillWindowPhaseDTO
                                {
                                    WindowId = "charge.primary",
                                    Kind = (int)SkillWindowKind.Charge,
                                    DurationMs = 1200,
                                    ChargeTierThresholdMs = new[] { 300, 700, 1100 },
                                },
                            },
                        },
                    },
                    new SkillPhaseDTO
                    {
                        Type = (int)SkillPhaseType.CommitPoint,
                        PhaseId = "commit",
                        CommitPoint = new SkillCommitPointPhaseDTO { CommitId = "resource.commit" },
                    },
                },
            };

            var restored = SkillFlowDef.FromDto(source).ToDto();

            Assert.That(restored.Phases[0].Type, Is.EqualTo((int)SkillPhaseType.Race));
            Assert.That(restored.Phases[0].Children[0].AwaitEvent.EventId, Is.EqualTo("projectile.hit"));
            Assert.That(restored.Phases[0].Children[0].AwaitEvent.Filters[0].UseCasterActorId, Is.True);
            Assert.That(restored.Phases[0].Children[1].Window.ChargeTierThresholdMs, Is.EqualTo(new[] { 300, 700, 1100 }));
            Assert.That(restored.Phases[1].CommitPoint.CommitId, Is.EqualTo("resource.commit"));
        }

        [Test]
        public void FromDtoAndToDto_RoundTripsP2DerivedSkillAndEventCaptures()
        {
            var source = new SkillFlowDTO
            {
                Id = 99200020,
                Name = "p2-derived-capture-flow",
                Phases = new[]
                {
                    new SkillPhaseDTO
                    {
                        Type = (int)SkillPhaseType.AwaitEvent,
                        PhaseId = "capture.hit",
                        AwaitEvent = new SkillAwaitEventPhaseDTO
                        {
                            EventId = "projectile.hit",
                            TimeoutMs = 1200,
                            Captures = new[]
                            {
                                new SkillEventCaptureDTO
                                {
                                    FieldId = 7,
                                    Key = "impact_damage",
                                    Scope = (int)SkillEventCaptureScope.Target,
                                    ValueType = (int)SkillEventCaptureValueType.Number,
                                    Required = true,
                                },
                            },
                        },
                    },
                    new SkillPhaseDTO
                    {
                        Type = (int)SkillPhaseType.DerivedSkill,
                        PhaseId = "derive.explosion",
                        DerivedSkill = new SkillDerivedSkillPhaseDTO
                        {
                            SkillId = 99200021,
                            InheritAim = true,
                            InheritTarget = true,
                            WaitForCompletion = true,
                            AbortOnFailure = true,
                            MaxDepth = 4,
                        },
                    },
                },
            };

            var restored = SkillFlowDef.FromDto(source).ToDto();

            Assert.That(restored.Phases[0].AwaitEvent.Captures, Has.Length.EqualTo(1));
            Assert.That(restored.Phases[0].AwaitEvent.Captures[0].Key, Is.EqualTo("impact_damage"));
            Assert.That(restored.Phases[0].AwaitEvent.Captures[0].Scope, Is.EqualTo((int)SkillEventCaptureScope.Target));
            Assert.That(restored.Phases[1].DerivedSkill.SkillId, Is.EqualTo(99200021));
            Assert.That(restored.Phases[1].DerivedSkill.WaitForCompletion, Is.True);
            Assert.That(restored.Phases[1].DerivedSkill.MaxDepth, Is.EqualTo(4));
        }

        [Test]
        public void ToDto_RetainsDeprecatedChecksForExistingAssetMigration()
        {
            var phase = new SkillChecksPhaseDef
            {
                PhaseId = "legacy_checks",
                Checks = new SkillChecksPhaseDTO
                {
                    CheckCooldown = true,
                    CheckCastingState = true,
                    RequiredTags = new[] { 101 },
                },
            };

            var dto = phase.ToDto();

            Assert.That(dto.Type, Is.EqualTo((int)SkillPhaseType.Checks));
            Assert.That(dto.PhaseId, Is.EqualTo("legacy_checks"));
            Assert.That(dto.Checks.CheckCooldown, Is.True);
            Assert.That(dto.Checks.RequiredTags, Is.EqualTo(new[] { 101 }));
        }

        [Test]
        public void InspectorSelection_ResolvesNestedRepeatPhaseAndStablePropertyPath()
        {
            var asset = ScriptableObject.CreateInstance<SkillFlowSO>();
            try
            {
                var target = new SkillTimelinePhaseDef { PhaseId = "repeat.hit" };
                asset.dataList = new[]
                {
                    new SkillFlowDef
                    {
                        Id = 7001,
                        Phases = new List<SkillPhaseDef>
                        {
                            new SkillSequencePhaseDef
                            {
                                PhaseId = "sequence",
                                Children = new List<SkillPhaseDef>
                                {
                                    new SkillParallelPhaseDef
                                    {
                                        PhaseId = "parallel",
                                        Children = new List<SkillPhaseDef>
                                        {
                                            new SkillRepeatPhaseDef
                                            {
                                                PhaseId = "repeat",
                                                Phase = target,
                                            },
                                        },
                                    },
                                },
                            },
                        },
                    },
                };

                var found = SkillFlowInspectorSelectionState.TrySelect(
                    asset,
                    7001,
                    "repeat.hit",
                    out var selection,
                    out var error);

                Assert.That(found, Is.True, error);
                Assert.That(selection.Asset, Is.SameAs(asset));
                Assert.That(selection.Phase, Is.SameAs(target));
                Assert.That(selection.SerializedPropertyPath, Is.EqualTo(
                    "dataList.Array.data[0].Phases.Array.data[0].Children.Array.data[0].Children.Array.data[0].Phase"));
                Assert.That(selection.Revision, Is.GreaterThan(0));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(asset);
            }
        }
    }
}
