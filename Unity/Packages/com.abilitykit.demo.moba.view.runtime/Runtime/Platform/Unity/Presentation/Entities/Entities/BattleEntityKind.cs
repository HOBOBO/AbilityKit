namespace AbilityKit.Game.Battle.Entity
{
    /// <summary>
    /// Kinds of battle entities from the view/presentation layer's perspective.
    /// Each kind may have distinct visual representation (shell/prefab), pooling strategy,
    /// and lifecycle management.
    ///
    /// Note: <see cref="AreaEffect"/> entities are not bound through the main
    /// <see cref="Flow.BattleViewBinder"/> path; they use <see cref="Flow.BattleAreaViewSystem"/> instead.
    /// </summary>
    public enum BattleEntityKind
    {
        /// <summary>Uninitialized or unrecognized entity kind.</summary>
        Unknown = 0,

        /// <summary>Hero / player character.</summary>
        Character = 1,

        /// <summary>Projectile (e.g. skill shot, arrow).</summary>
        Projectile = 2,

        /// <summary>Summoned creature / minion created by a skill.</summary>
        Summon = 3,

        /// <summary>Clone / mirror image created by a skill.</summary>
        Clone = 4,

        /// <summary>Defensive turret / tower.</summary>
        Turret = 5,

        /// <summary>Wild monster / neutral creep.</summary>
        Monster = 6,

        /// <summary>Static or destructible building.</summary>
        Building = 7,

        /// <summary>Area-effect zone (AOE) — handled by <see cref="Flow.BattleAreaViewSystem"/>.</summary>
        AreaEffect = 100,
    }
}
