using AbilityKit.Game.Battle.Shared.Assets;
using UnityEngine;

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

            return new BattleHudHpBarHandle
            {
                ActorId = actorId,
                Root = rt,
                HpFill = go.GetComponentInChildren<UnityEngine.UI.Image>(),
                WorldOffset = cfg.HpBarWorldOffset,
            };
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
