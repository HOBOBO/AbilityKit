using System;
using System.Collections.Generic;
using System.IO;

namespace AbilityKit.Core.Recording.FrameRecord
{
    public static class FrameRecordBinaryReader
    {
        private const uint Magic = 0x52464B41; // 'AKFR'

        public static FrameRecordFile Load(string path)
        {
            var resolved = RecordPathResolver.Resolve(path);

            using (var fs = new FileStream(resolved, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var br = new BinaryReader(fs))
            {
                var magic = br.ReadUInt32();
                if (magic != Magic)
                {
                    throw new InvalidDataException($"Invalid record magic: {magic}");
                }

                var version = br.ReadInt32();
                if (version <= 0)
                {
                    throw new InvalidDataException($"Invalid record version: {version}");
                }

                var meta = ReadMeta(br);

                var inputsCount = br.ReadInt32();
                if (inputsCount < 0) inputsCount = 0;
                var inputs = new List<FrameRecordInputFrame>(inputsCount);
                for (int i = 0; i < inputsCount; i++)
                {
                    var frame = br.ReadInt32();
                    var playerId = br.ReadString();
                    var opCode = br.ReadInt32();
                    var payload = ReadBytes(br);
                    var base64 = payload != null && payload.Length > 0 ? Convert.ToBase64String(payload) : string.Empty;

                    inputs.Add(new FrameRecordInputFrame
                    {
                        Frame = frame,
                        PlayerId = playerId,
                        OpCode = opCode,
                        PayloadBase64 = base64,
                    });
                }

                var hashesCount = br.ReadInt32();
                if (hashesCount < 0) hashesCount = 0;
                var hashes = new List<FrameRecordStateHashFrame>(hashesCount);
                for (int i = 0; i < hashesCount; i++)
                {
                    var hasValue = br.ReadBoolean();
                    if (!hasValue)
                    {
                        hashes.Add(null);
                        continue;
                    }

                    hashes.Add(new FrameRecordStateHashFrame
                    {
                        Frame = br.ReadInt32(),
                        Version = br.ReadInt32(),
                        Hash = br.ReadUInt32(),
                    });
                }

                var snapshotsCount = br.ReadInt32();
                if (snapshotsCount < 0) snapshotsCount = 0;
                var snapshots = new List<FrameRecordSnapshotFrame>(snapshotsCount);
                for (int i = 0; i < snapshotsCount; i++)
                {
                    var frame = br.ReadInt32();
                    var opCode = br.ReadInt32();
                    var payload = ReadBytes(br);
                    var base64 = payload != null && payload.Length > 0 ? Convert.ToBase64String(payload) : string.Empty;

                    snapshots.Add(new FrameRecordSnapshotFrame
                    {
                        Frame = frame,
                        OpCode = opCode,
                        PayloadBase64 = base64,
                    });
                }

                return new FrameRecordFile
                {
                    Meta = meta,
                    Inputs = inputs,
                    StateHashes = hashes,
                    Snapshots = snapshots,
                    Index = null,
                };
            }
        }

        private static FrameRecordMeta ReadMeta(BinaryReader br)
        {
            return new FrameRecordMeta
            {
                WorldId = br.ReadString(),
                WorldType = br.ReadString(),
                TickRate = br.ReadInt32(),
                RandomSeed = br.ReadInt32(),
                PlayerId = br.ReadString(),
                StartedAtUnixMs = br.ReadInt64(),
            };
        }

        private static byte[] ReadBytes(BinaryReader br)
        {
            var len = br.ReadInt32();
            if (len <= 0) return Array.Empty<byte>();
            return br.ReadBytes(len);
        }

    }
}
