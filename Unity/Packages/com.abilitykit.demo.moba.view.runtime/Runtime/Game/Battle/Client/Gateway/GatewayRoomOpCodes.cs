using RoomOpCodes = AbilityKit.Protocol.Room.RoomGatewayOpCodes;
using StateSyncOpCodes = AbilityKit.Protocol.Moba.StateSync.OpCodes;

namespace AbilityKit.Game.Battle.Agent
{
    public readonly struct GatewayRoomOpCodes
    {
        public readonly uint CreateRoom;
        public readonly uint JoinRoom;
        public readonly uint SubscribeStateSync;
        public readonly uint SetReady;
        public readonly uint PickHero;
        public readonly uint StartBattle;
        public readonly uint SubmitBattleInput;
        public readonly uint SnapshotPushed;
        public readonly uint DeltaSnapshotPushed;

        // 阶段 5：资源加载屏障 / 状态查询 / 状态变更推送 opcodes
        public readonly uint BeginLoading;
        public readonly uint ReportAssetsLoaded;
        public readonly uint CancelLoading;
        public readonly uint GetSnapshot;
        public readonly uint RestoreRoom;
        public readonly uint RoomStateChanged;

        public GatewayRoomOpCodes(uint createRoom, uint joinRoom)
            : this(createRoom, joinRoom, StateSyncOpCodes.SubscribeStateSync, RoomOpCodes.SetReady, RoomOpCodes.PickHero, RoomOpCodes.StartBattle, RoomOpCodes.SubmitBattleInput)
        {
        }

        public GatewayRoomOpCodes(uint createRoom, uint joinRoom, uint setReady, uint pickHero, uint startBattle)
            : this(createRoom, joinRoom, StateSyncOpCodes.SubscribeStateSync, setReady, pickHero, startBattle, RoomOpCodes.SubmitBattleInput)
        {
        }

        public GatewayRoomOpCodes(uint createRoom, uint joinRoom, uint setReady, uint pickHero, uint startBattle, uint submitBattleInput)
            : this(createRoom, joinRoom, StateSyncOpCodes.SubscribeStateSync, setReady, pickHero, startBattle, submitBattleInput)
        {
        }

        public GatewayRoomOpCodes(uint createRoom, uint joinRoom, uint subscribeStateSync, uint setReady, uint pickHero, uint startBattle, uint submitBattleInput)
            : this(createRoom, joinRoom, subscribeStateSync, setReady, pickHero, startBattle, submitBattleInput, StateSyncOpCodes.SnapshotPushed, StateSyncOpCodes.DeltaSnapshotPushed)
        {
        }

        public GatewayRoomOpCodes(uint createRoom, uint joinRoom, uint subscribeStateSync, uint setReady, uint pickHero, uint startBattle, uint submitBattleInput, uint snapshotPushed, uint deltaSnapshotPushed)
            : this(
                  createRoom,
                  joinRoom,
                  subscribeStateSync,
                  setReady,
                  pickHero,
                  startBattle,
                  submitBattleInput,
                  snapshotPushed,
                  deltaSnapshotPushed,
                  RoomOpCodes.BeginLoading,
                  RoomOpCodes.ReportAssetsLoaded,
                  RoomOpCodes.CancelLoading,
                  RoomOpCodes.GetSnapshot,
                  RoomOpCodes.RestoreRoom,
                  RoomOpCodes.RoomStateChanged)
        {
        }

        public GatewayRoomOpCodes(
            uint createRoom,
            uint joinRoom,
            uint subscribeStateSync,
            uint setReady,
            uint pickHero,
            uint startBattle,
            uint submitBattleInput,
            uint snapshotPushed,
            uint deltaSnapshotPushed,
            uint beginLoading,
            uint reportAssetsLoaded,
            uint cancelLoading,
            uint getSnapshot,
            uint restoreRoom,
            uint roomStateChanged)
        {
            CreateRoom = createRoom;
            JoinRoom = joinRoom;
            SubscribeStateSync = subscribeStateSync;
            SetReady = setReady;
            PickHero = pickHero;
            StartBattle = startBattle;
            SubmitBattleInput = submitBattleInput;
            SnapshotPushed = snapshotPushed;
            DeltaSnapshotPushed = deltaSnapshotPushed;
            BeginLoading = beginLoading;
            ReportAssetsLoaded = reportAssetsLoaded;
            CancelLoading = cancelLoading;
            GetSnapshot = getSnapshot;
            RestoreRoom = restoreRoom;
            RoomStateChanged = roomStateChanged;
        }

        /// <summary>
        /// 全部使用 <see cref="RoomGatewayOpCodes"/> 常量的默认实例。
        /// </summary>
        public static GatewayRoomOpCodes Default => new GatewayRoomOpCodes(
            RoomOpCodes.CreateRoom,
            RoomOpCodes.JoinRoom,
            StateSyncOpCodes.SubscribeStateSync,
            RoomOpCodes.SetReady,
            RoomOpCodes.PickHero,
            RoomOpCodes.StartBattle,
            RoomOpCodes.SubmitBattleInput,
            StateSyncOpCodes.SnapshotPushed,
            StateSyncOpCodes.DeltaSnapshotPushed,
            RoomOpCodes.BeginLoading,
            RoomOpCodes.ReportAssetsLoaded,
            RoomOpCodes.CancelLoading,
            RoomOpCodes.GetSnapshot,
            RoomOpCodes.RestoreRoom,
            RoomOpCodes.RoomStateChanged);
    }
}
