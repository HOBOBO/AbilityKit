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
        All = 8,
        Targeting = 9,
        Input = 10
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

    internal readonly struct BattleDebugTriggerStageSummary
    {
        public BattleDebugTriggerStageSummary(
            BattleDiagnosticTriggerAnalysisStage stage,
            int eventCount,
            int occurrenceCount,
            int passedCount,
            int failedCount,
            int blockedCount,
            int skippedCount,
            int firstFrame,
            int lastFrame,
            BattleDiagnosticTriggerAnalysisResult latestResult,
            long latestSequence,
            long latestContextId,
            string failureKey,
            string reason)
        {
            Stage = stage;
            EventCount = eventCount;
            OccurrenceCount = occurrenceCount;
            PassedCount = passedCount;
            FailedCount = failedCount;
            BlockedCount = blockedCount;
            SkippedCount = skippedCount;
            FirstFrame = firstFrame;
            LastFrame = lastFrame;
            LatestResult = latestResult;
            LatestSequence = latestSequence;
            LatestContextId = latestContextId;
            FailureKey = failureKey ?? string.Empty;
            Reason = reason ?? string.Empty;
        }

        public BattleDiagnosticTriggerAnalysisStage Stage { get; }
        public int EventCount { get; }
        public int OccurrenceCount { get; }
        public int PassedCount { get; }
        public int FailedCount { get; }
        public int BlockedCount { get; }
        public int SkippedCount { get; }
        public int FirstFrame { get; }
        public int LastFrame { get; }
        public BattleDiagnosticTriggerAnalysisResult LatestResult { get; }
        public long LatestSequence { get; }
        public long LatestContextId { get; }
        public string FailureKey { get; }
        public string Reason { get; }
        public bool IsObserved => EventCount > 0;
    }

    internal readonly struct BattleDebugTriggerFlow
    {
        public BattleDebugTriggerFlow(
            string key,
            int triggerId,
            long rootContextId,
            long firstRootContextId,
            long lastRootContextId,
            long sourceActorId,
            long targetActorId,
            int firstFrame,
            int lastFrame,
            long latestSequence,
            long latestContextId,
            BattleDebugTriggerStageSummary budget,
            BattleDebugTriggerStageSummary conditions,
            BattleDebugTriggerStageSummary plan,
            BattleDebugTriggerStageSummary execution)
        {
            Key = key ?? string.Empty;
            TriggerId = triggerId;
            RootContextId = rootContextId;
            FirstRootContextId = firstRootContextId;
            LastRootContextId = lastRootContextId;
            SourceActorId = sourceActorId;
            TargetActorId = targetActorId;
            FirstFrame = firstFrame;
            LastFrame = lastFrame;
            LatestSequence = latestSequence;
            LatestContextId = latestContextId;
            Budget = budget;
            Conditions = conditions;
            Plan = plan;
            Execution = execution;
        }

        public string Key { get; }
        public int TriggerId { get; }
        public long RootContextId { get; }
        public long FirstRootContextId { get; }
        public long LastRootContextId { get; }
        public bool SpansMultipleRoots =>
            FirstRootContextId != 0 &&
            LastRootContextId != 0 &&
            FirstRootContextId != LastRootContextId;
        public long SourceActorId { get; }
        public long TargetActorId { get; }
        public int FirstFrame { get; }
        public int LastFrame { get; }
        public long LatestSequence { get; }
        public long LatestContextId { get; }
        public BattleDebugTriggerStageSummary Budget { get; }
        public BattleDebugTriggerStageSummary Conditions { get; }
        public BattleDebugTriggerStageSummary Plan { get; }
        public BattleDebugTriggerStageSummary Execution { get; }

        public BattleDebugTriggerStageSummary GetStage(BattleDiagnosticTriggerAnalysisStage stage)
        {
            switch (stage)
            {
                case BattleDiagnosticTriggerAnalysisStage.Budget:
                    return Budget;
                case BattleDiagnosticTriggerAnalysisStage.Conditions:
                    return Conditions;
                case BattleDiagnosticTriggerAnalysisStage.Plan:
                    return Plan;
                case BattleDiagnosticTriggerAnalysisStage.Execution:
                    return Execution;
                default:
                    return default;
            }
        }
    }

    internal enum BattleDebugSkillCastPhase
    {
        InputToTarget = 1,
        TargetToCast = 2,
        PreCast = 3,
        Cast = 4,
        Economy = 5,
        WaitingChildren = 6,
        RuntimeTail = 7,
        Total = 8
    }

    internal readonly struct BattleDebugSkillCastPhaseTiming
    {
        public BattleDebugSkillCastPhaseTiming(
            BattleDebugSkillCastPhase phase,
            int startFrame,
            int endFrame)
        {
            Phase = phase;
            StartFrame = startFrame;
            EndFrame = endFrame;
        }

        public BattleDebugSkillCastPhase Phase { get; }
        public int StartFrame { get; }
        public int EndFrame { get; }
        public int DurationFrames => Math.Max(0, EndFrame - StartFrame);
        public bool IsObserved =>
            StartFrame != BattleDiagnosticFrames.Invalid &&
            EndFrame != BattleDiagnosticFrames.Invalid;
    }

    internal readonly struct BattleDebugSkillCastDeviation
    {
        public BattleDebugSkillCastDeviation(
            string castKey,
            BattleDebugSkillCastPhase phase,
            int actualFrames,
            int baselineFrames)
        {
            CastKey = castKey ?? string.Empty;
            Phase = phase;
            ActualFrames = actualFrames;
            BaselineFrames = baselineFrames;
        }

        public string CastKey { get; }
        public BattleDebugSkillCastPhase Phase { get; }
        public int ActualFrames { get; }
        public int BaselineFrames { get; }
        public int ExcessFrames => Math.Max(0, ActualFrames - BaselineFrames);
    }

    internal readonly struct BattleDebugSkillCastComparison
    {
        public BattleDebugSkillCastComparison(
            string key,
            int skillId,
            int skillSlot,
            int skillLevel,
            int castCount,
            int baselineSampleCount,
            int successCount,
            int problemCount,
            int medianTotalFrames,
            IReadOnlyList<BattleDebugSkillCastDeviation> deviations)
        {
            Key = key ?? string.Empty;
            SkillId = skillId;
            SkillSlot = skillSlot;
            SkillLevel = skillLevel;
            CastCount = castCount;
            BaselineSampleCount = baselineSampleCount;
            SuccessCount = successCount;
            ProblemCount = problemCount;
            MedianTotalFrames = medianTotalFrames;
            Deviations = deviations ?? Array.Empty<BattleDebugSkillCastDeviation>();
        }

        public string Key { get; }
        public int SkillId { get; }
        public int SkillSlot { get; }
        public int SkillLevel { get; }
        public int CastCount { get; }
        public int BaselineSampleCount { get; }
        public bool HasReliableBaseline => BaselineSampleCount >= 2;
        public int SuccessCount { get; }
        public int ProblemCount { get; }
        public int MedianTotalFrames { get; }
        public IReadOnlyList<BattleDebugSkillCastDeviation> Deviations { get; }
    }

    internal readonly struct BattleDebugSkillCastFlow
    {
        public BattleDebugSkillCastFlow(
            string key,
            long commandId,
            long rootContextId,
            BattleDiagnosticRuntimeHandle skillRuntime,
            int skillId,
            int skillSlot,
            int skillLevel,
            int castSequence,
            long sourceActorId,
            long targetActorId,
            int firstFrame,
            int lastFrame,
            long latestSequence,
            BattleDiagnosticEventOutcome outcome,
            int inputCount,
            int targetSearchCount,
            int skillStageCount,
            int economyCount,
            int consequenceCount,
            IReadOnlyList<BattleDebugSkillCastPhaseTiming> timings,
            IReadOnlyList<BattleDiagnosticEvent> events)
        {
            Key = key ?? string.Empty;
            CommandId = commandId;
            RootContextId = rootContextId;
            SkillRuntime = skillRuntime;
            SkillId = skillId;
            SkillSlot = skillSlot;
            SkillLevel = skillLevel;
            CastSequence = castSequence;
            SourceActorId = sourceActorId;
            TargetActorId = targetActorId;
            FirstFrame = firstFrame;
            LastFrame = lastFrame;
            LatestSequence = latestSequence;
            Outcome = outcome;
            InputCount = inputCount;
            TargetSearchCount = targetSearchCount;
            SkillStageCount = skillStageCount;
            EconomyCount = economyCount;
            ConsequenceCount = consequenceCount;
            Timings = timings ?? Array.Empty<BattleDebugSkillCastPhaseTiming>();
            Events = events ?? Array.Empty<BattleDiagnosticEvent>();
        }

        public string Key { get; }
        public long CommandId { get; }
        public long RootContextId { get; }
        public BattleDiagnosticRuntimeHandle SkillRuntime { get; }
        public int SkillId { get; }
        public int SkillSlot { get; }
        public int SkillLevel { get; }
        public int CastSequence { get; }
        public long SourceActorId { get; }
        public long TargetActorId { get; }
        public int FirstFrame { get; }
        public int LastFrame { get; }
        public int DurationFrames => Math.Max(0, LastFrame - FirstFrame);
        public long LatestSequence { get; }
        public BattleDiagnosticEventOutcome Outcome { get; }
        public bool IsComplete => Outcome != BattleDiagnosticEventOutcome.None;
        public int InputCount { get; }
        public int TargetSearchCount { get; }
        public int SkillStageCount { get; }
        public int EconomyCount { get; }
        public int ConsequenceCount { get; }
        public IReadOnlyList<BattleDebugSkillCastPhaseTiming> Timings { get; }
        public IReadOnlyList<BattleDiagnosticEvent> Events { get; }
        public int EventCount => Events.Count;

        public bool TryGetTiming(
            BattleDebugSkillCastPhase phase,
            out BattleDebugSkillCastPhaseTiming timing)
        {
            for (var i = 0; i < Timings.Count; i++)
            {
                if (Timings[i].Phase != phase) continue;
                timing = Timings[i];
                return timing.IsObserved;
            }
            timing = default;
            return false;
        }
    }

    internal sealed class BattleDebugDiagnosticEventsViewModel
    {
        private const int DisplayLimit = 200;
        private const int IssueGroupLimit = 6;
        private const int TriggerFlowLimit = 12;
        private const int SkillCastFlowLimit = 20;

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
        private BattleDiagnosticTriggerValueFilter _lastTriggerValueFilter;
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
        private IReadOnlyList<BattleDebugTriggerFlow> _triggerFlows;
        private IReadOnlyList<BattleDebugSkillCastFlow> _skillCastFlows;
        private IReadOnlyList<BattleDebugSkillCastComparison> _skillCastComparisons;

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
        public BattleDiagnosticTriggerValueFilter TriggerValueFilter { get; set; } =
            BattleDiagnosticTriggerValueFilter.Valuable;

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
                if (RootContextId != 0) return $"根 Trace={RootContextId}";
                if (SkillRuntimeId != 0) return $"技能运行时={SkillRuntimeId}";
                if (AttackId != 0) return $"攻击={AttackId}";
                if (ContextId != 0) return $"上下文={ContextId}";
                return string.Empty;
            }
        }

        /// <summary>当前缓存的查询结果（可能为 null 或空）。</summary>
        public IReadOnlyList<BattleDiagnosticEvent> Items => _cachedItems;

        /// <summary>当前结果中按根因归并的失败簇，按次数与最近帧排序。</summary>
        public IReadOnlyList<BattleDebugDiagnosticIssueGroup> IssueGroups => _issueGroups;

        /// <summary>当前工作集中按 Root 和 Trigger 归并的触发阶段流程。</summary>
        public IReadOnlyList<BattleDebugTriggerFlow> TriggerFlows => _triggerFlows;

        /// <summary>当前工作集中按命令、Trace 和技能运行时关联的施法链。</summary>
        public IReadOnlyList<BattleDebugSkillCastFlow> SkillCastFlows => _skillCastFlows;

        public IReadOnlyList<BattleDebugSkillCastComparison> SkillCastComparisons =>
            _skillCastComparisons;

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
            _triggerFlows = null;
            _skillCastFlows = null;
            _skillCastComparisons = null;
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
            TriggerValueFilter = BattleDiagnosticTriggerValueFilter.Valuable;
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
            TriggerValueFilter = BattleDiagnosticTriggerValueFilter.All;
            InvalidateCache();
        }

        public void FocusTriggerFlows()
        {
            FailuresOnly = false;
            EventScope = BattleDebugDiagnosticEventScope.Triggers;
            TriggerStage = BattleDiagnosticTriggerAnalysisStage.Unknown;
            TriggerResult = BattleDiagnosticTriggerAnalysisResult.Unknown;
            TriggerValueFilter = BattleDiagnosticTriggerValueFilter.Valuable;
            InvalidateCache();
        }

        public void FocusSkillCasts()
        {
            FailuresOnly = false;
            EventScope = BattleDebugDiagnosticEventScope.All;
            TriggerStage = BattleDiagnosticTriggerAnalysisStage.Unknown;
            TriggerResult = BattleDiagnosticTriggerAnalysisResult.Unknown;
            TriggerContextKind = 0;
            TriggerOriginKind = 0;
            TriggerValueFilter = BattleDiagnosticTriggerValueFilter.All;
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
                _lastTriggerValueFilter == TriggerValueFilter &&
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
            _lastTriggerValueFilter = TriggerValueFilter;
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
                _triggerFlows = BuildTriggerFlows(result.Items);
                _skillCastFlows = BuildSkillCastFlows(result.Items);
                _skillCastComparisons = BuildSkillCastComparisons(_skillCastFlows);
                StatusMessage = result.Status.HasMore
                    ? $"已加载 {result.Items.Count} 条（仍有更多）"
                    : string.Empty;
            }
            else
            {
                _cachedItems = result.Items;
                _issueGroups = Array.Empty<BattleDebugDiagnosticIssueGroup>();
                _triggerFlows = Array.Empty<BattleDebugTriggerFlow>();
                _skillCastFlows = Array.Empty<BattleDebugSkillCastFlow>();
                _skillCastComparisons = Array.Empty<BattleDebugSkillCastComparison>();
                StatusMessage = result.Status.Phase == BattleDiagnosticQueryPhase.Empty
                    ? BuildEmptyMessage(hasSelection)
                    : $"查询不可用：{BattleDebugDisplayText.Availability(result.Status.Availability)} {result.Status.Message}";
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
                        $"固定快照版本 {_worksetRevision} 已被淘汰；已保留 {_cachedItems.Count} 条调查结果。";
                }
                else if (result.Status.Phase == BattleDiagnosticQueryPhase.Empty)
                {
                    HasMore = false;
                    PagingStatusMessage = $"已加载全部 {_cachedItems.Count} 条调查结果。";
                }
                else
                {
                    PagingStatusMessage =
                        $"加载更多失败：{BattleDebugDisplayText.Availability(result.Status.Availability)} {result.Status.Message}";
                }

                return false;
            }

            _cachedItems = AppendDistinct(_cachedItems, result.Items);
            _issueGroups = BuildIssueGroups(_cachedItems);
            _triggerFlows = BuildTriggerFlows(_cachedItems);
            _skillCastFlows = BuildSkillCastFlows(_cachedItems);
            _skillCastComparisons = BuildSkillCastComparisons(_skillCastFlows);
            _nextPageOffset += DisplayLimit;
            HasMore = result.Status.HasMore;
            StatusMessage = string.Empty;
            PagingStatusMessage = HasMore
                ? $"已加载 {_cachedItems.Count} 条，快照版本 {_worksetRevision} 仍有更多。"
                : $"已加载全部 {_cachedItems.Count} 条，快照版本 {_worksetRevision}。";
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
                   _lastTriggerValueFilter == TriggerValueFilter &&
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

            return "事件存储当前为空。请施放技能或检查顶部数据源中的事件版本是否递增。";
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
                    : TriggerOriginKind,
                triggerValue: shared.TriggerValue != BattleDiagnosticTriggerValueFilter.All
                    ? shared.TriggerValue
                    : TriggerValueFilter);
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

                if (diagnosticEvent.Payload.TryGetTriggerAnalysisAggregate(out var aggregate))
                {
                    builder.Add(
                        aggregate.OccurrenceCount,
                        aggregate.FirstFrame,
                        aggregate.LastFrame);
                }
                else
                {
                    builder.Add(in diagnosticEvent);
                }
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

        internal static IReadOnlyList<BattleDebugTriggerFlow> BuildTriggerFlows(
            IReadOnlyList<BattleDiagnosticEvent> events)
        {
            if (events == null || events.Count == 0)
            {
                return Array.Empty<BattleDebugTriggerFlow>();
            }

            var builders = new Dictionary<string, TriggerFlowBuilder>(StringComparer.Ordinal);
            for (var i = 0; i < events.Count; i++)
            {
                var diagnosticEvent = events[i];
                if (diagnosticEvent.Payload.TryGetTriggerAnalysis(out var trigger))
                {
                    var rootContextId = diagnosticEvent.RootContextId;
                    var key = BuildTriggerFlowKey(
                        trigger.TriggerId,
                        rootContextId,
                        diagnosticEvent.ContextId,
                        diagnosticEvent.Sequence,
                        spansMultipleRoots: false);
                    if (!builders.TryGetValue(key, out var builder))
                    {
                        builder = new TriggerFlowBuilder(
                            key,
                            trigger.TriggerId,
                            rootContextId,
                            rootContextId,
                            rootContextId,
                            diagnosticEvent.SourceActorId,
                            diagnosticEvent.TargetActorId);
                        builders.Add(key, builder);
                    }

                    builder.Add(
                        trigger.Stage,
                        trigger.Result,
                        occurrenceCount: 1,
                        diagnosticEvent.Frame,
                        diagnosticEvent.Frame,
                        diagnosticEvent.Sequence,
                        diagnosticEvent.ContextId,
                        trigger.FailureKey,
                        trigger.Reason);
                    continue;
                }

                if (!diagnosticEvent.Payload.TryGetTriggerAnalysisAggregate(out var aggregate))
                {
                    continue;
                }

                var spansMultipleRoots = aggregate.FirstRootContextId != 0 &&
                                         aggregate.LastRootContextId != 0 &&
                                         aggregate.FirstRootContextId != aggregate.LastRootContextId;
                var aggregateRootContextId = spansMultipleRoots
                    ? 0L
                    : aggregate.LastRootContextId != 0
                        ? aggregate.LastRootContextId
                        : aggregate.FirstRootContextId != 0
                            ? aggregate.FirstRootContextId
                            : diagnosticEvent.RootContextId;
                var aggregateKey = BuildTriggerFlowKey(
                    aggregate.TriggerId,
                    aggregateRootContextId,
                    aggregate.LastContextId,
                    diagnosticEvent.Sequence,
                    spansMultipleRoots);
                if (!builders.TryGetValue(aggregateKey, out var aggregateBuilder))
                {
                    aggregateBuilder = new TriggerFlowBuilder(
                        aggregateKey,
                        aggregate.TriggerId,
                        aggregateRootContextId,
                        aggregate.FirstRootContextId,
                        aggregate.LastRootContextId,
                        diagnosticEvent.SourceActorId,
                        diagnosticEvent.TargetActorId);
                    builders.Add(aggregateKey, aggregateBuilder);
                }

                aggregateBuilder.Add(
                    aggregate.Stage,
                    aggregate.Result,
                    aggregate.OccurrenceCount,
                    aggregate.FirstFrame,
                    aggregate.LastFrame,
                    diagnosticEvent.Sequence,
                    aggregate.LastContextId,
                    aggregate.FailureKey,
                    aggregate.SampleReason);
            }

            if (builders.Count == 0) return Array.Empty<BattleDebugTriggerFlow>();

            var flows = new List<BattleDebugTriggerFlow>(builders.Count);
            foreach (var pair in builders)
            {
                flows.Add(pair.Value.Build());
            }

            flows.Sort((left, right) =>
            {
                var frameComparison = right.LastFrame.CompareTo(left.LastFrame);
                return frameComparison != 0
                    ? frameComparison
                    : right.LatestSequence.CompareTo(left.LatestSequence);
            });
            if (flows.Count > TriggerFlowLimit)
            {
                flows.RemoveRange(TriggerFlowLimit, flows.Count - TriggerFlowLimit);
            }

            return flows;
        }

        internal static IReadOnlyList<BattleDebugSkillCastFlow> BuildSkillCastFlows(
            IReadOnlyList<BattleDiagnosticEvent> events)
        {
            if (events == null || events.Count == 0)
                return Array.Empty<BattleDebugSkillCastFlow>();

            var links = new SkillCastLinkSet(events.Count);
            var commandOwners = new Dictionary<long, int>();
            var rootOwners = new Dictionary<long, int>();
            var runtimeOwners = new Dictionary<BattleDiagnosticRuntimeHandle, int>();
            var correlated = new bool[events.Count];

            for (var i = 0; i < events.Count; i++)
            {
                var item = events[i];
                var commandId = GetDiagnosticCommandId(in item);
                if (commandId != 0L)
                {
                    correlated[i] = true;
                    LinkSkillCast(commandOwners, commandId, i, links);
                }
                if (item.RootContextId != 0L)
                {
                    correlated[i] = true;
                    LinkSkillCast(rootOwners, item.RootContextId, i, links);
                }
                if (item.SkillRuntime.IsValid)
                {
                    correlated[i] = true;
                    LinkSkillCast(runtimeOwners, item.SkillRuntime, i, links);
                }
            }

            var builders = new Dictionary<int, SkillCastFlowBuilder>();
            for (var i = 0; i < events.Count; i++)
            {
                if (!correlated[i]) continue;
                var root = links.Find(i);
                if (!builders.TryGetValue(root, out var builder))
                {
                    builder = new SkillCastFlowBuilder();
                    builders.Add(root, builder);
                }
                var item = events[i];
                builder.Add(in item);
            }

            var flows = new List<BattleDebugSkillCastFlow>(builders.Count);
            foreach (var pair in builders)
            {
                if (pair.Value.HasSkillAnchor) flows.Add(pair.Value.Build());
            }
            flows.Sort((left, right) =>
            {
                var frameComparison = right.LastFrame.CompareTo(left.LastFrame);
                return frameComparison != 0
                    ? frameComparison
                    : right.LatestSequence.CompareTo(left.LatestSequence);
            });
            if (flows.Count > SkillCastFlowLimit)
                flows.RemoveRange(SkillCastFlowLimit, flows.Count - SkillCastFlowLimit);
            return flows;
        }

        internal static IReadOnlyList<BattleDebugSkillCastComparison> BuildSkillCastComparisons(
            IReadOnlyList<BattleDebugSkillCastFlow> casts)
        {
            if (casts == null || casts.Count == 0)
                return Array.Empty<BattleDebugSkillCastComparison>();

            var builders = new Dictionary<string, SkillCastComparisonBuilder>(StringComparer.Ordinal);
            for (var i = 0; i < casts.Count; i++)
            {
                var cast = casts[i];
                var key = cast.SkillId != 0
                    ? $"skill|{cast.SkillId}|level|{cast.SkillLevel}"
                    : $"slot|{cast.SkillSlot}|level|{cast.SkillLevel}";
                if (!builders.TryGetValue(key, out var builder))
                {
                    builder = new SkillCastComparisonBuilder(
                        key,
                        cast.SkillId,
                        cast.SkillSlot,
                        cast.SkillLevel);
                    builders.Add(key, builder);
                }
                builder.Add(in cast);
            }

            var comparisons = new List<BattleDebugSkillCastComparison>(builders.Count);
            foreach (var pair in builders) comparisons.Add(pair.Value.Build());
            comparisons.Sort((left, right) =>
            {
                var deviationComparison = right.Deviations.Count.CompareTo(left.Deviations.Count);
                if (deviationComparison != 0) return deviationComparison;
                var problemComparison = right.ProblemCount.CompareTo(left.ProblemCount);
                if (problemComparison != 0) return problemComparison;
                return right.CastCount.CompareTo(left.CastCount);
            });
            return comparisons;
        }

        private sealed class SkillCastComparisonBuilder
        {
            private readonly string _key;
            private readonly int _skillId;
            private readonly int _skillSlot;
            private readonly int _skillLevel;
            private readonly List<BattleDebugSkillCastFlow> _casts =
                new List<BattleDebugSkillCastFlow>();

            public SkillCastComparisonBuilder(
                string key,
                int skillId,
                int skillSlot,
                int skillLevel)
            {
                _key = key;
                _skillId = skillId;
                _skillSlot = skillSlot;
                _skillLevel = skillLevel;
            }

            public void Add(in BattleDebugSkillCastFlow cast)
            {
                _casts.Add(cast);
            }

            public BattleDebugSkillCastComparison Build()
            {
                var successCount = 0;
                var problemCount = 0;
                var baselineValues = new Dictionary<BattleDebugSkillCastPhase, List<int>>();
                for (var i = 0; i < _casts.Count; i++)
                {
                    var cast = _casts[i];
                    if (cast.Outcome == BattleDiagnosticEventOutcome.Succeeded)
                    {
                        successCount++;
                        for (var timingIndex = 0; timingIndex < cast.Timings.Count; timingIndex++)
                        {
                            var timing = cast.Timings[timingIndex];
                            if (!timing.IsObserved) continue;
                            if (!baselineValues.TryGetValue(timing.Phase, out var values))
                            {
                                values = new List<int>();
                                baselineValues.Add(timing.Phase, values);
                            }
                            values.Add(timing.DurationFrames);
                        }
                    }
                    else
                    {
                        problemCount++;
                    }
                }

                var baselines = new Dictionary<BattleDebugSkillCastPhase, int>();
                foreach (var pair in baselineValues)
                {
                    if (pair.Value.Count < 2) continue;
                    baselines.Add(pair.Key, Median(pair.Value));
                }

                var deviations = new List<BattleDebugSkillCastDeviation>();
                for (var i = 0; i < _casts.Count; i++)
                {
                    var cast = _casts[i];
                    for (var timingIndex = 0; timingIndex < cast.Timings.Count; timingIndex++)
                    {
                        var timing = cast.Timings[timingIndex];
                        if (!baselines.TryGetValue(timing.Phase, out var baseline)) continue;
                        var threshold = baseline + Math.Max(
                            2,
                            (int)Math.Ceiling(Math.Max(1, baseline) * 0.5d));
                        if (timing.DurationFrames <= threshold) continue;
                        deviations.Add(new BattleDebugSkillCastDeviation(
                            cast.Key,
                            timing.Phase,
                            timing.DurationFrames,
                            baseline));
                    }
                }
                deviations.Sort((left, right) =>
                {
                    var excessComparison = right.ExcessFrames.CompareTo(left.ExcessFrames);
                    return excessComparison != 0
                        ? excessComparison
                        : right.ActualFrames.CompareTo(left.ActualFrames);
                });

                var baselineSampleCount = baselineValues.TryGetValue(
                    BattleDebugSkillCastPhase.Total,
                    out var totalValues)
                    ? totalValues.Count
                    : 0;
                var medianTotal = baselines.TryGetValue(
                    BattleDebugSkillCastPhase.Total,
                    out var totalMedian)
                    ? totalMedian
                    : baselineSampleCount == 1
                        ? totalValues[0]
                        : 0;
                return new BattleDebugSkillCastComparison(
                    _key,
                    _skillId,
                    _skillSlot,
                    _skillLevel,
                    _casts.Count,
                    baselineSampleCount,
                    successCount,
                    problemCount,
                    medianTotal,
                    deviations);
            }

            private static int Median(List<int> values)
            {
                values.Sort();
                var middle = values.Count / 2;
                return values.Count % 2 != 0
                    ? values[middle]
                    : (values[middle - 1] + values[middle]) / 2;
            }
        }

        private static void LinkSkillCast<TKey>(
            Dictionary<TKey, int> owners,
            TKey key,
            int eventIndex,
            SkillCastLinkSet links)
        {
            if (owners.TryGetValue(key, out var owner))
                links.Union(eventIndex, owner);
            else
                owners.Add(key, eventIndex);
        }

        private static long GetDiagnosticCommandId(in BattleDiagnosticEvent item)
        {
            if (item.Payload.TryGetSkillExecution(out var execution)) return execution.CommandId;
            if (item.Payload.TryGetInputCommand(out var input)) return input.CommandId;
            if (item.Payload.TryGetTargetSearch(out var search)) return search.CommandId;
            if (item.Payload.TryGetSkillFailure(out var failure)) return failure.CommandId;
            return 0L;
        }

        private sealed class SkillCastLinkSet
        {
            private readonly int[] _parents;

            public SkillCastLinkSet(int count)
            {
                _parents = new int[count];
                for (var i = 0; i < count; i++) _parents[i] = i;
            }

            public int Find(int value)
            {
                var root = value;
                while (_parents[root] != root) root = _parents[root];
                while (_parents[value] != value)
                {
                    var parent = _parents[value];
                    _parents[value] = root;
                    value = parent;
                }
                return root;
            }

            public void Union(int left, int right)
            {
                var leftRoot = Find(left);
                var rightRoot = Find(right);
                if (leftRoot != rightRoot) _parents[rightRoot] = leftRoot;
            }
        }

        private sealed class SkillCastFlowBuilder
        {
            private readonly List<BattleDiagnosticEvent> _events = new List<BattleDiagnosticEvent>();
            private long _commandId;
            private long _rootContextId;
            private BattleDiagnosticRuntimeHandle _skillRuntime;
            private int _skillId;
            private int _skillSlot;
            private int _skillLevel;
            private int _castSequence;
            private long _sourceActorId;
            private long _targetActorId;
            private int _firstFrame = BattleDiagnosticFrames.Invalid;
            private int _lastFrame = BattleDiagnosticFrames.Invalid;
            private long _latestSequence;
            private BattleDiagnosticEventOutcome _outcome;
            private int _inputCount;
            private int _targetSearchCount;
            private int _skillStageCount;
            private int _economyCount;
            private int _consequenceCount;

            public bool HasSkillAnchor { get; private set; }

            public void Add(in BattleDiagnosticEvent item)
            {
                _events.Add(item);
                var commandId = GetDiagnosticCommandId(in item);
                if (_commandId == 0L && commandId != 0L) _commandId = commandId;
                if (_rootContextId == 0L && item.RootContextId != 0L) _rootContextId = item.RootContextId;
                if (!_skillRuntime.IsValid && item.SkillRuntime.IsValid) _skillRuntime = item.SkillRuntime;
                if (_sourceActorId == 0L && item.SourceActorId != 0L) _sourceActorId = item.SourceActorId;
                if (_targetActorId == 0L && item.TargetActorId != 0L) _targetActorId = item.TargetActorId;

                if (item.Payload.TryGetSkillExecution(out var execution))
                {
                    HasSkillAnchor = true;
                    if (_skillSlot == 0) _skillSlot = execution.SkillSlot;
                    if (_skillLevel == 0) _skillLevel = execution.SkillLevel;
                    if (_castSequence == 0) _castSequence = execution.CastSequence;
                    if (_skillId == 0 && item.ConfigId != 0) _skillId = item.ConfigId;
                    if (item.Kind == BattleDiagnosticEventKind.SkillEconomy) _economyCount++;
                    else _skillStageCount++;
                    if (IsTerminalSkillStage(execution.Stage)) UpdateOutcome(item.Outcome);
                }
                else if (item.Payload.TryGetSkillFailure(out var failure))
                {
                    HasSkillAnchor = true;
                    if (_skillSlot == 0) _skillSlot = failure.Slot;
                    if (_skillId == 0 && item.ConfigId != 0) _skillId = item.ConfigId;
                    _skillStageCount++;
                    UpdateOutcome(item.Outcome == BattleDiagnosticEventOutcome.None
                        ? BattleDiagnosticEventOutcome.Failed
                        : item.Outcome);
                }
                else
                {
                    switch (item.Kind)
                    {
                        case BattleDiagnosticEventKind.InputCommand:
                            _inputCount++;
                            if (item.Payload.TryGetInputCommand(out var input) && input.SkillSlot > 0)
                            {
                                HasSkillAnchor = true;
                                if (_skillSlot == 0) _skillSlot = input.SkillSlot;
                            }
                            break;
                        case BattleDiagnosticEventKind.TargetSearch:
                            _targetSearchCount++;
                            break;
                        case BattleDiagnosticEventKind.SkillRuntimeStarted:
                        case BattleDiagnosticEventKind.SkillRuntimeEnded:
                            HasSkillAnchor = true;
                            _skillStageCount++;
                            if (_skillId == 0 && item.ConfigId != 0) _skillId = item.ConfigId;
                            if (item.Kind == BattleDiagnosticEventKind.SkillRuntimeEnded)
                                UpdateOutcome(item.Outcome);
                            break;
                        case BattleDiagnosticEventKind.SkillEconomy:
                            HasSkillAnchor = true;
                            _economyCount++;
                            break;
                        default:
                            _consequenceCount++;
                            if (item.SkillRuntime.IsValid) HasSkillAnchor = true;
                            break;
                    }
                }

                var isLatest = _lastFrame == BattleDiagnosticFrames.Invalid ||
                               item.Frame > _lastFrame ||
                               (item.Frame == _lastFrame && item.Sequence > _latestSequence);
                if (_firstFrame == BattleDiagnosticFrames.Invalid || item.Frame < _firstFrame)
                    _firstFrame = item.Frame;
                if (_lastFrame == BattleDiagnosticFrames.Invalid || item.Frame > _lastFrame)
                    _lastFrame = item.Frame;
                if (isLatest || _latestSequence == 0L) _latestSequence = item.Sequence;
            }

            public BattleDebugSkillCastFlow Build()
            {
                _events.Sort((left, right) =>
                {
                    var frameComparison = left.Frame.CompareTo(right.Frame);
                    return frameComparison != 0
                        ? frameComparison
                        : left.Sequence.CompareTo(right.Sequence);
                });
                var key = _commandId != 0L
                    ? "command|" + _commandId
                    : _rootContextId != 0L
                        ? "root|" + _rootContextId
                        : _skillRuntime.IsValid
                            ? $"runtime|{_skillRuntime.RuntimeId}|{_skillRuntime.Generation}"
                            : "event|" + _latestSequence;
                return new BattleDebugSkillCastFlow(
                    key,
                    _commandId,
                    _rootContextId,
                    _skillRuntime,
                    _skillId,
                    _skillSlot,
                    _skillLevel,
                    _castSequence,
                    _sourceActorId,
                    _targetActorId,
                    _firstFrame,
                    _lastFrame,
                    _latestSequence,
                    _outcome,
                    _inputCount,
                    _targetSearchCount,
                    _skillStageCount,
                    _economyCount,
                    _consequenceCount,
                    BuildPhaseTimings(),
                    _events.ToArray());
            }

            private IReadOnlyList<BattleDebugSkillCastPhaseTiming> BuildPhaseTimings()
            {
                var inputFrame = BattleDiagnosticFrames.Invalid;
                var targetFrame = BattleDiagnosticFrames.Invalid;
                var preCastStartFrame = BattleDiagnosticFrames.Invalid;
                var preCastEndFrame = BattleDiagnosticFrames.Invalid;
                var castStartFrame = BattleDiagnosticFrames.Invalid;
                var castEndFrame = BattleDiagnosticFrames.Invalid;
                var economyStartFrame = BattleDiagnosticFrames.Invalid;
                var economyEndFrame = BattleDiagnosticFrames.Invalid;
                var waitingChildrenFrame = BattleDiagnosticFrames.Invalid;
                var runtimeEndFrame = BattleDiagnosticFrames.Invalid;

                for (var i = 0; i < _events.Count; i++)
                {
                    var item = _events[i];
                    if (item.Kind == BattleDiagnosticEventKind.InputCommand)
                        SetFirstFrame(ref inputFrame, item.Frame);
                    if (item.Kind == BattleDiagnosticEventKind.TargetSearch)
                        SetFirstFrame(ref targetFrame, item.Frame);
                    if (!item.Payload.TryGetSkillExecution(out var execution)) continue;

                    switch (execution.Stage)
                    {
                        case BattleDiagnosticSkillExecutionStage.PreCastStarted:
                            SetFirstFrame(ref preCastStartFrame, item.Frame);
                            break;
                        case BattleDiagnosticSkillExecutionStage.PreCastCompleted:
                            SetLastFrame(ref preCastEndFrame, item.Frame);
                            break;
                        case BattleDiagnosticSkillExecutionStage.CastStarted:
                            SetFirstFrame(ref castStartFrame, item.Frame);
                            break;
                        case BattleDiagnosticSkillExecutionStage.CastCompleted:
                        case BattleDiagnosticSkillExecutionStage.CastFailed:
                        case BattleDiagnosticSkillExecutionStage.CastInterrupted:
                            SetLastFrame(ref castEndFrame, item.Frame);
                            break;
                        case BattleDiagnosticSkillExecutionStage.EconomyReserved:
                            SetFirstFrame(ref economyStartFrame, item.Frame);
                            break;
                        case BattleDiagnosticSkillExecutionStage.ResourceConsumed:
                            SetFirstFrame(ref economyStartFrame, item.Frame);
                            SetLastFrame(ref economyEndFrame, item.Frame);
                            break;
                        case BattleDiagnosticSkillExecutionStage.EconomyCommitted:
                        case BattleDiagnosticSkillExecutionStage.EconomyRefunded:
                        case BattleDiagnosticSkillExecutionStage.EconomyRejected:
                            if (economyStartFrame == BattleDiagnosticFrames.Invalid)
                                SetFirstFrame(ref economyStartFrame, item.Frame);
                            SetLastFrame(ref economyEndFrame, item.Frame);
                            break;
                        case BattleDiagnosticSkillExecutionStage.RuntimeWaitingChildren:
                            SetFirstFrame(ref waitingChildrenFrame, item.Frame);
                            break;
                        case BattleDiagnosticSkillExecutionStage.RuntimeFinalized:
                        case BattleDiagnosticSkillExecutionStage.RuntimeForceTerminated:
                        case BattleDiagnosticSkillExecutionStage.RuntimeCleared:
                            SetLastFrame(ref runtimeEndFrame, item.Frame);
                            break;
                    }
                }

                var timings = new List<BattleDebugSkillCastPhaseTiming>(8);
                AddTiming(timings, BattleDebugSkillCastPhase.InputToTarget, inputFrame, targetFrame);
                AddTiming(timings, BattleDebugSkillCastPhase.TargetToCast, targetFrame, castStartFrame);
                AddTiming(timings, BattleDebugSkillCastPhase.PreCast, preCastStartFrame, preCastEndFrame);
                AddTiming(timings, BattleDebugSkillCastPhase.Cast, castStartFrame, castEndFrame);
                AddTiming(timings, BattleDebugSkillCastPhase.Economy, economyStartFrame, economyEndFrame);
                AddTiming(timings, BattleDebugSkillCastPhase.WaitingChildren, waitingChildrenFrame, runtimeEndFrame);
                AddTiming(timings, BattleDebugSkillCastPhase.RuntimeTail, castEndFrame, runtimeEndFrame);

                var totalStartFrame = inputFrame != BattleDiagnosticFrames.Invalid
                    ? inputFrame
                    : preCastStartFrame != BattleDiagnosticFrames.Invalid
                        ? preCastStartFrame
                        : castStartFrame != BattleDiagnosticFrames.Invalid
                            ? castStartFrame
                            : _firstFrame;
                var totalEndFrame = runtimeEndFrame != BattleDiagnosticFrames.Invalid
                    ? runtimeEndFrame
                    : castEndFrame;
                AddTiming(timings, BattleDebugSkillCastPhase.Total, totalStartFrame, totalEndFrame);
                return timings;
            }

            private static void AddTiming(
                List<BattleDebugSkillCastPhaseTiming> timings,
                BattleDebugSkillCastPhase phase,
                int startFrame,
                int endFrame)
            {
                if (startFrame == BattleDiagnosticFrames.Invalid ||
                    endFrame == BattleDiagnosticFrames.Invalid ||
                    endFrame < startFrame)
                {
                    return;
                }
                timings.Add(new BattleDebugSkillCastPhaseTiming(phase, startFrame, endFrame));
            }

            private static void SetFirstFrame(ref int value, int frame)
            {
                if (value == BattleDiagnosticFrames.Invalid || frame < value) value = frame;
            }

            private static void SetLastFrame(ref int value, int frame)
            {
                if (value == BattleDiagnosticFrames.Invalid || frame > value) value = frame;
            }

            private void UpdateOutcome(BattleDiagnosticEventOutcome outcome)
            {
                if (OutcomePriority(outcome) > OutcomePriority(_outcome)) _outcome = outcome;
            }

            private static int OutcomePriority(BattleDiagnosticEventOutcome outcome)
            {
                switch (outcome)
                {
                    case BattleDiagnosticEventOutcome.Failed: return 4;
                    case BattleDiagnosticEventOutcome.Interrupted: return 3;
                    case BattleDiagnosticEventOutcome.Cancelled: return 2;
                    case BattleDiagnosticEventOutcome.Succeeded: return 1;
                    default: return 0;
                }
            }

            private static bool IsTerminalSkillStage(BattleDiagnosticSkillExecutionStage stage)
            {
                switch (stage)
                {
                    case BattleDiagnosticSkillExecutionStage.CastCompleted:
                    case BattleDiagnosticSkillExecutionStage.CastFailed:
                    case BattleDiagnosticSkillExecutionStage.CastInterrupted:
                    case BattleDiagnosticSkillExecutionStage.EconomyRejected:
                    case BattleDiagnosticSkillExecutionStage.RuntimeFinalized:
                    case BattleDiagnosticSkillExecutionStage.RuntimeForceTerminated:
                    case BattleDiagnosticSkillExecutionStage.RuntimeCleared:
                        return true;
                    default:
                        return false;
                }
            }
        }

        private static string BuildTriggerFlowKey(
            int triggerId,
            long rootContextId,
            long contextId,
            long sequence,
            bool spansMultipleRoots)
        {
            if (spansMultipleRoots) return $"window|{sequence}";
            if (rootContextId != 0) return $"root|{rootContextId}|trigger|{triggerId}";
            if (contextId != 0) return $"context|{contextId}|trigger|{triggerId}";
            return $"event|{sequence}";
        }

        private sealed class TriggerFlowBuilder
        {
            private readonly string _key;
            private readonly int _triggerId;
            private readonly long _rootContextId;
            private readonly long _firstRootContextId;
            private readonly long _lastRootContextId;
            private readonly long _sourceActorId;
            private readonly long _targetActorId;
            private readonly Dictionary<BattleDiagnosticTriggerAnalysisStage, TriggerStageBuilder> _stages =
                new Dictionary<BattleDiagnosticTriggerAnalysisStage, TriggerStageBuilder>();
            private int _firstFrame = BattleDiagnosticFrames.Invalid;
            private int _lastFrame = BattleDiagnosticFrames.Invalid;
            private long _latestSequence;
            private long _latestContextId;

            public TriggerFlowBuilder(
                string key,
                int triggerId,
                long rootContextId,
                long firstRootContextId,
                long lastRootContextId,
                long sourceActorId,
                long targetActorId)
            {
                _key = key;
                _triggerId = triggerId;
                _rootContextId = rootContextId;
                _firstRootContextId = firstRootContextId;
                _lastRootContextId = lastRootContextId;
                _sourceActorId = sourceActorId;
                _targetActorId = targetActorId;
            }

            public void Add(
                BattleDiagnosticTriggerAnalysisStage stage,
                BattleDiagnosticTriggerAnalysisResult result,
                int occurrenceCount,
                int firstFrame,
                int lastFrame,
                long sequence,
                long contextId,
                string failureKey,
                string reason)
            {
                if (stage == BattleDiagnosticTriggerAnalysisStage.Unknown || occurrenceCount <= 0) return;
                if (!_stages.TryGetValue(stage, out var stageBuilder))
                {
                    stageBuilder = new TriggerStageBuilder(stage);
                    _stages.Add(stage, stageBuilder);
                }

                stageBuilder.Add(
                    result,
                    occurrenceCount,
                    firstFrame,
                    lastFrame,
                    sequence,
                    contextId,
                    failureKey,
                    reason);
                var isLatest = _lastFrame == BattleDiagnosticFrames.Invalid ||
                               lastFrame > _lastFrame ||
                               (lastFrame == _lastFrame && sequence >= _latestSequence);
                if (_firstFrame == BattleDiagnosticFrames.Invalid || firstFrame < _firstFrame)
                {
                    _firstFrame = firstFrame;
                }
                if (_lastFrame == BattleDiagnosticFrames.Invalid || lastFrame > _lastFrame)
                {
                    _lastFrame = lastFrame;
                }
                if (isLatest)
                {
                    _latestSequence = sequence;
                    _latestContextId = contextId;
                }
            }

            public BattleDebugTriggerFlow Build()
            {
                return new BattleDebugTriggerFlow(
                    _key,
                    _triggerId,
                    _rootContextId,
                    _firstRootContextId,
                    _lastRootContextId,
                    _sourceActorId,
                    _targetActorId,
                    _firstFrame,
                    _lastFrame,
                    _latestSequence,
                    _latestContextId,
                    GetStage(BattleDiagnosticTriggerAnalysisStage.Budget),
                    GetStage(BattleDiagnosticTriggerAnalysisStage.Conditions),
                    GetStage(BattleDiagnosticTriggerAnalysisStage.Plan),
                    GetStage(BattleDiagnosticTriggerAnalysisStage.Execution));
            }

            private BattleDebugTriggerStageSummary GetStage(BattleDiagnosticTriggerAnalysisStage stage)
            {
                return _stages.TryGetValue(stage, out var builder)
                    ? builder.Build()
                    : new BattleDebugTriggerStageSummary(
                        stage,
                        0,
                        0,
                        0,
                        0,
                        0,
                        0,
                        BattleDiagnosticFrames.Invalid,
                        BattleDiagnosticFrames.Invalid,
                        BattleDiagnosticTriggerAnalysisResult.Unknown,
                        0,
                        0,
                        string.Empty,
                        string.Empty);
            }
        }

        private sealed class TriggerStageBuilder
        {
            private readonly BattleDiagnosticTriggerAnalysisStage _stage;
            private int _eventCount;
            private int _occurrenceCount;
            private int _passedCount;
            private int _failedCount;
            private int _blockedCount;
            private int _skippedCount;
            private int _firstFrame = BattleDiagnosticFrames.Invalid;
            private int _lastFrame = BattleDiagnosticFrames.Invalid;
            private BattleDiagnosticTriggerAnalysisResult _latestResult;
            private long _latestSequence;
            private long _latestContextId;
            private string _failureKey = string.Empty;
            private string _reason = string.Empty;

            public TriggerStageBuilder(BattleDiagnosticTriggerAnalysisStage stage)
            {
                _stage = stage;
            }

            public void Add(
                BattleDiagnosticTriggerAnalysisResult result,
                int occurrenceCount,
                int firstFrame,
                int lastFrame,
                long sequence,
                long contextId,
                string failureKey,
                string reason)
            {
                _eventCount++;
                _occurrenceCount += occurrenceCount;
                switch (result)
                {
                    case BattleDiagnosticTriggerAnalysisResult.Passed:
                        _passedCount += occurrenceCount;
                        break;
                    case BattleDiagnosticTriggerAnalysisResult.Failed:
                        _failedCount += occurrenceCount;
                        break;
                    case BattleDiagnosticTriggerAnalysisResult.Blocked:
                        _blockedCount += occurrenceCount;
                        break;
                    case BattleDiagnosticTriggerAnalysisResult.Skipped:
                        _skippedCount += occurrenceCount;
                        break;
                }

                if (_firstFrame == BattleDiagnosticFrames.Invalid || firstFrame < _firstFrame)
                {
                    _firstFrame = firstFrame;
                }

                var isLatest = _lastFrame == BattleDiagnosticFrames.Invalid ||
                               lastFrame > _lastFrame ||
                               (lastFrame == _lastFrame && sequence >= _latestSequence);
                if (lastFrame > _lastFrame) _lastFrame = lastFrame;
                if (!isLatest) return;

                _latestResult = result;
                _latestSequence = sequence;
                _latestContextId = contextId;
                if (!string.IsNullOrEmpty(failureKey)) _failureKey = failureKey;
                if (!string.IsNullOrEmpty(reason)) _reason = reason;
            }

            public BattleDebugTriggerStageSummary Build()
            {
                return new BattleDebugTriggerStageSummary(
                    _stage,
                    _eventCount,
                    _occurrenceCount,
                    _passedCount,
                    _failedCount,
                    _blockedCount,
                    _skippedCount,
                    _firstFrame,
                    _lastFrame,
                    _latestResult,
                    _latestSequence,
                    _latestContextId,
                    _failureKey,
                    _reason);
            }
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
                var label = $"触发 {trigger.TriggerId}  {BattleDebugDisplayText.TriggerStage(trigger.Stage)}/{BattleDebugDisplayText.TriggerResult(trigger.Result)}";
                if (!string.IsNullOrEmpty(reason)) label += $"  {TrimLabel(reason, 42)}";
                return new IssueGroupDescriptor(
                    key,
                    label,
                    diagnosticEvent.ConfigId,
                    failureKey,
                    trigger.Stage,
                    trigger.Result);
            }

            if (diagnosticEvent.Payload.TryGetTriggerAnalysisAggregate(out var aggregate))
            {
                var failureKey = aggregate.FailureKey ?? string.Empty;
                var key = $"trigger|{aggregate.TriggerId}|{aggregate.Stage}|{aggregate.Result}|{failureKey}";
                var reason = string.IsNullOrEmpty(failureKey)
                    ? aggregate.SampleReason
                    : failureKey;
                var label = $"触发 {aggregate.TriggerId}  {BattleDebugDisplayText.TriggerStage(aggregate.Stage)}/{BattleDebugDisplayText.TriggerResult(aggregate.Result)}";
                if (!string.IsNullOrEmpty(reason)) label += $"  {TrimLabel(reason, 42)}";
                return new IssueGroupDescriptor(
                    key,
                    label,
                    diagnosticEvent.ConfigId,
                    failureKey,
                    aggregate.Stage,
                    aggregate.Result);
            }

            if (diagnosticEvent.Payload.TryGetSkillFailure(out var skillFailure))
            {
                var stableCode = skillFailure.Code ?? string.Empty;
                var key = $"skill|{stableCode}|{skillFailure.Source}|{skillFailure.Stage}";
                var label = string.IsNullOrEmpty(stableCode)
                    ? $"技能失败  {BattleDebugDisplayText.SkillFailureSource(skillFailure.Source)}/{BattleDebugDisplayText.SkillFailureStage(skillFailure.Stage)}"
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
                ? $"{BattleDebugDisplayText.EventKind(diagnosticEvent.Kind)}  配置={diagnosticEvent.ConfigId}"
                : BattleDebugDisplayText.EventKind(diagnosticEvent.Kind);
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
                Add(1, diagnosticEvent.Frame, diagnosticEvent.Frame);
            }

            public void Add(int count, int firstFrame, int latestFrame)
            {
                _count += count;
                if (_firstFrame == BattleDiagnosticFrames.Invalid || firstFrame < _firstFrame)
                {
                    _firstFrame = firstFrame;
                }

                if (latestFrame > _latestFrame) _latestFrame = latestFrame;
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
                case BattleDebugDiagnosticEventScope.Targeting:
                    return BattleDiagnosticEventChannel.Targeting;
                case BattleDebugDiagnosticEventScope.Input:
                    return BattleDiagnosticEventChannel.Input;
                default:
                    return BattleDiagnosticEventChannel.All;
            }
        }
    }
}
