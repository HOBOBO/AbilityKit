using System;
using System.Collections.Generic;
using System.Diagnostics;
using AbilityKit.Ability.Config;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;
using AbilityKit.Continuous;
using AbilityKit.GameplayTags;

namespace AbilityKit.Demo.Moba.Services
{
    public interface IMobaTagResultQueryService
    {
        MobaTagResultFlags GetResultFlags(int ownerActorId);
    }

    public interface IMobaTagDefinitionQueryService
    {
        bool TryGetActiveTagName(int definitionId, out string name);
    }

    public interface IMobaTagRuleQueryService
    {
        bool TryMatchesRule(int ownerActorId, int ruleGroupId, out bool matches);
    }

    public interface IMobaTagBehaviorGateQueryService
    {
        bool TryIsBlocked(int ownerActorId, moba_luban.MobaTagBehaviorGate gateId, out bool blocked);
    }

    public readonly struct MobaTagQueryMetrics
    {
        public MobaTagQueryMetrics(long cacheRebuilds, long resultReads, long ruleReads,
            long scannedContinuous, long mappingEvaluations, long rebuildElapsedTicks)
        {
            CacheRebuilds = cacheRebuilds;
            ResultReads = resultReads;
            RuleReads = ruleReads;
            ScannedContinuous = scannedContinuous;
            MappingEvaluations = mappingEvaluations;
            RebuildElapsedTicks = rebuildElapsedTicks;
        }

        public long CacheRebuilds { get; }
        public long ResultReads { get; }
        public long RuleReads { get; }
        public long ScannedContinuous { get; }
        public long MappingEvaluations { get; }
        public long RebuildElapsedTicks { get; }
    }

    public readonly struct MobaTagRequirementEvaluation
    {
        public MobaTagRequirementEvaluation(bool canActivate, bool shouldRemove, bool ongoingSatisfied)
        {
            CanActivate = canActivate;
            ShouldRemove = shouldRemove;
            OngoingSatisfied = ongoingSatisfied;
        }

        public bool CanActivate { get; }
        public bool ShouldRemove { get; }
        public bool OngoingSatisfied { get; }
    }

    public interface IMobaEffectiveTagQueryService
    {
        GameplayTagContainer GetEffectiveTags(int ownerActorId);
        bool HasTag(int ownerActorId, GameplayTag tag, bool exact = false);
        bool HasAnyTag(int ownerActorId, GameplayTagContainer tags, bool exact = false);
        bool CanExecute(IContinuous continuous);
        bool CanActivate(int ownerActorId, ContinuousTagRequirements requirements);
        bool ShouldRemove(int ownerActorId, ContinuousTagRequirements requirements);
        MobaTagRequirementEvaluation EvaluateRequirements(int ownerActorId, ContinuousTagRequirements requirements);
        void MarkDirty(int ownerActorId);
        bool RemoveActor(int ownerActorId);
    }

    [WorldService(typeof(IMobaEffectiveTagQueryService))]
    [WorldService(typeof(MobaEffectiveTagQueryService))]
    [WorldService(typeof(IMobaTagResultQueryService))]
    [WorldService(typeof(IMobaTagDefinitionQueryService))]
    [WorldService(typeof(IMobaTagRuleQueryService))]
    [WorldService(typeof(IMobaTagBehaviorGateQueryService))]
    public sealed class MobaEffectiveTagQueryService : IMobaEffectiveTagQueryService, IMobaTagResultQueryService, IMobaTagDefinitionQueryService, IMobaTagRuleQueryService, IMobaTagBehaviorGateQueryService, IContinuousLifecycleBinder, IWorldInitializable, IService
    {
        private sealed class OwnerCache
        {
            public GameplayTagContainer Tags;
            public bool Dirty = true;
            public MobaTagResultFlags Flags;
        }

        private readonly Dictionary<int, OwnerCache> _ownerCaches = new Dictionary<int, OwnerCache>();
        private IContinuousManager _continuous;
        private DefaultContinuousManager _continuousEvents;
        private MobaTagWorkbookRuntime _tagWorkbook;
        private long _cacheRebuilds;
        private long _resultReads;
        private long _ruleReads;
        private long _scannedContinuous;
        private long _mappingEvaluations;
        private long _rebuildElapsedTicks;

        public MobaTagQueryMetrics Metrics => new MobaTagQueryMetrics(_cacheRebuilds, _resultReads, _ruleReads,
            _scannedContinuous, _mappingEvaluations, _rebuildElapsedTicks);

        internal MobaTagWorkbookRuntime DebugWorkbook => _tagWorkbook;

        public void OnInit(IWorldResolver services)
        {
            if (services == null || !services.TryResolve<ITextAssetLoader>(out var assets) || assets == null)
                throw new InvalidOperationException("MOBA tag results require an ITextAssetLoader and the published tag workbook.");
            if (!services.TryResolve<IContinuousManager>(out _continuous) || !(_continuous is DefaultContinuousManager manager))
                throw new InvalidOperationException("MOBA tag results require a lifecycle-aware IContinuousManager.");

            _tagWorkbook = new MobaTagWorkbookRuntime(assets);
            _continuousEvents = manager;
            _continuousEvents.AddLifecycleBinder(this);
        }

        public void Dispose()
        {
            _continuousEvents?.RemoveLifecycleBinder(this);
            _ownerCaches.Clear();
            _continuous = null;
            _continuousEvents = null;
            _tagWorkbook = null;
            _cacheRebuilds = 0;
            _resultReads = 0;
            _ruleReads = 0;
            _scannedContinuous = 0;
            _mappingEvaluations = 0;
            _rebuildElapsedTicks = 0;
        }

        public GameplayTagContainer GetEffectiveTags(int ownerActorId)
        {
            if (ownerActorId <= 0) return new GameplayTagContainer();

            return new GameplayTagContainer(GetOrRefreshCache(ownerActorId).Tags);
        }

        public bool HasTag(int ownerActorId, GameplayTag tag, bool exact = false) =>
            ownerActorId > 0 && tag.IsValid &&
            (exact ? GetOrRefreshCache(ownerActorId).Tags.HasTagExact(tag) :
                GetOrRefreshCache(ownerActorId).Tags.HasTag(tag));

        public bool HasAnyTag(int ownerActorId, GameplayTagContainer tags, bool exact = false) =>
            ownerActorId > 0 && tags != null && tags.Count > 0 &&
            GetOrRefreshCache(ownerActorId).Tags.HasAny(tags, exact);

        public MobaTagResultFlags GetResultFlags(int ownerActorId)
        {
            _resultReads++;
            if (ownerActorId <= 0) return MobaTagResultFlags.None;
            return GetOrRefreshCache(ownerActorId).Flags;
        }

        public bool TryIsBlocked(int ownerActorId, moba_luban.MobaTagBehaviorGate gateId, out bool blocked)
        {
            blocked = false;
            if (ownerActorId <= 0 || _tagWorkbook == null ||
                !_tagWorkbook.TryGetBehaviorGateMask(gateId, out var mask)) return false;
            blocked = (GetResultFlags(ownerActorId) & mask) != 0;
            return true;
        }

        public bool TryGetActiveTagName(int definitionId, out string name)
        {
            name = null;
            return _tagWorkbook != null && _tagWorkbook.TryGetActiveTagName(definitionId, out name);
        }

        public bool TryMatchesRule(int ownerActorId, int ruleGroupId, out bool matches)
        {
            _ruleReads++;
            matches = false;
            if (ownerActorId <= 0 || ruleGroupId <= 0 || _tagWorkbook == null ||
                !_tagWorkbook.TryGetRule(ruleGroupId, out var query)) return false;
            matches = query.Matches(GetOrRefreshCache(ownerActorId).Tags);
            return true;
        }

        public bool CanExecute(IContinuous continuous)
        {
            if (continuous == null || continuous.IsTerminated || !continuous.IsActive || continuous.IsPaused)
                return false;
            if (!(continuous.Config is IMobaContinuousTagConfig config)) return true;
            var blockedMask = config.TagRequirements?.ExecutionBlockedResultMask ?? 0UL;
            if (blockedMask == 0UL) return true;
            var ownerId = continuous.Config.OwnerId;
            return ownerId > 0 && ownerId <= int.MaxValue &&
                ((ulong)GetResultFlags((int)ownerId) & blockedMask) == 0UL;
        }

        public bool CanActivate(int ownerActorId, ContinuousTagRequirements requirements)
        {
            return EvaluateRequirements(ownerActorId, requirements).CanActivate;
        }

        public bool ShouldRemove(int ownerActorId, ContinuousTagRequirements requirements)
        {
            return EvaluateRequirements(ownerActorId, requirements).ShouldRemove;
        }

        public MobaTagRequirementEvaluation EvaluateRequirements(int ownerActorId, ContinuousTagRequirements requirements)
        {
            if (requirements == null) return new MobaTagRequirementEvaluation(true, false, true);
            var tags = ownerActorId > 0 ? GetOrRefreshCache(ownerActorId).Tags : new GameplayTagContainer();
            return new MobaTagRequirementEvaluation(requirements.CanActivate(tags),
                requirements.ShouldRemove(tags), requirements.IsOngoingSatisfied(tags));
        }

        public void MarkDirty(int ownerActorId)
        {
            if (ownerActorId <= 0) return;

            var cache = GetOrCreateCache(ownerActorId);
            cache.Dirty = true;
        }

        public bool RemoveActor(int ownerActorId)
        {
            return ownerActorId > 0 && _ownerCaches.Remove(ownerActorId);
        }

        public void OnRegistered(IContinuous continuous, IContinuousManager manager)
        {
        }

        public void OnActivated(IContinuous continuous, IContinuousManager manager)
        {
            MarkDirty(continuous);
        }

        public void OnPaused(IContinuous continuous, IContinuousManager manager)
        {
            MarkDirty(continuous);
        }

        public void OnResumed(IContinuous continuous, IContinuousManager manager)
        {
            MarkDirty(continuous);
        }

        public void OnEnded(IContinuous continuous, ContinuousEndReason reason, IContinuousManager manager)
        {
            MarkDirty(continuous);
        }

        public void OnUnregistered(IContinuous continuous, ContinuousEndReason reason, IContinuousManager manager)
        {
            if (continuous != null && continuous.IsActive)
                MarkDirty(continuous);
        }

        private GameplayTagContainer BuildEffectiveTags(int ownerActorId)
        {
            var result = new GameplayTagContainer();
            if (_continuous == null) return result;

            var active = _continuous.GetOwnerContinuous(ownerActorId);
            if (active == null || active.Count == 0) return result;

            _scannedContinuous += active.Count;

            for (int i = 0; i < active.Count; i++)
            {
                var continuous = active[i];
                if (continuous == null || !continuous.IsActive || continuous.IsTerminated) continue;
                if (!(continuous.Config is IMobaContinuousTagConfig tagConfig)) continue;

                var applicationTags = tagConfig.TagRequirements?.ApplicationTags;
                if (applicationTags == null || applicationTags.Count == 0) continue;

                result.AppendTags(applicationTags);
            }

            return result;
        }

        private OwnerCache GetOrRefreshCache(int ownerActorId)
        {
            if (_tagWorkbook == null)
                throw new InvalidOperationException("MOBA tag result service has not been initialized.");
            var cache = GetOrCreateCache(ownerActorId);
            if (cache.Dirty || cache.Tags == null)
            {
                var started = Stopwatch.GetTimestamp();
                cache.Tags = BuildEffectiveTags(ownerActorId);
                cache.Flags = _tagWorkbook.Evaluate(cache.Tags);
                _mappingEvaluations += _tagWorkbook.ResultMappingCount;
                cache.Dirty = false;
                _cacheRebuilds++;
                _rebuildElapsedTicks += Stopwatch.GetTimestamp() - started;
            }
            return cache;
        }

        private OwnerCache GetOrCreateCache(int ownerActorId)
        {
            if (!_ownerCaches.TryGetValue(ownerActorId, out var cache))
            {
                cache = new OwnerCache();
                _ownerCaches.Add(ownerActorId, cache);
            }

            return cache;
        }

        private void MarkDirty(IContinuous continuous)
        {
            if (!(continuous?.Config is IMobaContinuousTagConfig tagConfig) ||
                tagConfig.TagRequirements?.ApplicationTags == null ||
                tagConfig.TagRequirements.ApplicationTags.Count == 0) return;
            var ownerId = continuous?.Config?.OwnerId ?? 0L;
            if (ownerId <= 0 || ownerId > int.MaxValue) return;

            MarkDirty((int)ownerId);
        }

    }
}
