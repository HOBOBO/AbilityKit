using System;

namespace AbilityKit.Game.Flow
{
    public enum PresentationStackPolicy { Independent = 0, ReplaceSameKey = 1, RejectSameKey = 2 }
    public enum PresentationStopPolicy { Immediate = 0, Drain = 1 }
    public enum PresentationTargetLossPolicy { BackendDefault = 0, Stop = 1, Detach = 2, Rebind = 3 }
    public enum PresentationCompletionPolicy { Duration = 0, ResourceOrDuration = 1, Manual = 2 }

    /// <summary>Engine independent instance lifetime intent. Zero values preserve legacy backend behavior.</summary>
    public readonly struct PresentationLifecyclePolicy
    {
        public PresentationLifecyclePolicy(
            PresentationStackPolicy stack = PresentationStackPolicy.Independent,
            PresentationStopPolicy stop = PresentationStopPolicy.Immediate,
            PresentationTargetLossPolicy targetLoss = PresentationTargetLossPolicy.BackendDefault,
            PresentationCompletionPolicy completion = PresentationCompletionPolicy.Duration,
            int stopGraceMs = 0)
        {
            Stack = Enum.IsDefined(typeof(PresentationStackPolicy), stack) ? stack : PresentationStackPolicy.Independent;
            Stop = Enum.IsDefined(typeof(PresentationStopPolicy), stop) ? stop : PresentationStopPolicy.Immediate;
            TargetLoss = Enum.IsDefined(typeof(PresentationTargetLossPolicy), targetLoss) ? targetLoss : PresentationTargetLossPolicy.BackendDefault;
            Completion = Enum.IsDefined(typeof(PresentationCompletionPolicy), completion) ? completion : PresentationCompletionPolicy.Duration;
            StopGraceMs = Math.Max(0, stopGraceMs);
        }

        public PresentationStackPolicy Stack { get; }
        public PresentationStopPolicy Stop { get; }
        public PresentationTargetLossPolicy TargetLoss { get; }
        public PresentationCompletionPolicy Completion { get; }
        public int StopGraceMs { get; }
        public int EffectiveStopGraceMs => StopGraceMs > 0 ? StopGraceMs : 3000;
    }

    /// <summary>Optional backend capability; unsupported resources fall back to the configured duration.</summary>
    public interface IPresentationLifetimeAdapter<TVisual>
    {
        bool TryIsComplete(TVisual visual, out bool complete);
        bool TryBeginDrain(TVisual visual);
    }

    /// <summary>Tracks only lifecycle state, never gameplay state. Drain timeout uses unscaled seconds.</summary>
    public sealed class PresentationLifetimeState
    {
        private float _drainSeconds;
        public bool IsStopping { get; private set; }
        public bool IsDetached { get; private set; }
        public bool IsFinished { get; private set; }

        public bool RequestStop(in PresentationLifecyclePolicy policy, bool canDrain, bool force = false)
        {
            if (IsFinished) return true;
            if (IsStopping && !force) return false;
            if (force || policy.Stop == PresentationStopPolicy.Immediate || !canDrain)
                return IsFinished = true;
            // Repeated stop notifications cannot extend the tail.
            IsStopping = true;
            return false;
        }

        public bool OnTargetMissing(in PresentationLifecyclePolicy policy)
        {
            if (IsDetached) return false;
            if (policy.TargetLoss == PresentationTargetLossPolicy.Detach) IsDetached = true;
            return policy.TargetLoss == PresentationTargetLossPolicy.Stop;
        }

        public bool Tick(in PresentationLifecyclePolicy policy, float unscaledDeltaSeconds,
            bool durationComplete, bool resourceComplete)
        {
            if (unscaledDeltaSeconds < 0f || float.IsNaN(unscaledDeltaSeconds) || float.IsInfinity(unscaledDeltaSeconds))
                throw new ArgumentOutOfRangeException(nameof(unscaledDeltaSeconds));
            if (IsFinished) return true;
            if (IsStopping)
            {
                _drainSeconds += unscaledDeltaSeconds;
                return IsFinished = resourceComplete || _drainSeconds >= policy.EffectiveStopGraceMs / 1000f;
            }
            if (policy.Completion == PresentationCompletionPolicy.Manual) return false;
            return IsFinished = durationComplete ||
                (policy.Completion == PresentationCompletionPolicy.ResourceOrDuration && resourceComplete);
        }
    }
}
