using System;
using AbilityKit.Game.Battle.Shared.Assets;
using UnityEngine;

namespace AbilityKit.Game.Flow
{
    public static class UnityGameplayPresentationProfileCatalogLoader
    {
        public const string ResourcePath = "moba/gameplay_presentation_profiles";
        private static GameplayPresentationProfileCatalog _default;

        public static GameplayPresentationProfileCatalog Default =>
            _default ??= Load(ResourcesAssetProvider.Shared);

        public static GameplayPresentationProfileCatalog Load(IAssetProvider provider)
        {
            if (provider == null) throw new ArgumentNullException(nameof(provider));
            var asset = provider.Load<TextAsset>(ResourcePath);
            if (asset == null)
                throw new InvalidOperationException("Missing gameplay presentation profiles: " + ResourcePath);
            return Parse(asset.text);
        }

        public static GameplayPresentationProfileCatalog Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) throw new ArgumentException("Profile JSON is empty.", nameof(json));
            var document = JsonUtility.FromJson<ProfileDocument>(json);
            var source = document?.Profiles;
            if (source == null)
                throw new InvalidOperationException("Gameplay presentation profile catalog is empty.");

            var profiles = new GameplayPresentationProfile[source.Length];
            for (var i = 0; i < source.Length; i++)
            {
                var item = source[i];
                profiles[i] = item == null ? null : new GameplayPresentationProfile(
                    item.GameplayId, item.HudModuleId, item.ProjectionModuleId,
                    item.SettlementModuleId, item.AssetPaths);
            }
            return new GameplayPresentationProfileCatalog(profiles);
        }

        [Serializable]
        private sealed class ProfileDocument
        {
            public ProfileRecord[] Profiles = Array.Empty<ProfileRecord>();
        }

        [Serializable]
        private sealed class ProfileRecord
        {
            public int GameplayId;
            public string HudModuleId = string.Empty;
            public string ProjectionModuleId = string.Empty;
            public string SettlementModuleId = string.Empty;
            public string[] AssetPaths = Array.Empty<string>();
        }
    }
}
