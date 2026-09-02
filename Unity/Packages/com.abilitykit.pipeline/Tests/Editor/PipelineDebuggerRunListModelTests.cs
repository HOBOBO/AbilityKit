#if UNITY_EDITOR

using System;
using NUnit.Framework;

namespace AbilityKit.Pipeline.Editor.Tests
{
    public sealed class PipelineDebuggerRunListModelTests
    {
        [Test]
        public void Rebuild_AllFilterGroupsRunsAndSortsNewestWithinGroup()
        {
            var model = new PipelineDebuggerRunListModel();
            model.Rebuild(
                new[]
                {
                    Run(1, Utc(5), active: false, pinned: false),
                    Run(2, Utc(2), active: true, pinned: false),
                    Run(3, Utc(4), active: false, pinned: true),
                    Run(4, Utc(3), active: true, pinned: false),
                    Run(5, Utc(6), active: false, pinned: true)
                },
                PipelineDebuggerRunFilter.All,
                null);

            AssertRunIds(model, 4, 2, 5, 3, 1);
        }

        [Test]
        public void Rebuild_FilteredListSortsNewestFirst()
        {
            var model = new PipelineDebuggerRunListModel();
            model.Rebuild(
                new[]
                {
                    Run(1, Utc(1), active: false, pinned: true),
                    Run(2, Utc(3), active: false, pinned: true),
                    Run(3, Utc(2), active: true, pinned: true)
                },
                PipelineDebuggerRunFilter.Pinned,
                null);

            AssertRunIds(model, 2, 3, 1);
        }

        [Test]
        public void Rebuild_AppliesSearchAndClearsPreviousProjection()
        {
            var model = new PipelineDebuggerRunListModel();
            model.Rebuild(
                new[]
                {
                    Run(1, Utc(1), ownerName: "Player"),
                    Run(2, Utc(2), ownerName: "Enemy")
                },
                PipelineDebuggerRunFilter.All,
                "player");

            AssertRunIds(model, 1);

            model.Rebuild(
                new[]
                {
                    Run(3, Utc(3), ownerName: "Boss")
                },
                PipelineDebuggerRunFilter.All,
                null);

            AssertRunIds(model, 3);
        }

        [Test]
        public void ResolveSelection_UsesCurrentProjection()
        {
            var model = new PipelineDebuggerRunListModel();
            model.Rebuild(
                new[]
                {
                    Run(1, Utc(1)),
                    Run(2, Utc(2))
                },
                PipelineDebuggerRunFilter.All,
                null);

            Assert.That(
                model.ResolveSelection(
                    selectedRunId: null,
                    followLatest: true,
                    registrySelectedRunId: null),
                Is.EqualTo(2));
        }

        [Test]
        public void Rebuild_RejectsNullRuns()
        {
            var model = new PipelineDebuggerRunListModel();

            Assert.Throws<ArgumentNullException>(() =>
                model.Rebuild(
                    null!,
                    PipelineDebuggerRunFilter.All,
                    null));
        }

        private static void AssertRunIds(
            PipelineDebuggerRunListModel model,
            params int[] expected)
        {
            Assert.That(model.VisibleRuns.Count, Is.EqualTo(expected.Length));
            for (int i = 0; i < expected.Length; i++)
            {
                Assert.That(
                    model.VisibleRuns[i].RunId,
                    Is.EqualTo(expected[i]));
            }
        }

        private static PipelineDebuggerRunView Run(
            int id,
            DateTime registeredAtUtc,
            bool active = false,
            bool pinned = false,
            string ownerName = "Owner")
        {
            return new PipelineDebuggerRunView(
                id,
                registeredAtUtc,
                active,
                pinned,
                EAbilityPipelineState.Executing,
                ownerName,
                "Pipeline.Type",
                "Config.Type",
                "Phase");
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
