using System;
using System.Text;
using AbilityKit.Core.Logging;

namespace AbilityKit.Network.Runtime
{
    /// <summary>
    /// 解码已有踢出推送格式，保留精简解析器的兼容行为，不决定连接生命周期。
    /// </summary>
    internal static class KickPushDecoder
    {
        internal static void Decode(ArraySegment<byte> payload, out string token, out string reason)
        {
            token = string.Empty;
            reason = string.Empty;

            try
            {
                if (payload.Array != null && payload.Count > 0)
                {
                    var json = Encoding.UTF8.GetString(payload.Array, payload.Offset, payload.Count);
                    token = TryGetJsonStringValue(json, "sessionToken") ?? string.Empty;
                    reason = TryGetJsonStringValue(json, "reason") ?? string.Empty;
                }
            }
            catch (Exception ex)
            {
                Log.Exception(ex, "[ConnectionManager] Kick push json decode failed");
                if (payload.Array != null && payload.Count > 0)
                {
                    reason = Encoding.UTF8.GetString(payload.Array, payload.Offset, payload.Count);
                }
            }
        }

        private static string TryGetJsonStringValue(string json, string key)
        {
            if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(key)) return null;

            /*
             * 面向 {"key":"value"} 形式载荷的精简 JSON 提取器，仅满足踢出推送载荷需求，不是通用 JSON 解析器。
             */
            var pattern = "\"" + key + "\"";
            var i = json.IndexOf(pattern, StringComparison.Ordinal);
            if (i < 0) return null;

            i = json.IndexOf(':', i + pattern.Length);
            if (i < 0) return null;
            i++;

            while (i < json.Length && char.IsWhiteSpace(json[i])) i++;
            if (i >= json.Length || json[i] != '"') return null;
            i++;

            var start = i;
            var sb = (StringBuilder)null;

            while (i < json.Length)
            {
                var c = json[i];
                if (c == '"')
                {
                    if (sb == null) return json.Substring(start, i - start);
                    return sb.ToString();
                }

                if (c == '\\')
                {
                    if (i + 1 >= json.Length) return null;
                    sb ??= new StringBuilder(json.Substring(start, i - start));
                    var esc = json[i + 1];
                    switch (esc)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        default:
                            /*
                             * 未知转义保持原样。
                             */
                            sb.Append(esc);
                            break;
                    }
                    i += 2;
                    start = i;
                    continue;
                }

                i++;
            }

            return null;
        }

    }
}
