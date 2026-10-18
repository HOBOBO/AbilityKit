using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace AbilityKit.Game.Flow.Dungeons
{
    /// <summary>
    /// 副本定义目录。副本定义来自 Luban 导出的配置表，这里只负责按 id 解析并缓存。
    /// </summary>
    public sealed class MobaDungeonCatalog
    {
        private readonly Dictionary<int, MobaDungeonDef> _byId;
        private readonly List<MobaDungeonDef> _ordered;

        public MobaDungeonCatalog(IEnumerable<MobaDungeonDef> dungeons)
        {
            if (dungeons == null) throw new ArgumentNullException(nameof(dungeons));
            _byId = new Dictionary<int, MobaDungeonDef>();
            _ordered = new List<MobaDungeonDef>();
            foreach (var dungeon in dungeons)
            {
                if (dungeon == null) continue;
                if (!_byId.TryAdd(dungeon.Id, dungeon))
                    throw new InvalidOperationException("Duplicate dungeon id: " + dungeon.Id);
                _ordered.Add(dungeon);
            }
            if (_ordered.Count == 0)
                throw new InvalidOperationException("Dungeon catalog is empty.");
        }

        public IReadOnlyList<MobaDungeonDef> All => new ReadOnlyCollection<MobaDungeonDef>(_ordered);

        public bool TryGet(int dungeonId, out MobaDungeonDef dungeon) =>
            _byId.TryGetValue(dungeonId, out dungeon);

        public MobaDungeonDef Get(int dungeonId) =>
            _byId.TryGetValue(dungeonId, out var dungeon)
                ? dungeon
                : throw new InvalidOperationException("Dungeon definition is missing: " + dungeonId);

        /// <summary>筛选出在指定宿主模式与人数下可进入的副本。</summary>
        public IReadOnlyList<MobaDungeonDef> Select(string hostMode, int playerCount)
        {
            var result = new List<MobaDungeonDef>(_ordered.Count);
            foreach (var dungeon in _ordered)
                if (dungeon.AllowsHostMode(hostMode) && dungeon.AcceptsPlayerCount(playerCount))
                    result.Add(dungeon);
            return result;
        }
    }
}
