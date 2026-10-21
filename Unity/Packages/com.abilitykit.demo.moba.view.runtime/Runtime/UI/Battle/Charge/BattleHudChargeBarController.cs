using System.Collections.Generic;
using AbilityKit.Demo.Moba.Share;
using UnityEngine;

namespace AbilityKit.Game.Flow.Battle.Hud
{
    /// <summary>
    /// Shows the active charge window beside an actor's HP bar. Charge recovery
    /// and cooldown snapshots deliberately do not create a bar.
    /// </summary>
    internal sealed class BattleHudChargeBarController
    {
        private readonly BattleHudConfig _cfg;
        private readonly RectTransform _root;
        private readonly BattleHudCanvasProjector _projector;
        private readonly IBattleHudActorPositionResolver _positionResolver;
        private readonly BattleHudChargeBarFactory _factory;
        private readonly Dictionary<int, BattleHudChargeBarHandle> _barsByActorId =
            new Dictionary<int, BattleHudChargeBarHandle>(8);

        public BattleHudChargeBarController(
            BattleHudConfig cfg,
            RectTransform root,
            BattleHudCanvasProjector projector,
            IBattleHudActorPositionResolver positionResolver,
            BattleHudChargeBarFactory factory = null)
        {
            _cfg = cfg;
            _root = root;
            _projector = projector;
            _positionResolver = positionResolver;
            _factory = factory ?? new BattleHudChargeBarFactory();
        }

        public int VisibleCount
        {
            get
            {
                var count = 0;
                foreach (var pair in _barsByActorId)
                    if (pair.Value?.Root != null && pair.Value.Root.gameObject.activeSelf) count++;
                return count;
            }
        }

        public void Apply(IReadOnlyList<SkillStateData> entries, int localActorId)
        {
            if (localActorId <= 0) return;
            var found = false;
            if (entries != null)
            {
                for (var i = 0; i < entries.Count; i++)
                {
                    var entry = entries[i];
                    if (entry.ActorId != localActorId || entry.CastingState != SkillCastingState.Charging) continue;
                    if (entry.Slot <= 0) continue;

                    var bar = Ensure(localActorId, entry.Slot);
                    if (bar == null) continue;
                    if (bar.Fill != null) bar.Fill.fillAmount = entry.CastProgress;
                    bar.SetVisible(true);
                    found = true;
                    break;
                }
            }

            if (!found && _barsByActorId.TryGetValue(localActorId, out var existing))
                existing?.SetVisible(false);
        }

        public void Tick()
        {
            foreach (var pair in _barsByActorId)
            {
                var bar = pair.Value;
                if (bar?.Root == null || !bar.Root.gameObject.activeSelf) continue;
                if (!_positionResolver.TryGetActorWorldPos(bar.ActorId, out var worldPos)) continue;
                if (!_projector.TryProject(worldPos + bar.WorldOffset, out var local)) continue;
                bar.Root.anchoredPosition = local;
            }
        }

        public void RemoveActor(int actorId)
        {
            if (!_barsByActorId.TryGetValue(actorId, out var bar)) return;
            if (bar?.Root != null) Object.Destroy(bar.Root.gameObject);
            _barsByActorId.Remove(actorId);
        }

        public void Clear()
        {
            foreach (var pair in _barsByActorId)
                if (pair.Value?.Root != null) Object.Destroy(pair.Value.Root.gameObject);
            _barsByActorId.Clear();
        }

        private BattleHudChargeBarHandle Ensure(int actorId, int slot)
        {
            if (_barsByActorId.TryGetValue(actorId, out var existing))
            {
                if (existing.Slot == slot) return existing;
                if (existing.Root != null) Object.Destroy(existing.Root.gameObject);
                _barsByActorId.Remove(actorId);
            }

            var created = _factory.Create(actorId, slot, _cfg, _root);
            _barsByActorId[actorId] = created;
            return created;
        }
    }
}
