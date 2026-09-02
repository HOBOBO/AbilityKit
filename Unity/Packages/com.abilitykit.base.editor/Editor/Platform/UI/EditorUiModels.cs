#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using AbilityKit.Editor.Platform.State;
using UnityEngine;

namespace AbilityKit.Editor.Platform.UI
{
    public sealed class EditorSearchState
    {
        private string _text = string.Empty;

        public event Action Changed;

        public string Text
        {
            get => _text;
            set
            {
                var next = value ?? string.Empty;
                if (string.Equals(_text, next, StringComparison.Ordinal)) return;
                _text = next;
                Changed?.Invoke();
            }
        }

        public bool IsEmpty => string.IsNullOrWhiteSpace(_text);

        public bool Matches(params string[] candidates)
        {
            if (IsEmpty) return true;
            return candidates != null && candidates.Any(candidate =>
                !string.IsNullOrEmpty(candidate)
                && candidate.IndexOf(_text, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        public void Clear()
        {
            Text = string.Empty;
        }
    }

    public sealed class EditorSplitterState
    {
        private readonly IEditorUserStateStore _store;
        private readonly string _stateKey;
        private float _position;

        public EditorSplitterState(
            float defaultPosition,
            float minimumPosition = 100f,
            float maximumPosition = 1000f,
            IEditorUserStateStore store = null,
            string stateKey = "splitter.position")
        {
            if (minimumPosition < 0f) throw new ArgumentOutOfRangeException(nameof(minimumPosition));
            if (maximumPosition < minimumPosition) throw new ArgumentOutOfRangeException(nameof(maximumPosition));
            if (string.IsNullOrWhiteSpace(stateKey)) throw new ArgumentException("State key is required.", nameof(stateKey));

            MinimumPosition = minimumPosition;
            MaximumPosition = maximumPosition;
            _store = store;
            _stateKey = stateKey;
            _position = Clamp(store?.GetFloat(stateKey, defaultPosition) ?? defaultPosition);
        }

        public event Action Changed;

        public float MinimumPosition { get; }
        public float MaximumPosition { get; }

        public float Position
        {
            get => _position;
            set
            {
                var next = Clamp(value);
                if (Mathf.Approximately(_position, next)) return;
                _position = next;
                _store?.SetFloat(_stateKey, next);
                Changed?.Invoke();
            }
        }

        private float Clamp(float value)
        {
            return Mathf.Clamp(value, MinimumPosition, MaximumPosition);
        }
    }

    public sealed class EditorTabDescriptor
    {
        public EditorTabDescriptor(
            string id,
            string labelKey,
            string tooltipKey = null,
            string commandId = null,
            int order = 0,
            Func<bool> isVisible = null,
            Func<bool> isEnabled = null)
        {
            Id = RequireValue(id, nameof(id));
            LabelKey = RequireValue(labelKey, nameof(labelKey));
            TooltipKey = tooltipKey ?? string.Empty;
            CommandId = commandId ?? string.Empty;
            Order = order;
            IsVisible = isVisible;
            IsEnabled = isEnabled;
        }

        public string Id { get; }
        public string LabelKey { get; }
        public string TooltipKey { get; }
        public string CommandId { get; }
        public int Order { get; }
        public Func<bool> IsVisible { get; }
        public Func<bool> IsEnabled { get; }

        private static string RequireValue(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("A non-empty value is required.", parameterName);
            }

            return value;
        }
    }

    public sealed class EditorTabState
    {
        private readonly List<EditorTabDescriptor> _tabs;
        private readonly IEditorUserStateStore _store;
        private readonly string _stateKey;
        private string _selectedId;

        public EditorTabState(
            IEnumerable<EditorTabDescriptor> tabs,
            string defaultSelectedId = null,
            IEditorUserStateStore store = null,
            string stateKey = "tabs.selected")
        {
            if (tabs == null) throw new ArgumentNullException(nameof(tabs));
            if (string.IsNullOrWhiteSpace(stateKey)) throw new ArgumentException("State key is required.", nameof(stateKey));

            _tabs = tabs.Where(tab => tab != null)
                .OrderBy(tab => tab.Order)
                .ThenBy(tab => tab.Id, StringComparer.Ordinal)
                .ToList();
            if (_tabs.Select(tab => tab.Id).Distinct(StringComparer.Ordinal).Count() != _tabs.Count)
            {
                throw new ArgumentException("Tab ids must be unique.", nameof(tabs));
            }

            _store = store;
            _stateKey = stateKey;
            var restored = store?.GetString(stateKey, defaultSelectedId ?? string.Empty);
            _selectedId = ResolveSelection(restored) ?? ResolveSelection(defaultSelectedId) ?? FirstAvailableId();
        }

        public event Action Changed;

        public IReadOnlyList<EditorTabDescriptor> Tabs => _tabs;
        public string SelectedId => _selectedId ?? string.Empty;
        public EditorTabDescriptor SelectedTab => _tabs.FirstOrDefault(tab =>
            string.Equals(tab.Id, _selectedId, StringComparison.Ordinal));

        public bool Select(string id)
        {
            var next = ResolveSelection(id);
            if (next == null || string.Equals(_selectedId, next, StringComparison.Ordinal)) return false;
            _selectedId = next;
            _store?.SetString(_stateKey, next);
            Changed?.Invoke();
            return true;
        }

        public void EnsureValidSelection()
        {
            if (ResolveSelection(_selectedId) != null) return;
            var next = FirstAvailableId();
            if (string.Equals(_selectedId, next, StringComparison.Ordinal)) return;
            _selectedId = next;
            if (!string.IsNullOrEmpty(next)) _store?.SetString(_stateKey, next);
            Changed?.Invoke();
        }

        private string ResolveSelection(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return null;
            var tab = _tabs.FirstOrDefault(candidate =>
                string.Equals(candidate.Id, id, StringComparison.Ordinal)
                && (candidate.IsVisible?.Invoke() ?? true)
                && (candidate.IsEnabled?.Invoke() ?? true));
            return tab?.Id;
        }

        private string FirstAvailableId()
        {
            return _tabs.FirstOrDefault(tab =>
                (tab.IsVisible?.Invoke() ?? true)
                && (tab.IsEnabled?.Invoke() ?? true))?.Id;
        }
    }

    public enum EditorStatusKind
    {
        Neutral = 0,
        Success = 1,
        Info = 2,
        Warning = 3,
        Error = 4
    }

    public sealed class EditorStatusBadgeModel
    {
        public EditorStatusBadgeModel(
            string textKey,
            EditorStatusKind kind = EditorStatusKind.Neutral,
            int? count = null,
            string tooltipKey = null)
        {
            if (string.IsNullOrWhiteSpace(textKey)) throw new ArgumentException("Text key is required.", nameof(textKey));
            TextKey = textKey;
            Kind = kind;
            Count = count;
            TooltipKey = tooltipKey ?? string.Empty;
        }

        public string TextKey { get; }
        public EditorStatusKind Kind { get; }
        public int? Count { get; }
        public string TooltipKey { get; }
    }
}
#endif
