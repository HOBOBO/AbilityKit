namespace AbilityKit.Core.Common.Projectile
{
    public readonly struct ProjectileId
    {
        public readonly int Value;

        public ProjectileId(int value)
        {
            Value = value;
        }

        public override string ToString() => Value.ToString();
    }
}
