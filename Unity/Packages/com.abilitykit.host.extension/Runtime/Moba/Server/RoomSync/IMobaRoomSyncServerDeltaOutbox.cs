namespace AbilityKit.Ability.Host.Extensions.Moba.RoomSync
{
    public interface IMobaRoomSyncServerDeltaOutbox
    {
        bool TryDequeue(out MobaRoomChangedMessage delta);

        int Count { get; }

        void Clear();
    }
}
