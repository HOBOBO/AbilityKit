using UnityEngine;

namespace AbilityKit.Game.Flow
{
    internal static class BattleHudInputLayout
    {
        private static readonly Vector2[] SkillButtonPositions =
        {
            new Vector2(-330f, 150f),
            new Vector2(-215f, 245f),
            new Vector2(-95f, 180f),
            new Vector2(-92f, 62f),
            new Vector2(-215f, 365f),
            new Vector2(-340f, 285f),
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
