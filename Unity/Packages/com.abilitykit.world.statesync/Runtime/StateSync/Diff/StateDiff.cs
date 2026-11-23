using System;
using System.IO;
using System.Collections.Generic;

namespace AbilityKit.Ability.StateSync.Diff
{
    /// <summary>
    /// 差分的只读元数据包装，数据数组共享引用，不提供完整性或基线校验。
    /// </summary>
    public sealed class StateDiff : IStateDiff
    {
        /// <summary>
        /// 构造时传入的来源帧；不参与默认差分应用校验。
        /// </summary>
        public int FromFrame { get; }
        /// <summary>
        /// 构造时传入的目标帧；不参与默认差分应用校验。
        /// </summary>
        public int ToFrame { get; }
        /// <summary>
        /// 构造时传入的时间元数据；默认提供器使用 UTC Unix 毫秒。
        /// </summary>
        public long Timestamp { get; }
        /// <summary>
        /// 存储并返回原始传入数组引用，不复制；调用方在使用期内不得修改。
        /// </summary>
        public byte[] CompressedData { get; }
        /// <summary>
        /// 构造时传入的未压缩目标状态大小，以字节计；不校验大小是否匹配。
        /// </summary>
        public int UncompressedSize { get; }
        /// <summary>
        /// 构造时传入的全量标记；默认提供器据此决定是否忽略基线。
        /// </summary>
        public bool IsFullSnapshot { get; }

        /// <summary>
        /// 使用参数构建记录，只拒绝空数据引用；不复制数组，不验证帧、时间或长度。
        /// </summary>
        public StateDiff(
            int fromFrame,
            int toFrame,
            long timestamp,
            byte[] compressedData,
            int uncompressedSize,
            bool isFullSnapshot)
        {
            FromFrame = fromFrame;
            ToFrame = toFrame;
            Timestamp = timestamp;
            CompressedData = compressedData ?? throw new ArgumentNullException(nameof(compressedData));
            UncompressedSize = uncompressedSize;
            IsFullSnapshot = isFullSnapshot;
        }

        /// <summary>
        /// 返回保存的数据数组长度，以字节计。
        /// </summary>
        public int CompressedSize => CompressedData?.Length ?? 0;
        /// <summary>
        /// 返回保存长度除以未压缩目标状态长度；后者非正时返回 0，结果可大于 1。
        /// </summary>
        public double CompressionRatio => UncompressedSize > 0 ? (double)CompressedSize / UncompressedSize : 0;
    }
}
