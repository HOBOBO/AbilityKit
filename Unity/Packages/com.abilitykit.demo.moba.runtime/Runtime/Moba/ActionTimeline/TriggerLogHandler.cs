using System;
using AbilityKit.ActionSchema;

namespace AbilityKit.Demo.Moba.ActionTimeline
{
    public sealed class TriggerLogHandler : IMobaClipHandler
    {
        public const string ClipType = "AbilityKit.ActionEditorImpl.TriggerLog";

        public bool TryHandle(float time, ClipDto clip, IMobaTimelineEventSink sink)
        {
            if (clip == null) return false;
            if (sink == null) return false;

            if (!IsMatch(clip.type)) return false;

            string msg = null;
            if (clip.args != null)
            {
                clip.args.TryGetValue("log", out msg);
            }

            sink.OnTriggerLog(time, msg ?? string.Empty);
            return true;
        }

        private static bool IsMatch(string type)
        {
            if (string.IsNullOrEmpty(type)) return false;
            return type == ClipType || type.EndsWith(".TriggerLog", StringComparison.Ordinal);
        }
    }
}
