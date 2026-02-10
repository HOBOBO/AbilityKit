using Sirenix.OdinInspector;
using UnityEngine;

namespace AbilityKit.Game.Flow
{
    [CreateAssetMenu(menuName = "AbilityKit/Game/Battle Start Runtime Overrides", fileName = "BattleStartRuntimeOverrides")]
    public sealed class BattleStartRuntimeOverrides : ScriptableObject
    {
        [Header("Overrides")]
        [LabelText("WorldId 覆盖(可选)")]
        public string WorldId;

        [LabelText("ClientId 覆盖(可选)")]
        public string ClientId;

        [LabelText("NumericRoomId 覆盖(可选)")]
        public ulong NumericRoomId;

        [LabelText("GatewayJoinRoomId 覆盖(可选)")]
        public string GatewayJoinRoomId;

        [LabelText("RecordOutputDirectory 覆盖(可选)")]
        public string RecordOutputDirectory;

        [LabelText("ReplayInputFilePath 覆盖(可选)")]
        public string ReplayInputFilePath;

        public bool HasWorldId => !string.IsNullOrEmpty(WorldId);
        public bool HasClientId => !string.IsNullOrEmpty(ClientId);
        public bool HasNumericRoomId => NumericRoomId != 0;
        public bool HasGatewayJoinRoomId => !string.IsNullOrEmpty(GatewayJoinRoomId);
        public bool HasRecordOutputDirectory => !string.IsNullOrEmpty(RecordOutputDirectory);
        public bool HasReplayInputFilePath => !string.IsNullOrEmpty(ReplayInputFilePath);
    }
}
