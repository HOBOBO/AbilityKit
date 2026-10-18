using System;
using System.Collections.Generic;
using Entitas;

namespace AbilityKit.Demo.Moba.EntitasAdapters
{
    /// <summary>
    /// <c>[Game]</c> context 的<b>单例实体机制</b>——Entitas 没有"每 context 恰好一个实体"的一等概念，
    /// 本类用「标记组件 + <see cref="IGroup{TEntity}.GetSingleEntity"/>」补上这个能力。
    ///
    /// <para>
    /// <strong>用法</strong>：约定某个 <c>[Game]</c> 组件是"单例宿主"（全局派生量这类），
    /// 用它的组件索引调用 <see cref="GetOrCreate{TComponent}"/>。本类不含任何业务语义，
    /// 也不预设具体组件——标记哪个组件由业务决定。
    /// </para>
    ///
    /// <para>
    /// <strong>为什么用 GetSingleEntity 而不是 EntityIndex</strong>（实测结论，Entitas 1.13）：
    /// </para>
    /// <list type="bullet">
    /// <item>
    /// <see cref="IGroup{TEntity}.GetSingleEntity"/>：空组返回 <c>null</c>，组内多于一个实体时抛
    /// <c>GroupSingleEntityException</c>——<strong>自带"恰好一个"的 fail-fast 检查</strong>，最省事也最安全。
    /// </item>
    /// <item>
    /// <see cref="EntityIndex{TEntity,TKey}"/>：只有 <c>GetEntities(TKey)</c> 返回 <c>HashSet</c>，
    /// <strong>没有单值 <c>GetEntity(TKey)</c></strong>；而且键冲突是<strong>静默</strong>的
    ///（两个实体同键时索引无任何警告地装下两个），框架不强制唯一。要用它做单例必须自己 <c>Single()</c>，
    /// 也就放弃了 Entitas 侧的 fail-fast。
    /// </item>
    /// </list>
    ///
    /// <para>
    /// <strong>为什么标记组件要用固定组件索引而不是实体 id</strong>：实测 <c>IContext.Reset()</c> 会把
    /// <c>creationIndex</c> 重置回 0，若拿 id 当唯一键，Reset 之后新建的实体会与复位前的旧实体撞号。
    /// 组件索引不随 <c>creationIndex</c> 回收而漂移。
    /// </para>
    ///
    /// <para>
    /// <strong>适用面</strong>（重要，别滥用）：只适合"由 Actor 组事件驱动、可 Entitas 可视化的全局派生量"。
    /// 帧号 / 战斗阶段 /随机 seed 这类"权威写入 + 只读读取 + 无组语义"的全局事实应留在 DI 服务里，
    /// 由既有手写回滚 provider 兜底——搬进实体会新增一套回滚清单却不降低 provider 规模。
    /// 判断准则：<strong>服务事实进 DI，实体事实进 <c>[Game]</c></strong>。
    /// </para>
    /// </summary>
    public static class MobaGameSingleton
    {
        // 按组件索引缓存 AllOf matcher（AllOf 每次调用会分配，留缓存避免热点路径重复分配）。
        private static readonly Dictionary<int, IAllOfMatcher<GameEntity>> MarkerCache =
            new Dictionary<int, IAllOfMatcher<GameEntity>>();

        /// <summary>
        /// 取（或创建）挂在指定标记组件上的单例实体。
        /// </summary>
        /// <typeparam name="TComponent">标记组件类型（须是 <c>[Game]</c> 组件）。</typeparam>
        /// <param name="context">Game context（<c>CreateEntity</c> 不在 <c>IContext</c> 接口上，故接具体类型）。</param>
        /// <param name="markerIndex">标记组件索引，取自 <c>GameComponentsLookup.*</c>。</param>
        /// <param name="componentFactory">创建单例实体时写入的标记组件实例工厂。</param>
        /// <param name="createIfMissing">为 true 时不存在则创建。</param>
        /// <returns>单例实体；<paramref name="createIfMissing"/> 为 false 且不存在时返回 <c>null</c>。</returns>
        public static GameEntity? GetOrCreate<TComponent>(
            GameContext context,
            int markerIndex,
            Func<TComponent> componentFactory,
            bool createIfMissing)
            where TComponent : class, IComponent
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (componentFactory == null) throw new ArgumentNullException(nameof(componentFactory));

            var single = context.GetGroup(GetMatcher(markerIndex)).GetSingleEntity();

            // GetSingleEntity：空组返回 null；多于一个会抛 GroupSingleEntityException（由 Entitas 主动 fail-fast）。
            if (single != null) return single;
            if (!createIfMissing) return null;

            var entity = context.CreateEntity();
            entity.AddComponent(markerIndex, componentFactory());
            return entity;
        }

        /// <summary>
        /// 读单例实体上的组件；单例不存在时返回 <c>null</c>。
        /// </summary>
        /// <remarks>
        /// 读取走静态 matcher 而非组遍历，因此调用方持有组件引用即可，无需每帧重新取实体。
        /// </remarks>
        public static TComponent? Read<TComponent>(GameContext context, int markerIndex)
            where TComponent : class, IComponent
        {
            if (context == null) throw new ArgumentNullException(nameof(context));

            var single = context.GetGroup(GetMatcher(markerIndex)).GetSingleEntity();
            return single == null ? null : single.GetComponent(markerIndex) as TComponent;
        }

        /// <summary>
        /// 写单例实体上的组件；单例不存在时按需创建。
        /// </summary>
        public static void Write<TComponent>(
            GameContext context,
            int markerIndex,
            Func<TComponent> componentFactory,
            bool createIfMissing)
            where TComponent : class, IComponent
        {
            var entity = GetOrCreate(context, markerIndex, componentFactory, createIfMissing)
                ?? throw new InvalidOperationException(
                    "[MobaGameSingleton] 单例实体创建失败（createIfMissing=true 却拿到 null，访问器契约被破坏）。");

            entity.ReplaceComponent(markerIndex, componentFactory());
        }

        /// <summary>取（或建）该组件索引对应的单例组 matcher。</summary>
        public static IAllOfMatcher<GameEntity> GetMatcher(int markerIndex)
        {
            ValidateMarkerIndex(markerIndex);

            if (!MarkerCache.TryGetValue(markerIndex, out var matcher))
            {
                matcher = GameMatcher.AllOf(markerIndex);
                MarkerCache[markerIndex] = matcher;
            }

            return matcher;
        }

        /// <summary>
        /// 前置校验：标记组件索引必须落在 Game context 的组件表内。
        /// </summary>
        /// <remarks>
        /// <c>Game</c> context 组件数为 0 时（尚未定义任何 <c>[Game]</c> 组件），
        /// <c>GetGroup(AllOf(0))</c> 会抛裸的 <c>IndexOutOfRangeException</c>，看不出是"你没定义 [Game] 组件"。
        /// 这里转成指名消息，避免排查时对着索引越界一头雾水。
        /// </remarks>
        private static void ValidateMarkerIndex(int markerIndex)
        {
            if (markerIndex >= 0 && markerIndex < GameComponentsLookup.TotalComponents) return;

            throw new ArgumentOutOfRangeException(
                nameof(markerIndex),
                markerIndex,
                $"[MobaGameSingleton] 标记组件索引越界：Game context 当前共 {GameComponentsLookup.TotalComponents} 个组件" +
                "（TotalComponents=0 表示尚未定义任何 [Game] 组件）。" +
                "单例机制需要一个 [Game] 组件作标记——请先在 Runtime/Domain/Components 定义并跑" +
                " generate-entitas.ps1 -Project moba -UpdatePin 重新生成。");
        }
    }
}