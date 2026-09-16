using System;
using System.Collections.Generic;
using System.IO;
using AbilityKit.Ability.Impl.BattleDemo.Moba.Editor;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Demo.Moba.Share.Config;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Demo.Moba.Diagnostics.Tests
{
    public sealed class SkillFlowDefTests
    {
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
