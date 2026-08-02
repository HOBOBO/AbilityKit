namespace AbilityKit.Ability.Host.Extensions.Moba.RoomSync
{
    public interface IMobaRoomSyncServerOutbox
    {
        bool TryDequeue(out MobaRoomSnapshotMessage snapshot);

        int Count { get; }

        void Clear();
    }
}
