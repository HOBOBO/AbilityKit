using System;
using System.Collections.Generic;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;
using AbilityKit.Continuous;
using AbilityKit.GameplayTags;
using AbilityKit.Modifiers;

namespace AbilityKit.Demo.Moba.Services
{
    public interface IMobaContinuousRuntimeQueryService
    {
        IReadOnlyList<MobaContinuousRuntimeView> GetOwnerContinuous(int ownerActorId, bool includeTerminated = false);
        IReadOnlyList<MobaContinuousRuntimeView> GetAllContinuous(bool includeTerminated = false);
        bool TryGetRuntimeView(IContinuous continuous, out MobaContinuousRuntimeView view);
        MobaContinuousLifecycleReason GetLifecycleReason(IContinuous continuous);
        MobaContinuousTagRuleResult GetTagRuleResult(IContinuous continuous);
    }


    [WorldService(typeof(IMobaContinuousRuntimeQueryService))]
    [WorldService(typeof(MobaContinuousRuntimeQueryService))]
    public sealed class MobaContinuousRuntimeQueryService : IMobaContinuousRuntimeQueryService, IContinuousLifecycleBinder, IWorldInitializable, IService
    {
        private readonly Dictionary<IContinuous, MobaContinuousLifecycleReason> _reasons = new Dictionary<IContinuous, MobaContinuousLifecycleReason>();
        private IContinuousManager _continuous;
        private DefaultContinuousManager _continuousEvents;
        private IMobaContinuousTagRuleService _tagRules;
        private IMobaContinuousRuntimeViewBuilder _viewBuilder;

        public void OnInit(IWorldResolver services)
        {
            services?.TryResolve(out _continuous);
            services?.TryResolve(out _tagRules);
            services?.TryResolve(out _viewBuilder);
            _continuousEvents = _continuous as DefaultContinuousManager;
            _continuousEvents?.AddLifecycleBinder(this);
        }

        public void Dispose()
        {
            _continuousEvents?.RemoveLifecycleBinder(this);
            _reasons.Clear();
            _continuous = null;
            _continuousEvents = null;
            _tagRules = null;
            _viewBuilder = null;
        }

        public IReadOnlyList<MobaContinuousRuntimeView> GetOwnerContinuous(int ownerActorId, bool includeTerminated = false)
        {
            if (ownerActorId <= 0 || _continuous == null) return Array.Empty<MobaContinuousRuntimeView>();

            var continuousList = _continuous.GetOwnerContinuous(ownerActorId);
            return BuildViews(continuousList, includeTerminated);
        }

        public IReadOnlyList<MobaContinuousRuntimeView> GetAllContinuous(bool includeTerminated = false)
        {
            if (_continuousEvents == null) return Array.Empty<MobaContinuousRuntimeView>();
            return BuildViews(_continuousEvents.GetAllContinuous(), includeTerminated);
        }

        public bool TryGetRuntimeView(IContinuous continuous, out MobaContinuousRuntimeView view)
        {
            view = null;
            if (continuous == null || _viewBuilder == null) return false;

            view = _viewBuilder.Build(continuous);
            return view != null;
        }

        public MobaContinuousLifecycleReason GetLifecycleReason(IContinuous continuous)
        {
            if (continuous == null) return MobaContinuousLifecycleReason.None;
            return _reasons.TryGetValue(continuous, out var reason) ? reason : MobaContinuousLifecycleReason.None;
        }

        public MobaContinuousTagRuleResult GetTagRuleResult(IContinuous continuous)
        {
            if (continuous == null || _tagRules == null) return MobaContinuousTagRuleResult.None;

            var result = _tagRules.GetLastResult(continuous);
            return result.HasResult ? result : _tagRules.Explain(continuous);
        }

        public void OnRegistered(IContinuous continuous, IContinuousManager manager)
        {
            SetReason(continuous, "Registered", null, default);
        }

        public void OnActivated(IContinuous continuous, IContinuousManager manager)
        {
            SetReason(continuous, "Activated", null, default);
        }

        public void OnPaused(IContinuous continuous, IContinuousManager manager)
        {
            SetReason(continuous, "Paused", "Paused by lifecycle or tag rule", default);
        }

        public void OnResumed(IContinuous continuous, IContinuousManager manager)
        {
            SetReason(continuous, "Resumed", "Resumed by lifecycle or tag rule", default);
        }

        public void OnEnded(IContinuous continuous, ContinuousEndReason reason, IContinuousManager manager)
        {
            SetReason(continuous, "Ended", reason.ToString(), reason);
        }

        public void OnUnregistered(IContinuous continuous, ContinuousEndReason reason, IContinuousManager manager)
        {
            SetReason(continuous, "Unregistered", reason.ToString(), reason);
        }

        private IReadOnlyList<MobaContinuousRuntimeView> BuildViews(IReadOnlyList<IContinuous> continuousList, bool includeTerminated)
        {
            if (continuousList == null || continuousList.Count == 0) return Array.Empty<MobaContinuousRuntimeView>();

            List<MobaContinuousRuntimeView> result = null;
            for (int i = 0; i < continuousList.Count; i++)
            {
                var continuous = continuousList[i];
                if (continuous == null) continue;
                if (!includeTerminated && continuous.IsTerminated) continue;

                var view = _viewBuilder?.Build(continuous);
                if (view == null) continue;

                result ??= new List<MobaContinuousRuntimeView>();
                result.Add(view);
            }

            return result ?? (IReadOnlyList<MobaContinuousRuntimeView>)Array.Empty<MobaContinuousRuntimeView>();
        }

        private void SetReason(IContinuous continuous, string lastEvent, string reason, ContinuousEndReason endReason)
        {
            if (continuous == null) return;
            _reasons[continuous] = new MobaContinuousLifecycleReason(lastEvent, reason, endReason);
        }
    }
}
