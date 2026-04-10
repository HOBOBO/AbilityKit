namespace AbilityKit.Core.Common.Projectile
{
    public sealed class ExitOnHitPolicy : IProjectileHitPolicy
    {
        public static readonly ExitOnHitPolicy Instance = new ExitOnHitPolicy();

        private ExitOnHitPolicy() { }

        public bool ShouldExitOnHit(in ProjectileHitEvent hit, ref int hitsRemaining)
        {
            hitsRemaining = 0;
            return true;
        }
    }
}
