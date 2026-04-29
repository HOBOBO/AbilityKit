using System;
using System.Collections.Generic;
using System.Linq;

namespace AbilityKit.Context
{
    /// <summary>
    /// 查询条件
    /// </summary>
    public struct QueryCondition
    {
        public int PropertyTypeId { get; }

        public QueryCondition(int propertyTypeId)
        {
            PropertyTypeId = propertyTypeId;
        }
    }

    /// <summary>
    /// 查询器
    /// 用于查询满足条件的实体
    /// </summary>
    public sealed class Query
    {
        private readonly List<QueryCondition> _conditions = new();

        internal Query() { }

        /// <summary>
        /// 添加条件：实体必须包含指定属性类型
        /// </summary>
        public Query With<T>() where T : IProperty
        {
            var type = PropertyTypeRegistry.Instance.Get<T>();
            if (type == null)
            {
                type = PropertyTypeRegistry.Instance.Register<T>();
            }
            _conditions.Add(new QueryCondition(type.Id));
            return this;
        }

        /// <summary>
        /// 执行查询
        /// </summary>
        public IEnumerable<long> Execute(ContextRegistry registry)
        {
            if (_conditions.Count == 0)
            {
                return Enumerable.Empty<long>();
            }

            var firstCondition = _conditions[0];
            var candidates = registry.GetEntitiesWith(firstCondition.PropertyTypeId);

            for (int i = 1; i < _conditions.Count; i++)
            {
                var entitiesWithProp = registry.GetEntitiesWith(_conditions[i].PropertyTypeId).ToHashSet();
                candidates = candidates.Intersect(entitiesWithProp).ToList();
            }

            return candidates;
        }
    }

    /// <summary>
    /// 查询构建器
    /// </summary>
    public sealed class QueryBuilder
    {
        private readonly ContextRegistry _registry;

        internal QueryBuilder(ContextRegistry registry)
        {
            _registry = registry;
        }

        /// <summary>
        /// 创建新的查询
        /// </summary>
        public Query CreateQuery()
        {
            return new Query();
        }
    }
}
