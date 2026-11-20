using System;
using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.FrameSync.Rollback;
using MemoryPack;

namespace AbilityKit.Combat.Projectile
{
    /// <summary>
    /// 将投射物、区域物理状态及未来发射任务接入帧回滚，并在导入前校验载荷和必要依赖。
    /// </summary>
    public sealed class ProjectileRollbackProvider : IRollbackStateProvider, IRollbackStatePreflightProvider
    {
        /// <summary>
        /// 正式投射物服务状态 Provider 的稳定注册键。
        /// </summary>
        public const int DefaultKey = 12001;

        private readonly IProjectileService _projectiles;

        /// <summary>
        /// 关联世界所属的投射物服务，不接管服务的帧驱动或释放职责。
        /// </summary>
        public ProjectileRollbackProvider(IProjectileService projectiles)
        {
            _projectiles = projectiles ?? throw new ArgumentNullException(nameof(projectiles));
        }

        /// <summary>
        /// 返回投射物服务状态 Provider 注册键。
        /// </summary>
        public int Key => DefaultKey;

        /// <summary>
        /// 导出给定帧的物理与未来任务状态；帧末结算资格由世界既有门禁保证。
        /// </summary>
        public byte[] Export(FrameIndex frame)
        {
            return _projectiles.ExportRollback(frame);
        }

        /// <summary>
        /// 委托服务静默导入帧状态；协调器应在此之前完成所有 Provider 的预检。
        /// </summary>
        public void Import(FrameIndex frame, byte[] payload)
        {
            _projectiles.ImportRollback(frame, payload);
        }

        /// <summary>
        /// 在任何 Provider 导入前校验完整物理、任务载荷及正式服务的可用依赖。
        /// 不调用自定义策略工厂、过滤器解析器或业务通知。
        /// </summary>
        public void ValidateImport(FrameIndex frame, byte[] payload)
        {
            if (_projectiles is IRollbackStatePreflightProvider preflight)
                preflight.ValidateImport(frame, payload);
            else
                ProjectileService.ReadRollbackSnapshot(frame, payload);
        }

        /// <summary>
        /// 只读解析完整载荷后返回活动发射任务摘要，保持服务中的确定性处理顺序。
        /// </summary>
        public static ProjectileScheduleRollbackInfo[] ReadScheduleSnapshot(byte[] payload, FrameIndex frame)
        {
            var service = ProjectileService.ReadRollbackSnapshot(frame, payload);
            var result = new ProjectileScheduleRollbackInfo[service.Schedules.Length];
            for (var i = 0; i < result.Length; i++) result[i] = new ProjectileScheduleRollbackInfo(in service.Schedules[i]);
            return result;
        }

        /// <summary>
        /// 只读校验完整服务载荷并返回目标帧的弹体物理状态，供关系 Provider 核对归属与数量。
        /// 不写入世界，不调用策略工厂、过滤器解析器或历史通知。
        /// </summary>
        public static ProjectileWorldSnapshotPayload ReadProjectileSnapshot(byte[] payload, FrameIndex frame)
        {
            var service = ProjectileService.ReadRollbackSnapshot(frame, payload);
            return ProjectileWorld.ReadRollbackSnapshot(service.Projectiles, frame);
        }

        /// <summary>
        /// 在隔离数据中读取目标帧的区域名单，供跨 provider 预检核对生命周期归属。
        /// 不修改物理世界，也不产生区域通知；沿用区域恢复支持的快照版本。
        /// </summary>
        public static AreaWorldSnapshot ReadAreaSnapshot(byte[] payload, FrameIndex frame)
        {
            if (payload == null || payload.Length == 0)
                throw new InvalidOperationException("Missing projectile rollback payload.");
            var service = ProjectileService.ReadRollbackSnapshot(frame, payload);
            var areas = AreaWorld.ReadRollbackSnapshot(service.Areas, frame.Value);
            if (areas.Items == null)
                throw new InvalidOperationException("Missing Area world rollback items.");
            var ids = new HashSet<int>();
            foreach (var item in areas.Items)
                if (item.Id <= 0 || item.Id >= areas.NextId || item.OwnerId <= 0 || !ids.Add(item.Id))
                    throw new InvalidOperationException("Invalid or duplicate physical Area identity or owner.");
            return areas;
        }

        /// <summary>
        /// 隔离读取目标帧的投射物身份，供关系 Provider 在任何导入前核对快照。
        /// 不修改物理世界，也不调用策略构造或过滤器解析。
        /// </summary>
        public static HashSet<int> ReadProjectileIds(byte[] payload)
        {
            if (payload == null || payload.Length == 0)
                throw new InvalidOperationException("Missing projectile rollback payload.");

            var service = MemoryPackSerializer.Deserialize<ProjectileServiceRollbackPayload>(payload);
            if (service.Version != 2 || service.Projectiles == null || service.Projectiles.Length == 0)
                throw new InvalidOperationException("Unsupported projectile service rollback payload.");

            var world = ProjectileWorld.ReadRollbackSnapshot(service.Projectiles);
            ProjectileService.ReadRollbackSnapshot(world.Frame, payload);
            if (world.Items == null)
                throw new InvalidOperationException("Unsupported projectile world rollback payload.");

            var ids = new HashSet<int>();
            foreach (var item in world.Items)
            {
                if (item.Id <= 0 || !ids.Add(item.Id))
                    throw new InvalidOperationException("Projectile rollback payload contains a duplicate or invalid projectile identity.");
            }
            return ids;
        }
    }
}
