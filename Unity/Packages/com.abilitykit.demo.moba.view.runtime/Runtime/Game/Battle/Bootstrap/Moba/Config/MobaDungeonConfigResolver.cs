using System;
using System.Collections.Generic;
using AbilityKit.Ability.Config;
using AbilityKit.Demo.Moba.Config.BattleDemo.MO;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Game.Flow.Dungeons;

namespace AbilityKit.Game.Battle.Moba.Config
{
    /// <summary>
    /// 把 Luban 导出的副本配置投影成运行时 <see cref="MobaDungeonDef"/>。
    /// 放在 Unity 侧而不是 Flow Core，因为只有它需要引用配置数据库类型；
    /// Flow Core 只依赖 <see cref="MobaDungeonDef"/> 与 <see cref="IBattleDungeonResolver"/> 抽象。
    /// </summary>
    public static class MobaDungeonConfigResolver
    {
        /// <summary>把配置表中的全部副本投影为运行时目录。</summary>
        /// <param name="dungeons">副本表内容，只含客户端装配契约。</param>
        /// <param name="gameplayDurationMs">玩法 id → 默认时长（毫秒）。副本时长是玩法常量，
        /// 由 gameplays 表提供，副本表不复制。</param>
        public static MobaDungeonCatalog Resolve(IEnumerable<DungeonMO> dungeons,
            Func<int, int> gameplayDurationMs = null)
        {
            if (dungeons == null) throw new ArgumentNullException(nameof(dungeons));
            gameplayDurationMs ??= _ => 0;
            var projected = new List<MobaDungeonDef>();
            foreach (var dungeon in dungeons)
            {
                if (dungeon == null) continue;
                projected.Add(ResolveOne(dungeon, gameplayDurationMs));
            }
            return new MobaDungeonCatalog(projected);
        }

        /// <summary>按 id 解析单个副本定义。</summary>
        public static MobaDungeonDef ResolveOne(DungeonMO dungeon, Func<int, int> gameplayDurationMs = null)
        {
            if (dungeon == null) throw new ArgumentNullException(nameof(dungeon));
            gameplayDurationMs ??= _ => 0;
            return new MobaDungeonDef(
                dungeon.Id,
                dungeon.Name,
                dungeon.GameplayId,
                dungeon.FlowProfileId,
                gameplayDurationMs(dungeon.GameplayId),
                dungeon.MinPlayerCount,
                dungeon.MaxPlayerCount,
                dungeon.AllowedHostModes,
                dungeon.SceneProviderId,
                dungeon.GameplayHandlerId,
                dungeon.OutcomeRuleId,
                dungeon.RequiredPortGroups,
                dungeon.Tags);
        }

        /// <summary>从配置数据库解析单个副本定义；未知 id 返回 null。</summary>
        public static MobaDungeonDef ResolveOne(MobaConfigDatabase database, int dungeonId)
        {
            if (database == null) throw new ArgumentNullException(nameof(database));
            return database.TryGetDungeon(dungeonId, out var dungeon) ? ResolveOne(dungeon) : null;
        }

        /// <summary>
        /// 面向配置数据库的副本解析器。大厅与对战流程共用同一个解析入口，
        /// 保证「列表看到的副本」与「实际进入的副本」来自同一份定义。
        /// </summary>
        public static MobaDungeonResolver FromDatabase(MobaConfigDatabase database,
            System.Func<MobaDungeonRegistry> registryFactory = null)
        {
            if (database == null) throw new ArgumentNullException(nameof(database));
            return new MobaDungeonResolver(
                () => Resolve(database.GetAllDungeons(), ResolveGameplayDurationMs(database)),
                registryFactory);
        }

        /// <summary>
        /// 构造「玩法 id → 默认时长」读取器。副本表不再存时长，统一从玩法表取，
        /// 这样同一玩法的时长只有一个来源，不会与副本表漂移。
        /// </summary>
        private static Func<int, int> ResolveGameplayDurationMs(MobaConfigDatabase database)
        {
            return gameplayId =>
                database.TryGetGameplay(gameplayId, out var gameplay) ? gameplay.DefaultDurationMs : 0;
        }
    }
}
