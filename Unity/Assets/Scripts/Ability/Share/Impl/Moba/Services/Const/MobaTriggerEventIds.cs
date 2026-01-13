namespace AbilityKit.Ability.Share.Impl.Moba.Services
{
    public static class MobaTriggerEventIds
    {
        public const string EffectExecute = "effect.execute";
        public const string EffectApply = "effect.apply";

        public static string EffectExecuteById(int effectId) => $"effect.execute.{effectId}";
        public static string EffectApplyById(int effectId) => $"effect.apply.{effectId}";
    }
}
