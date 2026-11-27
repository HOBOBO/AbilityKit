using System;
using System.IO;
using AbilityKit.Ability.Host;
using AbilityKit.Core.Recording.FrameRecord;

namespace AbilityKit.Game.Flow.Battle.Replay
{
    /// <summary>
    /// 为对局录制和回放绑定内置文件格式，不读取或改写进程级 codec 覆盖项。
    /// </summary>
    internal static class BattleRecordFiles
    {
        private static readonly FrameRecordOptimizedBinaryCodec Binary = new FrameRecordOptimizedBinaryCodec();
        private static readonly FrameRecordJsonCodec Json = new FrameRecordJsonCodec();

        internal static void ValidateOutput(string path, BattleRecordCoverage coverage)
        {
            if (coverage != BattleRecordCoverage.InputsAndHashes && coverage != BattleRecordCoverage.WithSnapshots)
                throw new InvalidOperationException($"Unsupported battle record coverage: {coverage}.");
            if (string.IsNullOrWhiteSpace(path))
                throw new InvalidOperationException("InputRecordOutputPath is required when run mode is Record.");
            if (IsBinary(path)) return;
            if (coverage == BattleRecordCoverage.WithSnapshots && HasExtension(path, ".json")) return;

            throw new InvalidOperationException(coverage == BattleRecordCoverage.InputsAndHashes
                ? "InputsAndHashes battle recording requires a .bin output path."
                : "WithSnapshots battle recording requires a .bin or .json output path.");
        }

        internal static IFrameRecordWriter OpenWriter(string path, BattleRecordCoverage coverage, FrameRecordMeta meta, Action<string> report)
        {
            ValidateOutput(path, coverage);
            var codec = SelectCodec(path);
            return new ScopedWriter(codec.CreateWriter(path, meta), codec.GetType().Name, path, coverage, report);
        }

        internal static FrameRecordFile Load(string path) => SelectCodec(path).Load(path);

        private static IFrameRecordCodec SelectCodec(string path) => IsBinary(path) ? (IFrameRecordCodec)Binary : Json;

        private static bool IsBinary(string path) => HasExtension(path, ".bin");

        private static bool HasExtension(string path, string extension) =>
            string.Equals(Path.GetExtension(path), extension, StringComparison.OrdinalIgnoreCase);

        private sealed class ScopedWriter : IFrameRecordWriter
        {
            private readonly IFrameRecordWriter _writer;
            private readonly string _codec;
            private readonly string _path;
            private readonly BattleRecordCoverage _coverage;
            private readonly Action<string> _report;
            private int _inputs;
            private int _hashes;
            private int _snapshots;
            private bool _disposed;

            internal ScopedWriter(IFrameRecordWriter writer, string codec, string path, BattleRecordCoverage coverage, Action<string> report)
            {
                _writer = writer;
                _codec = codec;
                _path = path;
                _coverage = coverage;
                _report = report;
            }

            public void Append(in PlayerInputCommand command)
            {
                if (_disposed) return;
                _writer.Append(in command);
                _inputs++;
            }

            public void AppendStateHash(int frame, int version, uint hash)
            {
                if (_disposed) return;
                _writer.AppendStateHash(frame, version, hash);
                _hashes++;
            }

            public void AppendSnapshot(int frame, int opCode, byte[] payload)
            {
                if (_disposed || _coverage != BattleRecordCoverage.WithSnapshots) return;
                _writer.AppendSnapshot(frame, opCode, payload);
                _snapshots++;
            }

            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                _writer.Dispose();
                _report?.Invoke($"[BattleRecord] path={_path} codec={_codec} coverage={_coverage} " +
                    $"inputs={_inputs} hashes={_hashes} snapshots={_snapshots}");
            }
        }
    }
}
