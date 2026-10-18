using System;

namespace AbilityKit.Demo.Moba.Share.Config
{
    [Serializable]
    public sealed class GameplayDTO
    {
        public int Id;
        public string Name;
        public int[] TriggerIds;
        public int DefaultDurationMs;
        public int[] Tags;
    }

    /// <summary>
    /// 一场战斗副本的客户端装配契约：加载哪个流程 Profile、挂哪些扩展点实现、
    /// 以及入场协议约束。玩法常量（时长、地图、队伍规模、胜负条件）由 gameplays /
    /// battle_maps 等表负责，副本表只引用不复制。
    /// </summary>
    [Serializable]
    public sealed class DungeonDTO
    {
        public int Id;
        public string Name;
        public int GameplayId;
        public string FlowProfileId;
        public int MinPlayerCount;
        public int MaxPlayerCount;
        public string[] AllowedHostModes;
        public string SceneProviderId;
        public string GameplayHandlerId;
        public string OutcomeRuleId;
        public string[] RequiredPortGroups;
        public int[] Tags;
    }
}
