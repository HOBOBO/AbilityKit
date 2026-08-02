namespace AbilityKit.Ability.Host.Extensions.Moba.RoomSync
{
    public interface IMobaRoomSyncClient
    {
        void ApplySnapshot(in MobaRoomSnapshotMessage snapshot);

        void ApplyCommandResult(in MobaRoomCommandResultMessage result);

        void ApplyDelta(in MobaRoomChangedMessage delta);

        bool TryBuildRequestSnapshot(string clientId, out MobaRoomRequestSnapshotMessage request);
    }
}
