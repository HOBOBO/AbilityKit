using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace AbilityKit.Triggering.Blackboard
{
    /// <summary>Explicit opt-in; the ordinary snapshot API still requires identical membership.</summary>
    public interface IOwnerBlackboardMembershipSnapshotStore
    {
        bool TryPrepareMembershipRestore(BlackboardSnapshotSet snapshot, IReadOnlyCollection<long> mutableOwnerKeys,
            out IOwnerBlackboardMembershipRestorePlan plan, out string error);
    }

    public interface IOwnerBlackboardMembershipRestorePlan
    {
        bool TryApply(out string error);
    }

    public sealed partial class OwnerBlackboardStore
    {
        public bool TryPrepareMembershipRestore(BlackboardSnapshotSet snapshot, IReadOnlyCollection<long> mutableOwnerKeys,
            out IOwnerBlackboardMembershipRestorePlan plan, out string error)
        {
            plan = null;
            error = null;
            if (snapshot == null || snapshot.Version != BlackboardSnapshotSet.CurrentVersion || mutableOwnerKeys == null)
            {
                error = "Invalid Blackboard membership restore request.";
                return false;
            }

            // Detach mutable DTOs; the caller cannot alter a prepared plan through its input.
            var target = BlackboardSnapshotSet.FromJson(snapshot.ToJson());
            var allowed = new HashSet<long>(mutableOwnerKeys);
            if (allowed.Contains(0)) { error = "Owner key zero is invalid."; return false; }
            var targetKeys = new HashSet<long>();
            var staged = new OwnerBlackboardStore(_fallback);
            staged.Configure(_plans);
            foreach (var owner in target.Owners ?? new List<BlackboardSnapshot>())
            {
                if (owner == null || owner.OwnerKey == 0 || owner.Version != BlackboardSnapshot.CurrentVersion ||
                    !targetKeys.Add(owner.OwnerKey))
                {
                    error = "Duplicate, invalid, or unsupported Blackboard owner snapshot.";
                    return false;
                }
                if (!_owners.ContainsKey(owner.OwnerKey) && !allowed.Contains(owner.OwnerKey))
                {
                    error = $"Membership restore cannot create protected owner {owner.OwnerKey}.";
                    return false;
                }

                // Validate all target boards against the configured template, even for surviving owners.
                staged.GetOrCreate(owner.OwnerKey);
                if (!staged.TryRestoreSnapshot(owner.OwnerKey, owner, out error)) return false;
                if (_owners.ContainsKey(owner.OwnerKey) && !ValidateSnapshotForOwner(owner.OwnerKey, owner, out error))
                    return false;
            }
            foreach (var key in _owners.Keys)
                if (!targetKeys.Contains(key) && !allowed.Contains(key))
                {
                    error = $"Membership restore cannot remove protected owner {key}.";
                    return false;
                }

            plan = new MembershipRestorePlan(this, target, staged);
            return true;
        }

        private sealed class MembershipRestorePlan : IOwnerBlackboardMembershipRestorePlan
        {
            private readonly OwnerBlackboardStore _store;
            private readonly BlackboardSnapshotSet _target;
            private readonly OwnerBlackboardStore _staged;
            private readonly Dictionary<long, IBlackboardResolver> _originalOwners;
            private readonly List<BlackboardInitializationPlan> _originalPlans;
            private readonly string _originalTemplate;
            private bool _consumed;

            public MembershipRestorePlan(OwnerBlackboardStore store, BlackboardSnapshotSet target, OwnerBlackboardStore staged)
            {
                _store = store;
                _target = target;
                _staged = staged;
                _originalOwners = new Dictionary<long, IBlackboardResolver>(store._owners);
                _originalPlans = store._plans;
                _originalTemplate = JsonConvert.SerializeObject(store._plans);
            }

            public bool TryApply(out string error)
            {
                error = null;
                if (_consumed || !ReferenceEquals(_originalPlans, _store._plans) ||
                    !string.Equals(_originalTemplate, JsonConvert.SerializeObject(_store._plans), StringComparison.Ordinal) ||
                    _originalOwners.Count != _store._owners.Count)
                {
                    error = "Blackboard membership restore plan is consumed or stale.";
                    return false;
                }
                _consumed = true;
                foreach (var pair in _originalOwners)
                    if (!_store._owners.TryGetValue(pair.Key, out var current) || !ReferenceEquals(current, pair.Value))
                    {
                        error = $"Blackboard membership changed after prepare. owner={pair.Key}.";
                        return false;
                    }

                // Capture immediately before mutation, allowing value changes between prepare and apply.
                if (!_store.TryCaptureSnapshotSet(out var backup, out error)) return false;
                try
                {
                    foreach (var owner in _target.Owners ?? new List<BlackboardSnapshot>())
                    {
                        if (_originalOwners.ContainsKey(owner.OwnerKey) &&
                            !_store.TryRestoreSnapshot(owner.OwnerKey, owner, out error))
                            throw new InvalidOperationException(error);
                    }
                    // Keep surviving resolvers and boards: live subscriptions may hold their references.
                    foreach (var key in _originalOwners.Keys)
                        if (!_staged._owners.ContainsKey(key)) _store._owners.Remove(key);
                    foreach (var pair in _staged._owners)
                        if (!_store._owners.ContainsKey(pair.Key)) _store._owners.Add(pair.Key, pair.Value);
                    return true;
                }
                catch (Exception failure)
                {
                    _store._owners.Clear();
                    foreach (var pair in _originalOwners) _store._owners.Add(pair.Key, pair.Value);
                    if (!_store.TryRestoreSnapshotSet(backup, out var compensationError))
                        throw new InvalidOperationException("Blackboard membership restore and compensation failed: " + compensationError, failure);
                    error = "Blackboard membership restore failed; previous state restored. " + failure.Message;
                    return false;
                }
            }
        }
    }
}
