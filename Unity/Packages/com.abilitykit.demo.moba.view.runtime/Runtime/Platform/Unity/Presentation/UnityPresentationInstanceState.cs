using AbilityKit.Game.Flow;
using UnityEngine;

namespace AbilityKit.Game.Platform.Unity.Presentation
{
    /// <summary>Read-only inspection surface for a live instance and its actual scale source ledger.</summary>
    [DisallowMultipleComponent]
    public sealed class UnityPresentationInstanceState : MonoBehaviour
    {
        public bool IsBound { get; private set; }
        public string Backend { get; private set; }
        public PresentationRequest Request { get; private set; }
        public Vector3 BaseScale { get; private set; }
        public PresentationScaleResolver Sources { get; private set; }
        public PresentationRateResolver RateSources { get; private set; }
        public PresentationRateResolution RateResolution { get; private set; }
        public PresentationScaleResolution ScaleResolution { get; private set; }
        public float EffectiveRate => RateResolution.EffectiveRate;
        public PresentationOrdering Ordering { get; private set; }
        public Transform AttachmentTarget { get; private set; }
        public Vector3 LastTargetScale { get; private set; } = Vector3.one;
        public bool IsDetached { get; private set; }
        public bool IsStopping { get; private set; }
        public bool RequestScaleInSources { get; private set; }
        internal bool HasPlacedAttachment;
        internal IPresentationOrderingAdapter<GameObject> OrderingAdapter;

        internal void Bind(string backend, in PresentationRequest request, Vector3 baseScale,
            PresentationScaleResolver sources, PresentationRateResolver rateSources, bool requestScaleInSources)
        {
            ResetForReuse();
            IsBound = true;
            Backend = backend;
            Request = request;
            BaseScale = baseScale;
            Sources = sources;
            RateSources = rateSources;
            RequestScaleInSources = requestScaleInSources;
            Ordering = request.Ordering;
        }

        internal void UpdateRequest(in PresentationRequest request)
        {
            Request = request;
            Ordering = request.Ordering;
        }

        internal void RefreshResolution(PresentationRateChannel rateChannel, PresentationScaleChannel scaleChannel)
        {
            RateResolution = RateSources == null ? default : RateSources.Explain(rateChannel);
            ScaleResolution = Sources == null ? default : Sources.Explain(scaleChannel, PresentationVector3.One);
        }

        internal void RecordTarget(Transform target, Vector3 scale)
        {
            AttachmentTarget = target;
            LastTargetScale = scale;
            HasPlacedAttachment = true;
        }

        internal void RecordLifetime(bool detached, bool stopping)
        {
            IsDetached = detached;
            IsStopping = stopping;
            if (detached) AttachmentTarget = null;
        }

        public void ResetForReuse()
        {
            IsBound = false;
            Backend = null;
            Request = default;
            Sources = null;
            RateSources = null;
            RateResolution = default;
            ScaleResolution = default;
            Ordering = default;
            AttachmentTarget = null;
            LastTargetScale = Vector3.one;
            HasPlacedAttachment = false;
            IsDetached = IsStopping = false;
            RequestScaleInSources = false;
            OrderingAdapter = null;
        }
    }
}
