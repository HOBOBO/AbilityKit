using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.World.Services;

namespace AbilityKit.Combat.Projectile
{
    public interface IProjectileService : IService
    {
        int ActiveCount { get; }

        ProjectileId Spawn(in ProjectileSpawnParams p);
        bool Despawn(ProjectileId id);
        bool Despawn(ProjectileId id, int frame, ProjectileExitReason reason);
        bool TryGetRuntimeState(ProjectileId id, out ProjectileRuntimeState state);
        bool TrySetPosition(ProjectileId id, in AbilityKit.Core.Mathematics.Vec3 position);
        bool ResumeSimulation(ProjectileId id);

        void Tick(int frame, float fixedDeltaSeconds);

        void DrainSpawnEvents(List<ProjectileSpawnEvent> results);
        void DrainHitEvents(List<ProjectileHitEvent> results);
        void DrainExitEvents(List<ProjectileExitEvent> results);
        void DrainTickEvents(List<ProjectileTickEvent> results);
        void PeekSpawnEvents(List<ProjectileSpawnEvent> results);
        void PeekHitEvents(List<ProjectileHitEvent> results);
        void PeekExitEvents(List<ProjectileExitEvent> results);
        void PeekTickEvents(List<ProjectileTickEvent> results);

        byte[] ExportRollback(FrameIndex frame);
        void ImportRollback(FrameIndex frame, byte[] payload);

        ProjectileScheduleId ScheduleEmit(IProjectileSpawnPattern pattern, in ProjectileSpawnParams baseSpawn, in ProjectileScheduleParams schedule);
        ProjectileScheduleId ScheduleEmit(IProjectileSpawnPatternProvider patternProvider, in ProjectileSpawnParams baseSpawn, in ProjectileScheduleParams schedule);
        bool HasSchedule(ProjectileScheduleId id);
        bool CancelSchedule(ProjectileScheduleId id);

        AreaId SpawnArea(in AreaSpawnParams p, int frame);
        bool DespawnArea(AreaId id, int frame);

        void DrainAreaSpawnEvents(List<AreaSpawnEvent> results);
        void DrainAreaEnterEvents(List<AreaEnterEvent> results);
        void DrainAreaStayEvents(List<AreaStayEvent> results);
        void DrainAreaExitEvents(List<AreaExitEvent> results);
        void DrainAreaExpireEvents(List<AreaExpireEvent> results);
    }
}
