using System;
using System.Collections.Generic;

namespace AbilityKit.Game.Flow
{
    public sealed class GameplayPresentationProfile
    {
        public GameplayPresentationProfile(int gameplayId, string hudModuleId,
            string projectionModuleId, string settlementModuleId, string[] assetPaths = null)
        {
            GameplayId = gameplayId;
            HudModuleId = hudModuleId;
            ProjectionModuleId = projectionModuleId;
            SettlementModuleId = settlementModuleId;
            AssetPaths = Array.AsReadOnly(assetPaths == null
                ? Array.Empty<string>() : (string[])assetPaths.Clone());
        }

        public int GameplayId { get; }
        public string HudModuleId { get; }
        public string ProjectionModuleId { get; }
        public string SettlementModuleId { get; }
        public IReadOnlyList<string> AssetPaths { get; }
    }

    public sealed class GameplayPresentationProfileCatalog
    {
        public const int DefaultGameplayId = 1;
        private readonly Dictionary<int, GameplayPresentationProfile> _byGameplayId =
            new Dictionary<int, GameplayPresentationProfile>();

        public GameplayPresentationProfileCatalog(IEnumerable<GameplayPresentationProfile> profiles)
        {
            if (profiles == null) throw new ArgumentNullException(nameof(profiles));
            foreach (var profile in profiles)
            {
                if (profile == null || profile.GameplayId <= 0 ||
                    string.IsNullOrWhiteSpace(profile.HudModuleId) ||
                    string.IsNullOrWhiteSpace(profile.ProjectionModuleId) ||
                    string.IsNullOrWhiteSpace(profile.SettlementModuleId) ||
                    !_byGameplayId.TryAdd(profile.GameplayId, profile))
                    throw new InvalidOperationException("Invalid or duplicate gameplay presentation profile.");

                foreach (var path in profile.AssetPaths)
                    if (string.IsNullOrWhiteSpace(path))
                        throw new InvalidOperationException("Gameplay presentation asset path is empty.");
            }
            if (_byGameplayId.Count == 0)
                throw new InvalidOperationException("Gameplay presentation profile catalog is empty.");
        }

        public IEnumerable<GameplayPresentationProfile> Profiles => _byGameplayId.Values;

        public GameplayPresentationProfile Get(int gameplayId)
        {
            if (!_byGameplayId.TryGetValue(gameplayId, out var profile))
                throw new InvalidOperationException("Gameplay presentation profile is missing: " + gameplayId);
            return profile;
        }
    }
}
