using System;
using System.Collections.Generic;

namespace AbilityKit.Triggering.Runtime
{
    /// <summary>
    /// 组合上下文源
    /// 允许合并多个上下文源，根据优先级返回有效的上下文
    /// </summary>
    public sealed class CompositeContextSource<TCtx> : ITriggerContextSource<TCtx>
    {
        private readonly List<ContextSourceEntry<TCtx>> _sources = new List<ContextSourceEntry<TCtx>>();
        private TCtx _lastContext;
        private int _currentIndex = -1;

        /// <summary>
        /// 添加一个上下文源
        /// </summary>
        public CompositeContextSource<TCtx> Add(ITriggerContextSource<TCtx> source, int priority = 0)
        {
            if (source != null)
            {
                _sources.Add(new ContextSourceEntry<TCtx>(source, priority));
                _sources.Sort((a, b) => b.Priority.CompareTo(a.Priority));
            }
            return this;
        }

        /// <summary>
        /// 移除所有上下文源
        /// </summary>
        public void Clear()
        {
            _sources.Clear();
            _currentIndex = -1;
        }

        /// <summary>
        /// 获取当前有效的上下文
        /// 按优先级遍历源，返回第一个非空/有效的上下文
        /// </summary>
        public TCtx GetContext()
        {
            if (_sources.Count == 0)
            {
                return _lastContext;
            }

            for (int i = 0; i < _sources.Count; i++)
            {
                var entry = _sources[i];
                if (entry.Source == null) continue;

                var ctx = entry.Source.GetContext();
                if (IsValidContext(ctx))
                {
                    _currentIndex = i;
                    _lastContext = ctx;
                    return ctx;
                }
            }

            return _lastContext;
        }

        /// <summary>
        /// 获取当前上下文源的索引
        /// </summary>
        public int CurrentSourceIndex => _currentIndex;

        /// <summary>
        /// 获取上下文源总数
        /// </summary>
        public int SourceCount => _sources.Count;

        private static bool IsValidContext(TCtx ctx)
        {
            if (ctx == null) return false;

            if (ctx is ValueType && typeof(TCtx).IsValueType)
            {
                return true;
            }

            return ctx != null;
        }

        private readonly struct ContextSourceEntry<TCtx>
        {
            public readonly ITriggerContextSource<TCtx> Source;
            public readonly int Priority;

            public ContextSourceEntry(ITriggerContextSource<TCtx> source, int priority)
            {
                Source = source;
                Priority = priority;
            }
        }
    }

    /// <summary>
    /// 组合上下文源工厂
    /// </summary>
    public static class CompositeContextSource
    {
        /// <summary>
        /// 创建一个组合上下文源
        /// </summary>
        public static CompositeContextSource<TCtx> Create<TCtx>()
        {
            return new CompositeContextSource<TCtx>();
        }

        /// <summary>
        /// 创建一个组合上下文源并添加初始源
        /// </summary>
        public static CompositeContextSource<TCtx> Create<TCtx>(ITriggerContextSource<TCtx> primary)
        {
            return new CompositeContextSource<TCtx>().Add(primary, priority: 0);
        }

        /// <summary>
        /// 创建一个组合上下文源并添加多个源
        /// </summary>
        public static CompositeContextSource<TCtx> Create<TCtx>(params ITriggerContextSource<TCtx>[] sources)
        {
            var composite = new CompositeContextSource<TCtx>();
            for (int i = 0; i < sources.Length; i++)
            {
                composite.Add(sources[i], priority: sources.Length - i);
            }
            return composite;
        }
    }
}
