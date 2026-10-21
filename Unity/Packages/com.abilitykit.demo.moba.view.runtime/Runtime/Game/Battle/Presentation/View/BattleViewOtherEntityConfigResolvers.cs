using System;
using System.Collections.Generic;
using AbilityKit.Core.Logging;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Game.Battle.Entity;

namespace AbilityKit.Game.Flow
{
    /// <summary>
    /// Minion projections carry the resolved model id in EntityCode. This keeps
    /// presentation independent from the minion character/attribute tables.
    /// </summary>
    internal sealed class BattleViewMinionConfigResolver
    {
        public int ResolveModelId(MobaConfigDatabase configs, BattleEntityMetaComponent meta)
        {
            if (meta == null || meta.Kind != BattleEntityKind.Minion) return 0;
            return meta.EntityCode > 0
                ? meta.EntityCode
                : BattleViewFallbackPolicy.DevelopmentOnly(
                    BattleViewPlaceholderIds.CharacterModel,
                    "minion:" + meta.EntityCode);
        }
    }

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

    public sealed class BattleViewModelResolverRegistry
    {
        private readonly Dictionary<BattleEntityKind, Func<MobaConfigDatabase, int, int>> _resolvers =
            new Dictionary<BattleEntityKind, Func<MobaConfigDatabase, int, int>>();

        public BattleViewModelResolverRegistry Register(
            BattleEntityKind kind, Func<MobaConfigDatabase, int, int> resolveModelId)
        {
            if ((kind != BattleEntityKind.Turret &&
                 kind != BattleEntityKind.Monster &&
                 kind != BattleEntityKind.Building) ||
                resolveModelId == null || _resolvers.ContainsKey(kind))
                throw new ArgumentException("Invalid or duplicate battle view model resolver: " + kind);
            _resolvers.Add(kind, resolveModelId);
            return this;
        }

        internal int ResolveModelId(MobaConfigDatabase configs, BattleEntityMetaComponent meta)
        {
            if (meta == null) return 0;
            var modelId = _resolvers.TryGetValue(meta.Kind, out var resolve)
                ? resolve(configs, meta.EntityCode)
                : 0;
            return modelId > 0
                ? modelId
                : BattleViewFallbackPolicy.DevelopmentOnly(
                    PlaceholderFor(meta.Kind), meta.Kind + ":" + meta.EntityCode);
        }

        private static int PlaceholderFor(BattleEntityKind kind) => kind switch
        {
            BattleEntityKind.Turret => BattleViewPlaceholderIds.TurretModel,
            BattleEntityKind.Monster => BattleViewPlaceholderIds.MonsterModel,
            BattleEntityKind.Building => BattleViewPlaceholderIds.BuildingModel,
            _ => 0,
        };
    }
}
