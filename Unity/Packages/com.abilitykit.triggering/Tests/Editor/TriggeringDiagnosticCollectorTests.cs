using System;
using AbilityKit.Triggering.Runtime.Diagnostics;
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
        public void RecordLoadDiagnostic_KeepsOrderAndAggregatesBySeverity()
        {
            var collector = new TriggeringDiagnosticCollector();
            collector.RecordLoadDiagnostic(TriggeringLoadDiagnosticSeverity.Warning, "fallback");
            collector.RecordLoadDiagnostic(TriggeringLoadDiagnosticSeverity.Error, "bad", "rules.yaml", "triggers[0]", "BAD_RULE");
            collector.RecordLoadDiagnostic(TriggeringLoadDiagnosticSeverity.Info, "loaded");

            var snapshot = collector.Snapshot;
            Assert.That(snapshot.LoadErrors, Is.EqualTo(1));
            Assert.That(snapshot.LoadWarnings, Is.EqualTo(1));
            Assert.That(snapshot.LoadInfos, Is.EqualTo(1));
            Assert.That(snapshot.TotalRecords, Is.EqualTo(3));
            var records = collector.Records;
            Assert.That(records[0].Code, Is.EqualTo("Warning"));
            Assert.That(records[0].WarningCount, Is.EqualTo(1));
            Assert.That(records[1].Kind, Is.EqualTo(TriggeringDiagnosticEventKind.PlanLoad));
            Assert.That(records[1].Source, Is.EqualTo("rules.yaml"));
            Assert.That(records[1].Path, Is.EqualTo("triggers[0]"));
            Assert.That(records[1].Code, Is.EqualTo("BAD_RULE"));
            Assert.That(records[1].Message, Is.EqualTo("bad"));
            Assert.That(records[1].ErrorCount, Is.EqualTo(1));
            Assert.That(records[2].Code, Is.EqualTo("Info"));
            Assert.That(records[2].InfoCount, Is.EqualTo(1));
        }

        [Test]
        public void RecordLoadDiagnostic_InvalidSeverityDoesNotChangeState()
        {
            var collector = new TriggeringDiagnosticCollector();
            collector.RecordLoadDiagnostic(TriggeringLoadDiagnosticSeverity.Info, "loaded");

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                collector.RecordLoadDiagnostic((TriggeringLoadDiagnosticSeverity)99, "bad severity"));

            var snapshot = collector.Snapshot;
            Assert.That(snapshot.LoadErrors, Is.Zero);
            Assert.That(snapshot.LoadWarnings, Is.Zero);
            Assert.That(snapshot.LoadInfos, Is.EqualTo(1));
            Assert.That(snapshot.TotalRecords, Is.EqualTo(1));
        }

        [Test]
        public void RuntimeEvents_AggregateExecutionScheduleAndLegacyHits()
        {
            var collector = new TriggeringDiagnosticCollector();

            collector.RecordExecutionFailure(11, 22, "failed", "$.executionRoot");
            collector.RecordScheduleEvent("rule:1", "scheduled", triggerId: 11, actionId: 22);
            collector.RecordLegacyHit("Runtime/Scheduler", "Runtime/Scheduling/ExecutionTree", "demo");

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
            collector.RecordLoadDiagnostic(TriggeringLoadDiagnosticSeverity.Error, "bad");
            collector.RecordLoadDiagnostic(TriggeringLoadDiagnosticSeverity.Warning, "fallback");
            collector.RecordLoadDiagnostic(TriggeringLoadDiagnosticSeverity.Info, "loaded");
            var previous = collector.Snapshot;

            Assert.That(records, Has.Count.EqualTo(4));
            collector.Clear();

            Assert.That(records, Is.Empty);
            Assert.That(collector.Snapshot.ExecutionFailures, Is.Zero);
            Assert.That(collector.Snapshot.LoadErrors, Is.Zero);
            Assert.That(collector.Snapshot.LoadWarnings, Is.Zero);
            Assert.That(collector.Snapshot.LoadInfos, Is.Zero);
            Assert.That(collector.Snapshot.TotalRecords, Is.Zero);
            Assert.That(previous.ExecutionFailures, Is.EqualTo(1));
            Assert.That(previous.LoadErrors, Is.EqualTo(1));
            Assert.That(previous.LoadWarnings, Is.EqualTo(1));
            Assert.That(previous.LoadInfos, Is.EqualTo(1));
            Assert.That(previous.TotalRecords, Is.EqualTo(4));
        }
    }
}
