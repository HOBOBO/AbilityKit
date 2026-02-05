using System;

namespace AbilityKit.Game.Flow.Battle.Modules
{
    public readonly struct StartSessionRequested
    {
    }

    public sealed class BattleSessionHooks
    {
        public readonly Hook<float> PreTick = new Hook<float>();
        public readonly Hook<float> PostTick = new Hook<float>();

        public readonly InterceptHook<BattleStartPlan> PlanBuilt = new InterceptHook<BattleStartPlan>();
        public readonly Hook<BattleStartPlan> SessionStarted = new Hook<BattleStartPlan>();
        public readonly Hook<Exception> SessionFailed = new Hook<Exception>();
        public readonly Hook FirstFrameReceived = new Hook();

        public readonly Hook SessionStarting = new Hook();
        public readonly Hook SessionStopping = new Hook();
    }

    public readonly struct PlanBuiltEvent
    {
        public readonly BattleStartPlan Plan;

        public PlanBuiltEvent(BattleStartPlan plan)
        {
            Plan = plan;
        }
    }

    public readonly struct SessionStartedEvent
    {
        public readonly BattleStartPlan Plan;

        public SessionStartedEvent(BattleStartPlan plan)
        {
            Plan = plan;
        }
    }
}
