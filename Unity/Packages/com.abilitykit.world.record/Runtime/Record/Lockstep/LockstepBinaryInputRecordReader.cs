using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace AbilityKit.Core.Common.Record.Lockstep
{
    public static class LockstepBinaryInputRecordReader
    {
        private const uint Magic = 0x524C4B41; // 'AKLR'

        public static LockstepInputRecordFile Load(string path)
        {
            var resolved = ResolvePath(path);

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
                var inputs = new List<LockstepInputRecordFrame>(inputsCount);
                for (int i = 0; i < inputsCount; i++)
                {
                    var frame = br.ReadInt32();
                    var playerId = br.ReadString();
                    var opCode = br.ReadInt32();
                    var payload = ReadBytes(br);
                    var base64 = payload != null && payload.Length > 0 ? Convert.ToBase64String(payload) : string.Empty;

                    inputs.Add(new LockstepInputRecordFrame
                    {
                        Frame = frame,
                        PlayerId = playerId,
                        OpCode = opCode,
                        PayloadBase64 = base64,
                    });
                }

                var hashesCount = br.ReadInt32();
                if (hashesCount < 0) hashesCount = 0;
                var hashes = new List<LockstepStateHashRecordFrame>(hashesCount);
                for (int i = 0; i < hashesCount; i++)
                {
                    var hasValue = br.ReadBoolean();
                    if (!hasValue)
                    {
                        hashes.Add(null);
                        continue;
                    }

                    hashes.Add(new LockstepStateHashRecordFrame
                    {
                        Frame = br.ReadInt32(),
                        Version = br.ReadInt32(),
                        Hash = br.ReadUInt32(),
                    });
                }

                var snapshotsCount = br.ReadInt32();
                if (snapshotsCount < 0) snapshotsCount = 0;
                var snapshots = new List<LockstepSnapshotRecordFrame>(snapshotsCount);
                for (int i = 0; i < snapshotsCount; i++)
                {
                    var frame = br.ReadInt32();
                    var opCode = br.ReadInt32();
                    var payload = ReadBytes(br);
                    var base64 = payload != null && payload.Length > 0 ? Convert.ToBase64String(payload) : string.Empty;

                    snapshots.Add(new LockstepSnapshotRecordFrame
                    {
                        Frame = frame,
                        OpCode = opCode,
                        PayloadBase64 = base64,
                    });
                }

                return new LockstepInputRecordFile
                {
                    Meta = meta,
                    Inputs = inputs,
                    StateHashes = hashes,
                    Snapshots = snapshots,
                    Index = null,
                };
            }
        }

        private static LockstepInputRecordMeta ReadMeta(BinaryReader br)
        {
            return new LockstepInputRecordMeta
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

        private static string ResolvePath(string path)
        {
            if (string.IsNullOrEmpty(path)) return path;
            if (Path.IsPathRooted(path)) return path;

            var baseDir = Application.persistentDataPath;
            if (string.IsNullOrEmpty(baseDir)) baseDir = Application.dataPath;
            return Path.Combine(baseDir, path);
        }
    }
}
