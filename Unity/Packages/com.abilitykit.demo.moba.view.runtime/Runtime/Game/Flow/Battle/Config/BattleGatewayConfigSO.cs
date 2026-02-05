using Sirenix.OdinInspector;
using UnityEngine;

namespace AbilityKit.Game.Flow
{
    [CreateAssetMenu(menuName = "AbilityKit/Game/Battle Gateway Config", fileName = "BattleGatewayConfig")]
    public sealed class BattleGatewayConfigSO : ScriptableObject
    {
        [LabelText("使用网关 Transport")]
        public bool UseGatewayTransport = false;

        [LabelText("Host")]
        public string Host = "127.0.0.1";

        [LabelText("Port")]
        public int Port = 4000;

        [LabelText("NumericRoomId")]
        public ulong NumericRoomId = 0;

        [LabelText("SessionToken")]
        public string SessionToken = string.Empty;

        [LabelText("Region")]
        public string Region = "dev";

        [LabelText("ServerId")]
        public string ServerId = "local";

        [LabelText("自动创建房间")]
        public bool AutoCreateRoom = false;

        [LabelText("自动加入房间")]
        public bool AutoJoinRoom = false;

        [LabelText("JoinRoomId")]
        public string JoinRoomId = string.Empty;

        [LabelText("CreateRoom OpCode")]
        public uint CreateRoomOpCode = 110;

        [LabelText("JoinRoom OpCode")]
        public uint JoinRoomOpCode = 111;

        [LabelText("TimeSync OpCode")]
        public uint TimeSyncOpCode = 1300;

        [LabelText("TimeSync 间隔(ms)")]
        public int TimeSyncIntervalMs = 1000;

        [LabelText("TimeSync Alpha")]
        public double TimeSyncAlpha = 0.20;

        [LabelText("TimeSync 超时(ms)")]
        public int TimeSyncTimeoutMs = 2000;

        [LabelText("理想帧安全边际(常量)")]
        public int IdealFrameSafetyConstMarginFrames = 2;

        [LabelText("理想帧安全边际(RTT系数)")]
        public double IdealFrameSafetyRttFactor = 1.0;

        [LabelText("理想帧安全边际(最小)")]
        public int IdealFrameSafetyMinMarginFrames = 0;

        [LabelText("理想帧安全边际(最大)")]
        public int IdealFrameSafetyMaxMarginFrames = 30;
    }
}
