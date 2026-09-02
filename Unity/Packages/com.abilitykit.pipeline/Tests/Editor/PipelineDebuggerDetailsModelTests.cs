#if UNITY_EDITOR

using System;
using NUnit.Framework;

namespace AbilityKit.Pipeline.Editor.Tests
{
    public sealed class PipelineDebuggerDetailsModelTests
    {
        [Test]
        public void Constructor_SelectsOverviewByDefault()
        {
            var model = new PipelineDebuggerDetailsModel();

            Assert.That(
                model.SelectedTab,
                Is.EqualTo(PipelineDebuggerDetailTab.Overview));
        }

        [Test]
        public void Select_UpdatesSelectedTab()
        {
            var model = new PipelineDebuggerDetailsModel();

            model.Select(PipelineDebuggerDetailTab.Trace);

            Assert.That(
                model.SelectedTab,
                Is.EqualTo(PipelineDebuggerDetailTab.Trace));
        }

        [Test]
        public void Select_RejectsUndefinedTab()
        {
            var model = new PipelineDebuggerDetailsModel();

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                model.Select((PipelineDebuggerDetailTab)999));
        }

        [Test]
        public void BuildTabLabels_CompactLabelsDoNotIncludeCounts()
        {
            var model = new PipelineDebuggerDetailsModel();

            var labels = model.BuildTabLabels(
                compact: true,
                phaseCount: 2,
                traceCount: 3,
                contextCount: 4);

            Assert.That(
                labels,
                Is.EqualTo(new[]
                {
                    "Overview",
                    "Phases",
                    "Trace",
                    "Context"
                }));
        }

        [Test]
        public void BuildTabLabels_FullLabelsIncludeCounts()
        {
            var model = new PipelineDebuggerDetailsModel();

            var labels = model.BuildTabLabels(
                compact: false,
                phaseCount: 2,
                traceCount: 3,
                contextCount: 4);

            Assert.That(
                labels,
                Is.EqualTo(new[]
                {
                    "Overview",
                    "Phases 2",
                    "Trace 3",
                    "Context 4"
                }));
        }

        [TestCase(-1, 0, 0, "phaseCount")]
        [TestCase(0, -1, 0, "traceCount")]
        [TestCase(0, 0, -1, "contextCount")]
        public void BuildTabLabels_RejectsNegativeCounts(
            int phaseCount,
            int traceCount,
            int contextCount,
            string parameterName)
        {
            var model = new PipelineDebuggerDetailsModel();

            var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
                model.BuildTabLabels(
                    compact: false,
                    phaseCount,
                    traceCount,
                    contextCount));

            Assert.That(exception!.ParamName, Is.EqualTo(parameterName));
        }
    }
}

#endif
