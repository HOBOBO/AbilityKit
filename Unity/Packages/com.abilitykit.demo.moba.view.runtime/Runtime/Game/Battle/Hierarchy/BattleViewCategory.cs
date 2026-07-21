namespace AbilityKit.Game.Battle.Hierarchy
{
    /// <summary>
    /// Categories for organizing battle view GameObjects in the Unity Hierarchy.
    /// Each category maps to a dedicated sub-root under <see cref="BattleViewHierarchyRoot"/>.
    ///
    /// Hierarchy layout produced by the manager:
    /// <code>
    /// [Battle] (BattleViewHierarchyRoot)
    ///   ├── _Pool/          // inactive, pooled instances awaiting reuse
    ///   │   ├── _Shell         // BattleViewShellPool buckets
    ///   │   ├── _Vfx           // BattleVfxGameObjectPool buckets
    ///   │   ├── _Area          // BattleAreaVfxPool buckets (Model / Range / Vfx kinds)
    ///   │   ├── _Projectile    // BattleProjectileShellPool buckets
    ///   │   ├── _FloatingText  // pooled world-space combat text
    ///   │   └── _Hud           // other HUD-related pooled UI elements
    ///   ├── _Active/        // currently rented, live GameObjects
    ///   │   ├── _Character     // hero / player shells
    ///   │   ├── _Projectile    // active projectiles
    ///   │   ├── _Summon        // summoned creatures
    ///   │   ├── _Turret        // towers
    ///   │   ├── _Monster       // jungle creeps
    ///   │   ├── _Building      // buildings
    ///   │   ├── _Area          // live area effects
    ///   │   ├── _Vfx           // live VFX entities
    ///   │   └── _FloatingText  // live world-space combat text
    ///   └── _Debug/         // editor-only visualization helpers
    /// </code>
    /// </summary>
    public enum BattleViewCategory
    {
        /// <summary>Unknown / unclassified. Used as a safe fallback.</summary>
        Unknown = 0,

        // ---- Pool (inactive) categories ----

        /// <summary>Pooled entity shells (BattleViewShellPool buckets).</summary>
        PoolShell = 100,

        /// <summary>Pooled VFX GameObjects (BattleVfxGameObjectPool buckets).</summary>
        PoolVfx = 101,

        /// <summary>Pooled area-effect GameObjects (BattleAreaVfxPool buckets).</summary>
        PoolArea = 102,

        /// <summary>Pooled projectile shells (BattleProjectileShellPool buckets).</summary>
        PoolProjectile = 103,

        /// <summary>Pooled HUD UI elements (buffs, indicators, etc.).</summary>
        PoolHud = 104,

        /// <summary>Pooled world-space combat floating text.</summary>
        PoolFloatingText = 105,

        // ---- Active (live) categories ----

        /// <summary>Live hero / player character shells.</summary>
        ActiveCharacter = 200,

        /// <summary>Live projectile shells (currently in flight).</summary>
        ActiveProjectile = 201,

        /// <summary>Live summoned creatures / minions.</summary>
        ActiveSummon = 202,

        /// <summary>Live mirror / clone entities.</summary>
        ActiveClone = 203,

        /// <summary>Live turret / tower entities.</summary>
        ActiveTurret = 204,

        /// <summary>Live wild monster / neutral creep entities.</summary>
        ActiveMonster = 205,

        /// <summary>Live building / static structure entities.</summary>
        ActiveBuilding = 206,

        /// <summary>Live area-effect zones.</summary>
        ActiveArea = 207,

        /// <summary>Live VFX entities (skill VFX, hit flashes, attached VFX).</summary>
        ActiveVfx = 208,

        /// <summary>Live world-space combat floating text.</summary>
        ActiveFloatingText = 209,

        // ---- Misc ----

        /// <summary>Debug / inspection helpers (editor only).</summary>
        Debug = 900,
    }

    /// <summary>
    /// Static helpers that centralize the human-readable category path used to
    /// build the Unity Hierarchy. The returned path uses "/" as a hierarchy
    /// separator and is relative to the <see cref="BattleViewHierarchyRoot"/>.
    /// </summary>
    public static class BattleViewCategoryPaths
    {
        public const string PoolRoot = "_Pool";
        public const string ActiveRoot = "_Active";
        public const string DebugRoot = "_Debug";

        /// <summary>
        /// Returns the path segments relative to the
        /// <see cref="BattleViewHierarchyRoot"/> root for the given category.
        /// </summary>
        /// <remarks>
        /// The returned array always has 2 elements: [topLevel, leafName].
        /// Callers should walk the path top-down, lazily creating each level.
        /// </remarks>
        public static string[] GetPathSegments(BattleViewCategory category)
        {
            switch (category)
            {
                // Pool
                case BattleViewCategory.PoolShell:        return new[] { PoolRoot, "_Shell" };
                case BattleViewCategory.PoolVfx:          return new[] { PoolRoot, "_Vfx" };
                case BattleViewCategory.PoolArea:         return new[] { PoolRoot, "_Area" };
                case BattleViewCategory.PoolProjectile:   return new[] { PoolRoot, "_Projectile" };
                case BattleViewCategory.PoolHud:          return new[] { PoolRoot, "_Hud" };
                case BattleViewCategory.PoolFloatingText: return new[] { PoolRoot, "_FloatingText" };

                // Active
                case BattleViewCategory.ActiveCharacter:  return new[] { ActiveRoot, "_Character" };
                case BattleViewCategory.ActiveProjectile: return new[] { ActiveRoot, "_Projectile" };
                case BattleViewCategory.ActiveSummon:     return new[] { ActiveRoot, "_Summon" };
                case BattleViewCategory.ActiveClone:      return new[] { ActiveRoot, "_Clone" };
                case BattleViewCategory.ActiveTurret:     return new[] { ActiveRoot, "_Turret" };
                case BattleViewCategory.ActiveMonster:    return new[] { ActiveRoot, "_Monster" };
                case BattleViewCategory.ActiveBuilding:   return new[] { ActiveRoot, "_Building" };
                case BattleViewCategory.ActiveArea:         return new[] { ActiveRoot, "_Area" };
                case BattleViewCategory.ActiveVfx:          return new[] { ActiveRoot, "_Vfx" };
                case BattleViewCategory.ActiveFloatingText: return new[] { ActiveRoot, "_FloatingText" };

                // Debug
                case BattleViewCategory.Debug: return new[] { DebugRoot, "_Inspector" };

                default: return new[] { ActiveRoot, "_Uncategorized" };
            }
        }

        /// <summary>
        /// Maps a <see cref="AbilityKit.Game.Battle.Entity.BattleEntityKind"/> to its
        /// matching active-view category. Returns <see cref="BattleViewCategory.Unknown"/>
        /// if no specific category exists.
        /// </summary>
        public static BattleViewCategory FromEntityKind(AbilityKit.Game.Battle.Entity.BattleEntityKind kind)
        {
            switch (kind)
            {
                case AbilityKit.Game.Battle.Entity.BattleEntityKind.Character: return BattleViewCategory.ActiveCharacter;
                case AbilityKit.Game.Battle.Entity.BattleEntityKind.Projectile: return BattleViewCategory.ActiveProjectile;
                case AbilityKit.Game.Battle.Entity.BattleEntityKind.Summon: return BattleViewCategory.ActiveSummon;
                case AbilityKit.Game.Battle.Entity.BattleEntityKind.Clone: return BattleViewCategory.ActiveClone;
                case AbilityKit.Game.Battle.Entity.BattleEntityKind.Turret: return BattleViewCategory.ActiveTurret;
                case AbilityKit.Game.Battle.Entity.BattleEntityKind.Monster: return BattleViewCategory.ActiveMonster;
                case AbilityKit.Game.Battle.Entity.BattleEntityKind.Building: return BattleViewCategory.ActiveBuilding;
                case AbilityKit.Game.Battle.Entity.BattleEntityKind.AreaEffect: return BattleViewCategory.ActiveArea;
                default: return BattleViewCategory.Unknown;
            }
        }
    }
}