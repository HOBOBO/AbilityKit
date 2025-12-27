using System;
using AbilityKit.Triggering;
using AbilityKit.Triggering.Definitions;
using AbilityKit.Triggering.Runtime;
using UnityEngine;

namespace AbilityKit.Ability.Impl.Triggering
{
    public sealed class DebugLogAction : ITriggerAction
    {
        private readonly string _message;

        public DebugLogAction(string message)
        {
            _message = message;
        }

        public static DebugLogAction FromDef(ActionDef def)
        {
            if (def == null) throw new ArgumentNullException(nameof(def));
            var args = def.Args;
            if (args == null) return new DebugLogAction(string.Empty);

            args.TryGetValue("message", out var msgObj);
            return new DebugLogAction(msgObj as string);
        }

        public void Execute(TriggerContext context)
        {
            var eventId = context.Event.Id;
            var payloadType = context.Event.Payload?.GetType().Name ?? "null";
            var msg = string.IsNullOrEmpty(_message)
                ? $"[Trigger] event={eventId}, payloadType={payloadType}"
                : _message;

            Debug.Log(msg);
        }
    }
}
