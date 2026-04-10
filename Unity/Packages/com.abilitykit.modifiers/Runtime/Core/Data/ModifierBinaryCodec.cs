using System;
using System.Runtime.CompilerServices;

namespace AbilityKit.Modifiers
{
    // ============================================================================
    // 修改器二进制编解码器
    // ============================================================================

    /// <summary>
    /// 修改器快照数据（紧凑格式）。
    /// 用于网络传输和状态存储。
    /// 
    /// 32 字节固定大小，适合高性能场景：
    /// - 网络传输（帧同步、状态同步）
    /// - 快照保存（回滚、存档）
    /// - 状态缓存（预计算、批量处理）
    /// 
    /// 字段布局：
    /// - 4 字节: Key (ModifierKey.Packed)
    /// - 1 字节: Op (ModifierOp)
    /// - 2 字节: Priority
    /// - 4 字节: SourceId
    /// - 1 字节: MagnitudeType
    /// - 4 字节: BaseValue
    /// - 4 字节: Coefficient
    /// - 4 字节: DecayParams
    /// - 4 字节: CurveDataIndex
    /// - 2 字节: SourceNameIndex
    /// - 2 字节: 对齐填充
    /// </summary>
    public struct ModifierSnapshotData
    {
        /// <summary>二进制大小（固定 32 字节）</summary>
        public const int BinarySize = 32;

        #region 序列化字段

        /// <summary>修改目标键</summary>
        public uint KeyPacked;

        /// <summary>操作类型</summary>
        public ModifierOp Op;

        /// <summary>优先级</summary>
        public short Priority;

        /// <summary>来源标识</summary>
        public int SourceId;

        /// <summary>数值类型</summary>
        public MagnitudeSourceType MagnitudeType;

        /// <summary>基础值</summary>
        public float BaseValue;

        /// <summary>系数</summary>
        public float Coefficient;

        /// <summary>衰减参数</summary>
        public float DecayParams;

        /// <summary>曲线数据索引</summary>
        public int CurveDataIndex;

        /// <summary>来源名称索引</summary>
        public short SourceNameIndex;

        #endregion

        #region 转换方法

        /// <summary>
        /// 从 ModifierData 转换
        /// </summary>
        public static ModifierSnapshotData FromModifierData(in ModifierData data)
        {
            return new ModifierSnapshotData
            {
                KeyPacked = data.Key.Packed,
                Op = data.Op,
                Priority = (short)data.Priority,
                SourceId = data.SourceId,
                MagnitudeType = data.Magnitude.Type,
                BaseValue = data.Magnitude.BaseValue,
                Coefficient = data.Magnitude.Coefficient,
                DecayParams = data.Magnitude.Data2,
                CurveDataIndex = 0,
                SourceNameIndex = data.SourceNameIndex
            };
        }

        /// <summary>
        /// 转换为 ModifierData
        /// </summary>
        public ModifierData ToModifierData()
        {
            return new ModifierData
            {
                Key = ModifierKey.FromPacked(KeyPacked),
                Op = Op,
                Priority = Priority,
                SourceId = SourceId,
                SourceNameIndex = SourceNameIndex,
                Magnitude = new MagnitudeSource
                {
                    Type = MagnitudeType,
                    Data0 = BaseValue,
                    Data1 = Coefficient,
                    Data2 = DecayParams
                }
            };
        }

        #endregion

        #region 二进制序列化

        /// <summary>
        /// 写入二进制数据（小端序）
        /// </summary>
        /// <param name="buffer">目标缓冲区（至少 32 字节）</param>
        /// <returns>写入的字节数</returns>
        public int WriteTo(Span<byte> buffer)
        {
            if (buffer.Length < BinarySize)
                return 0;

            int offset = 0;

            // Key: 4 字节
            LittleEndian.Write(buffer.Slice(offset, 4), KeyPacked);
            offset += 4;

            // Op: 1 字节
            buffer[offset++] = (byte)Op;

            // Priority: 2 字节
            LittleEndian.Write(buffer.Slice(offset, 2), (ushort)Priority);
            offset += 2;

            // SourceId: 4 字节
            LittleEndian.Write(buffer.Slice(offset, 4), (uint)SourceId);
            offset += 4;

            // MagnitudeType: 1 字节
            buffer[offset++] = (byte)MagnitudeType;

            // BaseValue: 4 字节
            LittleEndian.WriteFloat(buffer.Slice(offset, 4), BaseValue);
            offset += 4;

            // Coefficient: 4 字节
            LittleEndian.WriteFloat(buffer.Slice(offset, 4), Coefficient);
            offset += 4;

            // DecayParams: 4 字节
            LittleEndian.WriteFloat(buffer.Slice(offset, 4), DecayParams);
            offset += 4;

            // CurveDataIndex: 4 字节
            LittleEndian.Write(buffer.Slice(offset, 4), (uint)CurveDataIndex);
            offset += 4;

            // SourceNameIndex: 2 字节
            LittleEndian.Write(buffer.Slice(offset, 2), (ushort)SourceNameIndex);
            offset += 2;

            // 保留 2 字节对齐
            offset += 2;

            return offset;
        }

        /// <summary>
        /// 从二进制数据读取（小端序）
        /// </summary>
        /// <param name="buffer">源缓冲区（至少 32 字节）</param>
        /// <returns>读取的数据</returns>
        public static ModifierSnapshotData ReadFrom(ReadOnlySpan<byte> buffer)
        {
            if (buffer.Length < BinarySize)
                return default;

            int offset = 0;
            var data = new ModifierSnapshotData();

            // Key: 4 字节
            data.KeyPacked = LittleEndian.ReadUInt32(buffer.Slice(offset, 4));
            offset += 4;

            // Op: 1 字节
            data.Op = (ModifierOp)buffer[offset++];

            // Priority: 2 字节
            data.Priority = (short)LittleEndian.ReadUInt16(buffer.Slice(offset, 2));
            offset += 2;

            // SourceId: 4 字节
            data.SourceId = (int)LittleEndian.ReadUInt32(buffer.Slice(offset, 4));
            offset += 4;

            // MagnitudeType: 1 字节
            data.MagnitudeType = (MagnitudeSourceType)buffer[offset++];

            // BaseValue: 4 字节
            data.BaseValue = LittleEndian.ReadFloat(buffer.Slice(offset, 4));
            offset += 4;

            // Coefficient: 4 字节
            data.Coefficient = LittleEndian.ReadFloat(buffer.Slice(offset, 4));
            offset += 4;

            // DecayParams: 4 字节
            data.DecayParams = LittleEndian.ReadFloat(buffer.Slice(offset, 4));
            offset += 4;

            // CurveDataIndex: 4 字节
            data.CurveDataIndex = (int)LittleEndian.ReadUInt32(buffer.Slice(offset, 4));
            offset += 4;

            // SourceNameIndex: 2 字节
            data.SourceNameIndex = (short)LittleEndian.ReadUInt16(buffer.Slice(offset, 2));
            offset += 2;

            // 跳过 2 字节对齐
            offset += 2;

            return data;
        }

        #endregion

        #region 批量操作

        /// <summary>
        /// 批量写入多个快照数据
        /// </summary>
        /// <param name="data">快照数据数组</param>
        /// <param name="buffer">目标缓冲区</param>
        /// <returns>写入的字节数</returns>
        public static int WriteBatch(ReadOnlySpan<ModifierSnapshotData> data, Span<byte> buffer)
        {
            int requiredSize = data.Length * BinarySize;
            if (buffer.Length < requiredSize)
                return 0;

            for (int i = 0; i < data.Length; i++)
            {
                data[i].WriteTo(buffer.Slice(i * BinarySize, BinarySize));
            }

            return requiredSize;
        }

        /// <summary>
        /// 批量读取多个快照数据
        /// </summary>
        /// <param name="buffer">源缓冲区</param>
        /// <param name="count">要读取的数量</param>
        /// <returns>快照数据数组</returns>
        public static ModifierSnapshotData[] ReadBatch(ReadOnlySpan<byte> buffer, int count)
        {
            int requiredSize = count * BinarySize;
            if (buffer.Length < requiredSize)
                return Array.Empty<ModifierSnapshotData>();

            var result = new ModifierSnapshotData[count];
            for (int i = 0; i < count; i++)
            {
                result[i] = ReadFrom(buffer.Slice(i * BinarySize, BinarySize));
            }

            return result;
        }

        #endregion
    }

    // ============================================================================
    // 小端字节序工具
    // ============================================================================

    /// <summary>
    /// 小端字节序读写工具。
    /// 用于二进制序列化/反序列化。
    /// </summary>
    internal static class LittleEndian
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint ReadUInt32(ReadOnlySpan<byte> buffer)
            => (uint)(buffer[0] | (buffer[1] << 8) | (buffer[2] << 16) | (buffer[3] << 24));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ushort ReadUInt16(ReadOnlySpan<byte> buffer)
            => (ushort)(buffer[0] | (buffer[1] << 8));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Write(Span<byte> buffer, uint value)
        {
            buffer[0] = (byte)value;
            buffer[1] = (byte)(value >> 8);
            buffer[2] = (byte)(value >> 16);
            buffer[3] = (byte)(value >> 24);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Write(Span<byte> buffer, ushort value)
        {
            buffer[0] = (byte)value;
            buffer[1] = (byte)(value >> 8);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ReadFloat(ReadOnlySpan<byte> buffer)
        {
            uint bits = ReadUInt32(buffer);
            return BitConverter.Int32BitsToSingle((int)bits);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteFloat(Span<byte> buffer, float value)
        {
            int bits = BitConverter.SingleToInt32Bits(value);
            Write(buffer, (uint)bits);
        }
    }
}
