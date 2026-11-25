using System;
using System.Collections.Generic;
using System.Globalization;
using AbilityKit.Demo.Moba.Share;
using AbilityKit.Demo.Moba.Share.Config;
using UnityEngine;

namespace AbilityKit.Game.Flow
{
    /// <summary>
    /// Actor 头顶面板控制器；完整资源状态、护盾和技能状态保留各自唯一来源。
    /// </summary>
    internal sealed class BattleHudHpBarController
    {
        private readonly BattleHudConfig _cfg;
        private readonly RectTransform _root;
        private readonly BattleHudCanvasProjector _projector;
        private readonly IBattleHudActorPositionResolver _positionResolver;
        private readonly BattleHudHpBarFactory _factory;
        private readonly BattleHudOverheadCatalog _catalog = new BattleHudOverheadCatalog();
        private readonly Dictionary<int, BattleHudHpBarHandle> _byActorId = new Dictionary<int, BattleHudHpBarHandle>(64);
        private readonly Dictionary<int, Stack<BattleHudHpBarHandle>> _pool = new Dictionary<int, Stack<BattleHudHpBarHandle>>();
        private readonly HashSet<int> _seen = new HashSet<int>();
        private readonly HashSet<int> _retired = new HashSet<int>();
        private readonly Dictionary<int, int> _spawnFrames = new Dictionary<int, int>();
        private readonly Dictionary<int, int> _versions = new Dictionary<int, int>();
        private readonly Dictionary<int, int> _teams = new Dictionary<int, int>();
        private readonly Dictionary<int, ShieldStateData> _shields = new Dictionary<int, ShieldStateData>();
        private readonly List<int> _remove = new List<int>();
        private int _lastFrame = int.MinValue;
        private int _lastShieldFrame = int.MinValue;

        public BattleHudHpBarController(BattleHudConfig cfg, RectTransform root, BattleHudCanvasProjector projector,
            IBattleHudActorPositionResolver positionResolver, BattleHudHpBarFactory factory = null)
        { _cfg = cfg; _root = root; _projector = projector; _positionResolver = positionResolver; _factory = factory ?? new BattleHudHpBarFactory(); }

        public void Ensure(int actorId, int layoutId = 0)
        {
            if (actorId <= 0 || _byActorId.ContainsKey(actorId) || _retired.Contains(actorId)) return;
            var handle = _pool.TryGetValue(layoutId, out var available) && available.Count > 0
                ? available.Pop() : _factory.Create(actorId, _cfg, _root);
            handle.ActorId = actorId; Reset(handle); _byActorId.Add(actorId, handle);
            if (_shields.TryGetValue(actorId, out var shield))
            { handle.Shield = shield.CurrentShield; handle.ShieldCapacity = shield.Capacity; }
        }

        public RectTransform PanelSlot(int actorId, bool cast)
        {
            return _byActorId.TryGetValue(actorId, out var h) ? cast ? h.View.CastSlot : h.View.BuffSlot : null;
        }

        public void ObserveSpawns(int frame, ActorSpawnData[] entries)
        {
            if (entries == null) return;
            foreach (var entry in entries)
            {
                if (_spawnFrames.TryGetValue(entry.ActorId, out var existing) && existing > frame) continue;
                _teams[entry.ActorId] = entry.TeamId;
                if (_versions.TryGetValue(entry.ActorId, out var version) && version == entry.EntityVersion && !_retired.Contains(entry.ActorId)) continue;
                RemoveActor(entry.ActorId); _retired.Remove(entry.ActorId); _spawnFrames[entry.ActorId] = frame;
                _versions[entry.ActorId] = entry.EntityVersion;
            }
        }

        public bool IsReplacement(int frame, int actorId, int version) =>
            (!_spawnFrames.TryGetValue(actorId, out var born) || frame >= born) &&
            _versions.TryGetValue(actorId, out var previous) && previous != version;

        public void Apply(int frame, ActorIndicatorStateData[] entries, int localActorId)
        {
            if (entries == null || frame < _lastFrame) return;
            _lastFrame = frame; _seen.Clear();
            foreach (var pair in _byActorId)
            {
                pair.Value.HasHealth = false;
                foreach (var row in pair.Value.Rows.Values) row.Seen = false;
            }
            foreach (var state in entries)
            {
                if (_retired.Contains(state.ActorId) || (_spawnFrames.TryGetValue(state.ActorId, out var born) && frame < born) ||
                    !_positionResolver.TryGetActorWorldPos(state.ActorId, out _)) continue;
                var layout = _catalog.Layout(state.LayoutId); var definition = _catalog.Indicator(state.IndicatorId);
                if (layout == null || definition == null) continue;
                if (_byActorId.TryGetValue(state.ActorId, out var previous) && previous.LayoutId != layout.Id) Release(state.ActorId);
                Ensure(state.ActorId, layout.Id); var handle = _byActorId[state.ActorId]; _seen.Add(state.ActorId);
                SetLayout(handle, layout);
                if (definition.Semantic == IndicatorSemantic.Health)
                {
                    handle.HasHealth = true;
                    if (state.Availability == 1) UpdateHp(state.ActorId, state.Current, state.Max);
                    else
                    { handle.Hp = handle.MaxHp = 0; BattleHudOverheadView.SetFill(handle.HpFill, 0); handle.View.ValueText.text = "?"; }
                    var friendly = state.ActorId == localActorId || (_teams.TryGetValue(localActorId, out var localTeam) && localTeam > 0 && _teams.TryGetValue(state.ActorId, out var team) && localTeam == team);
                    handle.HpFill.color = friendly ? new Color(0.2f, 0.85f, 0.3f) : new Color(0.9f, 0.24f, 0.2f);
                    handle.View.ValueText.gameObject.SetActive(state.ActorId == localActorId || state.Availability != 1);
                }
                else if (handle.Rows.TryGetValue(state.IndicatorId, out var row)) row.Apply(in state, localActorId);
            }
            _remove.Clear();
            foreach (var pair in _byActorId)
            {
                if (!_seen.Contains(pair.Key)) _remove.Add(pair.Key);
                else foreach (var row in pair.Value.Rows.Values) if (!row.Seen) row.Root.gameObject.SetActive(false);
            }
            foreach (var id in _remove) Release(id);
        }

        private void SetLayout(BattleHudHpBarHandle handle, OverheadLayoutDTO layout)
        {
            if (handle.LayoutId == layout.Id) return;
            handle.LayoutId = layout.Id; handle.WorldOffset = new Vector3(0, layout.WorldOffsetY, 0);
            foreach (var row in handle.Rows.Values) DestroyView(row.Root.gameObject);
            handle.Rows.Clear();
            var healthHeight = 10f;
            foreach (var row in layout.Rows)
                if (_catalog.Indicator(row.IndicatorId).Semantic == IndicatorSemantic.Health) healthHeight = row.Height;
            handle.View.HealthBackground.rectTransform.sizeDelta = new Vector2(layout.Width, healthHeight);
            var offset = healthHeight / 2; var previousSlot = -1; var previousHeight = 0f;
            foreach (var definition in layout.Rows)
            {
                var indicator = _catalog.Indicator(definition.IndicatorId);
                if (indicator.Semantic == IndicatorSemantic.Health) continue;
                if (definition.Slot != previousSlot) { offset += previousHeight + layout.Spacing; previousSlot = definition.Slot; }
                var row = new BattleHudIndicatorRow(definition, layout.Width, handle.View.RowsRoot,
                    definition.Renderer == IndicatorRendererKind.Segments ? Mathf.RoundToInt(indicator.Max) : 0);
                row.Root.anchoredPosition = new Vector2(0, -offset - definition.Height / 2);
                row.Root.gameObject.SetActive(false); handle.Rows.Add(definition.IndicatorId, row); previousHeight = definition.Height;
            }
            handle.Root.sizeDelta = new Vector2(layout.Width, offset + previousHeight + healthHeight / 2);
            handle.View.ValueText.rectTransform.anchoredPosition = new Vector2(0, healthHeight / 2 + 13);
            handle.View.ShieldValue.rectTransform.anchoredPosition = new Vector2(layout.Width / 2 + 26, healthHeight / 2 + 3);
            handle.View.CastSlot.anchoredPosition = new Vector2(0, healthHeight / 2 + 25);
            handle.View.BuffSlot.anchoredPosition = new Vector2(0, healthHeight / 2 + 55);
        }

        public void UpdateHp(int actorId, float hp, float maxHp)
        {
            if (!_byActorId.TryGetValue(actorId, out var handle)) return;
            var changed = handle.Hp != hp || handle.MaxHp != maxHp;
            handle.Hp = hp; handle.MaxHp = maxHp;
            BattleHudOverheadView.SetFill(handle.HpFill, maxHp > 0 ? hp / maxHp : 0);
            if (changed || handle.View.ValueText.text.Length == 0)
                handle.View.ValueText.text = hp.ToString("0", CultureInfo.InvariantCulture) + " / " + maxHp.ToString("0", CultureInfo.InvariantCulture);
            RefreshShield(handle);
        }

        public void UpdateShields(ShieldStateData[] entries)
        {
            if (entries == null) return;
            _shields.Clear();
            foreach (var state in entries) if (!_retired.Contains(state.ActorId)) _shields[state.ActorId] = state;
            foreach (var h in _byActorId.Values) { h.Shield = h.ShieldCapacity = 0; RefreshShield(h); }
            foreach (var state in entries)
                if (_byActorId.TryGetValue(state.ActorId, out var h))
                { h.Shield = state.CurrentShield; h.ShieldCapacity = state.Capacity; RefreshShield(h); }
        }

        public void UpdateShields(int frame, ShieldStateData[] entries)
        {
            if (entries == null || frame < _lastShieldFrame) return;
            _lastShieldFrame = frame;
            UpdateShields(entries);
        }

        private static void RefreshShield(BattleHudHpBarHandle h)
        {
            h.ShieldFill.fillAmount = h.MaxHp > 0 ? Mathf.Clamp01(h.Shield / h.MaxHp) : 0;
            h.ShieldFill.rectTransform.sizeDelta = new Vector2(h.View.HealthBackground.rectTransform.sizeDelta.x * h.ShieldFill.fillAmount, 3);
            h.ShieldFill.rectTransform.pivot = new Vector2(0, 0.5f);
            h.ShieldFill.rectTransform.anchoredPosition = new Vector2(-h.View.HealthBackground.rectTransform.sizeDelta.x / 2,
                h.View.HealthBackground.rectTransform.sizeDelta.y / 2 + 3);
            h.ShieldFill.gameObject.SetActive(h.Shield > 0 && h.MaxHp > 0);
            var overflow = Mathf.Max(0, h.Shield - h.MaxHp);
            h.View.ShieldValue.gameObject.SetActive(overflow > 0 && h.MaxHp > 0);
            if (h.ShieldOverflow != overflow)
            {
                h.ShieldOverflow = overflow;
                h.View.ShieldValue.text = overflow > 0 ? "+" + overflow.ToString("0", CultureInfo.InvariantCulture) : "";
            }
        }

        public void Tick()
        {
            foreach (var h in _byActorId.Values)
            {
                var point = Vector2.zero;
                var visible = h.HasHealth && (h.Hp > 0 || h.MaxHp <= 0) &&
                    _positionResolver.TryGetActorWorldPos(h.ActorId, out var worldPos) &&
                    _projector.TryProject(worldPos + h.WorldOffset, out point);
                h.Root.gameObject.SetActive(visible);
                if (visible) h.Root.anchoredPosition = point;
            }
        }

        public void RemoveActor(int actorId) { Release(actorId); _shields.Remove(actorId); _retired.Add(actorId); }
        public bool AcceptDespawn(int frame, int actorId) => frame >= _lastFrame &&
            (!_spawnFrames.TryGetValue(actorId, out var born) || frame >= born);
        private void Release(int actorId)
        {
            if (!_byActorId.TryGetValue(actorId, out var h)) return;
            _byActorId.Remove(actorId); Reset(h);
            if (!_pool.TryGetValue(h.LayoutId, out var available))
                _pool.Add(h.LayoutId, available = new Stack<BattleHudHpBarHandle>());
            available.Push(h);
        }
        private void Reset(BattleHudHpBarHandle h)
        {
            DetachChildren(h.View.CastSlot); DetachChildren(h.View.BuffSlot);
            h.Hp = h.MaxHp = h.Shield = h.ShieldCapacity = 0; h.HasHealth = false;
            h.ShieldOverflow = -1;
            BattleHudOverheadView.SetFill(h.HpFill, 0); h.ShieldFill.fillAmount = 0;
            h.ShieldFill.gameObject.SetActive(false); h.View.ValueText.text = "";
            h.View.ShieldValue.text = ""; h.View.ShieldValue.gameObject.SetActive(false);
            foreach (var row in h.Rows.Values) row.Root.gameObject.SetActive(false);
            h.Root.gameObject.SetActive(false);
        }
        private void DetachChildren(RectTransform slot)
        {
            while (slot.childCount > 0)
            {
                var child = slot.GetChild(0); child.gameObject.SetActive(false); child.SetParent(_root, false);
            }
        }
        public void ResetBaseline()
        {
            _retired.Clear(); _spawnFrames.Clear(); _versions.Clear(); _teams.Clear();
            ResetIndicatorBaseline();
        }
        public void ResetIndicatorBaseline()
        {
            _lastFrame = _lastShieldFrame = int.MinValue; _shields.Clear();
            foreach (var h in _byActorId.Values) Reset(h);
        }
        public void Clear()
        {
            foreach (var h in _byActorId.Values) if (h.Root != null) DestroyView(h.Root.gameObject);
            foreach (var available in _pool.Values)
                foreach (var h in available) if (h.Root != null) DestroyView(h.Root.gameObject);
            _byActorId.Clear(); _pool.Clear(); _retired.Clear(); _spawnFrames.Clear(); _versions.Clear(); _teams.Clear(); _shields.Clear(); _lastFrame = _lastShieldFrame = int.MinValue;
        }
        private static void DestroyView(GameObject view)
        {
            if (Application.isPlaying) UnityEngine.Object.Destroy(view);
            else UnityEngine.Object.DestroyImmediate(view);
        }
    }
}
