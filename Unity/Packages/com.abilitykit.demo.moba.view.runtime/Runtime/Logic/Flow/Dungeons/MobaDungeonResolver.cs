using System;
using System.Collections.Generic;

namespace AbilityKit.Game.Flow.Dungeons
{
    /// <summary>
    /// 延迟解析的副本解析器。目录在首次使用时才建立，避免启动路径强制加载配置。
    /// </summary>
    public sealed class MobaDungeonResolver : IBattleDungeonResolver
    {
        private readonly Lazy<MobaDungeonCatalog> _catalog;
        private readonly Func<MobaDungeonRegistry> _registryFactory;

        public MobaDungeonResolver(Func<MobaDungeonCatalog> catalogFactory,
            Func<MobaDungeonRegistry> registryFactory = null)
        {
            if (catalogFactory == null) throw new ArgumentNullException(nameof(catalogFactory));
            _catalog = new Lazy<MobaDungeonCatalog>(catalogFactory);
            _registryFactory = registryFactory ?? MobaDungeonRegistry.CreateStandard;
        }

        public MobaDungeonDef Resolve(int dungeonId) =>
            _catalog.Value.TryGet(dungeonId, out var dungeon) ? dungeon : null;

        /// <summary>全部副本定义，大厅用它渲染可选副本列表。</summary>
        public IReadOnlyList<MobaDungeonDef> All => _catalog.Value.All;

        public MobaDungeonRegistry CreateRegistry() => _registryFactory();
    }
}
