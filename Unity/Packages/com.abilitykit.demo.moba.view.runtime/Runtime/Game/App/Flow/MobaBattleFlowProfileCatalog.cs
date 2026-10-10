using System;
using System.Collections.Generic;
using AbilityKit.Game.Battle.Shared.Assets;
using UnityEngine;

namespace AbilityKit.Game.Flow
{
    [Serializable]
    internal sealed class MobaBattleFlowProfileDocument
    {
        public string DefaultProfileId = string.Empty;
        public MobaBattleFlowProfile[] Profiles = Array.Empty<MobaBattleFlowProfile>();
    }

    internal sealed class MobaBattleFlowProfileCatalog
    {
        public const string ResourcePath = "moba/battle_flow_profiles";
        private static MobaBattleFlowProfileCatalog _default;
        private readonly Dictionary<string, MobaBattleFlowProfile> _profiles =
            new Dictionary<string, MobaBattleFlowProfile>(StringComparer.Ordinal);
        private readonly string _defaultProfileId;

        private MobaBattleFlowProfileCatalog(MobaBattleFlowProfileDocument document)
        {
            if (document?.Profiles == null || document.Profiles.Length == 0 ||
                string.IsNullOrWhiteSpace(document.DefaultProfileId))
                throw new InvalidOperationException("Battle flow profile catalog is empty or has no default.");

            foreach (var profile in document.Profiles)
            {
                if (profile == null) throw new InvalidOperationException("Battle flow profile is null.");
                profile.ValidateOrThrow();
                if (!_profiles.TryAdd(profile.ProfileId, profile))
                    throw new InvalidOperationException("Duplicate battle flow profile: " + profile.ProfileId);
            }
            if (!_profiles.ContainsKey(document.DefaultProfileId))
                throw new InvalidOperationException("Default battle flow profile is missing: " + document.DefaultProfileId);
            _defaultProfileId = document.DefaultProfileId;
        }

        public static MobaBattleFlowProfileCatalog Default =>
            _default ??= Load(ResourcesAssetProvider.Shared);

        public MobaBattleFlowProfile DefaultProfile => _profiles[_defaultProfileId];

        public MobaBattleFlowProfile Get(string profileId) =>
            _profiles.TryGetValue(profileId, out var profile)
                ? profile
                : throw new InvalidOperationException("Battle flow profile is missing: " + profileId);

        public static MobaBattleFlowProfileCatalog Load(IAssetProvider provider)
        {
            if (provider == null) throw new ArgumentNullException(nameof(provider));
            var asset = provider.Load<TextAsset>(ResourcePath);
            if (asset == null) throw new InvalidOperationException("Missing battle flow profiles: " + ResourcePath);
            return Parse(asset.text);
        }

        public static MobaBattleFlowProfileCatalog Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) throw new ArgumentException("Battle flow profiles are empty.", nameof(json));
            return new MobaBattleFlowProfileCatalog(JsonUtility.FromJson<MobaBattleFlowProfileDocument>(json));
        }
    }
}
