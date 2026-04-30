using System;
using System.IO;
using System.Collections.Generic;

namespace AbilityKit.Ability.StateSync.Diff
{
    public sealed class StateDiff : IStateDiff
    {
        public int FromFrame { get; }
        public int ToFrame { get; }
        public long Timestamp { get; }
        public byte[] CompressedData { get; }
        public int UncompressedSize { get; }
        public bool IsFullSnapshot { get; }

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

        public int CompressedSize => CompressedData?.Length ?? 0;
        public double CompressionRatio => UncompressedSize > 0 ? (double)CompressedSize / UncompressedSize : 0;
    }
}
