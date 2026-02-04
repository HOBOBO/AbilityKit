using System;
using System.Collections.Generic;
using AbilityKit.Triggering.Eventing;

namespace AbilityKit.Triggering.Runtime.Plan
{
    public static class RpnNumericExprParser
    {
        public const string LangRpnV1 = "rpn_v1";

        public static RpnNumericNode[] Parse(
            string exprText,
            Func<string, int> payloadFieldIdResolver = null,
            Func<string, int> blackboardDomainIdResolver = null,
            Func<string, int> blackboardKeyIdResolver = null)
        {
            if (string.IsNullOrWhiteSpace(exprText)) return Array.Empty<RpnNumericNode>();

            var tokens = exprText.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length == 0) return Array.Empty<RpnNumericNode>();

            var nodes = new List<RpnNumericNode>(tokens.Length);
            for (int i = 0; i < tokens.Length; i++)
            {
                var t = tokens[i];
                if (t == null) continue;

                if (TryParseOp(t, out var op))
                {
                    nodes.Add(op);
                    continue;
                }

                if (TryParseConstNumeric(t, out var c))
                {
                    nodes.Add(RpnNumericNode.Push(NumericValueRef.Const(c)));
                    continue;
                }

                if (TryParsePayload(t, out var payloadName))
                {
                    var id = payloadFieldIdResolver != null ? payloadFieldIdResolver(payloadName) : StableStringId.Get("payload:" + payloadName);
                    if (id == 0) throw new InvalidOperationException("Payload field id resolve failed: " + payloadName);
                    nodes.Add(RpnNumericNode.Push(NumericValueRef.PayloadField(id)));
                    continue;
                }

                if (TryParseBlackboard(t, out var domain, out var key))
                {
                    var boardId = blackboardDomainIdResolver != null ? blackboardDomainIdResolver(domain) : StableStringId.Get("bb:" + domain);
                    var keyId = blackboardKeyIdResolver != null ? blackboardKeyIdResolver(domain + ":" + key) : StableStringId.Get("bb:" + domain + ":" + key);
                    if (boardId == 0 || keyId == 0) throw new InvalidOperationException("Blackboard id resolve failed: " + domain + ":" + key);
                    nodes.Add(RpnNumericNode.Push(NumericValueRef.Blackboard(boardId, keyId)));
                    continue;
                }

                throw new NotSupportedException("Unsupported RPN token: " + t);
            }

            return nodes.ToArray();
        }

        private static bool TryParseOp(string token, out RpnNumericNode node)
        {
            switch (token)
            {
                case "+":
                    node = RpnNumericNode.Add();
                    return true;
                case "-":
                    node = RpnNumericNode.Sub();
                    return true;
                case "*":
                    node = RpnNumericNode.Mul();
                    return true;
                case "/":
                    node = RpnNumericNode.Div();
                    return true;
                default:
                    node = default;
                    return false;
            }
        }

        private static bool TryParseConstNumeric(string token, out double value)
        {
            return double.TryParse(token, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out value);
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
