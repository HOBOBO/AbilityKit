using System;
using System.Collections.Generic;
using AbilityKit.Ability.Editor.Utilities;
using NUnit.Framework;

namespace AbilityKit.Ability.Editor.Tests
{
    public sealed class TriggerPlanExportPipelineTests
    {
        [Test]
        public void EnsureCompleteExport_AllowsCompleteExport()
        {
            Assert.DoesNotThrow(() => TriggerPlanExportPipeline.EnsureCompleteExport(
                0,
                0,
                0,
                0,
                0,
                new List<int>()));
        }

        [Test]
        public void EnsureCompleteExport_RejectsPartialDatabase()
        {
            var exception = Assert.Throws<InvalidOperationException>(() =>
                TriggerPlanExportPipeline.EnsureCompleteExport(
                    0,
                    1,
                    0,
                    0,
                    0,
                    new List<int> { 1001 }));

            StringAssert.Contains("export aborted", exception.Message);
            StringAssert.Contains("1001", exception.Message);
        }
    }
}
