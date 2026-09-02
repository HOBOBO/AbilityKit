#if UNITY_EDITOR

using System;
using NUnit.Framework;

namespace AbilityKit.Pipeline.Editor.Tests
{
    public sealed class PipelineDebuggerTraceModelTests
    {
        [Test]
        public void Rebuild_AppliesFilterAndSearchInOneProjection()
        {
            var model = new PipelineDebuggerTraceModel();
            DateTime started = Utc(0);

            model.Rebuild(
                new[]
                {
                    Trace(1, EPipelineTraceEventType.RunStart, "", "begin", Utc(1)),
                    Trace(2, EPipelineTraceEventType.PhaseStart, "Cast", "prepare", Utc(2)),
                    Trace(3, EPipelineTraceEventType.PhaseError, "Damage", "target missing", Utc(3))
                },
                PipelineDebuggerTraceFilter.Phases,
                "damage",
                started,
                relativeTime: true);

            Assert.That(model.VisibleRows.Count, Is.EqualTo(1));
            Assert.That(model.VisibleRows[0].TraceEvent.Seq, Is.EqualTo(3));
            Assert.That(model.VisibleRows[0].TimeLabel, Is.EqualTo("+3.000"));
        }

        [Test]
        public void Rebuild_ClearsPreviousProjection()
        {
            var model = new PipelineDebuggerTraceModel();
            model.Rebuild(
                new[] { Trace(1, EPipelineTraceEventType.RunStart, "", "first", Utc(1)) },
                PipelineDebuggerTraceFilter.All,
                null,
                Utc(0),
                relativeTime: true);

            model.Rebuild(
                new[] { Trace(2, EPipelineTraceEventType.RunEnd, "", "second", Utc(2)) },
                PipelineDebuggerTraceFilter.All,
                null,
                Utc(0),
                relativeTime: true);

            Assert.That(model.VisibleRows.Count, Is.EqualTo(1));
            Assert.That(model.VisibleRows[0].TraceEvent.Seq, Is.EqualTo(2));
        }

        [Test]
        public void FormatTime_ClampsRelativeTimeAndFormatsUtc()
        {
            PipelineTraceEvent item = Trace(
                1,
                EPipelineTraceEventType.Tick,
                "",
                "tick",
                Utc(1));

            Assert.That(
                PipelineDebuggerTraceModel.FormatTime(item, Utc(2), relativeTime: true),
                Is.EqualTo("+0.000"));
            Assert.That(
                PipelineDebuggerTraceModel.FormatTime(item, Utc(0), relativeTime: false),
                Is.EqualTo("00:00:01.000"));
        }

        [Test]
        public void ResolveSelection_ClearsMissingSequence()
        {
            var trace = new[]
            {
                Trace(1, EPipelineTraceEventType.RunStart, "", "begin", Utc(1))
            };

            Assert.That(
                PipelineDebuggerTraceModel.ResolveSelection(1, trace),
                Is.EqualTo(1));
            Assert.That(
                PipelineDebuggerTraceModel.ResolveSelection(2, trace),
                Is.Null);
            Assert.That(
                PipelineDebuggerTraceModel.ResolveSelection(null, trace),
                Is.Null);
        }

        [Test]
        public void TryGetSelected_ReturnsMatchingEvent()
        {
            var trace = new[]
            {
                Trace(4, EPipelineTraceEventType.Pause, "Cast", "paused", Utc(4))
            };

            bool found = PipelineDebuggerTraceModel.TryGetSelected(
                4,
                trace,
                out PipelineTraceEvent selected);

            Assert.That(found, Is.True);
            Assert.That(selected.Message, Is.EqualTo("paused"));
            Assert.That(
                PipelineDebuggerTraceModel.TryGetSelected(null, trace, out _),
                Is.False);
        }

        [Test]
        public void Methods_RejectNullTrace()
        {
            var model = new PipelineDebuggerTraceModel();

            Assert.Throws<ArgumentNullException>(() => model.Rebuild(
                null!,
                PipelineDebuggerTraceFilter.All,
                null,
                Utc(0),
                relativeTime: true));
            Assert.Throws<ArgumentNullException>(() =>
                PipelineDebuggerTraceModel.ResolveSelection(1, null!));
            Assert.Throws<ArgumentNullException>(() =>
                PipelineDebuggerTraceModel.TryGetSelected(1, null!, out _));
        }

        private static PipelineTraceEvent Trace(
            int sequence,
            EPipelineTraceEventType type,
            string phase,
            string message,
            DateTime utc)
        {
            return new PipelineTraceEvent(
                sequence,
                type,
                new AbilityPipelinePhaseId(phase),
                type == EPipelineTraceEventType.PhaseError
                    ? EAbilityPipelineState.Failed
                    : EAbilityPipelineState.Executing,
                message,
                utc);
        }

        private static DateTime Utc(int second)
        {
            return new DateTime(
                2026,
                1,
                1,
                0,
                0,
                second,
                DateTimeKind.Utc);
        }
    }
}

#endif
