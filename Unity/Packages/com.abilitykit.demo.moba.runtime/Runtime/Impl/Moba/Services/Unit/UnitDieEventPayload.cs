namespace AbilityKit.Ability.Share.Impl.Moba.Services
{
    using AbilityKit.Ability.Impl.Moba;
    public readonly struct UnitDieEventPayload
    {
        public readonly int ActorId;
        public readonly int KillerActorId;

        public readonly int DamageType;
        public readonly int ReasonKind;
        public readonly int ReasonParam;

        public readonly float DamageValue;

        public UnitDieEventPayload(int actorId, int killerActorId, int damageType, int reasonKind, int reasonParam, float damageValue)
        {
            ActorId = actorId;
            KillerActorId = killerActorId;
            DamageType = damageType;
            ReasonKind = reasonKind;
            ReasonParam = reasonParam;
            DamageValue = damageValue;
        }
    }
}
