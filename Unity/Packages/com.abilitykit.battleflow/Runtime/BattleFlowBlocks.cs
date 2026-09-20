using System;
using System.Collections.Generic;
using AbilityKit.Scenario;

namespace AbilityKit.BattleFlow
{
    /// <summary>Creates a conventional caster/target setup with one author-facing block.</summary>
    public sealed class DuelSetupBlock : BattleAuthorBlock
    {
        public string EnvironmentProfileId { get; set; } = string.Empty;
        public string CasterAlias { get; set; } = "caster";
        public int CasterHeroId { get; set; }
        public int CasterAttributeTemplateId { get; set; }
        public string CasterPlayerId { get; set; } = "player_1";
        public int CasterTeamId { get; set; } = 1;
        public string TargetAlias { get; set; } = "target";
        public int TargetHeroId { get; set; }
        public int TargetAttributeTemplateId { get; set; }
        public int TargetTeamId { get; set; } = 2;
        public float TargetDistance { get; set; } = 3f;

        public override IReadOnlyList<string> Validate()
        {
            var errors = new List<string>();
            if (string.IsNullOrWhiteSpace(CasterAlias)) errors.Add("caster alias is required");
            if (string.IsNullOrWhiteSpace(TargetAlias)) errors.Add("target alias is required");
            if (string.Equals(CasterAlias, TargetAlias, StringComparison.Ordinal))
                errors.Add("caster and target aliases must be different");
            if (TargetDistance < 0f) errors.Add("target distance cannot be negative");
            return errors;
        }

        public override IReadOnlyList<BattleBlock> Expand()
        {
            var blocks = new List<BattleBlock>();
            if (!string.IsNullOrWhiteSpace(EnvironmentProfileId))
                blocks.Add(new SetEnvironmentBlock { ProfileId = EnvironmentProfileId });
            blocks.Add(new SpawnActorBlock
            {
                Alias = CasterAlias,
                HeroId = CasterHeroId,
                AttributeTemplateId = CasterAttributeTemplateId,
                PlayerId = CasterPlayerId,
                TeamId = CasterTeamId,
                Position = new TestVector3(0f, 0f, 0f),
            });
            blocks.Add(new SpawnActorBlock
            {
                Alias = TargetAlias,
                HeroId = TargetHeroId,
                AttributeTemplateId = TargetAttributeTemplateId,
                TeamId = TargetTeamId,
                Position = new TestVector3(TargetDistance, 0f, 0f),
            });
            return blocks;
        }
    }

    /// <summary>Author-facing request to cast one skill at a target.</summary>
    public sealed class CastSkillBlock : BattleAuthorBlock
    {
        public string CasterAlias { get; set; } = "caster";
        public string TargetAlias { get; set; } = "target";
        public int Slot { get; set; } = 1;
        public int AtMs { get; set; } = 100;

        public override IReadOnlyList<string> Validate()
        {
            var errors = new List<string>();
            if (string.IsNullOrWhiteSpace(CasterAlias)) errors.Add("caster alias is required");
            if (string.IsNullOrWhiteSpace(TargetAlias)) errors.Add("target alias is required");
            if (Slot < 0) errors.Add("skill slot cannot be negative");
            if (AtMs < 0) errors.Add("cast time cannot be negative");
            return errors;
        }

        public override IReadOnlyList<BattleBlock> Expand() => new BattleBlock[]
        {
            new TimelineStepBlock
            {
                AtMs = AtMs,
                Action = "cast_skill",
                ActorAlias = CasterAlias,
                TargetAlias = TargetAlias,
                Slot = Slot,
            },
        };
    }

    /// <summary>Configures deterministic scenario execution independently of world setup.</summary>
    public sealed class ExecutionSettingsBlock : BattleAtomicBlock
    {
        /// <inheritdoc/>
        public override BattleBlockSection Section => BattleBlockSection.Settings;

        /// <summary>Fixed simulation ticks per second.</summary>
        public int TickRate { get; set; } = 30;
        /// <summary>Hard safety ceiling for the complete run.</summary>
        public int MaxDurationMs { get; set; } = 30_000;
        /// <summary>Observation window after normal completion.</summary>
        public int SettleDurationMs { get; set; } = 500;
        /// <summary>Normal completion condition kind.</summary>
        public string EndCondition { get; set; } = TestEndConditionKinds.TimelineComplete;
        /// <summary>Duration used when <see cref="EndCondition"/> is duration-based.</summary>
        public int DurationMs { get; set; }

        /// <inheritdoc/>
        public override void Compile(BattleFlowBuilder builder) => builder.SetExecution(
            TickRate,
            MaxDurationMs,
            SettleDurationMs,
            EndCondition,
            DurationMs);
    }

    /// <summary>Sets the deterministic seed stored in the neutral scenario IR.</summary>
    public sealed class SetScenarioSeedBlock : BattleAtomicBlock
    {
        /// <inheritdoc/>
        public override BattleBlockSection Section => BattleBlockSection.Settings;

        /// <summary>Deterministic random seed.</summary>
        public int Seed { get; set; }

        /// <inheritdoc/>
        public override void Compile(BattleFlowBuilder builder) => builder.SetSeed(Seed);
    }

    /// <summary>A timestamped command compiled into the neutral scenario IR.</summary>
    public sealed class CommandBlock : BattleAtomicBlock
    {
        /// <inheritdoc/>
        public override BattleBlockSection Section => BattleBlockSection.Timeline;

        /// <summary>Virtual scenario timestamp in milliseconds.</summary>
        public int AtMs { get; set; }
        /// <summary>Runtime-neutral command name.</summary>
        public string Name { get; set; } = string.Empty;
        /// <summary>Optional actor selected by the command.</summary>
        public string? ActorAlias { get; set; }
        /// <summary>Optional target selected by the command.</summary>
        public string? TargetAlias { get; set; }
        /// <summary>Opaque parameters interpreted by the owning runtime adapter.</summary>
        public IReadOnlyDictionary<string, string> Parameters { get; set; } =
            new Dictionary<string, string>();

        /// <inheritdoc/>
        public override void Compile(BattleFlowBuilder builder)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));
            builder.AddCommand(new TestCommand
            {
                AtMs = AtMs,
                Name = Name,
                ActorAlias = ActorAlias,
                TargetAlias = TargetAlias,
                Parameters = new Dictionary<string, string>(Parameters),
            });
        }
    }

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
