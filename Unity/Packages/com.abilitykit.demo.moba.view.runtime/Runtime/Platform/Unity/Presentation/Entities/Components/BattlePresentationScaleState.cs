using AbilityKit.Game.Flow;
using UnityEngine;

namespace AbilityKit.Game.Battle.Component
{
    /// <summary>
    /// Stable scale state for a presentation object. It keeps the base model
    /// scale separate from logical entity scale and named presentation sources.
    /// </summary>
    public sealed class BattlePresentationScaleState : MonoBehaviour
    {
        private PresentationScaleResolver _sources = new PresentationScaleResolver();

        [SerializeField] private Vector3 _baseModelScale = Vector3.one;
        [SerializeField] private Vector3 _logicalEntityScale = Vector3.one;
        private bool _hasBaseModelScale;
        public Vector3 EffectiveScale { get; private set; } = Vector3.one;

        public PresentationScaleResolver Sources => _sources;
        public Vector3 BaseModelScale => _baseModelScale;
        public Vector3 LogicalEntityScale => _logicalEntityScale;

        /// <summary>
        /// Binds this replaceable GameObject component to the persistent source table
        /// owned by its logical view handle.
        /// </summary>
        public void BindSources(PresentationScaleResolver sources)
        {
            if (sources != null) _sources = sources;
        }

        public void CaptureBaseModelScale(Vector3 scale)
        {
            _baseModelScale = Sanitize(scale, Vector3.one);
            _hasBaseModelScale = true;
        }

        public void SetLogicalEntityScale(Vector3 scale)
        {
            _logicalEntityScale = Sanitize(scale, Vector3.one);
        }

        public void Apply(GameObject target, PresentationScaleChannel channels)
        {
            if (target == null) return;
            if (!_hasBaseModelScale) CaptureBaseModelScale(target.transform.localScale);

            var sourceScale = _sources.Resolve(channels, PresentationVector3.One);
            EffectiveScale = Multiply(
                Multiply(_baseModelScale, _logicalEntityScale),
                new Vector3(sourceScale.X, sourceScale.Y, sourceScale.Z));
            target.transform.localScale = EffectiveScale;
        }

        public void ApplyLogicalScale(GameObject target, Vector3 logicalScale)
        {
            SetLogicalEntityScale(logicalScale);
            Apply(target, PresentationScaleChannel.Avatar | PresentationScaleChannel.Model);
        }

        public void SetSource(
            GameObject target,
            in PresentationScaleSource source,
            PresentationScaleChannel channels = PresentationScaleChannel.Avatar | PresentationScaleChannel.Model)
        {
            if (!_hasBaseModelScale && target != null) CaptureBaseModelScale(target.transform.localScale);
            _sources.Set(in source);
            Apply(target, channels);
        }

        public bool RemoveSource(
            GameObject target,
            string sourceId,
            PresentationScaleChannel channels = PresentationScaleChannel.Avatar | PresentationScaleChannel.Model)
        {
            var removed = _sources.Remove(sourceId);
            if (removed) Apply(target, channels);
            return removed;
        }

        public static void ApplyCueScale(GameObject target, float scale, float radius)
        {
            if (target == null) return;
            var state = target.GetComponent<BattlePresentationScaleState>();
            if (state == null) state = target.AddComponent<BattlePresentationScaleState>();

            var requestScale = scale > 0f && !float.IsInfinity(scale) && !float.IsNaN(scale) ? scale : 1f;
            state.SetSource(
                target,
                new PresentationScaleSource("cue.request", PresentationScaleChannel.Vfx | PresentationScaleChannel.Cue, requestScale),
                PresentationScaleChannel.Vfx | PresentationScaleChannel.Cue);

            var radiusScale = radius > 0f && !float.IsInfinity(radius) && !float.IsNaN(radius) ? radius : 1f;
            state.SetSource(
                target,
                new PresentationScaleSource("cue.radius", PresentationScaleChannel.Cue, radiusScale),
                PresentationScaleChannel.Vfx | PresentationScaleChannel.Cue);
        }

        public void SetSource(in PresentationScaleSource source)
        {
            _sources.Set(in source);
        }

        public bool RemoveSource(string sourceId)
        {
            return _sources.Remove(sourceId);
        }

        private static Vector3 Multiply(Vector3 a, Vector3 b)
        {
            return new Vector3(a.x * b.x, a.y * b.y, a.z * b.z);
        }

        private static Vector3 Sanitize(Vector3 value, Vector3 fallback)
        {
            return IsFiniteNonNegative(value.x) && IsFiniteNonNegative(value.y) && IsFiniteNonNegative(value.z)
                ? value
                : fallback;
        }

        private static bool IsFiniteNonNegative(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;
        }
    }
}
