using System.Collections.Generic;
using AbilityKit.Ability.Flow.Stages;

namespace AbilityKit.Starter
{
    public static class StarterStages
    {
        public static readonly FlowStageKey ProjectInit = new FlowStageKey("starter_project_init");
        public static readonly FlowStageKey SdkInit = new FlowStageKey("starter_sdk_init");
        public static readonly FlowStageKey EnterGame = new FlowStageKey("starter_enter_game");

        public static IReadOnlyList<FlowStageKey> TryStages { get; } = new List<FlowStageKey>
        {
            ProjectInit,
            SdkInit,
            EnterGame
        };

        public static IReadOnlyList<FlowStageKey> FinallyStages { get; } = new List<FlowStageKey>();
    }
}
