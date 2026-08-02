using AbilityKit.Core.Logging;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Game.Battle.Entity;

namespace AbilityKit.Game.Flow
{
    /// <summary>
    /// Resolves view-model parameters for <see cref="BattleEntityKind.Summon"/> entities.
    /// Falls back to placeholder when the summon config is unavailable.
    /// </summary>
    internal sealed class BattleViewSummonConfigResolver
    {
        public int ResolveModelId(MobaConfigDatabase configs, BattleEntityMetaComponent meta)
        {
            if (meta == null) return 0;
            if (meta.Kind != BattleEntityKind.Summon) return 0;

            int modelId = ResolveFromSummonConfig(configs, meta.EntityCode);
            return modelId > 0
                ? modelId
                : BattleViewFallbackPolicy.DevelopmentOnly(
                    BattleViewPlaceholderIds.CharacterModel,
                    "summon:" + meta.EntityCode);
        }

        private static int ResolveFromSummonConfig(MobaConfigDatabase configs, int entityCode)
        {
            if (configs == null) return 0;
            try
            {
                if (configs.TryGetSummon(entityCode, out var summon) && summon != null)
                {
                    return summon.ModelId;
                }
            }
            catch (System.Exception ex)
            {
                Log.Exception(ex);
            }
            return 0;
        }
    }

    /// <summary>
    /// Resolves view-model parameters for <see cref="BattleEntityKind.Turret"/> entities.
    /// Falls back to placeholder (TurretModel) when the turret config is unavailable.
    /// TODO: Wire up Turret config lookup once <c>configs.TryGetTurret()</c> is available.
    /// </summary>
    internal sealed class BattleViewTurretConfigResolver
    {
        public int ResolveModelId(MobaConfigDatabase configs, BattleEntityMetaComponent meta)
        {
            if (meta == null) return 0;
            if (meta.Kind != BattleEntityKind.Turret) return 0;

            // TODO: Replace with configs.TryGetTurret(meta.EntityCode, out var turret) once available.
            int modelId = ResolveFromConfig(configs, meta.EntityCode);
            return modelId > 0
                ? modelId
                : BattleViewFallbackPolicy.DevelopmentOnly(
                    BattleViewPlaceholderIds.TurretModel,
                    "turret:" + meta.EntityCode);
        }

        private static int ResolveFromConfig(MobaConfigDatabase configs, int entityCode)
        {
            // Placeholder: configs.TryGetTurret(entityCode, out var turret) → turret.ModelId
            return 0;
        }
    }

    /// <summary>
    /// Resolves view-model parameters for <see cref="BattleEntityKind.Monster"/> entities.
    /// Falls back to placeholder (MonsterModel) when the monster config is unavailable.
    /// TODO: Wire up Monster config lookup once <c>configs.TryGetMonster()</c> is available.
    /// </summary>
    internal sealed class BattleViewMonsterConfigResolver
    {
        public int ResolveModelId(MobaConfigDatabase configs, BattleEntityMetaComponent meta)
        {
            if (meta == null) return 0;
            if (meta.Kind != BattleEntityKind.Monster) return 0;

            int modelId = ResolveFromConfig(configs, meta.EntityCode);
            return modelId > 0
                ? modelId
                : BattleViewFallbackPolicy.DevelopmentOnly(
                    BattleViewPlaceholderIds.MonsterModel,
                    "monster:" + meta.EntityCode);
        }

        private static int ResolveFromConfig(MobaConfigDatabase configs, int entityCode)
        {
            // Placeholder: configs.TryGetMonster(entityCode, out var monster) → monster.ModelId
            return 0;
        }
    }

    /// <summary>
    /// Resolves view-model parameters for <see cref="BattleEntityKind.Building"/> entities.
    /// Falls back to placeholder (BuildingModel) when the building config is unavailable.
    /// TODO: Wire up Building config lookup once <c>configs.TryGetBuilding()</c> is available.
    /// </summary>
    internal sealed class BattleViewBuildingConfigResolver
    {
        public int ResolveModelId(MobaConfigDatabase configs, BattleEntityMetaComponent meta)
        {
            if (meta == null) return 0;
            if (meta.Kind != BattleEntityKind.Building) return 0;

            int modelId = ResolveFromConfig(configs, meta.EntityCode);
            return modelId > 0
                ? modelId
                : BattleViewFallbackPolicy.DevelopmentOnly(
                    BattleViewPlaceholderIds.BuildingModel,
                    "building:" + meta.EntityCode);
        }

        private static int ResolveFromConfig(MobaConfigDatabase configs, int entityCode)
        {
            // Placeholder: configs.TryGetBuilding(entityCode, out var building) → building.ModelId
            return 0;
        }
    }
}
