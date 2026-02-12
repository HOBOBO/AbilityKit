using MemoryPack;

namespace AbilityKit.Ability.Host.Extensions.Moba.RoomSync
{
    public enum MobaRoomSyncMessageKind
    {
        Unknown = 0,
        Hello = 1,
        Snapshot = 2,
        Command = 3,
        CommandResult = 4,
    }

    [MemoryPackable]
    public readonly partial struct MobaRoomHello
    {
        [MemoryPackOrder(0)] public readonly string ClientId;
        [MemoryPackOrder(1)] public readonly int LastSeenRevision;
        [MemoryPackOrder(2)] public readonly int LastAckClientSeq;

        [MemoryPackConstructor]
        public MobaRoomHello(string clientId, int lastSeenRevision, int lastAckClientSeq)
        {
            ClientId = clientId;
            LastSeenRevision = lastSeenRevision;
            LastAckClientSeq = lastAckClientSeq;
        }
    }
}
