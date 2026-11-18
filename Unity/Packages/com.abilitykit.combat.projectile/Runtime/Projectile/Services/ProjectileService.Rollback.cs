using System;
using System.Collections.Generic;
using System.Linq;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.FrameSync.Rollback;
using MemoryPack;

namespace AbilityKit.Combat.Projectile
{
    /// <summary>
    /// 通过可选回滚入口恢复服务拥有的物理状态与未来调度状态。
    /// </summary>
    public sealed partial class ProjectileService
    {
        private IProjectileSchedulePatternResolver _schedulePatternResolver;

        /// <summary>
        /// 导出完整物理及未来发射状态；不执行策略取样，不复制世界随机流。
        /// 活动自定义策略缺少数据描述或解析依赖时明确拒绝捕获。
        /// </summary>
        public byte[] ExportRollback(FrameIndex frame)
        {
            var filters = new List<ProjectileScheduleFilterSnapshot>();
            var filterInstances = new List<IProjectileHitFilter>();
            var schedules = new ProjectileScheduleSnapshot[_schedules.Count];
            for (var i = 0; i < schedules.Length; i++)
            {
                var s = _schedules[i];
                if (s.BaseSpawn.HitPolicy != null)
                    throw new InvalidOperationException("Scheduled explicit hit policy instances have no rollback descriptor; use kind/param.");
                var pattern = ProjectileSchedulePatternCodec.Capture((object)s.PatternProvider ?? s.Pattern);
                ValidatePattern(in pattern, s.PatternProvider != null, in s.BaseSpawn);
                var filterIndex = -1;
                var filter = s.BaseSpawn.HitFilter;
                if (filter != null && !ReferenceEquals(filter, DefaultProjectileHitFilter.Instance) || filter == null && s.BaseSpawn.HitFilterKey > 0)
                {
                    var key = s.BaseSpawn.HitFilterKey;
                    if (key == 0) key = _world.FindRollbackHitFilterKey(filter);
                    if (filter == null && key > 0) filter = _world.FindRollbackHitFilter(key);
                    for (var j = 0; j < filters.Count; j++)
                        if (key > 0 && filters[j].Key == key || filter != null && ReferenceEquals(filterInstances[j], filter))
                        { filterIndex = j; break; }
                    if (filterIndex < 0)
                    {
                        var data = (filter as IProjectileHitFilterSnapshotSource)?.CaptureSnapshot();
                        _world.ValidateScheduleHitFilter(key, data);
                        filterIndex = filters.Count;
                        filters.Add(new ProjectileScheduleFilterSnapshot { Key = key, Data = data });
                        filterInstances.Add(filter);
                    }
                }
                schedules[i] = new ProjectileScheduleSnapshot {
                    Id = s.Id.Value, NextFrame = s.NextFrame, IntervalFrames = s.IntervalFrames,
                    Remaining = s.Remaining, IsProvider = s.PatternProvider != null, Pattern = pattern,
                    BaseSpawn = new ProjectileScheduledSpawnSnapshot(in s.BaseSpawn), FilterIndex = filterIndex
                };
            }
            var payload = MemoryPackSerializer.Serialize(new ProjectileServiceRollbackPayload(
                2, _world.ExportRollback(frame), _areas.ExportRollback(frame.Value), _nextScheduleId,
                schedules, filters.ToArray()));
            ReadRollbackSnapshot(frame, payload);
            return payload;
        }

        internal static ProjectileServiceRollbackPayload ReadRollbackSnapshot(FrameIndex frame, byte[] payload)
        {
            if (payload == null || payload.Length == 0)
                throw new InvalidOperationException("Missing projectile service rollback payload.");
            var snapshot = MemoryPackSerializer.Deserialize<ProjectileServiceRollbackPayload>(payload);
            if (snapshot.Version != 2)
                throw new InvalidOperationException($"Unsupported projectile service snapshot version {snapshot.Version}; rebuild checkpoint.");
            var physical = ProjectileWorld.ReadRollbackSnapshot(snapshot.Projectiles, frame);
            AreaWorld.ReadRollbackSnapshot(snapshot.Areas, frame.Value);
            if (snapshot.NextScheduleId <= 0 || snapshot.Schedules == null || snapshot.Filters == null)
                throw new InvalidOperationException("Missing schedule roster or allocator.");
            var ids = new HashSet<int>();
            var filterKeys = new HashSet<int>();
            foreach (var filter in snapshot.Filters)
            {
                if (filter.Key < 0 || filter.Key > 0 && !filterKeys.Add(filter.Key) ||
                    filter.Key == 0 && (filter.Data == null || filter.Data.Length == 0))
                    throw new InvalidOperationException("Invalid schedule filter descriptor.");
                foreach (var p in physical.Items)
                    if (filter.Key > 0 && p.HitFilterKey == filter.Key &&
                        !(p.HitFilterSnapshot ?? Array.Empty<byte>()).SequenceEqual(filter.Data ?? Array.Empty<byte>()))
                        throw new InvalidOperationException("Schedule and physical projectile disagree on a shared filter.");
            }
            foreach (var s in snapshot.Schedules)
            {
                if (s.Id <= 0 || s.Id >= snapshot.NextScheduleId || !ids.Add(s.Id) || s.IntervalFrames < 0 ||
                    s.FilterIndex < -1 || s.FilterIndex >= snapshot.Filters.Length)
                    throw new InvalidOperationException("Invalid schedule identity, cursor or filter reference.");
                ProjectileSchedulePatternCodec.Validate(in s.Pattern, s.IsProvider);
                s.BaseSpawn.Validate();
                ProjectileHitPolicyFactory.ValidateRegisteredKind(s.BaseSpawn.HitPolicyKind);
                if (!Enum.IsDefined(typeof(ProjectileHitIdentityPolicy), s.BaseSpawn.HitIdentityPolicy) ||
                    !Enum.IsDefined(typeof(ProjectileMaxDistanceResponse), s.BaseSpawn.MaxDistanceResponse))
                    throw new InvalidOperationException("Invalid schedule spawn policy.");
            }
            return snapshot;
        }

        private void ValidatePattern(in ProjectileSchedulePatternSnapshot pattern, bool isProvider, in ProjectileSpawnParams baseSpawn)
        {
            ProjectileSchedulePatternCodec.Validate(in pattern, isProvider);
            if (ProjectileSchedulePatternCodec.IsBuiltIn(pattern.Kind)) return;
            if (_schedulePatternResolver == null)
                throw new InvalidOperationException("Scheduled projectile strategy requires its rollback resolver.");
            _schedulePatternResolver.Validate(in pattern, isProvider, in baseSpawn);
        }

        void IRollbackStatePreflightProvider.ValidateImport(FrameIndex frame, byte[] payload)
        {
            var snapshot = ReadRollbackSnapshot(frame, payload);
            ValidateDependencies(in snapshot);
        }

        private void ValidateDependencies(in ProjectileServiceRollbackPayload snapshot)
        {
            var projectiles = ProjectileWorld.ReadRollbackSnapshot(snapshot.Projectiles);
            _world.ValidateRollbackDependencies(in projectiles);
            foreach (var s in snapshot.Schedules)
            {
                var baseSpawn = s.BaseSpawn.Restore(null, 0);
                ValidatePattern(in s.Pattern, s.IsProvider, in baseSpawn);
                _world.ValidateScheduleHitIdentity(s.BaseSpawn.HitIdentityPolicy);
            }
            foreach (var filter in snapshot.Filters)
                _world.ValidateScheduleHitFilter(filter.Key, filter.Data);
        }

        /// <summary>
        /// 静默替换物理及任务名单、顺序、游标和分配器；恢复过程不发射或重放通知。
        /// 空载荷维持清空语义；非空载荷只接受具备任务状态的版本 2。
        /// </summary>
        public void ImportRollback(FrameIndex frame, byte[] payload)
        {
            if (payload == null || payload.Length == 0)
            {
                _world.ImportRollback(frame, payload);
                _areas.ImportRollback(payload);
                _schedules.Clear();
                _nextScheduleId = 1;
            }
            else
            {
                var snapshot = ReadRollbackSnapshot(frame, payload);
                ValidateDependencies(in snapshot);
                var restored = new ScheduledEmit[snapshot.Schedules.Length];
                /*
                 * 策略先准备，禁止通过 GetPattern 或 Build 猜回任务。
                 * 外部构造失败沿既有恢复失败门禁处理，不承诺跨 Provider 事务。
                 */
                for (var i = 0; i < restored.Length; i++)
                {
                    var s = snapshot.Schedules[i];
                    var pattern = !s.IsProvider ? (ProjectileSchedulePatternCodec.IsBuiltIn(s.Pattern.Kind)
                        ? ProjectileSchedulePatternCodec.Restore(in s.Pattern)
                        : _schedulePatternResolver.RestorePattern(in s.Pattern)) : null;
                    var provider = s.IsProvider ? _schedulePatternResolver.RestoreProvider(in s.Pattern) : null;
                    if (pattern == null && provider == null)
                        throw new InvalidOperationException("Schedule pattern resolver returned no strategy.");
                    restored[i] = new ScheduledEmit(new ProjectileScheduleId(s.Id), pattern, provider,
                        default, s.NextFrame, s.IntervalFrames, s.Remaining);
                }
                _world.ImportRollback(frame, snapshot.Projectiles);
                _areas.ImportRollback(snapshot.Areas);
                var physicalKeys = new HashSet<int>();
                foreach (var p in ProjectileWorld.ReadRollbackSnapshot(snapshot.Projectiles).Items)
                    if (p.HitFilterKey > 0) physicalKeys.Add(p.HitFilterKey);
                var filters = new IProjectileHitFilter[snapshot.Filters.Length];
                for (var i = 0; i < filters.Length; i++)
                {
                    var f = snapshot.Filters[i];
                    filters[i] = _world.RestoreScheduleHitFilter(f.Key, f.Data, physicalKeys.Contains(f.Key));
                }
                for (var i = 0; i < restored.Length; i++)
                {
                    var s = snapshot.Schedules[i];
                    restored[i].BaseSpawn = s.BaseSpawn.Restore(s.FilterIndex < 0 ? null : filters[s.FilterIndex],
                        s.FilterIndex < 0 ? 0 : snapshot.Filters[s.FilterIndex].Key);
                }
                _schedules.Clear();
                _schedules.AddRange(restored);
                _nextScheduleId = snapshot.NextScheduleId;
            }
            _spawnEvents.Clear(); _hitEvents.Clear(); _exitEvents.Clear(); _arrivalEvents.Clear(); _tickEvents.Clear();
            _areaSpawnEvents.Clear(); _areaEnterEvents.Clear(); _areaStayEvents.Clear();
            _areaExitEvents.Clear(); _areaExpireEvents.Clear();
        }
    }

    /// <summary>
    /// 服务版本 2 的完整状态包络，不承载历史玩法通知。
    /// </summary>
    [MemoryPackable]
    internal partial struct ProjectileServiceRollbackPayload
    {
        public int Version;
        public byte[] Projectiles;
        public byte[] Areas;
        public int NextScheduleId;
        public ProjectileScheduleSnapshot[] Schedules;
        public ProjectileScheduleFilterSnapshot[] Filters;
        public ProjectileServiceRollbackPayload(int version, byte[] projectiles, byte[] areas)
            : this(version, projectiles, areas, 1, Array.Empty<ProjectileScheduleSnapshot>(), Array.Empty<ProjectileScheduleFilterSnapshot>()) { }

        [MemoryPackConstructor]
        public ProjectileServiceRollbackPayload(int version, byte[] projectiles, byte[] areas,
            int nextScheduleId, ProjectileScheduleSnapshot[] schedules, ProjectileScheduleFilterSnapshot[] filters)
        {
            Version = version; Projectiles = projectiles; Areas = areas; NextScheduleId = nextScheduleId;
            Schedules = schedules;
            Filters = filters;
        }
    }

    /// <summary>
    /// 保留原列表顺序的未来发射任务数据，包括策略描述、冻结参数及游标。
    /// </summary>
    [MemoryPackable]
    internal partial struct ProjectileScheduleSnapshot
    {
        public int Id;
        public int NextFrame;
        public int IntervalFrames;
        public int Remaining;
        public bool IsProvider;
        public ProjectileSchedulePatternSnapshot Pattern;
        public ProjectileScheduledSpawnSnapshot BaseSpawn;
        public int FilterIndex;
    }

    /// <summary>
    /// 任务共享的过滤器数据；尚未首次发射时可以没有已分配物理键。
    /// </summary>
    [MemoryPackable]
    internal partial struct ProjectileScheduleFilterSnapshot
    {
        public int Key;
        public byte[] Data;
    }
}
