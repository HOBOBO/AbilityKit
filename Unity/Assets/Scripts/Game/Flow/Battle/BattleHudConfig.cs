using UnityEngine;

namespace AbilityKit.Game.Flow
{
    public sealed class BattleHudConfig
    {
        public static readonly BattleHudConfig Default = new BattleHudConfig();

        public string HpBarPrefabPath = "ui/battle/hud_hpbar";

        public Vector3 HpBarWorldOffset = new Vector3(0f, 2.2f, 0f);

        public Vector3 FloatingTextWorldOffset = new Vector3(0f, 2.4f, 0f);
        public float FloatingTextLifetime = 0.9f;
        public float FloatingTextRisePixels = 50f;
        public float FloatingTextSpreadPixels = 16f;

        public Color DamageTextColor = new Color(1f, 0.25f, 0.25f, 1f);
        public Color HealTextColor = new Color(0.25f, 1f, 0.25f, 1f);
    }
}
