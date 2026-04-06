using System;

namespace AbilityKit.World.ECS
{
    /// <summary>
    /// 实体查询结果，提供类型安全的遍历API。
    /// 支持单组件、双组件、三组件查询。
    /// </summary>

    #region 单组件查询

    /// <summary>单组件查询结果。</summary>
    public readonly struct EntityQuery<T1>
        where T1 : struct
    {
        private readonly int _typeId1;
        private readonly EntityWorld _world;

        internal EntityQuery(int typeId1, EntityWorld world)
        {
            _typeId1 = typeId1;
            _world = world;
        }

        /// <summary>遍历所有匹配实体（零分配）。</summary>
        public void ForEach(Action<IEntity, T1> visitor)
        {
            _world.QueryImpl<T1>(_typeId1, visitor);
        }

        /// <summary>返回匹配实体的数量。</summary>
        public int Count()
        {
            int count = 0;
            ForEach((_, _) => count++);
            return count;
        }

        /// <summary>检查是否有任何匹配实体。</summary>
        public bool Any()
        {
            bool found = false;
            ForEach((_, _) => found = true);
            return found;
        }
    }

    #endregion

    #region 双组件查询

    /// <summary>双组件查询结果。</summary>
    public readonly struct EntityQuery<T1, T2>
        where T1 : struct where T2 : struct
    {
        private readonly int _typeId1;
        private readonly int _typeId2;
        private readonly EntityWorld _world;

        internal EntityQuery(int typeId1, int typeId2, EntityWorld world)
        {
            _typeId1 = typeId1;
            _typeId2 = typeId2;
            _world = world;
        }

        /// <summary>遍历所有匹配实体（零分配）。</summary>
        public void ForEach(Action<IEntity, T1, T2> visitor)
        {
            _world.QueryImpl<T1, T2>(_typeId1, _typeId2, visitor);
        }

        /// <summary>返回匹配实体的数量。</summary>
        public int Count()
        {
            int count = 0;
            ForEach((_, _, _) => count++);
            return count;
        }

        /// <summary>检查是否有任何匹配实体。</summary>
        public bool Any()
        {
            bool found = false;
            ForEach((_, _, _) => found = true);
            return found;
        }
    }

    #endregion

    #region 三组件查询

    /// <summary>三组件查询结果。</summary>
    public readonly struct EntityQuery<T1, T2, T3>
        where T1 : struct where T2 : struct where T3 : struct
    {
        private readonly int _typeId1;
        private readonly int _typeId2;
        private readonly int _typeId3;
        private readonly EntityWorld _world;

        internal EntityQuery(int typeId1, int typeId2, int typeId3, EntityWorld world)
        {
            _typeId1 = typeId1;
            _typeId2 = typeId2;
            _typeId3 = typeId3;
            _world = world;
        }

        /// <summary>遍历所有匹配实体（零分配）。</summary>
        public void ForEach(Action<IEntity, T1, T2, T3> visitor)
        {
            _world.QueryImpl<T1, T2, T3>(_typeId1, _typeId2, _typeId3, visitor);
        }

        /// <summary>返回匹配实体的数量。</summary>
        public int Count()
        {
            int count = 0;
            ForEach((_, _, _, _) => count++);
            return count;
        }

        /// <summary>检查是否有任何匹配实体。</summary>
        public bool Any()
        {
            bool found = false;
            ForEach((_, _, _, _) => found = true);
            return found;
        }
    }

    #endregion
}
