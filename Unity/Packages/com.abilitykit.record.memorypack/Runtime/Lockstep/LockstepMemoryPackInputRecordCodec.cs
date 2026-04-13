using System;
using System.IO;
using System.Reflection;
using AbilityKit.Core.Common.Record.Lockstep;
using UnityEngine;

namespace AbilityKit.Record.MemoryPack
{
    public sealed class LockstepMemoryPackInputRecordCodec : ILockstepInputRecordCodec
    {
        private const uint Magic = 0x524C4D50; // 'PMLR'
        private const int Version = 1;

        private static readonly Type SerializerType = FindSerializerType();

        public ILockstepInputRecordWriter CreateWriter(string outputPath, LockstepInputRecordMeta meta)
        {
            return new Writer(outputPath, meta);
        }

        public LockstepInputRecordFile Load(string path)
        {
            var resolved = ResolvePath(path);

            using (var fs = new FileStream(resolved, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var br = new BinaryReader(fs))
            {
                var magic = br.ReadUInt32();
                if (magic != Magic) throw new InvalidDataException($"Invalid record magic: {magic}");

                var version = br.ReadInt32();
                if (version != Version) throw new InvalidDataException($"Invalid record version: {version}");

                var len = br.ReadInt32();
                if (len <= 0) return new LockstepInputRecordFile();

                var bytes = br.ReadBytes(len);
                return Deserialize<LockstepInputRecordFile>(bytes);
            }
        }

        private sealed class Writer : ILockstepInputRecordWriter
        {
            private readonly string _outputPath;
            private readonly LockstepInputRecordMeta _meta;
            private readonly System.Collections.Generic.List<LockstepInputRecordFrame> _inputs = new System.Collections.Generic.List<LockstepInputRecordFrame>(2048);
            private readonly System.Collections.Generic.List<LockstepStateHashRecordFrame> _stateHashes = new System.Collections.Generic.List<LockstepStateHashRecordFrame>(256);
            private readonly System.Collections.Generic.List<LockstepSnapshotRecordFrame> _snapshots = new System.Collections.Generic.List<LockstepSnapshotRecordFrame>(256);
            private bool _disposed;

            public Writer(string outputPath, LockstepInputRecordMeta meta)
            {
                _outputPath = ResolvePath(outputPath);
                _meta = meta;
            }

            public void Append(in AbilityKit.Ability.Host.PlayerInputCommand cmd)
            {
                if (_disposed) return;
                var payload = cmd.Payload;
                var base64 = payload != null && payload.Length > 0 ? Convert.ToBase64String(payload) : string.Empty;

                _inputs.Add(new LockstepInputRecordFrame
                {
                    Frame = cmd.Frame.Value,
                    PlayerId = cmd.Player.Value,
                    OpCode = cmd.OpCode,
                    PayloadBase64 = base64,
                });
            }

            public void AppendStateHash(int frame, int version, uint hash)
            {
                if (_disposed) return;
                _stateHashes.Add(new LockstepStateHashRecordFrame { Frame = frame, Version = version, Hash = hash });
            }

            public void AppendSnapshot(int frame, int opCode, byte[] payload)
            {
                if (_disposed) return;
                var base64 = payload != null && payload.Length > 0 ? Convert.ToBase64String(payload) : string.Empty;
                _snapshots.Add(new LockstepSnapshotRecordFrame { Frame = frame, OpCode = opCode, PayloadBase64 = base64 });
            }

            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;

                var file = new LockstepInputRecordFile
                {
                    Meta = _meta,
                    Inputs = _inputs,
                    StateHashes = _stateHashes,
                    Snapshots = _snapshots,
                    Index = null,
                };

                var payload = Serialize(in file);

                var dir = Path.GetDirectoryName(_outputPath);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

                using (var fs = new FileStream(_outputPath, FileMode.Create, FileAccess.Write, FileShare.Read))
                using (var bw = new BinaryWriter(fs))
                {
                    bw.Write(Magic);
                    bw.Write(Version);
                    bw.Write(payload.Length);
                    bw.Write(payload);
                }
            }
        }

        private static byte[] Serialize<T>(in T value)
        {
            var t = SerializerType;
            if (t == null) throw new InvalidOperationException("MemoryPack is not available.");

            var m = t.GetMethod("Serialize", BindingFlags.Public | BindingFlags.Static);
            if (m == null) throw new MissingMethodException("MemoryPackSerializer.Serialize not found.");

            if (m.IsGenericMethodDefinition) m = m.MakeGenericMethod(typeof(T));
            var result = m.Invoke(null, new object[] { value });
            return (byte[])result;
        }

        private static T Deserialize<T>(byte[] bytes)
        {
            var t = SerializerType;
            if (t == null) throw new InvalidOperationException("MemoryPack is not available.");

            var m = t.GetMethod("Deserialize", BindingFlags.Public | BindingFlags.Static);
            if (m == null) throw new MissingMethodException("MemoryPackSerializer.Deserialize not found.");

            if (m.IsGenericMethodDefinition) m = m.MakeGenericMethod(typeof(T));
            var result = m.Invoke(null, new object[] { bytes });
            return (T)result;
        }

        private static Type FindSerializerType()
        {
            try
            {
                var direct = Type.GetType("MemoryPack.MemoryPackSerializer", throwOnError: false);
                if (direct != null) return direct;

                var asms = AppDomain.CurrentDomain.GetAssemblies();
                for (int i = 0; i < asms.Length; i++)
                {
                    var tt = asms[i].GetType("MemoryPack.MemoryPackSerializer", throwOnError: false);
                    if (tt != null) return tt;
                }
            }
            catch
            {
            }

            return null;
        }

        private static string ResolvePath(string path)
        {
            if (string.IsNullOrEmpty(path)) path = "battle_record.pmlr";
            if (Path.IsPathRooted(path)) return path;

            var baseDir = Application.persistentDataPath;
            if (string.IsNullOrEmpty(baseDir)) baseDir = Application.dataPath;
            return Path.Combine(baseDir, path);
        }
    }
}
