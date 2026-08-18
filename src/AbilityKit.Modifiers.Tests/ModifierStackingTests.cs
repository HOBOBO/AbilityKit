using AbilityKit.Modifiers;
using Xunit;

namespace AbilityKit.Modifiers.Tests
{
    /// <summary>
    /// ModifierStacking 堆叠语义测试。
    /// Aggregate：层数叠加，计算语义为「单层值 × 层数」；
    /// Exclusive：同组仅一层，新条目替换旧条目。
    /// </summary>
    public sealed class ModifierStackingTests
    {
        [Fact]
        public void CreateAggregate_sets_config_and_initial_count()
        {
            var key = ModifierKey.Create(5, 0, 0);
            var stacking = ModifierStacking.CreateAggregate(key, maxStack: 3, initialCount: 2);

            Assert.Equal(StackingType.Aggregate, stacking.Config.Type);
            Assert.Equal(key, stacking.Config.StackKey);
            Assert.Equal(3, stacking.Config.MaxStackCount);
            Assert.Equal(2, stacking.StackCount);
        }

        [Fact]
        public void CreateAggregate_defaults_are_single_stack()
        {
            var stacking = ModifierStacking.CreateAggregate(ModifierKey.Create(5, 0, 0));
            Assert.Equal(1, stacking.StackCount);
            Assert.Equal(1, stacking.Config.MaxStackCount);
            Assert.Equal(default(ModifierData), stacking.Entry);
        }

        [Fact]
        public void CreateExclusive_sets_max_one()
        {
            var stacking = ModifierStacking.CreateExclusive(ModifierKey.Create(5, 1, 0));
            Assert.Equal(StackingType.Exclusive, stacking.Config.Type);
            Assert.Equal(1, stacking.Config.MaxStackCount);
            Assert.Equal(1, stacking.StackCount);
        }

        [Fact]
        public void TryPush_aggregate_increments_count_until_max()
        {
            var entry = ModifierData.Add(TestKeys.Attack, 10f);
            var stacking = ModifierStacking.CreateAggregate(ModifierKey.Create(5, 0, 0), maxStack: 3, initialCount: 1, entry: entry);

            Assert.True(stacking.TryPush(entry));
            Assert.Equal(2, stacking.StackCount);
            Assert.True(stacking.TryPush(entry));
            Assert.Equal(3, stacking.StackCount);
        }

        [Fact]
        public void TryPush_aggregate_at_max_returns_false_and_keeps_count()
        {
            var entry = ModifierData.Add(TestKeys.Attack, 10f);
            var stacking = ModifierStacking.CreateAggregate(ModifierKey.Create(5, 0, 0), maxStack: 2, initialCount: 2, entry: entry);

            Assert.False(stacking.TryPush(entry));
            Assert.Equal(2, stacking.StackCount);
        }

        [Fact]
        public void TryPush_aggregate_does_not_store_new_entry_data_documents_current_behavior()
        {
            // 钉当前行为（可疑）：Aggregate 模式 TryPush 只加层数，不替换 Entry 数据 ——
            // 即使传入不同条目，生效值仍是创建时的 Entry。
            var original = ModifierData.Add(TestKeys.Attack, 10f);
            var different = ModifierData.Add(TestKeys.Attack, 99f);
            var stacking = ModifierStacking.CreateAggregate(ModifierKey.Create(5, 0, 0), maxStack: 5, initialCount: 1, entry: original);

            Assert.True(stacking.TryPush(different));
            Assert.Equal(10f, stacking.Entry.Magnitude.BaseValue);
        }

        [Fact]
        public void TryPush_exclusive_when_full_returns_false_and_keeps_entry()
        {
            // 创建即满（count=1, max=1），Exclusive 的"替换"分支需要先 Pop 腾出层数才可达。
            var original = ModifierData.Add(TestKeys.Attack, 10f);
            var stacking = ModifierStacking.CreateExclusive(ModifierKey.Create(5, 1, 0), entry: original);

            Assert.False(stacking.TryPush(ModifierData.Add(TestKeys.Attack, 20f)));
            Assert.Equal(10f, stacking.Entry.Magnitude.BaseValue);
            Assert.Equal(1, stacking.StackCount);
        }

        [Fact]
        public void TryPush_exclusive_after_pop_replaces_entry()
        {
            var original = ModifierData.Add(TestKeys.Attack, 10f);
            var stacking = ModifierStacking.CreateExclusive(ModifierKey.Create(5, 1, 0), entry: original);

            stacking.TryPop(); // count 1 → 0，Entry 清空

            var replacement = ModifierData.Mul(TestKeys.Attack, 2f);
            Assert.True(stacking.TryPush(replacement));
            Assert.Equal(1, stacking.StackCount);
            Assert.Equal(ModifierOp.Mul, stacking.Entry.Op);
            Assert.Equal(2f, stacking.Entry.Magnitude.BaseValue);
        }

        [Fact]
        public void TryPop_aggregate_decrements_and_signals_remaining()
        {
            var stacking = ModifierStacking.CreateAggregate(ModifierKey.Create(5, 0, 0), maxStack: 3, initialCount: 3);
            Assert.True(stacking.TryPop());  // 3→2，还有剩余
            Assert.True(stacking.TryPop());  // 2→1，还有剩余
            Assert.False(stacking.TryPop()); // 1→0，没有剩余
            Assert.Equal(0, stacking.StackCount);
        }

        [Fact]
        public void TryPop_aggregate_at_zero_returns_false()
        {
            var stacking = ModifierStacking.CreateAggregate(ModifierKey.Create(5, 0, 0), maxStack: 2, initialCount: 0);
            Assert.False(stacking.TryPop());
            Assert.Equal(0, stacking.StackCount);
        }

        [Fact]
        public void TryPop_exclusive_clears_entry()
        {
            var entry = ModifierData.Add(TestKeys.Attack, 10f);
            var stacking = ModifierStacking.CreateExclusive(ModifierKey.Create(5, 1, 0), entry: entry);

            stacking.TryPop();
            Assert.Equal(0, stacking.StackCount);
            Assert.Equal(default, stacking.Entry);
        }

        [Fact]
        public void TryPop_aggregate_keeps_entry_data()
        {
            var entry = ModifierData.Add(TestKeys.Attack, 10f);
            var stacking = ModifierStacking.CreateAggregate(ModifierKey.Create(5, 0, 0), maxStack: 3, initialCount: 2, entry: entry);

            stacking.TryPop();
            Assert.Equal(10f, stacking.Entry.Magnitude.BaseValue); // Entry 保留，仅层数减少
        }

        [Fact]
        public void Clear_resets_count_and_entry()
        {
            var entry = ModifierData.Add(TestKeys.Attack, 10f);
            var stacking = ModifierStacking.CreateAggregate(ModifierKey.Create(5, 0, 0), maxStack: 3, initialCount: 3, entry: entry);

            stacking.Clear();
            Assert.Equal(0, stacking.StackCount);
            Assert.Equal(default, stacking.Entry);
        }

        [Fact]
        public void ExpandTo_exclusive_writes_single_entry()
        {
            var entry = ModifierData.Add(TestKeys.Attack, 10f);
            var stacking = ModifierStacking.CreateExclusive(ModifierKey.Create(5, 1, 0), entry: entry);
            var buffer = new ModifierData[4];

            int written = stacking.ExpandTo(buffer);
            Assert.Equal(1, written);
            Assert.Equal(entry, buffer[0]);
        }

        [Fact]
        public void ExpandTo_aggregate_writes_stack_count_copies()
        {
            var entry = ModifierData.Add(TestKeys.Attack, 10f);
            var stacking = ModifierStacking.CreateAggregate(ModifierKey.Create(5, 0, 0), maxStack: 3, initialCount: 3, entry: entry);
            var buffer = new ModifierData[4];

            int written = stacking.ExpandTo(buffer);
            Assert.Equal(3, written);
            Assert.Equal(entry, buffer[0]);
            Assert.Equal(entry, buffer[1]);
            Assert.Equal(entry, buffer[2]);
            Assert.Equal(default, buffer[3]);
        }

        [Fact]
        public void ExpandTo_zero_stacks_returns_zero()
        {
            var stacking = ModifierStacking.CreateAggregate(ModifierKey.Create(5, 0, 0), maxStack: 3, initialCount: 0);
            Assert.Equal(0, stacking.ExpandTo(new ModifierData[4]));
        }

        [Fact]
        public void ExpandTo_aggregate_smaller_buffer_returns_full_count_documents_current_behavior()
        {
            // 钉当前行为（可疑）：缓冲区不足时只写满缓冲区长度，但返回值仍是完整 StackCount，
            // 调用方若用返回值切片会读到未初始化条目。
            var entry = ModifierData.Add(TestKeys.Attack, 10f);
            var stacking = ModifierStacking.CreateAggregate(ModifierKey.Create(5, 0, 0), maxStack: 5, initialCount: 3, entry: entry);

            int written = stacking.ExpandTo(new ModifierData[2]);
            Assert.Equal(3, written); // 大于实际写入的 2 条
        }

        [Fact]
        public void CalculateStackedValue_add_is_base_plus_layer_times_count()
        {
            var entry = ModifierData.Add(TestKeys.Attack, 10f);
            var stacking = ModifierStacking.CreateAggregate(ModifierKey.Create(5, 0, 0), maxStack: 5, initialCount: 3, entry: entry);
            FloatAssert.Near(130f, stacking.CalculateStackedValue(100f)); // 100 + 10×3
        }

        [Fact]
        public void CalculateStackedValue_mul_multiplies_sum_of_multipliers_not_power()
        {
            // 钉语义：3 层 ×2 → base × (2×3) = ×6，而不是 2^3 = 8（多层乘法按"和"叠加）。
            var entry = ModifierData.Mul(TestKeys.Attack, 2f);
            var stacking = ModifierStacking.CreateAggregate(ModifierKey.Create(5, 0, 0), maxStack: 5, initialCount: 3, entry: entry);
            FloatAssert.Near(600f, stacking.CalculateStackedValue(100f));
        }

        [Fact]
        public void CalculateStackedValue_percent_add_is_base_times_one_plus_sum()
        {
            var entry = ModifierData.PercentAdd(TestKeys.Attack, 0.1f);
            var stacking = ModifierStacking.CreateAggregate(ModifierKey.Create(5, 0, 0), maxStack: 5, initialCount: 3, entry: entry);
            FloatAssert.Near(130f, stacking.CalculateStackedValue(100f)); // 100 × (1 + 0.1×3)
        }

        [Fact]
        public void CalculateStackedValue_override_ignores_stack_count()
        {
            var entry = ModifierData.Override(TestKeys.Attack, 42f);
            var stacking = ModifierStacking.CreateAggregate(ModifierKey.Create(5, 0, 0), maxStack: 5, initialCount: 4, entry: entry);
            FloatAssert.Near(42f, stacking.CalculateStackedValue(100f));
        }

        [Fact]
        public void CalculateStackedValue_zero_stacks_returns_base()
        {
            var stacking = ModifierStacking.CreateAggregate(ModifierKey.Create(5, 0, 0), maxStack: 5, initialCount: 0);
            FloatAssert.Near(100f, stacking.CalculateStackedValue(100f));
        }

        [Fact]
        public void StackingConfig_to_string_contains_type_key_and_max()
        {
            var config = new StackingConfig
            {
                Type = StackingType.Aggregate,
                StackKey = ModifierKey.Create(1, 0, 0),
                MaxStackCount = 5,
            };
            Assert.Equal("Aggregate[ModifierKey(1.0.0)] x5", config.ToString());
        }
    }
}
