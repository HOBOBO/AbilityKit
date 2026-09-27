using System;
using System.Collections.Generic;

namespace AbilityKit.Context
{
    /// <summary>
    /// Committed lifecycle changes exposed to optional context observers.
    /// </summary>
    public enum ContextLifecycleEventKind : byte
    {
        Created = 1,
        Ended = 2,
        PredictionRetracted = 3,
        Reconciled = 4,
        Cleared = 5,
    }

    /// <summary>
    /// Immutable notification emitted after authoritative context state has changed.
    /// </summary>
    public readonly struct ContextLifecycleEvent<TNode>
    {
        public ContextLifecycleEvent(
            ContextLifecycleEventKind kind,
            in TNode node,
            long revision,
            long predictionBoundary = 0L,
            bool isReplay = false)
        {
            Kind = kind;
            Node = node;
            Revision = revision;
            PredictionBoundary = predictionBoundary;
            IsReplay = isReplay;
        }

        public ContextLifecycleEventKind Kind { get; }
        public TNode Node { get; }
        public long Revision { get; }
        public long PredictionBoundary { get; }
        public bool IsReplay { get; }
    }

    public delegate void ContextLifecycleEventHandler<TNode>(
        in ContextLifecycleEvent<TNode> contextEvent);

    /// <summary>
    /// Read-only lifecycle source for optional projections such as tracing and diagnostics.
    /// Observers cannot veto or mutate the committed context operation.
    /// </summary>
    public interface IContextLifecycleSource<TNode>
    {
        long LifecycleRevision { get; }

        IReadOnlyList<TNode> CaptureLifecycleSnapshot();

        IDisposable Observe(
            ContextLifecycleEventHandler<TNode> observer,
            bool replayExisting = true);
    }
}
