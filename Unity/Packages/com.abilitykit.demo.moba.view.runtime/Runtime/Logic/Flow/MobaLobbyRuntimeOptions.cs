using System;

namespace AbilityKit.Game.Flow
{
    [Serializable]
    internal sealed class MobaLobbyRuntimeOptions
    {
        public int Version = 1;
        public int RoomNoticeDurationMilliseconds = 6000;
        public int RoomListRefreshMilliseconds = 3000;
        public int SnapshotRecoveryMilliseconds = 5000;

        public static MobaLobbyRuntimeOptions CreateDefault() => new MobaLobbyRuntimeOptions();

        public void ValidateOrThrow()
        {
            if (Version != 1)
                throw new InvalidOperationException("Unsupported lobby runtime options version: " + Version);
            ValidateInterval(RoomNoticeDurationMilliseconds, nameof(RoomNoticeDurationMilliseconds));
            ValidateInterval(RoomListRefreshMilliseconds, nameof(RoomListRefreshMilliseconds));
            ValidateInterval(SnapshotRecoveryMilliseconds, nameof(SnapshotRecoveryMilliseconds));
        }

        private static void ValidateInterval(int value, string name)
        {
            if (value < 100 || value > 60000)
                throw new InvalidOperationException("Lobby runtime interval is out of range: " + name);
        }
    }
}
