using System;
using AbilityKit.Game.Battle.Shared.Assets;
using UnityEngine;

namespace AbilityKit.Game.Flow
{
    internal static class MobaLobbyRuntimeOptionsCatalog
    {
        internal const string ResourcePath = "moba/lobby_runtime_options";
        private static MobaLobbyRuntimeOptions _default;

        internal static MobaLobbyRuntimeOptions Default => _default ??= Load(ResourcesAssetProvider.Shared);

        internal static MobaLobbyRuntimeOptions Load(IAssetProvider provider)
        {
            if (provider == null) throw new ArgumentNullException(nameof(provider));
            var asset = provider.Load<TextAsset>(ResourcePath);
            if (asset == null) throw new InvalidOperationException("Missing lobby runtime options: " + ResourcePath);
            return Parse(asset.text);
        }

        internal static MobaLobbyRuntimeOptions Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new ArgumentException("Lobby runtime options are empty.", nameof(json));
            var options = JsonUtility.FromJson<MobaLobbyRuntimeOptions>(json);
            if (options == null)
                throw new InvalidOperationException("Lobby runtime options are invalid.");
            options.ValidateOrThrow();
            return options;
        }
    }
}
