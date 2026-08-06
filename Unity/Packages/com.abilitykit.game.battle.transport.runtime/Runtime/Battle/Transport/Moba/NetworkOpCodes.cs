using AbilityKit.Protocol.Moba.Generated.GatewayFrameSync;
using AbilityKit.Protocol.Room;
using StateSyncOpCodes = AbilityKit.Protocol.Moba.StateSync.OpCodes;

namespace AbilityKit.Game.Battle.Transport.Moba
{
    /// <summary>
    /// 网络操作码
    /// 与服务器保持一致
    /// 
    /// OpCode 定义来源：
    /// - 网络专用 OpCode（登录、房间管理）: 此文件定义
    /// - 帧同步/状态同步 OpCode: AbilityKit.Protocol.Moba 中定义
    ///   - GatewayFrameSync.OpCodes.SubmitFrameInput (2001)
    ///   - GatewayFrameSync.OpCodes.FramePushed (9001)
    ///   - StateSync.OpCodes.SnapshotPushed (9002)
    /// </summary>
    public static class NetworkOpCodes
    {
        // ========== 网络专用 OpCode ==========
        public const uint Hello = 1;
        public const uint GuestLogin = 100;
        public const uint CreateSessionForAccount = 122;
        public const uint RenewSession = 120;
        public const uint Logout = 121;

        public const uint CreateRoom = 110;
        public const uint JoinRoom = 111;
        public const uint LeaveRoom = 112;
        public const uint ListRooms = 113;
        public const uint CloseRoom = 114;

        public const uint TimeSync = 1300;

        public const uint KickPush = RoomGatewayOpCodes.SessionKicked;

        // ========== 帧同步/状态同步 OpCode (引用 Protocol.Moba) ==========
        public const uint SubmitFrameInput = OpCodes.SubmitFrameInput;
        public const uint FramePushed = OpCodes.FramePushed;
        public const uint SnapshotPushed = StateSyncOpCodes.SnapshotPushed;
    }
}
