using System;
using System.Collections.Generic;
using System.IO;
using AbilityKit.Ability.Host;

namespace AbilityKit.Core.Recording.FrameRecord
{
    public sealed class FrameRecordBinaryWriter : IFrameRecordWriter
    {
        private const uint Magic = 0x52464B41; // 'AKFR'
        private const int Version = 1;

        private readonly string _outputPath;
        private readonly FrameRecordMeta _meta;

        private readonly List<InputFrame> _inputs = new List<InputFrame>(2048);
        private readonly List<FrameRecordStateHashFrame> _stateHashes = new List<FrameRecordStateHashFrame>(256);
        private readonly List<SnapshotFrame> _snapshots = new List<SnapshotFrame>(256);

        private bool _disposed;

        private sealed class InputFrame
        {
            public int Frame;
            public string PlayerId;
            public int OpCode;
            public byte[] Payload;
        }

        private sealed class SnapshotFrame
        {
            public int Frame;
            public int OpCode;
            public byte[] Payload;
        }

        public FrameRecordBinaryWriter(string outputPath, FrameRecordMeta meta)
        {
            _outputPath = RecordPathResolver.Resolve(string.IsNullOrEmpty(outputPath) ? "battle_record.bin" : outputPath);
            _meta = meta;
        }

        public void Append(in PlayerInputCommand cmd)
        {
            if (_disposed) return;

            var payload = cmd.Payload;
            _inputs.Add(new InputFrame
            {
                Frame = cmd.Frame.Value,
                PlayerId = cmd.Player.Value,
                OpCode = cmd.OpCode,
                Payload = payload != null && payload.Length > 0 ? (byte[])payload.Clone() : Array.Empty<byte>(),
            });
        }

        public void AppendStateHash(int frame, int version, uint hash)
        {
            if (_disposed) return;

            _stateHashes.Add(new FrameRecordStateHashFrame
            {
                Frame = frame,
                Version = version,
                Hash = hash,
            });
        }

        public void AppendSnapshot(int frame, int opCode, byte[] payload)
        {
            if (_disposed) return;

            _snapshots.Add(new SnapshotFrame
            {
                Frame = frame,
                OpCode = opCode,
                Payload = payload != null && payload.Length > 0 ? (byte[])payload.Clone() : Array.Empty<byte>(),
            });
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            var dir = Path.GetDirectoryName(_outputPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            using (var fs = new FileStream(_outputPath, FileMode.Create, FileAccess.Write, FileShare.Read))
            using (var bw = new BinaryWriter(fs))
            {
                bw.Write(Magic);
                bw.Write(Version);

                WriteMeta(bw, _meta);

                bw.Write(_inputs.Count);
                for (int i = 0; i < _inputs.Count; i++)
                {
                    var e = _inputs[i];
                    bw.Write(e.Frame);
                    bw.Write(e.PlayerId ?? string.Empty);
                    bw.Write(e.OpCode);
                    WriteBytes(bw, e.Payload);
                }

                bw.Write(_stateHashes.Count);
                for (int i = 0; i < _stateHashes.Count; i++)
                {
                    var e = _stateHashes[i];
                    if (e == null)
                    {
                        bw.Write(false);
                        continue;
                    }

                    bw.Write(true);
                    bw.Write(e.Frame);
                    bw.Write(e.Version);
                    bw.Write(e.Hash);
                }

                bw.Write(_snapshots.Count);
                for (int i = 0; i < _snapshots.Count; i++)
                {
                    var e = _snapshots[i];
                    bw.Write(e.Frame);
                    bw.Write(e.OpCode);
                    WriteBytes(bw, e.Payload);
                }
            }
        }

        private static void WriteMeta(BinaryWriter bw, FrameRecordMeta meta)
        {
            bw.Write(meta?.WorldId ?? string.Empty);
            bw.Write(meta?.WorldType ?? string.Empty);
            bw.Write(meta != null ? meta.TickRate : 0);
            bw.Write(meta != null ? meta.RandomSeed : 0);
            bw.Write(meta?.PlayerId ?? string.Empty);
            bw.Write(meta != null ? meta.StartedAtUnixMs : 0L);
        }

        private static void WriteBytes(BinaryWriter bw, byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
            {
                bw.Write(0);
                return;
            }

            bw.Write(bytes.Length);
            bw.Write(bytes);
        }

    }
}
