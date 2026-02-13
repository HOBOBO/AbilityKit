using System;
using System.Collections.Generic;

namespace AbilityKit.Ability.Share.Common.Config
{
    public sealed class FlatJsonSettings
    {
        private readonly Dictionary<string, object> _values;

        public FlatJsonSettings(Dictionary<string, object> values)
        {
            _values = values != null
                ? new Dictionary<string, object>(values, StringComparer.Ordinal)
                : new Dictionary<string, object>(StringComparer.Ordinal);
        }

        public IReadOnlyDictionary<string, object> Values => _values;

        public bool TryGetBool(string key, out bool value)
        {
            value = default;
            if (string.IsNullOrEmpty(key)) return false;
            if (!_values.TryGetValue(key, out var raw) || raw == null) return false;

            if (raw is bool b)
            {
                value = b;
                return true;
            }

            if (raw is int i)
            {
                value = i != 0;
                return true;
            }

            if (raw is long l)
            {
                value = l != 0;
                return true;
            }

            if (raw is float f)
            {
                value = global::System.Math.Abs(f) > 1e-6f;
                return true;
            }

            if (raw is double d)
            {
                value = global::System.Math.Abs(d) > 1e-12;
                return true;
            }

            if (raw is string s)
            {
                if (bool.TryParse(s, out var sb))
                {
                    value = sb;
                    return true;
                }

                if (long.TryParse(s, out var sl))
                {
                    value = sl != 0;
                    return true;
                }
            }

            return false;
        }

        public bool TryGetInt(string key, out int value)
        {
            value = default;
            if (string.IsNullOrEmpty(key)) return false;
            if (!_values.TryGetValue(key, out var raw) || raw == null) return false;

            if (raw is int i)
            {
                value = i;
                return true;
            }

            if (raw is long l)
            {
                if (l is >= int.MinValue and <= int.MaxValue)
                {
                    value = (int)l;
                    return true;
                }
                return false;
            }

            if (raw is float f)
            {
                value = (int)f;
                return true;
            }

            if (raw is double d)
            {
                value = (int)d;
                return true;
            }

            if (raw is bool b)
            {
                value = b ? 1 : 0;
                return true;
            }

            if (raw is string s && int.TryParse(s, out var si))
            {
                value = si;
                return true;
            }

            return false;
        }

        public bool TryGetFloat(string key, out float value)
        {
            value = default;
            if (string.IsNullOrEmpty(key)) return false;
            if (!_values.TryGetValue(key, out var raw) || raw == null) return false;

            if (raw is float f)
            {
                value = f;
                return true;
            }

            if (raw is double d)
            {
                value = (float)d;
                return true;
            }

            if (raw is int i)
            {
                value = i;
                return true;
            }

            if (raw is long l)
            {
                value = l;
                return true;
            }

            if (raw is string s && float.TryParse(s, out var sf))
            {
                value = sf;
                return true;
            }

            return false;
        }

        public bool TryGetString(string key, out string value)
        {
            value = default;
            if (string.IsNullOrEmpty(key)) return false;
            if (!_values.TryGetValue(key, out var raw) || raw == null) return false;

            if (raw is string s)
            {
                value = s;
                return true;
            }

            value = raw.ToString();
            return !string.IsNullOrEmpty(value);
        }

        public static FlatJsonSettings Empty() => new FlatJsonSettings(null);

        public static FlatJsonSettings FromJson(string json, Func<string, Dictionary<string, object>> deserialize)
        {
            if (deserialize == null) return Empty();
            if (string.IsNullOrEmpty(json)) return Empty();
            try
            {
                var dict = deserialize(json);
                return new FlatJsonSettings(dict);
            }
            catch
            {
                return Empty();
            }
        }
    }
}
