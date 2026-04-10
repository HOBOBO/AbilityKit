using System;
using System.Collections.Generic;
using System.Text;
using AbilityKit.Core.Common.Log;
using AbilityKit.Ability.Triggering;
using AbilityKit.Ability.Triggering.Definitions;
using AbilityKit.Ability.Triggering.Runtime;

namespace AbilityKit.Ability.Impl.Triggering
{
    public sealed class DebugLogAction : ITriggerAction
    {
        private readonly string _message;
        private readonly bool _dumpArgs;

        public DebugLogAction(string message, bool dumpArgs)
        {
            _message = message;
            _dumpArgs = dumpArgs;
        }

        public static DebugLogAction FromDef(ActionDef def)
        {
            if (def == null) throw new ArgumentNullException(nameof(def));
            var args = def.Args;
            if (args == null) return new DebugLogAction(string.Empty, dumpArgs: false);

            args.TryGetValue("message", out var msgObj);
            args.TryGetValue("dump_args", out var dumpObj);
            var dumpArgs = dumpObj is bool b && b;
            return new DebugLogAction(msgObj as string, dumpArgs);
        }

        public void Execute(TriggerContext context)
        {
            var eventId = context.Event.Id;
            var payloadType = context.Event.Payload?.GetType().Name ?? "null";
            var msgTemplate = string.IsNullOrEmpty(_message)
                ? $"[Trigger] event={eventId}, payloadType={payloadType}"
                : _message;

            var msg = FormatMessage(msgTemplate, context);
            if (_dumpArgs)
            {
                var dump = DumpArgs(context?.Event.Args);
                msg = string.IsNullOrEmpty(dump) ? msg : (msg + "\n" + dump);
            }

            var sink = context?.Services?.GetService(typeof(ILogSink)) as ILogSink;
            if (sink != null)
            {
                sink.Info(msg);
                return;
            }

            Log.Info(msg);
        }

        private static string FormatMessage(string template, TriggerContext context)
        {
            if (string.IsNullOrEmpty(template)) return template;
            if (context == null) return template;

            var sb = new StringBuilder(template.Length + 32);
            for (int i = 0; i < template.Length; i++)
            {
                var ch = template[i];
                if (ch != '{')
                {
                    sb.Append(ch);
                    continue;
                }

                var end = template.IndexOf('}', i + 1);
                if (end < 0)
                {
                    sb.Append(ch);
                    continue;
                }

                var key = template.Substring(i + 1, end - i - 1);
                if (string.IsNullOrEmpty(key))
                {
                    sb.Append("{}");
                    i = end;
                    continue;
                }

                if (TryResolveValue(context, key, out var value))
                {
                    sb.Append(value ?? "null");
                }
                else
                {
                    sb.Append('{').Append(key).Append('}');
                }

                i = end;
            }

            return sb.ToString();
        }

        private static bool TryResolveValue(TriggerContext context, string key, out string value)
        {
            value = null;
            if (context == null || string.IsNullOrEmpty(key)) return false;

            if (string.Equals(key, "event.id", StringComparison.Ordinal))
            {
                value = context.Event.Id;
                return true;
            }

            if (string.Equals(key, "event.payloadType", StringComparison.Ordinal))
            {
                value = context.Event.Payload?.GetType().Name ?? "null";
                return true;
            }

            if (string.Equals(key, "source", StringComparison.Ordinal) || string.Equals(key, "effect.source", StringComparison.Ordinal))
            {
                value = context.Source?.ToString() ?? "null";
                return true;
            }

            if (string.Equals(key, "target", StringComparison.Ordinal) || string.Equals(key, "effect.target", StringComparison.Ordinal))
            {
                value = context.Target?.ToString() ?? "null";
                return true;
            }

            var args = context.Event.Args;
            if (args != null && args.TryGetValue(key, out var obj))
            {
                value = obj?.ToString() ?? "null";
                return true;
            }

            if (context.TryGetVar(VarScope.Local, key, out obj) || context.TryGetVar(VarScope.Global, key, out obj))
            {
                value = obj?.ToString() ?? "null";
                return true;
            }

            return false;
        }

        private static string DumpArgs(IReadOnlyDictionary<string, object> args)
        {
            if (args == null || args.Count == 0) return string.Empty;

            var sb = new StringBuilder(256);
            sb.Append("[TriggerArgs]");
            foreach (var kv in args)
            {
                if (kv.Key == null) continue;
                sb.Append("\n").Append(kv.Key).Append("=").Append(kv.Value ?? "null");
            }
            return sb.ToString();
        }
    }
}
