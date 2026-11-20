using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AbilityKit.Core.Mathematics;

namespace AbilityKit.Ability.Behavior
{
    /// <summary>
    /// 回滚能力的可选只读验证契约；未实现时仍允许正常业务运行。
    /// 验证不得执行决策、调用恢复或修改外部状态。
    /// </summary>
    public interface IBehaviorSnapshotValidator : IBehaviorRuntimeSnapshot
    {
        /// <summary>
        /// 验证完整载荷、配置兼容及所属依赖；失败抛出异常。
        /// </summary>
        void ValidateSnapshot(byte[] payload);
    }

    /// <summary>
    /// 行为领域有限值格式；只在显式捕获/恢复时检查，不限制普通 State.Set。
    /// </summary>
    internal static class BehaviorRecoveryCodec
    {
        internal const int MaxLength = 16 * 1024 * 1024;

        internal static byte[] Write(Action<BinaryWriter> write)
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);
            write(writer);
            writer.Flush();
            if (stream.Length > MaxLength) throw new InvalidOperationException("Behavior recovery payload exceeds limit.");
            return stream.ToArray();
        }

        internal static T Read<T>(byte[] payload, Func<BinaryReader, T> read)
        {
            if (payload == null || payload.Length == 0 || payload.Length > MaxLength)
                throw new InvalidOperationException("Missing or oversized behavior recovery payload.");
            using var stream = new MemoryStream(payload, false);
            using var reader = new BinaryReader(stream);
            var result = read(reader);
            if (stream.Position != stream.Length) throw new InvalidOperationException("Trailing behavior recovery data.");
            return result;
        }

        internal static int Count(BinaryReader reader)
        {
            var count = reader.ReadInt32();
            if (count < 0 || count > 100000 || count > reader.BaseStream.Length - reader.BaseStream.Position)
                throw new InvalidOperationException("Invalid behavior recovery collection length.");
            return count;
        }

        internal static byte[] Bytes(BinaryReader reader)
        {
            var count = reader.ReadInt32();
            if (count < 0 || count > MaxLength || count > reader.BaseStream.Length - reader.BaseStream.Position)
                throw new InvalidOperationException("Invalid behavior recovery byte length.");
            return reader.ReadBytes(count);
        }

        internal static void Bytes(BinaryWriter writer, byte[] value)
        {
            if (value == null || value.Length > MaxLength) throw new InvalidOperationException("Behavior snapshot is missing or oversized.");
            writer.Write(value.Length);
            writer.Write(value);
        }

        internal static void Text(BinaryWriter writer, string value)
        {
            writer.Write(value != null);
            if (value != null) writer.Write(value);
        }

        internal static bool Boolean(BinaryReader reader)
        {
            var value = reader.ReadByte();
            if (value > 1) throw new InvalidOperationException("Invalid behavior boolean.");
            return value == 1;
        }

        internal static string Text(BinaryReader reader) => Boolean(reader) ? reader.ReadString() : null;

        internal static float Float(BinaryReader reader)
        {
            var value = reader.ReadSingle();
            if (float.IsNaN(value) || float.IsInfinity(value)) throw new InvalidOperationException("Non-finite behavior value.");
            return value;
        }

        internal static Vec3 Vector(BinaryReader reader) => new Vec3(Float(reader), Float(reader), Float(reader));

        internal static void Vector(BinaryWriter writer, Vec3 value)
        {
            if (float.IsNaN(value.X) || float.IsInfinity(value.X) || float.IsNaN(value.Y) ||
                float.IsInfinity(value.Y) || float.IsNaN(value.Z) || float.IsInfinity(value.Z))
                throw new InvalidOperationException("Non-finite behavior vector.");
            writer.Write(value.X); writer.Write(value.Y); writer.Write(value.Z);
        }

        internal static void Map(BinaryWriter writer, IReadOnlyDictionary<string, object> map)
        {
            writer.Write(map != null);
            if (map == null) return;
            Count(writer, map.Count);
            foreach (var pair in map.OrderBy(p => p.Key, StringComparer.Ordinal))
            {
                writer.Write(pair.Key);
                switch (pair.Value)
                {
                    case null: writer.Write((byte)0); break;
                    case bool value: writer.Write((byte)1); writer.Write(value); break;
                    case int value: writer.Write((byte)2); writer.Write(value); break;
                    case long value: writer.Write((byte)3); writer.Write(value); break;
                    case float value when !float.IsNaN(value) && !float.IsInfinity(value):
                        writer.Write((byte)4); writer.Write(value); break;
                    case string value: writer.Write((byte)5); writer.Write(value); break;
                    case Vec3 value: writer.Write((byte)6); Vector(writer, value); break;
                    case BehaviorEntityId value: writer.Write((byte)7); writer.Write(value.Value); break;
                    default: throw new InvalidOperationException($"Behavior rollback field '{pair.Key}' has unsupported value type '{pair.Value.GetType().FullName}'. Separate runtime dependencies or provide a supported value before enabling rollback.");
                }
            }
        }

        internal static void Count(BinaryWriter writer, int count)
        {
            if (count < 0 || count > 100000) throw new InvalidOperationException("Behavior recovery collection exceeds limit.");
            writer.Write(count);
        }

        internal static Dictionary<string, object> Map(BinaryReader reader)
        {
            if (!Boolean(reader)) return null;
            var count = Count(reader);
            var result = new Dictionary<string, object>(StringComparer.Ordinal);
            string previous = null;
            for (var i = 0; i < count; i++)
            {
                var key = reader.ReadString();
                if (previous != null && string.CompareOrdinal(previous, key) >= 0)
                    throw new InvalidOperationException("Duplicate or unordered behavior key.");
                previous = key;
                object value = reader.ReadByte() switch
                {
                    0 => null, 1 => Boolean(reader), 2 => reader.ReadInt32(), 3 => reader.ReadInt64(),
                    4 => Float(reader), 5 => reader.ReadString(), 6 => Vector(reader),
                    7 => new BehaviorEntityId(reader.ReadInt64()),
                    _ => throw new InvalidOperationException("Unknown behavior value tag.")
                };
                result.Add(key, value);
            }
            return result;
        }
    }
}
