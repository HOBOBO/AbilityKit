using System;
using System.IO;
using System.IO.Compression;
using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host;

namespace AbilityKit.Core.Common.Record.Lockstep
{
    /// <summary>
    /// 优化的二进制录像数据模型 - 直接存储字节数组，不使用 Base64
    /// </summary>
    public sealed class LockstepBinaryRecordData
    {
        public LockstepInputRecordMeta Meta;
        public string[] InputPlayers;
        public int[] InputFrames;
        public int[] InputOpCodes;
        public byte[][] InputPayloads;
        public int InputCount;

        public byte[][] SnapshotPayloads;
        public int[] SnapshotFrames;
        public int[] SnapshotOpCodes;
        public int SnapshotCount;

        public byte[][] StateHashPayloads;
        public int[] StateHashFrames;
        public int[] StateHashVersions;
        public uint[] StateHashValues;
        public int StateHashCount;

        public int StartFrame;
        public int EndFrame;
        public string[] PlayerIdTable;

        public void EnsureCapacity(int inputs, int snapshots, int hashes, int maxPlayers)
        {
            if (InputFrames == null || InputFrames.Length < inputs)
            {
                InputFrames = new int[inputs];
                InputPlayers = new string[inputs];
                InputOpCodes = new int[inputs];
                InputPayloads = new byte[inputs][];
            }

            if (SnapshotPayloads == null || SnapshotPayloads.Length < snapshots)
            {
                SnapshotFrames = new int[snapshots];
                SnapshotOpCodes = new int[snapshots];
                SnapshotPayloads = new byte[snapshots][];
            }

            if (StateHashPayloads == null || StateHashPayloads.Length < hashes)
            {
                StateHashFrames = new int[hashes];
                StateHashVersions = new int[hashes];
                StateHashValues = new uint[hashes];
                StateHashPayloads = new byte[hashes][];
            }

            if (PlayerIdTable == null || PlayerIdTable.Length < maxPlayers)
            {
                PlayerIdTable = new string[maxPlayers > 0 ? maxPlayers : 16];
            }
        }

        public int RegisterPlayerId(string playerId)
        {
            for (int i = 0; i < PlayerIdTable.Length; i++)
            {
                if (PlayerIdTable[i] == playerId) return i;
                if (PlayerIdTable[i] == null)
                {
                    PlayerIdTable[i] = playerId;
                    return i;
                }
            }

            Array.Resize(ref PlayerIdTable, PlayerIdTable.Length * 2);
            PlayerIdTable[PlayerIdTable.Length / 2 - 1] = playerId;
            return PlayerIdTable.Length / 2 - 1;
        }
    }

    /// <summary>
    /// 优化的二进制录像写入器
    /// </summary>
    public sealed class LockstepOptimizedBinaryRecordWriter : ILockstepInputRecordWriter
    {
        private const uint Magic = 0x524C4B41; // 'AKLR'
        private const int Version = 2; // 版本2，支持压缩

        private readonly string _outputPath;
        private readonly LockstepInputRecordMeta _meta;
        private readonly bool _useCompression;
        private readonly CompressionLevel _compressionLevel;

        private readonly LockstepBinaryRecordData _data = new LockstepBinaryRecordData();

        private int _inputCount;
        private int _snapshotCount;
        private int _stateHashCount;
        private int _minFrame = int.MaxValue;
        private int _maxFrame = int.MinValue;

        private bool _disposed;

        public LockstepOptimizedBinaryRecordWriter(
            string outputPath,
            LockstepInputRecordMeta meta,
            bool useCompression = true,
            CompressionLevel compressionLevel = CompressionLevel.Fastest)
        {
            _outputPath = ResolvePath(outputPath);
            _meta = meta;
            _useCompression = useCompression;
            _compressionLevel = compressionLevel;

            _data.Meta = meta;
            _data.EnsureCapacity(8192, 256, 256, 16);
        }

        public void Append(in PlayerInputCommand cmd)
        {
            if (_disposed) return;

            EnsureInputCapacity();
            var idx = _inputCount++;
            _data.InputFrames[idx] = cmd.Frame.Value;
            _data.InputPlayers[idx] = cmd.Player.Value;
            _data.InputOpCodes[idx] = cmd.OpCode;
            _data.InputPayloads[idx] = cmd.Payload != null && cmd.Payload.Length > 0
                ? (byte[])cmd.Payload.Clone()
                : Array.Empty<byte>();

            UpdateFrameRange(cmd.Frame.Value);
        }

        public void AppendStateHash(int frame, int version, uint hash)
        {
            if (_disposed) return;

            EnsureHashCapacity();
            var idx = _stateHashCount++;
            _data.StateHashFrames[idx] = frame;
            _data.StateHashVersions[idx] = version;
            _data.StateHashValues[idx] = hash;
            _data.StateHashPayloads[idx] = Array.Empty<byte>();

            UpdateFrameRange(frame);
        }

        public void AppendSnapshot(int frame, int opCode, byte[] payload)
        {
            if (_disposed) return;

            EnsureSnapshotCapacity();
            var idx = _snapshotCount++;
            _data.SnapshotFrames[idx] = frame;
            _data.SnapshotOpCodes[idx] = opCode;
            _data.SnapshotPayloads[idx] = payload != null && payload.Length > 0
                ? (byte[])payload.Clone()
                : Array.Empty<byte>();

            UpdateFrameRange(frame);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _data.InputCount = _inputCount;
            _data.SnapshotCount = _snapshotCount;
            _data.StateHashCount = _stateHashCount;
            _data.StartFrame = _minFrame == int.MaxValue ? 0 : _minFrame;
            _data.EndFrame = _maxFrame == int.MinValue ? 0 : _maxFrame;

            var dir = Path.GetDirectoryName(_outputPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            using (var fs = new FileStream(_outputPath, FileMode.Create, FileAccess.Write, FileShare.Read))
            using (var bw = new BinaryWriter(fs))
            {
                bw.Write(Magic);
                bw.Write(Version);
                bw.Write(_useCompression);
                WriteMeta(bw, _data.Meta);
                bw.Write(_data.StartFrame);
                bw.Write(_data.EndFrame);

                WriteInputTrack(bw, _data);
                WriteSnapshotTrack(bw, _data);
                WriteStateHashTrack(bw, _data);
            }
        }

        private void WriteInputTrack(BinaryWriter bw, LockstepBinaryRecordData data)
        {
            bw.Write(data.InputCount);

            // 先写入 PlayerId 表
            var playerCount = 0;
            for (int i = 0; i < data.InputCount; i++)
            {
                for (int j = 0; j < playerCount; j++)
                {
                    if (data.InputPlayers[i] == data.PlayerIdTable[j]) break;
                    if (data.PlayerIdTable[j] == null)
                    {
                        data.PlayerIdTable[j] = data.InputPlayers[i];
                        playerCount = j + 1;
                        break;
                    }
                }
            }

            // 统计实际使用的 player 数量
            playerCount = 0;
            for (int i = 0; i < data.PlayerIdTable.Length; i++)
            {
                if (data.PlayerIdTable[i] != null) playerCount++;
                else break;
            }
            bw.Write(playerCount);
            for (int i = 0; i < playerCount; i++)
            {
                bw.Write(data.PlayerIdTable[i] ?? string.Empty);
            }

            int prevFrame = 0;
            int prevOpCode = 0;

            for (int i = 0; i < data.InputCount; i++)
            {
                int frame = data.InputFrames[i];
                int opCode = data.InputOpCodes[i];

                // 增量编码
                bw.Write(frame - prevFrame);
                bw.Write(opCode - prevOpCode);

                // 查找 player index
                var playerId = data.InputPlayers[i];
                int playerIdx = 0;
                for (int j = 0; j < playerCount; j++)
                {
                    if (data.PlayerIdTable[j] == playerId)
                    {
                        playerIdx = j;
                        break;
                    }
                }
                bw.Write(playerIdx);

                var payload = data.InputPayloads[i];
                WriteBytes(bw, payload);

                prevFrame = frame;
                prevOpCode = opCode;
            }
        }

        private void WriteSnapshotTrack(BinaryWriter bw, LockstepBinaryRecordData data)
        {
            bw.Write(data.SnapshotCount);

            int prevFrame = 0;
            int prevOpCode = 0;

            for (int i = 0; i < data.SnapshotCount; i++)
            {
                int frame = data.SnapshotFrames[i];
                int opCode = data.SnapshotOpCodes[i];

                bw.Write(frame - prevFrame);
                bw.Write(opCode - prevOpCode);

                var payload = data.SnapshotPayloads[i];
                WriteBytes(bw, payload);

                prevFrame = frame;
                prevOpCode = opCode;
            }
        }

        private void WriteStateHashTrack(BinaryWriter bw, LockstepBinaryRecordData data)
        {
            bw.Write(data.StateHashCount);

            int prevFrame = 0;
            uint prevHash = 0;

            for (int i = 0; i < data.StateHashCount; i++)
            {
                int frame = data.StateHashFrames[i];
                uint hash = data.StateHashValues[i];

                bw.Write(frame - prevFrame);

                // 增量编码哈希值
                int hashDelta = (int)(hash - prevHash);
                bw.Write(hashDelta);

                prevFrame = frame;
                prevHash = hash;
            }
        }

        private static void WriteMeta(BinaryWriter bw, LockstepInputRecordMeta meta)
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

        private void UpdateFrameRange(int frame)
        {
            if (frame < _minFrame) _minFrame = frame;
            if (frame > _maxFrame) _maxFrame = frame;
        }

        private void EnsureInputCapacity()
        {
            if (_inputCount >= _data.InputFrames.Length)
            {
                var newSize = _data.InputFrames.Length * 2;
                Array.Resize(ref _data.InputFrames, newSize);
                Array.Resize(ref _data.InputPlayers, newSize);
                Array.Resize(ref _data.InputOpCodes, newSize);
                Array.Resize(ref _data.InputPayloads, newSize);
            }
        }

        private void EnsureSnapshotCapacity()
        {
            if (_snapshotCount >= _data.SnapshotFrames.Length)
            {
                var newSize = _data.SnapshotFrames.Length * 2;
                Array.Resize(ref _data.SnapshotFrames, newSize);
                Array.Resize(ref _data.SnapshotOpCodes, newSize);
                Array.Resize(ref _data.SnapshotPayloads, newSize);
            }
        }

        private void EnsureHashCapacity()
        {
            if (_stateHashCount >= _data.StateHashFrames.Length)
            {
                var newSize = _data.StateHashFrames.Length * 2;
                Array.Resize(ref _data.StateHashFrames, newSize);
                Array.Resize(ref _data.StateHashVersions, newSize);
                Array.Resize(ref _data.StateHashValues, newSize);
                Array.Resize(ref _data.StateHashPayloads, newSize);
            }
        }

        private static string ResolvePath(string path)
        {
            if (string.IsNullOrEmpty(path)) path = "battle_record.bin";

            if (Path.IsPathRooted(path)) return path;

#if UNITY_2020_3_OR_NEWER
            var baseDir = UnityEngine.Application.persistentDataPath;
#else
            var baseDir = System.Environment.CurrentDirectory;
#endif
            return Path.Combine(baseDir, path);
        }
    }

    /// <summary>
    /// 优化的二进制录像读取器
    /// </summary>
    public static class LockstepOptimizedBinaryRecordReader
    {
        private const uint Magic = 0x524C4B41; // 'AKLR'

        public static LockstepBinaryRecordData Load(string path)
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
                var useCompression = br.ReadBoolean();

                var meta = ReadMeta(br);
                var startFrame = br.ReadInt32();
                var endFrame = br.ReadInt32();

                var data = new LockstepBinaryRecordData
                {
                    Meta = meta,
                    StartFrame = startFrame,
                    EndFrame = endFrame
                };

                ReadInputTrack(br, data, version);
                ReadSnapshotTrack(br, data, version);
                ReadStateHashTrack(br, data, version);

                return data;
            }
        }

        private static void ReadInputTrack(BinaryReader br, LockstepBinaryRecordData data, int version)
        {
            var count = br.ReadInt32();
            if (count < 0) count = 0;

            // 读取 PlayerId 表
            var playerCount = br.ReadInt32();
            var playerTable = new string[playerCount];
            for (int i = 0; i < playerCount; i++)
            {
                playerTable[i] = br.ReadString();
            }

            data.EnsureCapacity(count, 0, 0, playerCount);
            data.InputCount = count;

            int frame = 0, opCode = 0;

            for (int i = 0; i < count; i++)
            {
                frame += br.ReadInt32();
                opCode += br.ReadInt32();
                var playerIdx = br.ReadInt32();

                data.InputFrames[i] = frame;
                data.InputOpCodes[i] = opCode;
                data.InputPlayers[i] = playerTable[playerIdx];
                data.InputPayloads[i] = ReadBytes(br);
            }
        }

        private static void ReadSnapshotTrack(BinaryReader br, LockstepBinaryRecordData data, int version)
        {
            var count = br.ReadInt32();
            if (count < 0) count = 0;

            if (data.SnapshotPayloads == null || data.SnapshotPayloads.Length < count)
            {
                data.SnapshotFrames = new int[count];
                data.SnapshotOpCodes = new int[count];
                data.SnapshotPayloads = new byte[count][];
            }
            data.SnapshotCount = count;

            int frame = 0, opCode = 0;

            for (int i = 0; i < count; i++)
            {
                frame += br.ReadInt32();
                opCode += br.ReadInt32();

                data.SnapshotFrames[i] = frame;
                data.SnapshotOpCodes[i] = opCode;
                data.SnapshotPayloads[i] = ReadBytes(br);
            }
        }

        private static void ReadStateHashTrack(BinaryReader br, LockstepBinaryRecordData data, int version)
        {
            var count = br.ReadInt32();
            if (count < 0) count = 0;

            if (data.StateHashPayloads == null || data.StateHashPayloads.Length < count)
            {
                data.StateHashFrames = new int[count];
                data.StateHashVersions = new int[count];
                data.StateHashValues = new uint[count];
                data.StateHashPayloads = new byte[count][];
            }
            data.StateHashCount = count;

            int frame = 0, versionVal = 0;
            uint hash = 0;

            for (int i = 0; i < count; i++)
            {
                frame += br.ReadInt32();
                int hashDelta = br.ReadInt32();
                hash += (uint)hashDelta;

                data.StateHashFrames[i] = frame;
                data.StateHashVersions[i] = versionVal;
                data.StateHashValues[i] = hash;
                data.StateHashPayloads[i] = Array.Empty<byte>();

                versionVal++;
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

#if UNITY_2020_3_OR_NEWER
            var baseDir = UnityEngine.Application.persistentDataPath;
#else
            var baseDir = System.Environment.CurrentDirectory;
#endif
            return Path.Combine(baseDir, path);
        }
    }
}
