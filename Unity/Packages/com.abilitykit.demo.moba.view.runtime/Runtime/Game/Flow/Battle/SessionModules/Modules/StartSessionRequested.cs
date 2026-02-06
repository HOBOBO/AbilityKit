using System;
using AbilityKit.Ability.World.Abstractions;

namespace AbilityKit.Game.Flow.Battle.Modules
{
    public readonly struct StartSessionRequested
    {
    }

    public readonly struct SessionStartingEvent
    {
        public readonly BattleStartPlan Plan;

        public SessionStartingEvent(BattleStartPlan plan)
        {
            Plan = plan;
        }
    }

    public readonly struct SessionStoppingEvent
    {
        public readonly BattleStartPlan Plan;

        public SessionStoppingEvent(BattleStartPlan plan)
        {
            Plan = plan;
        }
    }

    public readonly struct ViewBinderReadyEvent
    {
        public readonly bool IsConfirmed;
        public readonly WorldId WorldId;

        public ViewBinderReadyEvent(bool isConfirmed, WorldId worldId)
        {
            IsConfirmed = isConfirmed;
            WorldId = worldId;
        }
    }

    public readonly struct ViewsReboundEvent
    {
        public readonly bool IsConfirmed;
        public readonly WorldId WorldId;
        public readonly int Frame;

        public ViewsReboundEvent(bool isConfirmed, WorldId worldId, int frame)
        {
            IsConfirmed = isConfirmed;
            WorldId = worldId;
            Frame = frame;
        }
    }

    public readonly struct ViewFrameAlignedEvent
    {
        public readonly bool IsConfirmed;
        public readonly WorldId WorldId;
        public readonly int Frame;

        public ViewFrameAlignedEvent(bool isConfirmed, WorldId worldId, int frame)
        {
            IsConfirmed = isConfirmed;
            WorldId = worldId;
            Frame = frame;
        }
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
