using UnityEngine;
using UnityEngine.UI;

namespace AbilityKit.Game.Flow.Battle.Hud
{
    internal sealed class BattleHudChargeBarFactory
    {
        public BattleHudChargeBarHandle Create(int actorId, int slot, BattleHudConfig cfg, RectTransform root)
        {
            var go = new GameObject($"ChargeBar_{actorId}_{slot}", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(root, worldPositionStays: false);
            rt.sizeDelta = cfg.ChargeBarSize;
            rt.pivot = new Vector2(0.5f, 0.5f);

            var bg = new GameObject("Background", typeof(RectTransform), typeof(Image));
            bg.transform.SetParent(rt, worldPositionStays: false);
            var bgImage = bg.GetComponent<Image>();
            bgImage.color = cfg.ChargeBarBackgroundColor;
            bgImage.raycastTarget = false;
            BattleHudRectTransformLayout.StretchToParent((RectTransform)bg.transform);

            var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fill.transform.SetParent(bg.transform, worldPositionStays: false);
            var fillImage = fill.GetComponent<Image>();
            fillImage.color = cfg.ChargeBarFillColor;
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Horizontal;
            fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
            fillImage.fillAmount = 0f;
            fillImage.raycastTarget = false;
            BattleHudRectTransformLayout.StretchToParent((RectTransform)fill.transform);

            return new BattleHudChargeBarHandle
            {
                ActorId = actorId,
                Slot = slot,
                Root = rt,
                Fill = fillImage,
                WorldOffset = cfg.ChargeBarWorldOffset,
            };
        }
    }
}
