using System.Collections.Generic;
using AbilityKit.Scenario;

namespace AbilityKit.BattleFlow
{
    /// <summary>编译过程中的 IR 累积器：原子积木往里追加构件，最终 <see cref="Build"/> 出 <see cref="TestScenario"/>。</summary>
    public sealed class BattleFlowBuilder
    {
        private readonly List<TestActor> _actors = new List<TestActor>();
        private readonly List<TestObstacle> _obstacles = new List<TestObstacle>();
        private readonly List<TestTimelineStep> _timeline = new List<TestTimelineStep>();
        private readonly List<TestCommand> _commands = new List<TestCommand>();
        private string? _environmentProfileId;

        /// <summary>场景 caseId（编译入口传入）。</summary>
        public string CaseId { get; set; } = string.Empty;

        /// <summary>项目侧的断言插件（opaque，如 MOBA 的 MobaAcceptanceExpectation）。断言积木往里累积。</summary>
        public object? Expectations { get; set; }

        /// <summary>设置环境 Profile 引用。</summary>
        public void SetEnvironment(string profileId) => _environmentProfileId = profileId;

        /// <summary>设置断言插件。</summary>
        public void SetExpectations(object? expectations) => Expectations = expectations;

        /// <summary>追加一个 actor。</summary>
        public void AddActor(TestActor actor) => _actors.Add(actor);

        /// <summary>追加一个障碍物。</summary>
        public void AddObstacle(TestObstacle obstacle) => _obstacles.Add(obstacle);

        /// <summary>追加一条时间线步骤。</summary>
        public void AddTimelineStep(TestTimelineStep step) => _timeline.Add(step);

        /// <summary>追加一条命令。</summary>
        public void AddCommand(TestCommand command) => _commands.Add(command);

        /// <summary>产出玩法中立的 <see cref="TestScenario"/>。</summary>
        public TestScenario Build() => new TestScenario
        {
            CaseId = CaseId,
            EnvironmentProfileId = _environmentProfileId,
            Actors = _actors,
            Obstacles = _obstacles,
            Timeline = _timeline,
            Commands = _commands,
            Expectations = Expectations,
        };
    }
}
