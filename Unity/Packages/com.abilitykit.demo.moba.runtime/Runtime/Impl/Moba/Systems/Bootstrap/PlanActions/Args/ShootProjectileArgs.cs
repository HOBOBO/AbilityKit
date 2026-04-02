namespace AbilityKit.Ability.Impl.Moba.Systems
{
    /// <summary>
    /// shoot_projectile Action 的强类型参数
    /// </summary>
    public readonly struct ShootProjectileArgs
    {
        /// <summary>
        /// 发射器ID
        /// </summary>
        public readonly int LauncherId;

        /// <summary>
        /// 弹体ID
        /// </summary>
        public readonly int ProjectileId;

        public ShootProjectileArgs(int launcherId, int projectileId)
        {
            LauncherId = launcherId;
            ProjectileId = projectileId;
        }

        public static ShootProjectileArgs Default => new ShootProjectileArgs(0, 0);
    }
}
