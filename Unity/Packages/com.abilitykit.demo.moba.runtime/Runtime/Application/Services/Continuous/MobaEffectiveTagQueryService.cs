using System.Collections.Generic;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;
using AbilityKit.Continuous;
using AbilityKit.GameplayTags;

namespace AbilityKit.Demo.Moba.Services
{
    public interface IMobaEffectiveTagQueryService
    {
        GameplayTagContainer GetEffectiveTags(int ownerActorId);
        bool CanActivate(int ownerActorId, ContinuousTagRequirements requirements);
        bool ShouldRemove(int ownerActorId, ContinuousTagRequirements requirements);
        void MarkDirty(int ownerActorId);
        bool RemoveActor(int ownerActorId);
    }

    [WorldService(typeof(IMobaEffectiveTagQueryService))]
    [WorldService(typeof(MobaEffectiveTagQueryService))]
    public sealed class MobaEffectiveTagQueryService : IMobaEffectiveTagQueryService, IContinuousLifecycleBinder, IWorldInitializable, IService
    {
        private sealed class OwnerCache
        {
            public GameplayTagContainer Tags;
            public bool Dirty = true;
        }

        private readonly Dictionary<int, OwnerCache> _ownerCaches = new Dictionary<int, OwnerCache>();
        private IGameplayTagService _baseTags;
        private IContinuousManager _continuous;
        private DefaultContinuousManager _continuousEvents;
        private IMobaContinuousTagRuleService _tagRules;
        private IWorldResolver _services;

        public void OnInit(IWorldResolver services)
        {
            _services = services;
            services?.TryResolve(out _baseTags);
            services?.TryResolve(out _continuous);

            if (_baseTags != null)
            {
                _baseTags.TagsChanged += OnBaseTagsChanged;
            }

            _continuousEvents = _continuous as DefaultContinuousManager;
            _continuousEvents?.AddLifecycleBinder(this);
        }

        public void Dispose()
        {
            if (_baseTags != null)
            {
                _baseTags.TagsChanged -= OnBaseTagsChanged;
            }

            _continuousEvents?.RemoveLifecycleBinder(this);
            _ownerCaches.Clear();
            _baseTags = null;
            _continuous = null;
            _continuousEvents = null;
            _tagRules = null;
            _services = null;
        }

        public GameplayTagContainer GetEffectiveTags(int ownerActorId)
        {
            if (ownerActorId <= 0) return new GameplayTagContainer();

            var cache = GetOrCreateCache(ownerActorId);
            if (cache.Dirty || cache.Tags == null)
            {
                cache.Tags = BuildEffectiveTags(ownerActorId);
                cache.Dirty = false;
            }

            return cache.Tags;
        }

        public bool CanActivate(int ownerActorId, ContinuousTagRequirements requirements)
        {
            return requirements == null || requirements.CanActivate(GetEffectiveTags(ownerActorId));
        }

        public bool ShouldRemove(int ownerActorId, ContinuousTagRequirements requirements)
        {
            if (requirements == null) return false;

            var tags = GetEffectiveTags(ownerActorId);
            return requirements.ShouldRemove(tags);
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
            MarkDirty(continuous);
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
            MarkDirty(continuous);
        }

        private GameplayTagContainer BuildEffectiveTags(int ownerActorId)
        {
            var result = CopyTags(_baseTags?.GetTags(ownerActorId));
            if (_continuous == null) return result;

            var active = _continuous.GetOwnerActiveContinuous(ownerActorId);
            if (active == null || active.Count == 0) return result;

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

        private OwnerCache GetOrCreateCache(int ownerActorId)
        {
            if (!_ownerCaches.TryGetValue(ownerActorId, out var cache))
            {
                cache = new OwnerCache();
                _ownerCaches.Add(ownerActorId, cache);
            }

            return cache;
        }

        private void OnBaseTagsChanged(int ownerActorId, GameplayTagDelta delta, GameplayTagSource source)
        {
            MarkDirty(ownerActorId);
            ResolveTagRules()?.ReconcileOwner(ownerActorId);
        }

        private void MarkDirty(IContinuous continuous)
        {
            var ownerId = continuous?.Config?.OwnerId ?? 0L;
            if (ownerId <= 0 || ownerId > int.MaxValue) return;

            MarkDirty((int)ownerId);
        }

        private IMobaContinuousTagRuleService ResolveTagRules()
        {
            if (_tagRules == null)
            {
                _services?.TryResolve(out _tagRules);
            }

            return _tagRules;
        }

        private static GameplayTagContainer CopyTags(GameplayTagContainer tags)
        {
            return tags == null ? new GameplayTagContainer() : new GameplayTagContainer(tags);
        }
    }
}
