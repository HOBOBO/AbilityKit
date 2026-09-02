#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace AbilityKit.Pipeline.Editor.Tests
{
    public sealed class PipelineDebuggerViewPolicyTests
    {
        [Test]
        public void MatchesRun_AppliesFilterAndCaseInsensitiveSearch()
        {
            var failed = Run(
                42,
                active: false,
                pinned: true,
                state: EAbilityPipelineState.Failed,
                ownerName: "Player Skill",
                phaseId: "Impact");

            Assert.That(
                PipelineDebuggerViewPolicy.MatchesRun(
                    failed,
                    PipelineDebuggerRunFilter.Failed,
                    "player"),
                Is.True);
            Assert.That(
                PipelineDebuggerViewPolicy.MatchesRun(
                    failed,
                    PipelineDebuggerRunFilter.Active,
                    "player"),
                Is.False);
            Assert.That(
                PipelineDebuggerViewPolicy.MatchesRun(
                    failed,
                    PipelineDebuggerRunFilter.Pinned,
                    "42"),
                Is.True);
            Assert.That(
                PipelineDebuggerViewPolicy.MatchesRun(
                    failed,
                    PipelineDebuggerRunFilter.All,
                    "impact"),
                Is.True);
        }

        [Test]
        public void GetRunGroup_PrioritizesActiveThenPinnedThenHistory()
        {
            Assert.That(
                PipelineDebuggerViewPolicy.GetRunGroup(
                    Run(1, active: true, pinned: true)),
                Is.EqualTo(0));
            Assert.That(
                PipelineDebuggerViewPolicy.GetRunGroup(
                    Run(2, active: false, pinned: true)),
                Is.EqualTo(1));
            Assert.That(
                PipelineDebuggerViewPolicy.GetRunGroup(
                    Run(3, active: false, pinned: false)),
                Is.EqualTo(2));
        }

        [Test]
        public void ResolveSelection_EmptyListClearsSelection()
        {
            int? selected = PipelineDebuggerViewPolicy.ResolveSelection(
                10,
                followLatest: true,
                registrySelectedRunId: 10,
                Array.Empty<PipelineDebuggerRunView>());

            Assert.That(selected, Is.Null);
        }

        [Test]
        public void ResolveSelection_KeepsVisibleSelectionWhenFollowLatestIsOff()
        {
            var runs = Runs(
                Run(1, registeredAtUtc: Utc(1)),
                Run(2, registeredAtUtc: Utc(2)));

            int? selected = PipelineDebuggerViewPolicy.ResolveSelection(
                1,
                followLatest: false,
                registrySelectedRunId: null,
                runs);

            Assert.That(selected, Is.EqualTo(1));
        }

        [Test]
        public void ResolveSelection_FallsBackToFirstVisibleRun()
        {
            var runs = Runs(
                Run(1, registeredAtUtc: Utc(1)),
                Run(2, registeredAtUtc: Utc(2)));

            int? selected = PipelineDebuggerViewPolicy.ResolveSelection(
                99,
                followLatest: false,
                registrySelectedRunId: null,
                runs);

            Assert.That(selected, Is.EqualTo(1));
        }

        [Test]
        public void ResolveSelection_FollowLatestSelectsNewestWhenSelectionIsMissing()
        {
            var runs = Runs(
                Run(1, registeredAtUtc: Utc(3)),
                Run(2, registeredAtUtc: Utc(5)),
                Run(3, registeredAtUtc: Utc(4)));

            int? selected = PipelineDebuggerViewPolicy.ResolveSelection(
                null,
                followLatest: true,
                registrySelectedRunId: null,
                runs);

            Assert.That(selected, Is.EqualTo(2));
        }

        [Test]
        public void ResolveSelection_FollowLatestKeepsVisibleManualSelection()
        {
            var runs = Runs(
                Run(1, registeredAtUtc: Utc(1)),
                Run(2, registeredAtUtc: Utc(2)));

            int? selected = PipelineDebuggerViewPolicy.ResolveSelection(
                1,
                followLatest: true,
                registrySelectedRunId: 1,
                runs);

            Assert.That(selected, Is.EqualTo(1));
        }

        [Test]
        public void MatchesTrace_AppliesCategoryAndSearch()
        {
            var error = new PipelineTraceEvent(
                1,
                EPipelineTraceEventType.PhaseError,
                new AbilityPipelinePhaseId("Damage"),
                EAbilityPipelineState.Failed,
                "Target missing",
                Utc(1));

            Assert.That(
                PipelineDebuggerViewPolicy.MatchesTrace(
                    error,
                    PipelineDebuggerTraceFilter.Errors,
                    "target"),
                Is.True);
            Assert.That(
                PipelineDebuggerViewPolicy.MatchesTrace(
                    error,
                    PipelineDebuggerTraceFilter.Control,
                    null),
                Is.False);
            Assert.That(
                PipelineDebuggerViewPolicy.MatchesTrace(
                    error,
                    PipelineDebuggerTraceFilter.Phases,
                    "damage"),
                Is.True);
        }

        [Test]
        public void MatchesContext_SearchesNameInitialAndCurrentValues()
        {
            Assert.That(
                PipelineDebuggerViewPolicy.MatchesContext(
                    "Health",
                    "100",
                    "75",
                    "health"),
                Is.True);
            Assert.That(
                PipelineDebuggerViewPolicy.MatchesContext(
                    "Health",
                    "100",
                    "75",
                    "75"),
                Is.True);
            Assert.That(
                PipelineDebuggerViewPolicy.MatchesContext(
                    "Health",
                    "100",
                    "75",
                    "mana"),
                Is.False);
        }

        private static IReadOnlyList<PipelineDebuggerRunView> Runs(
            params PipelineDebuggerRunView[] runs)
        {
            return runs;
        }

        private static PipelineDebuggerRunView Run(
            int id,
            DateTime? registeredAtUtc = null,
            bool active = false,
            bool pinned = false,
            EAbilityPipelineState state = EAbilityPipelineState.Executing,
            string ownerName = "Owner",
            string phaseId = "Phase")
        {
            return new PipelineDebuggerRunView(
                id,
                registeredAtUtc ?? Utc(id),
                active,
                pinned,
                state,
                ownerName,
                "Pipeline.Type",
                "Config.Type",
                phaseId);
        }

        private static DateTime Utc(int second)
        {
            return new DateTime(2026, 1, 1, 0, 0, second, DateTimeKind.Utc);
        }
    }
}

#endif
