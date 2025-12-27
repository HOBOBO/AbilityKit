using System.Collections.Generic;

namespace AbilityKit.Ability.Flow.Stages
{
    public static class FlowStages
    {
        public static readonly FlowStageKey PreEnter = new FlowStageKey("pre_enter");
        public static readonly FlowStageKey Enter = new FlowStageKey("enter");
        public static readonly FlowStageKey PostEnter = new FlowStageKey("post_enter");

        public static readonly FlowStageKey Running = new FlowStageKey("running");

        public static readonly FlowStageKey PreExit = new FlowStageKey("pre_exit");
        public static readonly FlowStageKey Exit = new FlowStageKey("exit");
        public static readonly FlowStageKey PostExit = new FlowStageKey("post_exit");

        public static IReadOnlyList<FlowStageKey> DefaultOrder { get; } = new List<FlowStageKey>
        {
            PreEnter,
            Enter,
            PostEnter,
            Running,
            PreExit,
            Exit,
            PostExit
        };

        public static IReadOnlyList<FlowStageKey> DefaultTryOrder { get; } = new List<FlowStageKey>
        {
            PreEnter,
            Enter,
            PostEnter,
            Running
        };

        public static IReadOnlyList<FlowStageKey> DefaultFinallyOrder { get; } = new List<FlowStageKey>
        {
            PreExit,
            Exit,
            PostExit
        };
    }
}
