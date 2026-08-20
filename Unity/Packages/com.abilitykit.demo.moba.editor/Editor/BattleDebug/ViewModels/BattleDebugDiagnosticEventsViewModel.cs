using System;
using System.Collections.Generic;
using AbilityKit.Demo.Moba.Diagnostics;
using AbilityKit.Game.Editor.Diagnostics;

namespace AbilityKit.Game.Editor
{
    internal enum BattleDebugDiagnosticEventScope
    {
        DamageAndEffects = 0,
        DamageAndHeal = 1,
        Effects = 2,
        Skills = 3,
        Buffs = 4,
        TemporaryEntities = 5,
        Warnings = 6,
        Triggers = 7,
        All = 8
    }

    /// <summary>
    /// 诊断事件面板的 ViewModel：持有过滤状态、查询缓存和结果状态，
    /// 不依赖 UnityEditor，可被 IMGUI、UI Toolkit 或其他前端复用。
    /// 绘制层只读取 <see cref="Items"/>、<see cref="StatusMessage"/> 和 <see cref="StoreRevision"/> 进行渲染。
    /// </summary>
    internal readonly struct BattleDebugDiagnosticIssueGroup
    {
        public BattleDebugDiagnosticIssueGroup(
            string key,
            string label,
            int count,
            int firstFrame,
            int latestFrame,
            int configId,
            string searchText,
            BattleDiagnosticTriggerAnalysisStage triggerStage,
            BattleDiagnosticTriggerAnalysisResult triggerResult)
        {
            Key = key ?? string.Empty;
            Label = label ?? string.Empty;
            Count = count;
            FirstFrame = firstFrame;
            LatestFrame = latestFrame;
            ConfigId = configId;
            SearchText = searchText ?? string.Empty;
            TriggerStage = triggerStage;
            TriggerResult = triggerResult;
        }

        public string Key { get; }
        public string Label { get; }
        public int Count { get; }
        public int FirstFrame { get; }
        public int LatestFrame { get; }
        public int FrameSpan =>
            FirstFrame == BattleDiagnosticFrames.Invalid ||
            LatestFrame == BattleDiagnosticFrames.Invalid
                ? 0
                : Math.Max(0, LatestFrame - FirstFrame);
        public int ConfigId { get; }
        public string SearchText { get; }
        public BattleDiagnosticTriggerAnalysisStage TriggerStage { get; }
        public BattleDiagnosticTriggerAnalysisResult TriggerResult { get; }
    }

    internal sealed class BattleDebugDiagnosticEventsViewModel
    {
        private const int DisplayLimit = 200;
        private const int IssueGroupLimit = 6;

        private long _lastRequestId;
        private long _lastStoreRevision = -1;
        private BattleDiagnosticFilter _lastWorkspaceFilter = BattleDiagnosticFilter.Default;
        private bool _lastFilterBySelectedActor;
        private BattleDiagnosticActorRelation _lastActorRelation;
        private bool _lastFailuresOnly;
        private BattleDebugDiagnosticEventScope _lastEventScope;
        private int _lastRecentFrameCount;
        private string _lastSearchText;
        private BattleDiagnosticTriggerAnalysisStage _lastTriggerStage;
        private BattleDiagnosticTriggerAnalysisResult _lastTriggerResult;
        private int _lastTriggerContextKind;
        private int _lastTriggerOriginKind;
        private int _lastConfigId;
        private long _lastRootContextId;
        private long _lastContextId;
        private long _lastSkillRuntimeId;
        private long _lastAttackId;
        private long _lastSelectedActorId;
        private bool _lastHasSelection;
        private long _worksetRevision = -1;
        private int _nextPageOffset;
        private IReadOnlyList<BattleDiagnosticEvent> _cachedItems;
        private IReadOnlyList<BattleDebugDiagnosticIssueGroup> _issueGroups;

        /// <summary>最近一次事件查询的结构化状态。</summary>
        public BattleDiagnosticQueryStatus QueryStatus { get; private set; }

        /// <summary>当前是否存在会缩小结果集的面板局部过滤条件。</summary>
        public bool HasActiveFilter =>
            FilterBySelectedActor ||
            FailuresOnly ||
            EventScope != BattleDebugDiagnosticEventScope.All ||
            RecentFrameCount > 0 ||
            HasCorrelationFocus ||
            HasTriggerAnalysisFilter ||
            ConfigId != 0 ||
            !string.IsNullOrEmpty(SearchText);

        public bool HasEffectiveFilter(in BattleDiagnosticFilter workspaceFilter)
        {
            return workspaceFilter.ActiveFilterCount > 0 || HasActiveFilter;
        }

        /// <summary>是否按选中实体 ActorId 过滤。</summary>
        public bool FilterBySelectedActor { get; set; } = true;

        /// <summary>选中 Actor 作为来源、目标或任一方参与事件。</summary>
        public BattleDiagnosticActorRelation ActorRelation { get; set; } = BattleDiagnosticActorRelation.Either;

        /// <summary>是否只显示失败事件。</summary>
        public bool FailuresOnly { get; set; }

        /// <summary>历史事件类别。</summary>
        public BattleDebugDiagnosticEventScope EventScope { get; set; } = BattleDebugDiagnosticEventScope.DamageAndEffects;

        /// <summary>相对最新事件帧保留的窗口；0 表示全部历史。</summary>
        public int RecentFrameCount { get; set; } = 600;

        /// <summary>文本搜索关键字。</summary>
        public string SearchText { get; set; } = string.Empty;

        public BattleDiagnosticTriggerAnalysisStage TriggerStage { get; set; } = BattleDiagnosticTriggerAnalysisStage.Unknown;
        public BattleDiagnosticTriggerAnalysisResult TriggerResult { get; set; } = BattleDiagnosticTriggerAnalysisResult.Unknown;
        public int TriggerContextKind { get; set; }
        public int TriggerOriginKind { get; set; }

        /// <summary>按技能、效果或触发配置 ID 收敛事件；0 表示不过滤。</summary>
        public int ConfigId { get; set; }

        public bool HasTriggerAnalysisFilter =>
            TriggerStage != BattleDiagnosticTriggerAnalysisStage.Unknown ||
            TriggerResult != BattleDiagnosticTriggerAnalysisResult.Unknown ||
            TriggerContextKind != 0 ||
            TriggerOriginKind != 0;

        public long RootContextId { get; private set; }
        public long ContextId { get; private set; }
        public long SkillRuntimeId { get; private set; }
        public long AttackId { get; private set; }
        public bool HasCorrelationFocus =>
            RootContextId != 0 || ContextId != 0 || SkillRuntimeId != 0 || AttackId != 0;
        public string CorrelationFocusLabel
        {
            get
            {
                if (RootContextId != 0) return $"Root Trace={RootContextId}";
                if (SkillRuntimeId != 0) return $"Skill Runtime={SkillRuntimeId}";
                if (AttackId != 0) return $"Attack={AttackId}";
                if (ContextId != 0) return $"Context={ContextId}";
                return string.Empty;
            }
        }

        /// <summary>当前缓存的查询结果（可能为 null 或空）。</summary>
        public IReadOnlyList<BattleDiagnosticEvent> Items => _cachedItems;

        /// <summary>当前结果中按根因归并的失败簇，按次数与最近帧排序。</summary>
        public IReadOnlyList<BattleDebugDiagnosticIssueGroup> IssueGroups => _issueGroups;

        /// <summary>最近一次查询的状态消息（空字符串表示无特殊状态）。</summary>
        public string StatusMessage { get; private set; } = string.Empty;

        /// <summary>增量加载状态；失败时保留已经加载的工作集。</summary>
        public string PagingStatusMessage { get; private set; } = string.Empty;

        public bool HasMore { get; private set; }
        public int LoadedCount => _cachedItems?.Count ?? 0;

        /// <summary>当前调查工作集绑定的固定 Store Revision。</summary>
        public long WorksetRevision => _worksetRevision;

        /// <summary>最近一次自动刷新时观察到的 Store Revision。</summary>
        public long StoreRevision => _lastStoreRevision;

        /// <summary>标记缓存失效，下次 <see cref="RefreshIfNeeded"/> 会重新查询。</summary>
        public void InvalidateCache()
        {
            _cachedItems = null;
            _issueGroups = null;
            QueryStatus = default;
            _lastStoreRevision = -1;
            _worksetRevision = -1;
            _nextPageOffset = 0;
            HasMore = false;
            PagingStatusMessage = string.Empty;
        }

        public bool FocusRelated(in BattleDiagnosticEvent diagnosticEvent)
        {
            ClearCorrelationFocus();
            if (diagnosticEvent.RootContextId != 0)
            {
                RootContextId = diagnosticEvent.RootContextId;
            }
            else if (diagnosticEvent.SkillRuntime.RuntimeId != 0)
            {
                SkillRuntimeId = diagnosticEvent.SkillRuntime.RuntimeId;
            }
            else if (diagnosticEvent.AttackId != 0)
            {
                AttackId = diagnosticEvent.AttackId;
            }
            else if (diagnosticEvent.ContextId != 0)
            {
                ContextId = diagnosticEvent.ContextId;
            }

            if (!HasCorrelationFocus) return false;

            FilterBySelectedActor = false;
            FailuresOnly = false;
            EventScope = BattleDebugDiagnosticEventScope.All;
            RecentFrameCount = 0;
            SearchText = string.Empty;
            InvalidateCache();
            return true;
        }

        public void ClearCorrelationFocus()
        {
            RootContextId = 0;
            ContextId = 0;
            SkillRuntimeId = 0;
            AttackId = 0;
            InvalidateCache();
        }

        public void ClearLocalFilters()
        {
            FilterBySelectedActor = false;
            ActorRelation = BattleDiagnosticActorRelation.Either;
            FailuresOnly = false;
            EventScope = BattleDebugDiagnosticEventScope.All;
            RecentFrameCount = 0;
            SearchText = string.Empty;
            TriggerStage = BattleDiagnosticTriggerAnalysisStage.Unknown;
            TriggerResult = BattleDiagnosticTriggerAnalysisResult.Unknown;
            TriggerContextKind = 0;
            TriggerOriginKind = 0;
            ConfigId = 0;
            RootContextId = 0;
            ContextId = 0;
            SkillRuntimeId = 0;
            AttackId = 0;
            InvalidateCache();
        }

        /// <summary>
        /// 将当前查询收敛到一个失败簇。触发类问题使用结构化字段，其他问题使用配置与摘要文本。
        /// </summary>
        public void FocusIssueGroup(in BattleDebugDiagnosticIssueGroup group)
        {
            ClearCorrelationFocus();
            FilterBySelectedActor = false;
            FailuresOnly = true;
            EventScope = BattleDebugDiagnosticEventScope.All;
            RecentFrameCount = 0;
            ConfigId = group.ConfigId;
            SearchText = group.SearchText;
            TriggerStage = group.TriggerStage;
            TriggerResult = group.TriggerResult;
            TriggerContextKind = 0;
            TriggerOriginKind = 0;
            InvalidateCache();
        }

        /// <summary>应用高频排障预设，避免手动组合多个事件筛选器。</summary>
        public void FocusRecentFailures()
        {
            ClearCorrelationFocus();
            FilterBySelectedActor = false;
            FailuresOnly = true;
            EventScope = BattleDebugDiagnosticEventScope.All;
            RecentFrameCount = 600;
            ConfigId = 0;
            SearchText = string.Empty;
            TriggerStage = BattleDiagnosticTriggerAnalysisStage.Unknown;
            TriggerResult = BattleDiagnosticTriggerAnalysisResult.Unknown;
            TriggerContextKind = 0;
            TriggerOriginKind = 0;
            InvalidateCache();
        }

        public void FocusTriggerBlocks()
        {
            FocusRecentFailures();
            EventScope = BattleDebugDiagnosticEventScope.Triggers;
            TriggerStage = BattleDiagnosticTriggerAnalysisStage.Budget;
            TriggerResult = BattleDiagnosticTriggerAnalysisResult.Blocked;
            InvalidateCache();
        }

        public void FocusConditionFailures()
        {
            FocusRecentFailures();
            EventScope = BattleDebugDiagnosticEventScope.Triggers;
            TriggerStage = BattleDiagnosticTriggerAnalysisStage.Conditions;
            TriggerResult = BattleDiagnosticTriggerAnalysisResult.Failed;
            InvalidateCache();
        }

        /// <summary>
        /// 如果缓存有效则直接返回；否则根据当前过滤条件和选中实体重新查询。
        /// </summary>
        /// <param name="session">诊断只读会话。</param>
        /// <param name="selectedActorId">当前选中实体的 ActorId（0 表示无选中）。</param>
        /// <param name="hasSelection">是否存在有效选中。</param>
        /// <returns>当前缓存的事件列表（可能为 null）。</returns>
        public IReadOnlyList<BattleDiagnosticEvent> RefreshIfNeeded(
            IBattleDiagnosticReadOnlySession session,
            long selectedActorId,
            bool hasSelection)
        {
            var workspaceFilter = BattleDiagnosticFilter.Default;
            return RefreshIfNeeded(
                session,
                selectedActorId,
                hasSelection,
                in workspaceFilter);
        }

        public IReadOnlyList<BattleDiagnosticEvent> RefreshIfNeeded(
            IBattleDiagnosticReadOnlySession session,
            long selectedActorId,
            bool hasSelection,
            in BattleDiagnosticFilter workspaceFilter)
        {
            var currentRevision = session.EventStoreRevision;
            if (_cachedItems != null &&
                _lastStoreRevision == currentRevision &&
                _lastWorkspaceFilter.Equals(workspaceFilter) &&
                _lastFilterBySelectedActor == FilterBySelectedActor &&
                _lastActorRelation == ActorRelation &&
                _lastFailuresOnly == FailuresOnly &&
                _lastEventScope == EventScope &&
                _lastRecentFrameCount == RecentFrameCount &&
                string.Equals(_lastSearchText, SearchText, StringComparison.Ordinal) &&
                _lastTriggerStage == TriggerStage &&
                _lastTriggerResult == TriggerResult &&
                _lastTriggerContextKind == TriggerContextKind &&
                _lastTriggerOriginKind == TriggerOriginKind &&
                _lastConfigId == ConfigId &&
                _lastRootContextId == RootContextId &&
                _lastContextId == ContextId &&
                _lastSkillRuntimeId == SkillRuntimeId &&
                _lastAttackId == AttackId &&
                _lastSelectedActorId == selectedActorId &&
                _lastHasSelection == hasSelection)
            {
                return _cachedItems;
            }

            _lastRequestId++;
            if (_lastRequestId <= 0) _lastRequestId = 1;

            var filter = BuildFilter(selectedActorId, hasSelection, in workspaceFilter);
            var page = new BattleDiagnosticPageRequest(currentRevision, 0, DisplayLimit);
            var query = new BattleDiagnosticEventQuery(
                _lastRequestId,
                filter,
                page,
                newestFirst: true,
                recentFrameCount: RecentFrameCount);

            var result = session.QueryEvents(query);
            QueryStatus = result.Status;
            _lastStoreRevision = currentRevision;
            _lastWorkspaceFilter = workspaceFilter;
            _lastFilterBySelectedActor = FilterBySelectedActor;
            _lastActorRelation = ActorRelation;
            _lastFailuresOnly = FailuresOnly;
            _lastEventScope = EventScope;
            _lastRecentFrameCount = RecentFrameCount;
            _lastSearchText = SearchText;
            _lastTriggerStage = TriggerStage;
            _lastTriggerResult = TriggerResult;
            _lastTriggerContextKind = TriggerContextKind;
            _lastTriggerOriginKind = TriggerOriginKind;
            _lastConfigId = ConfigId;
            _lastRootContextId = RootContextId;
            _lastContextId = ContextId;
            _lastSkillRuntimeId = SkillRuntimeId;
            _lastAttackId = AttackId;
            _lastSelectedActorId = selectedActorId;
            _lastHasSelection = hasSelection;

            _worksetRevision = result.Status.StoreRevision;
            _nextPageOffset = DisplayLimit;
            HasMore = result.Status.HasMore;
            PagingStatusMessage = string.Empty;

            if (result.Status.CanDisplayResults)
            {
                _cachedItems = result.Items;
                _issueGroups = BuildIssueGroups(result.Items);
                StatusMessage = result.Status.HasMore
                    ? $"已加载 {result.Items.Count} 条（仍有更多）"
                    : string.Empty;
            }
            else
            {
                _cachedItems = result.Items;
                _issueGroups = Array.Empty<BattleDebugDiagnosticIssueGroup>();
                StatusMessage = result.Status.Phase == BattleDiagnosticQueryPhase.Empty
                    ? BuildEmptyMessage(hasSelection)
                    : $"查询不可用：{result.Status.Availability} {result.Status.Message}";
            }

            return _cachedItems;
        }

        /// <summary>
        /// 从当前调查工作集的固定 revision 追加下一页。失败不会清除已加载结果。
        /// </summary>
        public bool LoadMore(
            IBattleDiagnosticReadOnlySession session,
            long selectedActorId,
            bool hasSelection)
        {
            var workspaceFilter = BattleDiagnosticFilter.Default;
            return LoadMore(
                session,
                selectedActorId,
                hasSelection,
                in workspaceFilter);
        }

        public bool LoadMore(
            IBattleDiagnosticReadOnlySession session,
            long selectedActorId,
            bool hasSelection,
            in BattleDiagnosticFilter workspaceFilter)
        {
            if (session == null || _cachedItems == null || !HasMore)
            {
                return false;
            }

            if (!MatchesCachedFilter(selectedActorId, hasSelection, in workspaceFilter))
            {
                PagingStatusMessage = "筛选条件已变化，请先刷新调查工作集。";
                return false;
            }

            _lastRequestId++;
            if (_lastRequestId <= 0) _lastRequestId = 1;

            var filter = BuildFilter(selectedActorId, hasSelection, in workspaceFilter);
            var page = new BattleDiagnosticPageRequest(
                _worksetRevision,
                _nextPageOffset,
                DisplayLimit);
            var query = new BattleDiagnosticEventQuery(
                _lastRequestId,
                filter,
                page,
                newestFirst: true,
                recentFrameCount: RecentFrameCount);
            var result = session.QueryEvents(query);

            if (!result.Status.CanDisplayResults)
            {
                if (result.Status.Availability == BattleDiagnosticDataAvailability.Evicted)
                {
                    HasMore = false;
                    PagingStatusMessage =
                        $"固定快照 revision {_worksetRevision} 已被淘汰；已保留 {_cachedItems.Count} 条调查结果。";
                }
                else if (result.Status.Phase == BattleDiagnosticQueryPhase.Empty)
                {
                    HasMore = false;
                    PagingStatusMessage = $"已加载全部 {_cachedItems.Count} 条调查结果。";
                }
                else
                {
                    PagingStatusMessage =
                        $"加载更多失败：{result.Status.Availability} {result.Status.Message}";
                }

                return false;
            }

            _cachedItems = AppendDistinct(_cachedItems, result.Items);
            _issueGroups = BuildIssueGroups(_cachedItems);
            _nextPageOffset += DisplayLimit;
            HasMore = result.Status.HasMore;
            StatusMessage = string.Empty;
            PagingStatusMessage = HasMore
                ? $"已加载 {_cachedItems.Count} 条，快照 revision {_worksetRevision} 仍有更多。"
                : $"已加载全部 {_cachedItems.Count} 条，快照 revision {_worksetRevision}。";
            return true;
        }

        private bool MatchesCachedFilter(
            long selectedActorId,
            bool hasSelection,
            in BattleDiagnosticFilter workspaceFilter)
        {
            return _lastWorkspaceFilter.Equals(workspaceFilter) &&
                   _lastFilterBySelectedActor == FilterBySelectedActor &&
                   _lastActorRelation == ActorRelation &&
                   _lastFailuresOnly == FailuresOnly &&
                   _lastEventScope == EventScope &&
                   _lastRecentFrameCount == RecentFrameCount &&
                   string.Equals(_lastSearchText, SearchText, StringComparison.Ordinal) &&
                   _lastTriggerStage == TriggerStage &&
                   _lastTriggerResult == TriggerResult &&
                   _lastTriggerContextKind == TriggerContextKind &&
                   _lastTriggerOriginKind == TriggerOriginKind &&
                   _lastConfigId == ConfigId &&
                   _lastRootContextId == RootContextId &&
                   _lastContextId == ContextId &&
                   _lastSkillRuntimeId == SkillRuntimeId &&
                   _lastAttackId == AttackId &&
                   _lastSelectedActorId == selectedActorId &&
                   _lastHasSelection == hasSelection;
        }

        private static IReadOnlyList<BattleDiagnosticEvent> AppendDistinct(
            IReadOnlyList<BattleDiagnosticEvent> current,
            IReadOnlyList<BattleDiagnosticEvent> additional)
        {
            if (additional == null || additional.Count == 0) return current;

            var sequences = new HashSet<long>();
            var combined = new List<BattleDiagnosticEvent>(current.Count + additional.Count);
            for (var i = 0; i < current.Count; i++)
            {
                var item = current[i];
                combined.Add(item);
                sequences.Add(item.Sequence);
            }

            for (var i = 0; i < additional.Count; i++)
            {
                var item = additional[i];
                if (sequences.Add(item.Sequence)) combined.Add(item);
            }

            return combined;
        }

        private string BuildEmptyMessage(bool hasSelection)
        {
            if (FilterBySelectedActor && !hasSelection)
            {
                return "已启用“仅选中实体”，但当前未选中 Actor。";
            }

            if (FilterBySelectedActor)
            {
                return "当前 Actor 在所选过滤条件下没有匹配事件。";
            }

            if (HasActiveFilter)
            {
                return "当前历史窗口和过滤条件下没有匹配事件。";
            }

            return "事件存储当前为空。请施放技能或检查顶部数据源中的 Event revision 是否递增。";
        }

        public BattleDiagnosticFilter BuildEffectiveFilter(
            long selectedActorId,
            bool hasSelection,
            in BattleDiagnosticFilter workspaceFilter)
        {
            return BuildFilter(selectedActorId, hasSelection, in workspaceFilter);
        }

        public BattleDiagnosticFilter BuildLocalFilter(long selectedActorId, bool hasSelection)
        {
            var workspaceFilter = BattleDiagnosticFilter.Default;
            return BuildFilter(
                selectedActorId,
                hasSelection,
                in workspaceFilter);
        }

        private BattleDiagnosticFilter BuildFilter(
            long selectedActorId,
            bool hasSelection,
            in BattleDiagnosticFilter workspaceFilter)
        {
            var shared = workspaceFilter;
            long localActorId = 0;
            var localActorRelation = BattleDiagnosticActorRelation.Any;

            if (FilterBySelectedActor && hasSelection)
            {
                localActorId = selectedActorId;
                localActorRelation = ActorRelation;
            }

            return new BattleDiagnosticFilter(
                frames: shared.Frames,
                channels: shared.Channels & ResolveChannels(EventScope),
                actorId: shared.HasActorFilter ? shared.ActorId : localActorId,
                actorRelation: shared.HasActorFilter ? shared.ActorRelation : localActorRelation,
                configId: shared.ConfigId != 0 ? shared.ConfigId : ConfigId,
                rootContextId: shared.RootContextId != 0 ? shared.RootContextId : RootContextId,
                contextId: shared.ContextId != 0 ? shared.ContextId : ContextId,
                skillRuntimeId: shared.SkillRuntimeId != 0 ? shared.SkillRuntimeId : SkillRuntimeId,
                attackId: shared.AttackId != 0 ? shared.AttackId : AttackId,
                failuresOnly: shared.FailuresOnly || FailuresOnly,
                unfinishedOnly: shared.UnfinishedOnly,
                searchText: shared.HasTextSearch ? shared.SearchText : SearchText ?? string.Empty,
                triggerStage: shared.TriggerStage != BattleDiagnosticTriggerAnalysisStage.Unknown
                    ? shared.TriggerStage
                    : TriggerStage,
                triggerResult: shared.TriggerResult != BattleDiagnosticTriggerAnalysisResult.Unknown
                    ? shared.TriggerResult
                    : TriggerResult,
                triggerContextKind: shared.TriggerContextKind != 0
                    ? shared.TriggerContextKind
                    : TriggerContextKind,
                triggerOriginKind: shared.TriggerOriginKind != 0
                    ? shared.TriggerOriginKind
                    : TriggerOriginKind);
        }


        private static IReadOnlyList<BattleDebugDiagnosticIssueGroup> BuildIssueGroups(
            IReadOnlyList<BattleDiagnosticEvent> events)
        {
            if (events == null || events.Count == 0)
            {
                return Array.Empty<BattleDebugDiagnosticIssueGroup>();
            }

            var builders = new Dictionary<string, IssueGroupBuilder>(StringComparer.Ordinal);
            for (var i = 0; i < events.Count; i++)
            {
                var diagnosticEvent = events[i];
                if (!IsIssue(in diagnosticEvent)) continue;

                var descriptor = BuildIssueDescriptor(in diagnosticEvent);
                if (!builders.TryGetValue(descriptor.Key, out var builder))
                {
                    builder = new IssueGroupBuilder(descriptor);
                    builders.Add(descriptor.Key, builder);
                }

                builder.Add(in diagnosticEvent);
            }

            var groups = new List<BattleDebugDiagnosticIssueGroup>(builders.Count);
            foreach (var pair in builders)
            {
                groups.Add(pair.Value.ToGroup());
            }

            groups.Sort((left, right) =>
            {
                var countComparison = right.Count.CompareTo(left.Count);
                if (countComparison != 0) return countComparison;
                var frameComparison = right.LatestFrame.CompareTo(left.LatestFrame);
                return frameComparison != 0
                    ? frameComparison
                    : string.CompareOrdinal(left.Label, right.Label);
            });

            if (groups.Count > IssueGroupLimit)
            {
                groups.RemoveRange(IssueGroupLimit, groups.Count - IssueGroupLimit);
            }

            return groups;
        }

        private static bool IsIssue(in BattleDiagnosticEvent diagnosticEvent)
        {
            if (diagnosticEvent.IsFailure) return true;
            return diagnosticEvent.Payload.TryGetTriggerAnalysis(out var trigger) &&
                   (trigger.Result == BattleDiagnosticTriggerAnalysisResult.Failed ||
                    trigger.Result == BattleDiagnosticTriggerAnalysisResult.Blocked);
        }

        private static IssueGroupDescriptor BuildIssueDescriptor(in BattleDiagnosticEvent diagnosticEvent)
        {
            if (diagnosticEvent.Payload.TryGetTriggerAnalysis(out var trigger))
            {
                var failureKey = trigger.FailureKey ?? string.Empty;
                var key = $"trigger|{trigger.TriggerId}|{trigger.Stage}|{trigger.Result}|{failureKey}";
                var reason = string.IsNullOrEmpty(failureKey)
                    ? trigger.Reason
                    : failureKey;
                var label = $"触发 {trigger.TriggerId}  {trigger.Stage}/{trigger.Result}";
                if (!string.IsNullOrEmpty(reason)) label += $"  {TrimLabel(reason, 42)}";
                return new IssueGroupDescriptor(
                    key,
                    label,
                    diagnosticEvent.ConfigId,
                    failureKey,
                    trigger.Stage,
                    trigger.Result);
            }

            if (diagnosticEvent.Payload.TryGetSkillFailure(out var skillFailure))
            {
                var stableCode = skillFailure.Code ?? string.Empty;
                var key = $"skill|{stableCode}|{skillFailure.Source}|{skillFailure.Stage}";
                var label = string.IsNullOrEmpty(stableCode)
                    ? $"技能失败  {skillFailure.Source}/{skillFailure.Stage}"
                    : $"技能失败  {stableCode}";
                var searchText = string.IsNullOrEmpty(stableCode)
                    ? skillFailure.Message
                    : stableCode;
                return new IssueGroupDescriptor(
                    key,
                    label,
                    diagnosticEvent.ConfigId,
                    searchText,
                    BattleDiagnosticTriggerAnalysisStage.Unknown,
                    BattleDiagnosticTriggerAnalysisResult.Unknown);
            }

            var summary = diagnosticEvent.Summary ?? string.Empty;
            var genericKey = $"event|{diagnosticEvent.Kind}|{diagnosticEvent.ConfigId}|{summary}";
            var genericLabel = diagnosticEvent.ConfigId != 0
                ? $"{diagnosticEvent.Kind}  cfg={diagnosticEvent.ConfigId}"
                : diagnosticEvent.Kind.ToString();
            if (!string.IsNullOrEmpty(summary)) genericLabel += $"  {TrimLabel(summary, 42)}";
            return new IssueGroupDescriptor(
                genericKey,
                genericLabel,
                diagnosticEvent.ConfigId,
                summary,
                BattleDiagnosticTriggerAnalysisStage.Unknown,
                BattleDiagnosticTriggerAnalysisResult.Unknown);
        }

        private static string TrimLabel(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= maxLength) return value ?? string.Empty;
            return value.Substring(0, maxLength - 3) + "...";
        }

        private readonly struct IssueGroupDescriptor
        {
            public IssueGroupDescriptor(
                string key,
                string label,
                int configId,
                string searchText,
                BattleDiagnosticTriggerAnalysisStage triggerStage,
                BattleDiagnosticTriggerAnalysisResult triggerResult)
            {
                Key = key;
                Label = label;
                ConfigId = configId;
                SearchText = searchText;
                TriggerStage = triggerStage;
                TriggerResult = triggerResult;
            }

            public string Key { get; }
            public string Label { get; }
            public int ConfigId { get; }
            public string SearchText { get; }
            public BattleDiagnosticTriggerAnalysisStage TriggerStage { get; }
            public BattleDiagnosticTriggerAnalysisResult TriggerResult { get; }
        }

        private sealed class IssueGroupBuilder
        {
            private readonly IssueGroupDescriptor _descriptor;
            private int _count;
            private int _firstFrame = BattleDiagnosticFrames.Invalid;
            private int _latestFrame = BattleDiagnosticFrames.Invalid;

            public IssueGroupBuilder(in IssueGroupDescriptor descriptor)
            {
                _descriptor = descriptor;
            }

            public void Add(in BattleDiagnosticEvent diagnosticEvent)
            {
                _count++;
                if (_firstFrame == BattleDiagnosticFrames.Invalid ||
                    diagnosticEvent.Frame < _firstFrame)
                {
                    _firstFrame = diagnosticEvent.Frame;
                }

                if (diagnosticEvent.Frame > _latestFrame) _latestFrame = diagnosticEvent.Frame;
            }

            public BattleDebugDiagnosticIssueGroup ToGroup()
            {
                return new BattleDebugDiagnosticIssueGroup(
                    _descriptor.Key,
                    _descriptor.Label,
                    _count,
                    _firstFrame,
                    _latestFrame,
                    _descriptor.ConfigId,
                    _descriptor.SearchText,
                    _descriptor.TriggerStage,
                    _descriptor.TriggerResult);
            }
        }

        private static BattleDiagnosticEventChannel ResolveChannels(BattleDebugDiagnosticEventScope scope)
        {
            switch (scope)
            {
                case BattleDebugDiagnosticEventScope.DamageAndEffects:
                    return BattleDiagnosticEventChannel.DamageAndHeal | BattleDiagnosticEventChannel.Effect;
                case BattleDebugDiagnosticEventScope.DamageAndHeal:
                    return BattleDiagnosticEventChannel.DamageAndHeal;
                case BattleDebugDiagnosticEventScope.Effects:
                    return BattleDiagnosticEventChannel.Effect;
                case BattleDebugDiagnosticEventScope.Skills:
                    return BattleDiagnosticEventChannel.Skill;
                case BattleDebugDiagnosticEventScope.Buffs:
                    return BattleDiagnosticEventChannel.Buff;
                case BattleDebugDiagnosticEventScope.TemporaryEntities:
                    return BattleDiagnosticEventChannel.TemporaryEntity;
                case BattleDebugDiagnosticEventScope.Warnings:
                    return BattleDiagnosticEventChannel.WarningAndException;
                case BattleDebugDiagnosticEventScope.Triggers:
                    return BattleDiagnosticEventChannel.Trigger;
                default:
                    return BattleDiagnosticEventChannel.All;
            }
        }
    }
}
