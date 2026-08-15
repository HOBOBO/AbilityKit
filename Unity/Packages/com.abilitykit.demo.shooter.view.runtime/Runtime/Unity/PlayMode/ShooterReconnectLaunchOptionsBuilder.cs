#nullable enable

namespace AbilityKit.Demo.Shooter.View.PlayMode
{
    internal static class ShooterReconnectLaunchOptionsBuilder
    {
        public static ShooterRemoteStateSyncLaunchOptions RestoreOnly(
            ShooterRemoteStateSyncLaunchOptions source,
            string roomId)
        {
            return new ShooterRemoteStateSyncLaunchOptions(
                source.SessionOptions,
                source.Endpoint,
                source.SessionToken,
                source.Region,
                source.ServerId,
                ShooterRemoteStateSyncLaunchMode.RestoreOnly,
                source.Timeout,
                string.IsNullOrWhiteSpace(roomId) ? source.RoomId : roomId,
                source.RoomLaunchSpec,
                source.ReliableEventCheckpointStore,
                source.ReliableEventCheckpointLifecycleOptions);
        }
    }
}
