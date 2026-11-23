using System.Collections.Generic;
using AbilityKit.Triggering.Runtime.Diagnostics;
using AbilityKit.Triggering.Runtime.Plan.Json;
using AbilityKit.Triggering.Validation;
using NUnit.Framework;

namespace AbilityKit.Triggering.Tests
{
    public sealed class TriggeringDiagnosticCollectorTests
    {
        [Test]
        public void RecordValidation_AggregatesIssueCounts()
        {
            var result = ValidationResult.Success;
            result.AddError(ValidationErrorCodes.ACTION_NOT_FOUND, "missing", "$.actions[0]");
            result.AddWarning(ValidationErrorCodes.UNUSED_ACTION_RETRY, "unused", "$.actions[0].retry");
            var collector = new TriggeringDiagnosticCollector();

            collector.RecordValidation(in result, "unit");

            var snapshot = collector.Snapshot;
            Assert.That(snapshot.ValidationErrors, Is.EqualTo(1));
            Assert.That(snapshot.ValidationWarnings, Is.EqualTo(1));
            Assert.That(snapshot.TotalRecords, Is.EqualTo(1));
        }

        [Test]
        public void RecordJsonDiagnostics_AggregatesBySeverity()
        {
            var collector = new TriggeringDiagnosticCollector();
            var diagnostics = new List<TriggerPlanJsonDiagnostic>
            {
                new TriggerPlanJsonDiagnostic(TriggerPlanJsonDiagnosticSeverity.Error, "bad", "source", "$.triggers[0]"),
                new TriggerPlanJsonDiagnostic(TriggerPlanJsonDiagnosticSeverity.Warning, "warn", "source", "$.strings")
            };

            collector.RecordJsonDiagnostics(diagnostics);

            var snapshot = collector.Snapshot;
            Assert.That(snapshot.JsonErrors, Is.EqualTo(1));
            Assert.That(snapshot.JsonWarnings, Is.EqualTo(1));
            Assert.That(snapshot.TotalRecords, Is.EqualTo(2));
        }

        [Test]
        public void RuntimeEvents_AggregateExecutionScheduleAndLegacyHits()
        {
            var collector = new TriggeringDiagnosticCollector();

            collector.RecordExecutionFailure(11, 22, "failed", "$.executionRoot");
            collector.RecordScheduleEvent("rule:1", "scheduled", triggerId: 11, actionId: 22);
            collector.RecordLegacyHit("Runtime/Scheduler", "Runtime/RuleScheduler", "demo");

            var snapshot = collector.Snapshot;
            Assert.That(snapshot.ExecutionFailures, Is.EqualTo(1));
            Assert.That(snapshot.ScheduleEvents, Is.EqualTo(1));
            Assert.That(snapshot.LegacyHits, Is.EqualTo(1));
            Assert.That(snapshot.TotalRecords, Is.EqualTo(3));
        }

        [Test]
        public void Clear_ResetsLiveRecordsWithoutChangingPublishedSnapshot()
        {
            var collector = new TriggeringDiagnosticCollector();
            var records = collector.Records;
            collector.RecordExecutionFailure(11, 22, "failed");
            var previous = collector.Snapshot;

            Assert.That(records, Has.Count.EqualTo(1));
            collector.Clear();

            Assert.That(records, Is.Empty);
            Assert.That(collector.Snapshot.ExecutionFailures, Is.Zero);
            Assert.That(collector.Snapshot.TotalRecords, Is.Zero);
            Assert.That(previous.ExecutionFailures, Is.EqualTo(1));
            Assert.That(previous.TotalRecords, Is.EqualTo(1));
        }
    }
}
