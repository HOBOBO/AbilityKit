using System;
using System.Collections.Generic;

namespace AbilityKit.World.ECS
{
    /// <summary>
    /// 组件类型注册表，管理组件类型到整数ID的映射。
    /// 支持实例化，允许每个 World 有独立的组件注册表。
    /// </summary>
    public interface IComponentRegistry
    {
        /// <summary>获取类型的注册ID（首次访问时自动注册）。</summary>
        int GetId<T>();

        /// <summary>获取类型的注册ID（反射版本）。</summary>
        int GetId(Type type);

        /// <summary>尝试通过ID获取类型。</summary>
        bool TryGetType(int typeId, out Type type);

        /// <summary>通过ID获取类型（未注册抛出异常）。</summary>
        Type GetType(int typeId);

        /// <summary>已注册类型数量。</summary>
        int Count { get; }

        /// <summary>获取所有已注册类型的快照。</summary>
        IReadOnlyList<Type> GetAllTypes();
    }
}
