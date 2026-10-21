using UnityEngine;

namespace AbilityKit.Game.Flow
{
    public sealed class BattleHudConfig
    {
        public static readonly BattleHudConfig Default = new BattleHudConfig();

        public string HpBarPrefabPath = "ui/battle/hud_hpbar";

        public Vector3 HpBarWorldOffset = new Vector3(0f, 2.2f, 0f);

        public Vector3 ChargeBarWorldOffset = new Vector3(0f, 2.42f, 0f);
        public Vector2 ChargeBarSize = new Vector2(76f, 6f);
        public Color ChargeBarBackgroundColor = new Color(0f, 0f, 0f, 0.8f);
        public Color ChargeBarFillColor = new Color(1f, 0.78f, 0.18f, 1f);

        public Vector3 FloatingTextWorldOffset = new Vector3(0f, 2.4f, 0f);
        public float FloatingTextLifetime = 0.9f;
        public float FloatingTextRisePixels = 50f;
        public float FloatingTextSpreadPixels = 16f;

        public Color DamageTextColor = new Color(1f, 0.25f, 0.25f, 1f);
        public Color HealTextColor = new Color(0.25f, 1f, 0.25f, 1f);

        /// <summary>
        /// World offset above the actor where the buff bar is anchored.
        /// </summary>
        public Vector3 BuffBarWorldOffset = new Vector3(0f, 2.7f, 0f);

        /// <summary>
        /// Per-icon size in pixels.
        /// </summary>
        public Vector2 BuffIconSize = new Vector2(36f, 36f);

        /// <summary>
        /// Spacing between icons.
        /// </summary>
        public float BuffIconSpacing = 4f;

        /// <summary>
        /// Maximum number of icons rendered per actor.
        /// Older icons are dropped when exceeded.
        /// </summary>
        public int MaxBuffIconsPerActor = 8;

        /// <summary>
        /// Whether the buff bar shows on local-controlled actors only, or on every spawned actor.
        /// </summary>
        public bool BuffBarOnlyLocalActor = false;

        /// <summary>
        /// Foreground ring color drawn over a buff icon to indicate remaining time.
        /// </summary>
        public Color BuffRingColor = new Color(0.05f, 0.85f, 1f, 0.85f);

        /// <summary>
        /// Background ring color (drained portion) of the buff timer ring.
        /// </summary>
        public Color BuffRingBackgroundColor = new Color(0f, 0f, 0f, 0.6f);

        /// <summary>
        /// Tint applied to the buff icon background. Tinted by TemplateId via a deterministic hash.
        /// </summary>
        public Color BuffIconBackgroundColor = new Color(0.08f, 0.08f, 0.08f, 0.85f);

        /// <summary>
        /// Stack-count label color.
        /// </summary>
        public Color BuffStackTextColor = Color.white;
    }
}
