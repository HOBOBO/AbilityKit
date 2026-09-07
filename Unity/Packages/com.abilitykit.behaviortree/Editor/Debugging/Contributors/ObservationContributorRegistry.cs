#nullable enable

using System;
using System.Collections.Generic;

using UnityEngine.Scripting.APIUpdating;
namespace AbilityKit.BehaviorTree.Editor.Debugging.Contributors
{
    /// <summary>贡献者类别（用于错误归因）。</summary>
    [MovedFrom(true, "AbilityKit.BehaviorTree.Editor", "AbilityKit.BehaviorTree.Editor", "BtObservationContributorKind")]
    public enum ObservationContributorKind
    {
        Detail = 0,
        Filter = 1,
        Overlay = 2,
    }

    /// <summary>被隔离的贡献者异常记录。</summary>
    [MovedFrom(true, "AbilityKit.BehaviorTree.Editor", "AbilityKit.BehaviorTree.Editor", "BtObservationContributorError")]
    public sealed class ObservationContributorError
    {
        public string ContributorId { get; }
        public ObservationContributorKind Kind { get; }
        public Exception Exception { get; }

        public ObservationContributorError(string contributorId, ObservationContributorKind kind, Exception exception)
        {
            ContributorId = contributorId ?? "";
            Kind = kind;
            Exception = exception;
        }
    }

    /// <summary>
    /// 观察扩展的注册中心：管理 detail/filter/overlay 三类只读贡献者。
    /// 返回独立 <see cref="IDisposable"/> 句柄；重复 id 注册抛出、同优先级按后注册者优先、
    /// 贡献者异常被隔离为 diagnostics 而不中断调用方；<see cref="Clear"/> 支持 domain reload 后重建。
    /// </summary>
    [MovedFrom(true, "AbilityKit.BehaviorTree.Editor", "AbilityKit.BehaviorTree.Editor", "BtObservationContributorRegistry")]
    public sealed class ObservationContributorRegistry
    {
        private readonly List<IObservationDetailContributor> _details = new();
        private readonly List<IObservationFilter> _filters = new();
        private readonly List<IObservationOverlayContributor> _overlays = new();
        private readonly List<ObservationContributorError> _errors = new();

        public static ObservationContributorRegistry Default { get; } = new ObservationContributorRegistry();

        public IReadOnlyList<IObservationDetailContributor> Details => _details;
        public IReadOnlyList<IObservationFilter> Filters => _filters;
        public IReadOnlyList<IObservationOverlayContributor> Overlays => _overlays;
        public IReadOnlyList<ObservationContributorError> Errors => _errors;

        public IDisposable Register(IObservationDetailContributor contributor) =>
            RegisterCore(contributor, ObservationContributorKind.Detail, c => AddDetail((IObservationDetailContributor)c));

        public IDisposable Register(IObservationFilter filter) =>
            RegisterCore(filter, ObservationContributorKind.Filter, c => AddFilter((IObservationFilter)c));

        public IDisposable Register(IObservationOverlayContributor contributor) =>
            RegisterCore(contributor, ObservationContributorKind.Overlay, c => AddOverlay((IObservationOverlayContributor)c));

        /// <summary>收集 detail section，隔离每个贡献者异常。</summary>
        public List<ObservationDetailSection> CollectSections(ObservationDetailContext context)
        {
            var result = new List<ObservationDetailSection>();
            for (var i = 0; i < _details.Count; i++)
            {
                var contributor = _details[i];
                try
                {
                    var sections = contributor.GetSections(context);
                    if (sections != null) result.AddRange(sections);
                }
                catch (Exception ex)
                {
                    _errors.Add(new ObservationContributorError(contributor.Id, ObservationContributorKind.Detail, ex));
                }
            }
            return result;
        }

        /// <summary>收集 overlay，隔离每个贡献者异常。</summary>
        public List<ObservationOverlay> CollectOverlays(ObservationOverlayContext context)
        {
            var result = new List<ObservationOverlay>();
            CollectOverlays(context, result);
            return result;
        }

        public void CollectOverlays(ObservationOverlayContext context, List<ObservationOverlay> result)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            result.Clear();
            for (var i = 0; i < _overlays.Count; i++)
            {
                var contributor = _overlays[i];
                try
                {
                    var overlays = contributor.GetOverlays(context);
                    if (overlays != null) result.AddRange(overlays);
                }
                catch (Exception ex)
                {
                    _errors.Add(new ObservationContributorError(contributor.Id, ObservationContributorKind.Overlay, ex));
                }
            }
        }

        /// <summary>
        /// 任一适用于当前范围的过滤器匹配即返回 true。若当前范围没有适用过滤器则不过滤；
        /// 抛异常的过滤器视为不匹配并记录。
        /// </summary>
        public bool AnyFilterMatches(ObservationFilterContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            var scope = context.Scope();
            var hasApplicableFilter = false;
            for (var i = 0; i < _filters.Count; i++)
            {
                var filter = _filters[i];
                if (filter is IScopedObservationFilter scoped && !scoped.AppliesTo(scope)) continue;
                hasApplicableFilter = true;
                try
                {
                    if (filter.Matches(context)) return true;
                }
                catch (Exception ex)
                {
                    _errors.Add(new ObservationContributorError(filter.Id, ObservationContributorKind.Filter, ex));
                }
            }
            return !hasApplicableFilter;
        }

        public void ClearErrors() => _errors.Clear();

        /// <summary>清空全部贡献者与错误记录（domain reload 后重建用）。</summary>
        public void Clear()
        {
            _details.Clear();
            _filters.Clear();
            _overlays.Clear();
            _errors.Clear();
        }

        private IDisposable RegisterCore(
            object contributor, ObservationContributorKind kind, Action<object> add)
        {
            if (contributor == null) throw new ArgumentNullException(nameof(contributor));
            if (ContainsId(contributor))
                throw new ArgumentException($"A contributor with id '{ContributorId(contributor)}' is already registered.", nameof(contributor));
            add(contributor);
            return new Handle(this, kind, contributor);
        }

        private void AddDetail(IObservationDetailContributor contributor) =>
            InsertByPriority(_details, contributor, c => c.Priority);

        // 过滤器可组合，无优先级；按注册顺序追加。
        private void AddFilter(IObservationFilter filter) => _filters.Add(filter);

        private void AddOverlay(IObservationOverlayContributor contributor) =>
            InsertByPriority(_overlays, contributor, c => c.Priority);

        private static void InsertByPriority<T>(List<T> list, T item, Func<T, int> priorityOf)
        {
            var priority = priorityOf(item);
            var index = 0;
            while (index < list.Count && priorityOf(list[index]) < priority) index++;
            list.Insert(index, item);
        }

        private bool ContainsId(object contributor)
        {
            var id = ContributorId(contributor);
            return IndexOfId(id) >= 0;
        }

        private int IndexOfId(string id)
        {
            for (var i = 0; i < _details.Count; i++) if (string.Equals(_details[i].Id, id, StringComparison.Ordinal)) return i;
            for (var i = 0; i < _filters.Count; i++) if (string.Equals(_filters[i].Id, id, StringComparison.Ordinal)) return i;
            for (var i = 0; i < _overlays.Count; i++) if (string.Equals(_overlays[i].Id, id, StringComparison.Ordinal)) return i;
            return -1;
        }

        private static string ContributorId(object contributor) => contributor switch
        {
            IObservationDetailContributor d => d.Id,
            IObservationFilter f => f.Id,
            IObservationOverlayContributor o => o.Id,
            _ => "",
        };

        private void Remove(ObservationContributorKind kind, object contributor)
        {
            switch (kind)
            {
                case ObservationContributorKind.Detail:
                    _details.Remove((IObservationDetailContributor)contributor);
                    break;
                case ObservationContributorKind.Filter:
                    _filters.Remove((IObservationFilter)contributor);
                    break;
                case ObservationContributorKind.Overlay:
                    _overlays.Remove((IObservationOverlayContributor)contributor);
                    break;
            }
        }

        private sealed class Handle : IDisposable
        {
            private readonly ObservationContributorRegistry _owner;
            private readonly ObservationContributorKind _kind;
            private readonly object _contributor;
            private bool _disposed;

            public Handle(ObservationContributorRegistry owner, ObservationContributorKind kind, object contributor)
            {
                _owner = owner;
                _kind = kind;
                _contributor = contributor;
            }

            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                _owner.Remove(_kind, _contributor);
            }
        }
    }
}
