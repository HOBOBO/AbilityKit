using AbilityKit.Demo.Moba.Rollback;
using MemoryPack;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.Minions;

public sealed class MobaMinionRollbackProviderTests
{
    [Fact]
    public void Payload_round_trips_long_gameplay_id_and_sorted_state()
    {
        var payload = new MobaMinionRollbackPayload(
            version: 1,
            gameplayId: 4_294_967_301L,
            committedWaveCount: 3,
            nextWaveOrdinal: 4,
            lastFrame: 120,
            elapsedMs: 15_000L,
            pending: new[]
            {
                new MobaMinionPendingRollbackEntry(10L, 1, 1, 2_000L, 0),
                new MobaMinionPendingRollbackEntry(10L, 2, 1, 2_000L, 1),
            },
            active: new[]
            {
                new MobaMinionActiveRollbackEntry(100, 10L, 1, 1, 0, 1, 2, false, -1L, false, 0),
            },
            cooldowns: new[]
            {
                new MobaMinionCooldownRollbackEntry(100, 16_000L),
            },
            reportedDeaths: new[] { 90, 91 });

        var restored = MobaMinionRollbackProvider.ValidatePayload(
            MemoryPackSerializer.Serialize(payload));

        Assert.Equal(payload.GameplayId, restored.GameplayId);
        Assert.Equal(payload.Pending[1].Team, restored.Pending[1].Team);
        Assert.Equal(payload.Active[0].ActorId, restored.Active[0].ActorId);
        Assert.Equal(payload.Cooldowns[0].NextAttackAtMs, restored.Cooldowns[0].NextAttackAtMs);
        Assert.Equal(payload.ReportedDeaths, restored.ReportedDeaths);
    }

    [Fact]
    public void Payload_rejects_unsupported_version()
    {
        var valid = CreateValidPayload();
        var payload = new MobaMinionRollbackPayload(2, valid.GameplayId,
            valid.CommittedWaveCount, valid.NextWaveOrdinal, valid.LastFrame, valid.ElapsedMs,
            valid.Pending, valid.Active, valid.Cooldowns, valid.ReportedDeaths);

        Assert.Throws<InvalidOperationException>(() =>
            MobaMinionRollbackProvider.ValidatePayload(MemoryPackSerializer.Serialize(payload)));
    }

    [Fact]
    public void Payload_rejects_unsorted_pending_entries()
    {
        var valid = CreateValidPayload();
        var payload = new MobaMinionRollbackPayload(1, valid.GameplayId,
            valid.CommittedWaveCount, valid.NextWaveOrdinal, valid.LastFrame, valid.ElapsedMs,
            new[]
            {
                new MobaMinionPendingRollbackEntry(10L, 1, 1, 3_000L, 0),
                new MobaMinionPendingRollbackEntry(10L, 2, 1, 2_000L, 1),
            }, valid.Active, valid.Cooldowns, valid.ReportedDeaths);

        Assert.Throws<InvalidOperationException>(() =>
            MobaMinionRollbackProvider.ValidatePayload(MemoryPackSerializer.Serialize(payload)));
    }

    [Fact]
    public void Payload_rejects_duplicate_actor_or_death_entries()
    {
        var valid = CreateValidPayload();
        var duplicateActor = new MobaMinionRollbackPayload(1, valid.GameplayId,
            valid.CommittedWaveCount, valid.NextWaveOrdinal, valid.LastFrame, valid.ElapsedMs,
            valid.Pending,
            new[]
            {
                new MobaMinionActiveRollbackEntry(100, 10L, 1, 1, 0, 1, 0, false, -1L, false, 0),
                new MobaMinionActiveRollbackEntry(100, 10L, 1, 1, 1, 1, 0, false, -1L, false, 0),
            }, valid.Cooldowns, valid.ReportedDeaths);
        var duplicateDeath = new MobaMinionRollbackPayload(1, valid.GameplayId,
            valid.CommittedWaveCount, valid.NextWaveOrdinal, valid.LastFrame, valid.ElapsedMs,
            valid.Pending, valid.Active, valid.Cooldowns, new[] { 90, 90 });

        Assert.Throws<InvalidOperationException>(() =>
            MobaMinionRollbackProvider.ValidatePayload(MemoryPackSerializer.Serialize(duplicateActor)));
        Assert.Throws<InvalidOperationException>(() =>
            MobaMinionRollbackProvider.ValidatePayload(MemoryPackSerializer.Serialize(duplicateDeath)));
    }

    [Fact]
    public void Provider_key_does_not_overlap_continuous_capture_provider()
    {
        Assert.NotEqual(
            MobaContinuousModifierCaptureRollbackProvider.DefaultKey,
            MobaMinionRollbackProvider.DefaultKey);
    }

    private static MobaMinionRollbackPayload CreateValidPayload()
    {
        return new MobaMinionRollbackPayload(
            1, 9L, 1, 2, 10, 1_000L,
            new[] { new MobaMinionPendingRollbackEntry(10L, 1, 1, 2_000L, 0) },
            new[] { new MobaMinionActiveRollbackEntry(100, 10L, 1, 1, 0, 1, 0, false, -1L, false, 0) },
            new[] { new MobaMinionCooldownRollbackEntry(100, 1_500L) },
            new[] { 90 });
    }
}
