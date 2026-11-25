using UnityEngine;
using UnityEngine.UI;

namespace AbilityKit.Game.Flow
{
    /// <summary>
    /// 头顶面板强类型绑定；正式 prefab 与运行时降级结构共享槽位合同。
    /// </summary>
    public sealed class BattleHudOverheadView : MonoBehaviour
    {
        public Image HealthBackground, HealthFill, ShieldFill;
        public RectTransform RowsRoot, CastSlot, BuffSlot;
        public Text ValueText, ShieldValue;
        public bool IsValid => HealthBackground != null && HealthFill != null && ShieldFill != null &&
            RowsRoot != null && CastSlot != null && BuffSlot != null && ValueText != null && ShieldValue != null;

        /// <summary>
        /// 创建缺省结构，编辑器资源生成复用本入口。
        /// </summary>
        public static BattleHudOverheadView CreateDefault(Transform parent)
        {
            var root = Rect("OverheadPanel", parent, new Vector2(132, 22));
            var view = root.gameObject.AddComponent<BattleHudOverheadView>();
            view.HealthBackground = Image("HealthBackground", root, new Color(0.035f, 0.045f, 0.06f, 0.95f));
            view.HealthBackground.rectTransform.sizeDelta = new Vector2(132, 10);
            view.HealthFill = Image("HealthFill", view.HealthBackground.transform, new Color(0.2f, 0.85f, 0.28f));
            SetFill(view.HealthFill, 0);
            var outline = view.HealthBackground.gameObject.AddComponent<Outline>();
            outline.effectColor = Color.black; outline.effectDistance = new Vector2(1, 1);
            view.ShieldFill = Image("ShieldLayer", root, new Color(0.65f, 0.92f, 1, 1));
            view.ShieldFill.rectTransform.anchoredPosition = new Vector2(0, 8);
            view.ShieldFill.rectTransform.sizeDelta = new Vector2(132, 3);
            view.ShieldFill.gameObject.SetActive(false);
            view.RowsRoot = Rect("Rows", root, Vector2.zero);
            view.CastSlot = Rect("CastSlot", root, Vector2.zero);
            view.BuffSlot = Rect("BuffSlot", root, Vector2.zero);
            view.ValueText = Text("HealthValue", root, 10);
            view.ValueText.rectTransform.anchoredPosition = new Vector2(0, 18);
            view.ShieldValue = Text("ShieldOverflow", root, 10);
            view.ShieldValue.rectTransform.anchoredPosition = new Vector2(92, 8);
            view.ShieldValue.gameObject.SetActive(false);
            return view;
        }

        /// <summary>
        /// 设置实际几何宽度与契约值；不依赖缺省 Sprite 是否支持 Filled 网格。
        /// </summary>
        public static void SetFill(Image image, float ratio)
        {
            ratio = Mathf.Clamp01(ratio);
            var rt = image.rectTransform;
            if (image.fillAmount == ratio && rt.anchorMin == Vector2.zero && rt.anchorMax == new Vector2(ratio, 1)) return;
            image.fillAmount = ratio;
            rt.anchorMin = Vector2.zero; rt.anchorMax = new Vector2(ratio, 1);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        /// <summary>
        /// 创建中心锚点槽位。
        /// </summary>
        public static RectTransform Rect(string name, Transform parent, Vector2 size)
        {
            var rt = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rt.SetParent(parent, false); rt.sizeDelta = size;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f); return rt;
        }

        /// <summary>
        /// 创建不拦截输入的图形。
        /// </summary>
        public static Image Image(string name, Transform parent, Color color)
        {
            var rt = Rect(name, parent, Vector2.zero);
            var image = rt.gameObject.AddComponent<Image>(); image.color = color; image.raycastTarget = false; return image;
        }

        /// <summary>
        /// 创建带阴影的不拦截输入文本。
        /// </summary>
        public static Text Text(string name, Transform parent, int size)
        {
            var rt = Rect(name, parent, new Vector2(160, 18));
            var text = rt.gameObject.AddComponent<Text>(); text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size; text.alignment = TextAnchor.MiddleCenter; text.color = Color.white;
            text.raycastTarget = false; text.horizontalOverflow = HorizontalWrapMode.Overflow;
            rt.gameObject.AddComponent<Shadow>().effectColor = Color.black; return text;
        }
    }
}
