using System;
using System.Collections.Generic;
using AbilityKit.Triggering.Eventing;

namespace AbilityKit.Triggering.Runtime.Plan
{
    public static class RpnIntExprParser
    {
        public const string LangRpnV1 = "rpn_v1";

        public static RpnIntNode[] Parse(
            string exprText,
            Func<string, int> payloadFieldIdResolver = null,
            Func<string, int> blackboardDomainIdResolver = null,
            Func<string, int> blackboardKeyIdResolver = null)
        {
            if (string.IsNullOrWhiteSpace(exprText)) return Array.Empty<RpnIntNode>();

            var tokens = exprText.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length == 0) return Array.Empty<RpnIntNode>();

            var nodes = new List<RpnIntNode>(tokens.Length);
            for (int i = 0; i < tokens.Length; i++)
            {
                var t = tokens[i];
                if (t == null) continue;

                if (TryParseOp(t, out var op))
                {
                    nodes.Add(op);
                    continue;
                }

                if (TryParseConstInt(t, out var c))
                {
                    nodes.Add(RpnIntNode.Push(IntValueRef.Const(c)));
                    continue;
                }

                if (TryParsePayload(t, out var payloadName))
                {
                    var id = payloadFieldIdResolver != null ? payloadFieldIdResolver(payloadName) : StableStringId.Get("payload:" + payloadName);
                    if (id == 0) throw new InvalidOperationException("Payload field id resolve failed: " + payloadName);
                    nodes.Add(RpnIntNode.Push(IntValueRef.PayloadField(id)));
                    continue;
                }

                if (TryParseBlackboard(t, out var domain, out var key))
                {
                    var boardId = blackboardDomainIdResolver != null ? blackboardDomainIdResolver(domain) : StableStringId.Get("bb:" + domain);
                    var keyId = blackboardKeyIdResolver != null ? blackboardKeyIdResolver(domain + ":" + key) : StableStringId.Get("bb:" + domain + ":" + key);
                    if (boardId == 0 || keyId == 0) throw new InvalidOperationException("Blackboard id resolve failed: " + domain + ":" + key);
                    nodes.Add(RpnIntNode.Push(IntValueRef.Blackboard(boardId, keyId)));
                    continue;
                }

                throw new NotSupportedException("Unsupported RPN token: " + t);
            }

            return nodes.ToArray();
        }

        private static bool TryParseOp(string token, out RpnIntNode node)
        {
            switch (token)
            {
                case "+":
                    node = RpnIntNode.Add();
                    return true;
                case "-":
                    node = RpnIntNode.Sub();
                    return true;
                case "*":
                    node = RpnIntNode.Mul();
                    return true;
                case "/":
                    node = RpnIntNode.Div();
                    return true;
                default:
                    node = default;
                    return false;
            }
        }

        private static bool TryParseConstInt(string token, out int value)
        {
            return int.TryParse(token, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out value);
        }

        private static bool TryParsePayload(string token, out string name)
        {
            const string p = "payload:";
            if (token.StartsWith(p, StringComparison.Ordinal))
            {
                name = token.Substring(p.Length);
                return !string.IsNullOrEmpty(name);
            }

            name = null;
            return false;
        }

        private static bool TryParseBlackboard(string token, out string domain, out string key)
        {
            domain = null;
            key = null;

            const string p = "bb:";
            if (!token.StartsWith(p, StringComparison.Ordinal)) return false;

            var rest = token.Substring(p.Length);
            var idx = rest.IndexOf(':');
            if (idx <= 0 || idx >= rest.Length - 1) return false;

            domain = rest.Substring(0, idx);
            key = rest.Substring(idx + 1);
            return !string.IsNullOrEmpty(domain) && !string.IsNullOrEmpty(key);
        }
    }
}
