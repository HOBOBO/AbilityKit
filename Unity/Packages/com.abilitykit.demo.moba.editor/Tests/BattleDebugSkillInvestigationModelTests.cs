using AbilityKit.Demo.Moba.Diagnostics;
using AbilityKit.Game.Editor;
using NUnit.Framework;

namespace AbilityKit.Demo.Moba.Diagnostics.Tests
{
    public sealed class BattleDebugSkillInvestigationModelTests
    {
        private static readonly BattleDiagnosticSessionScope Scope =
            new BattleDiagnosticSessionScope("test", "investigation", 1);

        [Test]
        public void Build_RootTraceCaseIncludesSurroundingEvidenceAndConfirmsConditionFailure()
        {
            var condition = new BattleDiagnosticTriggerAnalysisPayload(
                7001,
                contextKind: 2,
                originKind: 3,
                BattleDiagnosticTriggerAnalysisStage.Conditions,
                BattleDiagnosticTriggerAnalysisResult.Failed,
                failureKey: "missingMana",
                reason: "Mana is insufficient.");
            var events = new[]
            {
                Event(10, 1, BattleDiagnosticEventKind.EffectStarted, BattleDiagnosticEventOutcome.Succeeded, 900, 901),
                Event(12, 2, BattleDiagnosticEventKind.TriggerAnalysis, BattleDiagnosticEventOutcome.Failed, 900, 902, BattleDiagnosticEventPayload.FromTriggerAnalysis(in condition)),
                Event(14, 3, BattleDiagnosticEventKind.EffectEnded, BattleDiagnosticEventOutcome.Failed, 900, 901)
            };

            var cases = BattleDebugSkillInvestigationModel.Build(events);

            Assert.That(cases, Has.Count.EqualTo(1));
            Assert.That(cases[0].Key, Is.EqualTo("root:900"));
            Assert.That(cases[0].Evidence, Has.Count.EqualTo(3));
            Assert.That(cases[0].Cause, Is.EqualTo(BattleDebugInvestigationCause.TriggerConditionFailed));
            Assert.That(cases[0].Confidence, Is.EqualTo(BattleDebugInvestigationConfidence.Confirmed));
            Assert.That(cases[0].FirstFrame, Is.EqualTo(10));
            Assert.That(cases[0].LastFrame, Is.EqualTo(14));
        }

        [Test]
        public void Build_TraceLessFailuresRemainSeparateAndUseEvidenceConfidence()
        {
            var events = new[]
            {
                Event(20, 2, BattleDiagnosticEventKind.EffectEnded, BattleDiagnosticEventOutcome.Failed),
                Event(18, 1, BattleDiagnosticEventKind.Damage, BattleDiagnosticEventOutcome.Failed)
            };

            var cases = BattleDebugSkillInvestigationModel.Build(events);

            Assert.That(cases, Has.Count.EqualTo(2));
            Assert.That(cases[0].Key, Is.EqualTo("event:2"));
            Assert.That(cases[0].Cause, Is.EqualTo(BattleDebugInvestigationCause.EffectExecutionFailed));
            Assert.That(cases[0].Confidence, Is.EqualTo(BattleDebugInvestigationConfidence.Inferred));
            Assert.That(cases[1].Key, Is.EqualTo("event:1"));
            Assert.That(cases[1].Cause, Is.EqualTo(BattleDebugInvestigationCause.Unknown));
            Assert.That(cases[1].Confidence, Is.EqualTo(BattleDebugInvestigationConfidence.InsufficientEvidence));
        }

        [Test]
        public void Build_StructuredSkillFailureIsConfirmedAndPreservesStableCode()
        {
            var failure = new BattleDiagnosticSkillFailurePayload(
                slot: 2,
                source: "Cast",
                stage: "Preparation",
                code: "Cast.TargetOutOfRange",
                message: "Target is outside cast range.");
            var events = new[]
            {
                Event(
                    25,
                    4,
                    BattleDiagnosticEventKind.SkillFailure,
                    BattleDiagnosticEventOutcome.Failed,
                    payload: BattleDiagnosticEventPayload.FromSkillFailure(in failure))
            };

            var cases = BattleDebugSkillInvestigationModel.Build(events);

            Assert.That(cases, Has.Count.EqualTo(1));
            Assert.That(cases[0].Key, Is.EqualTo("event:4"));
            Assert.That(cases[0].Cause, Is.EqualTo(BattleDebugInvestigationCause.SkillFailure));
            Assert.That(cases[0].Confidence, Is.EqualTo(BattleDebugInvestigationConfidence.Confirmed));
            Assert.That(cases[0].Conclusion, Does.Contain("Cast.TargetOutOfRange"));
            Assert.That(cases[0].EvidenceSummary, Does.Contain("Target is outside cast range."));
        }

        [Test]
        public void Build_CaseFiltersComposeConfidenceAndCause()
        {
            var failure = new BattleDiagnosticSkillFailurePayload(
                slot: 2,
                source: "Cast",
                stage: "Preparation",
                code: "Cast.TargetOutOfRange",
                message: "Target is outside cast range.");
            var events = new[]
            {
                Event(
                    30,
                    3,
                    BattleDiagnosticEventKind.SkillFailure,
                    BattleDiagnosticEventOutcome.Failed,
                    payload: BattleDiagnosticEventPayload.FromSkillFailure(in failure)),
                Event(20, 2, BattleDiagnosticEventKind.EffectEnded, BattleDiagnosticEventOutcome.Failed),
                Event(10, 1, BattleDiagnosticEventKind.Damage, BattleDiagnosticEventOutcome.Failed)
            };

            var confirmedSkillFailures = BattleDebugSkillInvestigationModel.Build(
                events,
                BattleDebugInvestigationConfidenceFilter.Confirmed,
                BattleDebugInvestigationCauseFilter.SkillFailure);
            var inferredEffects = BattleDebugSkillInvestigationModel.Build(
                events,
                BattleDebugInvestigationConfidenceFilter.Inferred,
                BattleDebugInvestigationCauseFilter.EffectExecutionFailed);

            Assert.That(confirmedSkillFailures, Has.Count.EqualTo(1));
            Assert.That(confirmedSkillFailures[0].Key, Is.EqualTo("event:3"));
            Assert.That(inferredEffects, Has.Count.EqualTo(1));
            Assert.That(inferredEffects[0].Key, Is.EqualTo("event:2"));
        }

        [Test]
        public void Build_BudgetBlockIsConfirmedAndMaximumCasesKeepsNewestCase()
        {
            var budget = new BattleDiagnosticTriggerAnalysisPayload(
                7002,
                contextKind: 2,
                originKind: 3,
                BattleDiagnosticTriggerAnalysisStage.Budget,
                BattleDiagnosticTriggerAnalysisResult.Blocked,
                failureKey: "DepthLimit",
                reason: "Trigger depth limit reached.");
            var events = new[]
            {
                Event(10, 1, BattleDiagnosticEventKind.Damage, BattleDiagnosticEventOutcome.Failed),
                Event(30, 2, BattleDiagnosticEventKind.TriggerAnalysis, BattleDiagnosticEventOutcome.Failed, 901, 902, BattleDiagnosticEventPayload.FromTriggerAnalysis(in budget))
            };

            var cases = BattleDebugSkillInvestigationModel.Build(events, maximumCases: 1);

            Assert.That(cases, Has.Count.EqualTo(1));
            Assert.That(cases[0].Key, Is.EqualTo("root:901"));
            Assert.That(cases[0].Cause, Is.EqualTo(BattleDebugInvestigationCause.TriggerBudgetBlocked));
            Assert.That(cases[0].Confidence, Is.EqualTo(BattleDebugInvestigationConfidence.Confirmed));
        }

        private static BattleDiagnosticEvent Event(
            int frame,
            long sequence,
            BattleDiagnosticEventKind kind,
            BattleDiagnosticEventOutcome outcome,
            long rootContextId = 0,
            long contextId = 0,
            BattleDiagnosticEventPayload payload = default)
        {
            return new BattleDiagnosticEvent(
                Scope,
                frame,
                sequence,
                sequence,
                kind,
                BattleDiagnosticEventChannel.Effect,
                outcome,
                sourceActorId: 101,
                targetActorId: 202,
                configId: 7001,
                rootContextId: rootContextId,
                contextId: contextId,
                payloadVersion: payload.HasValue ? payload.SchemaVersion : 1,
                summary: "test diagnostic",
                payload: payload);
        }
    }
}
