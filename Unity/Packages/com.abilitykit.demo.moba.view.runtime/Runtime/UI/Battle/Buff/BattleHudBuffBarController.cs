using System.Collections.Generic;
using AbilityKit.Demo.Moba.Share;
using UnityEngine;

namespace AbilityKit.Game.Flow.Battle.Hud
{
    /// <summary>
    /// Manages per-actor <see cref="BattleHudBuffBar"/> instances:
    /// creates one lazily when a Buff cue arrives for an actor,
    /// destroys it on entity destroyed,
    /// and projects each bar to the actor's world position every Tick.
    /// </summary>
    internal sealed class BattleHudBuffBarController
    {
        private readonly BattleHudConfig _cfg;
        private readonly RectTransform _root;
        private readonly BattleHudCanvasProjector _projector;
        private readonly IBattleHudActorPositionResolver _positionResolver;
        private readonly BattleHudBuffBarFactory _factory;

        private readonly Dictionary<int, BattleHudBuffBar> _barsByActorId = new Dictionary<int, BattleHudBuffBar>(32);
        private readonly Dictionary<string, int> _actorByInstanceKey = new Dictionary<string, int>(64);

        public BattleHudBuffBarController(
            BattleHudConfig cfg,
            RectTransform root,
            BattleHudCanvasProjector projector,
            IBattleHudActorPositionResolver positionResolver,
            BattleHudBuffBarFactory factory = null)
        {
            _cfg = cfg;
            _root = root;
            _projector = projector;
            _positionResolver = positionResolver;
            _factory = factory ?? new BattleHudBuffBarFactory();
        }

        public int BarCount => _barsByActorId.Count;

        public int IconCount
        {
            get
            {
                var total = 0;
                foreach (var kv in _barsByActorId)
                {
                    if (kv.Value == null) continue;
                    total += kv.Value.IconCount;
                }
                return total;
            }
        }

        public void Ensure(int actorId)
        {
            if (actorId <= 0) return;
            if (_barsByActorId.ContainsKey(actorId)) return;
            var bar = _factory.Create(actorId, _cfg, _root);
            _barsByActorId[actorId] = bar;
        }

        public void HandleCues(IReadOnlyList<PresentationCueData> entries)
        {
            if (entries == null || entries.Count == 0) return;
            var localOnly = _cfg.BuffBarOnlyLocalActor;

            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (!BattleHudBuffCueFilter.IsBuffCue(in entry)) continue;

                var actorId = entry.TargetActorId;
                if (actorId <= 0) continue;
                if (localOnly && !IsLocalActor(actorId)) continue;

                var stage = entry.Stage;
                var isRemove = BattleHudBuffCueFilter.IsBuffRemoveStage(stage);
                var isActive = BattleHudBuffCueFilter.IsBuffActiveStage(stage);

                if (isRemove)
                {
                    RemoveBuff(actorId, entry.InstanceKey);
                    continue;
                }
                if (!isActive) continue;

                Ensure(actorId);
                if (!_barsByActorId.TryGetValue(actorId, out var bar) || bar == null) continue;

                var totalSecondsHint = ResolveTotalSecondsHint(entry);
                bar.ApplyCue(in entry, totalSecondsHint, _factory.IconFactory);
                _actorByInstanceKey[entry.InstanceKey] = actorId;
            }
        }

        public void Tick(float deltaTime)
        {
            // Decay every active icon by real time.
            foreach (var kv in _barsByActorId)
            {
                var bar = kv.Value;
                bar?.Tick();
            }

            // Re-anchor bars whose actor positions are known.
            foreach (var kv in _barsByActorId)
            {
                var bar = kv.Value;
                if (bar?.Root == null) continue;
                if (!_positionResolver.TryGetActorWorldPos(bar.ActorId, out var worldPos)) continue;
                if (!_projector.TryProject(worldPos + _cfg.BuffBarWorldOffset, out var local)) continue;
                bar.Root.anchoredPosition = local;
            }
        }

        public void RemoveActor(int actorId)
        {
            if (!_barsByActorId.TryGetValue(actorId, out var bar) || bar == null)
            {
                _barsByActorId.Remove(actorId);
                return;
            }

            // Drop actor-key mappings pointing at this actor.
            var toRemove = new List<string>();
            foreach (var kv in _actorByInstanceKey)
            {
                if (kv.Value == actorId) toRemove.Add(kv.Key);
            }
            for (var i = 0; i < toRemove.Count; i++)
            {
                _actorByInstanceKey.Remove(toRemove[i]);
            }

            if (bar.Root != null) Object.Destroy(bar.Root.gameObject);
            _barsByActorId.Remove(actorId);
        }

        public void Clear()
        {
            foreach (var kv in _barsByActorId)
            {
                if (kv.Value?.Root != null) Object.Destroy(kv.Value.Root.gameObject);
            }
            _barsByActorId.Clear();
            _actorByInstanceKey.Clear();
        }

        private void RemoveBuff(int actorId, string instanceKey)
        {
            if (string.IsNullOrEmpty(instanceKey)) return;
            if (!_barsByActorId.TryGetValue(actorId, out var bar) || bar == null) return;
            bar.RemoveIcon(instanceKey);
            _actorByInstanceKey.Remove(instanceKey);
        }

        private static float ResolveTotalSecondsHint(in PresentationCueData entry)
        {
            if (entry.DurationMsOverride > 0) return entry.DurationMsOverride / 1000f;
            return 0f;
        }

        private bool IsLocalActor(int actorId)
        {
            // Reserved for future "local player only" filtering; conservatively returns true so the HUD
            // is visible during local play (where there is no authoritative local actor filter yet).
            return actorId > 0;
        }
    }
}
