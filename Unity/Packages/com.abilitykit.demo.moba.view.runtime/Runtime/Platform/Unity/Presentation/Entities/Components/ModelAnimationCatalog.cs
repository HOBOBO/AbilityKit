using System;
using System.Collections.Generic;
using AbilityKit.Ability.Config;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AbilityKit.Game.Battle.Component
{
    /// <summary>Imported model animation facts, independent of character states and skills.</summary>
    public sealed class ModelAnimationCatalog
    {
        public const string ResourcePath = "moba/model_animations";
        private readonly Dictionary<int, Dictionary<string, Entry>> _models =
            new Dictionary<int, Dictionary<string, Entry>>();

        public sealed class Entry
        {
            [JsonProperty] public int Id { get; private set; }
            [JsonProperty] public int ModelId { get; private set; }
            [JsonProperty] public string ActionKey { get; private set; }
            [JsonProperty] public string AnimatorState { get; private set; }
            [JsonProperty] public int FrameCount { get; private set; }
            [JsonProperty] public decimal FrameRate { get; private set; }
            [JsonProperty] public int Layer { get; private set; }
            [JsonIgnore] public decimal DurationSeconds => FrameCount / FrameRate;
        }

        public static ModelAnimationCatalog Load(ITextAssetLoader loader) =>
            loader != null && loader.TryLoadText(ResourcePath, out var json)
                ? LoadJson(json) : new ModelAnimationCatalog();

        public static ModelAnimationCatalog LoadJson(string json)
        {
            var token = JToken.Parse(json, new JsonLoadSettings {
                DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
            if (!(token is JArray)) throw new InvalidOperationException("Model animations require an array.");
            var entries = token.ToObject<List<Entry>>(JsonSerializer.Create(new JsonSerializerSettings {
                MissingMemberHandling = MissingMemberHandling.Error }));
            var result = new ModelAnimationCatalog();
            var ids = new HashSet<int>();
            foreach (var entry in entries)
            {
                if (entry == null || entry.Id <= 0 || !ids.Add(entry.Id) || entry.ModelId <= 0 ||
                    string.IsNullOrWhiteSpace(entry.ActionKey) || entry.ActionKey != entry.ActionKey.Trim() ||
                    string.IsNullOrWhiteSpace(entry.AnimatorState) || entry.FrameCount <= 0 ||
                    entry.FrameRate <= 0 || entry.Layer < 0)
                    throw new InvalidOperationException("Invalid or duplicate model animation entry.");
                if (!result._models.TryGetValue(entry.ModelId, out var model))
                    result._models.Add(entry.ModelId, model = new Dictionary<string, Entry>(StringComparer.Ordinal));
                if (!model.TryAdd(entry.ActionKey, entry))
                    throw new InvalidOperationException($"Duplicate model animation: {entry.ModelId}/{entry.ActionKey}");
            }
            return result;
        }

        public Entry Get(int modelId, string key)
        {
            if (!_models.TryGetValue(modelId, out var model) || key == null || !model.TryGetValue(key, out var entry))
                throw new InvalidOperationException($"Missing model animation: {modelId}/{key}");
            return entry;
        }
    }
}
