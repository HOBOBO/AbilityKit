#nullable enable annotations

using System;
using System.IO;
using System.Threading;

namespace AbilityKit.Core.Recording.FrameRecord
{
    /// <summary>
    /// 提供默认帧记录格式分派及进程级编解码器覆盖入口。
    /// </summary>
    public static class FrameRecordCodecs
    {
        private static readonly IFrameRecordCodec Default = new DefaultFrameRecordCodec();
        private static IFrameRecordCodec? s_override;

        /// <summary>
        /// 获取或替换进程级编解码器。未安装覆盖项或设置为空时，使用内置默认分派。
        /// </summary>
        /// <remarks>
        /// 默认只按扩展名分派：不区分大小写的 .bin 使用优化二进制，其余使用 JSON。
        /// 在应用装配时选择覆盖项；运行期切换不会转换已有文件或影响已创建的写入器。
        /// </remarks>
        [System.Diagnostics.CodeAnalysis.AllowNull]
        public static IFrameRecordCodec Current
        {
            get => Volatile.Read(ref s_override) ?? Default;
            set => Volatile.Write(ref s_override, value);
        }

        private sealed class DefaultFrameRecordCodec : IFrameRecordCodec
        {
            private readonly FrameRecordOptimizedBinaryCodec _bin = new FrameRecordOptimizedBinaryCodec();
            private readonly FrameRecordJsonCodec _json = new FrameRecordJsonCodec();

            public IFrameRecordWriter CreateWriter(string outputPath, FrameRecordMeta meta) =>
                SelectCodec(outputPath).CreateWriter(outputPath, meta);

            public FrameRecordFile Load(string path) => SelectCodec(path).Load(path);

            private IFrameRecordCodec SelectCodec(string path) =>
                string.Equals(Path.GetExtension(path), ".bin", StringComparison.OrdinalIgnoreCase) ? _bin : _json;
        }
    }
}
