using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.World.Services;

namespace AbilityKit.Core.Common.Projectile
{
    public interface IProjectileService : IService
    {
        int ActiveCount { get; }

        ProjectileId Spawn(in ProjectileSpawnParams p);
        bool Despawn(ProjectileId id);

        void Tick(int frame, float fixedDeltaSeconds);

        void DrainSpawnEvents(List<ProjectileSpawnEvent> results);
        void DrainHitEvents(List<ProjectileHitEvent> results);
        void DrainExitEvents(List<ProjectileExitEvent> results);
        void DrainTickEvents(List<ProjectileTickEvent> results);

        byte[] ExportRollback(FrameIndex frame);
        void ImportRollback(FrameIndex frame, byte[] payload);

        ProjectileScheduleId ScheduleEmit(IProjectileSpawnPattern pattern, in ProjectileSpawnParams baseSpawn, in ProjectileScheduleParams schedule);
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
