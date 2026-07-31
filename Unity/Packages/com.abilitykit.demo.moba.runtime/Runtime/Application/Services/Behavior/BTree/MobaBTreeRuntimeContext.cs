using System;
using AbilityKit.Ability.Behavior;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Demo.Moba.Services.Search;
using BTCore.Runtime.Blackboards;

namespace AbilityKit.Demo.Moba.Services.Behavior.BTree
{
    public interface IMobaBTreeContextNode
    {
        void Bind(MobaBTreeRuntimeContext context);
    }

    /// <summary>
    /// Runtime-only dependencies available to sensing nodes. Conditions and intent nodes should
    /// communicate through the blackboard instead of querying the world directly.
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

    internal static class MobaBTreeBlackboard
    {
        public static void Initialize(Blackboard bb)
        {
            if (bb == null) throw new ArgumentNullException(nameof(bb));
            if (bb.Values == null)
                bb.Values = new System.Collections.Generic.List<BTCore.Runtime.Blackboards.BlackboardValue>();

            ValidateDeclaredKeys(bb);
            Ensure<long>(bb, MobaBTreeKeys.OwnerId);
            Ensure<float>(bb, MobaBTreeKeys.OwnerX);
            Ensure<float>(bb, MobaBTreeKeys.OwnerY);
            Ensure<float>(bb, MobaBTreeKeys.OwnerZ);
            Ensure<float>(bb, MobaBTreeKeys.OwnerSpeed);
            Ensure<bool>(bb, MobaBTreeKeys.OwnerCanMove);
            Ensure<bool>(bb, MobaBTreeKeys.OwnerCanCast);
            Ensure<long>(bb, MobaBTreeKeys.EvaluationFrame);

            Ensure<bool>(bb, MobaBTreeKeys.TargetValid);
            Ensure<int>(bb, MobaBTreeKeys.TargetId);
            Ensure<float>(bb, MobaBTreeKeys.TargetX);
            Ensure<float>(bb, MobaBTreeKeys.TargetY);
            Ensure<float>(bb, MobaBTreeKeys.TargetZ);
            Ensure<float>(bb, MobaBTreeKeys.TargetDistance);
            Ensure<long>(bb, MobaBTreeKeys.TargetSelectedFrame);

            Ensure<bool>(bb, MobaBTreeKeys.SkillValid);
            Ensure<int>(bb, MobaBTreeKeys.SkillId);
            Ensure<int>(bb, MobaBTreeKeys.SkillSlot);
            Ensure<float>(bb, MobaBTreeKeys.SkillRange);
            Ensure<float>(bb, MobaBTreeKeys.SkillApproachRange);
            Ensure<int>(bb, MobaBTreeKeys.SkillCategory);
            Ensure<int>(bb, MobaBTreeKeys.SkillType);
            Ensure<int>(bb, MobaBTreeKeys.SkillTargetQueryId);

            Ensure<bool>(bb, MobaBTreeKeys.AimValid);
            Ensure<int>(bb, MobaBTreeKeys.AimTargetActorId);
            Ensure<float>(bb, MobaBTreeKeys.AimX);
            Ensure<float>(bb, MobaBTreeKeys.AimY);
            Ensure<float>(bb, MobaBTreeKeys.AimZ);
            Ensure<float>(bb, MobaBTreeKeys.AimDirectionX);
            Ensure<float>(bb, MobaBTreeKeys.AimDirectionY);
            Ensure<float>(bb, MobaBTreeKeys.AimDirectionZ);

            Ensure<bool>(bb, MobaBTreeKeys.CastRequestValid);
            Ensure<int>(bb, MobaBTreeKeys.CastRequestPriority);
            Ensure<int>(bb, MobaBTreeKeys.CastRequestSkillId);
            Ensure<int>(bb, MobaBTreeKeys.CastRequestSkillSlot);
            Ensure<int>(bb, MobaBTreeKeys.CastRequestTargetActorId);
            Ensure<float>(bb, MobaBTreeKeys.CastRequestAimX);
            Ensure<float>(bb, MobaBTreeKeys.CastRequestAimY);
            Ensure<float>(bb, MobaBTreeKeys.CastRequestAimZ);
            Ensure<float>(bb, MobaBTreeKeys.CastRequestDirectionX);
            Ensure<float>(bb, MobaBTreeKeys.CastRequestDirectionY);
            Ensure<float>(bb, MobaBTreeKeys.CastRequestDirectionZ);

            Ensure<bool>(bb, MobaBTreeKeys.MoveRequestValid);
            Ensure<int>(bb, MobaBTreeKeys.MoveRequestPriority);
            Ensure<float>(bb, MobaBTreeKeys.MoveRequestX);
            Ensure<float>(bb, MobaBTreeKeys.MoveRequestY);
            Ensure<float>(bb, MobaBTreeKeys.MoveRequestZ);
            Ensure<float>(bb, MobaBTreeKeys.MoveRequestStopRange);

            Ensure<bool>(bb, MobaBTreeKeys.HoldRequestValid);
            Ensure<int>(bb, MobaBTreeKeys.HoldRequestPriority);

            Ensure<int>(bb, MobaBTreeKeys.OutputKind);
            Ensure<bool>(bb, MobaBTreeKeys.HasMove);
            Ensure<float>(bb, MobaBTreeKeys.MoveX);
            Ensure<float>(bb, MobaBTreeKeys.MoveY);
            Ensure<float>(bb, MobaBTreeKeys.MoveZ);
            Ensure<bool>(bb, MobaBTreeKeys.HasCast);
            Ensure<int>(bb, MobaBTreeKeys.CastSkillId);
            Ensure<int>(bb, MobaBTreeKeys.CastSkillSlot);
            Ensure<int>(bb, MobaBTreeKeys.CastTargetActorId);
            Ensure<float>(bb, MobaBTreeKeys.CastAimX);
            Ensure<float>(bb, MobaBTreeKeys.CastAimY);
            Ensure<float>(bb, MobaBTreeKeys.CastAimZ);
            Ensure<float>(bb, MobaBTreeKeys.CastDirectionX);
            Ensure<float>(bb, MobaBTreeKeys.CastDirectionY);
            Ensure<float>(bb, MobaBTreeKeys.CastDirectionZ);
        }

        public static void ClearTarget(Blackboard bb)
        {
            bb.SetValue(MobaBTreeKeys.TargetValid, false);
            bb.SetValue(MobaBTreeKeys.TargetId, 0);
            bb.SetValue(MobaBTreeKeys.TargetX, 0f);
            bb.SetValue(MobaBTreeKeys.TargetY, 0f);
            bb.SetValue(MobaBTreeKeys.TargetZ, 0f);
            bb.SetValue(MobaBTreeKeys.TargetDistance, 0f);
            bb.SetValue(MobaBTreeKeys.TargetSelectedFrame, 0L);
        }

        public static void ClearSkill(Blackboard bb, float defaultApproachRange)
        {
            bb.SetValue(MobaBTreeKeys.SkillValid, false);
            bb.SetValue(MobaBTreeKeys.SkillId, 0);
            bb.SetValue(MobaBTreeKeys.SkillSlot, 0);
            bb.SetValue(MobaBTreeKeys.SkillRange, 0f);
            bb.SetValue(MobaBTreeKeys.SkillApproachRange, defaultApproachRange);
            bb.SetValue(MobaBTreeKeys.SkillCategory, 0);
            bb.SetValue(MobaBTreeKeys.SkillType, 0);
            bb.SetValue(MobaBTreeKeys.SkillTargetQueryId, 0);
        }

        public static void ClearTransientIntents(Blackboard bb)
        {
            bb.SetValue(MobaBTreeKeys.AimValid, false);
            bb.SetValue(MobaBTreeKeys.AimTargetActorId, 0);
            bb.SetValue(MobaBTreeKeys.AimX, 0f);
            bb.SetValue(MobaBTreeKeys.AimY, 0f);
            bb.SetValue(MobaBTreeKeys.AimZ, 0f);
            bb.SetValue(MobaBTreeKeys.AimDirectionX, 0f);
            bb.SetValue(MobaBTreeKeys.AimDirectionY, 0f);
            bb.SetValue(MobaBTreeKeys.AimDirectionZ, 0f);
            bb.SetValue(MobaBTreeKeys.CastRequestValid, false);
            bb.SetValue(MobaBTreeKeys.CastRequestPriority, 0);
            bb.SetValue(MobaBTreeKeys.CastRequestSkillId, 0);
            bb.SetValue(MobaBTreeKeys.CastRequestSkillSlot, 0);
            bb.SetValue(MobaBTreeKeys.CastRequestTargetActorId, 0);
            bb.SetValue(MobaBTreeKeys.CastRequestAimX, 0f);
            bb.SetValue(MobaBTreeKeys.CastRequestAimY, 0f);
            bb.SetValue(MobaBTreeKeys.CastRequestAimZ, 0f);
            bb.SetValue(MobaBTreeKeys.CastRequestDirectionX, 0f);
            bb.SetValue(MobaBTreeKeys.CastRequestDirectionY, 0f);
            bb.SetValue(MobaBTreeKeys.CastRequestDirectionZ, 0f);
            bb.SetValue(MobaBTreeKeys.MoveRequestValid, false);
            bb.SetValue(MobaBTreeKeys.MoveRequestPriority, 0);
            bb.SetValue(MobaBTreeKeys.MoveRequestX, 0f);
            bb.SetValue(MobaBTreeKeys.MoveRequestY, 0f);
            bb.SetValue(MobaBTreeKeys.MoveRequestZ, 0f);
            bb.SetValue(MobaBTreeKeys.MoveRequestStopRange, 0f);
            bb.SetValue(MobaBTreeKeys.HoldRequestValid, false);
            bb.SetValue(MobaBTreeKeys.HoldRequestPriority, 0);
            bb.SetValue(MobaBTreeKeys.OutputKind, (int)MobaBTreeIntentKind.Hold);
            bb.SetValue(MobaBTreeKeys.HasMove, false);
            bb.SetValue(MobaBTreeKeys.MoveX, 0f);
            bb.SetValue(MobaBTreeKeys.MoveY, 0f);
            bb.SetValue(MobaBTreeKeys.MoveZ, 0f);
            bb.SetValue(MobaBTreeKeys.HasCast, false);
            bb.SetValue(MobaBTreeKeys.CastSkillId, 0);
            bb.SetValue(MobaBTreeKeys.CastSkillSlot, 0);
            bb.SetValue(MobaBTreeKeys.CastTargetActorId, 0);
            bb.SetValue(MobaBTreeKeys.CastAimX, 0f);
            bb.SetValue(MobaBTreeKeys.CastAimY, 0f);
            bb.SetValue(MobaBTreeKeys.CastAimZ, 0f);
            bb.SetValue(MobaBTreeKeys.CastDirectionX, 0f);
            bb.SetValue(MobaBTreeKeys.CastDirectionY, 0f);
            bb.SetValue(MobaBTreeKeys.CastDirectionZ, 0f);
        }

        private static void ValidateDeclaredKeys(Blackboard bb)
        {
            var names = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < bb.Values.Count; i++)
            {
                var value = bb.Values[i]
                    ?? throw new InvalidOperationException($"MOBA behavior-tree blackboard value at index {i} is null.");
                if (string.IsNullOrWhiteSpace(value.Name))
                    throw new InvalidOperationException($"MOBA behavior-tree blackboard value at index {i} has an empty name.");
                if (!names.Add(value.Name))
                    throw new InvalidOperationException($"MOBA behavior-tree blackboard key '{value.Name}' is duplicated.");
            }
        }

        private static void Ensure<T>(Blackboard bb, string key)
        {
            var existing = bb.Values.Find(value => string.Equals(value.Name, key, StringComparison.Ordinal));
            if (existing == null)
            {
                bb.Values.Add(new BTCore.Runtime.Blackboards.BlackboardValue<T>(key));
                return;
            }

            if (existing.Type != typeof(T))
            {
                throw new InvalidOperationException(
                    $"MOBA behavior-tree blackboard key '{key}' must be '{typeof(T).FullName}', but was '{existing.Type?.FullName ?? "<null>"}'.");
            }
        }
    }
}
