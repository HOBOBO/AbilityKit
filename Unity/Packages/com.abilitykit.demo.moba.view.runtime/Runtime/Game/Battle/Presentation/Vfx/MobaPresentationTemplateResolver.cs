using System;
using AbilityKit.ActionSchema;
using AbilityKit.Demo.Moba.Share;
using AbilityKit.Demo.Moba.Share.Config;
using AbilityKit.Game.Flow.Battle.Presentation.Timeline;
using UnityEngine;

namespace AbilityKit.Game.Flow
{
    /// <summary>Resolves an ActionEditor presentation-template ID through the published MOBA tables.</summary>
    public sealed class MobaPresentationTemplateResolver : IMobaPresentationTemplateResolver
    {
        [Serializable] private sealed class TemplateRoot { public PresentationTemplateDTO[] Items; }
        [Serializable] private sealed class VfxRoot { public VfxDTO[] Items; }

        private readonly PresentationTemplateDTO[] _templates;
        private readonly VfxDTO[] _vfx;
        private long _castInstanceId;
        private CastPresentationFrame _castFrame;

        public void SetCastFrame(long castInstanceId, in CastPresentationFrame frame)
        {
            _castInstanceId = castInstanceId;
            _castFrame = frame;
        }

        public MobaPresentationTemplateResolver()
        {
            _templates = LoadArray<TemplateRoot, PresentationTemplateDTO>("luban/moba/presentation_templates", root => root.Items);
            _vfx = LoadArray<VfxRoot, VfxDTO>("vfx/vfx", root => root.Items);
        }

        public MobaPresentationTemplateResolver(PresentationTemplateDTO[] templates, VfxDTO[] vfx)
        {
            _templates = templates ?? Array.Empty<PresentationTemplateDTO>();
            _vfx = vfx ?? Array.Empty<VfxDTO>();
        }

        public bool TryCreateRequest(int templateId, string logicalKey, GroupDto group, int durationMs,
            float offsetSeconds, out PresentationRequest request)
            => TryCreateRequest(templateId, logicalKey, group != null ? group.actorId : 0, durationMs,
                offsetSeconds, out request);

        public bool TryCreateRequest(int templateId, string logicalKey, int actorId, int durationMs,
            float offsetSeconds, out PresentationRequest request)
        {
            request = default;
            var template = Find(_templates, templateId, item => item.Id);
            if (template == null) return false;
            var assetId = template.AssetId > 0 ? template.AssetId : template.Id;
            var vfx = Find(_vfx, assetId, item => item.Id);
            if (vfx == null) return false;

            var resource = !string.IsNullOrWhiteSpace(template.ResourceKey) ? template.ResourceKey :
                !string.IsNullOrWhiteSpace(vfx.ResourceKey) ? vfx.ResourceKey : vfx.Resource;
            if (string.IsNullOrWhiteSpace(resource)) return false;

            var attached = template.AttachMode != 0;
            var kind = attached && !string.IsNullOrEmpty(template.Socket)
                ? PresentationAttachmentKind.Socket
                : attached ? PresentationAttachmentKind.Actor : PresentationAttachmentKind.World;
            var position = new PresentationVector3(template.OffsetX, template.OffsetY, template.OffsetZ);
            var attachment = new PresentationAttachment(kind, actorId,
                template.Socket, position, Enum.IsDefined(typeof(PresentationOffsetSpace), template.OffsetSpace)
                    ? (PresentationOffsetSpace)template.OffsetSpace
                    : PresentationOffsetSpace.TargetLocal);
            var rotation = PresentationRotation.Identity;
            if (template.PlacementSpace == 1)
            {
                if (string.IsNullOrEmpty(logicalKey) || _castInstanceId <= 0 || !_castFrame.HasValue || !_castFrame.IsValid ||
                    !logicalKey.StartsWith("timeline:" + _castInstanceId + ":", StringComparison.Ordinal)) return false;
                var facing = Quaternion.LookRotation(new Vector3(_castFrame.ForwardX, _castFrame.ForwardY, _castFrame.ForwardZ));
                var worldPosition = new Vector3(_castFrame.OriginX, _castFrame.OriginY, _castFrame.OriginZ) +
                    facing * new Vector3(template.OffsetX, template.OffsetY, template.OffsetZ);
                position = new PresentationVector3(worldPosition.x, worldPosition.y, worldPosition.z);
                rotation = new PresentationRotation(facing.x, facing.y, facing.z, facing.w);
                attachment = new PresentationAttachment(PresentationAttachmentKind.World);
            }
            var duration = template.DefaultDurationMs > 0 ? template.DefaultDurationMs :
                vfx.DurationMs > 0 ? vfx.DurationMs : durationMs;
            var scale = template.Scale > 0f ? template.Scale : 1f;
            var playback = new PresentationPlaybackSpec(
                clipKey: vfx.ClipKey,
                loop: vfx.Loop,
                speed: vfx.Speed,
                playCount: vfx.PlayCount,
                delayMs: vfx.DelayMs,
                intervalMs: vfx.IntervalMs);
            var policy = new PresentationEffectPolicy(vfx.PolicyGroup, vfx.Unique, vfx.MinIntervalMs, vfx.MaxActive);
            request = new PresentationRequest(
                logicalKey,
                assetId,
                position,
                rotation: rotation,
                attachment: attachment,
                durationMs: duration,
                scale: scale,
                radius: template.Radius,
                resourceKey: resource,
                assetKind: Enum.IsDefined(typeof(PresentationAssetKind), vfx.AssetKind)
                    ? (PresentationAssetKind)vfx.AssetKind : PresentationAssetKind.Particle,
                playback: playback,
                policy: policy,
                followAttachment: template.PlacementSpace == 0 && template.Follow,
                parentScaleMode: Enum.IsDefined(typeof(PresentationParentScaleMode), template.ParentScaleMode)
                    ? (PresentationParentScaleMode)template.ParentScaleMode
                    : PresentationParentScaleMode.Inherit,
                ordering: new PresentationOrdering(
                    Enum.IsDefined(typeof(PresentationOrderingMode), template.OrderingMode)
                        ? (PresentationOrderingMode)template.OrderingMode
                        : PresentationOrderingMode.ResourceDefault,
                    template.OrderingPriority),
                lifecycle: ResolveLifecycle(template),
                templateId: template.Id);
            return request.IsValid;
        }

        /// <summary>Looks up the template ordering contract for a VFX asset used by Trigger/Cue playback.</summary>
        public bool TryResolveOrdering(int assetId, out PresentationOrdering ordering)
        {
            ordering = PresentationOrdering.Default;
            if (!TryResolveTemplate(0, assetId, out var template)) return false;
            ordering = new PresentationOrdering((PresentationOrderingMode)template.OrderingMode, template.OrderingPriority);
            return true;
        }

        public bool TryResolveTemplate(int templateId, int assetId, out PresentationTemplateDTO template)
        {
            template = null;
            if (templateId > 0)
            {
                var exact = Find(_templates, templateId, item => item.Id);
                if (exact == null || (exact.AssetId > 0 ? exact.AssetId : exact.Id) != assetId) return false;
                template = exact;
                return true;
            }
            // Legacy VFX-only cues can use a template only if the asset mapping is unambiguous.
            if (assetId <= 0 || _templates == null) return false;
            for (var i = 0; i < _templates.Length; i++)
            {
                var candidate = _templates[i];
                if (candidate == null || (candidate.AssetId > 0 ? candidate.AssetId : candidate.Id) != assetId) continue;
                if (template != null) { template = null; return false; }
                template = candidate;
            }
            return template != null;
        }

        public static PresentationLifecyclePolicy ResolveLifecycle(PresentationTemplateDTO template) =>
            template == null ? default : new PresentationLifecyclePolicy(
                (PresentationStackPolicy)template.StackPolicy, (PresentationStopPolicy)template.StopPolicy,
                (PresentationTargetLossPolicy)template.TargetLossPolicy,
                (PresentationCompletionPolicy)template.CompletionPolicy, template.StopGraceMs);

        private static T[] LoadArray<TRoot, T>(string path, Func<TRoot, T[]> select) where TRoot : class
        {
            var asset = Resources.Load<TextAsset>(path);
            if (asset == null) return Array.Empty<T>();
            var root = JsonUtility.FromJson<TRoot>("{\"Items\":" + asset.text + "}");
            return root == null ? Array.Empty<T>() : select(root) ?? Array.Empty<T>();
        }

        private static T Find<T>(T[] entries, int id, Func<T, int> getId) where T : class
        {
            if (entries == null) return null;
            for (var i = 0; i < entries.Length; i++)
                if (entries[i] != null && getId(entries[i]) == id) return entries[i];
            return null;
        }
    }
}
