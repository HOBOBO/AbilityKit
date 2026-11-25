using AbilityKit.Game.Battle.Shared.Assets;
using UnityEngine;

namespace AbilityKit.Game.Flow
{
    /// <summary>
    /// 强类型面板工厂；正式资源和降级结构采用相同槽位。
    /// </summary>
    internal sealed class BattleHudHpBarFactory
    {
        private static bool _missingReported;
        public BattleHudHpBarFactory(BattleHudFallbackUiFactory fallbackUi = null) { }
        public BattleHudHpBarHandle Create(int actorId, BattleHudConfig cfg, RectTransform root)
        {
            var prefab = string.IsNullOrEmpty(cfg.HpBarPrefabPath) ? null : ResourcesAssetProvider.Shared.Load<GameObject>(cfg.HpBarPrefabPath);
            BattleHudOverheadView view;
            if (prefab != null)
            {
                var instance = Object.Instantiate(prefab, root);
                view = instance.GetComponent<BattleHudOverheadView>();
                if (view == null || !view.IsValid)
                { Object.Destroy(instance); throw new System.InvalidOperationException("Invalid overhead prefab bindings: " + cfg.HpBarPrefabPath); }
            }
            else
            {
                if (!_missingReported && !string.IsNullOrEmpty(cfg.HpBarPrefabPath))
                { Debug.LogWarning("[OverheadHud] Missing prefab, using typed fallback: " + cfg.HpBarPrefabPath); _missingReported = true; }
                view = BattleHudOverheadView.CreateDefault(root);
            }
            return new BattleHudHpBarHandle { ActorId = actorId, Root = (RectTransform)view.transform, View = view,
                HpFill = view.HealthFill, ShieldFill = view.ShieldFill, WorldOffset = cfg.HpBarWorldOffset };
        }
    }
}
