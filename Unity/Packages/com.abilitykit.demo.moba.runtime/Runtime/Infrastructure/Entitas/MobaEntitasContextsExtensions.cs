using System;

namespace AbilityKit.Demo.Moba
{
    /// <summary>
    /// Entitas 上下文访问扩展。当前仅两个 context：
    /// <list type="bullet">
    /// <item><c>Actor</c>：战斗实体自身（唯一有组件的 context）。</item>
    /// <item><c>Game</c>：战斗内全局状态的显式容器，暂未承载组件。</item>
    /// </list>
    /// <para>
    /// 2026-10-12 收敛：<c>Input</c> / <c>Service</c> 两个空壳已删除。
    /// 输入经 MobaSkillInputCommandHandler 直达 SkillCastCoordinator，服务经 [WorldService] 注册进 DI，
    /// 两者原本与 ECS 职责重复且零消费者。新增全局态请先判断是服务事实（进 DI）还是实体事实（[Game]）。
    /// </para>
    /// </summary>
    public static class MobaEntitasContextsExtensions
    {
        public static global::ActorContext Actor(this global::Entitas.IContexts contexts)
        {
            if (contexts == null) throw new ArgumentNullException(nameof(contexts));
            return ((global::Contexts)contexts).actor;
        }

        public static global::GameContext Game(this global::Entitas.IContexts contexts)
        {
            if (contexts == null) throw new ArgumentNullException(nameof(contexts));
            return ((global::Contexts)contexts).game;
        }
    }
}
