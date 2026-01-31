using System;
using System.Globalization;

namespace AbilityKit.Game.Battle.Agent
{
    internal static class TinyJson
    {
        public static string TryGetString(string json, string key)
        {
            if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(key)) return null;

            var pattern = "\"" + key + "\"";
            var i = json.IndexOf(pattern, StringComparison.Ordinal);
            if (i < 0) return null;

            i = json.IndexOf(':', i + pattern.Length);
            if (i < 0) return null;
            i++;

            while (i < json.Length && char.IsWhiteSpace(json[i])) i++;
            if (i >= json.Length) return null;

            if (json[i] != '"') return null;
            i++;

            var start = i;
            while (i < json.Length)
            {
                var c = json[i];
                if (c == '"')
                {
                    return json.Substring(start, i - start);
                }

                if (c == '\\')
                {
                    // Not a full unescape. If payload contains escapes we return null to avoid silent corruption.
                    return null;
                }

                i++;
            }

            return null;
        }

        public static bool TryGetUInt64(string json, string key, out ulong value)
        {
            value = 0;
            if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(key)) return false;

            var pattern = "\"" + key + "\"";
            var i = json.IndexOf(pattern, StringComparison.Ordinal);
            if (i < 0) return false;

            i = json.IndexOf(':', i + pattern.Length);
            if (i < 0) return false;
            i++;

            while (i < json.Length && char.IsWhiteSpace(json[i])) i++;
            if (i >= json.Length) return false;

            var start = i;
            while (i < json.Length)
            {
                var c = json[i];
                if ((c >= '0' && c <= '9')) { i++; continue; }
                break;
            }

            if (i <= start) return false;
            var s = json.Substring(start, i - start);
            return ulong.TryParse(s, NumberStyles.None, CultureInfo.InvariantCulture, out value);
        }

        public static string TryGetObjectJson(string json, string key)
        {
            if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(key)) return null;

            var pattern = "\"" + key + "\"";
            var i = json.IndexOf(pattern, StringComparison.Ordinal);
            if (i < 0) return null;

            i = json.IndexOf(':', i + pattern.Length);
            if (i < 0) return null;
            i++;

            while (i < json.Length && char.IsWhiteSpace(json[i])) i++;
            if (i >= json.Length || json[i] != '{') return null;

            var start = i;
            var depth = 0;
            while (i < json.Length)
            {
                var c = json[i];
                if (c == '"')
                {
                    // Skip strings (minimal)
                    i++;
                    while (i < json.Length)
                    {
                        if (json[i] == '\\') { i += 2; continue; }
                        if (json[i] == '"') { i++; break; }
                        i++;
                    }
                    continue;
                }

                if (c == '{') depth++;
                if (c == '}')
                {
                    depth--;
                    if (depth == 0)
                    {
                        var end = i;
                        return json.Substring(start, end - start + 1);
                    }
                }
                i++;
            }

            return null;
        }
    }
}
