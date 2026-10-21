using System.Collections.Generic;
using UnityEngine;

namespace AbilityKit.Game.Flow
{
    internal sealed class BattleHudHpBarController
    {
        private readonly BattleHudConfig _cfg;
        private readonly RectTransform _root;
        private readonly BattleHudCanvasProjector _projector;
        private readonly IBattleHudActorPositionResolver _positionResolver;
        private readonly BattleHudHpBarFactory _factory;
        private readonly Dictionary<int, BattleHudHpBarHandle> _byActorId = new Dictionary<int, BattleHudHpBarHandle>(64);

        public BattleHudHpBarController(
            BattleHudConfig cfg,
            RectTransform root,
            BattleHudCanvasProjector projector,
            IBattleHudActorPositionResolver positionResolver,
            BattleHudHpBarFactory factory = null)
        {
            _cfg = cfg;
            _root = root;
            _projector = projector;
            _positionResolver = positionResolver;
            _factory = factory ?? new BattleHudHpBarFactory();
        }

        public void Ensure(int actorId)
        {
            if (_byActorId.ContainsKey(actorId)) return;

            _byActorId[actorId] = _factory.Create(actorId, _cfg, _root);
        }

        public void UpdateHp(int actorId, float hp, float maxHp)
        {
            if (!_byActorId.TryGetValue(actorId, out var handle) || handle == null) return;

            handle.Hp = hp;
            handle.MaxHp = maxHp;

            if (handle.HpFill == null) return;

            var denom = Mathf.Max(1f, maxHp);
            handle.HpFill.fillAmount = Mathf.Clamp01(hp / denom);
        }

        public void UpdateShields(AbilityKit.Demo.Moba.Share.ShieldStateData[] entries)
        {
            if (entries == null) return;
            foreach (var pair in _byActorId)
            {
                if (pair.Value?.ShieldFill != null) pair.Value.ShieldFill.gameObject.SetActive(false);
            }
            for (var i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                Ensure(entry.ActorId);
                if (!_byActorId.TryGetValue(entry.ActorId, out var handle) || handle == null || handle.ShieldFill == null) continue;
                var capacity = Mathf.Max(1f, entry.Capacity);
                handle.Shield = entry.CurrentShield;
                handle.ShieldCapacity = entry.Capacity;
                handle.ShieldFill.fillAmount = Mathf.Clamp01(entry.CurrentShield / capacity);
                handle.ShieldFill.gameObject.SetActive(entry.CurrentShield > 0.001f);
            }
        }

        public void Tick()
        {
            foreach (var kv in _byActorId)
            {
                var handle = kv.Value;
                if (handle?.Root == null) continue;
                if (!_positionResolver.TryGetActorWorldPos(handle.ActorId, out var worldPos) ||
                    !_projector.TryProject(worldPos + handle.WorldOffset, out var local))
                {
                    handle.Root.gameObject.SetActive(false);
                    continue;
                }

                handle.Root.anchoredPosition = local;
                if (!handle.Root.gameObject.activeSelf) handle.Root.gameObject.SetActive(true);
            }
        }

        public void RemoveActor(int actorId)
        {
            if (!_byActorId.TryGetValue(actorId, out var hud) || hud == null) return;

            DestroyHandle(hud);
            _byActorId.Remove(actorId);
        }

        public void Clear()
        {
            foreach (var kv in _byActorId)
            {
                DestroyHandle(kv.Value);
            }

            _byActorId.Clear();
        }

        private void DestroyHandle(BattleHudHpBarHandle handle)
        {
            if (handle?.Root != null)
            {
                Object.Destroy(handle.Root.gameObject);
            }
        }
    }
}
