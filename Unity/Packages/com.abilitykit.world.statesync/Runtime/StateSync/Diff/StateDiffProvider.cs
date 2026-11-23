using System;
using System.IO;
using System.IO.Compression;

namespace AbilityKit.Ability.StateSync.Diff
{
    /// <summary>
    /// 保留的压缩选项；默认提供器只区分不压缩和使用同一种 Deflate 实现。
    /// </summary>
    public enum DiffCompressionLevel
    {
        /// <summary>
        /// 禁用压缩和解压缩。
        /// </summary>
        None = 0,
        /// <summary>
        /// 兼容的低压缩标记；当前与其他启用值行为相同。
        /// </summary>
        Low = 1,
        /// <summary>
        /// 默认压缩标记；当前与其他启用值行为相同。
        /// </summary>
        Medium = 2,
        /// <summary>
        /// 兼容的高压缩标记；当前与其他启用值行为相同。
        /// </summary>
        High = 3
    }

    /// <summary>
    /// 面向既有状态差分用法的兼容提供器；调用方负责前态身份及类型和配置匹配。
    /// 不应用于外部不可信数据、跨版本协议或本地固定基线恢复的准入判断。
    /// </summary>
    public sealed class StateDiffProvider : IStateDiffProvider
    {
        private readonly DiffCompressionLevel _compressionLevel;

        /// <summary>
        /// 创建以中等压缩标记为默认值的提供器；当前所有非禁用标记使用相同压缩行为。
        /// </summary>
        public StateDiffProvider(DiffCompressionLevel compressionLevel = DiffCompressionLevel.Medium)
        {
            _compressionLevel = compressionLevel;
        }

        /// <summary>
        /// 根据前态是否为空生成全量或增量记录；当前状态为空时抛异常。
        /// 结果帧元数据均为 0，时间取当前 UTC Unix 毫秒，未压缩大小取完整目标状态的大小。
        /// </summary>
        public IStateDiff ComputeDiff<TState>(TState current, TState previous) where TState : class
        {
            if (current == null) throw new ArgumentNullException(nameof(current));

            var currentData = SerializeState(current);
            int uncompressedSize = currentData.Length;

            byte[] compressedData;
            bool isFullSnapshot;

            if (previous == null)
            {
                compressedData = Compress(currentData);
                isFullSnapshot = true;
            }
            else
            {
                var previousData = SerializeState(previous);
                var diffData = ComputeBinaryDiff(currentData, previousData);
                compressedData = Compress(diffData);
                isFullSnapshot = false;
            }

            return new StateDiff(
                fromFrame: 0,
                toFrame: 0,
                timestamp: DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                compressedData: compressedData,
                uncompressedSize: uncompressedSize,
                isFullSnapshot: isFullSnapshot);
        }

        /// <summary>
        /// 全量记录忽略前态，增量由调用方提供匹配的前态；不验证帧、大小元数据或基线身份。
        /// 空差分抛异常，底层失败直接传播；成功返回重建对象，不原地修改前态。
        /// </summary>
        public TState ApplyDiff<TState>(TState baseState, IStateDiff diff) where TState : class
        {
            if (diff == null) throw new ArgumentNullException(nameof(diff));

            var decompressed = Decompress(diff.CompressedData);
            byte[] targetData;

            if (diff.IsFullSnapshot)
            {
                targetData = decompressed;
            }
            else
            {
                var baseData = SerializeState(baseState);
                targetData = ApplyBinaryDiff(baseData, decompressed);
            }

            return DeserializeState<TState>(targetData);
        }

        /// <summary>
        /// 世界快照沿用其现有格式；其他类型走公开实例字段的兼容路径，不保证一般对象图或跨版本兼容。
        /// </summary>
        public byte[] SerializeState<TState>(TState state) where TState : class
        {
            if (state is Snapshot.WorldStateSnapshot worldSnapshot)
            {
                return worldSnapshot.ToBytes();
            }

            using var stream = new MemoryStream();
            var serializer = new BinarySerializerImpl(stream, leaveOpen: true);
            serializer.Serialize(state);
            return stream.ToArray();
        }

        /// <summary>
        /// 空引用或空数据返回空；要求目标类型及表示与写入端匹配。
        /// 泛型目标恰为世界快照时使用其格式，其他类型沿用兼容路径，失败向调用方传播。
        /// </summary>
        public TState DeserializeState<TState>(byte[] data) where TState : class
        {
            if (data == null || data.Length == 0) return null;

            if (typeof(TState) == typeof(Snapshot.WorldStateSnapshot))
            {
                return (TState)(object)Snapshot.WorldStateSnapshot.FromBytes(data);
            }

            using var stream = new MemoryStream(data);
            var deserializer = new BinarySerializerImpl(stream, leaveOpen: true);
            return (TState)deserializer.Deserialize(typeof(TState));
        }

        private byte[] Compress(byte[] data)
        {
            if (_compressionLevel == DiffCompressionLevel.None || data == null || data.Length == 0)
                return data;

            using var outputStream = new MemoryStream();
            using (var deflateStream = new DeflateStream(outputStream, CompressionMode.Compress, leaveOpen: true))
            {
                deflateStream.Write(data, 0, data.Length);
            }
            return outputStream.ToArray();
        }

        private byte[] Decompress(byte[] data)
        {
            if (_compressionLevel == DiffCompressionLevel.None || data == null || data.Length == 0)
                return data;

            using var inputStream = new MemoryStream(data);
            using var deflateStream = new DeflateStream(inputStream, CompressionMode.Decompress, leaveOpen: true);
            using var outputStream = new MemoryStream();
            deflateStream.CopyTo(outputStream);
            return outputStream.ToArray();
        }

        private byte[] ComputeBinaryDiff(byte[] current, byte[] previous)
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);

            writer.Write(current.Length);
            writer.Write(previous?.Length ?? 0);

            int minLength = System.Math.Min(current.Length, previous?.Length ?? 0);

            writer.Write(minLength);
            for (int i = 0; i < minLength; i++)
            {
                if (current[i] != (previous != null ? previous[i] : 0))
                {
                    writer.Write((byte)1);
                    writer.Write(i);
                    writer.Write(current[i]);
                }
                else
                {
                    writer.Write((byte)0);
                }
            }

            var extraLength = current.Length - minLength;
            writer.Write(extraLength);
            if (extraLength > 0)
            {
                for (int i = minLength; i < current.Length; i++)
                {
                    writer.Write(current[i]);
                }
            }

            return stream.ToArray();
        }

        private byte[] ApplyBinaryDiff(byte[] baseData, byte[] diff)
        {
            using var stream = new MemoryStream(diff);
            using var reader = new BinaryReader(stream);

            int currentLength = reader.ReadInt32();
            int previousLength = reader.ReadInt32();
            if (currentLength < 0 || previousLength < 0)
                throw new InvalidDataException("State diff contains a negative payload length.");
            if (baseData != null && baseData.Length != previousLength)
                throw new InvalidDataException(
                    $"State diff base length mismatch. Expected {previousLength}, got {baseData.Length}.");

            var result = new byte[currentLength];

            if (baseData != null && baseData.Length > 0)
            {
                Array.Copy(baseData, result, System.Math.Min(baseData.Length, currentLength));
            }

            int minLength = reader.ReadInt32();
            if (minLength < 0 || minLength > currentLength || minLength > previousLength)
                throw new InvalidDataException("State diff contains an invalid shared payload length.");

            for (int i = 0; i < minLength; i++)
            {
                byte hasChange = reader.ReadByte();
                if (hasChange == 1)
                {
                    int index = reader.ReadInt32();
                    byte value = reader.ReadByte();
                    if (index != i)
                        throw new InvalidDataException($"State diff index mismatch. Expected {i}, got {index}.");
                    result[index] = value;
                }
                else if (hasChange != 0)
                {
                    throw new InvalidDataException($"State diff contains an invalid change marker: {hasChange}.");
                }
            }

            int extraLength = reader.ReadInt32();
            if (extraLength != currentLength - minLength)
                throw new InvalidDataException("State diff contains an invalid trailing payload length.");

            for (int i = 0; i < extraLength; i++)
            {
                result[minLength + i] = reader.ReadByte();
            }

            if (stream.Position != stream.Length)
                throw new InvalidDataException("State diff contains trailing data.");

            return result;
        }

        /// <summary>
        /// 旧版非快照状态的序列化辅助类型，仅供配对的提供器内部使用。
        /// </summary>
        private class BinarySerializerImpl
        {
            private static readonly System.Collections.Concurrent.ConcurrentDictionary<Type, System.Reflection.FieldInfo[]> SerializableFieldsCache =
                new System.Collections.Concurrent.ConcurrentDictionary<Type, System.Reflection.FieldInfo[]>();

            private readonly BinaryReader _reader;
            private readonly BinaryWriter _writer;

            public BinarySerializerImpl(Stream stream, bool leaveOpen)
            {
                _reader = new BinaryReader(stream);
                _writer = new BinaryWriter(stream);
            }

            private static System.Reflection.FieldInfo[] GetSerializableFields(Type type)
            {
                return SerializableFieldsCache.GetOrAdd(
                    type,
                    static t => t.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance));
            }

            public void Serialize(object value)
            {
                SerializeObject(value, 0);
            }

            public object Deserialize(Type type)
            {
                return DeserializeObject(type, 0);
            }

            private void SerializeObject(object value, int depth)
            {
                if (depth > 32) throw new InvalidOperationException("Max depth exceeded");

                if (value == null) { _writer.Write(false); return; }
                _writer.Write(true);

                var type = value.GetType();
                if (type.IsPrimitive) { WritePrimitive(value); return; }
                if (value is string s) { _writer.Write(s); return; }

                if (value is Array arr)
                {
                    _writer.Write(arr.Length);
                    foreach (var item in arr) SerializeObject(item, depth + 1);
                    return;
                }

                var fields = GetSerializableFields(type);
                _writer.Write(fields.Length);
                foreach (var f in fields) SerializeObject(f.GetValue(value), depth + 1);
            }

            private object DeserializeObject(Type type, int depth)
            {
                if (depth > 32) throw new InvalidOperationException("Max depth exceeded");

                if (!_reader.ReadBoolean()) return null;
                if (type.IsPrimitive) return ReadPrimitive(type);
                if (type == typeof(string)) return _reader.ReadString();

                if (type.IsArray)
                {
                    var len = _reader.ReadInt32();
                    var arr = Array.CreateInstance(type.GetElementType(), len);
                    for (int i = 0; i < len; i++) arr.SetValue(DeserializeObject(type.GetElementType(), depth + 1), i);
                    return arr;
                }

                var obj = Activator.CreateInstance(type);
                var fields = GetSerializableFields(type);
                var count = _reader.ReadInt32();
                for (int i = 0; i < count && i < fields.Length; i++)
                    fields[i].SetValue(obj, DeserializeObject(fields[i].FieldType, depth + 1));
                return obj;
            }

            private void WritePrimitive(object value)
            {
                if (value is bool b) _writer.Write(b);
                else if (value is byte bt) _writer.Write(bt);
                else if (value is sbyte sb) _writer.Write(sb);
                else if (value is char c) _writer.Write(c);
                else if (value is short s) _writer.Write(s);
                else if (value is ushort us) _writer.Write(us);
                else if (value is int i) _writer.Write(i);
                else if (value is uint ui) _writer.Write(ui);
                else if (value is long l) _writer.Write(l);
                else if (value is ulong ul) _writer.Write(ul);
                else if (value is float f) _writer.Write(f);
                else if (value is double d) _writer.Write(d);
            }

            private object ReadPrimitive(Type type)
            {
                if (type == typeof(bool)) return _reader.ReadBoolean();
                if (type == typeof(byte)) return _reader.ReadByte();
                if (type == typeof(sbyte)) return _reader.ReadSByte();
                if (type == typeof(char)) return _reader.ReadChar();
                if (type == typeof(short)) return _reader.ReadInt16();
                if (type == typeof(ushort)) return _reader.ReadUInt16();
                if (type == typeof(int)) return _reader.ReadInt32();
                if (type == typeof(uint)) return _reader.ReadUInt32();
                if (type == typeof(long)) return _reader.ReadInt64();
                if (type == typeof(ulong)) return _reader.ReadUInt64();
                if (type == typeof(float)) return _reader.ReadSingle();
                if (type == typeof(double)) return _reader.ReadDouble();
                throw new NotSupportedException();
            }
        }
    }
}
