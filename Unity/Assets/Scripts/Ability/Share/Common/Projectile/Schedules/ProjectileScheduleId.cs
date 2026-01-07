namespace AbilityKit.Ability.Share.Common.Projectile
{
    public readonly struct ProjectileScheduleId
    {
        public readonly int Value;

        public ProjectileScheduleId(int value)
        {
            Value = value;
        }

        public override string ToString() => Value.ToString();
    }
}
