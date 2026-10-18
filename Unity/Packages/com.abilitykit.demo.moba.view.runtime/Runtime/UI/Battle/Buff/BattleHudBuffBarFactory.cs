using UnityEngine;
using UnityEngine.UI;

namespace AbilityKit.Game.Flow.Battle.Hud
{
    /// <summary>
    /// Builds <see cref="BattleHudBuffBar"/> GameObjects (a horizontal layout with no prefab dependency).
    /// </summary>
    internal sealed class BattleHudBuffBarFactory
    {
        private readonly BattleHudBuffIconFactory _iconFactory;

        public BattleHudBuffBarFactory(BattleHudBuffIconFactory iconFactory = null)
        {
            _iconFactory = iconFactory ?? new BattleHudBuffIconFactory();
        }

        public BattleHudBuffIconFactory IconFactory => _iconFactory;

        public BattleHudBuffBar Create(int actorId, BattleHudConfig cfg, RectTransform root)
        {
            var go = new GameObject($"BuffBar_{actorId}", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(root, worldPositionStays: false);
            rt.sizeDelta = new Vector2(160f, 40f);
            rt.pivot = new Vector2(0.5f, 0f);

            var layout = go.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.spacing = cfg.BuffIconSpacing;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = false;
            layout.padding = new RectOffset(2, 2, 2, 2);

            var bar = BattleHudBuffBar.Create(rt, layout);
            bar.Bind(actorId);
            return bar;
        }
    }
}