using System;

namespace AbilityKit.Ability.World
{
    /// <summary>
    /// Entitas 上下文的工厂接口。
    /// 用于创建和释放 Entitas 上下文集合。
    /// </summary>
    public interface IEntitasContextsFactory
    {
        /// <summary>
        /// 创建 Entitas 上下文集合。
        /// </summary>
        /// <returns>新创建的上下文集合</returns>
        global::Entitas.IContexts Create();

        /// <summary>
        /// 释放指定的上下文集合。
        /// </summary>
        /// <param name="contexts">要释放的上下文集合</param>
        void Release(global::Entitas.IContexts contexts);
    }
}