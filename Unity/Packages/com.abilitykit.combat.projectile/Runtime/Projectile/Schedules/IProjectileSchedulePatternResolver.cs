using MemoryPack;

namespace AbilityKit.Combat.Projectile
{
    /// <summary>
    /// 发射策略的纯数据描述；种类 1 至 4 为内置模式，扩展种类从 1024 开始。
    /// Data 由对应策略维护版本，不包含服务、随机流或委托引用。
    /// </summary>
    [MemoryPackable]
    public readonly partial struct ProjectileSchedulePatternSnapshot
    {
        /// <summary>
        /// 返回稳定策略种类。
        /// </summary>
        public readonly int Kind;

        /// <summary>
        /// 返回策略拥有的版本化数据。
        /// </summary>
        public readonly byte[] Data;

        /// <summary>
        /// 创建策略数据描述。
        /// </summary>
        public ProjectileSchedulePatternSnapshot(int kind, byte[] data)
        { Kind = kind; Data = data; }
    }

    /// <summary>
    /// 为自定义静态模式或动态提供者导出未来发射所需的描述。
    /// 捕获必须只读，不能执行 Build、GetPattern 或消耗随机数。
    /// </summary>
    public interface IProjectileSchedulePatternSnapshotSource
    {
        /// <summary>
        /// 捕获当前策略的数据描述。
        /// </summary>
        ProjectileSchedulePatternSnapshot CaptureSchedulePattern();
    }

    /// <summary>
    /// 在原世界中校验策略描述并静默重绑依赖，不拥有业务推进或随机状态。
    /// 只有捕获或恢复自定义活动任务时需要此扩展，普通发射不依赖它。
    /// </summary>
    public interface IProjectileSchedulePatternResolver
    {
        /// <summary>
        /// 只读校验描述及必要依赖；不得构造执行策略或取样业务数据。
        /// </summary>
        void Validate(in ProjectileSchedulePatternSnapshot snapshot, bool isProvider, in ProjectileSpawnParams baseSpawn);

        /// <summary>
        /// 恢复冻结模式，不执行发射或历史通知。
        /// </summary>
        IProjectileSpawnPattern RestorePattern(in ProjectileSchedulePatternSnapshot snapshot);

        /// <summary>
        /// 恢复动态提供者，后续正常发射时才重新取样。
        /// </summary>
        IProjectileSpawnPatternProvider RestoreProvider(in ProjectileSchedulePatternSnapshot snapshot);
    }
}
