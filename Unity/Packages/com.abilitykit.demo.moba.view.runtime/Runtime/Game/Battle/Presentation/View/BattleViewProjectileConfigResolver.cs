using AbilityKit.Core.Logging;
using AbilityKit.Demo.Moba.Config.BattleDemo.MO;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Game.Battle.Entity;

namespace AbilityKit.Game.Flow
{
    internal sealed class BattleViewProjectileConfigResolver
    {
        public int ResolveAttachedVfxId(MobaConfigDatabase configs, BattleEntityMetaComponent meta)
        {
            if (meta == null) return 0;
            if (meta.Kind != BattleEntityKind.Projectile) return 0;

            var projectile = TryGet(configs, meta.EntityCode);
            if (projectile != null && projectile.VfxId > 0) return projectile.VfxId;
            return BattleViewFallbackPolicy.DevelopmentOnly(
                BattleViewPlaceholderIds.ProjectileVfx,
                "projectile.vfx:" + meta.EntityCode);
        }

        public ProjectileMO TryGet(MobaConfigDatabase configs, int templateId)
        {
            if (templateId <= 0) return null;
            if (configs == null) return null;

            try
            {
                return configs.GetProjectile(templateId);
            }
            catch (System.Exception ex)
            {
                Log.Exception(ex);
                return null;
            }
        }
    }
}
