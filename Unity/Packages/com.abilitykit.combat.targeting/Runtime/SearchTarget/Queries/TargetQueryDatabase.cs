using System;
using System.Collections.Generic;

namespace AbilityKit.Battle.SearchTarget
{
    /// <summary>
    /// 根据运行时上下文构建查询。业务包可实现该接口来扩展目标查找查询。
    /// </summary>
    public interface ITargetQueryFactory
    {
        bool TryBuild(SearchContext context, out SearchQuery query);
    }

    /// <summary>
    /// 静态查询工厂，适用于不需要根据上下文动态重建的查询。
    /// </summary>
    public sealed class StaticTargetQueryFactory : ITargetQueryFactory
    {
        private readonly SearchQuery _query;

        public StaticTargetQueryFactory(in SearchQuery query)
        {
            _query = query;
        }

        public bool TryBuild(SearchContext context, out SearchQuery query)
        {
            query = _query;
            return query.Provider != null;
        }
    }

    /// <summary>
    /// 轻量查询目录，用于提供类似数据库的目标查找能力。
    /// </summary>
    public sealed class TargetQueryDatabase
    {
        private readonly Dictionary<int, ITargetQueryFactory> _queries = new Dictionary<int, ITargetQueryFactory>();
        private readonly object _syncRoot = new object();
        private readonly TargetSearchEngine _engine;

        public TargetQueryDatabase()
            : this(new TargetSearchEngine())
        {
        }

        public TargetQueryDatabase(TargetSearchEngine engine)
        {
            _engine = engine ?? new TargetSearchEngine();
        }

        public int Count
        {
            get
            {
                lock (_syncRoot)
                {
                    return _queries.Count;
                }
            }
        }

        public void Register(int queryId, ITargetQueryFactory factory)
        {
            lock (_syncRoot)
            {
                if (factory == null)
                {
                    _queries.Remove(queryId);
                    return;
                }

                _queries[queryId] = factory;
            }
        }

        public void Register(int queryId, in SearchQuery query)
        {
            Register(queryId, new StaticTargetQueryFactory(in query));
        }

        public bool Unregister(int queryId)
        {
            lock (_syncRoot)
            {
                return _queries.Remove(queryId);
            }
        }

        public bool TryGetFactory(int queryId, out ITargetQueryFactory factory)
        {
            lock (_syncRoot)
            {
                return _queries.TryGetValue(queryId, out factory);
            }
        }

        public bool TrySearchIds(int queryId, SearchContext context, List<EntityId> results)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (results == null) throw new ArgumentNullException(nameof(results));
            results.Clear();

            if (!TryGetFactory(queryId, out var factory) || factory == null) return false;
            if (!factory.TryBuild(context, out var query) || query.Provider == null) return false;

            _engine.SearchIds(in query, context, results);
            return true;
        }

        public bool TrySearchIds(int queryId, SearchContext context, out SearchResult result)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            result = null;

            if (!TryGetFactory(queryId, out var factory) || factory == null) return false;
            if (!factory.TryBuild(context, out var query) || query.Provider == null) return false;

            result = _engine.SearchIds(in query, context);
            return true;
        }

        public void Clear()
        {
            lock (_syncRoot)
            {
                _queries.Clear();
            }
        }
    }
}
