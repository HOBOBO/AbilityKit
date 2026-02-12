using MemoryPack;

namespace AbilityKit.Ability.Host.Extensions.Moba.RoomSync
{
    [MemoryPackable]
    public readonly partial struct MobaRoomRequestSnapshotMessage
    {
        [MemoryPackOrder(0)] public readonly string ClientId;
        [MemoryPackOrder(1)] public readonly int LastSeenRevision;

        [MemoryPackConstructor]
        public MobaRoomRequestSnapshotMessage(string clientId, int lastSeenRevision)
        {
            ClientId = clientId;
            LastSeenRevision = lastSeenRevision;
        }
    }
}
