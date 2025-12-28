using System;
using Newtonsoft.Json;
using UnityEngine;

namespace AbilityKit.Ability.Impl.BattleDemo.Moba.Config
{
    public sealed class ResourcesJsonMobaConfigSource : IMobaConfigSource
    {
        private readonly string _resourcesDir;

        public ResourcesJsonMobaConfigSource(string resourcesDir)
        {
            if (string.IsNullOrEmpty(resourcesDir)) throw new ArgumentException(nameof(resourcesDir));
            _resourcesDir = resourcesDir;
        }

        public MobaConfigSnapshot Load()
        {
            return new MobaConfigSnapshot
            {
                Characters = LoadArray<CharacterDTO>(Combine(MobaConfigPaths.CharactersFile)),
                Skills = LoadArray<SkillDTO>(Combine(MobaConfigPaths.SkillsFile)),
                AttributeTemplates = LoadArray<BattleAttributeTemplateDTO>(Combine(MobaConfigPaths.AttributeTemplatesFile)),
                Models = LoadArray<ModelDTO>(Combine(MobaConfigPaths.ModelsFile))
            };
        }

        private string Combine(string fileWithoutExt)
        {
            return string.IsNullOrEmpty(_resourcesDir) ? fileWithoutExt : $"{_resourcesDir}/{fileWithoutExt}";
        }

        private static T[] LoadArray<T>(string resourcesPath)
        {
            var asset = Resources.Load<TextAsset>(resourcesPath);
            if (asset == null)
            {
                throw new InvalidOperationException($"Config json not found in Resources: {resourcesPath}");
            }

            var json = asset.text;
            if (string.IsNullOrEmpty(json))
            {
                throw new InvalidOperationException($"Config json is empty: {resourcesPath}");
            }

            try
            {
                var arr = JsonConvert.DeserializeObject<T[]>(json);
                return arr ?? Array.Empty<T>();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to parse config json: {resourcesPath}", ex);
            }
        }
    }
}
