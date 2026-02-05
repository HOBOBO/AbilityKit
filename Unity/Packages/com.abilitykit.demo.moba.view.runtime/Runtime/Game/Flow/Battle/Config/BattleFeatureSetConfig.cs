using System.Collections.Generic;
using UnityEngine;

namespace AbilityKit.Game.Flow
{
    [CreateAssetMenu(menuName = "AbilityKit/Game/Battle Feature Set", fileName = "BattleFeatureSet")]
    public sealed class BattleFeatureSetConfig : ScriptableObject
    {
        public List<string> FeatureIds = new List<string>
        {
            "context",
            "session",
            "entity",
            "sync",
            "input",
            "view",
            "hud",
            "debug_ongui"
        };
    }
}
