using System;
using AbilityKit.Ability.Share.Common.Log;
using AbilityKit.Ability.Triggering;
using AbilityKit.Ability.Triggering.Definitions;
using AbilityKit.Ability.Triggering.Runtime;

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

            var sink = context?.Services?.GetService(typeof(ILogSink)) as ILogSink;
            if (sink != null)
            {
                sink.Info(msg);
                return;
            }

            Log.Info(msg);
        }
    }
}
