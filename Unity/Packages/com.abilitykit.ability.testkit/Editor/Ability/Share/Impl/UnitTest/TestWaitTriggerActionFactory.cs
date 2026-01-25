using System;
using AbilityKit.Ability.Triggering;
using AbilityKit.Ability.Triggering.Definitions;
using AbilityKit.Ability.Triggering.Runtime;
using UnityEngine.Scripting;

namespace AbilityKit.Ability.Share.Impl.UnitTest
{
    [TriggerActionType("test_wait", "Test Wait", "Test", 0)]
    [Preserve]
    public sealed class TestWaitTriggerActionFactory : IActionFactory
    {
        public ITriggerAction Create(ActionDef def)
        {
            return TestWaitTriggerAction.FromDef(def);
        }
    }

    public sealed class TestWaitTriggerAction : ITriggerActionV2
    {
        private readonly float _duration;

        public TestWaitTriggerAction(float duration)
        {
            _duration = duration;
        }

        public static TestWaitTriggerAction FromDef(ActionDef def)
        {
            if (def == null) throw new ArgumentNullException(nameof(def));
            var args = def.Args;
            if (args == null) return new TestWaitTriggerAction(0f);

            if (args.TryGetValue("duration", out var dObj) && dObj != null)
            {
                var d = dObj is float f ? f : dObj is int i ? i : Convert.ToSingle(dObj);
                return new TestWaitTriggerAction(d);
            }

            return new TestWaitTriggerAction(0f);
        }

        public void Execute(TriggerContext context)
        {
            Start(context);
        }

        public IRunningAction Start(TriggerContext context)
        {
            return new WaitRunningAction(_duration);
        }

        private sealed class WaitRunningAction : IRunningAction
        {
            private float _remaining;
            private bool _cancelled;

            public WaitRunningAction(float duration)
            {
                _remaining = duration;
            }

            public bool IsDone => _cancelled || _remaining <= 0f;

            public void Tick(float deltaTime)
            {
                if (IsDone) return;
                _remaining -= deltaTime;
            }

            public void Cancel()
            {
                _cancelled = true;
            }

            public void Dispose()
            {
            }
        }
    }
}
