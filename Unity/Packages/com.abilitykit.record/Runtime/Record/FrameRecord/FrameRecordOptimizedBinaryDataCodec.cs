using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host;
using AbilityKit.Core.Pooling;

namespace AbilityKit.Core.Recording.FrameRecord
{
    /// <summary>
    /// 优化的二进制录像数据模型 - 直接存储字节数组，不使用 Base64
    /// </summary>
    public sealed class FrameRecordBinaryData
    {
        public FrameRecordMeta Meta;
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

        public void Reset()
        {
            if (InputPlayers != null) Array.Clear(InputPlayers, 0, InputCount);
            if (InputPayloads != null) Array.Clear(InputPayloads, 0, InputCount);
            if (SnapshotPayloads != null) Array.Clear(SnapshotPayloads, 0, SnapshotCount);
            if (StateHashPayloads != null) Array.Clear(StateHashPayloads, 0, StateHashCount);
            if (PlayerIdTable != null) Array.Clear(PlayerIdTable, 0, PlayerIdTable.Length);

            Meta = null;
            InputCount = 0;
            SnapshotCount = 0;
            StateHashCount = 0;
            StartFrame = 0;
            EndFrame = 0;
        }

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

            var firstNewIndex = PlayerIdTable.Length;
            Array.Resize(ref PlayerIdTable, PlayerIdTable.Length * 2);
            PlayerIdTable[firstNewIndex] = playerId;
            return firstNewIndex;
        }
    }

    /// <summary>
    /// 优化的二进制录像写入器。
    /// </summary>
    public sealed class FrameRecordOptimizedBinaryWriter : IFrameRecordWriter
    {
        private const uint Magic = 0x52464B41; // 'AKFR'
        private const int Version = 4; // 版本4，state hash track 保留真实 hash schema version
        private static readonly PoolKey DataPoolKey = new PoolKey("record.frame.optimized.binary.data");

        private readonly string _outputPath;
        private readonly FrameRecordMeta _meta;
        private readonly bool _useCompression;
        private readonly CompressionLevel _compressionLevel;

        private readonly FrameRecordBinaryData _data;

        private int _inputCount;
        private int _snapshotCount;
        private int _stateHashCount;
        private int _minFrame = int.MaxValue;
        private int _maxFrame = int.MinValue;

        private bool _disposed;

        public FrameRecordOptimizedBinaryWriter(
            string outputPath,
            FrameRecordMeta meta,
            bool useCompression = true,
            CompressionLevel compressionLevel = CompressionLevel.Fastest)
        {
            _outputPath = ResolvePath(outputPath);
            _meta = meta;
            _useCompression = useCompression;
            _compressionLevel = compressionLevel;

            _data = Pools.Get(
                DataPoolKey,
                static () => new FrameRecordBinaryData(),
                onGet: static data => data.Reset(),
                onRelease: static data => data.Reset(),
                defaultCapacity: 2,
                maxSize: 16,
                collectionCheck: false);
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

            try
            {
                using (var fs = new FileStream(_outputPath, FileMode.Create, FileAccess.Write, FileShare.Read))
                using (var bw = new BinaryWriter(fs))
                {
                    bw.Write(Magic);
                    bw.Write(Version);
                    bw.Write(_useCompression);
                    WriteMeta(bw, _data.Meta);
                    bw.Write(_data.StartFrame);
                    bw.Write(_data.EndFrame);
                    bw.Flush();

                    if (_useCompression)
                    {
                        using (var deflate = new DeflateStream(fs, _compressionLevel, leaveOpen: true))
                        using (var payloadWriter = new BinaryWriter(deflate))
                        {
                            WriteInputTrack(payloadWriter, _data);
                            WriteSnapshotTrack(payloadWriter, _data);
                            WriteStateHashTrack(payloadWriter, _data);
                        }
                    }
                    else
                    {
                        WriteInputTrack(bw, _data);
                        WriteSnapshotTrack(bw, _data);
                        WriteStateHashTrack(bw, _data);
                    }
                }
            }
            finally
            {
                Pools.Release(DataPoolKey, _data);
            }
        }

        private void WriteInputTrack(BinaryWriter bw, FrameRecordBinaryData data)
        {
            bw.Write(data.InputCount);

            Array.Clear(data.PlayerIdTable, 0, data.PlayerIdTable.Length);
            for (int i = 0; i < data.InputCount; i++)
            {
                data.RegisterPlayerId(data.InputPlayers[i] ?? string.Empty);
            }

            var playerCount = 0;
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
                WriteSignedVarInt(bw, frame - prevFrame);
                WriteSignedVarInt(bw, opCode - prevOpCode);

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
                WriteUnsignedVarInt(bw, (uint)playerIdx);

                var payload = data.InputPayloads[i];
                WriteBytes(bw, payload);

                prevFrame = frame;
                prevOpCode = opCode;
            }
        }

        private void WriteSnapshotTrack(BinaryWriter bw, FrameRecordBinaryData data)
        {
            bw.Write(data.SnapshotCount);

            int prevFrame = 0;
            int prevOpCode = 0;

            for (int i = 0; i < data.SnapshotCount; i++)
            {
                int frame = data.SnapshotFrames[i];
                int opCode = data.SnapshotOpCodes[i];

                WriteSignedVarInt(bw, frame - prevFrame);
                WriteSignedVarInt(bw, opCode - prevOpCode);

                var payload = data.SnapshotPayloads[i];
                WriteBytes(bw, payload);

                prevFrame = frame;
                prevOpCode = opCode;
            }
        }

        private void WriteStateHashTrack(BinaryWriter bw, FrameRecordBinaryData data)
        {
            bw.Write(data.StateHashCount);

            int prevFrame = 0;
            int prevVersion = 0;
            uint prevHash = 0;

            for (int i = 0; i < data.StateHashCount; i++)
            {
                int frame = data.StateHashFrames[i];
                int version = data.StateHashVersions[i];
                uint hash = data.StateHashValues[i];

                WriteSignedVarInt(bw, frame - prevFrame);
                WriteSignedVarInt(bw, version - prevVersion);

                // 增量编码哈希值
                int hashDelta = (int)(hash - prevHash);
                WriteSignedVarInt(bw, hashDelta);

                prevFrame = frame;
                prevVersion = version;
                prevHash = hash;
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
                WriteUnsignedVarInt(bw, 0u);
                return;
            }

            WriteUnsignedVarInt(bw, (uint)bytes.Length);
            bw.Write(bytes);
        }

        private static void WriteSignedVarInt(BinaryWriter bw, int value)
        {
            WriteUnsignedVarInt(bw, (uint)((value << 1) ^ (value >> 31)));
        }

        private static void WriteUnsignedVarInt(BinaryWriter bw, uint value)
        {
            while (value >= 0x80u)
            {
                bw.Write((byte)(value | 0x80u));
                value >>= 7;
            }

            bw.Write((byte)value);
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
    /// 优化的二进制录像读取器。
    /// </summary>
    public static class FrameRecordOptimizedBinaryReader
    {
        private const uint Magic = 0x52464B41; // 'AKFR'
        private const int MinimumSupportedVersion = 1;
        private const int MaximumSupportedVersion = 4;
        private const int MaximumTrackEntryCount = 10_000_000;
        private const int MaximumPlayerCount = 1_000_000;
        private const int MaximumPayloadLength = 256 * 1024 * 1024;

        public static FrameRecordBinaryData Load(string path)
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
                if (version < MinimumSupportedVersion || version > MaximumSupportedVersion)
                {
                    throw new InvalidDataException($"Unsupported optimized frame record version: {version}");
                }

                var useCompression = br.ReadBoolean();

                var meta = ReadMeta(br);
                var startFrame = br.ReadInt32();
                var endFrame = br.ReadInt32();

                var data = new FrameRecordBinaryData
                {
                    Meta = meta,
                    StartFrame = startFrame,
                    EndFrame = endFrame
                };

                if (useCompression)
                {
                    using (var deflate = new DeflateStream(fs, CompressionMode.Decompress, leaveOpen: true))
                    using (var payloadReader = new BinaryReader(deflate))
                    {
                        ReadInputTrack(payloadReader, data, version);
                        ReadSnapshotTrack(payloadReader, data, version);
                        ReadStateHashTrack(payloadReader, data, version);
                    }
                }
                else
                {
                    ReadInputTrack(br, data, version);
                    ReadSnapshotTrack(br, data, version);
                    ReadStateHashTrack(br, data, version);
                }

                return data;
            }
        }

        private static void ReadInputTrack(BinaryReader br, FrameRecordBinaryData data, int version)
        {
            var count = ReadValidatedCount(br, "input");

            // 读取 PlayerId 表
            var playerCount = ReadValidatedCount(br, "player", MaximumPlayerCount);
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
                frame += version >= 3 ? ReadSignedVarInt(br) : br.ReadInt32();
                opCode += version >= 3 ? ReadSignedVarInt(br) : br.ReadInt32();
                var playerIdx = version >= 3 ? (int)ReadUnsignedVarInt(br) : br.ReadInt32();
                if ((uint)playerIdx >= (uint)playerTable.Length)
                {
                    throw new InvalidDataException($"Invalid player index {playerIdx} for player table size {playerTable.Length}.");
                }

                data.InputFrames[i] = frame;
                data.InputOpCodes[i] = opCode;
                data.InputPlayers[i] = playerTable[playerIdx];
                data.InputPayloads[i] = ReadBytes(br);
            }
        }

        private static void ReadSnapshotTrack(BinaryReader br, FrameRecordBinaryData data, int version)
        {
            var count = ReadValidatedCount(br, "snapshot");

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
                frame += version >= 3 ? ReadSignedVarInt(br) : br.ReadInt32();
                opCode += version >= 3 ? ReadSignedVarInt(br) : br.ReadInt32();

                data.SnapshotFrames[i] = frame;
                data.SnapshotOpCodes[i] = opCode;
                data.SnapshotPayloads[i] = ReadBytes(br);
            }
        }

        private static void ReadStateHashTrack(BinaryReader br, FrameRecordBinaryData data, int version)
        {
            var count = ReadValidatedCount(br, "state hash");

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
                frame += version >= 3 ? ReadSignedVarInt(br) : br.ReadInt32();
                if (version >= 4)
                {
                    versionVal += ReadSignedVarInt(br);
                }

                int hashDelta = version >= 3 ? ReadSignedVarInt(br) : br.ReadInt32();
                hash += (uint)hashDelta;

                data.StateHashFrames[i] = frame;
                data.StateHashVersions[i] = versionVal;
                data.StateHashValues[i] = hash;
                data.StateHashPayloads[i] = Array.Empty<byte>();

                // Versions 1-3 did not persist the schema version and exposed the ordinal instead.
                if (version < 4) versionVal++;
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
            var len = ReadUnsignedVarInt(br);
            if (len == 0u) return Array.Empty<byte>();
            if (len > MaximumPayloadLength)
            {
                throw new InvalidDataException($"Frame record payload length {len} exceeds the supported maximum {MaximumPayloadLength}.");
            }

            var bytes = br.ReadBytes((int)len);
            if (bytes.Length != (int)len)
            {
                throw new EndOfStreamException($"Expected {len} payload bytes but read {bytes.Length}.");
            }

            return bytes;
        }

        private static int ReadValidatedCount(BinaryReader br, string trackName, int maximum = MaximumTrackEntryCount)
        {
            var count = br.ReadInt32();
            if (count < 0 || count > maximum)
            {
                throw new InvalidDataException($"Invalid {trackName} count: {count}.");
            }

            return count;
        }

        private static int ReadSignedVarInt(BinaryReader br)
        {
            var value = ReadUnsignedVarInt(br);
            return (int)((value >> 1) ^ (uint)-(int)(value & 1u));
        }

        private static uint ReadUnsignedVarInt(BinaryReader br)
        {
            uint value = 0;
            var shift = 0;
            while (shift < 35)
            {
                var b = br.ReadByte();
                value |= (uint)(b & 0x7F) << shift;
                if ((b & 0x80) == 0) return value;
                shift += 7;
            }

            throw new InvalidDataException("Invalid varint in optimized frame record.");
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
