using UnityEngine;

namespace AbilityKit.Game.Flow
{
    internal readonly struct BattleHudSkillButtonLayoutSpec
    {
        public BattleHudSkillButtonLayoutSpec(int slot, string name, Vector2 anchoredPos)
        {
            Slot = slot;
            Name = name;
            AnchoredPos = anchoredPos;
        }

        public int Slot { get; }

        public string Name { get; }

        public Vector2 AnchoredPos { get; }
    }
}
