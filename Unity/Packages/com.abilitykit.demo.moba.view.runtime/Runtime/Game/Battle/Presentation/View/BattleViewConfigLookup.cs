using AbilityKit.Demo.Moba.Config.BattleDemo.MO;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Game.Battle.Entity;

namespace AbilityKit.Game.Flow
{
    internal sealed class BattleViewConfigLookup
    {
        private readonly BattleViewCharacterConfigResolver _characters;
        private readonly BattleViewMinionConfigResolver _minions;
        private readonly BattleViewProjectileConfigResolver _projectiles;
        private readonly BattleViewSummonConfigResolver _summons;
        private readonly BattleViewModelResolverRegistry _otherModels;
        private readonly BattleViewAoeConfigResolver _aoes;

        public BattleViewConfigLookup(
            BattleViewCharacterConfigResolver characters = null,
            BattleViewProjectileConfigResolver projectiles = null,
            BattleViewSummonConfigResolver summons = null,
            BattleViewModelResolverRegistry otherModels = null,
            BattleViewAoeConfigResolver aoes = null)
        {
            _characters = characters ?? new BattleViewCharacterConfigResolver();
            _minions = new BattleViewMinionConfigResolver();
            _projectiles = projectiles ?? new BattleViewProjectileConfigResolver();
            _summons = summons ?? new BattleViewSummonConfigResolver();
            _otherModels = otherModels ?? new BattleViewModelResolverRegistry();
            _aoes = aoes ?? new BattleViewAoeConfigResolver();
        }

        /// <summary>
        /// Resolves the modelId for any entity kind that uses a shared shell GameObject.
        /// Dispatches to the appropriate kind-specific resolver.
        /// </summary>
        public int ResolveModelId(MobaConfigDatabase configs, BattleEntityMetaComponent meta)
        {
            if (meta == null) return 0;

            switch (meta.Kind)
            {
                case BattleEntityKind.Character:  return _characters.ResolveModelId(configs, meta);
                case BattleEntityKind.Minion:      return _minions.ResolveModelId(configs, meta);
                case BattleEntityKind.Summon:     return _summons.ResolveModelId(configs, meta);
                case BattleEntityKind.Turret:
                case BattleEntityKind.Monster:
                case BattleEntityKind.Building:   return _otherModels.ResolveModelId(configs, meta);
                case BattleEntityKind.Clone:       return _summons.ResolveModelId(configs, meta); // Clone shares Summon resolution
                default:                           return 0;
            }
        }

        /// <summary>
        /// Checks if the given kind uses the main shell binding path (Actor shell + AttachedVfx).
        /// </summary>
        public static bool UsesShellBinding(BattleEntityKind kind)
        {
            switch (kind)
            {
                case BattleEntityKind.Character:
                case BattleEntityKind.Minion:
                case BattleEntityKind.Summon:
                case BattleEntityKind.Clone:
                case BattleEntityKind.Turret:
                case BattleEntityKind.Monster:
                case BattleEntityKind.Building:
                    return true;
                case BattleEntityKind.Projectile:
                case BattleEntityKind.AreaEffect:
                case BattleEntityKind.Unknown:
                default:
                    return false;
            }
        }

        public int ResolveProjectileVfxId(MobaConfigDatabase configs, BattleEntityMetaComponent meta)
        {
            return _projectiles.ResolveAttachedVfxId(configs, meta);
        }

        public ProjectileMO TryGetProjectile(MobaConfigDatabase configs, int templateId)
        {
            return _projectiles.TryGet(configs, templateId);
        }

        public AoeMO TryGetAoe(MobaConfigDatabase configs, int templateId)
        {
            return _aoes.TryGet(configs, templateId);
        }
    }
}
