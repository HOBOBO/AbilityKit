namespace AbilityKit.Ability.Share.Effect
{
    public static class EffectTriggering
    {
        public static class Events
        {
            public const string Apply = "effect.apply";
            public const string Tick = "effect.tick";
            public const string Remove = "effect.remove";
        }

        public static class Args
        {
            public const string Source = "source";
            public const string Target = "target";

            public const string Spec = "effect.spec";
            public const string Instance = "effect.instance";
            public const string InstanceId = "effect.instanceId";
            public const string StackCount = "effect.stackCount";
            public const string ElapsedSeconds = "effect.elapsedSeconds";
            public const string RemainingSeconds = "effect.remainingSeconds";
        }
    }
}
