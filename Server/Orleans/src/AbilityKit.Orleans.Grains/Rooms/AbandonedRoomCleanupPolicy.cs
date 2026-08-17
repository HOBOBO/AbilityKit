using AbilityKit.Orleans.Contracts.Rooms;
using AbilityKit.Orleans.Grains.Persistence;

namespace AbilityKit.Orleans.Grains.Rooms;

internal static class AbandonedRoomCleanupPolicy
{
    public static readonly TimeSpan GracePeriod = TimeSpan.FromMinutes(1);

    public static bool TryGetCleanupDeadlineTicks(
        RoomPersistentState state,
        out long cleanupDeadlineTicks)
    {
        ArgumentNullException.ThrowIfNull(state);
        cleanupDeadlineTicks = 0L;

        if (state.Phase is RoomPhase.Closing or RoomPhase.Closed or RoomPhase.Expired)
        {
            return false;
        }

        var clients = state.Members
            .Where(member => !member.State.IsBot)
            .ToArray();
        if (clients.Length == 0 || clients.Any(member =>
                member.State.IsOnline || member.State.OfflineSinceTicks <= 0))
        {
            return false;
        }

        var allClientsOfflineSinceTicks = clients.Max(member => member.State.OfflineSinceTicks);
        cleanupDeadlineTicks = allClientsOfflineSinceTicks > long.MaxValue - GracePeriod.Ticks
            ? long.MaxValue
            : allClientsOfflineSinceTicks + GracePeriod.Ticks;
        return true;
    }

    public static bool ShouldCleanup(RoomPersistentState state, long nowTicks)
    {
        return TryGetCleanupDeadlineTicks(state, out var deadlineTicks) &&
            nowTicks >= deadlineTicks;
    }
}
