using System;
using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Combat.Collision;
using AbilityKit.Core.Pooling;
using AbilityKit.Core.Mathematics;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services;

namespace AbilityKit.Combat.Projectile
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

            if (services.TryResolve<IProjectileTrackingTargetProvider>(out var trackingProvider) && trackingProvider != null)
            {
                _world.SetTrackingTargetProvider(trackingProvider);
            }
        }

        public int ActiveCount => _world.ActiveCount;

        public ProjectileId Spawn(in ProjectileSpawnParams p)
        {
            // Spawn 事件是生命周期钩子。这里无法得知精确帧；如果帧号很重要，生产者应优先使用 ScheduleEmit。
            // 默认使用 0。
            var id = _world.Spawn(in p);
            _spawnEvents.Add(new ProjectileSpawnEvent(id, p.OwnerId, p.TemplateId, p.LauncherActorId, p.RootActorId, frame: 0, p.Position, p.Direction));
            return id;
        }

        public bool Despawn(ProjectileId id) => _world.Despawn(id);

        public bool TryGetRuntimeState(ProjectileId id, out ProjectileRuntimeState state) =>
            _world.TryGetRuntimeState(id, out state);

        public bool TrySetPosition(ProjectileId id, in Vec3 position) =>
            _world.TrySetPosition(id, in position);

        public bool ResumeSimulation(ProjectileId id) => _world.ResumeSimulation(id);

        public bool Despawn(ProjectileId id, int frame, ProjectileExitReason reason)
        {
            if (!_world.Despawn(id, frame, reason, out var exitEvent))
            {
                return false;
            }

            _exitEvents.Add(exitEvent);
            return true;
        }

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
            return ScheduleEmit(pattern, patternProvider: null, in baseSpawn, in schedule);
        }

        public ProjectileScheduleId ScheduleEmit(IProjectileSpawnPatternProvider patternProvider, in ProjectileSpawnParams baseSpawn, in ProjectileScheduleParams schedule)
        {
            if (patternProvider == null) throw new ArgumentNullException(nameof(patternProvider));
            return ScheduleEmit(pattern: null, patternProvider: patternProvider, in baseSpawn, in schedule);
        }

        private ProjectileScheduleId ScheduleEmit(IProjectileSpawnPattern pattern, IProjectileSpawnPatternProvider patternProvider, in ProjectileSpawnParams baseSpawn, in ProjectileScheduleParams schedule)
        {
            var id = new ProjectileScheduleId(_nextScheduleId++);

            var interval = schedule.IntervalFrames;
            if (interval < 0) interval = 0;

            _schedules.Add(new ScheduledEmit(
                id: id,
                pattern: pattern,
                patternProvider: patternProvider,
                baseSpawn: baseSpawn,
                nextFrame: schedule.StartFrame,
                intervalFrames: interval,
                remaining: schedule.Count));

            return id;
        }

        public bool HasSchedule(ProjectileScheduleId id)
        {
            for (int i = 0; i < _schedules.Count; i++)
            {
                if (_schedules[i].Id.Value == id.Value) return true;
            }

            return false;
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

                var pattern = s.PatternProvider != null
                    ? s.PatternProvider.GetPattern(in s.BaseSpawn, frame)
                    : s.Pattern;
                if (pattern == null) continue;

                var list = s_spawnListPool.Get();
                try
                {
                    pattern.Build(in s.BaseSpawn, list);
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
            public IProjectileSpawnPatternProvider PatternProvider;
            public ProjectileSpawnParams BaseSpawn;
            public int NextFrame;
            public int IntervalFrames;
            public int Remaining;

            public ScheduledEmit(ProjectileScheduleId id, IProjectileSpawnPattern pattern, IProjectileSpawnPatternProvider patternProvider, in ProjectileSpawnParams baseSpawn, int nextFrame, int intervalFrames, int remaining)
            {
                Id = id;
                Pattern = pattern;
                PatternProvider = patternProvider;
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

            // 回滚恢复时也应清理待处理的瞬态事件。
            _spawnEvents.Clear();
            _hitEvents.Clear();
            _exitEvents.Clear();
            _tickEvents.Clear();

            _areaSpawnEvents.Clear();
            _areaEnterEvents.Clear();
            _areaStayEvents.Clear();
            _areaExitEvents.Clear();
            _areaExpireEvents.Clear();

            // 定时发射属于瞬态控制器状态，回滚恢复时清理。
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
