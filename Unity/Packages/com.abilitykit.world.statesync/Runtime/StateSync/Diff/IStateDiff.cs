namespace AbilityKit.Ability.StateSync
{
    /// <summary>
    /// 框架差分兼容记录，其帧和长度元数据不是对权威基线的证明。
    /// </summary>
    public interface IStateDiff
    {
        /// <summary>
        /// 返回差分来源帧元数据；默认提供器输出为 0。
        /// </summary>
        int FromFrame { get; }
        /// <summary>
        /// 返回差分目标帧元数据；默认提供器输出为 0。
        /// </summary>
        int ToFrame { get; }
        /// <summary>
        /// 返回差分时间元数据；默认提供器使用创建时的 UTC Unix 毫秒。
        /// </summary>
        long Timestamp { get; }
        /// <summary>
        /// 返回供成对提供器应用的兼容数据；是否复制和压缩由实现决定。
        /// </summary>
        byte[] CompressedData { get; }
        /// <summary>
        /// 返回未压缩目标状态大小，以字节计；不一定等于未压缩增量大小。
        /// </summary>
        int UncompressedSize { get; }
        /// <summary>
        /// 标识应用时是否可忽略基线；不保证该状态覆盖完整业务世界。
        /// </summary>
        bool IsFullSnapshot { get; }
    }
}
