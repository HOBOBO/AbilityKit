namespace AbilityKit.Game.Battle.Hierarchy
{
    /// <summary>
    ///
    /// 用于在 Unity 层级中组织战斗视图 GameObject 的分类。
    /// 每个分类映射到 <see cref="BattleViewHierarchyRoot"/> 下的专用子根节点。
    ///
    /// 管理器生成的层级布局：
    /// <code>
    /// [Battle] (BattleViewHierarchyRoot)
    /// ├── _Pool/          // 等待复用的非活动池实例
    /// │   ├── _Shell         // BattleViewShellPool 分桶
    /// │   ├── _Vfx           // BattleVfxGameObjectPool 分桶
    /// │   ├── _Area          // BattleAreaVfxPool 分桶（Model / Range / Vfx）
    /// │   ├── _Projectile    // BattleProjectileShellPool 分桶
    /// │   ├── _FloatingText  // 池化的世界空间战斗文字
    /// │   └── _Hud           // 其他 HUD 相关池化 UI 元素
    /// ├── _Active/        // 当前已租出的活动 GameObject
    /// │   ├── _Character     // 英雄或玩家外壳
    /// │   ├── _Projectile    // 活动投射物
    /// │   ├── _Summon        // 召唤生物
    ///   │   ├── _Turret        // towers
    /// │   ├── _Monster       // 野怪
    ///   │   ├── _Building      // buildings
    /// │   ├── _Area          // 活动区域效果
    /// │   ├── _Vfx           // 活动特效实体
    /// │   └── _FloatingText  // 活动世界空间战斗文字
    /// └── _Debug/         // 仅编辑器可视化辅助对象
    /// </code>
    ///
    /// </summary>
    public enum BattleViewCategory
    {
        /// <summary>
        /// 未知或未分类，用作安全回退。
        /// </summary>
        Unknown = 0,

        // ---- Pool (inactive) categories ----

        /// <summary>
        /// 池化实体外壳（BattleViewShellPool 分桶）。
        /// </summary>
        PoolShell = 100,

        /// <summary>
        /// 池化特效 GameObject（BattleVfxGameObjectPool 分桶）。
        /// </summary>
        PoolVfx = 101,

        /// <summary>
        /// 池化区域效果 GameObject（BattleAreaVfxPool 分桶）。
        /// </summary>
        PoolArea = 102,

        /// <summary>
        /// 池化投射物外壳（BattleProjectileShellPool 分桶）。
        /// </summary>
        PoolProjectile = 103,

        /// <summary>
        /// 池化 HUD UI 元素，如 Buff 和指示器。
        /// </summary>
        PoolHud = 104,

        /// <summary>
        /// 池化世界空间战斗飘字。
        /// </summary>
        PoolFloatingText = 105,

        // ---- Active (live) categories ----

        /// <summary>
        /// 活动英雄或玩家角色外壳。
        /// </summary>
        ActiveCharacter = 200,

        /// <summary>
        /// 飞行中的活动投射物外壳。
        /// </summary>
        ActiveProjectile = 201,

        /// <summary>
        /// 活动召唤生物或随从。
        /// </summary>
        ActiveSummon = 202,

        /// <summary>
        /// 活动镜像或分身实体。
        /// </summary>
        ActiveClone = 203,

        /// <summary>
        /// 活动炮塔或防御塔实体。
        /// </summary>
        ActiveTurret = 204,

        /// <summary>
        /// 活动野怪或中立生物实体。
        /// </summary>
        ActiveMonster = 205,

        /// <summary>
        /// 活动建筑或静态结构实体。
        /// </summary>
        ActiveBuilding = 206,

        /// <summary>
        /// 活动兵线小兵实体。
        /// </summary>
        ActiveMinion = 210,

        /// <summary>
        /// 活动区域效果范围。
        /// </summary>
        ActiveArea = 207,

        /// <summary>
        /// 活动特效实体，包括技能特效、命中闪光及附着特效。
        /// </summary>
        ActiveVfx = 208,

        /// <summary>
        /// 活动世界空间战斗飘字。
        /// </summary>
        ActiveFloatingText = 209,

        // ---- Misc ----

        /// <summary>
        /// 调试及检查辅助对象，仅编辑器使用。
        /// </summary>
        Debug = 900,
    }

    /// <summary>
    /// 集中管理 Unity 层级可读分类路径的静态辅助方法。返回路径相对于 <see cref="BattleViewHierarchyRoot"/>，使用“/”分隔层级。
    /// </summary>
    public static class BattleViewCategoryPaths
    {
        public const string PoolRoot = "_Pool";
        public const string ActiveRoot = "_Active";
        public const string DebugRoot = "_Debug";

        /// <summary>
        /// 返回指定分类相对于 <see cref="BattleViewHierarchyRoot"/> 的路径段。
        /// </summary>
        /// <remarks>
        /// 返回数组始终有两个元素：[topLevel, leafName]。调用方应从上到下遍历，按需创建各级。
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
                case BattleViewCategory.ActiveMinion:     return new[] { ActiveRoot, "_Minion" };
                case BattleViewCategory.ActiveArea:         return new[] { ActiveRoot, "_Area" };
                case BattleViewCategory.ActiveVfx:          return new[] { ActiveRoot, "_Vfx" };
                case BattleViewCategory.ActiveFloatingText: return new[] { ActiveRoot, "_FloatingText" };

                // Debug
                case BattleViewCategory.Debug: return new[] { DebugRoot, "_Inspector" };

                default: return new[] { ActiveRoot, "_Uncategorized" };
            }
        }

        /// <summary>
        /// 将 <see cref="AbilityKit.Game.Battle.Entity.BattleEntityKind"/> 映射为对应活动视图分类；无具体分类时返回 <see cref="BattleViewCategory.Unknown"/>。
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
                case AbilityKit.Game.Battle.Entity.BattleEntityKind.Minion: return BattleViewCategory.ActiveMinion;
                case AbilityKit.Game.Battle.Entity.BattleEntityKind.AreaEffect: return BattleViewCategory.ActiveArea;
                default: return BattleViewCategory.Unknown;
            }
        }
    }
}
