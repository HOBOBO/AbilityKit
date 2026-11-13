using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.FrameSync.Rollback;
using AbilityKit.Ability.World.Services;
using AbilityKit.Demo.Moba.Rollback;
using AbilityKit.Demo.Moba.Services;
using MemoryPack;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.StateSync;

public sealed class MobaCombatClockRollbackTests
{
    private static readonly FrameIndex Frame = new(19);

    [Theory]
    [InlineData(1, 2)]
    [InlineData(8, 7)]
    public void Combat_restore_preserves_absence_zero_bits_and_replaces_predicted_map(int source, int target)
    {
        var clock = new WorldClock();
        var combat = new MobaCombatActivityService(clock);
        var actors = Actors(source, target, 20);
        var provider = new MobaCombatActivityRollbackProvider(combat, actors);
        var empty = provider.Export(Frame);
        combat.RecordCombat(source); // Present at exactly zero, distinct from absence.
        clock.Tick(0.1f);
        clock.Tick(0.2f);
        combat.RecordCombat(target);
        var targetTime = clock.Time;
        var checkpoint = provider.Export(Frame);
        Assert.False(combat.IsOutOfCombat(target, 1f));
        combat.RemoveActor(source);
        clock.Tick(10f);
        combat.RecordCombat(target);
        combat.RecordCombat(20);
        var clockTime = clock.Time;

        provider.ValidateImport(Frame, checkpoint);
        Assert.True(combat.TryGetLastCombatTime(20, out _)); // Preflight is read-only.
        for (var i = 0; i < 2; i++)
        {
            provider.Import(Frame, checkpoint);
            Assert.True(combat.TryGetLastCombatTime(source, out var zero));
            Assert.Equal(0, BitConverter.SingleToInt32Bits(zero));
            Assert.True(combat.TryGetLastCombatTime(target, out var actual));
            Assert.Equal(BitConverter.SingleToInt32Bits(targetTime), BitConverter.SingleToInt32Bits(actual));
            Assert.False(combat.TryGetLastCombatTime(20, out _));
            Assert.Equal(clockTime, clock.Time); // This owner never restores the clock.
        }
        clock.RestoreState(targetTime, 0.2f);
        Assert.Equal(checkpoint, provider.Export(Frame));
        clock.RestoreState(1.5f, 0f);
        Assert.True(combat.IsOutOfCombat(target, 1f));
        provider.Import(Frame, empty);
        Assert.False(combat.TryGetLastCombatTime(source, out _));
        Assert.False(combat.TryGetLastCombatTime(target, out _));
        Assert.True(combat.IsOutOfCombat(target, 100f));
    }

    public static IEnumerable<object[]> InvalidCombatStates()
    {
        var roster = new[] { 1, 2 };
        var valid = new[] { new MobaCombatActivityRollbackEntry(1, 0.25f) };
        yield return new object[] { new MobaCombatActivityRollbackPayload(2, 19, 1f, roster, valid) };
        yield return new object[] { new MobaCombatActivityRollbackPayload(1, 18, 1f, roster, valid) };
        yield return new object[] { new MobaCombatActivityRollbackPayload(1, 19, float.NaN, roster, valid) };
        yield return new object[] { new MobaCombatActivityRollbackPayload(1, 19, -1f, roster, valid) };
        yield return new object[] { new MobaCombatActivityRollbackPayload(1, 19, 1f, null!, valid) };
        yield return new object[] { new MobaCombatActivityRollbackPayload(1, 19, 1f, roster, null!) };
        yield return new object[] { new MobaCombatActivityRollbackPayload(1, 19, 1f, new[] { 2, 1 }, valid) };
        yield return new object[] { new MobaCombatActivityRollbackPayload(1, 19, 1f, new[] { 1, 1 }, valid) };
        yield return new object[] { new MobaCombatActivityRollbackPayload(1, 19, 1f, new[] { 1, 3 }, valid) };
        foreach (var time in new[] { -1f, float.NaN, float.PositiveInfinity, float.NegativeInfinity, 1.01f })
            yield return new object[] { new MobaCombatActivityRollbackPayload(1, 19, 1f, roster,
                new[] { new MobaCombatActivityRollbackEntry(1, time) }) };
        foreach (var ids in new[] { new[] { 1, 1 }, new[] { 2, 1 }, new[] { 0, 1 }, new[] { 1, 3 } })
            yield return new object[] { new MobaCombatActivityRollbackPayload(1, 19, 1f, roster,
                ids.Select(id => new MobaCombatActivityRollbackEntry(id, 0f)).ToArray()) };
    }

    [Theory]
    [MemberData(nameof(InvalidCombatStates))]
    public void Invalid_combat_payload_rejects_before_mutation(MobaCombatActivityRollbackPayload state)
    {
        var clock = new WorldClock();
        clock.Tick(2f);
        var combat = new MobaCombatActivityService(clock);
        combat.RecordCombat(2);
        var provider = new MobaCombatActivityRollbackProvider(combat, Actors(1, 2));
        var before = provider.Export(Frame);
        var invalid = MemoryPackSerializer.Serialize(state);
        Assert.ThrowsAny<Exception>(() => provider.ValidateImport(Frame, invalid));
        Assert.ThrowsAny<Exception>(() => provider.Import(Frame, invalid));
        Assert.Equal(before, provider.Export(Frame));
    }

    [Fact]
    public void Roster_drift_and_corrupt_payloads_reject_without_mutation()
    {
        var clock = new WorldClock();
        var combat = new MobaCombatActivityService(clock);
        var actors = Actors(1, 2);
        var provider = new MobaCombatActivityRollbackProvider(combat, actors);
        combat.RecordCombat(1);
        var snapshot = provider.Export(Frame);
        actors.Unregister(2);
        Assert.Throws<InvalidOperationException>(() => provider.Import(Frame, snapshot));
        actors.Register(2, new ActorContext().CreateEntity());
        foreach (var invalid in new[] { Array.Empty<byte>(), new byte[] { 255 }, snapshot[..^1], snapshot.Concat(new byte[] { 1 }).ToArray() })
        {
            Assert.ThrowsAny<Exception>(() => provider.ValidateImport(Frame, invalid));
            Assert.ThrowsAny<Exception>(() => provider.Import(Frame, invalid));
            Assert.Equal(snapshot, provider.Export(Frame));
        }
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(0.033333335f)]
    public void Clock_restore_is_exact_repeatable_and_next_tick_uses_restored_time(float lastDelta)
    {
        var clock = new WorldClock();
        for (var i = 0; i < 19; i++) clock.Tick(1f / 30f);
        clock.Tick(lastDelta);
        var time = clock.Time;
        var provider = new MobaWorldClockRollbackProvider(clock);
        var snapshot = provider.Export(Frame);
        clock.Tick(7f);
        provider.ValidateImport(Frame, snapshot);
        Assert.True(clock.Time > time);
        for (var i = 0; i < 2; i++)
        {
            provider.Import(Frame, snapshot);
            Assert.Equal(BitConverter.SingleToInt32Bits(time), BitConverter.SingleToInt32Bits(clock.Time));
            Assert.Equal(BitConverter.SingleToInt32Bits(lastDelta), BitConverter.SingleToInt32Bits(clock.DeltaTime));
            Assert.Equal(snapshot, provider.Export(Frame));
        }
        clock.Tick(1f / 30f);
        Assert.Equal(BitConverter.SingleToInt32Bits(time + 1f / 30f), BitConverter.SingleToInt32Bits(clock.Time));
    }

    [Theory]
    [InlineData(-1f)]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    public void Clock_invalid_values_do_not_partially_assign(float invalid)
    {
        var clock = new WorldClock();
        clock.Tick(2f);
        var provider = new MobaWorldClockRollbackProvider(clock);
        var before = provider.Export(Frame);
        Assert.Throws<ArgumentOutOfRangeException>(() => clock.RestoreState(invalid, 1f));
        Assert.Throws<ArgumentOutOfRangeException>(() => clock.RestoreState(1f, invalid));
        foreach (var state in new[] { new MobaWorldClockRollbackPayload(1, 19, invalid, 0f),
                     new MobaWorldClockRollbackPayload(1, 19, 0f, invalid) })
        {
            var bytes = MemoryPackSerializer.Serialize(state);
            Assert.Throws<InvalidOperationException>(() => provider.ValidateImport(Frame, bytes));
            Assert.Throws<InvalidOperationException>(() => provider.Import(Frame, bytes));
        }
        Assert.Equal(before, provider.Export(Frame));
    }

    [Fact]
    public void Clock_invalid_format_version_frame_or_trailing_bytes_reject()
    {
        var clock = new WorldClock();
        clock.Tick(2f);
        var provider = new MobaWorldClockRollbackProvider(clock);
        var before = provider.Export(Frame);
        foreach (var bytes in new[] { Array.Empty<byte>(), new byte[] { 255 }, before[..^1],
                     before.Concat(new byte[] { 1 }).ToArray(),
                     MemoryPackSerializer.Serialize(new MobaWorldClockRollbackPayload(2, 19, 0f, 0f)),
                     MemoryPackSerializer.Serialize(new MobaWorldClockRollbackPayload(1, 18, 0f, 0f)) })
        {
            Assert.ThrowsAny<Exception>(() => provider.ValidateImport(Frame, bytes));
            Assert.ThrowsAny<Exception>(() => provider.Import(Frame, bytes));
            Assert.Equal(before, provider.Export(Frame));
        }
    }

    private static MobaActorRegistry Actors(params int[] ids)
    {
        var actors = new MobaActorRegistry();
        var context = new ActorContext();
        foreach (var id in ids)
        {
            var actor = context.CreateEntity();
            actor.AddActorId(id);
            actors.Register(id, actor);
        }
        return actors;
    }
}
