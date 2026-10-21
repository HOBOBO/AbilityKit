using System;
using System.Collections.Generic;
using AbilityKit.Demo.Moba.Share;
using AbilityKit.Game.Flow.Battle.Hud;
using UnityEngine;
using UnityEngine.UI;

namespace AbilityKit.Game.Flow
{
    internal sealed class BattleHudSkillBuffStatusBar : MonoBehaviour
    {
        private const int MaxVisibleItems = 4;
        private const float ItemWidth = 58f;
        private const float ItemHeight = 30f;

        private readonly Dictionary<string, Status> _active = new Dictionary<string, Status>();
        private readonly Dictionary<int, BuffHudRow> _templates = new Dictionary<int, BuffHudRow>();
        private readonly HashSet<string> _baselineKeys = new HashSet<string>();
        private readonly List<string> _scratchKeys = new List<string>();
        private readonly List<string> _activeKeys = new List<string>();
        private readonly Text[] _views = new Text[MaxVisibleItems];
        private RectTransform _root;
        private int _actorId;

        public static BattleHudSkillBuffStatusBar Create(Transform parent)
        {
            var go = new GameObject("SkillBuffStatusBar", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(RectMask2D), typeof(BattleHudSkillBuffStatusBar));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);
            rect.anchoredPosition = new Vector2(-450f, 150f);
            rect.sizeDelta = new Vector2(MaxVisibleItems * ItemWidth + 3f * 3f, ItemHeight + 4f);
            var layout = go.GetComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleRight;
            layout.spacing = 3f;
            layout.childControlWidth = layout.childControlHeight = false;
            var view = go.GetComponent<BattleHudSkillBuffStatusBar>();
            view._root = rect;
            view.LoadTemplates();
            return view;
        }

        public void SetActor(int actorId)
        {
            if (_actorId != actorId)
            {
                _active.Clear();
                Refresh();
            }
            _actorId = actorId;
        }

        public void Apply(PresentationCueData[] entries)
        {
            if (entries == null) return;
            var hasBaseline = false;
            _baselineKeys.Clear();
            foreach (var entry in entries)
            {
                if (entry.SnapshotKind == PresentationCueSnapshotKind.ActiveBaseline)
                {
                    hasBaseline = true;
                    if (entry.TargetActorId == _actorId && !string.IsNullOrEmpty(entry.InstanceKey))
                        _baselineKeys.Add(entry.InstanceKey);
                }
                else if (entry.SnapshotKind == PresentationCueSnapshotKind.ActiveBaselineEnd)
                {
                    hasBaseline = true;
                    continue;
                }

                if (!BattleHudStatusCueFilter.IsHudStatusCue(in entry) || entry.TargetActorId != _actorId) continue;
                if (BattleHudStatusCueFilter.IsStatusRemoveStage(entry.Stage))
                {
                    _active.Remove(entry.InstanceKey);
                    continue;
                }
                if (!BattleHudStatusCueFilter.IsStatusActiveStage(entry.Stage)) continue;

                if (BattleHudStatusCueFilter.IsTagCue(in entry))
                {
                    if (!BattleHudStatusCueFilter.TryResolveTagPresentation(in entry, out var tag)) continue;
                    var tagLabel = string.IsNullOrWhiteSpace(tag.DisplayName) ? tag.LocalizationKey : tag.DisplayName;
                    _active[entry.InstanceKey] = new Status(
                        tagLabel,
                        0f,
                        false,
                        false,
                        0,
                        new Color(tag.ColorR, tag.ColorG, tag.ColorB, tag.ColorA),
                        tag.Priority);
                }
                else
                {
                    var buffId = BattleHudBuffIconView.ExtractBuffIdFromInstanceKey(entry.InstanceKey);
                    if (!_templates.TryGetValue(buffId, out var template)) continue;
                    var label = string.IsNullOrEmpty(template.ShortName) ? template.Name : template.ShortName;
                    var remaining = Mathf.Max(0f, entry.RemainingSeconds);
                    _active[entry.InstanceKey] = new Status(
                        label,
                        remaining,
                        template.ShowCountdown && remaining > 0f,
                        template.ShowDisabledHint,
                        entry.StackCount,
                        new Color(0.12f, 0.2f, 0.24f, 0.94f),
                        0);
                }
                if (entry.SnapshotKind == PresentationCueSnapshotKind.ActiveBaseline)
                    _baselineKeys.Add(entry.InstanceKey);
            }

            if (hasBaseline)
            {
                _scratchKeys.Clear();
                foreach (var key in _active.Keys)
                {
                    if (!_baselineKeys.Contains(key)) _scratchKeys.Add(key);
                }
                for (var i = 0; i < _scratchKeys.Count; i++) _active.Remove(_scratchKeys[i]);
            }
            Refresh();
        }

        private void Update()
        {
            var dt = Time.unscaledDeltaTime;
            _scratchKeys.Clear();
            _activeKeys.Clear();
            foreach (var key in _active.Keys) _activeKeys.Add(key);
            for (var i = 0; i < _activeKeys.Count; i++)
            {
                var key = _activeKeys[i];
                if (!_active.TryGetValue(key, out var status)) continue;
                if (status.ShowCountdown) status.Remaining = Mathf.Max(0f, status.Remaining - dt);
                _active[key] = status;
                if (status.ShowCountdown && status.Remaining <= 0f) _scratchKeys.Add(key);
            }
            for (var i = 0; i < _scratchKeys.Count; i++) _active.Remove(_scratchKeys[i]);
            if (AnyCountdownBoundaryChanged()) Refresh();
        }

        private void LoadTemplates()
        {
            var asset = Resources.Load<TextAsset>("luban/moba/buff_hud_templates");
            if (asset == null) return;
            var data = JsonUtility.FromJson<Catalog>("{\"Items\":" + asset.text + "}");
            if (data?.Items == null) return;
            foreach (var row in data.Items) if (row != null) _templates[row.Id] = row;
        }

        private void Refresh()
        {
            var index = 0;
            _activeKeys.Clear();
            foreach (var key in _active.Keys) _activeKeys.Add(key);
            _activeKeys.Sort((left, right) =>
            {
                var priority = _active[right].Priority.CompareTo(_active[left].Priority);
                return priority != 0 ? priority : string.CompareOrdinal(left, right);
            });
            for (var i = 0; i < _activeKeys.Count && index < MaxVisibleItems; i++)
            {
                var key = _activeKeys[i];
                if (!_active.TryGetValue(key, out var status)) continue;
                var text = _views[index];
                if (text == null) _views[index] = text = CreateItem(index);
                text.gameObject.SetActive(true);
                text.GetComponent<Image>().color = status.Color;
                var countdown = status.ShowCountdown ? Mathf.CeilToInt(status.Remaining) + "s" : string.Empty;
                var stacks = status.StackCount > 1 ? " x" + status.StackCount : string.Empty;
                var disabled = status.ShowDisabledHint ? "禁用" : string.Empty;
                var detail = disabled.Length > 0 ? disabled : stacks + countdown;
                text.text = string.IsNullOrEmpty(detail) ? status.Label : status.Label + "\n" + detail;
                status.ShownSeconds = status.ShowCountdown ? Mathf.CeilToInt(status.Remaining) : 0;
                _active[key] = status;
                index++;
            }
            for (; index < MaxVisibleItems; index++)
                if (_views[index] != null) _views[index].gameObject.SetActive(false);
        }

        private bool AnyCountdownBoundaryChanged()
        {
            foreach (var status in _active.Values)
                if (status.ShowCountdown && status.ShownSeconds != Mathf.CeilToInt(status.Remaining)) return true;
            return false;
        }

        private Text CreateItem(int index)
        {
            var go = new GameObject("Buff" + index, typeof(RectTransform), typeof(Image), typeof(LayoutElement), typeof(Text));
            go.transform.SetParent(_root, false);
            go.GetComponent<Image>().color = new Color(0.12f, 0.2f, 0.24f, 0.94f);
            var layout = go.GetComponent<LayoutElement>(); layout.preferredWidth = ItemWidth; layout.preferredHeight = ItemHeight;
            var text = go.GetComponent<Text>(); text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); text.fontSize = 10; text.alignment = TextAnchor.MiddleCenter; text.color = Color.white; text.raycastTarget = false; text.horizontalOverflow = HorizontalWrapMode.Overflow; text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }

        private void OnDisable()
        {
            _active.Clear();
            _scratchKeys.Clear();
            _activeKeys.Clear();
            _baselineKeys.Clear();
        }

        [Serializable] private sealed class Catalog { public BuffHudRow[] Items; }
        [Serializable] private sealed class BuffHudRow { public int Id; public string Name; public string ShortName; public bool ShowCountdown; public bool ShowDisabledHint; }
        private struct Status
        {
            public string Label; public float Remaining; public bool ShowCountdown; public bool ShowDisabledHint; public int StackCount; public int ShownSeconds; public Color Color; public int Priority;
            public Status(string label, float remaining, bool countdown, bool disabled, int stackCount, Color color, int priority) { Label = label; Remaining = remaining; ShowCountdown = countdown; ShowDisabledHint = disabled; StackCount = stackCount; Color = color; Priority = priority; ShownSeconds = countdown ? Mathf.CeilToInt(remaining) : 0; }
        }
    }
}
