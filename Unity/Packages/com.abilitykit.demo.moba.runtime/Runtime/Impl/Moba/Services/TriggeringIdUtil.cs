namespace AbilityKit.Ability.Share.Impl.Moba.Services
{
    public static class TriggeringIdUtil
    {
        private static readonly object _lock = new object();
        private static readonly System.Collections.Generic.Dictionary<string, int> _eventEidCache = new System.Collections.Generic.Dictionary<string, int>(System.StringComparer.Ordinal);
        private static readonly System.Collections.Generic.Dictionary<string, int> _actionAidCache = new System.Collections.Generic.Dictionary<string, int>(System.StringComparer.Ordinal);

        public static int GetEventEid(string eventId)
        {
            var key = eventId ?? string.Empty;
            lock (_lock)
            {
                if (_eventEidCache.TryGetValue(key, out var cached)) return cached;
                var eid = AbilityKit.Triggering.Eventing.StableStringId.Get("event:" + eventId);
                _eventEidCache[key] = eid;
                return eid;
            }
        }

        public static int GetActionAid(string actionId)
        {
            var key = actionId ?? string.Empty;
            lock (_lock)
            {
                if (_actionAidCache.TryGetValue(key, out var cached)) return cached;
                var aid = AbilityKit.Triggering.Eventing.StableStringId.Get("action:" + actionId);
                _actionAidCache[key] = aid;
                return aid;
            }
        }
    }
}
