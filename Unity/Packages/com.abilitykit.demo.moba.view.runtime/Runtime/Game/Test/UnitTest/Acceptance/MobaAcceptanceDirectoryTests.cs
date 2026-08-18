using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;

namespace AbilityKit.Game.Test.UnitTest
{
    public sealed class MobaAcceptanceDirectoryTests : MobaAcceptanceTestBase
    {
        private const string Skill10010101ExpectationPath = "Unity/Packages/com.abilitykit.demo.moba.view.runtime/Runtime/Game/Test/Expectations/skill_10010101.expected.json";
        private const string Skill10020301ExpectationPath = "Unity/Packages/com.abilitykit.demo.moba.view.runtime/Runtime/Game/Test/Expectations/skill_10020301.expected.json";

        [Test]
        public void TimelineEffectSkills_WithConfiguredTriggers_HaveFormalAcceptanceContracts()
        {
            var skillFlowsPath = MobaAcceptanceRunner.ResolveProjectRelativePath(SkillFlowsPath);
            var triggerDirectory = MobaAcceptanceRunner.ResolveProjectRelativePath(SkillTriggerDirectory);
            var expectationDirectory = MobaAcceptanceRunner.ResolveProjectRelativePath(ExpectationDirectory);
            var expectedSkillIds = FindSkillIdsWithConfiguredSkillTriggers(skillFlowsPath, triggerDirectory);
            var coveredSkillIds = FindCoveredSkillIds(expectationDirectory, formalOnly: true);

            CollectionAssert.IsSubsetOf(expectedSkillIds, coveredSkillIds, "Every configured timeline effect skill must have at least one formal contract/golden *.expected.json before the skill is considered done; draft files do not satisfy this gate.");
        }

        [Test]
        public void AcceptanceExpectationDirectory_ExportsBatchSummary()
        {
            var batch = MobaAcceptanceRunner.RunExpectationDirectory(
                new MobaAcceptanceRunOptions
                {
                    ArtifactDirectory = ArtifactDirectory,
                    ExportArtifacts = true,
                    TraceExport = new MobaAcceptanceTraceExportOptions(),
                    Recursive = true
                },
                ExpectationDirectory);

            Assert.GreaterOrEqual(batch.total, 1);
            Assert.AreEqual(batch.total, batch.passed + batch.failed);
            Assert.IsTrue(string.IsNullOrEmpty(batch.categoryFilter));
            Assert.IsTrue(batch.allPassed, "Acceptance batch has failed cases; inspect " + batch.batchSummaryJsonPath);
            Assert.IsTrue(File.Exists(batch.batchSummaryJsonPath), $"Batch summary artifact missing: {batch.batchSummaryJsonPath}");
            for (var i = 0; i < batch.results.Length; i++)
            {
                Assert.IsNotNull(batch.results[i].summary, "Passing batch case must expose its summary: " + batch.results[i].caseId);
                Assert.IsTrue(
                    File.Exists(batch.results[i].summary.traceTextPath),
                    "Human-readable trace artifact missing: " + batch.results[i].summary.traceTextPath);
            }
        }

        [Test]
        public void RunOptions_DefaultDoesNotExportTraceText()
        {
            var options = new MobaAcceptanceRunOptions
            {
                ArtifactDirectory = Path.Combine(ArtifactDirectory, "default-no-trace-" + Guid.NewGuid().ToString("N")),
                ExportArtifacts = false
            };

            Assert.IsNull(options.TraceExport);
            Assert.IsFalse(options.ShouldExportTraceText);

            var summary = MobaAcceptanceRunner.RunSkillExpectationFile(options, Skill10010101ExpectationPath);

            Assert.IsFalse(File.Exists(summary.traceTextPath), "Default run options must not write a trace text artifact: " + summary.traceTextPath);
        }

        [Test]
        public void TraceText_FormatsParentChildTreeAndDiagnosticFields()
        {
            var summary = new MobaAcceptanceSummary
            {
                caseId = "trace_text_example",
                worldId = "test-world",
                tickRate = 30,
                result = new MobaAcceptanceResult
                {
                    passed = true,
                    finalFrame = 8,
                    finalTimeMs = 267,
                    effectRootId = 1
                },
                coverage = new MobaAcceptanceCoverageSummary
                {
                    expectedTraceNodeCount = 2,
                    matchedExpectedTraceNodeCount = 2,
                    expectedActionCount = 1,
                    executedExpectedActionCount = 1
                },
                traceCounts = new[]
                {
                    new MobaAcceptanceTraceCount { kind = "EffectAction", count = 1 },
                    new MobaAcceptanceTraceCount { kind = "EffectExecution", count = 1 }
                }
            };
            var records = new[]
            {
                new MobaAcceptanceTraceRecord
                {
                    nodeId = 1,
                    rootId = 1,
                    kind = "EffectExecution",
                    kindValue = 2,
                    configId = 1001,
                    configLabel = "Effect Fire (#1001)",
                    sourceActorId = 10,
                    sourceActorLabel = "来源角色 player (#10)",
                    targetActorId = 20,
                    targetActorLabel = "目标角色 enemy (#20)",
                    childCount = 1,
                    isRoot = true
                },
                new MobaAcceptanceTraceRecord
                {
                    nodeId = 2,
                    rootId = 1,
                    parentId = 1,
                    kind = "EffectAction",
                    kindValue = 3,
                    configId = 2001,
                    configLabel = "Damage (#2001)",
                    frame = 3,
                    timeMs = 100,
                    isEnded = true,
                    endedFrame = 3,
                    endReason = 1
                }
            };

            var text = MobaAcceptanceTraceExporter.BuildTraceText(summary, records);

            StringAssert.Contains("用例: trace_text_example", text);
            StringAssert.Contains("Root #1", text);
            StringAssert.Contains("- [1] EffectExecution", text);
            StringAssert.Contains("  - [2] EffectAction", text);
            StringAssert.Contains("配置: id=2001", text);
            StringAssert.Contains("状态=已结束 endedFrame=3 reason=1", text);
        }

        [Test]
        public void ContractCategory_ShouldContainBuffAcceptanceContract()
        {
            var expectation = LoadExpectation(Skill10020301ExpectationPath);
            Assert.AreEqual("contract", MobaAcceptanceRunner.ResolveCategory(expectation));
            Assert.IsFalse(MobaAcceptanceRunner.HasTag(expectation, "golden"));
            Assert.IsTrue(MobaAcceptanceRunner.HasTag(expectation, "buff"));
        }

        [Test]
        public void GoldenCategory_ShouldContainRepresentativeGoldenSample()
        {
            var expectation = LoadExpectation(Skill10010101ExpectationPath);
            Assert.AreEqual("golden", MobaAcceptanceRunner.ResolveCategory(expectation));
            Assert.IsTrue(MobaAcceptanceRunner.HasTag(expectation, "golden"));
            Assert.IsTrue(MobaAcceptanceRunner.HasTag(expectation, "dash"));
        }

        [Test]
        public void AcceptanceRunner_ShouldFilterByCategoryAndTag()
        {
            var contractBatch = MobaAcceptanceRunner.RunContractExpectationDirectory(ExpectationDirectory, ArtifactDirectory, exportArtifacts: false, recursive: true, tagFilter: "buff");
            var goldenBatch = MobaAcceptanceRunner.RunGoldenExpectationDirectory(ExpectationDirectory, ArtifactDirectory, exportArtifacts: false, recursive: true, tagFilter: "projectile");

            Assert.AreEqual("contract", contractBatch.categoryFilter);
            Assert.AreEqual("buff", contractBatch.tagFilter);
            Assert.GreaterOrEqual(contractBatch.total, 1);
            Assert.IsTrue(contractBatch.allPassed);
            Assert.AreEqual("golden", goldenBatch.categoryFilter);
            Assert.AreEqual("projectile", goldenBatch.tagFilter);
            Assert.GreaterOrEqual(goldenBatch.total, 1);
            Assert.IsTrue(goldenBatch.allPassed);
        }

        private static int[] FindSkillIdsWithConfiguredSkillTriggers(string skillFlowsPath, string triggerDirectory)
        {
            Assert.IsTrue(File.Exists(skillFlowsPath), "Skill flow config missing: " + skillFlowsPath);
            Assert.IsTrue(Directory.Exists(triggerDirectory), "Skill trigger directory missing: " + triggerDirectory);

            var json = File.ReadAllText(skillFlowsPath);
            var result = new List<int>();
            var cursor = 0;
            while (TryReadNextIntProperty(json, "\"Id\"", ref cursor, out var skillId))
            {
                var nextEntry = json.IndexOf("\"Id\"", cursor, StringComparison.Ordinal);
                var entryEnd = nextEntry >= 0 ? nextEntry : json.Length;
                var entry = json.Substring(cursor, entryEnd - cursor);
                var effectCursor = 0;
                while (TryReadNextIntProperty(entry, "\"EffectId\"", ref effectCursor, out var effectId))
                {
                    if (File.Exists(Path.Combine(triggerDirectory, "trigger_" + effectId + ".json")))
                    {
                        result.Add(skillId);
                        break;
                    }
                }
            }

            result.Sort();
            return result.ToArray();
        }

        private static int[] FindCoveredSkillIds(string expectationDirectory, bool formalOnly)
        {
            Assert.IsTrue(Directory.Exists(expectationDirectory), "Expectation directory missing: " + expectationDirectory);

            var files = Directory.GetFiles(expectationDirectory, "*.expected.json", SearchOption.AllDirectories);
            var result = new List<int>();
            for (var i = 0; i < files.Length; i++)
            {
                var expectation = MobaAcceptanceRunner.LoadExpectation(files[i]);
                var category = MobaAcceptanceRunner.ResolveCategory(expectation);
                if (formalOnly && string.Equals(category, "draft", StringComparison.OrdinalIgnoreCase)) continue;
                if (formalOnly && !string.Equals(category, "contract", StringComparison.OrdinalIgnoreCase) && !string.Equals(category, "golden", StringComparison.OrdinalIgnoreCase)) continue;
                if (expectation.config != null && expectation.config.skillId > 0) result.Add(expectation.config.skillId);
            }

            result.Sort();
            return result.ToArray();
        }

        private static bool TryReadNextIntProperty(string json, string propertyName, ref int cursor, out int value)
        {
            value = 0;
            var propertyIndex = json.IndexOf(propertyName, cursor, StringComparison.Ordinal);
            if (propertyIndex < 0) return false;

            var colonIndex = json.IndexOf(':', propertyIndex + propertyName.Length);
            if (colonIndex < 0) return false;

            var start = colonIndex + 1;
            while (start < json.Length && char.IsWhiteSpace(json[start])) start++;
            var end = start;
            if (end < json.Length && json[end] == '-') end++;
            while (end < json.Length && char.IsDigit(json[end])) end++;
            cursor = end;
            return int.TryParse(json.Substring(start, end - start), out value);
        }
    }
}
