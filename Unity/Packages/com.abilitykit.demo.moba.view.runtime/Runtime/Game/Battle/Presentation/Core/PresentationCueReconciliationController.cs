using System;
using System.Collections.Generic;

namespace AbilityKit.Game.Flow
{
    public enum PresentationCueSignal
    {
        None = 0,
        Activate = 1,
        Refresh = 2,
        Deactivate = 3,
    }

    public enum PresentationCuePredictionOutcome
    {
        None = 0,
        Predicted = 1,
        Confirmed = 2,
        Corrected = 3,
        Rejected = 4,
    }

    public enum PresentationCueCommandKind
    {
        None = 0,
        EnsureActive = 1,
        Stop = 2,
    }

    public enum PresentationCueReconciliationRejectReason
    {
        None = 0,
        EmptyKey = 1,
        GenerationMismatch = 2,
        StaleRevision = 3,
        SupersededPrediction = 4,
    }

    public readonly struct PresentationCueUpdate<TKey, TPayload>
    {
        public PresentationCueUpdate(
            TKey key,
            PresentationCueSignal signal,
            PresentationCuePredictionOutcome outcome,
            int revision,
            long generation,
            bool hasVisual,
            TPayload payload)
        {
            Key = key;
            Signal = signal;
            Outcome = outcome;
            Revision = revision;
            Generation = generation;
            HasVisual = hasVisual;
            Payload = payload;
        }

        public TKey Key { get; }

        public PresentationCueSignal Signal { get; }

        public PresentationCuePredictionOutcome Outcome { get; }

        public int Revision { get; }

        public long Generation { get; }

        public bool HasVisual { get; }

        public TPayload Payload { get; }
    }

    public readonly struct PresentationCueReconciliationDecision<TKey, TPayload>
    {
        public PresentationCueReconciliationDecision(
            bool accepted,
            PresentationCueCommandKind command,
            PresentationCueReconciliationRejectReason rejectReason,
            TKey key,
            TPayload payload,
            bool duplicate,
            bool promoted)
        {
            Accepted = accepted;
            Command = command;
            RejectReason = rejectReason;
            Key = key;
            Payload = payload;
            IsDuplicate = duplicate;
            IsPromotion = promoted;
        }

        public bool Accepted { get; }

        public PresentationCueCommandKind Command { get; }

        public PresentationCueReconciliationRejectReason RejectReason { get; }

        public TKey Key { get; }

        public TPayload Payload { get; }

        public bool IsDuplicate { get; }

        public bool IsPromotion { get; }
    }

    public readonly struct PresentationCueGenerationChange<TKey>
    {
        public PresentationCueGenerationChange(
            bool applied,
            long previousGeneration,
            long currentGeneration,
            TKey[] keysToStop)
        {
            Applied = applied;
            PreviousGeneration = previousGeneration;
            CurrentGeneration = currentGeneration;
            KeysToStop = keysToStop ?? Array.Empty<TKey>();
        }

        public bool Applied { get; }

        public long PreviousGeneration { get; }

        public long CurrentGeneration { get; }

        public IReadOnlyList<TKey> KeysToStop { get; }
    }

    /// <summary>
    /// Reconciles platform-neutral presentation intent. Commands are deliberately
    /// idempotent so a platform adapter can retry a failed spawn or destroy safely.
    /// </summary>
    public sealed class PresentationCueReconciliationController<TKey, TPayload>
    {
        private sealed class Entry
        {
            public bool HasLastUpdate;
            public PresentationCueSignal LastSignal;
            public PresentationCuePredictionOutcome LastOutcome;
            public int LastRevision;
            public bool HasAuthorityOutcome;
            public int LatestAuthorityRevision;
            public PresentationCuePredictionOutcome LatestAuthorityOutcome;
        }

        private readonly int _capacity;
        private readonly IEqualityComparer<TKey> _comparer;
        private readonly Dictionary<TKey, Entry> _entries;
        private readonly Queue<TKey> _receiptOrder;
        private readonly HashSet<TKey> _activeKeys;

        public PresentationCueReconciliationController(
            int capacity = 1024,
            long initialGeneration = 1,
            IEqualityComparer<TKey> comparer = null)
        {
            _capacity = capacity > 0 ? capacity : 1024;
            _comparer = comparer ?? EqualityComparer<TKey>.Default;
            _entries = new Dictionary<TKey, Entry>(_comparer);
            _receiptOrder = new Queue<TKey>(Math.Min(_capacity, 32));
            _activeKeys = new HashSet<TKey>(_comparer);
            CurrentGeneration = initialGeneration > 0 ? initialGeneration : 1;
        }

        public long CurrentGeneration { get; private set; }

        public long ConfirmedCount { get; private set; }

        public long CorrectedCount { get; private set; }

        public long RejectedCount { get; private set; }

        public long DuplicateUpdateCount { get; private set; }

        public long StaleUpdateCount { get; private set; }

        public long GenerationMismatchCount { get; private set; }

        public int ActiveCount => _activeKeys.Count;

        public PresentationCueReconciliationDecision<TKey, TPayload> Process(
            in PresentationCueUpdate<TKey, TPayload> update)
        {
            if (_comparer.Equals(update.Key, default(TKey)))
            {
                return Reject(in update, PresentationCueReconciliationRejectReason.EmptyKey);
            }

            if (update.Generation != CurrentGeneration)
            {
                GenerationMismatchCount++;
                return Reject(in update, PresentationCueReconciliationRejectReason.GenerationMismatch);
            }

            var entry = GetOrCreateEntry(update.Key);
            var isAuthorityOutcome = IsAuthorityOutcome(update.Outcome);
            if (update.Outcome == PresentationCuePredictionOutcome.Predicted
                && entry.HasAuthorityOutcome)
            {
                StaleUpdateCount++;
                return Reject(in update, PresentationCueReconciliationRejectReason.SupersededPrediction);
            }

            if (isAuthorityOutcome && IsStaleAuthorityOutcome(entry, in update))
            {
                StaleUpdateCount++;
                return Reject(in update, PresentationCueReconciliationRejectReason.StaleRevision);
            }

            var previousOutcome = entry.HasLastUpdate
                ? entry.LastOutcome
                : PresentationCuePredictionOutcome.None;
            var duplicate = isAuthorityOutcome
                ? entry.HasAuthorityOutcome
                    && entry.LatestAuthorityRevision == update.Revision
                    && entry.LatestAuthorityOutcome == update.Outcome
                : entry.HasLastUpdate
                    && update.Revision > 0
                    && entry.LastRevision == update.Revision
                    && entry.LastSignal == update.Signal
                    && entry.LastOutcome == update.Outcome;
            if (duplicate)
            {
                DuplicateUpdateCount++;
            }
            else if (isAuthorityOutcome)
            {
                CountAuthorityOutcome(update.Outcome);
            }

            if (isAuthorityOutcome && !duplicate)
            {
                entry.HasAuthorityOutcome = true;
                entry.LatestAuthorityRevision = update.Revision;
                entry.LatestAuthorityOutcome = update.Outcome;
            }

            entry.HasLastUpdate = true;
            entry.LastSignal = update.Signal;
            entry.LastOutcome = update.Outcome;
            entry.LastRevision = update.Revision;

            if (update.Outcome == PresentationCuePredictionOutcome.Rejected
                || update.Signal == PresentationCueSignal.Deactivate)
            {
                _activeKeys.Remove(update.Key);
                return Accept(in update, PresentationCueCommandKind.Stop, duplicate, false);
            }

            if ((update.Signal == PresentationCueSignal.Activate
                    || update.Signal == PresentationCueSignal.Refresh)
                && update.HasVisual)
            {
                var wasActive = _activeKeys.Contains(update.Key);
                _activeKeys.Add(update.Key);
                var promoted = wasActive
                    && previousOutcome == PresentationCuePredictionOutcome.Predicted
                    && (update.Outcome == PresentationCuePredictionOutcome.Confirmed
                        || update.Outcome == PresentationCuePredictionOutcome.Corrected);
                return Accept(
                    in update,
                    PresentationCueCommandKind.EnsureActive,
                    duplicate,
                    promoted);
            }

            return Accept(in update, PresentationCueCommandKind.None, duplicate, false);
        }

        public PresentationCueGenerationChange<TKey> BeginGeneration(long generation)
        {
            if (generation <= CurrentGeneration)
            {
                return new PresentationCueGenerationChange<TKey>(
                    false,
                    CurrentGeneration,
                    CurrentGeneration,
                    Array.Empty<TKey>());
            }

            var previousGeneration = CurrentGeneration;
            var keysToStop = new TKey[_activeKeys.Count];
            _activeKeys.CopyTo(keysToStop);
            _activeKeys.Clear();
            _entries.Clear();
            _receiptOrder.Clear();
            CurrentGeneration = generation;

            return new PresentationCueGenerationChange<TKey>(
                true,
                previousGeneration,
                generation,
                keysToStop);
        }

        private Entry GetOrCreateEntry(TKey key)
        {
            if (_entries.TryGetValue(key, out var entry)) return entry;

            while (_entries.Count >= _capacity && _receiptOrder.Count > 0)
            {
                _entries.Remove(_receiptOrder.Dequeue());
            }

            entry = new Entry();
            _entries.Add(key, entry);
            _receiptOrder.Enqueue(key);
            return entry;
        }

        private void CountAuthorityOutcome(PresentationCuePredictionOutcome outcome)
        {
            switch (outcome)
            {
                case PresentationCuePredictionOutcome.Confirmed:
                    ConfirmedCount++;
                    break;
                case PresentationCuePredictionOutcome.Corrected:
                    CorrectedCount++;
                    break;
                case PresentationCuePredictionOutcome.Rejected:
                    RejectedCount++;
                    break;
            }
        }

        private static bool IsAuthorityOutcome(PresentationCuePredictionOutcome outcome)
        {
            return outcome == PresentationCuePredictionOutcome.Confirmed
                || outcome == PresentationCuePredictionOutcome.Corrected
                || outcome == PresentationCuePredictionOutcome.Rejected;
        }

        private static bool IsStaleAuthorityOutcome(
            Entry entry,
            in PresentationCueUpdate<TKey, TPayload> update)
        {
            if (!entry.HasAuthorityOutcome) return false;
            if (entry.LatestAuthorityRevision > 0 && update.Revision <= 0) return true;
            if (entry.LatestAuthorityRevision > update.Revision) return true;
            if (entry.LatestAuthorityRevision != update.Revision) return false;

            return AuthorityOutcomeRank(entry.LatestAuthorityOutcome)
                > AuthorityOutcomeRank(update.Outcome);
        }

        private static int AuthorityOutcomeRank(PresentationCuePredictionOutcome outcome)
        {
            switch (outcome)
            {
                case PresentationCuePredictionOutcome.Confirmed:
                    return 1;
                case PresentationCuePredictionOutcome.Corrected:
                    return 2;
                case PresentationCuePredictionOutcome.Rejected:
                    return 3;
                default:
                    return 0;
            }
        }

        private static PresentationCueReconciliationDecision<TKey, TPayload> Accept(
            in PresentationCueUpdate<TKey, TPayload> update,
            PresentationCueCommandKind command,
            bool duplicate,
            bool promoted)
        {
            return new PresentationCueReconciliationDecision<TKey, TPayload>(
                true,
                command,
                PresentationCueReconciliationRejectReason.None,
                update.Key,
                update.Payload,
                duplicate,
                promoted);
        }

        private static PresentationCueReconciliationDecision<TKey, TPayload> Reject(
            in PresentationCueUpdate<TKey, TPayload> update,
            PresentationCueReconciliationRejectReason reason)
        {
            return new PresentationCueReconciliationDecision<TKey, TPayload>(
                false,
                PresentationCueCommandKind.None,
                reason,
                update.Key,
                update.Payload,
                false,
                false);
        }
    }
}
