using AbilityKit.ActionEditorImpl;
using AbilityKit.Game.Flow;
using AbilityKit.Game.Platform.Unity.Presentation;
using ActionEditorExample;
using NBC.ActionEditor;
using UnityEngine;

namespace AbilityKit.Demo.Moba.Editor
{
    /// <summary>MOBA specialization discovered by ActionEditor's subclass selection.</summary>
    [CustomPreview(typeof(PlayParticle))]
    public class MobaPlayParticlePreview : PlayParticlePreview
    {
        private PresentationRequest _request;
        private bool _hasTemplate;
        private Transform _target;
        private IPresentationOrderingAdapter<GameObject> _ordering;
        protected virtual Transform SourceTransform => ModelSampler.EditModel?.transform;
        protected override float SimulationSpeed => _hasTemplate ? _request.Playback.Speed : 1f;
        protected virtual MobaPresentationTemplateResolver CreateTemplateResolver() => new MobaPresentationTemplateResolver();
        protected virtual GameObject LoadResource(string key) => Resources.Load<GameObject>(key);
        protected virtual IPresentationOrderingAdapter<GameObject> CreateOrderingAdapter() => new UnityPresentationOrderingAdapter();

        protected override void ConfigureParticle(ParticleSystem system)
        {
            if (!_hasTemplate) return;
            // Runtime overrides prefab simulationSpeed with the resolved request rate.
            // Preview applies that rate to the sample cursor, exactly once.
            var main = system.main;
            main.simulationSpeed = 1f;
        }

        protected override GameObject LoadPrefab()
        {
            _hasTemplate = false;
            if (clip.presentationTemplateId <= 0) return base.LoadPrefab();
            // A positive editor-local identity represents the sampled model. Zero
            // means no actor under the runtime attachment contract.
            if (!CreateTemplateResolver().TryCreateRequest(clip.presentationTemplateId, "actioneditor.preview",
                    1, Mathf.RoundToInt(clip.GetLength() * 1000f), 0f, out _request))
            {
                Debug.LogWarning($"ActionEditor: presentation template {clip.presentationTemplateId} could not be resolved.");
                return null;
            }
            var prefab = LoadResource(_request.ResourceKey);
            if (prefab == null)
            {
                Debug.LogWarning($"ActionEditor: presentation resource '{_request.ResourceKey}' was not found.");
                return null;
            }
            _hasTemplate = true;
            _ordering = CreateOrderingAdapter();
            return prefab;
        }

        protected override void PlaceEffect()
        {
            if (!_hasTemplate) return;
            _target = _request.Attachment.IsAttached
                ? UnityPresentationTransformUtility.ResolveSocket(SourceTransform, _request.Attachment) : null;
            var position = _request.Position;
            EffectObject.transform.SetPositionAndRotation(new Vector3(position.X, position.Y, position.Z), Quaternion.identity);
            ApplyPlacement();
        }

        protected override void UpdatePlacement()
        {
            if (_hasTemplate && _request.FollowAttachment && _request.Attachment.IsAttached)
            {
                // Keep editor ownership independent of the model hierarchy.
                _target = UnityPresentationTransformUtility.ResolveSocket(SourceTransform, _request.Attachment);
                if (_target != null) ApplyPlacement();
            }
        }

        private void ApplyPlacement()
        {
            if (_target != null)
                EffectObject.transform.SetPositionAndRotation(
                    UnityPresentationTransformUtility.ResolvePosition(_target, _request.Attachment), _target.rotation);
            var radius = _request.Radius > 0f && !float.IsInfinity(_request.Radius) ? _request.Radius : 1f;
            var inherited = _target != null && _request.FollowAttachment &&
                _request.ParentScaleMode == PresentationParentScaleMode.Inherit ? _target.lossyScale : Vector3.one;
            UnityPresentationTransformUtility.ApplyWorldScale(EffectObject.transform, BaseScale * (_request.Scale * radius), inherited);
            _ordering.Apply(EffectObject, _target != null ? _target.gameObject : null, _request.Ordering);
        }

        public override void Dispose()
        {
            base.Dispose();
            _target = null;
            _ordering = null;
            _hasTemplate = false;
        }
    }
}
