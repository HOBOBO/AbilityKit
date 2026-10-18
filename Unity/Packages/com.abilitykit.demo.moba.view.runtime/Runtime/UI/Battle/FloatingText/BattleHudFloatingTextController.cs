using System.Collections.Generic;
using UnityEngine;

namespace AbilityKit.Game.Flow
{
    internal sealed class BattleHudFloatingTextController
    {
        private readonly BattleHudConfig _cfg;
        private readonly BattleHudCanvasProjector _projector;
        private readonly IBattleHudActorPositionResolver _positionResolver;
        private readonly List<BattleHudFloatingTextHandle> _floating = new List<BattleHudFloatingTextHandle>(64);
        private readonly BattleHudFloatingTextPool _pool;

        public BattleHudFloatingTextController(
            BattleHudConfig cfg,
            RectTransform root,
            BattleHudCanvasProjector projector,
            IBattleHudActorPositionResolver positionResolver,
            BattleHudFloatingTextPool pool = null)
        {
            _cfg = cfg;
            _projector = projector;
            _positionResolver = positionResolver;
            _pool = pool ?? new BattleHudFloatingTextPool(root);
        }

        public void Spawn(int targetActorId, string text, bool heal)
        {
            var floating = _pool.Rent();

            floating.TargetActorId = targetActorId;
            floating.Age = 0f;
            floating.Lifetime = _cfg.FloatingTextLifetime;
            floating.WorldOffset = _cfg.FloatingTextWorldOffset;
            floating.ScreenOffset = Random.insideUnitCircle * _cfg.FloatingTextSpreadPixels;

            if (floating.Text != null)
            {
                floating.Text.text = text;
                floating.Text.color = heal ? _cfg.HealTextColor : _cfg.DamageTextColor;
            }

            _floating.Add(floating);
        }

        public void Tick(float deltaTime)
        {
            for (var i = _floating.Count - 1; i >= 0; i--)
            {
                var floating = _floating[i];
                if (floating?.Root == null)
                {
                    _floating.RemoveAt(i);
                    continue;
                }

                floating.Age += deltaTime;
                if (floating.Age >= floating.Lifetime)
                {
                    RecycleAt(i, floating);
                    continue;
                }

                TickFloatingText(floating);
            }
        }

        public void RemoveActor(int actorId)
        {
            for (var i = _floating.Count - 1; i >= 0; i--)
            {
                var floating = _floating[i];
                if (floating == null)
                {
                    _floating.RemoveAt(i);
                    continue;
                }

                if (floating.TargetActorId != actorId) continue;

                RecycleAt(i, floating);
            }
        }

        public void Clear()
        {
            for (var i = 0; i < _floating.Count; i++)
            {
                _pool.DestroyHandle(_floating[i]);
            }

            _floating.Clear();
            _pool.Clear();
        }

        private void TickFloatingText(BattleHudFloatingTextHandle floating)
        {
            if (!_positionResolver.TryGetActorWorldPos(floating.TargetActorId, out var worldPos)) return;

            var t = floating.Age / Mathf.Max(0.001f, floating.Lifetime);
            var rise = Mathf.Lerp(0f, _cfg.FloatingTextRisePixels, t);
            if (_projector.TryProject(worldPos + floating.WorldOffset, out var local))
            {
                floating.Root.anchoredPosition = local + floating.ScreenOffset + new Vector2(0f, rise);
            }

            if (floating.Text != null)
            {
                var color = floating.Text.color;
                color.a = 1f - t;
                floating.Text.color = color;
            }
        }

        private void RecycleAt(int index, BattleHudFloatingTextHandle floating)
        {
            _pool.Recycle(floating);
            _floating.RemoveAt(index);
        }
    }
}
