using System;
using Newtonsoft.Json;
using AbilityKit.Combat.Projectile;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.Projectile;

namespace AbilityKit.Demo.Moba.Services.Projectile.Launch
{
    /// <summary>
    /// 发射 Continuous 的纯数据检查点，不持有实体、序列或委托引用。
    /// </summary>
    internal sealed class MobaProjectileLaunchContinuousRollbackState
    {
        [JsonProperty(Required = Required.Always)] internal int CasterActorId;
        [JsonProperty(Required = Required.Always)] internal int LauncherId;
        [JsonProperty(Required = Required.Always)] internal int ProjectileId;
        [JsonProperty(Required = Required.Always)] internal int CountPerShot;
        [JsonProperty(Required = Required.Always)] internal float FanAngleDeg;
        [JsonProperty(Required = Required.Always)] internal int DurationMs;
        [JsonProperty(Required = Required.Always)] internal int ContinuousProcessId;
        [JsonProperty(Required = Required.Always)] internal bool TrackTarget;
        [JsonProperty(Required = Required.Always)] internal float SpawnX, SpawnY, SpawnZ;
        [JsonProperty(Required = Required.Always)] internal float DirectionX, DirectionY, DirectionZ;
        [JsonProperty(Required = Required.Always)] internal float MaxDistance;
        [JsonProperty(Required = Required.Always)] internal int SourceActorId, InitialTargetActorId, SourceProjectileConfigId;
        [JsonProperty(Required = Required.Always)] internal long SourceContextId, RootContextId, OwnerContextId;
        [JsonProperty(Required = Required.Always)] internal long SkillRuntimeId, SkillRuntimeRootContextId;
        [JsonProperty(Required = Required.Always)] internal int SkillRuntimeGeneration;
        [JsonProperty(Required = Required.Always)] internal int OriginSourceActorId, OriginTargetActorId, OriginConfigId;
        [JsonProperty(Required = Required.Always)] internal int OriginKind;
        [JsonProperty(Required = Required.Always)] internal long OriginImmediateContextId, OriginParentContextId, OriginRootContextId, OriginOwnerContextId;
        [JsonProperty(Required = Required.Always)] internal long OriginSkillRuntimeId, OriginSkillRuntimeRootContextId;
        [JsonProperty(Required = Required.Always)] internal int OriginSkillRuntimeGeneration;
        [JsonProperty(Required = Required.Always)] internal int LauncherActorId, ScheduleId, IntervalFrames, TotalCount, StartFrame;
        [JsonProperty(Required = Required.Always)] internal long EndTimeMs;
        [JsonProperty(Required = Required.Always)] internal int ModifierSourceId;
        [JsonProperty(Required = Required.Always)] internal bool Success;
        [JsonProperty(Required = Required.Always)] internal string SequenceType;

        internal static MobaProjectileLaunchContinuousRollbackState Capture(MobaProjectileLaunchContinuous runtime)
        {
            var request = runtime.Request;
            var source = request.SourceContext;
            source.TryGetOrigin(out var origin);
            var result = runtime.Result;
            var state = new MobaProjectileLaunchContinuousRollbackState
            {
                CasterActorId = request.CasterActorId, LauncherId = request.LauncherId, ProjectileId = request.ProjectileId,
                CountPerShot = request.CountPerShot, FanAngleDeg = request.FanAngleDeg, DurationMs = request.DurationMs,
                ContinuousProcessId = request.ContinuousProcessId, TrackTarget = request.TrackTarget,
                SpawnX = request.SpawnPosition.X, SpawnY = request.SpawnPosition.Y, SpawnZ = request.SpawnPosition.Z,
                DirectionX = request.Direction.X, DirectionY = request.Direction.Y, DirectionZ = request.Direction.Z,
                MaxDistance = request.MaxDistance,
                SourceActorId = source.SourceActorId, InitialTargetActorId = source.InitialTargetActorId,
                SourceProjectileConfigId = source.ProjectileConfigId, SourceContextId = source.SourceContextId,
                RootContextId = source.RootContextId, OwnerContextId = source.OwnerContextId,
                SkillRuntimeId = source.SkillRuntimeHandle.RuntimeId,
                SkillRuntimeGeneration = source.SkillRuntimeHandle.Generation,
                SkillRuntimeRootContextId = source.SkillRuntimeHandle.RootContextId,
                OriginSourceActorId = origin.SourceActorId, OriginTargetActorId = origin.TargetActorId,
                OriginConfigId = origin.ImmediateConfigId, OriginKind = (int)origin.ImmediateKind,
                OriginImmediateContextId = origin.ImmediateContextId, OriginParentContextId = origin.ParentContextId,
                OriginRootContextId = origin.RootContextId, OriginOwnerContextId = origin.OwnerContextId,
                OriginSkillRuntimeId = origin.SkillRuntimeHandle.RuntimeId,
                OriginSkillRuntimeGeneration = origin.SkillRuntimeHandle.Generation,
                OriginSkillRuntimeRootContextId = origin.SkillRuntimeHandle.RootContextId,
                LauncherActorId = result.LauncherActorId, ScheduleId = result.ScheduleId.Value,
                IntervalFrames = result.IntervalFrames, TotalCount = result.TotalCount,
                StartFrame = result.StartFrame, EndTimeMs = result.EndTimeMs,
                ModifierSourceId = runtime.AllocatedModifierSourceId,
                Success = result.Success,
                SequenceType = result.Sequence?.GetType().FullName ?? string.Empty,
            };
            return state;
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        internal bool TryCreateRequest(MobaConfigDatabase configs, out MobaProjectileLaunchRequest request)
        {
            request = default;
            if (configs == null || !Success || SequenceType != typeof(RepeatProjectileLaunchSequence).FullName ||
                CasterActorId <= 0 || LauncherId <= 0 || ProjectileId <= 0 || CountPerShot <= 0 || FanAngleDeg < 0 ||
                DurationMs < 0 || ContinuousProcessId < 0 || SourceActorId <= 0 || SourceContextId <= 0 ||
                InitialTargetActorId < 0 || LauncherActorId <= 0 || ScheduleId <= 0 || IntervalFrames < 0 ||
                TotalCount <= 0 || StartFrame < 0 || EndTimeMs < 0 || ModifierSourceId < 0 ||
                !Finite(SpawnX) || !Finite(SpawnY) || !Finite(SpawnZ) ||
                !Finite(DirectionX) || !Finite(DirectionY) || !Finite(DirectionZ) ||
                !Finite(FanAngleDeg) || !Finite(MaxDistance) ||
                OriginKind < 0 || OriginKind > byte.MaxValue || !Enum.IsDefined(typeof(MobaExecutionKind), (MobaExecutionKind)OriginKind) ||
                !configs.TryGetProjectileLauncher(LauncherId, out var launcher) ||
                !configs.TryGetProjectile(ProjectileId, out var projectile)) return false;
            var handle = new MobaSkillCastRuntimeHandle(SkillRuntimeId, SkillRuntimeGeneration, SkillRuntimeRootContextId);
            var originHandle = new MobaSkillCastRuntimeHandle(OriginSkillRuntimeId, OriginSkillRuntimeGeneration, OriginSkillRuntimeRootContextId);
            var origin = new MobaGameplayOrigin(OriginSourceActorId, OriginTargetActorId,
                (MobaExecutionKind)OriginKind, OriginConfigId, OriginImmediateContextId, OriginParentContextId,
                OriginRootContextId, OriginOwnerContextId, originHandle);
            var source = new ProjectileSourceContext(SourceActorId, InitialTargetActorId, SourceProjectileConfigId,
                SourceContextId, RootContextId, OwnerContextId, in handle, in origin);
            var spawn = new Vec3(SpawnX, SpawnY, SpawnZ);
            var direction = new Vec3(DirectionX, DirectionY, DirectionZ);
            request = new MobaProjectileLaunchRequest(CasterActorId, launcher, projectile, CountPerShot, FanAngleDeg,
                DurationMs, ContinuousProcessId, TrackTarget, in spawn, in direction, in source, MaxDistance);
            return true;
        }
    }

}
