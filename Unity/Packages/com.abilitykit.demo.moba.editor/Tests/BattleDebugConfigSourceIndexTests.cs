using AbilityKit.Demo.Moba.Diagnostics;
using AbilityKit.Game.Editor;
using NUnit.Framework;

namespace AbilityKit.Demo.Moba.Diagnostics.Tests
{
    public sealed class BattleDebugConfigSourceIndexTests
    {
        private static readonly BattleDiagnosticSessionScope Scope =
            new BattleDiagnosticSessionScope("test", "config-source", 1);

        [Test]
        public void TryFindInJson_ArrayRoot_FindsExactIdLine()
        {
            const string json = "[\n" +
                                "  { \"Id\": 1, \"Name\": \"first\" },\n" +
                                "  {\n" +
                                "    \"Id\": 20,\n" +
                                "    \"Name\": \"second\"\n" +
                                "  }\n" +
                                "]";
            var reference = new BattleDebugConfigReference(BattleDebugConfigKind.Skill, 20);

            var found = BattleDebugConfigSourceIndex.TryFindInJson(
                json,
                in reference,
                out var lineNumber,
                out var error);

            Assert.That(found, Is.True, error);
            Assert.That(lineNumber, Is.EqualTo(4));
        }

        [Test]
        public void TryFindInJson_TriggerRoot_UsesTriggerId()
        {
            const string json = "{\n" +
                                "  \"FormatVersion\": 1,\n" +
                                "  \"Triggers\": [\n" +
                                "    { \"TriggerId\": 7, \"Actions\": [] }\n" +
                                "  ]\n" +
                                "}";
            var reference = new BattleDebugConfigReference(BattleDebugConfigKind.TriggerPlan, 7);

            var found = BattleDebugConfigSourceIndex.TryFindInJson(
                json,
                in reference,
                out var lineNumber,
                out var error);

            Assert.That(found, Is.True, error);
            Assert.That(lineNumber, Is.EqualTo(4));
        }

        [Test]
        public void TryFindInJson_SkillFlow_FindsNestedPhaseWithinMatchingFlow()
        {
            const string json = "[\n" +
                                "  {\n" +
                                "    \"Id\": 10,\n" +
                                "    \"Phases\": [{ \"PhaseId\": \"other\" }]\n" +
                                "  },\n" +
                                "  {\n" +
                                "    \"Id\": 20,\n" +
                                "    \"Phases\": [\n" +
                                "      {\n" +
                                "        \"PhaseId\": \"release\",\n" +
                                "        \"Children\": [{ \"PhaseId\": \"commit\" }]\n" +
                                "      }\n" +
                                "    ]\n" +
                                "  }\n" +
                                "]";
            var reference = new BattleDebugConfigReference(
                BattleDebugConfigKind.SkillFlow,
                20,
                "commit");

            var found = BattleDebugConfigSourceIndex.TryFindInJson(
                json,
                in reference,
                out var lineNumber,
                out var error);

            Assert.That(found, Is.True, error);
            Assert.That(lineNumber, Is.EqualTo(11));
        }

        [Test]
        public void TryFindInJson_MissingPhase_DoesNotFallBackToFlowEntry()
        {
            const string json = "[{ \"Id\": 20, \"Phases\": [{ \"PhaseId\": \"release\" }] }]";
            var reference = new BattleDebugConfigReference(
                BattleDebugConfigKind.SkillFlow,
                20,
                "missing");

            var found = BattleDebugConfigSourceIndex.TryFindInJson(
                json,
                in reference,
                out _,
                out var error);

            Assert.That(found, Is.False);
            StringAssert.Contains("missing", error);
        }

        [Test]
        public void TryLocate_ExistingPackageSkill_UsesAuthoritativeJson()
        {
            var reference = new BattleDebugConfigReference(BattleDebugConfigKind.Skill, 1);

            var found = BattleDebugConfigSourceIndex.TryLocate(
                in reference,
                out var location,
                out var error);

            Assert.That(found, Is.True, error);
            Assert.That(location.Asset, Is.Not.Null);
            Assert.That(location.AssetPath, Does.EndWith("/Resources/moba/skills.json"));
            Assert.That(location.LineNumber, Is.GreaterThan(0));
        }

        [Test]
        public void EventMapper_UsesStructuredTriggerPayloadId()
        {
            var payload = new BattleDiagnosticTriggerAnalysisPayload(
                9001,
                1,
                2,
                BattleDiagnosticTriggerAnalysisStage.Execution,
                BattleDiagnosticTriggerAnalysisResult.Failed,
                detailCode: 0,
                currentDepth: 0,
                currentFrameCount: 0,
                currentRootCount: 0,
                currentSameTriggerCount: 0,
                failureKey: string.Empty,
                reason: "failed");
            var diagnosticEvent = new BattleDiagnosticEvent(
                Scope,
                1,
                1,
                1,
                BattleDiagnosticEventKind.TriggerAnalysis,
                BattleDiagnosticEventChannel.Effect,
                BattleDiagnosticEventOutcome.Failed,
                configId: 123,
                payloadVersion: BattleDiagnosticTriggerAnalysisPayload.CurrentSchemaVersion,
                payload: BattleDiagnosticEventPayload.FromTriggerAnalysis(in payload));

            var mapped = BattleDebugConfigReferenceMapper.TryFromEvent(
                in diagnosticEvent,
                out var reference);

            Assert.That(mapped, Is.True);
            Assert.That(reference.Kind, Is.EqualTo(BattleDebugConfigKind.TriggerPlan));
            Assert.That(reference.Id, Is.EqualTo(9001));
        }

        [TestCase(BattleDiagnosticEventKind.Damage)]
        [TestCase(BattleDiagnosticEventKind.Heal)]
        public void EventMapper_RejectsAmbiguousReasonIds(BattleDiagnosticEventKind eventKind)
        {
            var diagnosticEvent = new BattleDiagnosticEvent(
                Scope,
                1,
                1,
                1,
                eventKind,
                BattleDiagnosticEventChannel.DamageAndHeal,
                BattleDiagnosticEventOutcome.Succeeded,
                configId: 42);

            var mapped = BattleDebugConfigReferenceMapper.TryFromEvent(
                in diagnosticEvent,
                out var reference);

            Assert.That(mapped, Is.False);
            Assert.That(reference.IsValid, Is.False);
        }

        [TestCase("SkillPhase", (int)BattleDebugConfigKind.Skill)]
        [TestCase("EffectExecution", (int)BattleDebugConfigKind.Effect)]
        [TestCase("BuffTick", (int)BattleDebugConfigKind.Buff)]
        [TestCase("ProjectileHit", (int)BattleDebugConfigKind.Projectile)]
        [TestCase("AreaStay", (int)BattleDebugConfigKind.Area)]
        [TestCase("SummonDeath", (int)BattleDebugConfigKind.Summon)]
        public void TraceMapper_MapsOnlyStableConfigKinds(
            string traceKind,
            int expectedKind)
        {
            var node = TraceNode(traceKind, 42);

            var mapped = BattleDebugConfigReferenceMapper.TryFromTraceNode(
                in node,
                out var reference);

            Assert.That(mapped, Is.True);
            Assert.That((int)reference.Kind, Is.EqualTo(expectedKind));
            Assert.That(reference.Id, Is.EqualTo(42));
        }

        [TestCase("EffectAction")]
        [TestCase("DamageApply")]
        [TestCase("UnknownKind")]
        public void TraceMapper_RejectsAmbiguousOrUnknownIds(string traceKind)
        {
            var node = TraceNode(traceKind, 42);

            var mapped = BattleDebugConfigReferenceMapper.TryFromTraceNode(
                in node,
                out var reference);

            Assert.That(mapped, Is.False);
            Assert.That(reference.IsValid, Is.False);
        }

        [Test]
        public void TraceMapper_MapsRealSkillPhaseToExactFlowPhase()
        {
            var node = TraceNode(
                "SkillPhase",
                configId: 1001,
                skillId: 1001,
                castFlowId: 7001,
                phaseId: "cast.release");

            var mapped = BattleDebugConfigReferenceMapper.TryFromTraceNode(
                in node,
                out var reference);

            Assert.That(mapped, Is.True);
            Assert.That(reference.Kind, Is.EqualTo(BattleDebugConfigKind.SkillFlow));
            Assert.That(reference.Id, Is.EqualTo(7001));
            Assert.That(reference.PhaseId, Is.EqualTo("cast.release"));
        }

        private static BattleDiagnosticTraceNodeSummary TraceNode(
            string kind,
            int configId,
            int skillId = 0,
            int castFlowId = 0,
            string phaseId = "")
        {
            return new BattleDiagnosticTraceNodeSummary(
                Scope,
                1,
                1,
                0,
                0,
                -1,
                BattleDiagnosticTraceNodeState.Active,
                configId: configId,
                kind: kind,
                skillId: skillId,
                castFlowId: castFlowId,
                phaseId: phaseId);
        }
    }
}
