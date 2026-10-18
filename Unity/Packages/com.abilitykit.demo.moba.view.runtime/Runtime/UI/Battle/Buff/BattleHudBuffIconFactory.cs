using UnityEngine;
using UnityEngine.UI;

namespace AbilityKit.Game.Flow.Battle.Hud
{
    /// <summary>
    /// Builds <see cref="BattleHudBuffIconView"/> GameObjects imperatively (no prefab required).
    /// The view is a single Image parent with a child ring Image and a Text for stack/countdown.
    /// </summary>
    internal sealed class BattleHudBuffIconFactory
    {
        private readonly BattleHudFallbackUiFactory _fallback;

        public BattleHudBuffIconFactory(BattleHudFallbackUiFactory fallback = null)
        {
            _fallback = fallback ?? new BattleHudFallbackUiFactory();
        }

        public BattleHudBuffIconView Create(RectTransform parent)
        {
            var rootGo = new GameObject("BuffIcon", typeof(RectTransform), typeof(Image), typeof(BattleHudBuffIconView));
            var rootRt = (RectTransform)rootGo.transform;
            rootRt.SetParent(parent, worldPositionStays: false);
            rootRt.sizeDelta = new Vector2(36f, 36f);

            var bg = rootGo.GetComponent<Image>();
            bg.color = new Color(0.08f, 0.08f, 0.08f, 0.85f);
            bg.raycastTarget = false;
            bg.type = Image.Type.Simple;

            // Ring child: a filled radial image drawn from the top, clockwise.
            var ringGo = new GameObject("Ring", typeof(RectTransform), typeof(Image));
            var ringRt = (RectTransform)ringGo.transform;
            ringRt.SetParent(rootRt, worldPositionStays: false);
            BattleHudRectTransformLayout.StretchToParent(ringRt);
            var ring = ringGo.GetComponent<Image>();
            ring.color = new Color(0.05f, 0.85f, 1f, 0.85f);
            ring.raycastTarget = false;
            ring.type = Image.Type.Filled;
            ring.fillMethod = Image.FillMethod.Radial360;
            ring.fillOrigin = (int)Image.Origin360.Top;
            ring.fillClockwise = true;
            ring.fillAmount = 1f;

            // Stack + countdown text overlay.
            var textGo = new GameObject("Stack", typeof(RectTransform), typeof(Text));
            var textRt = (RectTransform)textGo.transform;
            textRt.SetParent(rootRt, worldPositionStays: false);
            BattleHudRectTransformLayout.StretchToParent(textRt);
            var text = textGo.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.alignment = TextAnchor.LowerRight;
            text.text = string.Empty;
            text.color = Color.white;
            text.raycastTarget = false;
            text.fontSize = 14;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;

            var view = rootGo.GetComponent<BattleHudBuffIconView>();
            view.Initialize(rootRt, bg, ring, bg, text, text);
            return view;
        }
    }
}