using System.Collections.Generic;
using UnityEngine;

namespace AbilityKit.Game.Flow
{
    [CreateAssetMenu(menuName = "AbilityKit/Game/Battle Session Module Set", fileName = "BattleSessionModuleSet")]
    public sealed class BattleSessionModuleSetConfig : ScriptableObject
    {
        public List<string> ModuleIds = new List<string>
        {
            "gateway_room",
            "snapshot_routing"
        };
    }
}
