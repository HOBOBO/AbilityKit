using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace AbilityKit.Game.Test.UnitTest
{
    public static class MobaAcceptanceRunner
    {
        public const string DefaultExpectationDirectory = "Unity/Packages/com.abilitykit.demo.moba.view.runtime/Runtime/Game/Test/Expectations";

        public static MobaAcceptanceSummary RunSkillExpectationFile(string expectationPath, string artifactDirectory = null, bool exportArtifacts = true)
        {
            var expectation = LoadExpectation(expectationPath);
            return RunSkillExpectation(expectation, artifactDirectory, exportArtifacts, exportTraceText: false, expectationPath: expectationPath);
        }

        public static MobaAcceptanceSummary RunSkillExpectationFile(MobaAcceptanceRunOptions options, string expectationPath)
        {
            options = options ?? new MobaAcceptanceRunOptions();
            var expectation = LoadExpectation(expectationPath);
            return RunSkillExpectation(expectation, options.ArtifactDirectory, options.ExportArtifacts, options.ShouldExportTraceText, expectationPath);
        }

        public static MobaAcceptanceSummary RunSkillExpectation(MobaAcceptanceExpectation expectation, string artifactDirectory = null, bool exportArtifacts = true)
        {
            return RunSkillExpectation(expectation, artifactDirectory, exportArtifacts, exportTraceText: false, expectationPath: null);
        }

        public static MobaAcceptanceSummary RunSkillExpectation(MobaAcceptanceExpectation expectation, string artifactDirectory, bool exportArtifacts, string expectationPath)
        {
            return RunSkillExpectation(expectation, artifactDirectory, exportArtifacts, exportTraceText: false, expectationPath: expectationPath);
        }

        public static MobaAcceptanceSummary RunSkillExpectation(MobaAcceptanceRunOptions options, MobaAcceptanceExpectation expectation)
        {
            options = options ?? new MobaAcceptanceRunOptions();
            return RunSkillExpectation(expectation, options.ArtifactDirectory, options.ExportArtifacts, options.ShouldExportTraceText, expectationPath: null);
        }

        private static MobaAcceptanceSummary RunSkillExpectation(MobaAcceptanceExpectation expectation, string artifactDirectory, bool exportArtifacts, bool exportTraceText, string expectationPath)
        {
            Assert.IsNotNull(expectation, "Acceptance expectation must not be null.");

            var caseId = string.IsNullOrEmpty(expectation.caseId) ? "moba_acceptance" : expectation.caseId;
            var worldId = ResolveWorldId(expectation, caseId);
            var tickRate = ResolveTickRate(expectation);
            expectation.caseId = caseId;
            expectation.worldId = worldId;
            expectation.tickRate = tickRate;

            var tracePath = MobaAcceptanceTraceExporter.GetTraceJsonlPath(artifactDirectory, caseId);
            var traceTextPath = MobaAcceptanceTraceExporter.GetTraceTextPath(artifactDirectory, caseId);
            var summaryPath = MobaAcceptanceTraceExporter.GetSummaryJsonPath(artifactDirectory, caseId);

            return HasScenarioFlow(expectation)
                ? RunScenarioExpectation(expectation, artifactDirectory, exportArtifacts, exportTraceText, expectationPath, tracePath, traceTextPath, summaryPath)
                : RunLegacySkillExpectation(expectation, artifactDirectory, exportArtifacts, exportTraceText, expectationPath, tracePath, traceTextPath, summaryPath);
        }

        public static MobaAcceptanceBatchSummary RunExpectationDirectory(string expectationDirectory = null, string artifactDirectory = null, bool exportArtifacts = true, bool recursive = true)
        {
            return RunExpectationDirectory(expectationDirectory, artifactDirectory, exportArtifacts, recursive, null, null);
        }

        public static MobaAcceptanceBatchSummary RunExpectationDirectory(string expectationDirectory, string artifactDirectory, bool exportArtifacts, bool recursive, string categoryFilter)
        {
            return RunExpectationDirectory(expectationDirectory, artifactDirectory, exportArtifacts, recursive, categoryFilter, null);
        }

        public static MobaAcceptanceBatchSummary RunExpectationDirectory(string expectationDirectory, string artifactDirectory, bool exportArtifacts, bool recursive, string categoryFilter, string tagFilter)
        {
            return RunExpectationDirectory(expectationDirectory, artifactDirectory, exportArtifacts, exportTraceText: false, recursive: recursive, categoryFilter: categoryFilter, tagFilter: tagFilter);
        }

        public static MobaAcceptanceBatchSummary RunExpectationDirectory(MobaAcceptanceRunOptions options, string expectationDirectory = null)
        {
            options = options ?? new MobaAcceptanceRunOptions();
            return RunExpectationDirectory(
                expectationDirectory,
                options.ArtifactDirectory,
                options.ExportArtifacts,
                options.ShouldExportTraceText,
                options.Recursive,
                options.CategoryFilter,
                options.TagFilter);
        }

        private static MobaAcceptanceBatchSummary RunExpectationDirectory(string expectationDirectory, string artifactDirectory, bool exportArtifacts, bool exportTraceText, bool recursive, string categoryFilter, string tagFilter)
        {
            var stopwatch = Stopwatch.StartNew();
            var resolvedExpectationDirectory = ResolveProjectRelativePath(string.IsNullOrEmpty(expectationDirectory) ? DefaultExpectationDirectory : expectationDirectory);
            Assert.IsTrue(Directory.Exists(resolvedExpectationDirectory), $"Acceptance expectation directory missing: {expectationDirectory}");

            var directory = MobaAcceptanceTraceExporter.ResolveArtifactDirectory(artifactDirectory);
            var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            var files = Directory.GetFiles(resolvedExpectationDirectory, "*.expected.json", searchOption);
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);

            var results = new List<MobaAcceptanceCaseRunResult>(files.Length);
            for (var i = 0; i < files.Length; i++)
            {
                if (!ShouldRunExpectationFile(files[i], categoryFilter, tagFilter)) continue;
                results.Add(RunExpectationFileForBatch(files[i], directory, exportArtifacts, exportTraceText));
            }

            stopwatch.Stop();
            var batch = BuildBatchSummary(resolvedExpectationDirectory, directory, recursive, categoryFilter, tagFilter, stopwatch.ElapsedMilliseconds, results.ToArray());
            if (exportArtifacts)
            {
                ExportBatchSummary(batch);
            }

            return batch;
        }

        public static MobaAcceptanceBatchSummary RunContractExpectationDirectory(string expectationDirectory = null, string artifactDirectory = null, bool exportArtifacts = true, bool recursive = true, string tagFilter = null)
        {
            return RunExpectationDirectory(expectationDirectory, artifactDirectory, exportArtifacts, recursive, "contract", tagFilter);
        }

        public static MobaAcceptanceBatchSummary RunGoldenExpectationDirectory(string expectationDirectory = null, string artifactDirectory = null, bool exportArtifacts = true, bool recursive = true, string tagFilter = null)
        {
            return RunExpectationDirectory(expectationDirectory, artifactDirectory, exportArtifacts, recursive, "golden", tagFilter);
        }

        public static MobaAcceptanceExpectation LoadExpectation(string expectationPath)
        {
            Assert.IsFalse(string.IsNullOrEmpty(expectationPath), "Expectation path must not be empty.");
            var resolvedPath = ResolveProjectRelativePath(expectationPath);
            Assert.IsTrue(File.Exists(resolvedPath), $"Acceptance expectation file missing: {expectationPath}");

            var json = File.ReadAllText(resolvedPath);
            var expectation = JsonUtility.FromJson<MobaAcceptanceExpectation>(json);
            Assert.IsNotNull(expectation, $"Failed to parse acceptance expectation: {expectationPath}");
            return expectation;
        }

        public static MobaAcceptanceTraceRecord[] LoadTraceRecords(string traceJsonlPath)
        {
            Assert.IsFalse(string.IsNullOrEmpty(traceJsonlPath), "Trace jsonl path must not be empty.");
            var resolvedPath = ResolveProjectRelativePath(traceJsonlPath);
            Assert.IsTrue(File.Exists(resolvedPath), $"Acceptance trace jsonl file missing: {traceJsonlPath}");

            var lines = File.ReadAllLines(resolvedPath);
            var records = new List<MobaAcceptanceTraceRecord>(lines.Length);
            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                if (string.IsNullOrWhiteSpace(line)) continue;
                var record = JsonUtility.FromJson<MobaAcceptanceTraceRecord>(line);
                Assert.IsNotNull(record, $"Failed to parse acceptance trace record at line {i + 1}: {traceJsonlPath}");
                records.Add(record);
            }

            return records.ToArray();
        }

        public static string ResolveProjectRelativePath(string path)
        {
            if (string.IsNullOrEmpty(path)) return path;
            if (File.Exists(path) || Directory.Exists(path)) return path;

            const string unityPrefix = "Unity/";
            if (path.StartsWith(unityPrefix, StringComparison.Ordinal))
            {
                var withoutUnityPrefix = path.Substring(unityPrefix.Length);
                if (File.Exists(withoutUnityPrefix) || Directory.Exists(withoutUnityPrefix)) return withoutUnityPrefix;
            }

            var unityRelativePath = Path.Combine("Unity", path);
            if (File.Exists(unityRelativePath) || Directory.Exists(unityRelativePath)) return unityRelativePath;

            return path;
        }

        public static string GetBatchSummaryPath(string artifactDirectory)
        {
            var directory = MobaAcceptanceTraceExporter.ResolveArtifactDirectory(artifactDirectory);
            return Path.Combine(directory, "batch_summary.json");
        }

        private static MobaAcceptanceCaseRunResult RunExpectationFileForBatch(string expectationPath, string artifactDirectory, bool exportArtifacts, bool exportTraceText)
        {
            var stopwatch = Stopwatch.StartNew();
            var result = new MobaAcceptanceCaseRunResult
            {
                expectationPath = NormalizePath(expectationPath),
                startedUtc = DateTime.UtcNow.ToString("O")
            };

            try
            {
                result.summary = RunSkillExpectationFile(
                    new MobaAcceptanceRunOptions
                    {
                        ArtifactDirectory = artifactDirectory,
                        ExportArtifacts = exportArtifacts,
                        TraceExport = exportTraceText ? new MobaAcceptanceTraceExportOptions() : null
                    },
                    expectationPath);
                result.caseId = result.summary != null ? result.summary.caseId : string.Empty;
                result.passed = result.summary != null && result.summary.result != null && result.summary.result.passed;
            }
            catch (Exception ex)
            {
                result.passed = false;
                result.errorType = ex.GetType().FullName;
                result.errorMessage = ex.Message;
            }
            finally
            {
                stopwatch.Stop();
                result.durationMs = stopwatch.ElapsedMilliseconds;
            }

            return result;
        }

        private static MobaAcceptanceBatchSummary BuildBatchSummary(string expectationDirectory, string artifactDirectory, bool recursive, string categoryFilter, string tagFilter, long durationMs, MobaAcceptanceCaseRunResult[] results)
        {
            var passed = 0;
            var failed = 0;
            for (var i = 0; i < results.Length; i++)
            {
                if (results[i].passed) passed++;
                else failed++;
            }

            var summaryPath = GetBatchSummaryPath(artifactDirectory);
            return new MobaAcceptanceBatchSummary
            {
                expectationDirectory = NormalizePath(expectationDirectory),
                artifactDirectory = NormalizePath(string.IsNullOrEmpty(artifactDirectory) ? MobaAcceptanceTraceExporter.DefaultArtifactDirectory : artifactDirectory),
                categoryFilter = categoryFilter,
                tagFilter = tagFilter,
                recursive = recursive,
                startedUtc = DateTime.UtcNow.ToString("O"),
                durationMs = durationMs,
                total = results.Length,
                passed = passed,
                failed = failed,
                allPassed = failed == 0,
                results = results,
                batchSummaryJsonPath = NormalizePath(summaryPath)
            };
        }

        private static bool ShouldRunExpectationFile(string expectationPath, string categoryFilter, string tagFilter)
        {
            if (string.IsNullOrEmpty(categoryFilter) && string.IsNullOrEmpty(tagFilter)) return true;
            var expectation = LoadExpectation(expectationPath);
            if (!string.IsNullOrEmpty(categoryFilter) && !CategoryEquals(ResolveCategory(expectation), categoryFilter)) return false;
            if (!string.IsNullOrEmpty(tagFilter) && !HasTag(expectation, tagFilter)) return false;
            return true;
        }

        public static string ResolveCategory(MobaAcceptanceExpectation expectation)
        {
            if (expectation == null) return string.Empty;
            if (expectation.scenario != null && !string.IsNullOrEmpty(expectation.scenario.category)) return expectation.scenario.category;
            if (!string.IsNullOrEmpty(expectation.category)) return expectation.category;
            return "contract";
        }

        public static bool HasTag(MobaAcceptanceExpectation expectation, string tag)
        {
            if (expectation == null || string.IsNullOrEmpty(tag)) return false;
            if (HasTag(expectation.tags, tag)) return true;
            return expectation.scenario != null && HasTag(expectation.scenario.tags, tag);
        }

        private static bool HasTag(string[] tags, string tag)
        {
            if (tags == null) return false;
            for (var i = 0; i < tags.Length; i++)
            {
                if (string.Equals(tags[i], tag, StringComparison.OrdinalIgnoreCase)) return true;
            }

            return false;
        }

        private static bool CategoryEquals(string category, string expected)
        {
            return string.Equals(string.IsNullOrEmpty(category) ? "contract" : category, expected, StringComparison.OrdinalIgnoreCase);
        }

        private static void ExportBatchSummary(MobaAcceptanceBatchSummary batch)
        {
            if (batch == null) throw new ArgumentNullException(nameof(batch));
            var path = string.IsNullOrEmpty(batch.batchSummaryJsonPath) ? GetBatchSummaryPath(batch.artifactDirectory) : batch.batchSummaryJsonPath;
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            File.WriteAllText(path, JsonUtility.ToJson(batch, true));
        }

        private static MobaAcceptanceSummary RunLegacySkillExpectation(MobaAcceptanceExpectation expectation, string artifactDirectory, bool exportArtifacts, bool exportTraceText, string expectationPath, string tracePath, string traceTextPath, string summaryPath)
        {
            Assert.IsNotNull(expectation.config, $"Acceptance expectation {expectation.caseId} must include config section.");
            Assert.IsNotNull(expectation.input, $"Acceptance expectation {expectation.caseId} must include input section.");

            using (var harness = MobaSkillConfigTestHarness.CreateForSinglePlayer(
                skillIds: new[] { expectation.config.skillId },
                worldId: expectation.worldId,
                playerId: string.IsNullOrEmpty(expectation.input.playerId) ? MobaSkillConfigTestHarness.DefaultPlayerId : expectation.input.playerId,
                tickRate: expectation.tickRate))
            {
                AssertConfigChain(harness, expectation);

                harness.EnterGameAndWarmup(reason: "moba acceptance " + expectation.caseId);
                harness.AssertSlotSkill(expectation.input.slot, expectation.config.skillId);

                var effectTrace = harness.CastSkillSlotAndTickUntilEffect(
                    slot: expectation.input.slot,
                    skillId: expectation.config.skillId,
                    effectId: expectation.config.effectId);

                harness.AssertSkillCastTrace(expectation.config.skillId);
                AssertRuntimeEffects(harness, expectation, effectTrace.RootId);

                var records = MobaAcceptanceTraceExporter.CaptureTraceRecords(harness, expectation.caseId);
                var summary = MobaAcceptanceTraceExporter.BuildSummary(harness, expectation, records, tracePath, traceTextPath, summaryPath);
                summary.expectationPath = NormalizePath(string.IsNullOrEmpty(expectationPath) ? string.Empty : ResolveProjectRelativePath(expectationPath));

                if (exportArtifacts || exportTraceText)
                {
                    MobaAcceptanceTraceExporter.Export(artifactDirectory, summary, records, exportArtifacts, exportTraceText);
                }

                MobaAcceptanceExpectationAssert.AssertMatches(expectation, records, harness);
                return summary;
            }
        }

        private static MobaAcceptanceSummary RunScenarioExpectation(MobaAcceptanceExpectation expectation, string artifactDirectory, bool exportArtifacts, bool exportTraceText, string expectationPath, string tracePath, string traceTextPath, string summaryPath)
        {
            using (var harness = MobaSkillConfigTestHarness.CreateForScenario(expectation, worldId: expectation.worldId, tickRate: expectation.tickRate))
            {
                if (expectation.config != null)
                {
                    AssertConfigChain(harness, expectation);
                }

                harness.EnterGameAndWarmup(reason: "moba acceptance scenario " + expectation.caseId);
                AssertScenarioSlotSkills(harness, expectation);
                ExecuteSetupActions(harness, GetSetupActions(expectation));
                ExecuteTimeline(harness, GetTimeline(expectation));
                TickScenarioTail(harness, expectation);

                var records = MobaAcceptanceTraceExporter.CaptureTraceRecords(harness, expectation.caseId);
                var summary = MobaAcceptanceTraceExporter.BuildSummary(harness, expectation, records, tracePath, traceTextPath, summaryPath);
                summary.expectationPath = NormalizePath(string.IsNullOrEmpty(expectationPath) ? string.Empty : ResolveProjectRelativePath(expectationPath));

                if (exportArtifacts || exportTraceText)
                {
                    MobaAcceptanceTraceExporter.Export(artifactDirectory, summary, records, exportArtifacts, exportTraceText);
                }

                MobaAcceptanceExpectationAssert.AssertMatches(expectation, records, harness);
                MobaAcceptanceExpectationAssert.AssertStateMatches(expectation, records, harness);
                return summary;
            }
        }

        private static void AssertConfigChain(MobaSkillConfigTestHarness harness, MobaAcceptanceExpectation expectation)
        {
            harness.AssertSkillUsesCastFlow(expectation.config.skillId, expectation.config.castFlowId);
            harness.AssertCastFlowContainsTimelineEffect(expectation.config.castFlowId, expectation.config.effectId);

            if (expectation.config.expectedProjectile != null && expectation.config.expectedProjectile.projectileId > 0)
            {
                harness.AssertProjectileConfigExists(expectation.config.expectedProjectile.launcherId, expectation.config.expectedProjectile.projectileId);
            }

            var actions = expectation.config.expectedActions;
            if (actions == null || actions.Length == 0)
            {
                harness.AssertTriggerPlanContainsActions(expectation.config.triggerId);
                return;
            }

            var actionIds = new int[actions.Length];
            for (var i = 0; i < actions.Length; i++)
            {
                actionIds[i] = actions[i].actionId;
            }

            harness.AssertTriggerPlanContainsActions(expectation.config.triggerId, actionIds);
        }

        private static void AssertRuntimeEffects(MobaSkillConfigTestHarness harness, MobaAcceptanceExpectation expectation, long effectRootId)
        {
            var actions = expectation.config.expectedActions;
            if (actions != null)
            {
                for (var i = 0; i < actions.Length; i++)
                {
                    harness.AssertActionExecutedUnderEffect(effectRootId, actions[i].actionId, actions[i].type);
                }
            }

            if (expectation.config.expectedProjectile != null && expectation.config.expectedProjectile.projectileId > 0)
            {
                harness.AssertProjectileLaunchedUnderEffect(
                    effectRootId,
                    expectation.config.expectedProjectile.launcherId,
                    expectation.config.expectedProjectile.projectileId);
            }
        }

        private static void AssertScenarioSlotSkills(MobaSkillConfigTestHarness harness, MobaAcceptanceExpectation expectation)
        {
            var actors = GetActors(expectation);
            if (actors == null) return;

            for (var i = 0; i < actors.Length; i++)
            {
                var actor = actors[i];
                if (actor == null || string.IsNullOrEmpty(actor.alias) || string.IsNullOrEmpty(actor.playerId)) continue;
                if (actor.skillIds == null || actor.skillIds.Length == 0) continue;

                Assert.IsTrue(harness.TryGetPlayerId(actor.alias, out var playerId), $"Actor alias {actor.alias} is not bound to a player input source.");
                var actorId = harness.AssertPlayerActorBound(playerId, $"Player actor binding missing for scenario actor {actor.alias}({playerId.Value}).");
                var loadout = harness.World.Services.Resolve<AbilityKit.Demo.Moba.Services.MobaSkillLoadoutService>();
                for (var slot = 1; slot <= actor.skillIds.Length; slot++)
                {
                    Assert.IsTrue(loadout.TryGetSkillId(actorId, slot, out var slotSkillId), $"Slot {slot} skill missing for actor {actor.alias}({actorId}).");
                    Assert.AreEqual(actor.skillIds[slot - 1], slotSkillId);
                }
            }
        }

        private static void ExecuteSetupActions(MobaSkillConfigTestHarness harness, MobaAcceptanceSetupActionExpectation[] actions)
        {
            if (actions == null) return;
            for (var i = 0; i < actions.Length; i++)
            {
                var action = actions[i];
                if (action == null) continue;
                if (MobaAcceptanceSetupActionExecutor.IsEnvironmentCommand(action.action)
                    || MobaAcceptanceSetupActionExecutor.IsWaitAction(action.action))
                {
                    MobaAcceptanceSetupActionExecutor.Execute(harness, action);
                    continue;
                }

                ExecuteAction(harness, action);
            }
        }

        private static void ExecuteTimeline(MobaSkillConfigTestHarness harness, MobaAcceptanceTimelineStepExpectation[] timeline)
        {
            if (timeline == null || timeline.Length == 0) return;

            var steps = new List<MobaAcceptanceTimelineStepExpectation>(timeline);
            steps.Sort((a, b) => Math.Max(0, a != null ? a.atMs : 0).CompareTo(Math.Max(0, b != null ? b.atMs : 0)));
            var cursorMs = 0;
            for (var i = 0; i < steps.Count; i++)
            {
                var step = steps[i];
                if (step == null) continue;

                var atMs = Math.Max(0, step.atMs);
                harness.TickMilliseconds(atMs - cursorMs);
                cursorMs = atMs;
                ExecuteAction(harness, step);
            }
        }

        private static void TickScenarioTail(MobaSkillConfigTestHarness harness, MobaAcceptanceExpectation expectation)
        {
            if (expectation != null && expectation.config != null && expectation.config.skillId > 0 && expectation.config.effectId > 0)
            {
                harness.TickMilliseconds(harness.CalculateWaitMillisecondsForSkillEffect(expectation.config.skillId, expectation.config.effectId) + 100);
                return;
            }

            harness.Tick(5);
        }

        private static void ExecuteAction(MobaSkillConfigTestHarness harness, MobaAcceptanceSetupActionExpectation action)
        {
            if (action == null || action.enabled == false && string.Equals(action.action, "disabled", StringComparison.OrdinalIgnoreCase)) return;
            if (IsWaitAction(action.action))
            {
                harness.TickMilliseconds(action.durationMs);
                return;
            }

            if (IsSkillAction(action.action))
            {
                var actorAlias = ResolveActorAlias(harness, action.actorAlias);
                AssertSkillInputHandled(
                    harness,
                    actorAlias,
                    action.slot,
                    ResolveSkillInputPhase(action.action),
                    action.targetAlias,
                    action.targetActorId,
                    action.position,
                    action.direction,
                    $"setup action={action.action}");
                AdvanceAcceptedSkillInput(harness);
            }
        }

        private static void ExecuteAction(MobaSkillConfigTestHarness harness, MobaAcceptanceTimelineStepExpectation step)
        {
            if (step == null) return;
            if (IsWaitAction(step.action))
            {
                harness.TickMilliseconds(step.durationMs);
                return;
            }

            if (MobaAcceptanceSetupActionExecutor.IsEnvironmentCommand(step.action))
            {
                MobaAcceptanceSetupActionExecutor.Execute(harness, ConvertTimelineStepToSetupAction(step));
                return;
            }

            if (IsSkillAction(step.action))
            {
                var actorAlias = ResolveActorAlias(harness, step.actorAlias);
                AssertSkillInputHandled(
                    harness,
                    actorAlias,
                    step.slot,
                    ResolveSkillInputPhase(step.action),
                    step.targetAlias,
                    step.targetActorId,
                    step.position,
                    step.direction,
                    $"timeline action={step.action} atMs={Math.Max(0, step.atMs)}");
                AdvanceAcceptedSkillInput(harness);
            }
        }

        private static void AssertSkillInputHandled(
            MobaSkillConfigTestHarness harness,
            string actorAlias,
            int slot,
            string phase,
            string targetAlias,
            int targetActorId,
            MobaAcceptanceVector3Expectation position,
            MobaAcceptanceVector3Expectation direction,
            string context)
        {
            var result = harness.SubmitSkillInputAndGetResult(actorAlias, slot, phase, out var castFrame, targetAlias, targetActorId, position, direction);
            Assert.IsTrue(
                result.Succeeded,
                $"Scenario skill input rejected. {context}, actorAlias={actorAlias}, slot={slot}, phase={phase}, castFrame={castFrame.Value}, result={result}");
            Assert.Greater(
                result.HandledCount,
                0,
                $"Scenario skill input accepted but not handled. {context}, actorAlias={actorAlias}, slot={slot}, phase={phase}, castFrame={castFrame.Value}, result={result}");
        }

        private static void AdvanceAcceptedSkillInput(MobaSkillConfigTestHarness harness)
        {
            // Scenario input is submitted for Frame + 1. Advance one fixed step so the
            // runtime consumes the command before any larger accelerated wait is applied.
            harness.Tick(1);
        }

        private static MobaAcceptanceSetupActionExpectation ConvertTimelineStepToSetupAction(MobaAcceptanceTimelineStepExpectation step)
        {
            return new MobaAcceptanceSetupActionExpectation
            {
                action = step.action,
                actorAlias = step.actorAlias,
                targetAlias = step.targetAlias,
                playerId = step.playerId,
                slot = step.slot,
                skillId = step.skillId,
                targetActorId = step.targetActorId,
                durationMs = step.durationMs,
                property = step.property,
                value = step.value,
                intValue = step.intValue,
                position = step.position,
                direction = step.direction,
                payload = step.payload,
                note = step.note
            };
        }

        private static string ResolveActorAlias(MobaSkillConfigTestHarness harness, string actorAlias)
        {
            if (!string.IsNullOrEmpty(actorAlias)) return actorAlias;
            if (harness != null && harness.ScenarioActors != null)
            {
                for (var i = 0; i < harness.ScenarioActors.Length; i++)
                {
                    var actor = harness.ScenarioActors[i];
                    if (actor != null && !string.IsNullOrEmpty(actor.alias) && !string.IsNullOrEmpty(actor.playerId)) return actor.alias;
                }
            }

            return MobaSkillConfigTestHarness.DefaultPlayerId;
        }

        private static string ResolveSkillInputPhase(string action)
        {
            if (string.Equals(action, "release", StringComparison.OrdinalIgnoreCase)) return "release";
            if (string.Equals(action, "hold", StringComparison.OrdinalIgnoreCase)) return "hold";
            if (string.Equals(action, "cancel", StringComparison.OrdinalIgnoreCase)) return "cancel";
            return "press";
        }

        private static bool IsSkillAction(string action)
        {
            return string.IsNullOrEmpty(action)
                || string.Equals(action, "cast_skill", StringComparison.OrdinalIgnoreCase)
                || string.Equals(action, "skill_input", StringComparison.OrdinalIgnoreCase)
                || string.Equals(action, "press", StringComparison.OrdinalIgnoreCase)
                || string.Equals(action, "release", StringComparison.OrdinalIgnoreCase)
                || string.Equals(action, "hold", StringComparison.OrdinalIgnoreCase)
                || string.Equals(action, "cancel", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsWaitAction(string action)
        {
            return string.Equals(action, "wait", StringComparison.OrdinalIgnoreCase)
                || string.Equals(action, "tick", StringComparison.OrdinalIgnoreCase);
        }

        private static bool HasScenarioFlow(MobaAcceptanceExpectation expectation)
        {
            return expectation != null
                && (expectation.scenario != null
                    || HasAny(expectation.actors)
                    || HasAny(GetSetupActions(expectation))
                    || HasAny(GetTimeline(expectation))
                    || HasAny(GetStateExpectations(expectation))
                    || HasAny(GetContextExpectations(expectation)));
        }

        private static string ResolveWorldId(MobaAcceptanceExpectation expectation, string caseId)
        {
            if (expectation != null && expectation.scenario != null && !string.IsNullOrEmpty(expectation.scenario.worldId)) return expectation.scenario.worldId;
            if (expectation != null && !string.IsNullOrEmpty(expectation.worldId)) return expectation.worldId;
            return caseId + "_world";
        }

        private static int ResolveTickRate(MobaAcceptanceExpectation expectation)
        {
            if (expectation != null && expectation.scenario != null && expectation.scenario.tickRate > 0) return expectation.scenario.tickRate;
            if (expectation != null && expectation.tickRate > 0) return expectation.tickRate;
            return 30;
        }

        private static MobaAcceptanceActorExpectation[] GetActors(MobaAcceptanceExpectation expectation)
        {
            if (expectation == null) return null;
            if (expectation.scenario != null && expectation.scenario.actors != null && expectation.scenario.actors.Length > 0) return expectation.scenario.actors;
            return expectation.actors;
        }

        private static MobaAcceptanceSetupActionExpectation[] GetSetupActions(MobaAcceptanceExpectation expectation)
        {
            if (expectation == null) return null;
            if (expectation.scenario != null && expectation.scenario.setupActions != null && expectation.scenario.setupActions.Length > 0) return expectation.scenario.setupActions;
            return expectation.setupActions;
        }

        private static MobaAcceptanceTimelineStepExpectation[] GetTimeline(MobaAcceptanceExpectation expectation)
        {
            if (expectation == null) return null;
            if (expectation.scenario != null && expectation.scenario.timeline != null && expectation.scenario.timeline.Length > 0) return expectation.scenario.timeline;
            return expectation.timeline;
        }

        private static MobaAcceptanceStateExpectation[] GetStateExpectations(MobaAcceptanceExpectation expectation)
        {
            if (expectation == null) return null;
            if (expectation.scenario != null && expectation.scenario.stateExpectations != null && expectation.scenario.stateExpectations.Length > 0) return expectation.scenario.stateExpectations;
            return expectation.stateExpectations;
        }

        private static MobaAcceptanceContextExpectation[] GetContextExpectations(MobaAcceptanceExpectation expectation)
        {
            if (expectation == null) return null;
            if (expectation.scenario != null && expectation.scenario.contextExpectations != null && expectation.scenario.contextExpectations.Length > 0) return expectation.scenario.contextExpectations;
            return expectation.contextExpectations;
        }

        private static bool HasAny<T>(T[] values)
        {
            return values != null && values.Length > 0;
        }

        private static string NormalizePath(string path)
        {
            return string.IsNullOrEmpty(path) ? path : path.Replace('\\', '/');
        }
    }
}
