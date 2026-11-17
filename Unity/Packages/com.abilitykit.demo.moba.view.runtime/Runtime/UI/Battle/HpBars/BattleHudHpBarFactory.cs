using AbilityKit.Game.Battle.Shared.Assets;
using UnityEngine;
using UnityEngine.UI;

namespace AbilityKit.Game.Flow
{
    internal sealed class BattleHudHpBarFactory
    {
        private readonly BattleHudFallbackUiFactory _fallbackUi;

        public BattleHudHpBarFactory(BattleHudFallbackUiFactory fallbackUi = null)
        {
            _fallbackUi = fallbackUi ?? new BattleHudFallbackUiFactory();
        }

        public BattleHudHpBarHandle Create(int actorId, BattleHudConfig cfg, RectTransform root)
        {
            var go = CreateGameObject(cfg, root);
            var rt = go.GetComponent<RectTransform>();
            if (rt == null) rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(132f, 18f);

            var images = go.GetComponentsInChildren<Image>(true);
            Image hpFill = null;
            Image background = null;
            for (var i = 0; i < images.Length; i++)
            {
                if (images[i] != null &&
                    (images[i].gameObject.name.IndexOf("Bg", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                     images[i].gameObject.name.IndexOf("Background", System.StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    background = images[i];
                }
                if (images[i] != null && images[i].gameObject.name.IndexOf("Fill", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    hpFill = images[i];
                    break;
                }
            }
            if (background == null && images.Length > 0) background = images[0];
            if (background != null)
            {
                var outline = background.GetComponent<Outline>() ?? background.gameObject.AddComponent<Outline>();
                outline.effectColor = new Color(0f, 0f, 0f, 1f);
                outline.effectDistance = new Vector2(2f, 2f);
                outline.useGraphicAlpha = false;
            }
            return new BattleHudHpBarHandle
            {
                ActorId = actorId,
                Root = rt,
                HpFill = hpFill ?? (images.Length > 0 ? images[0] : null),
                ShieldFill = CreateShieldOverlay(go),
                WorldOffset = cfg.HpBarWorldOffset,
            };
        }

        private static UnityEngine.UI.Image CreateShieldOverlay(GameObject go)
        {
            var overlay = new GameObject("ShieldFill");
            overlay.transform.SetParent(go.transform, false);
            var image = overlay.AddComponent<UnityEngine.UI.Image>();
            image.color = new Color(0.25f, 0.85f, 1f, 0.85f);
            image.type = UnityEngine.UI.Image.Type.Filled;
            image.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal;
            image.fillOrigin = (int)UnityEngine.UI.Image.OriginHorizontal.Left;
            image.raycastTarget = false;
            BattleHudRectTransformLayout.StretchToParent(overlay.GetComponent<RectTransform>());
            overlay.transform.SetAsLastSibling();
            image.fillAmount = 0f;
            overlay.SetActive(false);
            return image;
        }

        private GameObject CreateGameObject(BattleHudConfig cfg, RectTransform root)
        {
            var prefab = !string.IsNullOrEmpty(cfg.HpBarPrefabPath)
                ? ResourcesAssetProvider.Shared.Load<GameObject>(cfg.HpBarPrefabPath)
                : null;

            if (prefab != null)
            {
                return Object.Instantiate(prefab, root);
            }

            var go = _fallbackUi.CreateHpBar();
            go.transform.SetParent(root, worldPositionStays: false);
            return go;
        }
    }
}
