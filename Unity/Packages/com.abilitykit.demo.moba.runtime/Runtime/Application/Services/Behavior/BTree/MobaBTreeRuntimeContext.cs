using System;
using AbilityKit.Ability.Behavior;
using AbilityKit.BehaviorTree.Blackboard;
using AbilityKit.BehaviorTree.Definition;
using AbilityKit.Deterministic;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Demo.Moba.Services.Search;
using ValueType = AbilityKit.BehaviorTree.Definition.ValueType;

namespace AbilityKit.Demo.Moba.Services.Behavior.BTree
{
    /// <summary>
    /// Runtime-only dependencies available to sensing nodes. Conditions and intent nodes should
    /// communicate through the blackboard instead of querying the world directly.
    /// 节点经 <see cref="AbilityKit.BehaviorTree.Execution.ServiceResolver"/> 解析本上下文（替代旧 IMobaBTreeContextNode 绑定）。
    /// </summary>
    public sealed class MobaBTreeRuntimeContext
    {
        internal MobaActorRegistry Registry { get; }
        internal MobaConfigDatabase Config { get; }
        internal SearchTargetService SearchTargets { get; }
        internal Func<long> CurrentTimeMsProvider { get; }
        internal MobaBrainSkillSelectionPolicy SkillSelectionPolicy { get; }
        internal IBehaviorContext Behavior { get; private set; }
        internal IWorldQuery World { get; private set; }

        internal MobaBTreeRuntimeContext(
            MobaActorRegistry registry,
            MobaConfigDatabase config,
            SearchTargetService searchTargets,
            Func<long> currentTimeMsProvider,
            MobaBrainSkillSelectionPolicy skillSelectionPolicy)
        {
            Registry = registry;
            Config = config;
            SearchTargets = searchTargets;
            CurrentTimeMsProvider = currentTimeMsProvider;
            SkillSelectionPolicy = skillSelectionPolicy;
        }

        internal void BeginEvaluation(IBehaviorContext behavior, IWorldQuery world)
        {
            Behavior = behavior;
            World = world;
        }

        internal void EndEvaluation()
        {
            Behavior = null;
            World = null;
        }

        internal long GetCurrentTimeMs()
        {
            return CurrentTimeMsProvider != null
                ? CurrentTimeMsProvider()
                : (long)Math.Round((Behavior?.ElapsedSeconds ?? 0d) * 1000d);
        }
    }

    internal static class MobaBTreeKeys
    {
        public const string OwnerId = "self.actorId";
        public const string OwnerX = "self.x";
        public const string OwnerY = "self.y";
        public const string OwnerZ = "self.z";
        public const string OwnerSpeed = "self.speed";
        public const string OwnerCanMove = "self.canMove";
        public const string OwnerCanCast = "self.canCast";
        public const string EvaluationFrame = "self.evaluationFrame";

        public const string TargetValid = "target.valid";
        public const string TargetId = "target.actorId";
        public const string TargetX = "target.x";
        public const string TargetY = "target.y";
        public const string TargetZ = "target.z";
        public const string TargetDistance = "target.distance";
        public const string TargetSelectedFrame = "target.selectedFrame";

        public const string SkillValid = "candidateSkill.valid";
        public const string SkillId = "candidateSkill.skillId";
        public const string SkillSlot = "candidateSkill.slot";
        public const string SkillRange = "candidateSkill.range";
        public const string SkillApproachRange = "candidateSkill.approachRange";
        public const string SkillCategory = "candidateSkill.category";
        public const string SkillType = "candidateSkill.type";
        public const string SkillTargetQueryId = "candidateSkill.targetQueryId";

        public const string AimValid = "aim.valid";
        public const string AimTargetActorId = "aim.targetActorId";
        public const string AimX = "aim.x";
        public const string AimY = "aim.y";
        public const string AimZ = "aim.z";
        public const string AimDirectionX = "aim.directionX";
        public const string AimDirectionY = "aim.directionY";
        public const string AimDirectionZ = "aim.directionZ";

        public const string CastRequestValid = "intent.cast.valid";
        public const string CastRequestPriority = "intent.cast.priority";
        public const string CastRequestSkillId = "intent.cast.skillId";
        public const string CastRequestSkillSlot = "intent.cast.skillSlot";
        public const string CastRequestTargetActorId = "intent.cast.targetActorId";
        public const string CastRequestAimX = "intent.cast.aimX";
        public const string CastRequestAimY = "intent.cast.aimY";
        public const string CastRequestAimZ = "intent.cast.aimZ";
        public const string CastRequestDirectionX = "intent.cast.directionX";
        public const string CastRequestDirectionY = "intent.cast.directionY";
        public const string CastRequestDirectionZ = "intent.cast.directionZ";

        public const string MoveRequestValid = "intent.move.valid";
        public const string MoveRequestPriority = "intent.move.priority";
        public const string MoveRequestX = "intent.move.x";
        public const string MoveRequestY = "intent.move.y";
        public const string MoveRequestZ = "intent.move.z";
        public const string MoveRequestStopRange = "intent.move.stopRange";

        public const string HoldRequestValid = "intent.hold.valid";
        public const string HoldRequestPriority = "intent.hold.priority";

        public const string OutputKind = "out.kind";
        public const string HasMove = "out.hasMove";
        public const string MoveX = "out.moveX";
        public const string MoveY = "out.moveY";
        public const string MoveZ = "out.moveZ";
        public const string HasCast = "out.hasCast";
        public const string CastSkillId = "out.skillId";
        public const string CastSkillSlot = "out.skillSlot";
        public const string CastTargetActorId = "out.targetActorId";
        public const string CastAimX = "out.aimX";
        public const string CastAimY = "out.aimY";
        public const string CastAimZ = "out.aimZ";
        public const string CastDirectionX = "out.directionX";
        public const string CastDirectionY = "out.directionY";
        public const string CastDirectionZ = "out.directionZ";
    }

    internal enum MobaBTreeIntentKind
    {
        Hold = 0,
        Move = 1,
        Cast = 2,
    }

    /// <summary>
    /// 黑板协议助手：标准 key 声明注入、目标/技能清理与每帧瞬时意图清空。
    /// 位置/距离/朝向以 Fixed64 存储（写入端 float 边界转换），id/优先级为 Int64。
    /// </summary>
    internal static class MobaBTreeBlackboard
    {
        /// <summary>把标准 key 补进树定义（树 JSON 只需声明用到的 key，其余由此注入）。</summary>
        public static void EnsureStandardSchema(TreeDefinition definition)
        {
            Ensure(definition, MobaBTreeKeys.OwnerId, ValueType.Int64);
            Ensure(definition, MobaBTreeKeys.OwnerX, ValueType.Fixed64);
            Ensure(definition, MobaBTreeKeys.OwnerY, ValueType.Fixed64);
            Ensure(definition, MobaBTreeKeys.OwnerZ, ValueType.Fixed64);
            Ensure(definition, MobaBTreeKeys.OwnerSpeed, ValueType.Fixed64);
            Ensure(definition, MobaBTreeKeys.OwnerCanMove, ValueType.Bool);
            Ensure(definition, MobaBTreeKeys.OwnerCanCast, ValueType.Bool);
            Ensure(definition, MobaBTreeKeys.EvaluationFrame, ValueType.Int64);

            Ensure(definition, MobaBTreeKeys.TargetValid, ValueType.Bool);
            Ensure(definition, MobaBTreeKeys.TargetId, ValueType.Int64);
            Ensure(definition, MobaBTreeKeys.TargetX, ValueType.Fixed64);
            Ensure(definition, MobaBTreeKeys.TargetY, ValueType.Fixed64);
            Ensure(definition, MobaBTreeKeys.TargetZ, ValueType.Fixed64);
            Ensure(definition, MobaBTreeKeys.TargetDistance, ValueType.Fixed64);
            Ensure(definition, MobaBTreeKeys.TargetSelectedFrame, ValueType.Int64);

            Ensure(definition, MobaBTreeKeys.SkillValid, ValueType.Bool);
            Ensure(definition, MobaBTreeKeys.SkillId, ValueType.Int64);
            Ensure(definition, MobaBTreeKeys.SkillSlot, ValueType.Int64);
            Ensure(definition, MobaBTreeKeys.SkillRange, ValueType.Fixed64);
            Ensure(definition, MobaBTreeKeys.SkillApproachRange, ValueType.Fixed64);
            Ensure(definition, MobaBTreeKeys.SkillCategory, ValueType.Int64);
            Ensure(definition, MobaBTreeKeys.SkillType, ValueType.Int64);
            Ensure(definition, MobaBTreeKeys.SkillTargetQueryId, ValueType.Int64);

            Ensure(definition, MobaBTreeKeys.AimValid, ValueType.Bool);
            Ensure(definition, MobaBTreeKeys.AimTargetActorId, ValueType.Int64);
            Ensure(definition, MobaBTreeKeys.AimX, ValueType.Fixed64);
            Ensure(definition, MobaBTreeKeys.AimY, ValueType.Fixed64);
            Ensure(definition, MobaBTreeKeys.AimZ, ValueType.Fixed64);
            Ensure(definition, MobaBTreeKeys.AimDirectionX, ValueType.Fixed64);
            Ensure(definition, MobaBTreeKeys.AimDirectionY, ValueType.Fixed64);
            Ensure(definition, MobaBTreeKeys.AimDirectionZ, ValueType.Fixed64);

            Ensure(definition, MobaBTreeKeys.CastRequestValid, ValueType.Bool);
            Ensure(definition, MobaBTreeKeys.CastRequestPriority, ValueType.Int64);
            Ensure(definition, MobaBTreeKeys.CastRequestSkillId, ValueType.Int64);
            Ensure(definition, MobaBTreeKeys.CastRequestSkillSlot, ValueType.Int64);
            Ensure(definition, MobaBTreeKeys.CastRequestTargetActorId, ValueType.Int64);
            Ensure(definition, MobaBTreeKeys.CastRequestAimX, ValueType.Fixed64);
            Ensure(definition, MobaBTreeKeys.CastRequestAimY, ValueType.Fixed64);
            Ensure(definition, MobaBTreeKeys.CastRequestAimZ, ValueType.Fixed64);
            Ensure(definition, MobaBTreeKeys.CastRequestDirectionX, ValueType.Fixed64);
            Ensure(definition, MobaBTreeKeys.CastRequestDirectionY, ValueType.Fixed64);
            Ensure(definition, MobaBTreeKeys.CastRequestDirectionZ, ValueType.Fixed64);

            Ensure(definition, MobaBTreeKeys.MoveRequestValid, ValueType.Bool);
            Ensure(definition, MobaBTreeKeys.MoveRequestPriority, ValueType.Int64);
            Ensure(definition, MobaBTreeKeys.MoveRequestX, ValueType.Fixed64);
            Ensure(definition, MobaBTreeKeys.MoveRequestY, ValueType.Fixed64);
            Ensure(definition, MobaBTreeKeys.MoveRequestZ, ValueType.Fixed64);
            Ensure(definition, MobaBTreeKeys.MoveRequestStopRange, ValueType.Fixed64);

            Ensure(definition, MobaBTreeKeys.HoldRequestValid, ValueType.Bool);
            Ensure(definition, MobaBTreeKeys.HoldRequestPriority, ValueType.Int64);

            Ensure(definition, MobaBTreeKeys.OutputKind, ValueType.Int64);
            Ensure(definition, MobaBTreeKeys.HasMove, ValueType.Bool);
            Ensure(definition, MobaBTreeKeys.MoveX, ValueType.Fixed64);
            Ensure(definition, MobaBTreeKeys.MoveY, ValueType.Fixed64);
            Ensure(definition, MobaBTreeKeys.MoveZ, ValueType.Fixed64);
            Ensure(definition, MobaBTreeKeys.HasCast, ValueType.Bool);
            Ensure(definition, MobaBTreeKeys.CastSkillId, ValueType.Int64);
            Ensure(definition, MobaBTreeKeys.CastSkillSlot, ValueType.Int64);
            Ensure(definition, MobaBTreeKeys.CastTargetActorId, ValueType.Int64);
            Ensure(definition, MobaBTreeKeys.CastAimX, ValueType.Fixed64);
            Ensure(definition, MobaBTreeKeys.CastAimY, ValueType.Fixed64);
            Ensure(definition, MobaBTreeKeys.CastAimZ, ValueType.Fixed64);
            Ensure(definition, MobaBTreeKeys.CastDirectionX, ValueType.Fixed64);
            Ensure(definition, MobaBTreeKeys.CastDirectionY, ValueType.Fixed64);
            Ensure(definition, MobaBTreeKeys.CastDirectionZ, ValueType.Fixed64);
        }


        private static void Ensure(TreeDefinition definition, string key, ValueType type)
        {
            if (definition.Blackboard.TryGetType(key, out var existing))
            {
                if (existing != type)
                {
                    throw new InvalidOperationException(
                        $"MOBA behavior-tree blackboard key '{key}' must be {type}, but the tree declares {existing}.");
                }
                return;
            }

            definition.Blackboard.Keys.Add(new BlackboardKeyDefinition { Name = key, Type = type });
        }

        public static void ClearTarget(Blackboard bb)
        {
            bb.SetBool(MobaBTreeKeys.TargetValid, false);
            bb.SetInt64(MobaBTreeKeys.TargetId, 0);
            bb.SetFixed64(MobaBTreeKeys.TargetX, Fixed64.Zero);
            bb.SetFixed64(MobaBTreeKeys.TargetY, Fixed64.Zero);
            bb.SetFixed64(MobaBTreeKeys.TargetZ, Fixed64.Zero);
            bb.SetFixed64(MobaBTreeKeys.TargetDistance, Fixed64.Zero);
            bb.SetInt64(MobaBTreeKeys.TargetSelectedFrame, 0);
        }

        public static void ClearSkill(Blackboard bb, Fixed64 defaultApproachRange)
        {
            bb.SetBool(MobaBTreeKeys.SkillValid, false);
            bb.SetInt64(MobaBTreeKeys.SkillId, 0);
            bb.SetInt64(MobaBTreeKeys.SkillSlot, 0);
            bb.SetFixed64(MobaBTreeKeys.SkillRange, Fixed64.Zero);
            bb.SetFixed64(MobaBTreeKeys.SkillApproachRange, defaultApproachRange);
            bb.SetInt64(MobaBTreeKeys.SkillCategory, 0);
            bb.SetInt64(MobaBTreeKeys.SkillType, 0);
            bb.SetInt64(MobaBTreeKeys.SkillTargetQueryId, 0);
        }

        public static void ClearTransientIntents(Blackboard bb)
        {
            bb.SetBool(MobaBTreeKeys.AimValid, false);
            bb.SetInt64(MobaBTreeKeys.AimTargetActorId, 0);
            bb.SetFixed64(MobaBTreeKeys.AimX, Fixed64.Zero);
            bb.SetFixed64(MobaBTreeKeys.AimY, Fixed64.Zero);
            bb.SetFixed64(MobaBTreeKeys.AimZ, Fixed64.Zero);
            bb.SetFixed64(MobaBTreeKeys.AimDirectionX, Fixed64.Zero);
            bb.SetFixed64(MobaBTreeKeys.AimDirectionY, Fixed64.Zero);
            bb.SetFixed64(MobaBTreeKeys.AimDirectionZ, Fixed64.Zero);
            bb.SetBool(MobaBTreeKeys.CastRequestValid, false);
            bb.SetInt64(MobaBTreeKeys.CastRequestPriority, 0);
            bb.SetInt64(MobaBTreeKeys.CastRequestSkillId, 0);
            bb.SetInt64(MobaBTreeKeys.CastRequestSkillSlot, 0);
            bb.SetInt64(MobaBTreeKeys.CastRequestTargetActorId, 0);
            bb.SetFixed64(MobaBTreeKeys.CastRequestAimX, Fixed64.Zero);
            bb.SetFixed64(MobaBTreeKeys.CastRequestAimY, Fixed64.Zero);
            bb.SetFixed64(MobaBTreeKeys.CastRequestAimZ, Fixed64.Zero);
            bb.SetFixed64(MobaBTreeKeys.CastRequestDirectionX, Fixed64.Zero);
            bb.SetFixed64(MobaBTreeKeys.CastRequestDirectionY, Fixed64.Zero);
            bb.SetFixed64(MobaBTreeKeys.CastRequestDirectionZ, Fixed64.Zero);
            bb.SetBool(MobaBTreeKeys.MoveRequestValid, false);
            bb.SetInt64(MobaBTreeKeys.MoveRequestPriority, 0);
            bb.SetFixed64(MobaBTreeKeys.MoveRequestX, Fixed64.Zero);
            bb.SetFixed64(MobaBTreeKeys.MoveRequestY, Fixed64.Zero);
            bb.SetFixed64(MobaBTreeKeys.MoveRequestZ, Fixed64.Zero);
            bb.SetFixed64(MobaBTreeKeys.MoveRequestStopRange, Fixed64.Zero);
            bb.SetBool(MobaBTreeKeys.HoldRequestValid, false);
            bb.SetInt64(MobaBTreeKeys.HoldRequestPriority, 0);
            bb.SetInt64(MobaBTreeKeys.OutputKind, (long)MobaBTreeIntentKind.Hold);
            bb.SetBool(MobaBTreeKeys.HasMove, false);
            bb.SetFixed64(MobaBTreeKeys.MoveX, Fixed64.Zero);
            bb.SetFixed64(MobaBTreeKeys.MoveY, Fixed64.Zero);
            bb.SetFixed64(MobaBTreeKeys.MoveZ, Fixed64.Zero);
            bb.SetBool(MobaBTreeKeys.HasCast, false);
            bb.SetInt64(MobaBTreeKeys.CastSkillId, 0);
            bb.SetInt64(MobaBTreeKeys.CastSkillSlot, 0);
            bb.SetInt64(MobaBTreeKeys.CastTargetActorId, 0);
            bb.SetFixed64(MobaBTreeKeys.CastAimX, Fixed64.Zero);
            bb.SetFixed64(MobaBTreeKeys.CastAimY, Fixed64.Zero);
            bb.SetFixed64(MobaBTreeKeys.CastAimZ, Fixed64.Zero);
            bb.SetFixed64(MobaBTreeKeys.CastDirectionX, Fixed64.Zero);
            bb.SetFixed64(MobaBTreeKeys.CastDirectionY, Fixed64.Zero);
            bb.SetFixed64(MobaBTreeKeys.CastDirectionZ, Fixed64.Zero);
        }
    }
}
