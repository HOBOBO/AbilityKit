#if UNITY_EDITOR

#nullable enable

using System;
using NUnit.Framework;

namespace AbilityKit.Pipeline.Editor.Tests
{
    public sealed class PipelineDebuggerOverviewModelTests
    {
        [Test]
        public void Rebuild_ProjectsRunAndTechnicalLabels()
        {
            var model = new PipelineDebuggerOverviewModel();
            var started = new DateTime(
                2026, 1, 2, 3, 4, 5, 678, DateTimeKind.Utc);
            var ended = started.AddSeconds(2);

            model.Rebuild(
                Source(
                    isPaused: false,
                    state: EAbilityPipelineState.Completed,
                    currentPhase: new AbilityPipelinePhaseId("Damage"),
                    activePhases: new[]
                    {
                        new AbilityPipelinePhaseId("Damage"),
                        new AbilityPipelinePhaseId("Effects")
                    },
                    startedUtc: started,
                    endedUtc: ended),
                Array.Empty<PipelineTraceEvent>());

            Assert.That(model.RunId, Is.EqualTo(7));
            Assert.That(model.StateLabel, Is.EqualTo("Completed"));
            Assert.That(model.CurrentPhaseLabel, Is.EqualTo("Damage"));
            Assert.That(model.ElapsedTimeLabel, Is.EqualTo("1.250 s"));
            Assert.That(model.WallDurationLabel, Is.EqualTo("2.500 s"));
            Assert.That(
                model.ActivePhaseLabels,
                Is.EqualTo(new[] { "Damage", "Effects" }));
            Assert.That(
                model.StartedUtcLabel,
                Is.EqualTo("2026-01-02 03:04:05.678"));
            Assert.That(
                model.EndedUtcLabel,
                Is.EqualTo("2026-01-02 03:04:07.678"));
            Assert.That(model.PipelineType, Is.EqualTo("Pipeline.Type"));
            Assert.That(model.ConfigType, Is.EqualTo("Config.Type"));
            Assert.That(model.ContextType, Is.EqualTo("Context.Type"));
            Assert.That(model.LastError, Is.Null);
        }

        [Test]
        public void Rebuild_UsesPausedAndRunningFallbackLabels()
        {
            var model = new PipelineDebuggerOverviewModel();

            model.Rebuild(
                Source(
                    isPaused: true,
                    state: EAbilityPipelineState.Executing,
                    currentPhase: default,
                    activePhases: Array.Empty<AbilityPipelinePhaseId>(),
                    startedUtc: Utc(1),
                    endedUtc: null),
                Array.Empty<PipelineTraceEvent>());

            Assert.That(model.StateLabel, Is.EqualTo("Executing (Paused)"));
            Assert.That(model.CurrentPhaseLabel, Is.EqualTo("No phase"));
            Assert.That(model.ActivePhaseLabels, Is.Empty);
            Assert.That(model.EndedUtcLabel, Is.EqualTo("Running"));
        }

        [Test]
        public void Rebuild_ClearsPreviousActivePhaseProjection()
        {
            var model = new PipelineDebuggerOverviewModel();
            model.Rebuild(
                Source(activePhases: new[]
                {
                    new AbilityPipelinePhaseId("Old")
                }),
                Array.Empty<PipelineTraceEvent>());

            model.Rebuild(
                Source(activePhases: Array.Empty<AbilityPipelinePhaseId>()),
                Array.Empty<PipelineTraceEvent>());

            Assert.That(model.ActivePhaseLabels, Is.Empty);
        }

        [Test]
        public void FindLastError_ReturnsNewestNonEmptyFailedMessage()
        {
            var trace = new[]
            {
                Trace(1, EAbilityPipelineState.Failed, "first"),
                Trace(2, EAbilityPipelineState.Failed, string.Empty),
                Trace(3, EAbilityPipelineState.Executing, "ignored"),
                Trace(4, EAbilityPipelineState.Failed, "latest")
            };

            Assert.That(
                PipelineDebuggerOverviewModel.FindLastError(trace),
                Is.EqualTo("latest"));
        }

        [Test]
        public void FindLastError_ReturnsNullWhenNoFailedMessageExists()
        {
            Assert.That(
                PipelineDebuggerOverviewModel.FindLastError(new[]
                {
                    Trace(1, EAbilityPipelineState.Executing, "message"),
                    Trace(2, EAbilityPipelineState.Failed, string.Empty)
                }),
                Is.Null);
        }

        [Test]
        public void Rebuild_RejectsNullTrace()
        {
            var model = new PipelineDebuggerOverviewModel();

            Assert.Throws<ArgumentNullException>(() =>
                model.Rebuild(Source(), null!));
        }

        [Test]
        public void Source_RejectsNullCollectionsAndTypeNames()
        {
            Assert.Throws<ArgumentNullException>(() =>
                CreateSource(null!, "Pipeline", "Config", "Context"));
            Assert.Throws<ArgumentNullException>(() =>
                CreateSource(
                    Array.Empty<AbilityPipelinePhaseId>(),
                    null!,
                    "Config",
                    "Context"));
            Assert.Throws<ArgumentNullException>(() =>
                CreateSource(
                    Array.Empty<AbilityPipelinePhaseId>(),
                    "Pipeline",
                    null!,
                    "Context"));
            Assert.Throws<ArgumentNullException>(() =>
                CreateSource(
                    Array.Empty<AbilityPipelinePhaseId>(),
                    "Pipeline",
                    "Config",
                    null!));
        }

        private static PipelineDebuggerOverviewSource CreateSource(
            AbilityPipelinePhaseId[] activePhases,
            string pipelineType,
            string configType,
            string contextType)
        {
            return new PipelineDebuggerOverviewSource(
                7,
                EAbilityPipelineState.Executing,
                false,
                default,
                1.25f,
                2.5d,
                activePhases,
                Utc(1),
                null,
                pipelineType,
                configType,
                contextType);
        }

        private static PipelineDebuggerOverviewSource Source(
            bool isPaused = false,
            EAbilityPipelineState state = EAbilityPipelineState.Executing,
            AbilityPipelinePhaseId currentPhase = default,
            AbilityPipelinePhaseId[]? activePhases = null,
            DateTime? startedUtc = null,
            DateTime? endedUtc = null,
            string pipelineType = "Pipeline.Type",
            string configType = "Config.Type",
            string contextType = "Context.Type")
        {
            return new PipelineDebuggerOverviewSource(
                7,
                state,
                isPaused,
                currentPhase,
                1.25f,
                2.5d,
                activePhases ?? Array.Empty<AbilityPipelinePhaseId>(),
                startedUtc ?? Utc(1),
                endedUtc,
                pipelineType,
                configType,
                contextType);
        }

        private static PipelineTraceEvent Trace(
            int sequence,
            EAbilityPipelineState state,
            string message)
        {
            return new PipelineTraceEvent(
                sequence,
                EPipelineTraceEventType.PhaseError,
                new AbilityPipelinePhaseId("Phase"),
                state,
                message,
                Utc(sequence));
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
