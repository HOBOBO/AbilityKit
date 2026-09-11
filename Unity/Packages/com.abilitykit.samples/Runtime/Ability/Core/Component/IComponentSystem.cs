#nullable enable

using System;
using System.Collections.Generic;

namespace AbilityKit.Samples.Logic.Ability.Core.Component
{
    /// <summary>
    /// 组件系统接口，定义组件的查询和管理操作。
    /// </summary>
    public interface IComponentSystem
    {
        /// <summary>
        /// 注册一个实体。
        /// </summary>
        void Register<T>(int entityId, T component) where T : class;

        /// <summary>
        /// 获取实体的组件。
        /// </summary>
        T? Get<T>(int entityId) where T : class;

        /// <summary>
        /// 移除实体的组件。
        /// </summary>
        bool Remove<T>(int entityId) where T : class;

        /// <summary>
        /// 检查实体是否拥有指定类型的组件。
        /// </summary>
        bool Has<T>(int entityId) where T : class;

        /// <summary>
        /// 查询所有拥有指定类型组件的实体。
        /// </summary>
        IReadOnlyList<int> Query<T>() where T : class;

        /// <summary>
        /// 查询满足多个组件条件的实体。
        /// </summary>
        IReadOnlyList<int> Query<T1, T2>() where T1 : class where T2 : class;
    }
}
