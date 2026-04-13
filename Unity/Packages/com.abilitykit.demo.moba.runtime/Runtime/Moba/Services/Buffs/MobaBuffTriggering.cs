namespace AbilityKit.Demo.Moba.Services
{
    public static class MobaBuffTriggering
    {
        public static class Events
        {
            public const string ApplyOrRefresh = "buff.apply";
            public const string Remove = "buff.remove";
            public const string Interval = "buff.interval";

            public static string WithEffect(string baseEventId, int effectId)
            {
                return string.IsNullOrEmpty(baseEventId) ? null : $"{baseEventId}.{effectId}";
            }
        }

        public static class Args
        {
            public const string BuffId = "buff.id";
            public const string EffectId = "buff.effectId";
            public const string StackCount = "buff.stackCount";
            public const string DurationSeconds = "buff.durationSeconds";

            public const string Stage = "buff.stage";

            public const string RemoveReason = "buff.removeReason";
        }
    }
}
