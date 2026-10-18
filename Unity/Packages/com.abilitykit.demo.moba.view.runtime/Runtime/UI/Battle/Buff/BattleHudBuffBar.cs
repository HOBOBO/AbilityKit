using System.Collections.Generic;
using AbilityKit.Demo.Moba.Share;
using UnityEngine;
using UnityEngine.UI;

namespace AbilityKit.Game.Flow.Battle.Hud
{
    /// <summary>
    /// Per-actor horizontal container that holds <see cref="BattleHudBuffIconView"/> children.
    /// Created lazily by <see cref="BattleHudBuffBarController"/> and anchored above the actor each frame.
    /// </summary>
    internal sealed class BattleHudBuffBar : MonoBehaviour
    {
        [SerializeField] private RectTransform _root;
        [SerializeField] private HorizontalLayoutGroup _layout;

        private readonly List<BattleHudBuffIconView> _icons = new List<BattleHudBuffIconView>(8);
        private readonly Dictionary<string, int> _iconIndexByInstanceKey = new Dictionary<string, int>(8);
        private readonly Queue<BattleHudBuffIconView> _freePool = new Queue<BattleHudBuffIconView>(8);

        public int ActorId { get; private set; }

        public RectTransform Root => _root;

        public static BattleHudBuffBar Create(RectTransform root, HorizontalLayoutGroup layout)
        {
            var go = root.gameObject;
            var bar = go.GetComponent<BattleHudBuffBar>() ?? go.AddComponent<BattleHudBuffBar>();
            bar._root = root;
            bar._layout = layout;
            return bar;
        }

        public void Bind(int actorId)
        {
            ActorId = actorId;
        }

        public bool HasIcon(string instanceKey)
        {
            if (string.IsNullOrEmpty(instanceKey)) return false;
            return _iconIndexByInstanceKey.ContainsKey(instanceKey);
        }

        public bool TryGetIcon(string instanceKey, out BattleHudBuffIconView icon)
        {
            icon = null;
            if (string.IsNullOrEmpty(instanceKey)) return false;
            if (!_iconIndexByInstanceKey.TryGetValue(instanceKey, out var idx)) return false;
            if (idx < 0 || idx >= _icons.Count) return false;
            icon = _icons[idx];
            return icon != null;
        }

        public int IconCount => _icons.Count;

        public void ApplyCue(in PresentationCueData entry, float totalSecondsHint, BattleHudBuffIconFactory factory)
        {
            if (string.IsNullOrEmpty(entry.InstanceKey)) return;
            if (!TryGetIcon(entry.InstanceKey, out var icon))
            {
                icon = AcquireIcon(factory);
                if (icon == null) return;
                _iconIndexByInstanceKey[entry.InstanceKey] = _icons.Count;
                _icons.Add(icon);
            }
            icon.Apply(in entry, totalSecondsHint);
        }

        public void RemoveIcon(string instanceKey)
        {
            if (string.IsNullOrEmpty(instanceKey)) return;
            if (!_iconIndexByInstanceKey.TryGetValue(instanceKey, out var idx)) return;
            _iconIndexByInstanceKey.Remove(instanceKey);

            if (idx < 0 || idx >= _icons.Count) return;
            var icon = _icons[idx];
            icon.ResetState();
            _icons.RemoveAt(idx);
            _freePool.Enqueue(icon);
            // Reindex subsequent entries because removal shifted indices.
            RebuildIndex();
        }

        private void RebuildIndex()
        {
            _iconIndexByInstanceKey.Clear();
            for (var i = 0; i < _icons.Count; i++)
            {
                var icon = _icons[i];
                if (icon == null) continue;
                if (string.IsNullOrEmpty(icon.InstanceKey)) continue;
                _iconIndexByInstanceKey[icon.InstanceKey] = i;
            }
        }

        public void Tick()
        {
            for (var i = 0; i < _icons.Count; i++)
            {
                _icons[i]?.TickDecay();
            }
        }

        public void Clear()
        {
            for (var i = 0; i < _icons.Count; i++)
            {
                _icons[i]?.ResetState();
            }
            _icons.Clear();
            _iconIndexByInstanceKey.Clear();
        }

        public void DestroyAllIcons()
        {
            for (var i = 0; i < _icons.Count; i++)
            {
                var icon = _icons[i];
                if (icon != null && icon.gameObject != null) Object.Destroy(icon.gameObject);
            }
            _icons.Clear();
            _iconIndexByInstanceKey.Clear();

            while (_freePool.Count > 0)
            {
                var icon = _freePool.Dequeue();
                if (icon != null && icon.gameObject != null) Object.Destroy(icon.gameObject);
            }
        }

        private BattleHudBuffIconView AcquireIcon(BattleHudBuffIconFactory factory)
        {
            if (factory == null) return null;
            if (_freePool.Count > 0)
            {
                var pooled = _freePool.Dequeue();
                if (pooled != null) return pooled;
            }
            var parent = _layout != null ? (RectTransform)_layout.transform : _root;
            return factory.Create(parent);
        }
    }
}
