using System;
using System.Collections.Generic;

namespace AbilityKit.Game.Flow.Dungeons
{
    /// <summary>
    /// 副本解析端口。视图层实现它来读取配置表，纯逻辑与测试可注入假实现。
    /// </summary>
    public interface IBattleDungeonResolver
    {
        /// <summary>解析单个副本定义；未知 id 返回 null。</summary>
        MobaDungeonDef Resolve(int dungeonId);

        /// <summary>
        /// 生成本局使用的扩展注册中心。实现方负责把平台相关的扩展点
        /// （例如 Unity 场景装配器）注册进来。
        /// </summary>
        MobaDungeonRegistry CreateRegistry();
    }

    /// <summary>
    /// 基于固定列表的解析器，供测试与 headless 场景使用。
    /// </summary>
    public sealed class StaticBattleDungeonResolver : IBattleDungeonResolver
    {
        private readonly MobaDungeonCatalog _catalog;
        private readonly Func<MobaDungeonRegistry> _registryFactory;

        public StaticBattleDungeonResolver(MobaDungeonCatalog catalog,
            Func<MobaDungeonRegistry> registryFactory = null)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _registryFactory = registryFactory ?? MobaDungeonRegistry.CreateStandard;
        }

        public MobaDungeonDef Resolve(int dungeonId) =>
            _catalog.TryGet(dungeonId, out var dungeon) ? dungeon : null;

        public MobaDungeonRegistry CreateRegistry() => _registryFactory();
    }
}
