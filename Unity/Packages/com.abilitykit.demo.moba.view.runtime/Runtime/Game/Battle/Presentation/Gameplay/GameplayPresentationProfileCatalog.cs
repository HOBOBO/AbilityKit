using System;
using System.Collections.Generic;
using AbilityKit.Game.Battle.Shared.Assets;
using UnityEngine;

namespace AbilityKit.Game.Flow
{
    [Serializable]
    public sealed class GameplayPresentationProfile
    {
        public int GameplayId;
        public string HudModuleId = string.Empty;
        public string ProjectionModuleId = string.Empty;
        public string SettlementModuleId = string.Empty;
        public string[] AssetPaths = Array.Empty<string>();
    }

    [Serializable]
    internal sealed class GameplayPresentationProfileDocument
    {
        public GameplayPresentationProfile[] Profiles = Array.Empty<GameplayPresentationProfile>();
    }

    public sealed class GameplayPresentationProfileCatalog
    {
        public const int DefaultGameplayId = 1;
        public const string ResourcePath = "moba/gameplay_presentation_profiles";
        private static GameplayPresentationProfileCatalog _default;
        private readonly Dictionary<int, GameplayPresentationProfile> _byGameplayId =
            new Dictionary<int, GameplayPresentationProfile>();

        private GameplayPresentationProfileCatalog(GameplayPresentationProfile[] profiles)
        {
            if (profiles == null || profiles.Length == 0)
                throw new InvalidOperationException("Gameplay presentation profile catalog is empty.");
            foreach (var profile in profiles)
            {
                if (profile == null || profile.GameplayId <= 0 ||
                    string.IsNullOrWhiteSpace(profile.HudModuleId) ||
                    string.IsNullOrWhiteSpace(profile.ProjectionModuleId) ||
                    string.IsNullOrWhiteSpace(profile.SettlementModuleId) ||
                    !_byGameplayId.TryAdd(profile.GameplayId, profile))
                    throw new InvalidOperationException("Invalid or duplicate gameplay presentation profile.");
                var paths = profile.AssetPaths ?? Array.Empty<string>();
                foreach (var path in paths)
                    if (string.IsNullOrWhiteSpace(path))
                        throw new InvalidOperationException("Gameplay presentation asset path is empty.");
            }
        }

        public static GameplayPresentationProfileCatalog Default =>
            _default ??= Load(ResourcesAssetProvider.Shared);

        public static GameplayPresentationProfileCatalog Load(IAssetProvider provider)
        {
            if (provider == null) throw new ArgumentNullException(nameof(provider));
            var asset = provider.Load<TextAsset>(ResourcePath);
            if (asset == null) throw new InvalidOperationException("Missing gameplay presentation profiles: " + ResourcePath);
            return Parse(asset.text);
        }

        public static GameplayPresentationProfileCatalog Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) throw new ArgumentException("Profile JSON is empty.", nameof(json));
            var document = JsonUtility.FromJson<GameplayPresentationProfileDocument>(json);
            return new GameplayPresentationProfileCatalog(document?.Profiles);
        }

        public GameplayPresentationProfile Get(int gameplayId)
        {
            if (!_byGameplayId.TryGetValue(gameplayId, out var profile))
                throw new InvalidOperationException("Gameplay presentation profile is missing: " + gameplayId);
            return profile;
        }

        internal void ValidateModules(GameplayPresentationModuleRegistry modules)
        {
            if (modules == null) throw new ArgumentNullException(nameof(modules));
            foreach (var profile in _byGameplayId.Values)
                modules.Validate(profile);
        }
    }
}
