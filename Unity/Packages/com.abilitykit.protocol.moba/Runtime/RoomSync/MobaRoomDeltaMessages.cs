using AbilityKit.Ability.Host;
using AbilityKit.Ability.Host.Extensions.Moba.Room;
using MemoryPack;

namespace AbilityKit.Ability.Host.Extensions.Moba.RoomSync
{
    [MemoryPackable]
    public readonly partial struct MobaRoomChangedMessage
    {
        [MemoryPackOrder(0)] public readonly MobaRoomChangeKind Kind;
        [MemoryPackOrder(1)] public readonly PlayerId PlayerId;
        [MemoryPackOrder(2)] public readonly int Revision;

        [MemoryPackConstructor]
        public MobaRoomChangedMessage(MobaRoomChangeKind kind, PlayerId playerId, int revision)
        {
            Kind = kind;
            PlayerId = playerId;
            Revision = revision;
        }

        public static MobaRoomChangedMessage FromArgs(in MobaRoomChangedArgs args)
            => new MobaRoomChangedMessage(args.Kind, args.PlayerId, args.Revision);
    }
}
