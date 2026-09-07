using System;
using System.Collections.Generic;
using AbilityKit.BehaviorTree.Blackboard;
using AbilityKit.BehaviorTree.Definition;
using AbilityKit.BehaviorTree.Execution;
using AbilityKit.BehaviorTree.Nodes;
using AbilityKit.BehaviorTree.Registry;
using AbilityKit.Core.Mathematics;
using AbilityKit.Deterministic;
using AbilityKit.Demo.Moba.Config.BattleDemo.MO;
using AbilityKit.Demo.Moba.Services.Search;
using AbilityKit.Demo.Moba.Share.Config;
using ExecutionContext = AbilityKit.BehaviorTree.Execution.ExecutionContext;
using ValueType = AbilityKit.BehaviorTree.Definition.ValueType;

namespace AbilityKit.Demo.Moba.Services.Behavior.BTree
{
    /// <summary>MOBA 领域节点类型 id（树 JSON 中的稳定字符串）。</summary>
    internal static class MobaBTreeNodeTypes
    {
        public const string SelectNearestEnemy = "moba.selectNearestEnemy";
        public const string SelectReadySkill = "moba.selectReadySkill";
        public const string ResolveTargetAim = "moba.resolveTargetAim";
        public const string CastSelectedSkill = "moba.castSelectedSkill";
        public const string MoveToEnemy = "moba.moveToEnemy";
        public const string HoldPosition = "moba.holdPosition";
        public const string ArbitrateCombatIntent = "moba.arbitrateCombatIntent";

        public const string HasEnemy = "moba.hasEnemy";
        public const string HasSelectedSkill = "moba.hasSelectedSkill";
        public const string CanCast = "moba.canCast";
        public const string CanMove = "moba.canMove";
        public const string SelectedSkillInRange = "moba.selectedSkillInRange";
        public const string ShouldApproachEnemy = "moba.shouldApproachEnemy";
    }

    /// <summary>
    /// Thin behavior-tree adapter over the shared target-search pipeline. Query composition remains
    /// in SearchTargetService; this node only selects a query and commits its first result.
    /// </summary>
    [NodeType(MobaBTreeNodeTypes.SelectNearestEnemy, "选取最近敌人", "MOBA", NodeKind.Action)]
    public sealed class MobaSelectNearestEnemyNode : ActionNodeBase, NodeDescriptorProvider
    {
        public const string QueryTemplateIdProperty = "queryTemplateId";
        public const string SearchRadiusProperty = "searchRadius";
        public const float DefaultSearchRadius = 1000f;

        private readonly List<int> _results = new List<int>(1);
        private MobaBTreeRuntimeContext _runtime = null!;
        private long _queryTemplateId;
        private Fixed64 _searchRadius = Fixed64.FromInt32(1000);
        private SearchTargetService _fallbackSearch;

        public NodeDescriptor BuildDescriptor(NodeTypeAttribute attribute) => new(
            attribute.NodeTypeId, attribute.DisplayName, attribute.Category, NodeKind.Action, 0, 0,
            () => new MobaSelectNearestEnemyNode(),
            new[]
            {
                new PropertyField(QueryTemplateIdProperty, ValueType.Int64,
                    PropertyValue.Of(0L), "搜索模板 id；0 = 用 searchRadius 构造默认圆形查询"),
                new PropertyField(SearchRadiusProperty, ValueType.Fixed64,
                    PropertyValue.Of(Fixed64.FromInt32(1000)), "默认查询半径"),
            });

        public override void OnInit(in NodeInitContext context)
        {
            _runtime = context.Context.Services.Resolve<MobaBTreeRuntimeContext>();
            _queryTemplateId = context.Properties.GetInt64(QueryTemplateIdProperty, 0);
            _searchRadius = context.Properties.GetFixed64(SearchRadiusProperty, Fixed64.FromInt32(1000));
        }

        public override NodeState OnTick(ExecutionContext context)
        {
            var bb = context.Blackboard;
            MobaBTreeBlackboard.ClearTarget(bb);

            var behavior = _runtime.Behavior;
            var world = _runtime.World;
            var registry = _runtime.Registry;
            if (behavior == null || world == null || registry == null
                || behavior.OwnerId.Value <= 0 || behavior.OwnerId.Value > int.MaxValue)
                return NodeState.Success;

            var search = _runtime.SearchTargets
                         ?? (_fallbackSearch ??= new SearchTargetService(registry, _runtime.Config));
            var ownerId = (int)behavior.OwnerId.Value;
            var ownerPosition = world.GetPosition(behavior.OwnerId);
            _results.Clear();

            var found = _queryTemplateId > 0
                ? search.TrySearchActorIds((int)_queryTemplateId, ownerId, in ownerPosition, 0, _results)
                : search.TrySearchActorIds(CreateDefaultQuery(_searchRadius.ToSingle()), ownerId, in ownerPosition, 0, _results);

            if (!found || _results.Count == 0) return NodeState.Success;
            var targetId = _results[0];
            if (!registry.TryGet(targetId, out var target) || target == null || !target.hasTransform)
                return NodeState.Success;

            var targetPosition = target.transform.Value.Position;
            bb.SetInt64(MobaBTreeKeys.TargetId, targetId);
            bb.SetFixed64(MobaBTreeKeys.TargetX, Fixed64.FromSingle(targetPosition.X));
            bb.SetFixed64(MobaBTreeKeys.TargetY, Fixed64.FromSingle(targetPosition.Y));
            bb.SetFixed64(MobaBTreeKeys.TargetZ, Fixed64.FromSingle(targetPosition.Z));
            bb.SetFixed64(MobaBTreeKeys.TargetDistance,
                Fixed64.FromSingle(world.GetDistanceToPosition(behavior.OwnerId, targetPosition)));
            bb.SetInt64(MobaBTreeKeys.TargetSelectedFrame, bb.GetInt64(MobaBTreeKeys.EvaluationFrame));
            bb.SetBool(MobaBTreeKeys.TargetValid, true);
            return NodeState.Success;
        }

        private static SearchQueryTemplateMO CreateDefaultQuery(float radius)
        {
            var rules = radius > 0f
                ? new[]
                {
                    new SearchTargetRuleConfig(
                        0,
                        (int)SearchTargetRuleKind.CircleShape,
                        center: (int)SearchTargetPointKind.Caster,
                        radius: radius)
                }
                : Array.Empty<SearchTargetRuleConfig>();

            return new SearchQueryTemplateMO(
                id: 0,
                name: "moba.ai.nearest-enemy",
                maxCount: 1,
                explicitTargetPolicy: (int)SearchQueryExplicitTargetPolicy.IgnoreExplicitTarget,
                provider: new SearchTargetProviderConfig(0, (int)SearchTargetProviderKind.EnemyTeam),
                rules: rules,
                scorers: new[]
                {
                    new SearchTargetScorerConfig(
                        0,
                        (int)SearchTargetScorerKind.DistanceToCaster,
                        (int)SearchTargetPointKind.Caster)
                },
                selector: new SearchTargetSelectorConfig(0, (int)SearchTargetSelectorKind.TopKByScore));
        }
    }

    /// <summary>
    /// Selects a cooldown-ready skill candidate. Range and cast-state checks are intentionally
    /// separate conditions so the same candidate can drive cast, approach, or hold branches.
    /// </summary>
    [NodeType(MobaBTreeNodeTypes.SelectReadySkill, "选取就绪技能", "MOBA", NodeKind.Action)]
    public sealed class MobaSelectReadySkillNode : ActionNodeBase, NodeDescriptorProvider
    {
        internal const float DefaultApproachRange = 0.5f;
        public const string SkillIdProperty = "skillId";
        public const string RequiredTagProperty = "requiredTag";

        private MobaBTreeRuntimeContext _runtime = null!;
        private long _requiredSkillId;
        private long _requiredTag;

        public NodeDescriptor BuildDescriptor(NodeTypeAttribute attribute) => new(
            attribute.NodeTypeId, attribute.DisplayName, attribute.Category, NodeKind.Action, 0, 0,
            () => new MobaSelectReadySkillNode(),
            new[]
            {
                new PropertyField(SkillIdProperty, ValueType.Int64,
                    PropertyValue.Of(0L), "限定技能 id；0 = 任意"),
                new PropertyField(RequiredTagProperty, ValueType.Int64,
                    PropertyValue.Of(0L), "限定技能标签；0 = 任意"),
            });

        public override void OnInit(in NodeInitContext context)
        {
            _runtime = context.Context.Services.Resolve<MobaBTreeRuntimeContext>();
            _requiredSkillId = context.Properties.GetInt64(SkillIdProperty, 0);
            _requiredTag = context.Properties.GetInt64(RequiredTagProperty, 0);
        }

        public override NodeState OnTick(ExecutionContext context)
        {
            var bb = context.Blackboard;
            MobaBTreeBlackboard.ClearSkill(bb, Fixed64.FromSingle(DefaultApproachRange));

            var behavior = _runtime.Behavior;
            var registry = _runtime.Registry;
            var config = _runtime.Config;
            if (behavior == null || registry == null || config == null
                || behavior.OwnerId.Value <= 0 || behavior.OwnerId.Value > int.MaxValue)
                return NodeState.Success;
            if (!registry.TryGet((int)behavior.OwnerId.Value, out var owner)
                || owner == null || !owner.hasSkillLoadout)
                return NodeState.Success;

            var skills = owner.skillLoadout.ActiveSkills;
            if (skills == null || skills.Length == 0) return NodeState.Success;

            var nowMs = _runtime.GetCurrentTimeMs();
            var maxConfiguredRange = 0f;
            var candidates = new List<MobaSkillSelectionCandidate>();

            for (var i = 0; i < skills.Length; i++)
            {
                var runtime = skills[i];
                if (runtime == null || runtime.SkillId <= 0) continue;
                if (!config.TryGetSkill(runtime.SkillId, out var skill) || skill == null) continue;

                var range = Math.Max(0f, skill.Range);
                maxConfiguredRange = Math.Max(maxConfiguredRange, range);
                if (_requiredSkillId > 0 && skill.Id != _requiredSkillId) continue;
                if (_requiredTag > 0 && !HasTag(skill, _requiredTag)) continue;
                if (runtime.CooldownEndTimeMs > nowMs) continue;

                candidates.Add(new MobaSkillSelectionCandidate(skill.Id, i + 1, range));
            }

            if (MobaBrainSkillSelectionPolicies.TrySelect(
                    _runtime.SkillSelectionPolicy,
                    candidates,
                    out var selected)
                && config.TryGetSkill(selected.SkillId, out var selectedSkill)
                && selectedSkill != null)
            {
                bb.SetInt64(MobaBTreeKeys.SkillId, selected.SkillId);
                bb.SetInt64(MobaBTreeKeys.SkillSlot, selected.Slot);
                bb.SetFixed64(MobaBTreeKeys.SkillRange, Fixed64.FromSingle(selected.Range));
                bb.SetFixed64(MobaBTreeKeys.SkillApproachRange,
                    selected.Range > 0f ? Fixed64.FromSingle(selected.Range) : Fixed64.FromSingle(DefaultApproachRange));
                bb.SetInt64(MobaBTreeKeys.SkillCategory, selectedSkill.Category);
                bb.SetInt64(MobaBTreeKeys.SkillType, (long)selectedSkill.SkillType);
                bb.SetInt64(MobaBTreeKeys.SkillTargetQueryId, selectedSkill.RequiredTargetQueryId);
                bb.SetBool(MobaBTreeKeys.SkillValid, true);
                return NodeState.Success;
            }

            if (maxConfiguredRange > 0f)
                bb.SetFixed64(MobaBTreeKeys.SkillApproachRange, Fixed64.FromSingle(maxConfiguredRange));
            return NodeState.Success;
        }

        private static bool HasTag(SkillMO skill, long requiredTag)
        {
            if (skill?.Tags == null) return false;
            for (var i = 0; i < skill.Tags.Count; i++)
            {
                if (skill.Tags[i] == requiredTag) return true;
            }

            return false;
        }
    }

    /// <summary>依据当前目标解析瞄准点与朝向。</summary>
    [NodeType(MobaBTreeNodeTypes.ResolveTargetAim, "解析目标瞄准", "MOBA", NodeKind.Action)]
    public sealed class MobaResolveTargetAimNode : ActionNodeBase
    {
        private static readonly Fixed64 MinLength = Fixed64.FromSingle(0.0001f);

        public override NodeState OnTick(ExecutionContext context)
        {
            var bb = context.Blackboard;
            if (!bb.GetBool(MobaBTreeKeys.TargetValid))
                return NodeState.Failure;

            var dx = bb.GetFixed64(MobaBTreeKeys.TargetX) - bb.GetFixed64(MobaBTreeKeys.OwnerX);
            var dy = bb.GetFixed64(MobaBTreeKeys.TargetY) - bb.GetFixed64(MobaBTreeKeys.OwnerY);
            var dz = bb.GetFixed64(MobaBTreeKeys.TargetZ) - bb.GetFixed64(MobaBTreeKeys.OwnerZ);
            var length = DeterministicMath.Sqrt(dx * dx + dy * dy + dz * dz);

            bb.SetInt64(MobaBTreeKeys.AimTargetActorId, bb.GetInt64(MobaBTreeKeys.TargetId));
            bb.SetFixed64(MobaBTreeKeys.AimX, bb.GetFixed64(MobaBTreeKeys.TargetX));
            bb.SetFixed64(MobaBTreeKeys.AimY, bb.GetFixed64(MobaBTreeKeys.TargetY));
            bb.SetFixed64(MobaBTreeKeys.AimZ, bb.GetFixed64(MobaBTreeKeys.TargetZ));
            bb.SetFixed64(MobaBTreeKeys.AimDirectionX, length > MinLength ? dx / length : Fixed64.Zero);
            bb.SetFixed64(MobaBTreeKeys.AimDirectionY, length > MinLength ? dy / length : Fixed64.Zero);
            bb.SetFixed64(MobaBTreeKeys.AimDirectionZ, length > MinLength ? dz / length : Fixed64.One);
            bb.SetBool(MobaBTreeKeys.AimValid, true);
            return NodeState.Success;
        }
    }

    /// <summary>发布施法意图（intent.cast.*）。</summary>
    [NodeType(MobaBTreeNodeTypes.CastSelectedSkill, "发布施法意图", "MOBA", NodeKind.Action)]
    public sealed class MobaCastSelectedSkillNode : ActionNodeBase, NodeDescriptorProvider
    {
        public const string PriorityProperty = "priority";
        public const long DefaultPriority = 100;

        private long _priority = DefaultPriority;

        public NodeDescriptor BuildDescriptor(NodeTypeAttribute attribute) => new(
            attribute.NodeTypeId, attribute.DisplayName, attribute.Category, NodeKind.Action, 0, 0,
            () => new MobaCastSelectedSkillNode(),
            new[] { new PropertyField(PriorityProperty, ValueType.Int64,
                PropertyValue.Of(DefaultPriority), "施法意图优先级") });

        public override void OnInit(in NodeInitContext context)
        {
            _priority = context.Properties.GetInt64(PriorityProperty, DefaultPriority);
        }

        public override NodeState OnTick(ExecutionContext context)
        {
            var bb = context.Blackboard;
            if (!bb.GetBool(MobaBTreeKeys.SkillValid) || !bb.GetBool(MobaBTreeKeys.AimValid))
                return NodeState.Failure;

            bb.SetInt64(MobaBTreeKeys.CastRequestPriority, _priority);
            bb.SetInt64(MobaBTreeKeys.CastRequestSkillId, bb.GetInt64(MobaBTreeKeys.SkillId));
            bb.SetInt64(MobaBTreeKeys.CastRequestSkillSlot, bb.GetInt64(MobaBTreeKeys.SkillSlot));
            bb.SetInt64(MobaBTreeKeys.CastRequestTargetActorId, bb.GetInt64(MobaBTreeKeys.AimTargetActorId));
            bb.SetFixed64(MobaBTreeKeys.CastRequestAimX, bb.GetFixed64(MobaBTreeKeys.AimX));
            bb.SetFixed64(MobaBTreeKeys.CastRequestAimY, bb.GetFixed64(MobaBTreeKeys.AimY));
            bb.SetFixed64(MobaBTreeKeys.CastRequestAimZ, bb.GetFixed64(MobaBTreeKeys.AimZ));
            bb.SetFixed64(MobaBTreeKeys.CastRequestDirectionX, bb.GetFixed64(MobaBTreeKeys.AimDirectionX));
            bb.SetFixed64(MobaBTreeKeys.CastRequestDirectionY, bb.GetFixed64(MobaBTreeKeys.AimDirectionY));
            bb.SetFixed64(MobaBTreeKeys.CastRequestDirectionZ, bb.GetFixed64(MobaBTreeKeys.AimDirectionZ));
            bb.SetBool(MobaBTreeKeys.CastRequestValid, true);
            return NodeState.Success;
        }
    }

    /// <summary>发布移动意图（intent.move.*），目标为当前目标位置。</summary>
    [NodeType(MobaBTreeNodeTypes.MoveToEnemy, "发布移动意图", "MOBA", NodeKind.Action)]
    public sealed class MobaMoveToEnemyNode : ActionNodeBase, NodeDescriptorProvider
    {
        public const string PriorityProperty = "priority";
        public const long DefaultPriority = 50;

        private long _priority = DefaultPriority;

        public NodeDescriptor BuildDescriptor(NodeTypeAttribute attribute) => new(
            attribute.NodeTypeId, attribute.DisplayName, attribute.Category, NodeKind.Action, 0, 0,
            () => new MobaMoveToEnemyNode(),
            new[] { new PropertyField(PriorityProperty, ValueType.Int64,
                PropertyValue.Of(DefaultPriority), "移动意图优先级") });

        public override void OnInit(in NodeInitContext context)
        {
            _priority = context.Properties.GetInt64(PriorityProperty, DefaultPriority);
        }

        public override NodeState OnTick(ExecutionContext context)
        {
            var bb = context.Blackboard;
            if (!bb.GetBool(MobaBTreeKeys.TargetValid))
                return NodeState.Failure;

            bb.SetInt64(MobaBTreeKeys.MoveRequestPriority, _priority);
            bb.SetFixed64(MobaBTreeKeys.MoveRequestX, bb.GetFixed64(MobaBTreeKeys.TargetX));
            bb.SetFixed64(MobaBTreeKeys.MoveRequestY, bb.GetFixed64(MobaBTreeKeys.TargetY));
            bb.SetFixed64(MobaBTreeKeys.MoveRequestZ, bb.GetFixed64(MobaBTreeKeys.TargetZ));
            bb.SetFixed64(MobaBTreeKeys.MoveRequestStopRange, bb.GetFixed64(MobaBTreeKeys.SkillApproachRange));
            bb.SetBool(MobaBTreeKeys.MoveRequestValid, true);
            return NodeState.Success;
        }
    }

    /// <summary>发布保持意图（intent.hold.*）。</summary>
    [NodeType(MobaBTreeNodeTypes.HoldPosition, "发布保持意图", "MOBA", NodeKind.Action)]
    public sealed class MobaHoldPositionNode : ActionNodeBase, NodeDescriptorProvider
    {
        public const string PriorityProperty = "priority";
        public const long DefaultPriority = 0;

        private long _priority = DefaultPriority;

        public NodeDescriptor BuildDescriptor(NodeTypeAttribute attribute) => new(
            attribute.NodeTypeId, attribute.DisplayName, attribute.Category, NodeKind.Action, 0, 0,
            () => new MobaHoldPositionNode(),
            new[] { new PropertyField(PriorityProperty, ValueType.Int64,
                PropertyValue.Of(DefaultPriority), "保持意图优先级") });

        public override void OnInit(in NodeInitContext context)
        {
            _priority = context.Properties.GetInt64(PriorityProperty, DefaultPriority);
        }

        public override NodeState OnTick(ExecutionContext context)
        {
            var bb = context.Blackboard;
            bb.SetInt64(MobaBTreeKeys.HoldRequestPriority, _priority);
            bb.SetBool(MobaBTreeKeys.HoldRequestValid, true);
            return NodeState.Success;
        }
    }

    /// <summary>
    /// The only node allowed to publish out.*. Candidate branches can be combined or run in
    /// parallel without relying on their execution order to resolve conflicts.
    /// </summary>
    [NodeType(MobaBTreeNodeTypes.ArbitrateCombatIntent, "仲裁战斗意图", "MOBA", NodeKind.Action)]
    public sealed class MobaArbitrateCombatIntentNode : ActionNodeBase
    {
        public override NodeState OnTick(ExecutionContext context)
        {
            var bb = context.Blackboard;
            bb.SetInt64(MobaBTreeKeys.OutputKind, (long)MobaBTreeIntentKind.Hold);
            bb.SetBool(MobaBTreeKeys.HasMove, false);
            bb.SetBool(MobaBTreeKeys.HasCast, false);

            var hasCast = bb.GetBool(MobaBTreeKeys.CastRequestValid);
            var hasMove = bb.GetBool(MobaBTreeKeys.MoveRequestValid);
            var hasHold = bb.GetBool(MobaBTreeKeys.HoldRequestValid);
            var castPriority = hasCast ? bb.GetInt64(MobaBTreeKeys.CastRequestPriority) : long.MinValue;
            var movePriority = hasMove ? bb.GetInt64(MobaBTreeKeys.MoveRequestPriority) : long.MinValue;
            var holdPriority = hasHold ? bb.GetInt64(MobaBTreeKeys.HoldRequestPriority) : long.MinValue;

            if (hasCast && castPriority >= movePriority && castPriority >= holdPriority)
            {
                PublishCast(bb);
            }
            else if (hasMove && movePriority >= holdPriority)
            {
                PublishMove(bb);
            }

            return NodeState.Success;
        }

        private static void PublishCast(Blackboard bb)
        {
            bb.SetInt64(MobaBTreeKeys.OutputKind, (long)MobaBTreeIntentKind.Cast);
            bb.SetBool(MobaBTreeKeys.HasCast, true);
            bb.SetInt64(MobaBTreeKeys.CastSkillId, bb.GetInt64(MobaBTreeKeys.CastRequestSkillId));
            bb.SetInt64(MobaBTreeKeys.CastSkillSlot, bb.GetInt64(MobaBTreeKeys.CastRequestSkillSlot));
            bb.SetInt64(MobaBTreeKeys.CastTargetActorId, bb.GetInt64(MobaBTreeKeys.CastRequestTargetActorId));
            bb.SetFixed64(MobaBTreeKeys.CastAimX, bb.GetFixed64(MobaBTreeKeys.CastRequestAimX));
            bb.SetFixed64(MobaBTreeKeys.CastAimY, bb.GetFixed64(MobaBTreeKeys.CastRequestAimY));
            bb.SetFixed64(MobaBTreeKeys.CastAimZ, bb.GetFixed64(MobaBTreeKeys.CastRequestAimZ));
            bb.SetFixed64(MobaBTreeKeys.CastDirectionX, bb.GetFixed64(MobaBTreeKeys.CastRequestDirectionX));
            bb.SetFixed64(MobaBTreeKeys.CastDirectionY, bb.GetFixed64(MobaBTreeKeys.CastRequestDirectionY));
            bb.SetFixed64(MobaBTreeKeys.CastDirectionZ, bb.GetFixed64(MobaBTreeKeys.CastRequestDirectionZ));
        }

        private static void PublishMove(Blackboard bb)
        {
            bb.SetInt64(MobaBTreeKeys.OutputKind, (long)MobaBTreeIntentKind.Move);
            bb.SetBool(MobaBTreeKeys.HasMove, true);
            bb.SetFixed64(MobaBTreeKeys.MoveX, bb.GetFixed64(MobaBTreeKeys.MoveRequestX));
            bb.SetFixed64(MobaBTreeKeys.MoveY, bb.GetFixed64(MobaBTreeKeys.MoveRequestY));
            bb.SetFixed64(MobaBTreeKeys.MoveZ, bb.GetFixed64(MobaBTreeKeys.MoveRequestZ));
        }
    }

    // ------------------------------------------------------------------
    // 条件节点
    // ------------------------------------------------------------------

    [NodeType(MobaBTreeNodeTypes.HasEnemy, "有目标", "MOBA", NodeKind.Condition)]
    public sealed class MobaHasEnemyNode : ConditionNodeBase
    {
        protected override bool Validate(ExecutionContext context)
            => context.Blackboard.GetBool(MobaBTreeKeys.TargetValid);
    }

    [NodeType(MobaBTreeNodeTypes.HasSelectedSkill, "有技能候选", "MOBA", NodeKind.Condition)]
    public sealed class MobaHasSelectedSkillNode : ConditionNodeBase
    {
        protected override bool Validate(ExecutionContext context)
            => context.Blackboard.GetBool(MobaBTreeKeys.SkillValid);
    }

    [NodeType(MobaBTreeNodeTypes.CanCast, "可施法", "MOBA", NodeKind.Condition)]
    public sealed class MobaCanCastNode : ConditionNodeBase
    {
        protected override bool Validate(ExecutionContext context)
            => context.Blackboard.GetBool(MobaBTreeKeys.OwnerCanCast);
    }

    [NodeType(MobaBTreeNodeTypes.CanMove, "可移动", "MOBA", NodeKind.Condition)]
    public sealed class MobaCanMoveNode : ConditionNodeBase
    {
        protected override bool Validate(ExecutionContext context)
            => context.Blackboard.GetBool(MobaBTreeKeys.OwnerCanMove);
    }

    [NodeType(MobaBTreeNodeTypes.SelectedSkillInRange, "技能射程内", "MOBA", NodeKind.Condition)]
    public sealed class MobaSelectedSkillInRangeNode : ConditionNodeBase
    {
        protected override bool Validate(ExecutionContext context)
        {
            var bb = context.Blackboard;
            if (!bb.GetBool(MobaBTreeKeys.TargetValid) || !bb.GetBool(MobaBTreeKeys.SkillValid))
                return false;

            var range = bb.GetFixed64(MobaBTreeKeys.SkillRange);
            return range > Fixed64.Zero && bb.GetFixed64(MobaBTreeKeys.TargetDistance) <= range;
        }
    }

    [NodeType(MobaBTreeNodeTypes.ShouldApproachEnemy, "需要接近", "MOBA", NodeKind.Condition)]
    public sealed class MobaShouldApproachEnemyNode : ConditionNodeBase
    {
        private static readonly Fixed64 DefaultApproachRange = Fixed64.FromSingle(0.5f);

        protected override bool Validate(ExecutionContext context)
        {
            var bb = context.Blackboard;
            if (!bb.GetBool(MobaBTreeKeys.TargetValid)) return false;
            var range = bb.GetFixed64(MobaBTreeKeys.SkillApproachRange);
            if (range <= Fixed64.Zero) range = DefaultApproachRange;
            return bb.GetFixed64(MobaBTreeKeys.TargetDistance) > range;
        }
    }
}
