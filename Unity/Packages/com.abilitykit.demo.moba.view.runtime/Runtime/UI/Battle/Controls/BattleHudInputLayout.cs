using UnityEngine;

namespace AbilityKit.Game.Flow
{
    internal static class BattleHudInputLayout
    {
        private static readonly Vector2[] SkillButtonPositions =
        {
            // Q/W above E/R; basic attack sits immediately to the right of the
            // lower row so all five controls form one compact thumb cluster.
            new Vector2(-330f, 250f),
            new Vector2(-200f, 250f),
            new Vector2(-330f, 120f),
            new Vector2(-200f, 120f),
            new Vector2(-70f, 120f),
        };

        public static readonly Vector2 InfoButtonPosition = new Vector2(-80f, -80f);

        public static BattleHudSkillButtonLayoutSpec GetSkill(int slot)
        {
            if (slot <= 0) slot = 1;
            var index = slot - 1;
            var pos = index < SkillButtonPositions.Length
                ? SkillButtonPositions[index]
                : ResolveOverflowPosition(index);
            return new BattleHudSkillButtonLayoutSpec(slot, "Skill" + slot, pos);
        }

        private static Vector2 ResolveOverflowPosition(int index)
        {
            var extra = index - SkillButtonPositions.Length;
            var row = extra / 4;
            var col = extra % 4;
            return new Vector2(-95f - col * 125f, 395f + row * 125f);
        }
    }
}
