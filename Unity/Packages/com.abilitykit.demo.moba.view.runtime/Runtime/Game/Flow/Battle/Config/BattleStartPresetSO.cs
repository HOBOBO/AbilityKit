using AbilityKit.Ability.Host;
using Sirenix.OdinInspector;
using UnityEngine;

namespace AbilityKit.Game.Flow
{
    [CreateAssetMenu(menuName = "AbilityKit/Game/Battle Start Preset", fileName = "BattleStartPreset")]
    public sealed class BattleStartPresetSO : ScriptableObject
    {
        [Header("Formal Start Profile")]
        [LabelText("WorldId(世界ID/房间ID)")]
        public string WorldId = "room_1";

        [LabelText("WorldType(世界类型)")]
        public string WorldType = "battle";

        [LabelText("ClientId(客户端ID)")]
        public string ClientId = "battle_client";

        [LabelText("HostMode(主机模式)")]
        public BattleStartConfig.BattleHostMode HostMode = BattleStartConfig.BattleHostMode.Local;

        [LabelText("AutoConnect(自动连接)")]
        public bool AutoConnect = true;

        [LabelText("AutoCreateWorld(自动创建World)")]
        public bool AutoCreateWorld = true;

        [LabelText("AutoJoin(自动加入)")]
        public bool AutoJoin = true;

        [LabelText("AutoReady(自动准备)")]
        public bool AutoReady = true;

        [LabelText("SyncMode(同步模式)")]
        public BattleSyncMode SyncMode = BattleSyncMode.Lockstep;

        [LabelText("ViewEventSourceMode(View事件源模式)")]
        public BattleViewEventSourceMode ViewEventSourceMode = BattleViewEventSourceMode.SnapshotOnly;

        [LabelText("EnabledSnapshotRegistryIds(Snapshot Registries)")]
        public string[] EnabledSnapshotRegistryIds;

        [LabelText("EnableClientPrediction(客户端预测)")]
        public bool EnableClientPrediction = true;

        [LabelText("EnableConfirmedAuthorityWorld(权威确认世界)")]
        public bool EnableConfirmedAuthorityWorld = false;

        [LabelText("EnterGame配置(可复用SO)")]
        public BattleEnterGameConfigSO EnterGameSO;

        [LabelText("玩家配置(可复用SO)")]
        public BattlePlayersConfigSO PlayersSO;

        [LabelText("运行模式配置(可复用SO)")]
        public BattleRunModeConfigSO RunModeSO;

        [LabelText("网关配置(可复用SO)")]
        public BattleGatewayConfigSO GatewaySO;

        [Header("Composition")]
        [LabelText("FeatureSet(战斗阶段Feature组合)")]
        public BattleFeatureSetConfig FeatureSet;

        [Header("Composition")]
        [LabelText("SessionModuleSet(Session内部模块组合)")]
        public BattleSessionModuleSetConfig SessionModuleSet;
    }
}
