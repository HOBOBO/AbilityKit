namespace AbilityKit.Ability.Share.Common.Projectile
{
    public readonly struct AreaId
    {
        public readonly int Value;

        public AreaId(int value)
        {
            Value = value;
        }

        public override string ToString() => Value.ToString();
    }
}
