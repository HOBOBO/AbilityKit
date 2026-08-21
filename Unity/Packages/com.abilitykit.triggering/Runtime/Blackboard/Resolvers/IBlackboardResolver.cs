using System;
using System.Collections.Generic;

namespace AbilityKit.Triggering.Blackboard
{
    public interface IBlackboardResolver
    {
        bool TryResolve(int boardId, out IBlackboard blackboard);
    }

    public interface IMutableBlackboardResolver : IBlackboardResolver
    {
        void Register(int boardId, IBlackboard blackboard);
        bool Unregister(int boardId);
    }

    public interface IOwnerBlackboardStore
    {
        IBlackboardResolver GetOrCreate(long ownerKey);
        bool TryGet(long ownerKey, out IBlackboardResolver resolver);
        bool Release(long ownerKey);
        void Configure(IEnumerable<BlackboardInitializationPlan> plans, bool releaseExisting = true);
    }

    public interface IOwnerBlackboardSnapshotStore : IOwnerBlackboardStore
    {
        bool TryCaptureSnapshot(long ownerKey, out BlackboardSnapshot snapshot, out string error);
        bool TryRestoreSnapshot(long ownerKey, BlackboardSnapshot snapshot, out string error);
        bool TryCaptureSnapshotSet(out BlackboardSnapshotSet snapshotSet, out string error);
        bool TryRestoreSnapshotSet(BlackboardSnapshotSet snapshotSet, out string error);
    }

    public interface IOwnerBlackboardSnapshotValidator
    {
        bool TryValidateSnapshotSet(BlackboardSnapshotSet snapshotSet, out string error);
    }

    public sealed class CompositeBlackboardResolver : IBlackboardResolver, IBlackboardSnapshotResolver
    {
        private readonly IBlackboardResolver _primary;
        private readonly IBlackboardResolver _fallback;

        public CompositeBlackboardResolver(IBlackboardResolver primary, IBlackboardResolver fallback)
        {
            _primary = primary ?? throw new ArgumentNullException(nameof(primary));
            _fallback = fallback;
        }

        public bool TryResolve(int boardId, out IBlackboard blackboard)
        {
            if (_primary.TryResolve(boardId, out blackboard)) return true;
            if (_fallback != null && _fallback.TryResolve(boardId, out blackboard)) return true;
            blackboard = null;
            return false;
        }

        public bool TryCaptureSnapshot(int boardId, out BlackboardSnapshotBoard snapshot, out string error)
        {
            if (_primary.TryResolve(boardId, out var blackboard) && blackboard is IBlackboardSnapshotParticipant participant)
                return participant.TryCaptureSnapshot(boardId, out snapshot, out error);
            snapshot = null;
            error = $"Primary Blackboard board {boardId} does not support snapshots.";
            return false;
        }

        public bool ValidateSnapshot(BlackboardSnapshotBoard snapshot, out string error)
        {
            if (snapshot != null && _primary.TryResolve(snapshot.BoardId, out var blackboard) && blackboard is IBlackboardSnapshotParticipant participant)
                return participant.ValidateSnapshot(snapshot, out error);
            error = $"Primary Blackboard board does not support snapshots. boardId={snapshot?.BoardId}.";
            return false;
        }

        public bool TryRestoreSnapshot(BlackboardSnapshotBoard snapshot, out string error)
        {
            if (snapshot != null && _primary.TryResolve(snapshot.BoardId, out var blackboard) && blackboard is IBlackboardSnapshotParticipant participant)
                return participant.TryRestoreSnapshot(snapshot, out error);
            error = $"Primary Blackboard board does not support snapshots. boardId={snapshot?.BoardId}.";
            return false;
        }
    }

    public interface IBlackboardSnapshotResolver
    {
        bool TryCaptureSnapshot(int boardId, out BlackboardSnapshotBoard snapshot, out string error);
        bool ValidateSnapshot(BlackboardSnapshotBoard snapshot, out string error);
        bool TryRestoreSnapshot(BlackboardSnapshotBoard snapshot, out string error);
    }

    public sealed class OwnerBlackboardStore : IOwnerBlackboardSnapshotStore, IOwnerBlackboardSnapshotValidator
    {
        private readonly IBlackboardResolver _fallback;
        private readonly Dictionary<long, IBlackboardResolver> _owners =
            new Dictionary<long, IBlackboardResolver>();
        private List<BlackboardInitializationPlan> _plans =
            new List<BlackboardInitializationPlan>();

        public OwnerBlackboardStore(IBlackboardResolver fallback = null)
        {
            _fallback = fallback;
        }

        public IBlackboardResolver GetOrCreate(long ownerKey)
        {
            if (ownerKey == 0) throw new ArgumentOutOfRangeException(nameof(ownerKey));
            if (_owners.TryGetValue(ownerKey, out var resolver)) return resolver;

            var local = new DictionaryBlackboardResolver(_plans.Count);
            BlackboardInitialization.Apply(
                _plans,
                local,
                BlackboardInitializationScopes.Owner,
                replaceExisting: true);
            resolver = new CompositeBlackboardResolver(local, _fallback);
            _owners.Add(ownerKey, resolver);
            return resolver;
        }

        public bool TryGet(long ownerKey, out IBlackboardResolver resolver)
        {
            if (ownerKey == 0)
            {
                resolver = null;
                return false;
            }

            return _owners.TryGetValue(ownerKey, out resolver);
        }

        public bool Release(long ownerKey)
        {
            return ownerKey != 0 && _owners.Remove(ownerKey);
        }

        public void Configure(IEnumerable<BlackboardInitializationPlan> plans, bool releaseExisting = true)
        {
            var next = new List<BlackboardInitializationPlan>();
            if (plans != null)
            {
                foreach (var plan in plans)
                {
                    if (plan != null && BlackboardInitializationScopes.IsOwner(plan.Scope))
                        next.Add(plan);
                }
            }

            _plans = next;
            if (releaseExisting) _owners.Clear();
        }

        public bool TryCaptureSnapshot(long ownerKey, out BlackboardSnapshot snapshot, out string error)
        {
            snapshot = null;
            error = null;
            if (ownerKey == 0 || !_owners.TryGetValue(ownerKey, out var resolver))
            {
                error = $"Owner Blackboard resolver was not found. ownerKey={ownerKey}.";
                return false;
            }
            var result = new BlackboardSnapshot { OwnerKey = ownerKey };
            for (var i = 0; i < _plans.Count; i++)
            {
                var plan = _plans[i];
                if (!resolver.TryResolve(plan.BoardId, out _) ||
                    !(resolver is IBlackboardSnapshotResolver snapshotResolver) ||
                    !snapshotResolver.TryCaptureSnapshot(plan.BoardId, out var board, out error))
                {
                    error = error ?? $"Owner Blackboard board could not be snapshotted. boardId={plan.BoardId}.";
                    return false;
                }
                result.Boards.Add(board);
            }
            result.Boards.Sort((left, right) => left.BoardId.CompareTo(right.BoardId));
            error = null;
            snapshot = result;
            return true;
        }

        public bool TryRestoreSnapshot(long ownerKey, BlackboardSnapshot snapshot, out string error)
        {
            error = null;
            if (snapshot == null)
            {
                error = "Blackboard snapshot is required.";
                return false;
            }
            if (snapshot.Version != BlackboardSnapshot.CurrentVersion)
            {
                error = $"Unsupported Blackboard snapshot version {snapshot.Version}. expected={BlackboardSnapshot.CurrentVersion}.";
                return false;
            }
            if (snapshot.OwnerKey != ownerKey)
            {
                error = $"Blackboard snapshot owner mismatch. expected={ownerKey} actual={snapshot.OwnerKey}.";
                return false;
            }
            if (!_owners.TryGetValue(ownerKey, out var resolver) || !(resolver is IBlackboardSnapshotResolver snapshotResolver))
            {
                error = $"Owner Blackboard resolver was not found or does not support snapshots. ownerKey={ownerKey}.";
                return false;
            }
            var boards = snapshot.Boards ?? new List<BlackboardSnapshotBoard>();
            if (boards.Count != _plans.Count)
            {
                error = $"Blackboard snapshot board count mismatch. expected={_plans.Count} actual={boards.Count}.";
                return false;
            }
            var byId = new Dictionary<int, BlackboardSnapshotBoard>();
            for (var i = 0; i < boards.Count; i++)
            {
                var board = boards[i];
                if (board == null || !byId.TryAdd(board.BoardId, board))
                {
                    error = "Blackboard snapshot contains duplicate or missing board entries.";
                    return false;
                }
            }
            for (var i = 0; i < _plans.Count; i++)
            {
                var plan = _plans[i];
                if (!byId.TryGetValue(plan.BoardId, out var board) || !snapshotResolver.ValidateSnapshot(board, out error))
                {
                    error = error ?? $"Blackboard snapshot is missing boardId={plan.BoardId}.";
                    return false;
                }
            }
            for (var i = 0; i < _plans.Count; i++)
            {
                if (!snapshotResolver.TryRestoreSnapshot(byId[_plans[i].BoardId], out error)) return false;
            }
            error = null;
            return true;
        }

        public bool TryCaptureSnapshotSet(out BlackboardSnapshotSet snapshotSet, out string error)
        {
            snapshotSet = new BlackboardSnapshotSet();
            var ownerKeys = new List<long>(_owners.Keys);
            ownerKeys.Sort();
            for (var i = 0; i < ownerKeys.Count; i++)
            {
                if (!TryCaptureSnapshot(ownerKeys[i], out var snapshot, out error))
                {
                    snapshotSet = null;
                    return false;
                }
                snapshotSet.Owners.Add(snapshot);
            }
            error = null;
            return true;
        }

        public bool TryRestoreSnapshotSet(BlackboardSnapshotSet snapshotSet, out string error)
        {
            if (!TryValidateSnapshotSet(snapshotSet, out error)) return false;

            var byOwner = new Dictionary<long, BlackboardSnapshot>();
            var snapshots = snapshotSet.Owners ?? new List<BlackboardSnapshot>();
            for (var i = 0; i < snapshots.Count; i++) byOwner.Add(snapshots[i].OwnerKey, snapshots[i]);
            foreach (var owner in byOwner)
            {
                if (!TryRestoreSnapshot(owner.Key, owner.Value, out error)) return false;
            }
            error = null;
            return true;
        }

        public bool TryValidateSnapshotSet(BlackboardSnapshotSet snapshotSet, out string error)
        {
            error = null;
            if (snapshotSet == null)
            {
                error = "Blackboard snapshot set is required.";
                return false;
            }
            if (snapshotSet.Version != BlackboardSnapshotSet.CurrentVersion)
            {
                error = $"Unsupported Blackboard snapshot set version {snapshotSet.Version}. expected={BlackboardSnapshotSet.CurrentVersion}.";
                return false;
            }

            var snapshots = snapshotSet.Owners ?? new List<BlackboardSnapshot>();
            if (snapshots.Count != _owners.Count)
            {
                error = $"Active Blackboard owner set mismatch. expected={_owners.Count} actual={snapshots.Count}.";
                return false;
            }

            var byOwner = new Dictionary<long, BlackboardSnapshot>();
            for (var i = 0; i < snapshots.Count; i++)
            {
                var snapshot = snapshots[i];
                if (snapshot == null || snapshot.Version != BlackboardSnapshot.CurrentVersion ||
                    !byOwner.TryAdd(snapshot.OwnerKey, snapshot))
                {
                    error = "Blackboard snapshot set contains duplicate, missing, or unsupported owner snapshots.";
                    return false;
                }
                if (!_owners.ContainsKey(snapshot.OwnerKey))
                {
                    error = $"Active Blackboard owner set mismatch. ownerKey={snapshot.OwnerKey} is not active.";
                    return false;
                }
            }

            foreach (var owner in _owners)
            {
                if (!byOwner.ContainsKey(owner.Key))
                {
                    error = $"Active Blackboard owner set mismatch. ownerKey={owner.Key} is missing from snapshot.";
                    return false;
                }
            }

            foreach (var owner in byOwner)
            {
                if (!ValidateSnapshotForOwner(owner.Key, owner.Value, out error)) return false;
            }

            return true;
        }

        private bool ValidateSnapshotForOwner(long ownerKey, BlackboardSnapshot snapshot, out string error)
        {
            error = null;
            if (snapshot.OwnerKey != ownerKey)
            {
                error = $"Blackboard snapshot owner mismatch. expected={ownerKey} actual={snapshot.OwnerKey}.";
                return false;
            }
            if (!_owners.TryGetValue(ownerKey, out var resolver) || !(resolver is IBlackboardSnapshotResolver snapshotResolver))
            {
                error = $"Owner Blackboard resolver was not found or does not support snapshots. ownerKey={ownerKey}.";
                return false;
            }
            var boards = snapshot.Boards ?? new List<BlackboardSnapshotBoard>();
            if (boards.Count != _plans.Count)
            {
                error = $"Blackboard snapshot board count mismatch. ownerKey={ownerKey} expected={_plans.Count} actual={boards.Count}.";
                return false;
            }
            var byId = new Dictionary<int, BlackboardSnapshotBoard>();
            for (var i = 0; i < boards.Count; i++)
            {
                var board = boards[i];
                if (board == null || !byId.TryAdd(board.BoardId, board))
                {
                    error = $"Blackboard snapshot contains duplicate or missing board entries. ownerKey={ownerKey}.";
                    return false;
                }
            }
            for (var i = 0; i < _plans.Count; i++)
            {
                var plan = _plans[i];
                if (!byId.TryGetValue(plan.BoardId, out var board) || !snapshotResolver.ValidateSnapshot(board, out error))
                {
                    error = error ?? $"Blackboard snapshot is missing boardId={plan.BoardId}. ownerKey={ownerKey}.";
                    return false;
                }
            }
            error = null;
            return true;
        }
    }
}
