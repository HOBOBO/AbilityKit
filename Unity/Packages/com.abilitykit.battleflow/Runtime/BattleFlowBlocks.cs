using AbilityKit.Scenario;

namespace AbilityKit.BattleFlow
{
    /// <summary>设置环境 Profile（引用 com.abilitykit.environment 的 EnvironmentProfileCatalog 里的具名场景）。</summary>
    public sealed class SetEnvironmentBlock : BattleAtomicBlock
    {
        /// <summary>环境 Profile id。</summary>
        public string ProfileId { get; set; } = string.Empty;

        /// <inheritdoc/>
        public override void Compile(BattleFlowBuilder builder) => builder.SetEnvironment(ProfileId);
    }

    /// <summary>生成一个 actor（施法者/目标）。</summary>
    public sealed class SpawnActorBlock : BattleAtomicBlock
    {
        /// <summary>actor 别名（供后续时间线步骤引用）。</summary>
        public string Alias { get; set; } = string.Empty;

        /// <summary>所属玩家（施法者绑本地玩家，如 "player_1"）。</summary>
        public string? PlayerId { get; set; }

        /// <summary>英雄/模板 id（不透明 int，项目语义）。</summary>
        public int HeroId { get; set; }

        /// <summary>属性模板 id（决定英雄的属性+技能 loadout）。</summary>
        public int AttributeTemplateId { get; set; }

        /// <summary>技能 id 列表（信息性，真实技能由属性模板的 ActiveSkills/PassiveSkills 解析）。</summary>
        public int[] SkillIds { get; set; } = System.Array.Empty<int>();

        /// <summary>阵营。</summary>
        public int TeamId { get; set; }

        /// <summary>生成位置（可选）。</summary>
        public TestVector3? Position { get; set; }

        /// <inheritdoc/>
        public override void Compile(BattleFlowBuilder builder) => builder.AddActor(new TestActor
        {
            Alias = Alias,
            PlayerId = PlayerId,
            HeroId = HeroId,
            AttributeTemplateId = AttributeTemplateId,
            SkillIds = SkillIds,
            TeamId = TeamId,
            Position = Position,
        });
    }

    /// <summary>一条时间线步骤（通用：Action 决定语义，如 cast_skill/wait/move_to）。项目可继承或复合成「施放技能」等语义积木。</summary>
    public sealed class TimelineStepBlock : BattleAtomicBlock
    {
        /// <inheritdoc/>
        public override BattleBlockSection Section => BattleBlockSection.Timeline;

        /// <summary>触发时刻（毫秒）。</summary>
        public int AtMs { get; set; }

        /// <summary>动作语义（cast_skill/wait/move_to…）。</summary>
        public string Action { get; set; } = string.Empty;

        /// <summary>施动者别名。</summary>
        public string? ActorAlias { get; set; }

        /// <summary>目标别名。</summary>
        public string? TargetAlias { get; set; }

        /// <summary>技能槽位/编号（不透明 int，项目语义）。</summary>
        public int Slot { get; set; }

        /// <inheritdoc/>
        public override void Compile(BattleFlowBuilder builder) => builder.AddTimelineStep(new TestTimelineStep
        {
            AtMs = AtMs,
            Action = Action,
            ActorAlias = ActorAlias,
            TargetAlias = TargetAlias,
            Slot = Slot,
        });
    }

    /// <summary>等待一段时间（wait）。</summary>
    public sealed class WaitBlock : BattleAtomicBlock
    {
        /// <inheritdoc/>
        public override BattleBlockSection Section => BattleBlockSection.Timeline;

        /// <summary>触发时刻（毫秒）。</summary>
        public int AtMs { get; set; }

        /// <summary>等待时长（毫秒）。</summary>
        public int DurationMs { get; set; } = 500;

        /// <inheritdoc/>
        public override void Compile(BattleFlowBuilder builder) => builder.AddTimelineStep(new TestTimelineStep
        {
            AtMs = AtMs,
            Action = "wait",
            DurationMs = DurationMs,
        });
    }

    /// <summary>把 actor 移动到某位置（move_to）。</summary>
    public sealed class MoveToBlock : BattleAtomicBlock
    {
        /// <inheritdoc/>
        public override BattleBlockSection Section => BattleBlockSection.Timeline;

        /// <summary>触发时刻（毫秒）。</summary>
        public int AtMs { get; set; }

        /// <summary>施动者别名。</summary>
        public string ActorAlias { get; set; } = string.Empty;

        /// <summary>目标位置。</summary>
        public TestVector3? Position { get; set; }

        /// <inheritdoc/>
        public override void Compile(BattleFlowBuilder builder) => builder.AddTimelineStep(new TestTimelineStep
        {
            AtMs = AtMs,
            Action = "move_to",
            ActorAlias = ActorAlias,
            Position = Position,
        });
    }

    /// <summary>放置一个障碍物（墙体/立柱）。</summary>
    public sealed class PlaceObstacleBlock : BattleAtomicBlock
    {
        /// <summary>障碍 id。</summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>障碍形状（box 等）。</summary>
        public string Shape { get; set; } = "box";

        /// <summary>障碍尺寸。</summary>
        public TestVector3 Size { get; set; } = new(1, 1, 1);

        /// <summary>障碍位置。</summary>
        public TestVector3 Position { get; set; }

        /// <inheritdoc/>
        public override void Compile(BattleFlowBuilder builder) => builder.AddObstacle(new TestObstacle
        {
            Id = Id,
            Shape = Shape,
            Size = Size,
            Position = Position,
        });
    }
}
