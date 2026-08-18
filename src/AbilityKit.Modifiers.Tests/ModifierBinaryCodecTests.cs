using System;
using AbilityKit.Modifiers;
using Xunit;

namespace AbilityKit.Modifiers.Tests
{
    /// <summary>
    /// ModifierSnapshotData 二进制编解码测试。
    /// 该结构体是 32 字节固定布局、小端序、用于网络/快照传输。
    /// 注意：快照只承载 Key/Op/Priority/SourceId/SourceNameIndex 与
    /// Magnitude 的 Type + Data0/Data1/Data2，ArrayData（曲线）与 PipelineData 是有损字段。
    /// </summary>
    public sealed class ModifierBinaryCodecTests
    {
        [Fact]
        public void FromModifierData_then_ToModifierData_preserves_serialized_fields()
        {
            var original = ModifierData.Add(TestKeys.Attack, 42.5f, sourceId: 7, sourceNameIndex: 3, priority: 10);
            var snapshot = ModifierSnapshotData.FromModifierData(original);
            var restored = snapshot.ToModifierData();

            Assert.Equal(original.Key, restored.Key);
            Assert.Equal(original.Op, restored.Op);
            Assert.Equal(original.Priority, restored.Priority);
            Assert.Equal(original.SourceId, restored.SourceId);
            Assert.Equal(original.SourceNameIndex, restored.SourceNameIndex);
            Assert.Equal(original.Magnitude.Type, restored.Magnitude.Type);
            FloatAssert.Near(original.Magnitude.Data0, restored.Magnitude.Data0);
            FloatAssert.Near(original.Magnitude.Data1, restored.Magnitude.Data1);
            FloatAssert.Near(original.Magnitude.Data2, restored.Magnitude.Data2);
        }

        [Fact]
        public void WriteTo_readfrom_round_trips_all_fields()
        {
            var original = new ModifierSnapshotData
            {
                KeyPacked = 0x0A0B0C0Du,
                Op = ModifierOp.Mul,
                Priority = -3,
                SourceId = -1234567,
                MagnitudeType = MagnitudeSourceType.TimeDecay,
                BaseValue = 12.5f,
                Coefficient = -0.25f,
                DecayParams = 3.75f,
                CurveDataIndex = 99,
                SourceNameIndex = -7,
            };

            var buffer = new byte[ModifierSnapshotData.BinarySize];
            int written = original.WriteTo(buffer);
            var restored = ModifierSnapshotData.ReadFrom(buffer);

            Assert.Equal(ModifierSnapshotData.BinarySize, written);
            Assert.Equal(original.KeyPacked, restored.KeyPacked);
            Assert.Equal(original.Op, restored.Op);
            Assert.Equal(original.Priority, restored.Priority);
            Assert.Equal(original.SourceId, restored.SourceId);
            Assert.Equal(original.MagnitudeType, restored.MagnitudeType);
            Assert.Equal(original.BaseValue, restored.BaseValue);
            Assert.Equal(original.Coefficient, restored.Coefficient);
            Assert.Equal(original.DecayParams, restored.DecayParams);
            Assert.Equal(original.CurveDataIndex, restored.CurveDataIndex);
            Assert.Equal(original.SourceNameIndex, restored.SourceNameIndex);
        }

        [Fact]
        public void WriteTo_uses_little_endian_layout()
        {
            var snapshot = new ModifierSnapshotData
            {
                KeyPacked = 0x0A0B0C0Du,
                Op = ModifierOp.PercentAdd,
                Priority = 0x1234,
                SourceId = 0x01020304,
            };

            var buffer = new byte[32];
            snapshot.WriteTo(buffer);

            // Key @0..3 小端：0x0D 0x0C 0x0B 0x0A
            Assert.Equal(0x0D, buffer[0]);
            Assert.Equal(0x0C, buffer[1]);
            Assert.Equal(0x0B, buffer[2]);
            Assert.Equal(0x0A, buffer[3]);
            // Op @4
            Assert.Equal((byte)ModifierOp.PercentAdd, buffer[4]);
            // Priority @5..6 小端：0x34 0x12
            Assert.Equal(0x34, buffer[5]);
            Assert.Equal(0x12, buffer[6]);
            // SourceId @7..10 小端：04 03 02 01
            Assert.Equal(0x04, buffer[7]);
            Assert.Equal(0x03, buffer[8]);
            Assert.Equal(0x02, buffer[9]);
            Assert.Equal(0x01, buffer[10]);
        }

        [Fact]
        public void WriteTo_undersized_buffer_returns_zero()
        {
            var snapshot = new ModifierSnapshotData { KeyPacked = 1u };

            Assert.Equal(0, snapshot.WriteTo(new byte[31]));
        }

        [Fact]
        public void ReadFrom_undersized_buffer_returns_default()
        {
            var result = ModifierSnapshotData.ReadFrom(new byte[31]);

            Assert.Equal(0u, result.KeyPacked);
            Assert.Equal(default(ModifierOp), result.Op);
            Assert.Equal(0, result.SourceId);
        }

        [Fact]
        public void WriteBatch_readbatch_round_trips_all_entries()
        {
            var a = ModifierSnapshotData.FromModifierData(ModifierData.Add(TestKeys.Attack, 10f, sourceId: 1));
            var b = ModifierSnapshotData.FromModifierData(ModifierData.Mul(TestKeys.Speed, 1.5f, sourceId: 2));
            var c = ModifierSnapshotData.FromModifierData(ModifierData.Override(TestKeys.Health, 999f, sourceId: 3));

            var data = new[] { a, b, c };
            var buffer = new byte[data.Length * ModifierSnapshotData.BinarySize];
            int written = ModifierSnapshotData.WriteBatch(data, buffer);
            var restored = ModifierSnapshotData.ReadBatch(buffer, data.Length);

            Assert.Equal(data.Length * ModifierSnapshotData.BinarySize, written);
            Assert.Equal(data.Length, restored.Length);
            for (int i = 0; i < data.Length; i++)
            {
                Assert.Equal(data[i].KeyPacked, restored[i].KeyPacked);
                Assert.Equal(data[i].Op, restored[i].Op);
                Assert.Equal(data[i].SourceId, restored[i].SourceId);
                Assert.Equal(data[i].BaseValue, restored[i].BaseValue);
            }
        }

        [Fact]
        public void WriteBatch_undersized_buffer_returns_zero()
        {
            var data = new[] { new ModifierSnapshotData(), new ModifierSnapshotData() };

            Assert.Equal(0, ModifierSnapshotData.WriteBatch(data, new byte[31]));
        }

        [Fact]
        public void ReadBatch_undersized_buffer_returns_empty()
        {
            var result = ModifierSnapshotData.ReadBatch(new byte[10], 2);

            Assert.Empty(result);
        }

        [Fact]
        public void Curve_array_data_is_not_serialized()
        {
            // 有损字段：ArrayData（等级曲线/自定义衰减曲线）不进入 32 字节快照。
            var curve = new float[] { 1f, 1f, 5f, 5f };
            var original = ModifierData.Add(TestKeys.Attack, MagnitudeSource.LevelCurve(10f, curve));
            var snapshot = ModifierSnapshotData.FromModifierData(original);
            var restored = snapshot.ToModifierData();

            Assert.Equal(0, snapshot.CurveDataIndex);
            Assert.Null(restored.Magnitude.ArrayData);
            Assert.NotEqual(original.Magnitude.ArrayData, restored.Magnitude.ArrayData);
        }

        [Fact]
        public void Pipeline_data_is_not_serialized()
        {
            var pipeline = new ModifierPipeline(new LevelCurveModifier(10f, new[] { 1f, 1f, 5f, 5f }));
            var original = ModifierData.AddWithPipeline(TestKeys.Attack, pipeline);
            var snapshot = ModifierSnapshotData.FromModifierData(original);
            var restored = snapshot.ToModifierData();

            Assert.True(restored.Magnitude.PipelineData.IsEmpty);
        }

        [Fact]
        public void Priority_outside_short_range_truncates()
        {
            // 有损字段：Priority 是 int，快照只存 short。超出 [-32768, 32767] 会被截断。
            // 钉住该现状，业务层必须保证 priority 在 short 范围内。
            var original = ModifierData.Add(TestKeys.Attack, 1f, priority: 70000);
            var snapshot = ModifierSnapshotData.FromModifierData(original);
            var restored = snapshot.ToModifierData();

            Assert.Equal((short)4464, snapshot.Priority); // 70000 - 65536 = 4464
            Assert.Equal(4464, restored.Priority);
            Assert.NotEqual(original.Priority, restored.Priority);
        }
    }
}
