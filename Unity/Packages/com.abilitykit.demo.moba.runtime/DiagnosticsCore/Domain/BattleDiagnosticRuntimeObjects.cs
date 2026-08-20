using System;
using System.Collections.Generic;

namespace AbilityKit.Demo.Moba.Diagnostics
{
    public enum BattleDiagnosticRuntimeObjectKind
    {
        Unknown = 0,
        Actor = 1,
        Projectile = 2,
        Area = 3,
        Summon = 4,
    }

    public enum BattleDiagnosticRuntimeObjectState
    {
        Unknown = 0,
        Active = 1,
        Ended = 2,
    }

    public enum BattleDiagnosticRuntimeObjectDiscoveryKind
    {
        Unknown = 0,
        LifecycleCreated = 1,
        ActiveBackfill = 2,
        LifecycleEndedOnly = 3,
    }

    public enum BattleDiagnosticDataCompleteness
    {
        Unknown = 0,
        Complete = 1,
        Partial = 2,
        Unreliable = 3,
    }

    public static class BattleDiagnosticRuntimeObjectCompletenessEvaluator
    {
        public static BattleDiagnosticDataCompleteness Evaluate(
            BattleDiagnosticRuntimeObjectDiscoveryKind discoveryKind)
        {
            switch (discoveryKind)
            {
                case BattleDiagnosticRuntimeObjectDiscoveryKind.LifecycleCreated:
                    return BattleDiagnosticDataCompleteness.Complete;
                case BattleDiagnosticRuntimeObjectDiscoveryKind.ActiveBackfill:
                    return BattleDiagnosticDataCompleteness.Partial;
                case BattleDiagnosticRuntimeObjectDiscoveryKind.LifecycleEndedOnly:
                case BattleDiagnosticRuntimeObjectDiscoveryKind.Unknown:
                default:
                    return BattleDiagnosticDataCompleteness.Unreliable;
            }
        }

        public static BattleDiagnosticDataCompleteness Evaluate(
            in BattleDiagnosticRuntimeObject runtimeObject)
        {
            return Evaluate(runtimeObject.DiscoveryKind);
        }

        public static BattleDiagnosticDataCompleteness Evaluate(
            IReadOnlyList<BattleDiagnosticRuntimeObject> items,
            bool truncated,
            long backfillFailureCount)
        {
            if (backfillFailureCount > 0L) return BattleDiagnosticDataCompleteness.Unreliable;

            var completeness = truncated
                ? BattleDiagnosticDataCompleteness.Partial
                : BattleDiagnosticDataCompleteness.Complete;
            if (items == null) return completeness;

            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                var itemCompleteness = Evaluate(in item);
                if (itemCompleteness == BattleDiagnosticDataCompleteness.Unreliable)
                    return itemCompleteness;
                if (itemCompleteness == BattleDiagnosticDataCompleteness.Partial)
                    completeness = BattleDiagnosticDataCompleteness.Partial;
            }

            return completeness;
        }
    }

    public readonly struct BattleDiagnosticRuntimeObjectCatalogSummary
    {
        public BattleDiagnosticRuntimeObjectCatalogSummary(
            int totalCount,
            int completeCount,
            int partialCount,
            int unreliableCount,
            int activeCount,
            int endedCount,
            BattleDiagnosticDataCompleteness completeness,
            bool truncated,
            long backfillAttemptCount,
            long backfillFailureCount,
            int lastBackfillFrame)
        {
            TotalCount = totalCount;
            CompleteCount = completeCount;
            PartialCount = partialCount;
            UnreliableCount = unreliableCount;
            ActiveCount = activeCount;
            EndedCount = endedCount;
            Completeness = completeness;
            Truncated = truncated;
            BackfillAttemptCount = backfillAttemptCount;
            BackfillFailureCount = backfillFailureCount;
            LastBackfillFrame = lastBackfillFrame;
        }

        public int TotalCount { get; }
        public int CompleteCount { get; }
        public int PartialCount { get; }
        public int UnreliableCount { get; }
        public int ActiveCount { get; }
        public int EndedCount { get; }
        public BattleDiagnosticDataCompleteness Completeness { get; }
        public bool Truncated { get; }
        public long BackfillAttemptCount { get; }
        public long BackfillFailureCount { get; }
        public int LastBackfillFrame { get; }

        public static BattleDiagnosticRuntimeObjectCatalogSummary Create(
            IReadOnlyList<BattleDiagnosticRuntimeObject> items,
            bool truncated,
            long backfillAttemptCount,
            long backfillFailureCount,
            int lastBackfillFrame)
        {
            var completeCount = 0;
            var partialCount = 0;
            var unreliableCount = 0;
            var activeCount = 0;
            var endedCount = 0;
            var count = items?.Count ?? 0;
            for (var i = 0; i < count; i++)
            {
                var item = items[i];
                switch (item.Completeness)
                {
                    case BattleDiagnosticDataCompleteness.Complete:
                        completeCount++;
                        break;
                    case BattleDiagnosticDataCompleteness.Partial:
                        partialCount++;
                        break;
                    default:
                        unreliableCount++;
                        break;
                }

                if (item.State == BattleDiagnosticRuntimeObjectState.Active) activeCount++;
                if (item.State == BattleDiagnosticRuntimeObjectState.Ended) endedCount++;
            }

            return new BattleDiagnosticRuntimeObjectCatalogSummary(
                count,
                completeCount,
                partialCount,
                unreliableCount,
                activeCount,
                endedCount,
                BattleDiagnosticRuntimeObjectCompletenessEvaluator.Evaluate(
                    items,
                    truncated,
                    backfillFailureCount),
                truncated,
                backfillAttemptCount,
                backfillFailureCount,
                lastBackfillFrame);
        }
    }

    public readonly struct BattleDiagnosticRuntimeObjectEventCoverageSummary
    {
        public BattleDiagnosticRuntimeObjectEventCoverageSummary(
            int eventCount,
            int referencedEventCount,
            int completeEventCount,
            int partialEventCount,
            int unreliableEventCount,
            int totalReferenceCount,
            int resolvedReferenceCount,
            int unresolvedReferenceCount)
        {
            EventCount = eventCount;
            ReferencedEventCount = referencedEventCount;
            CompleteEventCount = completeEventCount;
            PartialEventCount = partialEventCount;
            UnreliableEventCount = unreliableEventCount;
            TotalReferenceCount = totalReferenceCount;
            ResolvedReferenceCount = resolvedReferenceCount;
            UnresolvedReferenceCount = unresolvedReferenceCount;
        }

        public int EventCount { get; }
        public int ReferencedEventCount { get; }
        public int CompleteEventCount { get; }
        public int PartialEventCount { get; }
        public int UnreliableEventCount { get; }
        public int TotalReferenceCount { get; }
        public int ResolvedReferenceCount { get; }
        public int UnresolvedReferenceCount { get; }
        public float ResolvedReferenceRatio => TotalReferenceCount > 0
            ? (float)ResolvedReferenceCount / TotalReferenceCount
            : 1f;

        public static BattleDiagnosticRuntimeObjectEventCoverageSummary Create(
            IReadOnlyList<BattleDiagnosticEvent> events,
            BattleDiagnosticObjectCatalogSnapshot objects)
        {
            var eventCount = events?.Count ?? 0;
            var referencedEventCount = 0;
            var completeEventCount = 0;
            var partialEventCount = 0;
            var unreliableEventCount = 0;
            var totalReferenceCount = 0;
            var resolvedReferenceCount = 0;
            var unresolvedReferenceCount = 0;
            var exactObjects = new Dictionary<
                BattleDiagnosticRuntimeObjectReference,
                BattleDiagnosticRuntimeObject>(objects?.Items.Count ?? 0);
            if (objects != null)
            {
                for (var i = 0; i < objects.Items.Count; i++)
                {
                    var runtimeObject = objects.Items[i];
                    exactObjects[runtimeObject.Reference] = runtimeObject;
                }
            }
            for (var i = 0; i < eventCount; i++)
            {
                var item = events[i];
                var hasReference = false;
                var completeness = BattleDiagnosticDataCompleteness.Complete;
                var sourceActor = item.SourceActor;
                var targetActor = item.TargetActor;
                var subjectObject = item.SubjectObject;
                EvaluateReference(
                    in sourceActor,
                    item.Frame,
                    objects,
                    exactObjects,
                    ref hasReference,
                    ref completeness,
                    ref totalReferenceCount,
                    ref resolvedReferenceCount,
                    ref unresolvedReferenceCount);
                EvaluateReference(
                    in targetActor,
                    item.Frame,
                    objects,
                    exactObjects,
                    ref hasReference,
                    ref completeness,
                    ref totalReferenceCount,
                    ref resolvedReferenceCount,
                    ref unresolvedReferenceCount);
                EvaluateReference(
                    in subjectObject,
                    item.Frame,
                    objects,
                    exactObjects,
                    ref hasReference,
                    ref completeness,
                    ref totalReferenceCount,
                    ref resolvedReferenceCount,
                    ref unresolvedReferenceCount);
                if (!hasReference) continue;

                referencedEventCount++;
                switch (completeness)
                {
                    case BattleDiagnosticDataCompleteness.Complete:
                        completeEventCount++;
                        break;
                    case BattleDiagnosticDataCompleteness.Partial:
                        partialEventCount++;
                        break;
                    default:
                        unreliableEventCount++;
                        break;
                }
            }

            return new BattleDiagnosticRuntimeObjectEventCoverageSummary(
                eventCount,
                referencedEventCount,
                completeEventCount,
                partialEventCount,
                unreliableEventCount,
                totalReferenceCount,
                resolvedReferenceCount,
                unresolvedReferenceCount);
        }

        private static void EvaluateReference(
            in BattleDiagnosticRuntimeObjectReference reference,
            int frame,
            BattleDiagnosticObjectCatalogSnapshot objects,
            Dictionary<BattleDiagnosticRuntimeObjectReference, BattleDiagnosticRuntimeObject>
                exactObjects,
            ref bool hasReference,
            ref BattleDiagnosticDataCompleteness completeness,
            ref int totalReferenceCount,
            ref int resolvedReferenceCount,
            ref int unresolvedReferenceCount)
        {
            if (!reference.HasRuntimeId) return;
            hasReference = true;
            totalReferenceCount++;
            var runtimeObject = default(BattleDiagnosticRuntimeObject);
            var resolved = reference.IsResolved
                ? exactObjects.TryGetValue(reference, out runtimeObject)
                : objects != null && objects.TryResolve(in reference, frame, out runtimeObject);
            if (resolved)
            {
                resolvedReferenceCount++;
                completeness = Worst(completeness, runtimeObject.Completeness);
                return;
            }

            unresolvedReferenceCount++;
            completeness = BattleDiagnosticDataCompleteness.Unreliable;
        }

        private static BattleDiagnosticDataCompleteness Worst(
            BattleDiagnosticDataCompleteness left,
            BattleDiagnosticDataCompleteness right)
        {
            return (BattleDiagnosticDataCompleteness)Math.Max((int)left, (int)right);
        }
    }

    public readonly struct BattleDiagnosticRuntimeObjectKey :
        IEquatable<BattleDiagnosticRuntimeObjectKey>
    {
        public BattleDiagnosticRuntimeObjectKey(long runtimeId, int generation)
        {
            if (runtimeId == 0L) throw new ArgumentOutOfRangeException(nameof(runtimeId));
            if (generation < 0) throw new ArgumentOutOfRangeException(nameof(generation));
            RuntimeId = runtimeId;
            Generation = generation;
        }

        public long RuntimeId { get; }
        public int Generation { get; }
        public bool IsResolved => RuntimeId != 0L && Generation > 0;

        public bool Equals(BattleDiagnosticRuntimeObjectKey other)
        {
            return RuntimeId == other.RuntimeId && Generation == other.Generation;
        }

        public override bool Equals(object obj)
        {
            return obj is BattleDiagnosticRuntimeObjectKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            return (RuntimeId.GetHashCode() * 397) ^ Generation;
        }

        public static bool operator ==(
            BattleDiagnosticRuntimeObjectKey left,
            BattleDiagnosticRuntimeObjectKey right) => left.Equals(right);

        public static bool operator !=(
            BattleDiagnosticRuntimeObjectKey left,
            BattleDiagnosticRuntimeObjectKey right) => !left.Equals(right);
    }

    public readonly struct BattleDiagnosticRuntimeObjectReference :
        IEquatable<BattleDiagnosticRuntimeObjectReference>
    {
        public BattleDiagnosticRuntimeObjectReference(
            BattleDiagnosticRuntimeObjectKind kind,
            long runtimeId,
            int generation = 0)
        {
            if (kind == BattleDiagnosticRuntimeObjectKind.Unknown)
                throw new ArgumentOutOfRangeException(nameof(kind));
            if (runtimeId == 0L) throw new ArgumentOutOfRangeException(nameof(runtimeId));
            if (generation < 0) throw new ArgumentOutOfRangeException(nameof(generation));

            Kind = kind;
            Key = new BattleDiagnosticRuntimeObjectKey(runtimeId, generation);
        }

        public BattleDiagnosticRuntimeObjectKind Kind { get; }
        public BattleDiagnosticRuntimeObjectKey Key { get; }
        public long RuntimeId => Key.RuntimeId;
        public int Generation => Key.Generation;
        public bool HasRuntimeId => Kind != BattleDiagnosticRuntimeObjectKind.Unknown && RuntimeId != 0L;
        public bool IsResolved => HasRuntimeId && Generation > 0;

        public bool Equals(BattleDiagnosticRuntimeObjectReference other)
        {
            return Kind == other.Kind && Key.Equals(other.Key);
        }

        public override bool Equals(object obj)
        {
            return obj is BattleDiagnosticRuntimeObjectReference other && Equals(other);
        }

        public override int GetHashCode()
        {
            return ((int)Kind * 397) ^ Key.GetHashCode();
        }

        public override string ToString()
        {
            if (!HasRuntimeId) return "<none>";
            return IsResolved
                ? Kind + ":" + RuntimeId + ":" + Generation
                : Kind + ":" + RuntimeId + ":?";
        }

        public static BattleDiagnosticRuntimeObjectReference Create(
            BattleDiagnosticRuntimeObjectKind kind,
            long runtimeId,
            int generation = 0)
        {
            return kind == BattleDiagnosticRuntimeObjectKind.Unknown || runtimeId == 0L
                ? default
                : new BattleDiagnosticRuntimeObjectReference(kind, runtimeId, generation);
        }

        public static bool operator ==(
            BattleDiagnosticRuntimeObjectReference left,
            BattleDiagnosticRuntimeObjectReference right) => left.Equals(right);

        public static bool operator !=(
            BattleDiagnosticRuntimeObjectReference left,
            BattleDiagnosticRuntimeObjectReference right) => !left.Equals(right);
    }

    public readonly struct BattleDiagnosticRuntimeObject
    {
        public BattleDiagnosticRuntimeObject(
            BattleDiagnosticRuntimeObjectKind kind,
            long runtimeId,
            int generation,
            BattleDiagnosticDefinitionKind definitionKind,
            int definitionId,
            long relatedActorId,
            long ownerActorId,
            long sourceActorId,
            long targetActorId,
            int createdFrame,
            int destroyedFrame,
            long rootContextId,
            long contextId,
            BattleDiagnosticRuntimeObjectState state,
            int endReason,
            string displayName,
            BattleDiagnosticRuntimeObjectDiscoveryKind discoveryKind =
                BattleDiagnosticRuntimeObjectDiscoveryKind.Unknown,
            int backfilledFrame = -1)
        {
            if (kind == BattleDiagnosticRuntimeObjectKind.Unknown)
                throw new ArgumentOutOfRangeException(nameof(kind));
            if (runtimeId == 0L) throw new ArgumentOutOfRangeException(nameof(runtimeId));
            if (generation < 1) throw new ArgumentOutOfRangeException(nameof(generation));
            if (createdFrame < -1) throw new ArgumentOutOfRangeException(nameof(createdFrame));
            if (destroyedFrame < -1) throw new ArgumentOutOfRangeException(nameof(destroyedFrame));
            if (backfilledFrame < -1) throw new ArgumentOutOfRangeException(nameof(backfilledFrame));
            if (!Enum.IsDefined(typeof(BattleDiagnosticRuntimeObjectDiscoveryKind), discoveryKind))
                throw new ArgumentOutOfRangeException(nameof(discoveryKind));
            if (discoveryKind == BattleDiagnosticRuntimeObjectDiscoveryKind.ActiveBackfill)
            {
                if (backfilledFrame < 0) throw new ArgumentOutOfRangeException(nameof(backfilledFrame));
            }
            else if (backfilledFrame != -1)
            {
                throw new ArgumentException(
                    "A backfilled frame is only valid for an active-backfill object.",
                    nameof(backfilledFrame));
            }

            Kind = kind;
            RuntimeId = runtimeId;
            Generation = generation;
            DefinitionKind = definitionKind;
            DefinitionId = definitionId;
            RelatedActorId = relatedActorId;
            OwnerActorId = ownerActorId;
            SourceActorId = sourceActorId;
            TargetActorId = targetActorId;
            CreatedFrame = createdFrame;
            DestroyedFrame = destroyedFrame;
            RootContextId = rootContextId;
            ContextId = contextId;
            State = state;
            EndReason = endReason;
            DisplayName = displayName ?? string.Empty;
            DiscoveryKind = discoveryKind;
            BackfilledFrame = backfilledFrame;
        }

        public BattleDiagnosticRuntimeObjectKind Kind { get; }
        public long RuntimeId { get; }
        public int Generation { get; }
        public BattleDiagnosticDefinitionKind DefinitionKind { get; }
        public int DefinitionId { get; }
        public long RelatedActorId { get; }
        public long OwnerActorId { get; }
        public long SourceActorId { get; }
        public long TargetActorId { get; }
        public int CreatedFrame { get; }
        public int DestroyedFrame { get; }
        public long RootContextId { get; }
        public long ContextId { get; }
        public BattleDiagnosticRuntimeObjectState State { get; }
        public int EndReason { get; }
        public string DisplayName { get; }
        public BattleDiagnosticRuntimeObjectDiscoveryKind DiscoveryKind { get; }
        public int BackfilledFrame { get; }
        public bool WasBackfilled =>
            DiscoveryKind == BattleDiagnosticRuntimeObjectDiscoveryKind.ActiveBackfill;
        public BattleDiagnosticDataCompleteness Completeness =>
            BattleDiagnosticRuntimeObjectCompletenessEvaluator.Evaluate(this);
        public BattleDiagnosticRuntimeObjectKey Key =>
            new BattleDiagnosticRuntimeObjectKey(RuntimeId, Generation);
        public BattleDiagnosticRuntimeObjectReference Reference =>
            new BattleDiagnosticRuntimeObjectReference(Kind, RuntimeId, Generation);
    }

    public sealed class BattleDiagnosticObjectCatalogSnapshot
    {
        public BattleDiagnosticObjectCatalogSnapshot(
            BattleDiagnosticSessionScope scope,
            long revision,
            bool truncated,
            IReadOnlyList<BattleDiagnosticRuntimeObject> items,
            long backfillAttemptCount = 0L,
            long backfillFailureCount = 0L,
            int lastBackfillFrame = -1)
        {
            if (!scope.IsValid) throw new ArgumentException("A valid session scope is required.", nameof(scope));
            if (revision < 0L) throw new ArgumentOutOfRangeException(nameof(revision));
            if (backfillAttemptCount < 0L) throw new ArgumentOutOfRangeException(nameof(backfillAttemptCount));
            if (backfillFailureCount < 0L || backfillFailureCount > backfillAttemptCount)
                throw new ArgumentOutOfRangeException(nameof(backfillFailureCount));
            if (lastBackfillFrame < -1) throw new ArgumentOutOfRangeException(nameof(lastBackfillFrame));
            Scope = scope;
            Revision = revision;
            Truncated = truncated;
            Items = items == null
                ? Array.Empty<BattleDiagnosticRuntimeObject>()
                : new List<BattleDiagnosticRuntimeObject>(items);
            BackfillAttemptCount = backfillAttemptCount;
            BackfillFailureCount = backfillFailureCount;
            LastBackfillFrame = lastBackfillFrame;
            Summary = BattleDiagnosticRuntimeObjectCatalogSummary.Create(
                Items,
                truncated,
                backfillAttemptCount,
                backfillFailureCount,
                lastBackfillFrame);
        }

        public BattleDiagnosticSessionScope Scope { get; }
        public long Revision { get; }
        public bool Truncated { get; }
        public IReadOnlyList<BattleDiagnosticRuntimeObject> Items { get; }
        public long BackfillAttemptCount { get; }
        public long BackfillFailureCount { get; }
        public int LastBackfillFrame { get; }
        public bool HasBackfillFailures => BackfillFailureCount > 0L;
        public BattleDiagnosticRuntimeObjectCatalogSummary Summary { get; }
        public BattleDiagnosticDataCompleteness Completeness => Summary.Completeness;

        public bool TryResolve(
            in BattleDiagnosticRuntimeObjectReference reference,
            int frame,
            out BattleDiagnosticRuntimeObject runtimeObject)
        {
            if (!reference.HasRuntimeId)
            {
                runtimeObject = default;
                return false;
            }

            for (var i = Items.Count - 1; i >= 0; i--)
            {
                var candidate = Items[i];
                if (candidate.Kind != reference.Kind ||
                    candidate.RuntimeId != reference.RuntimeId) continue;
                if (reference.IsResolved)
                {
                    if (candidate.Generation != reference.Generation) continue;
                }
                else
                {
                    if (candidate.CreatedFrame >= 0 && frame >= 0 && frame < candidate.CreatedFrame)
                        continue;
                    if (candidate.DestroyedFrame >= 0 && frame >= 0 && frame > candidate.DestroyedFrame)
                        continue;
                }

                runtimeObject = candidate;
                return true;
            }

            runtimeObject = default;
            return false;
        }

        public static BattleDiagnosticObjectCatalogSnapshot Empty(BattleDiagnosticSessionScope scope)
        {
            return new BattleDiagnosticObjectCatalogSnapshot(
                scope,
                0L,
                false,
                Array.Empty<BattleDiagnosticRuntimeObject>());
        }
    }
}
