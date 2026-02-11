using System;
using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Share.Common.Pool;
using AbilityKit.Ability.Share.Math;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services;

namespace AbilityKit.Ability.Share.Common.Projectile
{
    public sealed class ProjectileService : IProjectileService, IWorldInitializable
    {
        private static readonly ObjectPool<List<ProjectileSpawnParams>> s_spawnListPool = Pools.GetPool(
            key: "ProjectileSpawnParamsList",
            createFunc: () => new List<ProjectileSpawnParams>(16),
            onRelease: list => list.Clear(),
            defaultCapacity: 16,
            maxSize: 256,
            collectionCheck: false);

        private readonly ProjectileWorld _world;
        private readonly AreaWorld _areas;

        private readonly List<ProjectileSpawnEvent> _spawnEvents = new List<ProjectileSpawnEvent>(32);
        private readonly List<ProjectileHitEvent> _hitEvents = new List<ProjectileHitEvent>(32);
        private readonly List<ProjectileExitEvent> _exitEvents = new List<ProjectileExitEvent>(32);
        private readonly List<ProjectileTickEvent> _tickEvents = new List<ProjectileTickEvent>(32);

        private readonly List<AreaSpawnEvent> _areaSpawnEvents = new List<AreaSpawnEvent>(16);
        private readonly List<AreaEnterEvent> _areaEnterEvents = new List<AreaEnterEvent>(32);
        private readonly List<AreaStayEvent> _areaStayEvents = new List<AreaStayEvent>(32);
        private readonly List<AreaExitEvent> _areaExitEvents = new List<AreaExitEvent>(32);
        private readonly List<AreaExpireEvent> _areaExpireEvents = new List<AreaExpireEvent>(16);

        private readonly List<ScheduledEmit> _schedules = new List<ScheduledEmit>(16);
        private int _nextScheduleId = 1;

        public ProjectileService(ICollisionService collisions)
        {
            if (collisions == null) throw new ArgumentNullException(nameof(collisions));
            _world = new ProjectileWorld(collisions.World);
            _areas = new AreaWorld(collisions.World);
        }

        public void OnInit(IWorldResolver services)
        {
            if (services == null) return;
            if (services.TryResolve<IProjectileReturnTargetProvider>(out var provider) && provider != null)
            {
                _world.SetReturnTargetProvider(provider);
            }
        }

        public int ActiveCount => _world.ActiveCount;

        public ProjectileId Spawn(in ProjectileSpawnParams p)
        {
            // Spawn event is a lifecycle hook. Frame is unknown here; producer should prefer ScheduleEmit
            // when exact frame is important. Use 0 as default.
            var id = _world.Spawn(in p);
            _spawnEvents.Add(new ProjectileSpawnEvent(id, p.OwnerId, p.TemplateId, p.LauncherActorId, p.RootActorId, frame: 0, p.Position, p.Direction));
            return id;
        }

        public bool Despawn(ProjectileId id) => _world.Despawn(id);

        public void Tick(int frame, float fixedDeltaSeconds)
        {
            TickSchedules(frame);
            _areas.Tick(frame, _areaEnterEvents, _areaStayEvents, _areaExitEvents, _areaExpireEvents);
            _world.Tick(frame, fixedDeltaSeconds, _hitEvents, _exitEvents, _tickEvents);
        }

        public AreaId SpawnArea(in AreaSpawnParams p, int frame)
        {
            return _areas.Spawn(in p, frame, _areaSpawnEvents);
        }

        public bool DespawnArea(AreaId id, int frame)
        {
            return _areas.Despawn(id, frame, _areaExpireEvents);
        }

        public void DrainSpawnEvents(List<ProjectileSpawnEvent> results)
        {
            if (results == null) return;
            results.AddRange(_spawnEvents);
            _spawnEvents.Clear();
        }

        public void PeekSpawnEvents(List<ProjectileSpawnEvent> results)
        {
            if (results == null) return;
            results.AddRange(_spawnEvents);
        }

        public ProjectileScheduleId ScheduleEmit(IProjectileSpawnPattern pattern, in ProjectileSpawnParams baseSpawn, in ProjectileScheduleParams schedule)
        {
            if (pattern == null) throw new ArgumentNullException(nameof(pattern));
            var id = new ProjectileScheduleId(_nextScheduleId++);

            var interval = schedule.IntervalFrames;
            if (interval < 0) interval = 0;

            _schedules.Add(new ScheduledEmit(
                id: id,
                pattern: pattern,
                baseSpawn: baseSpawn,
                nextFrame: schedule.StartFrame,
                intervalFrames: interval,
                remaining: schedule.Count));

            return id;
        }

        public bool CancelSchedule(ProjectileScheduleId id)
        {
            for (int i = 0; i < _schedules.Count; i++)
            {
                if (_schedules[i].Id.Value != id.Value) continue;
                RemoveScheduleAtSwapBack(i);
                return true;
            }
            return false;
        }

        private void TickSchedules(int frame)
        {
            if (_schedules.Count == 0) return;

            for (int i = 0; i < _schedules.Count; i++)
            {
                var s = _schedules[i];
                if (frame < s.NextFrame) continue;

                var list = s_spawnListPool.Get();
                try
                {
                    s.Pattern.Build(in s.BaseSpawn, list);
                    for (int j = 0; j < list.Count; j++)
                    {
                        var spawn = list[j].WithSpawnFrame(frame);
                        var id = _world.Spawn(in spawn);
                        _spawnEvents.Add(new ProjectileSpawnEvent(id, spawn.OwnerId, spawn.TemplateId, spawn.LauncherActorId, spawn.RootActorId, frame, spawn.Position, spawn.Direction));
                    }
                }
                finally
                {
                    s_spawnListPool.Release(list);
                }

                // Update schedule state.
                if (s.Remaining > 0) s.Remaining--;

                if (s.Remaining == 0)
                {
                    RemoveScheduleAtSwapBack(i);
                    i--;
                    continue;
                }

                var interval = s.IntervalFrames;
                if (interval <= 0) interval = 1;
                s.NextFrame = frame + interval;
                _schedules[i] = s;
            }
        }

        private void RemoveScheduleAtSwapBack(int index)
        {
            var last = _schedules.Count - 1;
            if (index != last)
            {
                _schedules[index] = _schedules[last];
            }
            _schedules.RemoveAt(last);
        }

        private struct ScheduledEmit
        {
            public ProjectileScheduleId Id;
            public IProjectileSpawnPattern Pattern;
            public ProjectileSpawnParams BaseSpawn;
            public int NextFrame;
            public int IntervalFrames;
            public int Remaining;

            public ScheduledEmit(ProjectileScheduleId id, IProjectileSpawnPattern pattern, in ProjectileSpawnParams baseSpawn, int nextFrame, int intervalFrames, int remaining)
            {
                Id = id;
                Pattern = pattern;
                BaseSpawn = baseSpawn;
                NextFrame = nextFrame;
                IntervalFrames = intervalFrames;
                Remaining = remaining;
            }
        }

        public void DrainHitEvents(List<ProjectileHitEvent> results)
        {
            if (results == null) return;
            results.AddRange(_hitEvents);
            _hitEvents.Clear();
        }

        public void PeekHitEvents(List<ProjectileHitEvent> results)
        {
            if (results == null) return;
            results.AddRange(_hitEvents);
        }

        public void DrainExitEvents(List<ProjectileExitEvent> results)
        {
            if (results == null) return;
            results.AddRange(_exitEvents);
            _exitEvents.Clear();
        }

        public void PeekExitEvents(List<ProjectileExitEvent> results)
        {
            if (results == null) return;
            results.AddRange(_exitEvents);
        }

        public void DrainTickEvents(List<ProjectileTickEvent> results)
        {
            if (results == null) return;
            results.AddRange(_tickEvents);
            _tickEvents.Clear();
        }

        public void PeekTickEvents(List<ProjectileTickEvent> results)
        {
            if (results == null) return;
            results.AddRange(_tickEvents);
        }

        public void DrainAreaSpawnEvents(List<AreaSpawnEvent> results)
        {
            if (results == null) return;
            results.AddRange(_areaSpawnEvents);
            _areaSpawnEvents.Clear();
        }

        public void PeekAreaSpawnEvents(List<AreaSpawnEvent> results)
        {
            if (results == null) return;
            results.AddRange(_areaSpawnEvents);
        }

        public void DrainAreaEnterEvents(List<AreaEnterEvent> results)
        {
            if (results == null) return;
            results.AddRange(_areaEnterEvents);
            _areaEnterEvents.Clear();
        }

        public void PeekAreaEnterEvents(List<AreaEnterEvent> results)
        {
            if (results == null) return;
            results.AddRange(_areaEnterEvents);
        }

        public void DrainAreaStayEvents(List<AreaStayEvent> results)
        {
            if (results == null) return;
            results.AddRange(_areaStayEvents);
            _areaStayEvents.Clear();
        }

        public void PeekAreaStayEvents(List<AreaStayEvent> results)
        {
            if (results == null) return;
            results.AddRange(_areaStayEvents);
        }

        public void DrainAreaExitEvents(List<AreaExitEvent> results)
        {
            if (results == null) return;
            results.AddRange(_areaExitEvents);
            _areaExitEvents.Clear();
        }

        public void PeekAreaExitEvents(List<AreaExitEvent> results)
        {
            if (results == null) return;
            results.AddRange(_areaExitEvents);
        }

        public void DrainAreaExpireEvents(List<AreaExpireEvent> results)
        {
            if (results == null) return;
            results.AddRange(_areaExpireEvents);
            _areaExpireEvents.Clear();
        }

        public void PeekAreaExpireEvents(List<AreaExpireEvent> results)
        {
            if (results == null) return;
            results.AddRange(_areaExpireEvents);
        }

        public byte[] ExportRollback(FrameIndex frame)
        {
            return _world.ExportRollback(frame);
        }

        public void ImportRollback(FrameIndex frame, byte[] payload)
        {
            _world.ImportRollback(frame, payload);

            // Rollback restore should also clear pending transient events.
            _spawnEvents.Clear();
            _hitEvents.Clear();
            _exitEvents.Clear();
            _tickEvents.Clear();

            _areaSpawnEvents.Clear();
            _areaEnterEvents.Clear();
            _areaStayEvents.Clear();
            _areaExitEvents.Clear();
            _areaExpireEvents.Clear();

            // Scheduled emissions are transient controller state; clear on rollback restore.
            _schedules.Clear();
        }

        public void Dispose()
        {
            _spawnEvents.Clear();
            _hitEvents.Clear();
            _exitEvents.Clear();
            _tickEvents.Clear();

            _areaSpawnEvents.Clear();
            _areaEnterEvents.Clear();
            _areaStayEvents.Clear();
            _areaExitEvents.Clear();
            _areaExpireEvents.Clear();

            _schedules.Clear();
        }
    }
}
