using System;
using System.Collections.Generic;

namespace AbilityKit.Core.Common.Config
{
    public sealed class LayeredJsonSettingsStore
    {
        private readonly Dictionary<string, object> _base = new Dictionary<string, object>(StringComparer.Ordinal);
        private readonly Dictionary<string, object> _persistent = new Dictionary<string, object>(StringComparer.Ordinal);
        private readonly Dictionary<string, object> _overrides = new Dictionary<string, object>(StringComparer.Ordinal);

        public event Action<string> OnChanged;

        public IReadOnlyDictionary<string, object> BaseValues => _base;
        public IReadOnlyDictionary<string, object> PersistentValues => _persistent;
        public IReadOnlyDictionary<string, object> OverrideValues => _overrides;

        public void ReplaceBase(FlatJsonSettings settings)
        {
            _base.Clear();
            if (settings != null)
            {
                foreach (var kv in settings.Values)
                {
                    if (string.IsNullOrEmpty(kv.Key)) continue;
                    _base[kv.Key] = kv.Value;
                }
            }
            OnChanged?.Invoke(null);
        }

        public void ReplacePersistent(FlatJsonSettings settings)
        {
            _persistent.Clear();
            if (settings != null)
            {
                foreach (var kv in settings.Values)
                {
                    if (string.IsNullOrEmpty(kv.Key)) continue;
                    _persistent[kv.Key] = kv.Value;
                }
            }
            OnChanged?.Invoke(null);
        }

        public void ClearOverrides()
        {
            if (_overrides.Count == 0) return;
            _overrides.Clear();
            OnChanged?.Invoke(null);
        }

        public void SetOverride(string key, object value)
        {
            if (string.IsNullOrEmpty(key)) return;
            _overrides[key] = value;
            OnChanged?.Invoke(key);
        }

        public bool ClearOverride(string key)
        {
            if (string.IsNullOrEmpty(key)) return false;
            if (!_overrides.Remove(key)) return false;
            OnChanged?.Invoke(key);
            return true;
        }

        public bool TryGetRaw(string key, out object value)
        {
            value = default;
            if (string.IsNullOrEmpty(key)) return false;

            if (_overrides.TryGetValue(key, out value)) return true;
            if (_persistent.TryGetValue(key, out value)) return true;
            if (_base.TryGetValue(key, out value)) return true;
            return false;
        }

        public bool TryGetBool(string key, out bool value) => new FlatJsonSettings(BuildEffectiveView()).TryGetBool(key, out value);
        public bool TryGetInt(string key, out int value) => new FlatJsonSettings(BuildEffectiveView()).TryGetInt(key, out value);
        public bool TryGetFloat(string key, out float value) => new FlatJsonSettings(BuildEffectiveView()).TryGetFloat(key, out value);
        public bool TryGetString(string key, out string value) => new FlatJsonSettings(BuildEffectiveView()).TryGetString(key, out value);

        public Dictionary<string, object> BuildEffectiveView()
        {
            var dict = new Dictionary<string, object>(_base, StringComparer.Ordinal);
            foreach (var kv in _persistent) dict[kv.Key] = kv.Value;
            foreach (var kv in _overrides) dict[kv.Key] = kv.Value;
            return dict;
        }
    }
}
