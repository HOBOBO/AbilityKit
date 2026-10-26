using System;
using AbilityKit.Game.Battle.Shared.Assets;
using AbilityKit.Game.Battle.Vfx;
using AbilityKit.Demo.Moba.Share.Config;
using Newtonsoft.Json;
using UnityEngine;

namespace AbilityKit.Game.Platform.Unity.Presentation
{
    internal static class UnityVfxDatabaseLoader
    {
        public static VfxDatabase LoadFromResources(string path)
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentException(nameof(path));
            var asset = ResourcesAssetProvider.Shared.Load<TextAsset>(path);
            if (asset == null) throw new InvalidOperationException($"Vfx json not found in Resources: {path}");
            return LoadFromJson(asset.text, path);
        }

        public static VfxDatabase LoadFromJson(string json, string sourceName = null)
        {
            if (string.IsNullOrEmpty(json))
                throw new InvalidOperationException($"Vfx json is empty: {sourceName ?? "<memory>"}");
            var database = VfxDatabase.FromEntries(JsonConvert.DeserializeObject<VfxDTO[]>(json));
            var errors = database.Validate();
            if (errors.Count > 0)
                throw new InvalidOperationException(
                    "Invalid VFX configuration " + (sourceName ?? "<memory>") + ": " + string.Join("; ", errors));
            return database;
        }
    }
}
