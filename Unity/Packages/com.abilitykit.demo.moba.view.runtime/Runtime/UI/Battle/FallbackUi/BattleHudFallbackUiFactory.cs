using UnityEngine;
using UnityEngine.UI;

namespace AbilityKit.Game.Flow
{
    internal sealed class BattleHudFallbackUiFactory
    {
        public GameObject CreateHpBar()
        {
            var root = new GameObject("HpBar");
            var rt = root.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(80, 10);

            var bg = new GameObject("Bg");
            bg.transform.SetParent(root.transform, worldPositionStays: false);
            var bgImg = bg.AddComponent<Image>();
            bgImg.color = new Color(0f, 0f, 0f, 0.6f);
            BattleHudRectTransformLayout.StretchToParent(bg.GetComponent<RectTransform>());

            var fill = new GameObject("Fill");
            fill.transform.SetParent(bg.transform, worldPositionStays: false);
            var fillImg = fill.AddComponent<Image>();
            fillImg.color = Color.red;
            fillImg.type = Image.Type.Filled;
            fillImg.fillMethod = Image.FillMethod.Horizontal;
            fillImg.fillOrigin = (int)Image.OriginHorizontal.Left;
            fillImg.fillAmount = 1f;
            BattleHudRectTransformLayout.StretchToParent(fill.GetComponent<RectTransform>());

            return root;
        }

        public GameObject CreateFloatingText()
        {
            var root = new GameObject("FloatingText");
            var rt = root.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(200, 40);

            var tgo = new GameObject("Text");
            tgo.transform.SetParent(root.transform, worldPositionStays: false);
            var text = tgo.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.alignment = TextAnchor.MiddleCenter;
            text.text = "0";
            text.color = Color.white;
            BattleHudRectTransformLayout.StretchToParent(tgo.GetComponent<RectTransform>());

            return root;
        }
    }
}
