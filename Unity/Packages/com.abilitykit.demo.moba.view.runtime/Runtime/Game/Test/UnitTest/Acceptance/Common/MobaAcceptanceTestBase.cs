using System.IO;
using NUnit.Framework;

namespace AbilityKit.Game.Test.UnitTest
{
    public abstract class MobaAcceptanceTestBase
    {
        protected const string ExpectationDirectory = "Unity/Packages/com.abilitykit.demo.moba.view.runtime/Runtime/Game/Test/Expectations";
        protected const string SkillsPath = "Unity/Packages/com.abilitykit.demo.moba.view.runtime/Resources/moba/skills.json";
        protected const string SkillFlowsPath = "Unity/Packages/com.abilitykit.demo.moba.view.runtime/Resources/moba/skill_flows.json";
        protected const string SkillTriggerDirectory = "Unity/Packages/com.abilitykit.demo.moba.view.runtime/Resources/ability/triggers/skills";
        protected const string ArtifactDirectory = "local/Logs/moba-acceptance";

        protected static MobaAcceptanceSummary RunExpectationFile(string expectationPath, bool exportArtifacts = true)
        {
            return MobaAcceptanceRunner.RunSkillExpectationFile(expectationPath, ArtifactDirectory, exportArtifacts);
        }

        protected static MobaAcceptanceSummary RunExpectation(MobaAcceptanceExpectation expectation, bool exportArtifacts = true)
        {
            return MobaAcceptanceRunner.RunSkillExpectation(expectation, ArtifactDirectory, exportArtifacts);
        }

        protected static MobaAcceptanceExpectation LoadExpectation(string expectationPath)
        {
            return MobaAcceptanceRunner.LoadExpectation(expectationPath);
        }

        protected static MobaAcceptanceTraceRecord[] LoadTraceRecords(MobaAcceptanceSummary summary)
        {
            Assert.IsNotNull(summary, "Acceptance summary is required before loading trace records.");
            return MobaAcceptanceRunner.LoadTraceRecords(summary.traceJsonlPath);
        }

        protected static void AssertPassed(MobaAcceptanceSummary summary)
        {
            Assert.IsNotNull(summary, "Acceptance summary is required.");
            Assert.IsTrue(summary.result.passed);
        }

        protected static void AssertNoMissingTraceNodes(MobaAcceptanceSummary summary)
        {
            Assert.IsNotNull(summary, "Acceptance summary is required.");
            Assert.AreEqual(0, summary.result.missingExpectedTraceNodeCount, summary.coverage != null ? summary.coverage.missingTraceNodes : string.Empty);
        }

        protected static void AssertArtifactsExist(MobaAcceptanceSummary summary)
        {
            Assert.IsNotNull(summary, "Acceptance summary is required.");
            Assert.IsTrue(File.Exists(summary.traceJsonlPath), $"Trace jsonl artifact missing: {summary.traceJsonlPath}");
            Assert.IsTrue(File.Exists(summary.summaryJsonPath), $"Summary json artifact missing: {summary.summaryJsonPath}");
        }
    }
}
