using System.Collections.Generic;
using AbilityKit.Ability.Share.Impl.UnitTest;
using AbilityKit.Ability.Triggering.Definitions;
using NUnit.Framework;

namespace AbilityKit.Game.Test.UnitTest
{
    public sealed class TriggerRunnerSmokeTests
    {
        [Test]
        public void TriggerRunner_RunOnce_AddsRunningAction_AndRunnerTicksItToCompletion()
        {
            using var h = new TriggerWorldTestHarness(new AbilityKit.Ability.World.Abstractions.WorldId("test_world"), "test");

            var actions = new List<ActionDef>
            {
                new ActionDef(
                    type: "test_wait",
                    args: new Dictionary<string, object>
                    {
                        { "duration", 0.05f }
                    })
            };

            var def = new TriggerDef(
                eventId: "evt_test",
                conditions: new List<ConditionDef>(0),
                actions: actions);

            var ok = h.TriggerRunner.RunOnce(def);
            Assert.IsTrue(ok);
            Assert.Greater(h.ActionRunner.RunningCount, 0);

            h.Tick(0.1f);
            Assert.AreEqual(0, h.ActionRunner.RunningCount);
        }
    }
}
