using System;
using System.Collections.Generic;
using AbilityKit.Demo.Moba.Share.Config;
using AbilityKit.Game.Flow;

namespace AbilityKit.Game.Battle.Vfx
{
    public sealed class VfxDatabase
    {
        private readonly Dictionary<int, VfxDTO> _byId;

        public VfxDatabase(Dictionary<int, VfxDTO> byId)
        {
            _byId = byId ?? throw new ArgumentNullException(nameof(byId));
        }

        public bool TryGet(int id, out VfxDTO dto)
        {
            return _byId.TryGetValue(id, out dto);
        }

        public bool TryGetPolicy(int id, out PresentationEffectPolicy policy)
        {
            policy = default;
            if (!TryGet(id, out var dto) || dto == null) return false;
            policy = new PresentationEffectPolicy(
                groupKey: dto.PolicyGroup,
                unique: dto.Unique,
                minIntervalMs: dto.MinIntervalMs,
                maxActive: dto.MaxActive);
            return policy.IsConfigured;
        }

        public bool TryFindIdByResource(string resourceKey, out int id)
        {
            id = 0;
            if (string.IsNullOrEmpty(resourceKey)) return false;
            foreach (var pair in _byId)
            {
                var dto = pair.Value;
                if (dto == null) continue;
                var resource = string.IsNullOrEmpty(dto.ResourceKey) ? dto.Resource : dto.ResourceKey;
                if (!string.Equals(resource, resourceKey, StringComparison.Ordinal)) continue;
                id = pair.Key;
                return true;
            }
            return false;
        }

        public bool TryCreateRequest(
            int id,
            string logicalKey,
            PresentationVector3 position,
            out PresentationRequest request,
            PresentationAttachment attachment = default,
            PresentationRotation rotation = default,
            float scale = 1f)
        {
            request = default;
            if (!TryGet(id, out var dto) || dto == null) return false;
            var resource = string.IsNullOrEmpty(dto.ResourceKey) ? dto.Resource : dto.ResourceKey;
            var kind = Enum.IsDefined(typeof(PresentationAssetKind), dto.AssetKind)
                ? (PresentationAssetKind)dto.AssetKind
                : PresentationAssetKind.Prefab;
            var playback = new PresentationPlaybackSpec(
                clipKey: dto.ClipKey,
                loop: dto.Loop,
                speed: dto.Speed,
                playCount: dto.PlayCount,
                delayMs: dto.DelayMs,
                intervalMs: dto.IntervalMs);
            var policy = new PresentationEffectPolicy(
                groupKey: dto.PolicyGroup,
                unique: dto.Unique,
                minIntervalMs: dto.MinIntervalMs,
                maxActive: dto.MaxActive);
            request = new PresentationRequest(
                logicalKey ?? ("vfx:" + id),
                assetId: id,
                position: position,
                rotation: rotation,
                attachment: attachment,
                durationMs: dto.DurationMs,
                scale: scale,
                resourceKey: resource,
                assetKind: kind,
                playback: playback,
                policy: policy);
            return request.IsValid;
        }

        public IReadOnlyList<string> Validate()
        {
            var errors = new List<string>();
            foreach (var pair in _byId)
            {
                var dto = pair.Value;
                if (dto == null) { errors.Add("VFX " + pair.Key + " is null."); continue; }
                var resource = string.IsNullOrEmpty(dto.ResourceKey) ? dto.Resource : dto.ResourceKey;
                if (!Enum.IsDefined(typeof(PresentationAssetKind), dto.AssetKind))
                    errors.Add("VFX " + dto.Id + " has an unknown AssetKind: " + dto.AssetKind + ".");
                if (dto.AssetKind == (int)PresentationAssetKind.Animation && string.IsNullOrWhiteSpace(dto.ClipKey))
                    errors.Add("VFX " + dto.Id + " is an animation without ClipKey.");
                if (string.IsNullOrWhiteSpace(resource) && dto.AssetKind != 1)
                    errors.Add("VFX " + dto.Id + " has no ResourceKey.");
                if (dto.DurationMs < 0 || dto.DelayMs < 0 || dto.IntervalMs < 0 || dto.MinIntervalMs < 0 || dto.MaxActive < 0)
                    errors.Add("VFX " + dto.Id + " contains a negative timing or policy value.");
                if (dto.PlayCount < 0) errors.Add("VFX " + dto.Id + " has a negative PlayCount.");
            }
            return errors;
        }

        public static VfxDatabase FromEntries(IEnumerable<VfxDTO> entries)
        {
            var dict = new Dictionary<int, VfxDTO>();
            if (entries != null)
            {
                foreach (var dto in entries)
                {
                    if (dto == null || dto.Id <= 0) continue;
                    dict[dto.Id] = dto;
                }
            }

            return new VfxDatabase(dict);
        }
    }
}
