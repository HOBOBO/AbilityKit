using System.Collections.Generic;
using AbilityKit.Ability.World.DI;
using AbilityKit.Core.Logging;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;
using AbilityKit.Continuous;
using AbilityKit.GameplayTags;

namespace AbilityKit.Demo.Moba.Services
{
    public interface IMobaContinuousTagRuleService
    {
        bool CanActivate(IContinuous continuous, IContinuousManager manager, out string reason);
        void ReconcileOwner(int ownerActorId);
        void ReconcileOwnerFor(IContinuous continuous);
        MobaContinuousTagRuleResult Explain(IContinuous continuous);
        MobaContinuousTagRuleResult GetLastResult(IContinuous continuous);
    }

    public enum MobaContinuousTagRuleAction
    {
        None,
        CanActivate,
        BlockActivate,
        KeepActive,
        Pause,
        Resume,
        Remove,
    }

    public readonly struct MobaContinuousTagRuleTagView
    {
        public MobaContinuousTagRuleTagView(int tagId, string tagName)
        {
            TagId = tagId;
            TagName = tagName;
        }

        public int TagId { get; }
        public string TagName { get; }
    }

    public readonly struct MobaContinuousTagRequirementCheck
    {
        public MobaContinuousTagRequirementCheck(
            string category,
            bool satisfied,
            bool requiredSatisfied,
            bool blockedSatisfied,
            bool exact,
            IReadOnlyList<MobaContinuousTagRuleTagView> requiredTags,
            IReadOnlyList<MobaContinuousTagRuleTagView> blockedTags)
        {
            Category = category;
            Satisfied = satisfied;
            RequiredSatisfied = requiredSatisfied;
            BlockedSatisfied = blockedSatisfied;
            Exact = exact;
            RequiredTags = requiredTags ?? new List<MobaContinuousTagRuleTagView>(0);
            BlockedTags = blockedTags ?? new List<MobaContinuousTagRuleTagView>(0);
        }

        public string Category { get; }
        public bool Satisfied { get; }
        public bool RequiredSatisfied { get; }
        public bool BlockedSatisfied { get; }
        public bool Exact { get; }
        public IReadOnlyList<MobaContinuousTagRuleTagView> RequiredTags { get; }
        public IReadOnlyList<MobaContinuousTagRuleTagView> BlockedTags { get; }
    }

    public readonly struct MobaContinuousTagRuleResult
    {
        public MobaContinuousTagRuleResult(
            int ownerActorId,
            MobaContinuousTagRuleAction action,
            bool allowed,
            string reason,
            IReadOnlyList<MobaContinuousTagRuleTagView> effectiveTags,
            IReadOnlyList<MobaContinuousTagRequirementCheck> checks)
        {
            OwnerActorId = ownerActorId;
            Action = action;
            Allowed = allowed;
            Reason = reason;
            EffectiveTags = effectiveTags ?? new List<MobaContinuousTagRuleTagView>(0);
            Checks = checks ?? new List<MobaContinuousTagRequirementCheck>(0);
        }

        public int OwnerActorId { get; }
        public MobaContinuousTagRuleAction Action { get; }
        public bool Allowed { get; }
        public string Reason { get; }
        public IReadOnlyList<MobaContinuousTagRuleTagView> EffectiveTags { get; }
        public IReadOnlyList<MobaContinuousTagRequirementCheck> Checks { get; }
        public bool HasResult => Action != MobaContinuousTagRuleAction.None || !string.IsNullOrEmpty(Reason);

        public static MobaContinuousTagRuleResult None => default;
    }

    [WorldService(typeof(IMobaContinuousTagRuleService))]
    [WorldService(typeof(MobaContinuousTagRuleService))]
    public sealed class MobaContinuousTagRuleService : IMobaContinuousTagRuleService, IContinuousAdmissionPolicy, IContinuousLifecycleBinder, IWorldInitializable, IService
    {
        private const int MaxReconcilePasses = 8;

        private readonly HashSet<IContinuous> _pausedByTags = new HashSet<IContinuous>();
        private readonly Dictionary<IContinuous, MobaContinuousTagRuleResult> _lastResults = new Dictionary<IContinuous, MobaContinuousTagRuleResult>();
        private bool _reconciling;

        private IMobaEffectiveTagQueryService _effectiveTags;
        private MobaContinuousManager _continuous;
        private IMobaContinuousTagRuleEvaluator _evaluator;

        public void OnInit(IWorldResolver services)
        {
            if (services == null) return;

            services.TryResolve(out _effectiveTags);
            services.TryResolve(out _continuous);
            services.TryResolve(out _evaluator);

            _continuous?.AddAdmissionPolicy(this);
            _continuous?.AddLifecycleBinder(this);
        }

        public void Dispose()
        {
            if (_continuous != null)
            {
                _continuous.RemoveAdmissionPolicy(this);
                _continuous.RemoveLifecycleBinder(this);
            }

            _pausedByTags.Clear();
            _lastResults.Clear();
            _effectiveTags = null;
            _continuous = null;
            _evaluator = null;
            _reconciling = false;
        }

        public bool CanRegister(IContinuous continuous, IContinuousManager manager, out string reason)
        {
            reason = null;
            return continuous != null && continuous.Config != null;
        }

        public bool CanActivate(IContinuous continuous, IContinuousManager manager, out string reason)
        {
            reason = null;
            var ownerActorId = ResolveOwnerActorId(continuous);
            var continuousType = continuous?.GetType().Name ?? "<null>";
            if (_evaluator == null)
            {
                reason = "Tag rule evaluator is unavailable";
                var unavailableResult = new MobaContinuousTagRuleResult(ownerActorId, MobaContinuousTagRuleAction.BlockActivate, false, reason, null, null);
                SetResult(continuous, unavailableResult);
                Log.Warning($"[MobaContinuousTagRuleService] CanActivate rejected. ownerActorId={ownerActorId} continuousType={continuousType} reason={reason}");
                return false;
            }

            var allowed = _evaluator.CanActivate(continuous, out reason);
            var evaluation = _evaluator.Evaluate(continuous);
            SetResult(continuous, evaluation);
            Log.Warning($"[MobaContinuousTagRuleService] CanActivate evaluated. ownerActorId={ownerActorId} continuousType={continuousType} allowed={allowed} reason={reason ?? "<none>"} action={evaluation.Action} resultAllowed={evaluation.Allowed} resultReason={evaluation.Reason ?? "<none>"}");
            return allowed;
        }

        public MobaContinuousTagRuleResult Explain(IContinuous continuous)
        {
            if (_evaluator == null)
                return new MobaContinuousTagRuleResult(ResolveOwnerActorId(continuous), MobaContinuousTagRuleAction.None, false, "Tag rule evaluator is unavailable", null, null);

            return _evaluator.Evaluate(continuous);
        }

        public MobaContinuousTagRuleResult GetLastResult(IContinuous continuous)
        {
            if (continuous == null) return MobaContinuousTagRuleResult.None;
            return _lastResults.TryGetValue(continuous, out var result) ? result : MobaContinuousTagRuleResult.None;
        }

        public void ReconcileOwnerFor(IContinuous continuous)
        {
            var ownerActorId = ResolveOwnerActorId(continuous);
            if (ownerActorId > 0)
                ReconcileOwner(ownerActorId);
        }

        public void ReconcileOwner(int ownerActorId)
        {
            if (ownerActorId <= 0 || _continuous == null || _effectiveTags == null) return;
            if (_reconciling) return;

            _reconciling = true;
            try
            {
                for (int pass = 0; pass < MaxReconcilePasses; pass++)
                {
                    _effectiveTags.MarkDirty(ownerActorId);
                    var tags = _effectiveTags.GetEffectiveTags(ownerActorId);
                    var changed = ReconcileOwnerPass(ownerActorId, tags);
                    if (!changed) break;
                }
            }
            finally
            {
                _reconciling = false;
            }
        }

        public void OnRegistered(IContinuous continuous, IContinuousManager manager)
        {
            SetResult(continuous, Explain(continuous));
            ReconcileOwnerFor(continuous);
        }

        public void OnActivated(IContinuous continuous, IContinuousManager manager)
        {
            _pausedByTags.Remove(continuous);
            SetResult(continuous, Explain(continuous));
            ReconcileOwnerFor(continuous);
        }

        public void OnPaused(IContinuous continuous, IContinuousManager manager)
        {
            MarkDirty(continuous);
            SetResult(continuous, Explain(continuous));
        }

        public void OnResumed(IContinuous continuous, IContinuousManager manager)
        {
            _pausedByTags.Remove(continuous);
            SetResult(continuous, Explain(continuous));
            ReconcileOwnerFor(continuous);
        }

        public void OnEnded(IContinuous continuous, ContinuousEndReason reason, IContinuousManager manager)
        {
            _pausedByTags.Remove(continuous);
            SetResult(continuous, Explain(continuous));
            ReconcileOwnerFor(continuous);
        }

        public void OnUnregistered(IContinuous continuous, ContinuousEndReason reason, IContinuousManager manager)
        {
            _pausedByTags.Remove(continuous);
            SetResult(continuous, Explain(continuous));
            ReconcileOwnerFor(continuous);
        }

        private bool ReconcileOwnerPass(int ownerActorId, GameplayTagContainer tags)
        {
            var ownerContinuous = _continuous.GetOwnerContinuous(ownerActorId);
            if (ownerContinuous == null || ownerContinuous.Count == 0) return false;

            var snapshot = new List<IContinuous>(ownerContinuous);
            var changed = false;
            for (int i = 0; i < snapshot.Count; i++)
            {
                var continuous = snapshot[i];
                if (!CanEvaluate(continuous)) continue;

                var result = Explain(continuous);
                if (continuous.IsActive && result.Action == MobaContinuousTagRuleAction.Remove)
                {
                    _pausedByTags.Remove(continuous);
                    SetResult(continuous, result);
                    changed = _continuous.TryInterrupt(continuous, "Removed by MOBA continuous tags") || changed;
                    continue;
                }

                if (continuous.IsActive && result.Action == MobaContinuousTagRuleAction.Pause)
                {
                    SetResult(continuous, result);
                    if (_continuous.TryPause(continuous))
                    {
                        _pausedByTags.Add(continuous);
                        changed = true;
                    }

                    continue;
                }

                if (continuous.IsPaused && _pausedByTags.Contains(continuous))
                {
                    if (result.Action == MobaContinuousTagRuleAction.Resume && _continuous.TryResume(continuous))
                    {
                        _pausedByTags.Remove(continuous);
                        SetResult(continuous, result);
                        changed = true;
                    }
                }
            }

            return changed;
        }

        private void SetResult(IContinuous continuous, MobaContinuousTagRuleResult result)
        {
            if (continuous == null) return;
            _lastResults[continuous] = result;
        }

        private void MarkDirty(IContinuous continuous)
        {
            var ownerActorId = ResolveOwnerActorId(continuous);
            if (ownerActorId > 0)
                _effectiveTags?.MarkDirty(ownerActorId);
        }

        private static bool CanEvaluate(IContinuous continuous)
        {
            return continuous != null && !continuous.IsTerminated && continuous.Config is IMobaContinuousTagConfig;
        }

        private static int ResolveOwnerActorId(IContinuous continuous)
        {
            if (continuous?.Config is IMobaContinuousProjectionConfig projection && projection.OwnerActorId > 0)
                return projection.OwnerActorId;

            var ownerId = continuous?.Config?.OwnerId ?? 0L;
            return ownerId > 0 && ownerId <= int.MaxValue ? (int)ownerId : 0;
        }
    }
}
