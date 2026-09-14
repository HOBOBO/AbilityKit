namespace AbilityKit.Demo.Moba.Services.Combat.Transactions
{
    public sealed class MobaDamageTransaction : MobaCombatTransactionBase, IMobaActorContextProvider, IMobaOriginContextProvider
    {
        public MobaDamageTransaction(AttackInfo attack)
            : base(ResolveRootContextId(attack))
        {
            Attack = attack;
        }

        public AttackInfo Attack { get; }

        public void Redirect(int targetActorId)
        {
            if (Attack != null) Attack.TargetActorId = targetActorId;
        }

        public void SetBaseDamage(float value)
        {
            if (Attack != null) Attack.BaseDamage.FixedBaseValue = MobaResourceFixedConvert.ToFixed(value);
        }

        public bool TryGetSourceActorId(out int actorId)
        {
            actorId = Attack?.AttackerActorId ?? 0;
            return actorId > 0;
        }

        public bool TryGetTargetActorId(out int actorId)
        {
            actorId = Attack?.TargetActorId ?? 0;
            return actorId > 0;
        }

        public bool TryGetOrigin(out MobaGameplayOrigin origin)
        {
            if (Attack != null && Attack.TryGetOrigin(out origin)) return origin.IsValid;
            origin = default;
            return false;
        }

        private static long ResolveRootContextId(AttackInfo attack)
        {
            return attack != null && attack.TryGetOrigin(out var origin)
                ? origin.EffectiveRootContextId
                : 0L;
        }
    }
}
