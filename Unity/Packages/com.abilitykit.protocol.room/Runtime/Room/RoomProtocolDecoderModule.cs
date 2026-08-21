#nullable enable

using System;
using AbilityKit.Protocol.Catalog;

namespace AbilityKit.Protocol.Room
{
    /// <summary>
    /// Installs the shared Room wire decoders into an application-owned registry. The module
    /// contains no network or editor dependency and is safe to invoke repeatedly.
    /// </summary>
    public static class RoomProtocolDecoderModule
    {
        public const string CatalogId = "abilitykit.room";

        public static void Register(ProtocolPayloadDecoderRegistry registry)
        {
            if (registry == null) throw new ArgumentNullException(nameof(registry));

            Register<WireRoomGuestLoginReq>(registry, "guest-login.request");
            Register<WireRoomGuestLoginRes>(registry, "guest-login.response");
            Register<WireRoomAccountLoginReq>(registry, "account-login.request");
            Register<WireRoomAccountLoginRes>(registry, "account-login.response");
            Register<WireRenewSessionReq>(registry, "renew-session.request");
            Register<WireRenewSessionRes>(registry, "renew-session.response");
            Register<WireCreateRoomReq>(registry, "create-room.request");
            Register<WireCreateRoomRes>(registry, "create-room.response");
            Register<WireJoinRoomReq>(registry, "join-room.request");
            Register<WireJoinRoomRes>(registry, "join-room.response");
            Register<WireRestoreRoomReq>(registry, "restore-room.request");
            Register<WireRestoreRoomRes>(registry, "restore-room.response");
            Register<WireListRoomsReq>(registry, "list-rooms.request");
            Register<WireListRoomsRes>(registry, "list-rooms.response");
            Register<WireRoomReadyReq>(registry, "set-ready.request");
            Register<WireRoomSnapshotRes>(registry, "set-ready.response");
            Register<WireRoomPickHeroReq>(registry, "pick-hero.request");
            Register<WireRoomSnapshotRes>(registry, "pick-hero.response");
            Register<WireStartRoomBattleReq>(registry, "start-battle.request");
            Register<WireStartRoomBattleRes>(registry, "start-battle.response");
            Register<WireSubmitBattleInputReq>(registry, "submit-battle-input.request");
            Register<WireSubmitBattleInputRes>(registry, "submit-battle-input.response");
            Register<WireSubscribeStateSyncReq>(registry, "subscribe-state-sync.request");
            Register<WireSubscribeStateSyncRes>(registry, "subscribe-state-sync.response");
            Register<WireRequestFullStateSyncReq>(registry, "request-full-state-sync.request");
            Register<WireRequestFullStateSyncRes>(registry, "request-full-state-sync.response");
            Register<WireGetStateSyncDeliveryMetricsReq>(registry, "state-sync-metrics.request");
            Register<WireGetStateSyncDeliveryMetricsRes>(registry, "state-sync-metrics.response");
            Register<WireBeginLoadingReq>(registry, "begin-loading.request");
            Register<WireRoomOperationRes>(registry, "begin-loading.response");
            Register<WireReportAssetsLoadedReq>(registry, "report-assets-loaded.request");
            Register<WireRoomOperationRes>(registry, "report-assets-loaded.response");
            Register<WireReportLoadingProgressReq>(registry, "report-loading-progress.request");
            Register<WireRoomOperationRes>(registry, "report-loading-progress.response");
            Register<WireCancelLoadingReq>(registry, "cancel-loading.request");
            Register<WireRoomOperationRes>(registry, "cancel-loading.response");
            Register<WireLeaveRoomReq>(registry, "leave-room.request");
            Register<WireRoomOperationRes>(registry, "leave-room.response");
            Register<WireGetSnapshotReq>(registry, "get-snapshot.request");
            Register<WireRoomSnapshotRes>(registry, "get-snapshot.response");
            Register<WireAckReliableBattleEventsReq>(registry, "ack-reliable-events.request");
            Register<WireAckReliableBattleEventsRes>(registry, "ack-reliable-events.response");
            Register<WireStateSyncSnapshotPush>(registry, "state-sync-snapshot.push");
            Register<WireStateSyncSnapshotPush>(registry, "state-sync-delta.push");
            Register<WireRoomStateChangedPush>(registry, "room-state-changed.push");
            Register<WireReliableBattleEventPush>(registry, "reliable-battle-events.push");
        }

        private static void Register<T>(ProtocolPayloadDecoderRegistry registry, string messageId)
        {
            registry.TryRegister(CatalogId, messageId, payload => WireRoomGatewayBinary.Deserialize<T>(payload));
        }
    }
}
